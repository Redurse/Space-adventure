using System.Linq;
using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

public sealed partial class World
{
    private const float InteractionRadius = InteractionConstants.DeviceInteractionRadius; // distance to a periscope/storage/locker to interact with it
    private const float SuitActionDurationSeconds = 2f;

    // Single E key, priority-ordered: 0) ignored entirely while mid-equip/unequip (can't react
    // instantly, game_design.md section 2), 1) stand up if manning, 2) use a held MedKit if hurt
    // (game_design.md section 4, M12 - anywhere, no proximity needed, since it's self-treatment
    // not a station), 3) reload a turret if carrying a crate, 4) pick up an ammo crate at storage,
    // 5) repair a damaged turret/system or man a free turret, 6) start putting on/taking off a suit
    // at a locker, 7) weld the nearest breached hull block in the current room. Hand tools/tanks/
    // weapons/consumables live in the storage racks now (World.Storage.cs's TryMoveItem, opened by
    // clicking one), not a separate E-key pickup - see game_design.md section 13.
    private void HandleInteract(Character character)
    {
        if (character.SuitActionRemaining > 0)
            return;

        if (character.IsOutside)
        {
            HandleEvaInteract(character);
            return;
        }

        // A station room has none of the ship's fixtures in it - the only thing to interact with
        // is somebody else's property (World.StationCrime.cs).
        if (character.OnStation)
        {
            TryStealCrate(character);
            return;
        }

        if (character.ManningTurretId is { } manned)
        {
            _turretRuntimes[manned].MannedByPlayerId = null;
            character.ManningTurretId = null;
            return;
        }

        if (character.IsAtHelm)
        {
            character.IsAtHelm = false; // the last commanded thrust is deliberately left as-is (game_design.md Phase 3, M15)
            character.EngineerFocusDeviceId = null; // M57 - the whole helm screen (and its Engineer tab) closes with it
            ResetTimeAccelerationIfNobodyAtHelm(); // M57 - nobody left to react, don't keep racing ahead unsupervised
            return;
        }

        if (character.Inventory.IsHolding(ItemType.MedKit) && character.Health < Character.MaxHealth)
        {
            TryUseMedKit(character);
            return;
        }

        var nearbyTurret = Ship.Turrets.FirstOrDefault(t =>
            t.RoomId == character.RoomId &&
            (t.PeriscopePosition - character.Position).Length() < InteractionRadius);

        if (character.CarryingAmmoCrate)
        {
            if (nearbyTurret is null || nearbyTurret.WeaponType == TurretWeaponType.Laser)
                return; // laser turrets draw from the reactor, not a crate - Magnetic/MachineGun both do

            var runtime = _turretRuntimes[nearbyTurret.Id];
            runtime.AmmoRemaining = runtime.Definition.MagazineCapacity;
            character.Inventory.TryRemove(ItemType.AmmoCrate);
            return;
        }

        var nearbyStorage = Ship.AmmoStorages.FirstOrDefault(s =>
            s.RoomId == character.RoomId &&
            (s.Position - character.Position).Length() < InteractionRadius);

        if (nearbyStorage is not null)
        {
            TryTakeAmmoCrate(character, nearbyStorage);
            return;
        }

        if (nearbyTurret is not null)
        {
            var runtime = _turretRuntimes[nearbyTurret.Id];
            if (runtime.Damaged)
            {
                // Needs a repair tool actually held in hand — matches the game's tool/hand model.
                if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                    runtime.Damaged = false;
                return;
            }

            if (runtime.MannedByPlayerId is null)
            {
                runtime.MannedByPlayerId = character.PlayerId;
                character.ManningTurretId = nearbyTurret.Id;
            }
            return;
        }

        // Only intercepts a damaged block — a healthy one has nothing to do here, so F falls
        // through to whatever's next (e.g. welding a breach at the same spot).
        var nearbyDamagedSystem = Ship.SystemDevices.FirstOrDefault(d =>
            d.RoomId == character.RoomId &&
            (d.Position - character.Position).Length() < InteractionRadius &&
            !IsDeviceConnected(d.Id));

        if (nearbyDamagedSystem is not null)
        {
            // A held wrench/screwdriver no longer fixes it outright - it starts (or advances) the
            // repair minigame instead (World.SystemRepair.cs).
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                AttemptSystemRepair(nearbyDamagedSystem.Id);
            return;
        }

        // The reactor and its sibling "boxes" plus the helm/scanner consoles (enemy/weapon overhaul -
        // "реактор и коробки могли быть сломаны", "штурвал, сонар можно было сломать") - same
        // minigame, same plain bool Damaged state as a turret rather than a wire (World.SystemRepair.cs
        // already steps/finishes all five via RepairableBlockKinds/IsBlockBroken/SetBlockBroken).
        var nearbyBrokenBlock = RepairableBlockKinds
            .Select(RepairableBlock)
            .FirstOrDefault(b => IsBlockBroken(b.Kind) && (b.Position - character.Position).Length() < InteractionRadius);

        if (nearbyBrokenBlock is not null)
        {
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                AttemptSystemRepair(nearbyBrokenBlock.Id);
            return;
        }

        // Cosmoteer-style marching engines (direct user request) - the seized-throttle Control tile
        // repairs with the same minigame, found by proximity like everything else here.
        var nearbyBrokenEngineControl = Ship.Engines.FirstOrDefault(e =>
            e.RoomId == character.RoomId && IsEngineControlBroken(e.Id) &&
            (e.ControlPosition - character.Position).Length() < InteractionRadius);

        if (nearbyBrokenEngineControl is not null)
        {
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                AttemptSystemRepair(nearbyBrokenEngineControl.Id);
            return;
        }

        // A hull camera's own junction box (M48) - not a ShipSystemDevice (WireGraphFactory's own
        // comment explains why), so it needs this separate lookup, but the repair itself is the
        // exact same minigame every other device above uses.
        var nearbyDamagedCamera = Ship.Cameras.FirstOrDefault(c =>
            c.RoomId == character.RoomId &&
            (c.InteriorPosition - character.Position).Length() < InteractionRadius &&
            !IsDeviceConnected(c.Id));

        if (nearbyDamagedCamera is not null)
        {
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                AttemptSystemRepair(nearbyDamagedCamera.Id);
            return;
        }

        // Junction boxes ("щитки") are their own breakable device (game_design.md) - a damaged one
        // repairs the same way a SystemDevice does (wrench/screwdriver drives the minigame above,
        // at its own trunk wire this time - IsJunctionDamaged/RepairDeviceWiring both already
        // handle a Junction's id correctly with no change of their own). An undamaged one has
        // nothing left for F to do - it's a fixed fixture, not something the wrench relocates.
        var nearbyJunction = _components.FirstOrDefault(c =>
            c.Kind == ComponentKind.Junction &&
            c.RoomId == character.RoomId &&
            (c.Position - character.Position).Length() < InteractionRadius);

        if (nearbyJunction is not null && IsJunctionDamaged(nearbyJunction.Id))
        {
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                AttemptSystemRepair(nearbyJunction.Id);
            return;
        }

        // Doors have their own hit points now too (game_design.md) - only a destroyed one has
        // anything to do here (an intact one is opened/closed by clicking it, not F), repaired by
        // the same wrench/screwdriver-driven minigame as everything else above.
        var nearbyDestroyedDoor = AllShipDoors().FirstOrDefault(d =>
            d.Connects(character.RoomId) && (d.Position - character.Position).Length() < InteractionRadius && IsDoorDestroyed(d.Id));

        if (nearbyDestroyedDoor.Id is not null)
        {
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                AttemptSystemRepair(nearbyDestroyedDoor.Id);
            return;
        }

        // Content-каталог отсеков - an extra bridge room's own seat works exactly like the primary
        // HelmConsole for piloting (same ship-wide IsAtHelm/throttle, Ship.cs's own doc comment on
        // ExtraHelmConsoles), it's just never the one HelmConsoleBroken/repair targets.
        var atAnyHelm = (!HelmConsoleBroken && Ship.HelmConsole.RoomId == character.RoomId &&
                (Ship.HelmConsole.Position - character.Position).Length() < InteractionRadius)
            || Ship.ExtraHelmConsoles.Any(c => c.RoomId == character.RoomId && (c.Position - character.Position).Length() < InteractionRadius);
        if (atAnyHelm)
        {
            character.IsAtHelm = true;
            return;
        }

        var nearbyLocker = Ship.SuitLockers.FirstOrDefault(l =>
            l.RoomId == character.RoomId &&
            (l.Position - character.Position).Length() < InteractionRadius);

        if (nearbyLocker is not null)
        {
            // Each locker holds exactly one suit now (World.SuitLockers.cs) - taking one requires
            // this locker to actually have one, and putting one back requires this locker to be
            // the empty one to receive it, same as a ComponentMount only accepting an install into
            // a free socket.
            var equipping = !character.WearingSuit;
            if (equipping != SuitLockerHasSuit(nearbyLocker.Id))
                return;

            character.SuitActionRemaining = SuitActionDurationSeconds;
            character.SuitActionEquipping = equipping;
            character.SuitActionLockerId = nearbyLocker.Id;
            return;
        }
    }
}
