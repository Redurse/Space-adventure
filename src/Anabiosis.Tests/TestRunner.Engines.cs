using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Same "room a"/"room b" shape as BuildSimpleCustomShipDefinition (TestRunner.CustomShip.cs),
    // minus the flat CustomDeviceKind.Engine entry - this ship's own thrust comes entirely from the
    // one real ShipEngine instead. Facing West at Y=1.5 - a real wall-segment CENTER (room a's own
    // left wall generates one block per unit row, y+0.5 - Ship.cs's GenerateOuterWallBlocks), the
    // same alignment RoomCatalog.EnginesFor now uses, so Bulkhead (0,1.5) lands exactly where that
    // WallBlock would have been (and Ship.cs's own constructor drops it in favor of the engine) and
    // Nozzle (-1,1.5) lands in genuine open space beyond it - room a's left side is confirmed real
    // exterior hull by CustomShip_FromDefinition_SkipsWallBlocksOnInteriorAndAirlockSides.
    private static CustomShipDefinition BuildEngineCustomShipDefinition() => new(
        "Тестовый корабль с двигателем",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Шлюз", 4, 0, 4, 4),
        },
        new[] { new CustomDoorDef(4, 2, true, true) }, // shared wall at X=4, centered on its Y=[0,4] span
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 1),
            new CustomDeviceDef(CustomDeviceKind.Helm, 1, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 2, 3),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 5, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 5, 2),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(1f, 1.5f, TileSide.West, 20f) });

    private static void SitAtEngineTestHelm(World world, int playerId = 1)
    {
        var console = world.Ship.HelmConsole.Position;
        MoveCharacterTo(world, playerId, (float)console.X, (float)console.Y);
        world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true));
    }

    private static bool Ship_Engine_FromDefinition_ComputesTilePositionsAlongFacing()
    {
        var ship = Ship.FromCustomDefinition(BuildEngineCustomShipDefinition());
        var engine = ship.Engines.Single();
        return engine.ControlPosition == new Vec2(1, 1.5)
            && engine.BulkheadPosition == new Vec2(0, 1.5)
            && engine.NozzlePosition == new Vec2(-1, 1.5)
            && engine.RoomId == "a";
    }

    // Regression for the fix in Ship.cs's constructor - without it, the room's own outer-wall
    // generation would ALSO place a plain WallBlock at (0, 1.5), overlapping the engine's own
    // Bulkhead almost exactly (both hittable/weldable, silently double-booking that one spot).
    private static bool Ship_Engine_BulkheadPosition_ExcludesGeneratedWallBlock()
    {
        var ship = Ship.FromCustomDefinition(BuildEngineCustomShipDefinition());
        return ship.WallBlocks.All(b => (b.Position - new Vec2(0, 1.5)).Length() > 0.1);
    }

    // Fresh engine, no throttle: not thrusting. Throttled while everything's intact: thrusting.
    private static bool World_Engine_IsThrustingTracksLiveThrottleWhileIntact()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        SitAtEngineTestHelm(world, 1);

        var idle = world.CreateSnapshot().EngineStates!.Single();
        if (idle.IsThrusting)
            return false;

        world.DebugSetHelmInput(1f, 0f, 0f);
        world.Step(RealtimeStep);
        return world.CreateSnapshot().EngineStates!.Single().IsThrusting;
    }

    // Direct user request - "если на полном ходу сломается 1 часть, то двигатель будет работать в
    // полную мощность, пока не починить 1 тайл": breaking Control seizes the throttle at whatever it
    // already was - dropping the live helm throttle back to zero afterward must NOT stop this engine.
    private static bool World_Engine_ControlBreak_FreezesThrottleUntilRepaired()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        SitAtEngineTestHelm(world, 1);
        var engineId = world.Ship.Engines.Single().Id;

        world.DebugSetHelmInput(1f, 0f, 0f);
        world.Step(RealtimeStep);
        world.DebugBreakEngineControl(engineId);
        if (!world.IsEngineControlBroken(engineId))
            return false;

        // Live throttle drops to zero, but the frozen engine should keep thrusting at what it held.
        world.DebugSetHelmInput(0f, 0f, 0f);
        world.Step(RealtimeStep);
        if (!world.CreateSnapshot().EngineStates!.Single().IsThrusting)
            return false;

        // Repairing Control hands control back to the live throttle (already zero) - thrusting stops.
        world.DebugRepairEngineControl(engineId);
        world.Step(RealtimeStep);
        return !world.CreateSnapshot().EngineStates!.Single().IsThrusting;
    }

    // Direct user request - "при поломке 3 тайла(сопла) данный двигатель больше не генерирует тягу" -
    // independent of Control/throttle, which stays live and nonzero throughout.
    private static bool World_Engine_NozzleBreak_StopsThrustRegardlessOfThrottle()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        SitAtEngineTestHelm(world, 1);
        var engineId = world.Ship.Engines.Single().Id;

        world.DebugSetHelmInput(1f, 0f, 0f);
        world.Step(RealtimeStep);
        if (!world.CreateSnapshot().EngineStates!.Single().IsThrusting)
            return false;

        world.DebugBreakEngineNozzle(engineId);
        world.Step(RealtimeStep);
        var afterBreak = world.CreateSnapshot().EngineStates!.Single();
        return afterBreak.NozzleBroken && !afterBreak.IsThrusting;
    }

    // Direct user request - "2 клетка ... при поломке начинает пропускать воздух" - a breached
    // Bulkhead leaks its own room's oxygen exactly like a breached WallBlock would.
    private static bool World_Engine_BulkheadBreach_LeaksRoomOxygen()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        var engineId = world.Ship.Engines.Single().Id;
        var roomId = world.Ship.Engines.Single().RoomId;

        var before = world.CreateSnapshot().RoomOxygen.First(r => r.RoomId == roomId).Oxygen;
        world.DebugBreachEngineBulkhead(engineId);
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().RoomOxygen.First(r => r.RoomId == roomId).Oxygen;
        return before >= 99f && after < before;
    }

    // Same fetch as TakeFromRack, but walks straight to the rack (MoveCharacterTo) instead of via
    // WalkAcrossShipTo's own "doorRow=3" shortcut. That shortcut assumes every door sits at the
    // hull's own vertical center (true on the standard 6-tall row-of-rooms hulls it was written
    // for), but BuildEngineCustomShipDefinition's rooms are only 4 units tall, so their real door
    // between "a"/"b" is centered at Y=2, not Y=3 - walking the doorRow=3 route means grazing the
    // very edge of that door instead of its middle, which stalled indefinitely once combined with
    // this method's own float-precision convergence (found live: burned through hundreds of ticks
    // never quite crossing, long enough for the bulkhead breach's own oxygen leak - triggered right
    // before this call - to suffocate the character before they ever reached the rack). A direct
    // diagonal walk keeps Y near its own start/target (both ~2, safely inside the door's true span)
    // the whole time, the same fix already used by hand in World_Eva_CorvetteCrewGoesOutThroughABeamPort
    // for this exact class of problem.
    private static int TakeFromRackDirect(World world, ItemType item)
    {
        var rackSlots = world.CreateSnapshot().RackSlots;
        var rackSlotIndex = -1;
        for (var i = 0; i < rackSlots.Count; i++)
            if (rackSlots[i] == item) { rackSlotIndex = i; break; }
        if (rackSlotIndex < 0)
            return -1;

        var rack = world.Ship.StorageRacks[rackSlotIndex / StorageRack.Capacity];
        MoveCharacterTo(world, 1, rack.X, rack.Y);

        var mainSlots = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots;
        var freeMainSlot = -1;
        for (var i = 0; i < mainSlots.Count; i++)
            if (mainSlots[i] is null) { freeMainSlot = i; break; }
        if (freeMainSlot < 0)
            return -1;

        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Rack, rackSlotIndex), MoveItemTo: new SlotRef(ItemSlotKind.Main, freeMainSlot)));
        return freeMainSlot;
    }

    // Direct user request - the Bulkhead "держит воздух" like a wall panel, so it welds shut like
    // one too (World.Welding.cs's own FindAimedEngine, added alongside FindAimedWallBlock). Standing
    // at (0.5, 1.5) rather than right at Control keeps this comfortably close to Bulkhead(0,1.5)
    // without also depending on Control's own exact position.
    private static bool World_Engine_Welder_RepairsBulkhead()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        var engineId = world.Ship.Engines.Single().Id;
        world.DebugBreachEngineBulkhead(engineId);

        var toolSlot = TakeFromRackDirect(world, ItemType.WeldingTool);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: toolSlot));
        TakeFromRackDirect(world, ItemType.WeldingTank);
        AttachTankTo(world, Array.IndexOf(
            world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.WeldingTool),
            ItemType.WeldingTank);
        MoveCharacterTo(world, 1, 0.5f, 1.5f);
        for (var i = 0; i < 60; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: -1f, LookY: 0f));
            world.Step(RealtimeStep);
        }

        return !world.CreateSnapshot().EngineStates!.Single().BulkheadBroken;
    }

    // Direct user request - the Control tile's seized throttle repairs with the same wrench/
    // screwdriver minigame as the reactor/helm/etc. "boxes" (World.SystemRepair.cs).
    private static bool World_Engine_Wrench_RepairsControlViaMinigame()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        var engineId = world.Ship.Engines.Single().Id;
        world.DebugBreakEngineControl(engineId);

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        MoveCharacterTo(world, 1, 1f, 1.5f); // Control's own position
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // starts the repair timer
        world.Step(RealtimeStep);
        world.DebugFastForwardAllRepairs(999999);
        world.Step(RealtimeStep);

        return !world.IsEngineControlBroken(engineId);
    }

    // Cosmoteer-style engines, RCS follow-up (direct user request - "по его образу сделаем все
    // остальные") - same fixture shape as BuildEngineCustomShipDefinition, just Role: Rcs.
    private static CustomShipDefinition BuildRcsEngineCustomShipDefinition() => new(
        "Тестовый корабль с РКС",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Шлюз", 4, 0, 4, 4),
        },
        new[] { new CustomDoorDef(4, 2, true, true) }, // shared wall at X=4, centered on its Y=[0,4] span
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 1),
            new CustomDeviceDef(CustomDeviceKind.Helm, 1, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 2, 3),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 5, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 5, 2),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(1f, 1.5f, TileSide.West, 15f, EngineRole.Rcs) });

    private static bool Ship_Engine_Rcs_FromDefinition_HasRcsRole() =>
        Ship.FromCustomDefinition(BuildRcsEngineCustomShipDefinition()).Engines.Single().Role == EngineRole.Rcs;

    // Same freeze mechanic as the marching engine's own throttle, but tracking helm TURN instead -
    // World.Engines.cs's EffectiveControl branches on Role for exactly this.
    private static bool World_Engine_Rcs_ControlBreak_FreezesTurnUntilRepaired()
    {
        var world = new World(ShipKind.Custom, BuildRcsEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        SitAtEngineTestHelm(world, 1);
        var engineId = world.Ship.Engines.Single().Id;

        world.DebugSetHelmInput(0f, 0f, 1f);
        world.Step(RealtimeStep);
        world.DebugBreakEngineControl(engineId);
        if (!world.IsEngineControlBroken(engineId))
            return false;

        // Live turn input drops to zero, but the frozen engine should keep "thrusting" (puffing) at
        // whatever it held.
        world.DebugSetHelmInput(0f, 0f, 0f);
        world.Step(RealtimeStep);
        if (!world.CreateSnapshot().EngineStates!.Single().IsThrusting)
            return false;

        world.DebugRepairEngineControl(engineId);
        world.Step(RealtimeStep);
        return !world.CreateSnapshot().EngineStates!.Single().IsThrusting;
    }
}
