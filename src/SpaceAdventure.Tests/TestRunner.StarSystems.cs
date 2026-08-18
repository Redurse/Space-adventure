using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // The galaxy is more than one system from the start (GalaxyMap.CreateStarter's "sol" +
    // "alpha-centauri"), even though nothing can reach the second one yet (M31 is data-model
    // only) - and a fresh crew's home station is unambiguously in the first.
    private static bool World_StarSystem_GalaxyHasMoreThanOneSystemFromTheStart()
    {
        var world = new World();
        return world.GalaxyMap.Systems.Count > 1
            && world.GalaxyMap.SystemOf("home-station").Id == "sol"
            && world.AsteroidField == world.GalaxyMap.GetSystem("sol").Field;
    }

    // Flying to the system's own WarpPoint and parking there arms CanWarpNow - the same "parked
    // alongside, under the speed limit" gate as docking (World.StationDocking.cs's CanDockNow),
    // just for a different kind of point (World.StarSystems.cs).
    private static bool World_StarSystem_FlyToWarpPointThenJumpToOtherSystem()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sol-warp-point"));
        for (var i = 0; i < 120 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);

        if (!world.CanWarpNow)
            return false; // never reached the warp point - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var snapshot = world.CreateSnapshot();

        return snapshot.CurrentSystemId == "alpha-centauri"
            && world.AsteroidField == world.GalaxyMap.GetSystem("alpha-centauri").Field;
    }

    // Arrival now drops the ship right at the NEW system's own WarpPoint, not the field's bare
    // centre (game_design.md) - which also means CanWarpNow is already armed the instant the jump
    // lands, so a system on the chain with more than one neighbour (alpha-centauri sits between
    // sol and tau-ceti) can be crossed straight through without first flying anywhere.
    private static bool World_StarSystem_ArrivesAtNewSystemsWarpPointAndCanContinueWarping()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sol-warp-point"));
        for (var i = 0; i < 120 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);
        if (!world.CanWarpNow)
            return false; // never reached the warp point - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var acWarpPoint = world.GalaxyMap.GetPoint("ac-warp-point");
        var shipField = world.CreateSnapshot().ShipField;
        var landedAtWarpPoint = (acWarpPoint.Position - new Vec2(shipField.X, shipField.Y)).Length() < 1f;
        var canContinueImmediately = world.CanWarpNow;

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "tau-ceti"));

        return landedAtWarpPoint && canContinueImmediately && world.CreateSnapshot().CurrentSystemId == "tau-ceti";
    }

    // Only arms once actually parked at the warp point - mashing the button from across the
    // system (or from a different system entirely) must not teleport the ship.
    private static bool World_StarSystem_WarpDoesNothingWithoutBeingAtTheWarpPoint()
    {
        var world = new World();
        world.SpawnCharacter(1);

        if (world.CanWarpNow)
            return false; // starts docked, nowhere near the warp point - setup problem

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        return world.CreateSnapshot().CurrentSystemId == "sol";
    }

    // The galaxy is now a limited, non-crossing corridor graph, not a full one (game_design.md -
    // "но так чтобы не возникало путанных варп коридоров") - tau-ceti is two hops from sol
    // (through alpha-centauri), so a direct jump there must be refused even while parked and slow
    // at sol's own warp point.
    private static bool World_StarSystem_WarpFailsToANonAdjacentSystem()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sol-warp-point"));
        for (var i = 0; i < 120 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);
        if (!world.CanWarpNow)
            return false; // never reached the warp point - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "tau-ceti"));
        return world.CreateSnapshot().CurrentSystemId == "sol";
    }

    // Every system reaches every other by hopping through its neighbours (a connected path graph),
    // even though no two are directly linked except their immediate neighbours.
    private static bool World_StarSystem_GalaxyFormsOneConnectedChain()
    {
        var world = new World();
        var map = world.GalaxyMap;
        var visited = new HashSet<string> { map.Systems[0].Id };
        var frontier = new Queue<string>();
        frontier.Enqueue(map.Systems[0].Id);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in map.ConnectedSystemIds(current))
                if (visited.Add(neighbor))
                    frontier.Enqueue(neighbor);
        }

        return visited.Count == map.Systems.Count && map.Systems.Count > 4;
    }
}
