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

    // A point clear of every asteroid in sol's field by a wide margin (a horizontal line at
    // y=150 from the dock berth), 140 units from the field's centre (150,150) - just past
    // GalaxyMap.WarpZoneRadius (138), so flying there and slowing down arms CanWarpNow with no
    // specific point to hunt down and park on, the same "parked alongside, under the speed limit"
    // gate as docking (World.StationDocking.cs's CanDockNow), just aimed at an area instead.
    private const float SolWarpZoneX = 10f;
    private const float SolWarpZoneY = 150f;

    private static bool World_StarSystem_FlyToWarpZoneThenJumpToOtherSystem()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToX: SolWarpZoneX, TravelToY: SolWarpZoneY));
        for (var i = 0; i < 120 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);

        if (!world.CanWarpNow)
            return false; // never reached the warp zone - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var snapshot = world.CreateSnapshot();

        return snapshot.CurrentSystemId == "alpha-centauri"
            && world.AsteroidField == world.GalaxyMap.GetSystem("alpha-centauri").Field;
    }

    // Arrival now drops the ship right at the edge of the NEW system's own field (still past
    // WarpZoneRadius from its centre), not the field's bare centre (game_design.md) - which also
    // means CanWarpNow is already armed the instant the jump lands, so a system on the chain with
    // more than one neighbour (alpha-centauri sits between sol and tau-ceti) can be crossed
    // straight through without first flying anywhere.
    private static bool World_StarSystem_ArrivesAtEdgeOfNewSystemAndCanContinueWarping()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToX: SolWarpZoneX, TravelToY: SolWarpZoneY));
        for (var i = 0; i < 120 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);
        if (!world.CanWarpNow)
            return false; // never reached the warp zone - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var alphaCentauriCenter = world.GalaxyMap.GetSystem("alpha-centauri").Field.Center;
        var shipField = world.CreateSnapshot().ShipField;
        var landedInWarpZone = (alphaCentauriCenter - new Vec2(shipField.X, shipField.Y)).Length() >= GalaxyMap.WarpZoneRadius - 0.01f;
        var canContinueImmediately = world.CanWarpNow;

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "tau-ceti"));

        return landedInWarpZone && canContinueImmediately && world.CreateSnapshot().CurrentSystemId == "tau-ceti";
    }

    // Only arms once actually out past WarpZoneRadius and slowed down - mashing the button from
    // across the system (or from a different system entirely) must not teleport the ship.
    private static bool World_StarSystem_WarpDoesNothingOutsideTheWarpZone()
    {
        var world = new World();
        world.SpawnCharacter(1);

        if (world.CanWarpNow)
            return false; // starts docked, nowhere near the warp zone - setup problem

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        return world.CreateSnapshot().CurrentSystemId == "sol";
    }

    // A valid warp target is any system within GalaxyMap.WarpJumpRadius of the current one, not a
    // full graph (game_design.md - "но так чтобы не возникало путанных варп коридоров") - tau-ceti
    // sits two hand-authored steps from sol (240 units, just outside WarpJumpRadius's 220), so a
    // direct jump there must be refused even while parked and slow in sol's own warp zone.
    private static bool World_StarSystem_WarpFailsOutsideWarpRadius()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToX: SolWarpZoneX, TravelToY: SolWarpZoneY));
        for (var i = 0; i < 120 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);
        if (!world.CanWarpNow)
            return false; // never reached the warp zone - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "tau-ceti"));
        return world.CreateSnapshot().CurrentSystemId == "sol";
    }

    // Every system reaches every other by hopping through systems within warp range of each other -
    // GalaxyMap.GenerateProceduralSystems guarantees this by construction (each new system is
    // placed within WarpJumpRadius of at least one already-placed one), so the whole galaxy forms a
    // single component even though there's no explicit edge list to walk.
    private static bool World_StarSystem_GalaxyIsFullyReachableByWarpRadius()
    {
        var world = new World();
        var map = world.GalaxyMap;
        var visited = new HashSet<string> { map.Systems[0].Id };
        var frontier = new Queue<string>();
        frontier.Enqueue(map.Systems[0].Id);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in map.SystemsWithinWarpRange(current))
                if (visited.Add(neighbor))
                    frontier.Enqueue(neighbor);
        }

        return visited.Count == map.Systems.Count && map.Systems.Count > 4;
    }

    // "Большая галактическая карта на 200 солнечных систем" - the 6 hand-authored systems plus
    // GalaxyMap.CreateStarter's ProceduralSystemCount (194) generated ones. Every system warps from
    // anywhere past its own field's WarpZoneRadius (no dedicated marker needed per system anymore),
    // so the only thing left to prove at this scale is that the whole thing stays one
    // warp-reachable component, not just "generated without crashing".
    private static bool World_StarSystem_GalaxyHas200SystemsAllReachable()
    {
        var world = new World();
        var map = world.GalaxyMap;
        if (map.Systems.Count != 200)
            return false;

        var visited = new HashSet<string> { map.Systems[0].Id };
        var frontier = new Queue<string>();
        frontier.Enqueue(map.Systems[0].Id);
        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            foreach (var neighbor in map.SystemsWithinWarpRange(current))
                if (visited.Add(neighbor))
                    frontier.Enqueue(neighbor);
        }

        return visited.Count == 200;
    }
}
