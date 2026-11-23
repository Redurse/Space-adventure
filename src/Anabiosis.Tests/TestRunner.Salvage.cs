using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Hidden places and the fog of war (PointOfInterest.cs, PoiGenerator.cs, FogGrid.cs, World.Exploration.cs) -
// the old "salvage points" tests, moved onto the scheme that replaced them.
internal static partial class TestRunner
{
    private static bool Poi_EverySystem_IsDeterministicPerSeed_AndClearOfBodies()
    {
        foreach (var systemId in new[] { "sol", "alpha-centauri", "sys-001", "sys-042" })
        foreach (var seed in new[] { 0, 7, 12345 })
        {
            var first = PoiGenerator.Generate(seed, systemId, Array.Empty<GalaxyPoint>());
            var second = PoiGenerator.Generate(seed, systemId, Array.Empty<GalaxyPoint>());
            if (first.Pois.Count < 1 || !first.Pois.SequenceEqual(second.Pois) || !first.Asteroids.SequenceEqual(second.Asteroids))
                return false;

            var bodies = CelestialBodyGenerator.Generate(systemId);
            var byId = bodies.ToDictionary(b => b.Id);
            var half = CelestialBodyGenerator.FieldSize(bodies) / 2f;
            var center = new Vec2(half, half);
            foreach (var poi in first.Pois)
            foreach (var body in bodies)
                if ((CelestialBodyGenerator.PositionAt(body, byId) + center - poi.Position).Length() < body.Radius + poi.Radius)
                    return false;
        }
        return true;
    }

    // A new game is a new layout; the fixed test world (seed 0) has finds but no extra rocks.
    private static bool Poi_DifferentSeeds_GiveDifferentPlaces_AndSeedZeroHasNoClusters()
    {
        var a = PoiGenerator.Generate(101, "sol", Array.Empty<GalaxyPoint>());
        var b = PoiGenerator.Generate(202, "sol", Array.Empty<GalaxyPoint>());
        var zero = PoiGenerator.Generate(0, "sol", Array.Empty<GalaxyPoint>());
        return !a.Pois.Select(p => p.Position).SequenceEqual(b.Pois.Select(p => p.Position))
            && zero.Asteroids.Count == 0 && zero.Pois.All(p => p.Kind != PoiKind.AsteroidCluster);
    }

    private static bool Poi_AsteroidCluster_RocksSitInsideItsRadius_AndCarryOre()
    {
        for (var seed = 1; seed < 40; seed++)
        {
            var places = PoiGenerator.Generate(seed, "sol", Array.Empty<GalaxyPoint>());
            var cluster = places.Pois.FirstOrDefault(p => p.Kind == PoiKind.AsteroidCluster);
            if (cluster is null)
                continue;
            var rocks = places.Asteroids.Where(r => r.Id.StartsWith($"poi-{cluster.Id}-")).ToList();
            var rockIds = rocks.Select(r => r.Id).ToHashSet();
            return rocks.Count >= 5
                && rocks.All(r => (r.Position - cluster.Position).Length() + r.Radius <= cluster.Radius + 0.01f)
                && places.OreDeposits.Any(d => rockIds.Contains(d.AsteroidId));
        }
        return false; // no seed in 1..39 rolled a cluster at all
    }

    // The world really has them: a seeded game carries the cluster rocks and their ore in the system field.
    private static bool World_Exploration_SeededWorld_HasClusterRocksInItsField()
    {
        var world = new World(worldSeed: 4242);
        var system = world.GalaxyMap.GetSystem("sol");
        var clusters = system.Pois.Where(p => p.Kind == PoiKind.AsteroidCluster).ToList();
        if (clusters.Count == 0)
            return false;
        var rocks = system.Field.Asteroids.Where(a => a.Id.StartsWith("poi-")).ToList();
        var rockIds = rocks.Select(r => r.Id).ToHashSet();
        return rocks.Count >= 5 && system.Field.OreDeposits.Any(d => rockIds.Contains(d.AsteroidId))
            && new World().GalaxyMap.GetSystem("sol").Field.Asteroids.All(a => !a.Id.StartsWith("poi-"));
    }

    private static World UndockedWorld(int worldSeed = 0)
    {
        var world = new World(worldSeed: worldSeed);
        world.SpawnCharacter(1);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        return world;
    }

    private static bool World_Exploration_FlyingIntoAFind_PaysOutOnceOnly()
    {
        var world = UndockedWorld();
        var poi = world.GalaxyMap.GetSystem("sol").Pois.First(p => PoiCatalog.IsLootOnApproach(p.Kind));
        var loot = PoiCatalog.LootFor(poi);
        var creditsBefore = world.Credits;
        var fuelBefore = world.HyperiumAboard;

        world.DebugPlaceShip(poi.Position);
        world.Step(RealtimeStep);
        var snapshot = world.CreateSnapshot();
        var paidOnce = world.Credits == creditsBefore + loot.Credits
            && world.HyperiumAboard == fuelBefore + loot.Hyperium
            && snapshot.PointsOfInterest!.Single(p => p.Id == poi.Id) is { Looted: true, Identified: true }
            && snapshot.SalvageNotice is not null;

        world.Step(RealtimeStep);
        return paidOnce && world.Credits == creditsBefore + loot.Credits;
    }

