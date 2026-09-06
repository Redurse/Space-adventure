namespace Anabiosis.Shared.Model;

// A landable body's own local ground (M55 - "сесть на планету... собственный ландшафт"),
// entered by landing (World.PlanetLanding.cs) - a separate, unrelated-scale bounded 2D space,
// the same relationship a docked Station's interior has to the field it's parked in rather than
// a literal zoomed-in patch of the body's own (KSP-scale) sphere. Position is never stored: like
// CelestialBodyGenerator.Generate/AsteroidField.CreateForSystem, this is a pure function of the
// body's own id, so server and client always derive the identical surface without it ever
// crossing the network.
public static class PlanetSurface
{
    public const float Width = 300f;
    public const float Height = 300f;
    public static Vec2 Center => new(Width / 2f, Height / 2f);

    private const int ObstaclesMin = 15;
    private const int ObstaclesMax = 30;
    private const float ObstacleRadiusMin = 4f;
    private const float ObstacleRadiusMax = 18f;

    // Kept clear of rocks so a freshly landed ship (TryLandOnPlanet, World.PlanetLanding.cs)
    // never spawns wedged into one - generous next to the ship's own hull footprint.
    private const float LandingPadClearRadius = 30f;

    // Obstacles reuse Asteroid/AsteroidShape wholesale (same collidable-rock model already used
    // by AsteroidField) rather than a new shape type - a rock is a rock whether it's floating in
    // a belt or sitting on the ground.
    public static IReadOnlyList<Asteroid> Generate(string bodyId)
    {
        var random = new Random(AsteroidShape.StableHash($"surface-{bodyId}"));
        var count = random.Next(ObstaclesMin, ObstaclesMax + 1);
        var result = new List<Asteroid>(count);
        var center = Center;

        for (var i = 0; i < count; i++)
        {
            Vec2 position;
            do
            {
                position = new Vec2((float)random.NextDouble() * Width, (float)random.NextDouble() * Height);
            } while ((position - center).Length() < LandingPadClearRadius);

            var radius = ObstacleRadiusMin + (float)random.NextDouble() * (ObstacleRadiusMax - ObstacleRadiusMin);
            var (posX, posY) = position.AsFloat();
            result.Add(new Asteroid($"surface-{bodyId}-{i}", posX, posY, radius));
        }

        return result;
    }
}
