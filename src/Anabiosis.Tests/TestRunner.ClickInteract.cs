using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // humble-soaring-cat.md - "Полный переход на клик как в Baro": each new ClientCommand id field
    // has to reach the exact same effect its [E]-key twin already had (World.Interact.cs), just
    // addressed by id instead of "nearest in range". These mirror the existing E-key tests for the
    // same actions (World_AmmoStorage_*, World_Crime_StealCrate_*, World_RepairSystem_*) one for one.

    // Direct user request ("это в будущем будет одно из главных устройств, их будет много") - a
    // minimal 2-room custom ship carrying TWO Terminal devices, one per room, to prove each toggles
    // independently (Ship.Terminals/World.Terminals.cs, the same "list of per-instance states" shape
    // SuitLocker already has) rather than the old single shared TerminalOn.
    private static CustomShipDefinition BuildTwoTerminalShipDefinition() => new(
        "Тестовый корабль с 2 терминалами",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 6, 6),
            new CustomRoomDef("b", "Отсек", 6, 0, 6, 6),
        },
        new[] { new CustomDoorDef(6, 3, true, true) }, // shared wall at X=6, centered on its Y=[0,6] span
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 4),
            new CustomDeviceDef(CustomDeviceKind.Helm, 4, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 4, 4),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 9, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 9, 2),
            new CustomDeviceDef(CustomDeviceKind.Terminal, 1, 4),
            new CustomDeviceDef(CustomDeviceKind.Terminal, 9, 4),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(2f, 5f, TileSide.West, 20f) });

    private static bool World_ClickInteract_Terminal_TogglesOnlyTheOneClicked()
    {
        var world = new World(ShipKind.Custom, BuildTwoTerminalShipDefinition());
        world.SpawnCharacter(1);
        var terminals = world.Ship.Terminals;
        if (terminals.Count != 2)
            return false;
        var (near, far) = ((float)terminals[0].X, (float)terminals[0].Y) == (1f, 4f)
            ? (terminals[0], terminals[1])
            : (terminals[1], terminals[0]);

        MoveCharacterTo(world, 1, (float)near.X, (float)near.Y);
        world.ApplyCommand(1, new ClientCommand(1, TerminalInteractId: near.Id));
        world.Step(RealtimeStep);

        var states = world.CreateSnapshot().Terminals!;
        var nearState = states.Single(s => s.Block.Id == near.Id);
        var farState = states.Single(s => s.Block.Id == far.Id);
        if (!nearState.On || farState.On)
            return false; // only the clicked terminal should have flipped on

        // Toggling it again turns it back off, still without touching the other one.
        world.ApplyCommand(1, new ClientCommand(1, TerminalInteractId: near.Id));
        world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Terminals!;
        return !after.Single(s => s.Block.Id == near.Id).On && !after.Single(s => s.Block.Id == far.Id).On;
    }

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
