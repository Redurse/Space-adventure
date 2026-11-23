using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// An airlock that is a door placed onto open space (the editor's "Шлюз" tool, a door edge with one side open) must be walkable-through,
// like a rectangular airlock door. Direct user report on the built-in "КРУТОЙ КОРАБЛЬ", whose airlocks are all of this kind: with the
// door open, in a suit with a tank, a crew member could not get out into space.
internal static partial class TestRunner
{
    // A 14x14 deck whose east side has one door onto open space at row 3; the devices stand in the far south-east corner (the reactor, whose body is an obstacle, in the north-west), out of the way.
    private static World BuildEdgeAirlockWorld()
    {
        var grid = new TileGrid();
        for (var y = 0; y < 14; y++)
            for (var x = 0; x < 14; x++)
                grid.SetFloor(new TileCoord(x, y), true);
        grid.AddDoorEdge(new TileCoord(13, 3), TileSide.East, "edge-lock");
        var (definition, errors) = BuildTileDefinition(grid);
        if (definition is null)
            throw new InvalidOperationException("setup: " + string.Join("; ", errors));
        var simple = BuildSimpleCustomShipDefinition();
        return new World(ShipKind.Custom, definition with { Devices = simple.Devices.Select(d => d.Kind == CustomDeviceKind.Reactor ? d : d with { X = d.X + 7, Y = d.Y + 7 }).ToList() });
    }

    // Undocks, optionally opens the airlock, walks the character to the deck beside it and pushes east into it for a while.
    private static bool WalkIntoEdgeAirlock(bool openFirst) => WalkIntoEdgeAirlock(openFirst, out _);

    private static bool WalkIntoEdgeAirlock(bool openFirst, out World world)
    {
        world = BuildEdgeAirlockWorld();
        var w = world; // locals of a method with an out parameter cannot be captured by the local function below
        world.SpawnCharacter(1);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        var edge = world.Ship.DoorEdges.FirstOrDefault(e => e.RoomAId is null || e.RoomBId is null);
        if (edge is null)
            throw new InvalidOperationException("setup: the airlock did not survive the build");
        // Up to the door's row, then east along it into the airlock - real walking, the way a player does it.
        void Walk(int moveX, int moveY, Func<CharacterState, bool> done, int maxTicks)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                if (done(w.CreateSnapshot().Characters.Single(c => c.PlayerId == 1)))
                    return;
                w.ApplyCommand(1, new ClientCommand(1, MoveX: moveX, MoveY: moveY));
                w.Step(RealtimeStep);
            }
        }
        Walk(0, -1, c => c.Y <= 3.5, 400);
        Walk(1, 0, c => c.X >= 12.0, 400);
        // Opened only now, standing beside it: an open airlock is vacuum for the whole room, and an unsuited crew member has three seconds.
        if (openFirst)
        {
            world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: edge.Id));
            world.Step(RealtimeStep);
            if (!world.IsDoorOpen(edge.Id))
                throw new InvalidOperationException("setup: the airlock would not open");
        }
        Walk(1, 0, c => c.IsOutside, 200);
        var end = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return end.IsOutside;
    }
    private static bool World_EdgeAirlock_OpenDoor_LetsACrewMemberWalkOut() => WalkIntoEdgeAirlock(openFirst: true);

    // Shut, the same door holds: nobody walks out through a closed airlock.
    private static bool World_EdgeAirlock_ClosedDoor_KeepsACrewMemberInside() => !WalkIntoEdgeAirlock(openFirst: false);

    // Out through the open airlock on foot, then back in by flying into it on the jetpack - the way a player does it. Direct user report: flying into
    // the open airlock did nothing and the character sailed on through the ship.
    private static bool World_EdgeAirlock_OpenDoor_LetsAJetpackFlierBackIn()
    {
        if (!WalkIntoEdgeAirlock(openFirst: true, out var world))
            return false;
        // Drifting back toward the ship at walking pace (a jetpack burn would need a suit and a tank; the door does not care how the speed came).
        for (var i = 0; i < 90; i++)
        {
            world.DebugSetEvaVelocity(1, new Vec2(-3, 0));
            world.Step(RealtimeStep);
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            if (!me.IsOutside)
                return me.Health > 0f;
        }
        return false;
    }
}
