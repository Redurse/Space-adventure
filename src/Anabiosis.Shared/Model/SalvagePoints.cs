namespace Anabiosis.Shared.Model;

// Cosmoteer-style points of interest that can be looted just by flying up to them once: a storage
// pod, an abandoned ship, a ship graveyard. Generated deterministically per system (same id => same
// layout every session) and appended to the system's own points by StarSystem's constructor.
public static class SalvagePoints
{
    public static bool IsSalvage(GalaxyPointKind kind) =>
        kind is GalaxyPointKind.StoragePod or GalaxyPointKind.ShipGraveyard or GalaxyPointKind.AbandonedShip;

    public readonly record struct Loot(int Credits, int Hyperium);

    private const int MinPerSystem = 2;
    private const int MaxPerSystem = 4;
    private const float MinSpacing = 60f;
    private const float BodyClearanceMargin = 15f;
    // Stays well inside the warp ring (StarSystem.WarpZoneRadiusFraction = 0.92) so salvage never
    // sits where the jump prompt arms.
    private const float MaxRadiusFractionOfHalfField = 0.8f;

    public static Loot LootFor(GalaxyPoint point)
    {
        var random = new Random(AsteroidShape.StableHash(point.Id + "-loot"));
        return point.Kind switch
        {
            GalaxyPointKind.StoragePod => new Loot(random.Next(30, 81), random.Next(0, 3)),
            GalaxyPointKind.AbandonedShip => new Loot(random.Next(60, 151), random.Next(1, 4)),
            GalaxyPointKind.ShipGraveyard => new Loot(random.Next(120, 301), random.Next(1, 5)),
            _ => new Loot(0, 0),
        };
    }

    public static string NameFor(GalaxyPointKind kind) => kind switch
    {
        GalaxyPointKind.StoragePod => "Контейнер с грузом",
        GalaxyPointKind.AbandonedShip => "Брошенный корабль",
        GalaxyPointKind.ShipGraveyard => "Кладбище кораблей",
        _ => "?",
    };

    private static float CaptureRadiusFor(GalaxyPointKind kind) => kind switch
    {
        GalaxyPointKind.StoragePod => 10f,
        GalaxyPointKind.AbandonedShip => 12f,
        _ => 18f,
    };

    public static IReadOnlyList<GalaxyPoint> For(string systemId)
    {
        var bodies = CelestialBodyGenerator.Generate(systemId);
        var byId = bodies.ToDictionary(b => b.Id);
        var star = bodies.Single(b => b.ParentId is null);
        var fieldSize = CelestialBodyGenerator.FieldSize(bodies);
        var center = new Vec2(fieldSize / 2f, fieldSize / 2f);
        var minRadius = CelestialBodyGenerator.ClearanceRadius(star) + 30f;
        var maxRadius = fieldSize / 2f * MaxRadiusFractionOfHalfField;
        var bodyPositions = bodies.Select(b => (Body: b, Position: CelestialBodyGenerator.PositionAt(b, byId) + center)).ToList();

        var random = new Random(AsteroidShape.StableHash(systemId + "-salvage"));
        var count = random.Next(MinPerSystem, MaxPerSystem + 1);
        var kinds = new[] { GalaxyPointKind.StoragePod, GalaxyPointKind.AbandonedShip, GalaxyPointKind.ShipGraveyard };
        var result = new List<GalaxyPoint>();
        if (maxRadius <= minRadius)
            return result;

        for (var i = 0; i < count; i++)
        {
            for (var attempt = 0; attempt < 40; attempt++)
            {
                var angle = random.NextDouble() * 2 * Math.PI;
                var radius = minRadius + random.NextDouble() * (maxRadius - minRadius);
                var position = center + new Vec2(Math.Cos(angle), Math.Sin(angle)) * radius;
                var clearOfBodies = bodyPositions.All(b =>
                    (b.Position - position).Length() >= CelestialBodyGenerator.ClearanceRadius(b.Body) + BodyClearanceMargin);
                var clearOfOthers = result.All(p => (p.Position - position).Length() >= MinSpacing);
                if (!clearOfBodies || !clearOfOthers)
                    continue;

                var kind = kinds[random.Next(kinds.Length)];
                result.Add(new GalaxyPoint($"{systemId}-salvage-{i}", NameFor(kind), position.X, position.Y, kind,
                    CaptureRadius: CaptureRadiusFor(kind)));
                break;
            }
        }
        return result;
    }
}
