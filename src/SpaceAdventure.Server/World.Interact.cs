using System.Linq;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const float InteractionRadius = 1.0f; // units, distance to a periscope/storage/locker to interact with it
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
            if (nearbyTurret is null || nearbyTurret.WeaponType != TurretWeaponType.Ballistic)
                return; // laser turrets don't take ammo crates

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

        if (Ship.HelmConsole.RoomId == character.RoomId &&
            (Ship.HelmConsole.Position - character.Position).Length() < InteractionRadius)
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
