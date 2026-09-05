using System.Linq;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Click-based counterparts to HandleInteract's E-key branches (humble-soaring-cat.md - "Полный
// переход на клик как в Baro"). Each method below re-implements ONE existing branch's exact
// condition/effect, just keyed by an explicit id the client already highlighted/clicked instead of
// "nearest object in range" - same trust model as ComponentMountInteractId/SabotageDeviceId
// (World.cs's ApplyCommand): the server still re-checks room/distance/tool itself, the id only
// says WHICH candidate to check instead of searching for one. HandleInteract itself is left
// completely alone - E keeps working exactly as before, this is a parallel input path, not a
// replacement.
public sealed partial class World
{
    private void TrySuitLockerInteractById(Character character, string lockerId)
    {
        if (character.IsOutside || character.OnStation || character.SuitActionRemaining > 0)
            return;

        var locker = Ship.SuitLockers.FirstOrDefault(l => l.Id == lockerId);
        if (locker is null || locker.RoomId != character.RoomId ||
            (locker.Position - character.Position).Length() >= InteractionRadius)
            return;

        var equipping = !character.WearingSuit;
        if (equipping != SuitLockerHasSuit(locker.Id))
            return;

        character.SuitActionRemaining = SuitActionDurationSeconds;
        character.SuitActionEquipping = equipping;
        character.SuitActionLockerId = locker.Id;
    }

    private void TryTurretInteractById(Character character, string turretId)
    {
        if (character.IsOutside || character.OnStation || character.ManningTurretId is not null || character.IsAtHelm)
            return;

        var turret = Ship.Turrets.FirstOrDefault(t => t.Id == turretId);
        if (turret is null || turret.RoomId != character.RoomId ||
            (turret.PeriscopePosition - character.Position).Length() >= InteractionRadius)
            return;

        if (character.CarryingAmmoCrate)
        {
            if (turret.WeaponType == TurretWeaponType.Laser)
                return; // laser turrets draw from the reactor, not a crate - Magnetic/MachineGun both do

            var reloadRuntime = _turretRuntimes[turret.Id];
            reloadRuntime.AmmoRemaining = reloadRuntime.Definition.MagazineCapacity;
            character.Inventory.TryRemove(ItemType.AmmoCrate);
            return;
        }

        var runtime = _turretRuntimes[turret.Id];
        if (runtime.Damaged)
        {
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                runtime.Damaged = false;
            return;
        }

        if (runtime.MannedByPlayerId is null)
        {
            runtime.MannedByPlayerId = character.PlayerId;
            character.ManningTurretId = turret.Id;
        }
    }

    private void TryTakeAmmoCrateById(Character character, string storageId)
    {
        if (character.IsOutside || character.OnStation)
            return;

        var storage = Ship.AmmoStorages.FirstOrDefault(s => s.Id == storageId);
        if (storage is null || storage.RoomId != character.RoomId ||
            (storage.Position - character.Position).Length() >= InteractionRadius)
            return;

        TryTakeAmmoCrate(character, storage);
    }

    private void TryStealCrateById(Character character, string crateId)
    {
        if (!character.OnStation)
            return;

        var crate = Station.Crates.FirstOrDefault(c => c.Id == crateId);
        if (crate is null || crate.RoomId != character.RoomId || IsCrateLooted(crate.Id) ||
            (crate.Position - character.Position).Length() >= InteractionRadius)
            return;

        if (!character.Inventory.TryAdd(crate.Item))
            return; // reached it but had nowhere to put it

        _lootedCrateIds.Add(crate.Id);
        _stolenItemCount[character.PlayerId] = GetStolenItemCount(character.PlayerId) + 1;
    }

    // Widest of the five - the six repair categories HandleInteract's branches 9-14 each search
    // for separately (SystemDevice, one of the five RepairableBlockKinds "boxes", an engine's
    // Control tile, a hull camera, a Junction, a door) all end at the exact same
    // AttemptSystemRepair(id) call, so this just needs to find WHICH kind of thing this id names,
    // re-check the same room/distance/tool/damaged conditions that kind's branch already used, and
    // call it - no new repair logic, only a different lookup key.
    private void TryRepairDeviceById(Character character, string deviceId)
    {
        if (character.IsOutside || character.OnStation)
            return;
        var holdingTool = character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver);
        if (!holdingTool)
            return;

        var device = Ship.SystemDevices.FirstOrDefault(d => d.Id == deviceId);
        if (device is not null)
        {
            if (device.RoomId == character.RoomId && (device.Position - character.Position).Length() < InteractionRadius &&
                !IsDeviceConnected(device.Id))
                AttemptSystemRepair(device.Id);
            return;
        }

        var blockKind = RepairableBlockKinds.Cast<DeviceKind?>().FirstOrDefault(k => RepairableBlock(k!.Value).Id == deviceId);
        if (blockKind is { } kind)
        {
            var block = RepairableBlock(kind);
            if (block.RoomId == character.RoomId && (block.Position - character.Position).Length() < InteractionRadius &&
                IsBlockBroken(kind))
                AttemptSystemRepair(block.Id);
            return;
        }

        var engine = Ship.Engines.FirstOrDefault(e => e.Id == deviceId);
        if (engine is not null)
        {
            if (engine.RoomId == character.RoomId && IsEngineControlBroken(engine.Id) &&
                (engine.ControlPosition - character.Position).Length() < InteractionRadius)
                AttemptSystemRepair(engine.Id);
            return;
        }

        var camera = Ship.Cameras.FirstOrDefault(c => c.Id == deviceId);
        if (camera is not null)
        {
            if (camera.RoomId == character.RoomId && (camera.InteriorPosition - character.Position).Length() < InteractionRadius &&
                !IsDeviceConnected(camera.Id))
                AttemptSystemRepair(camera.Id);
            return;
        }

        var junction = _components.FirstOrDefault(c => c.Kind == ComponentKind.Junction && c.Id == deviceId);
        if (junction is not null)
        {
            if (junction.RoomId == character.RoomId && (junction.Position - character.Position).Length() < InteractionRadius &&
                IsJunctionDamaged(junction.Id))
                AttemptSystemRepair(junction.Id);
            return;
        }

        var door = AllShipDoors().FirstOrDefault(d => d.Id == deviceId);
        if (door.Id is not null)
        {
            if (door.Connects(character.RoomId) && (door.Position - character.Position).Length() < InteractionRadius &&
                IsDoorDestroyed(door.Id))
                AttemptSystemRepair(door.Id);
        }
    }
}
