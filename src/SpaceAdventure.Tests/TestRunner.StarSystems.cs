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

    // A point clear of every asteroid in sol's field by a wide margin, just past
    // GalaxyMap.WarpZoneRadius(1104) from the field's own centre (1200,1200, M40's 2400x2400
    // scale, recentred alongside sol's own hand-placed content - AsteroidField.RecenterOffsetM40)
    // - was (10,150), just past the old WarpZoneRadius(138) from the old (150,150) centre, for the
    // old 300x300 field. Flying there and slowing down arms CanWarpNow with no specific point to
    // hunt down and park on, the same "parked alongside, under the speed limit" gate as docking
    // (World.StationDocking.cs's CanDockNow), just aimed at an area instead.
    private const float SolWarpZoneX = 80f;
    private const float SolWarpZoneY = 1200f;

    private static bool World_StarSystem_FlyToWarpZoneThenJumpToOtherSystem()
    {
        var world = new World();
        world.SpawnCharacter(1);

        FlyToward(world, new Vec2(SolWarpZoneX, SolWarpZoneY), () => world.CanWarpNow, 1, maxTicks: 500 * 30);

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

        FlyToward(world, new Vec2(SolWarpZoneX, SolWarpZoneY), () => world.CanWarpNow, 1, maxTicks: 500 * 30);
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

        FlyToward(world, new Vec2(SolWarpZoneX, SolWarpZoneY), () => world.CanWarpNow, 1, maxTicks: 500 * 30);
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
        // The procedural tail is generated lazily now (GalaxyMap.cs's EnsureGenerated) - an
        // impossible neighbour target forces it to roll out every remaining system before this
        // test's own full-reachability sweep below.
        map.EnsureGenerated(map.Systems[0].Id, int.MaxValue);
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

    // Most of the procedural tail answers to somebody (GalaxyMap.cs's ControlledSystemChance),
    // not left null/contested by default - the whole point of the distinction existing.
    private static bool World_StarSystem_MostProceduralSystemsHaveAControllingFaction()
    {
        var map = GalaxyMap.CreateStarter();
        map.EnsureAtLeast(50);
        var procedural = map.Systems.Skip(6).Take(50).ToList(); // the 6 hand-authored come first
        return procedural.Count(s => s.ControllingFaction is not null) > 30; // ~85% expected, generous margin
    }

    // A controlled system generates calmer than a contested one - fewer of its own single point
    // turns up as a HostileSector (GalaxyMap.cs's ControlledSystemHostileSectorChance vs.
    // ContestedSystemHostileSectorChance).
    private static bool World_StarSystem_ControlledSystemsGetFewerHostileSectorsThanContested()
    {
        var map = GalaxyMap.CreateStarter();
        map.EnsureAtLeast(194);
        var procedural = map.Systems.Skip(6).ToList();
        var controlled = procedural.Where(s => s.ControllingFaction is not null).ToList();
        var contested = procedural.Where(s => s.ControllingFaction is null).ToList();
        if (controlled.Count < 20 || contested.Count < 5)
            return false; // not enough of either to compare meaningfully - setup problem

        var controlledHostileFraction = controlled.Count(s => s.Points[0].Kind == GalaxyPointKind.HostileSector) / (float)controlled.Count;
        var contestedHostileFraction = contested.Count(s => s.Points[0].Kind == GalaxyPointKind.HostileSector) / (float)contested.Count;
        return controlledHostileFraction < contestedHostileFraction;
    }

    // Rolling the same galaxy in different-sized chunks must land on exactly the same systems in
    // the same order - GalaxyMap.cs's own _proceduralRandom picks up where the last call left it,
    // never restarting, so the pacing of EnsureAtLeast/EnsureGenerated calls can never change what
    // the galaxy actually turns out to be.
    private static bool World_StarSystem_ChunkedGenerationMatchesWhicheverPacingReachesIt()
    {
        var mapA = GalaxyMap.CreateStarter();
        mapA.EnsureAtLeast(194);

        var mapB = GalaxyMap.CreateStarter();
        for (var target = mapB.GeneratedProceduralCount + 7; target <= 194; target += 7)
            mapB.EnsureAtLeast(target);
        mapB.EnsureAtLeast(194); // covers any remainder the loop's own step size didn't land on exactly

        if (mapA.Systems.Count != mapB.Systems.Count)
            return false;
        for (var i = 0; i < mapA.Systems.Count; i++)
        {
            var (a, b) = (mapA.Systems[i], mapB.Systems[i]);
            if (a.Id != b.Id || a.GalaxyX != b.GalaxyX || a.GalaxyY != b.GalaxyY || a.ControllingFaction != b.ControllingFaction)
                return false;
        }
        return true;
    }

    // The whole point of lazy generation: asking for more neighbours than currently exist actually
    // grows the galaxy to satisfy that, rather than just reporting however many happen to be there.
    // +1, not some larger fixed jump - sol's own spiral placement only ever has so many procedural
    // systems land within WarpJumpRadius even with the whole 194-system tail rolled out (a sparser
    // neighbourhood than a denser-packed system might get), so asking for more than that would
    // never be satisfiable no matter how this method behaves - +1 stays safely inside whatever
    // that ceiling turns out to be while still proving growth actually happens on demand.
    private static bool World_StarSystem_EnsureGeneratedGrowsUntilNeighborTargetIsMet()
    {
        var map = GalaxyMap.CreateStarter();
        var before = map.SystemsWithinWarpRange("sol").Count;
        map.EnsureGenerated("sol", before + 1);
        return map.SystemsWithinWarpRange("sol").Count >= before + 1;
    }

    // A station's own controlling faction stands down peacefully unless the crew has actually made
    // an enemy of it - fall far enough (World.Voyage.cs's Arrive, M37) and the station meets the
    // approach with a defensive squadron instead of the usual docking approach.
    private static bool World_Station_HostileStandingTriggersDefensiveSquadronOnApproach()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // survives any breaches the fight opens up
        GrindStandingHostile(world, "sector-delta", FactionId.Consortium); // trade-station is Consortium's
        if (world.GetStanding(FactionId.Consortium) > FactionDefinitions.HostileThreshold)
            return false; // didn't actually anger them enough - setup problem, not the behavior under test

        // Flying at the berth itself (World.Voyage.cs's UpdateNearestStation checks hostility on
        // the same proximity scan that arms CanDockNow) - there's no separate "approach" state to
        // land in short of the fight any more (M39), so this is just flying at the station until
        // either the fight starts or the berth would otherwise be reachable. FlyToward's own until
        // predicate stops it the moment either happens, rather than fighting through a battle the
        // way ApproachBerth does - catching that moment is the whole point of this test.
        var target = world.GalaxyMap.GetPoint("trade-station").Position;
        FlyToward(world, target, () => world.IsInBattle ||
            ((world.DockBerthPosition - target).Length() < 40f && world.CanDockNow), 1, maxTicks: 200 * 30);

        return world.IsInBattle;
    }
}
