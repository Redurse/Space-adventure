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

    // A point clear of every asteroid in sol's field by a wide margin, just past sol's own real,
    // body-driven WarpZoneRadius (M50 - StarSystem.WarpZoneRadius, no longer a single flat
    // constant shared by every system) from the field's own real centre. Computed fresh from the
    // current field rather than a hand-picked literal, the same reason TestRunner.Campaign.cs's own
    // safeBearing target is computed rather than hardcoded - a fixed literal tuned for one field
    // size silently stops meaning "past the warp zone" the moment that size changes again. The same
    // (50,58) bearing Campaign.cs already uses, clear of every hostile sector along the way.
    private static Vec2 SolWarpZoneTarget(World world)
    {
        var sol = world.GalaxyMap.GetSystem("sol");
        var safeBearing = new Vec2(50f, 58f).Normalized();
        // +200 past the radius itself, not right on it - FlyToSolWarpZoneAndStop's own arrival
        // tolerance (50 units) must never be able to land short of the actual boundary.
        return sol.Field.Center + safeBearing * (sol.WarpZoneRadius + 200f);
    }

    // Teleports (comfortably near) the warp zone rather than actually flying there - simulated
    // flight stopped being reproducible once CruiseMaxSpeed became a real, high KSP-scale cap
    // (World.Gravity.cs) and the warp zone itself sits hundreds of billions of units out (a
    // per-system, body-driven WarpZoneRadius, M52): even at CruiseMaxSpeed's own ~830,000 units/s
    // ceiling, closing a several-hundred-billion-unit gap takes DAYS of simulated time, wildly past
    // any test's own tick budget (confirmed via a scratch trace - after 1500s of full cruise the
    // ship had covered barely 0.1% of the distance). What every caller actually checks afterward is
    // warp GATING (CanWarpNow, WarpToSystemId behavior), not whether the autopilot can physically
    // make the trip - exactly DebugPlaceShip's own "skip the piloting problem for setup that was
    // never about piloting" reasoning (see World.ShipField.cs), applied here instead of to docking.
    private static void FlyToSolWarpZoneAndStop(World world, int playerId = 1)
    {
        var target = SolWarpZoneTarget(world);
        if (world.IsDocked)
        {
            world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
            world.Step(RealtimeStep);
        }
        world.DebugPlaceShip(target);
        world.Step(RealtimeStep);
    }

    private static bool World_StarSystem_FlyToWarpZoneThenJumpToOtherSystem()
    {
        var world = new World();
        world.SpawnCharacter(1);

        FlyToSolWarpZoneAndStop(world);

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

        FlyToSolWarpZoneAndStop(world);
        if (!world.CanWarpNow)
            return false; // never reached the warp zone - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var alphaCentauriCenter = world.GalaxyMap.GetSystem("alpha-centauri").Field.Center;
        var shipField = world.CreateSnapshot().ShipField;
        var landedInWarpZone = (alphaCentauriCenter - new Vec2(shipField.X, shipField.Y)).Length() >= world.GalaxyMap.GetSystem("alpha-centauri").WarpZoneRadius - 0.01f;
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

        FlyToSolWarpZoneAndStop(world);
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

        // Teleports right onto the berth rather than flying there - UpdateNearestStation
        // (World.Voyage.cs) checks hostility on a proximity scan against the station's own LIVE
        // position every tick, so landing on it directly triggers the exact same defensive-squadron
        // check a real approach would (M58 follow-up: flying there for real used to work when
        // "trade-station" sat still at a single, forever-valid target point, but a hosted station
        // now genuinely orbits - the same "skip the piloting problem for setup that was never about
        // piloting" reasoning as DebugPlaceShip itself, see World.ShipField.cs).
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        // Matches the station's own live velocity, not just its position - CaptureRadius(40) is
        // tiny next to how far a hosted station moves in even a single tick (~1600+ units at this
        // scale), so a STATIONARY ship placed exactly on it is already outside the radius again by
        // the time the very next Step evaluates the proximity check (confirmed via a scratch trace).
        // Two-sample finite-difference velocity match, same as ApproachBerth/DockAtStation
        // (TestRunner.StationDocking.cs, TestRunner.HelmAndHull.cs) - sampled from the ship's OWN
        // current position (never placed at the station itself before the final placement): doing
        // so even briefly used to arm-and-instantly-clear the defensive battle on that earlier tick
        // (World.Voyage.cs's HasFledTheSector had its own bug then - fixed - but there is still no
        // reason to trigger the real thing twice).
        var tradeStation = world.GalaxyMap.GetPoint("trade-station");
        var sample1 = world.ResolveGalaxyPointPosition(tradeStation);
        world.Step(RealtimeStep); // just advances time - the ship itself stays wherever it already is
        var sample2 = world.ResolveGalaxyPointPosition(tradeStation);
        var stationVelocity = (sample2 - sample1) * (1.0 / RealtimeStep);
        // Anticipates the on-rails "establish" tick's own quirk: the very first Step after
        // DebugSetShipVelocity re-derives Kepler orbital elements from the CURRENT (position,
        // velocity) pair but stamps them with THAT tick's own (already-advanced) timestamp - so it
        // reproduces the input exactly and the ship doesn't actually move at all on this first tick,
        // even though real time passed and the station did move (confirmed via scratch trace: ship
        // pinned exactly at its pre-step position, live station ~1684 units further on). Placing the
        // ship one tick's worth of station-motion AHEAD of `sample2` means the "frozen" anchor and
        // the station's real position coincide by the time this Step's own proximity check runs.
        var anticipated = sample2 + stationVelocity * RealtimeStep;
        world.DebugPlaceShip(anticipated);
        world.DebugSetShipVelocity(stationVelocity);
        world.Step(RealtimeStep);

        return world.IsInBattle;
    }
}
