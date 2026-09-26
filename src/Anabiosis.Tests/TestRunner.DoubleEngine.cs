using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Direct user request ("двойной двигатель... это как 2 двигателя состоящих из 3 клеток имели бы
    // общую клетку в основании и 2 сопла двигателя смотрели под углом 90 градусов в их разнице") -
    // two ordinary ShipEngine fixtures sharing one Control position (1,1.5), Facing West and North
    // (90 degrees apart). Same room shape as BuildEngineCustomShipDefinition (TestRunner.Engines.cs) -
    // West's own Bulkhead/Nozzle land exactly where that test's single engine already proved works
    // (0,1.5)/(-1,1.5); North's own Bulkhead (1,0.5) sits on room a's other exterior wall (its top,
    // Y=0), with Nozzle (1,-0.5) in genuine open space beyond it - no other device or room occupies
    // either the North arm's own tiles.
    private static CustomShipDefinition BuildDoubleEngineCustomShipDefinition() => new(
        "Тестовый корабль с двойным двигателем",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Шлюз", 4, 0, 4, 4),
        },
        new[] { new CustomDoorDef(4, 2, true, true) },
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
        EnginesRaw: new[]
        {
            new CustomEngineDef(1f, 1.5f, TileSide.West, 20f),
            new CustomEngineDef(1f, 1.5f, TileSide.North, 20f),
        });

    // Confirms the shared-Control data model needs no changes at all (ShipEngine.cs's own doc
    // comment - Bulkhead/Nozzle are COMPUTED from Facing, never stored) - two engines at the exact
    // same ControlPosition, each with its own correctly-computed Bulkhead/Nozzle 90 degrees apart.
    private static bool Ship_DoubleEngine_FromDefinition_SharesControlPositionWithTwoFacings()
    {
        var ship = Ship.FromCustomDefinition(BuildDoubleEngineCustomShipDefinition());
        if (ship.Engines.Count != 2)
            return false;
        var west = ship.Engines.Single(e => e.Facing == TileSide.West);
        var north = ship.Engines.Single(e => e.Facing == TileSide.North);
        return west.ControlPosition == new Vec2(1, 1.5) && north.ControlPosition == new Vec2(1, 1.5)
            && west.BulkheadPosition == new Vec2(0, 1.5) && west.NozzlePosition == new Vec2(-1, 1.5)
            && north.BulkheadPosition == new Vec2(1, 0.5) && north.NozzlePosition == new Vec2(1, -0.5)
            && west.Id != north.Id;
    }

    // Direct user request ("2 вектора тяги") - a pure forward-throttle request (ForwardDegrees=0
    // means ship-local forward is East, same convention BuildSymmetricEngineCustomShipDefinition's
    // own doc comment already spells out) only fires the West-facing arm (push direction East, fully
    // aligned); the North-facing arm (push direction South) sits idle for it, same "real geometry
    // decides who fires" rule World.Engines.cs's MarchingRawControl already applies to any 2 engines.
    private static bool World_DoubleEngine_Throttle_FiresOnlyTheAlignedArm()
    {
        var world = new World(ShipKind.Custom, BuildDoubleEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);

        world.DebugSetHelmInput(1f, 0f, 0f);
        world.Step(RealtimeStep);

        var states = world.CreateSnapshot().EngineStates!;
        var west = states.Single(s => s.Facing == TileSide.West);
        var north = states.Single(s => s.Facing == TileSide.North);
        return west.IsThrusting && !north.IsThrusting;
    }

    // The other half of the same claim - a pure strafe request fires the North-facing arm instead
    // (same perpendicular-engine pattern as World_Engine_Strafe_FiresThePerpendicularEngine), proving
    // the double engine really does give 2 independent, individually-selectable thrust vectors rather
    // than always firing together as one unit.
    private static bool World_DoubleEngine_Strafe_FiresTheOtherArm()
    {
        var world = new World(ShipKind.Custom, BuildDoubleEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);

        world.DebugSetHelmInput(0f, 1f, 0f);
        world.Step(RealtimeStep);

        var states = world.CreateSnapshot().EngineStates!;
        var west = states.Single(s => s.Facing == TileSide.West);
        var north = states.Single(s => s.Facing == TileSide.North);
        return north.IsThrusting && !west.IsThrusting;
    }

    // Direct user decision (AskUserQuestion - "Чинить/чинить по очереди с одной точки") - the two
    // arms have fully INDEPENDENT hp/damage state despite sharing a Control tile: breaking one arm's
    // Nozzle stops only that arm, the other keeps thrusting normally when its own axis is requested.
    private static bool World_DoubleEngine_NozzleBreak_OnlyStopsThatArm()
    {
        var world = new World(ShipKind.Custom, BuildDoubleEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);
        var westId = world.Ship.Engines.Single(e => e.Facing == TileSide.West).Id;

        world.DebugBreakEngineNozzle(westId);

        world.DebugSetHelmInput(1f, 0f, 0f);
        world.Step(RealtimeStep);
        if (world.CreateSnapshot().EngineStates!.Single(s => s.Facing == TileSide.West).IsThrusting)
            return false;

        world.DebugSetHelmInput(0f, 1f, 0f);
        world.Step(RealtimeStep);
        return world.CreateSnapshot().EngineStates!.Single(s => s.Facing == TileSide.North).IsThrusting;
    }

    // Same independence, the other direction: breaking one arm's Control seizes ONLY that arm's own
    // throttle (World.Engines.cs's _engineFrozenThrottle, id-keyed) - the other arm keeps responding
    // live to fresh helm input the whole time.
    private static bool World_DoubleEngine_ControlBreak_OnlyFreezesThatArm()
    {
        var world = new World(ShipKind.Custom, BuildDoubleEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);
        var westId = world.Ship.Engines.Single(e => e.Facing == TileSide.West).Id;
        var northId = world.Ship.Engines.Single(e => e.Facing == TileSide.North).Id;

        world.DebugSetHelmInput(1f, 0f, 0f);
        world.Step(RealtimeStep);
        world.DebugBreakEngineControl(westId);
        if (!world.IsEngineControlBroken(westId) || world.IsEngineControlBroken(northId))
            return false;

        // Live input drops to a pure strafe request: the frozen West arm should keep pushing at
        // whatever throttle it held (ignoring the fact that throttle is now 0), while the intact
        // North arm should respond live and fire for the new strafe request.
        world.DebugSetHelmInput(0f, 1f, 0f);
        world.Step(RealtimeStep);
        var states = world.CreateSnapshot().EngineStates!;
        return states.Single(s => s.Facing == TileSide.West).IsThrusting
            && states.Single(s => s.Facing == TileSide.North).IsThrusting;
    }
}