    // Nothing is known at the start; the fog clears around the ship; a place is identified in sensor range.
    private static bool World_Exploration_PlacesStartHidden_AndAreIdentifiedInSensorRange()
    {
        var world = UndockedWorld();
        world.Step(RealtimeStep);
        var before = world.CreateSnapshot();
        var poi = world.GalaxyMap.GetSystem("sol").Pois
            .OrderByDescending(p => (p.Position - before.Voyage.ShipMapPosition).Length()).First();
        if (before.PointsOfInterest!.Any(p => p.Id == poi.Id) || world.IsFogRevealedAt(poi.Position)
            || !world.IsFogRevealedAt(before.Voyage.ShipMapPosition) || before.Fog is null)
            return false;

        // Just inside sensor range, but outside the radius of the place itself: seen, not looted.
        world.DebugPlaceShip(poi.Position + new Vec2(World.ShipSensorRadius * 0.8, 0));
        world.Step(RealtimeStep);
        var after = world.CreateSnapshot();
        return after.PointsOfInterest!.Single(p => p.Id == poi.Id) is { Identified: true, Looted: false }
            && world.IsFogRevealedAt(poi.Position)
            && !ReferenceEquals(before.Fog.Bits, after.Fog!.Bits);
    }

    // A sonar ping along the right bearing clears the fog out to the place and marks it as an unknown signal -
    // what it is stays hidden until the ship is close. A ping the other way finds nothing.
    private static bool World_Exploration_SonarClearsFogAndFindsUnknownSignals()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);
        MoveToNavigationConsole(world); // walked while still docked, the way the scanner tests do it
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        var poi = world.GalaxyMap.GetSystem("sol").Pois.First();
        var from = poi.Position + new Vec2(-700, 0); // the place is 700 units due east (+X) of the ship
        world.DebugPlaceShip(from);
        world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, ScannerSweepDegrees: 180f, ScannerPingPressed: true));
        world.Step(RealtimeStep);
        if (world.CreateSnapshot().PointsOfInterest!.Any(p => p.Id == poi.Id) || world.IsFogRevealedAt(poi.Position))
            return false;

        WaitOutScannerCooldown(world);
        world.ApplyCommand(1, new ClientCommand(1, ScannerSweepDegrees: 0f, ScannerPingPressed: true));
        world.Step(RealtimeStep);
        var state = world.CreateSnapshot().PointsOfInterest!.SingleOrDefault(p => p.Id == poi.Id);
        return state is { Identified: false, Kind: null } && state.Name == PoiCatalog.UnknownSignalName
            && world.IsFogRevealedAt(poi.Position)
            && world.IsFogRevealedAt(from + new Vec2(350, 0));
    }

    // "После первого посещения система уже не изменяется": the seed, the fog and what was found survive a save.
    private static bool World_Exploration_SurvivesSaveAndLoad()
    {
        var world = UndockedWorld(worldSeed: 4242);
        var system = world.GalaxyMap.GetSystem("sol");
        var find = system.Pois.First(p => PoiCatalog.IsLootOnApproach(p.Kind));
        world.DebugPlaceShip(find.Position);
        world.Step(RealtimeStep);
        // through the real save file format
        var path = Path.Combine(Path.GetTempPath(), $"anabiosis-exploration-test-{Guid.NewGuid():N}.json");
        try
        {
            SaveStore.Save(world.CreateSave(), path);
            var loaded = SaveStore.Load(path)!;
            var restored = new World(worldSeed: loaded.WorldSeed ?? -1);
            restored.SpawnCharacter(1);
            restored.ApplySave(loaded);
            return loaded.WorldSeed == 4242
                && restored.GalaxyMap.GetSystem("sol").Pois.SequenceEqual(system.Pois)
                && restored.LootedPoiIds.Contains(find.Id) && restored.IdentifiedPoiIds.Contains(find.Id)
                && restored.IsFogRevealedAt(find.Position);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool FogGrid_RevealCircleAndCone_MarkTheRightCells()
    {
        var fog = new FogGrid(6400f); // 50-unit cells
        var changed = fog.RevealCircle(new Vec2(3200, 3200), 300f);
        var again = fog.RevealCircle(new Vec2(3200, 3200), 300f);
        var cone = new FogGrid(6400f);
        cone.RevealCone(new Vec2(1000, 3200), 0f, 12f, 1000f);
        var copy = new FogGrid(6400f, fog.ToBits());
        return changed && !again
            && fog.IsRevealed(new Vec2(3200, 3200)) && fog.IsRevealed(new Vec2(3420, 3200)) && !fog.IsRevealed(new Vec2(3700, 3200))
            && cone.IsRevealed(new Vec2(1900, 3200)) && !cone.IsRevealed(new Vec2(2200, 3200))   // in range / past it
            && !cone.IsRevealed(new Vec2(1500, 3600)) && !cone.IsRevealed(new Vec2(600, 3200))    // off the bearing / behind
            && copy.IsRevealed(new Vec2(3200, 3200)) && !copy.IsRevealed(new Vec2(100, 100));
    }
}
