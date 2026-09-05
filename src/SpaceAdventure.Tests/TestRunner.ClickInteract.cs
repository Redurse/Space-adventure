using System.Linq;
using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // humble-soaring-cat.md - "Полный переход на клик как в Baro": each new ClientCommand id field
    // has to reach the exact same effect its [E]-key twin already had (World.Interact.cs), just
    // addressed by id instead of "nearest in range". These mirror the existing E-key tests for the
    // same actions (World_AmmoStorage_*, World_Crime_StealCrate_*, World_RepairSystem_*) one for one.

    private static bool World_ClickInteract_SuitLocker_EquipsAndUnequips()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 20f, 3f); // "suit-locker-engine"

        world.ApplyCommand(1, new ClientCommand(1, SuitLockerInteractId: "suit-locker-engine"));
        var justStarted = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (justStarted.WearingSuit || justStarted.SuitActionRemaining <= 0)
            return false; // must actually start the timed equip action, not finish instantly

        // World_SuitAction_TakesTimeAndLocksMovement's own 70-step margin (10 + 60) past the 2s
        // duration - a bare 60 iterations of the float (1/30) decrement leaves a hair of positive
        // residual (rounding), so the action doesn't actually land until one iteration past that.
        for (var i = 0; i < 70; i++)
            world.Step(RealtimeStep);
        var equipped = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (!equipped.WearingSuit || equipped.SuitActionRemaining != 0f)
            return false;

        MoveCharacterTo(world, 1, 20f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, SuitLockerInteractId: "suit-locker-engine"));
        for (var i = 0; i < 70; i++)
            world.Step(RealtimeStep);
        var unequipped = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !unequipped.WearingSuit;
    }

    // Wrong id (a locker that isn't nearby) or too far from the right one must not fire at all -
    // same server-side re-check every other new id field gets (World.ClickInteract.cs).
    private static bool World_ClickInteract_SuitLocker_IgnoresUnreachableLocker()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor - far from either locker

        world.ApplyCommand(1, new ClientCommand(1, SuitLockerInteractId: "suit-locker-engine"));
        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).SuitActionRemaining == 0f;
    }

    private static bool World_ClickInteract_Turret_ReloadsAndMans()
    {
        var world = new World();
        world.SpawnCharacter(1);

        MoveCharacterTo(world, 1, 15f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, AmmoStorageInteractId: "ammo-storage-quarters")); // take a crate

        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, TurretInteractId: "turret-bow")); // reload via click

        var reloaded = world.CreateSnapshot();
        var turretAfterReload = reloaded.TurretStates.Single(t => t.Id == "turret-bow");
        var meAfterReload = reloaded.Characters.Single(c => c.PlayerId == 1);
        if (turretAfterReload.AmmoRemaining != turretAfterReload.MagazineCapacity || meAfterReload.CarryingAmmoCrate)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TurretInteractId: "turret-bow")); // man it via click
        return world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-bow").MannedByPlayerId == 1;
    }

    private static bool World_ClickInteract_AmmoStorage_TakesACrate()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 15f, 3f);

        world.ApplyCommand(1, new ClientCommand(1, AmmoStorageInteractId: "ammo-storage-quarters"));

        var snapshot = world.CreateSnapshot();
        var stock = snapshot.AmmoStorageStates.First(s => s.StorageId == "ammo-storage-quarters");
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        return me.CarryingAmmoCrate && stock.Remaining == World.AmmoStorageCapacity - 1;
    }

    private static bool World_ClickInteract_StealCrate_AddsItemAndMarksLooted()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        var crate = world.Station.Crates.First();
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, StealCrateId: crate.Id));

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.IsCrateLooted(crate.Id)
            && world.GetStolenItemCount(1) == 1
            && me.Inventory!.MainSlots.Contains(crate.Item);
    }

    // Mirrors World_RepairSystem_RequiresWrenchHeldInHand exactly, just via RepairDeviceId instead
    // of standing in reach and pressing [E] - same gradual-repair timer underneath either way.
    private static bool World_ClickInteract_RepairDeviceId_RepairsADamagedSystemDevice()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.CutWire("trunk-system-shields");

        WalkAcrossShipTo(world, 7.2f, 0.7f); // reactor room's shields device

        world.ApplyCommand(1, new ClientCommand(1, RepairDeviceId: "system-shields")); // no tool held - must not start
        var stillDamagedWithoutTool = !world.IsDeviceConnected("system-shields");

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));

        WalkAcrossShipTo(world, 7.2f, 0.7f);
        world.ApplyCommand(1, new ClientCommand(1, RepairDeviceId: "system-shields"));

        world.DebugFastForwardAllRepairs(13.0 * 3600.0);
        world.Step(RealtimeStep);

        return stillDamagedWithoutTool && world.IsDeviceConnected("system-shields");
    }
}
