namespace Anabiosis.Shared.Model;

// Rolls one system's hidden places (PointOfInterest.cs) from the game's world seed and the system's id.
// The same pair always gives the same layout - that is what makes a visited system "stay as it was"
// without storing the layout itself in the save (only the seed and what was found are stored).
//
// worldSeed 0 is the fixed, test-friendly world every plain `new World()` builds: it gets the loot
// finds but NO asteroid clusters. Clusters are solid rocks added to the system's field, and hundreds of
// existing tests fly fixed routes through sol with little or no obstacle avoidance - new rocks on those
// routes would break them for reasons that have nothing to do with what they test. A real game always
// has a non-zero seed (GameServer).
public static class PoiGenerator
{
    public sealed record Result(
        IReadOnlyList<PointOfInterest> Pois, IReadOnlyList<Asteroid> Asteroids, IReadOnlyList<OreDeposit> OreDeposits);

    // Stays well inside the warp ring (StarSystem.WarpZoneRadiusFraction = 0.92) so nothing sits where
    // the jump prompt arms.
    private const float MaxRadiusFractionOfHalfField = 0.8f;
    private const float BodyClearanceMargin = 15f;
    private const float SpacingBetweenPlaces = 60f;
    // Charted points (stations, hostile sectors): a find never sits on a berth or its approach.
    private const float ClearanceFromChartedPoints = 180f;

    public static Result Generate(int worldSeed, string systemId, IReadOnlyList<GalaxyPoint> chartedPoints)
    {
        var bodies = CelestialBodyGenerator.Generate(systemId);
        var byId = bodies.ToDictionary(b => b.Id);
        var star = bodies.Single(b => b.ParentId is null);
        var fieldSize = CelestialBodyGenerator.FieldSize(bodies);
        var center = new Vec2(fieldSize / 2f, fieldSize / 2f);
        var minRadius = CelestialBodyGenerator.ClearanceRadius(star) + 30f;
        var maxRadius = fieldSize / 2f * MaxRadiusFractionOfHalfField;
        var bodyPositions = bodies.Select(b => (Body: b, Position: CelestialBodyGenerator.PositionAt(b, byId) + center)).ToList();

        var pois = new List<PointOfInterest>();
        var asteroids = new List<Asteroid>();
        var deposits = new List<OreDeposit>();
        if (maxRadius <= minRadius)
            return new Result(pois, asteroids, deposits);

        var random = new Random(AsteroidShape.StableHash($"{worldSeed}-{systemId}-poi"));

        Vec2? Place(float radius)
        {
            for (var attempt = 0; attempt < 60; attempt++)
            {
                var angle = random.NextDouble() * 2 * Math.PI;
                var distance = minRadius + random.NextDouble() * (maxRadius - minRadius);
                var position = center + new Vec2(Math.Cos(angle), Math.Sin(angle)) * distance;
                if (bodyPositions.Any(b => (b.Position - position).Length() < CelestialBodyGenerator.ClearanceRadius(b.Body) + BodyClearanceMargin + radius))
                    continue;
                if (pois.Any(p => (p.Position - position).Length() < p.Radius + radius + SpacingBetweenPlaces))
                    continue;
                if (chartedPoints.Any(p => (p.Position - position).Length() < ClearanceFromChartedPoints + radius))
                    continue;
                return position;
            }
            return null;
        }

        var lootKinds = new[] { PoiKind.StoragePod, PoiKind.AbandonedShip, PoiKind.ShipGraveyard };
        var lootCount = worldSeed == 0 ? random.Next(2, 5) : random.Next(3, 7);
        for (var i = 0; i < lootCount; i++)
        {
            var kind = lootKinds[random.Next(lootKinds.Length)];
            var radius = kind switch { PoiKind.StoragePod => 10f, PoiKind.AbandonedShip => 12f, _ => 18f };
            if (Place(radius) is { } at)
                pois.Add(new PointOfInterest($"{systemId}-find-{i}", kind, at.X, at.Y, radius));
        }

        // Wrecks are real hulls that appear in the field when the ship comes near (World.Wrecks.cs) - like the
        // clusters, not in the fixed seed-0 world, where a surprise hull would get in the way of scripted flights.
        var wreckCount = worldSeed == 0 ? 0 : random.Next(1, 3);
        for (var i = 0; i < wreckCount; i++)
            if (Place(45f) is { } wreckAt)
                pois.Add(new PointOfInterest($"{systemId}-wreck-{i}", PoiKind.Wreck, wreckAt.X, wreckAt.Y, 45f));

        // At most one dead station per system, in two systems out of three; again not in the seed-0 world (it
        // would become the "nearest station" for scripted flights that pass it).
        if (worldSeed != 0 && random.NextDouble() < 0.67 && Place(140f) is { } stationAt)
            pois.Add(new PointOfInterest($"{systemId}-derelict-station", PoiKind.AbandonedStation, stationAt.X, stationAt.Y, 40f));

        var clusterCount = worldSeed == 0 ? 0 : random.Next(2, 5);
        for (var i = 0; i < clusterCount; i++)
        {
            var radius = 70f + (float)random.NextDouble() * 70f;
            if (Place(radius) is not { } at)
                continue;
            var poi = new PointOfInterest($"{systemId}-cluster-{i}", PoiKind.AsteroidCluster, at.X, at.Y, radius);
            pois.Add(poi);
            AddClusterRocks(poi, random, asteroids, deposits);
        }

        return new Result(pois, asteroids, deposits);
    }

    // 10-22 rocks scattered through the cluster's disc, kept apart so a ship can thread between them. Most
    // carry ore: a "vein" cluster (two in three) is all one ore, the rest are mixed like a belt.
    private static void AddClusterRocks(PointOfInterest poi, Random random, List<Asteroid> asteroids, List<OreDeposit> deposits)
    {
        var veinOre = random.NextDouble() < 0.66 ? AsteroidOre.RollOre(random) : (ItemType?)null;
        var count = random.Next(10, 23);
        var placed = new List<Asteroid>();
        for (var i = 0; i < count; i++)
        {
            var size = 6f + (float)random.NextDouble() * 16f;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var angle = random.NextDouble() * 2 * Math.PI;
                var distance = Math.Sqrt(random.NextDouble()) * (poi.Radius - size);
                var position = poi.Position + new Vec2(Math.Cos(angle), Math.Sin(angle)) * distance;
                if (placed.Any(r => (r.Position - position).Length() < r.Radius + size + 14f))
                    continue;
                var rock = new Asteroid($"poi-{poi.Id}-{i}", position.X, position.Y, size);
                placed.Add(rock);
                if (random.NextDouble() < 0.75)
                    AsteroidOre.AddBlocks(deposits, rock, size >= 15f ? random.Next(2, 4) : random.Next(1, 3), random,
                        () => veinOre ?? AsteroidOre.RollOre(random));
                break;
            }
        }
        asteroids.AddRange(placed);
    }
}
