using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // A fresh system's ambient traffic exists from the very first tick (World.NpcShips.cs's own
    // lazy repopulation) and never grows past the fleet's own hard cap, however many stations/
    // hostile factions sol happens to have.
    private static bool World_NpcShips_PopulateOnFirstStepAndStayWithinCap()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);

        var npcShips = world.CreateSnapshot().NpcShips;
        return npcShips.Count > 0 && npcShips.Count <= 8; // World.NpcShips.cs's NpcFleetMaxPerSystem
    }

    // Sol has several stations (home/trade/outpost-gamma/mining-outpost), so a Cargo hull's fixed
    // shuttle run actually has two different ends to alternate between, not one degenerate point.
    private static bool World_NpcShips_CargoShuttlesBetweenBothEndsOfItsRoute()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);

        var cargoId = world.CreateSnapshot().NpcShips.FirstOrDefault(n => n.Kind == NpcShipKind.Cargo)?.Id;
        if (cargoId is null)
            return false; // setup problem - sol has multiple stations, so a Cargo hull must exist

        var stationPositions = world.GalaxyMap.GetSystem("sol").Points
            .Where(p => p.Kind == GalaxyPointKind.Station)
            .Select(p => p.Position)
            .ToArray();
        var visitedStations = new HashSet<int>();

        // A full round trip between two of sol's stations comfortably fits in a few real minutes
        // at NpcCargoSpeed(4) - 20 simulated minutes is generous slack, not a tight budget.
        for (var i = 0; i < 20 * 60 * 30 && visitedStations.Count < 2; i++)
        {
            world.Step(RealtimeStep);
            var cargo = world.CreateSnapshot().NpcShips.FirstOrDefault(n => n.Id == cargoId);
            if (cargo is null)
                return false; // ambient hulls never die on their own
            var here = new Vec2(cargo.X, cargo.Y);
            for (var s = 0; s < stationPositions.Length; s++)
                if ((stationPositions[s] - here).Length() < 16f)
                    visitedStations.Add(s);
        }

        return visitedStations.Count >= 2;
    }

    // The whole point of a persistent Military hull (game_design.md, M43): once its faction
    // actually hates the crew, running into it in open space is a real fight - the same squadron/
    // projectile/boarding machinery a hostile sector already runs, triggered by proximity to an
    // ambient hull instead of a fixed marker - and losing that hull costs the faction standing
    // exactly like any other kill.
    private static bool World_NpcShips_MilitaryTurnsHostileAndFightingItCostsStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);

        var npcId = world.CreateSnapshot().NpcShips
            .FirstOrDefault(n => n.Kind == NpcShipKind.Military && n.FactionId == FactionId.FreeFleet)?.Id;
        if (npcId is null)
            return false; // setup problem - sol's FreeFleet sectors mean a FreeFleet patrol must exist

        GrindStandingHostile(world, "sector-alpha", FactionId.FreeFleet);
        var standingBeforeThisKill = world.GetStanding(FactionId.FreeFleet);
        if (standingBeforeThisKill > FactionDefinitions.HostileThreshold)
            return false; // setup problem, not the behavior under test

        // GrindStandingHostile's own last step docks at home to repair - cast off and place the
        // ship right on wherever the (still-patrolling) NPC has drifted to since, rather than
        // flying there for real (World.DebugPlaceShip's own doc comment - this test is about the
        // hostility/combat hookup, not piloting).
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        var npc = world.CreateSnapshot().NpcShips.FirstOrDefault(n => n.Id == npcId);
        if (npc is null)
            return false; // setup problem - the grind above must never touch the ambient fleet itself
        world.DebugPlaceShip(new Vec2(npc.X, npc.Y));
        world.Step(RealtimeStep);

        if (!world.IsInBattle || world.CreateSnapshot().NpcShips.Any(n => n.Id == npcId))
            return false; // never engaged, or engaged without converting out of the ambient list

        FireBowTurretUntilEnemyDefeated(world, 1);
        for (var i = 0; i < 30 && world.IsInBattle; i++)
            world.Step(RealtimeStep); // let StepVoyage resolve the kill and settle the standing

        return !world.IsInBattle && world.GetStanding(FactionId.FreeFleet) < standingBeforeThisKill;
    }
}
