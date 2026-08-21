namespace SpaceAdventure.Shared.Model;

// Local, bounded 2D space entered by flying to a GalaxyPointKind.AsteroidField point (M15). The
// ship is piloted freely here instead of auto-travelling in a straight line; asteroids are static
// obstacles the ship (and, once outside via the airlock, a character) can collide with.
public sealed class AsteroidField
{
    public float Width { get; }
    public float Height { get; }
    public IReadOnlyList<Asteroid> Asteroids { get; }
    public IReadOnlyList<OreDeposit> OreDeposits { get; }
    public Vec2 Center => new(Width / 2, Height / 2);

    public AsteroidField(float width, float height, IReadOnlyList<Asteroid> asteroids, IReadOnlyList<OreDeposit> oreDeposits)
    {
        Width = width;
        Height = height;
        Asteroids = asteroids;
        OreDeposits = oreDeposits;
    }

    // Sized well beyond the ship's own ~26x6 hull so there's real room to maneuver between
    // asteroids rather than starting the field pinned against one - also generous enough that a
    // full jetpack burn (EvaAccelerationPerSecond over its whole fuel tank, from World.Eva.cs)
    // can't reach the edge starting from center, which would otherwise clip a drifting
    // character's momentum against the field boundary mid-flight.
    // Shifts the whole hand-placed cluster below (and every hand-authored GalaxyPoint in sol,
    // GalaxyMap.cs) from the old 300x300 field's own centre (150,150) to the new 2400x2400 one's
    // (1200,1200) (M40) - so the recognizable layout everyone already flies around keeps sitting
    // in the middle of the system, with the newly opened-up space radiating outward on every side,
    // rather than getting stranded in one corner of a much bigger field.
    public const float RecenterOffsetM40 = 1050f;

    public static AsteroidField CreateDefault()
    {
        var asteroids = new[]
        {
            new Asteroid("asteroid-1", 80f, 60f, 5f),
            new Asteroid("asteroid-2", 180f, 200f, 8f),
            new Asteroid("asteroid-3", 60f, 220f, 4f),
            new Asteroid("asteroid-4", 220f, 100f, 6f),
            new Asteroid("asteroid-5", 150f, 40f, 4f),
        }.Select(a => a with { X = a.X + RecenterOffsetM40, Y = a.Y + RecenterOffsetM40 }).ToArray();

        // Two or three blocks of ore per asteroid, sitting on its surface (game_design.md Phase 3,
        // M18). Each is cut apart on its own and drops one item, so a rock is worth a couple of
        // minutes of work rather than a couple of keypresses. Written as points near the nominal
        // circle and pulled onto the real outline below.
        var oreDeposits = new[]
        {
            new OreDeposit("ore-1a", "asteroid-1", 85f, 60f, 100f),
            new OreDeposit("ore-1b", "asteroid-1", 80f, 55f, 100f),
            new OreDeposit("ore-1c", "asteroid-1", 75f, 60f, 100f),
            new OreDeposit("ore-2a", "asteroid-2", 188f, 200f, 120f),
            new OreDeposit("ore-2b", "asteroid-2", 184f, 208f, 120f),
            new OreDeposit("ore-2c", "asteroid-2", 176f, 194f, 120f),
            new OreDeposit("ore-3a", "asteroid-3", 64f, 220f, 90f),
            new OreDeposit("ore-3b", "asteroid-3", 58f, 216f, 90f),
            new OreDeposit("ore-4a", "asteroid-4", 226f, 100f, 110f),
            new OreDeposit("ore-4b", "asteroid-4", 220f, 106f, 110f),
            new OreDeposit("ore-4c", "asteroid-4", 214f, 100f, 110f),
            new OreDeposit("ore-5a", "asteroid-5", 154f, 40f, 90f),
            new OreDeposit("ore-5b", "asteroid-5", 148f, 36f, 90f),
        }.Select(d => d with { X = d.X + RecenterOffsetM40, Y = d.Y + RecenterOffsetM40 }).ToArray();

        // Veins are written down as points on the nominal circle, then pulled onto the rock's real
        // outline (AsteroidShape) - otherwise a deposit sits buried inside a spur or floating off a
        // notch, and the miner can't get within arm's reach of it.
        var onTheSurface = oreDeposits.Select(deposit =>
        {
            var rock = asteroids.First(a => a.Id == deposit.AsteroidId);
            var surface = AsteroidShape.SurfacePoint(rock, deposit.Position, 0f);
            return deposit with { X = surface.X, Y = surface.Y };
        }).ToArray();

        // 2400x2400 (M40, game_design.md - 8 minutes edge-to-edge under manual flight at
        // ShipMaxSpeed(5) rather than the ~1 minute a 300x300 field gave) - the cluster above is
        // already recentred (RecenterOffsetM40), so it sits in the middle of the newly opened-up
        // space rather than being redistributed across it; M43's persistent NPC traffic and M44's
        // scanner-revealed contacts are what populate the rest.
        return new AsteroidField(width: 2400f, height: 2400f, asteroids, onTheSurface);
    }
}
