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
    // The field doubled again on top of that (M48 - "в 2 раза больше по длине и ширине"), so the
    // cluster needs to shift the extra distance from the old centre (1200,1200) to the new one
    // (2400,2400) as well: RecenterOffsetM40 + (2400-1200) = 2250.
    public const float RecenterOffsetM48 = RecenterOffsetM40 + 1200f;

    // The field size every hand-placed sol coordinate above (and every hand-authored GalaxyPoint,
    // GalaxyMap.cs) was actually authored against, right up through M48's own doubling - the
    // baseline M50's own real, body-driven sizing (SolFieldSize below) is measured against to
    // rescale that whole layout by a single factor, the same "everything just got bigger, same
    // relative shape" migration RecenterOffsetM40/M48 already did twice, just computed instead of
    // hand-picked this time since a real generated system's own size isn't a round number.
    public const float LegacyFieldSize = 4800f;

    // sol's own real body layout (M50) - generated once, reused both for sizing sol's own field
    // below and by GalaxyMap.cs to rescale sol's hand-authored point positions by the same factor.
    private static readonly IReadOnlyList<CelestialBody> SolBodies = CelestialBodyGenerator.Generate("sol");
    public static float SolFieldSize => CelestialBodyGenerator.FieldSize(SolBodies);
    public static float SolRescale => SolFieldSize / LegacyFieldSize;

    // A small buffer ON TOP of a body's own clearance radius (CelestialBodyGenerator.ClearanceRadius)
    // - shared with GalaxyMap.cs's own identical clearance pass over every OTHER hand-placed sol
    // point, so both agree on the exact same margin. M59 follow-up - rescaled down from 1000f
    // alongside the rest of the map (small, Cosmoteer-scale system instead of KSP-real), no longer
    // tied to a gravity SOI (M59 removed the gravity model entirely) - purely a visual/navigational
    // buffer now.
    public const float OrbitBandMargin = 30f;

    // Where the hand-placed cluster below (and GalaxyMap.cs's own asteroid-field-epsilon marker)
    // actually lands in absolute sol-field coordinates - the CLOSEST point to the field's own
    // centre that's still clear of every real body's own orbit band (star first, then each planet
    // in increasing orbit order, always pushed outward - see GalaxyMap.cs's own ClearOfEveryOrbit
    // for why "always outward" specifically, rather than whichever edge happens to be nearer).
    // Originally sat exactly at the centre ("alongside the sun it orbits") back when the sun was
    // purely decorative (pre-M50) - kept as close to that as sol's own real body layout allows,
    // rather than pushed all the way out past every planet, since this is also the one fixed spot
    // a large share of the test suite parks a ship at to test helm/EVA/mining/collision mechanics
    // in isolation (EnterAsteroidFieldAndManHelm) and a genuinely cross-system flight there would
    // break every budget/steering assumption those tests already make.
    public static Vec2 ClusterCenter => SafePointPositionFor(SolBodies, SolFieldSize);

    // General version of the same "closest-to-centre point that's still clear of every real body's
    // own orbit band" search above, for ANY system rather than just sol - every hand-authored
    // single-point stub system (alpha-centauri etc.) and every procedural system (GalaxyMap.cs)
    // used to place their one point at a literal (2400,2400), which only ever made sense back when
    // every system shared the same fixed 4800x4800 field. Once M50 sized each system's own field to
    // its own real generated bodies, that literal coordinate could land outside the real body
    // cluster entirely (a much bigger field) or inside the star itself (a much smaller one) -
    // exactly the "objects outside the solar system" bug this replaces.
    public static Vec2 SafePointPosition(string systemId)
    {
        var bodies = CelestialBodyGenerator.Generate(systemId);
        return SafePointPositionFor(bodies, CelestialBodyGenerator.FieldSize(bodies));
    }

    private static Vec2 SafePointPositionFor(IReadOnlyList<CelestialBody> bodies, float fieldSize)
    {
        var star = bodies.Single(b => b.ParentId is null);
        var planets = bodies.Where(b => b.ParentId == star.Id).OrderBy(p => p.OrbitRadius);
        var radius = CelestialBodyGenerator.ClearanceRadius(star) + OrbitBandMargin;
        foreach (var planet in planets)
        {
            // ClearanceRadius(planet) alone already exceeds planet.Radius + moonReach by
            // construction (CelestialBodyGenerator's own MoonContainmentFactor keeps every moon's
            // own clearance buffer, let alone its orbit radius, safely inside its parent's) - no
            // separate moon-reach term needed.
            var halfBand = CelestialBodyGenerator.ClearanceRadius(planet) + OrbitBandMargin;
            var lower = MathF.Max(0f, planet.OrbitRadius - halfBand);
            var upper = planet.OrbitRadius + halfBand;
            if (radius >= lower && radius < upper)
                radius = upper;
        }

        var half = fieldSize / 2f;
        // A different bearing from TestRunner.Gravity.cs's own FarFieldClearPoint (0.6,0.8) - both
        // pick a point "safely clear of every real body", and sol's own cluster additionally has
        // real, static asteroids sitting at it (the hand-placed cluster below); sharing a bearing
        // would spawn a gravity test's ship inside that same rock field by coincidence.
        return new Vec2(half, half) + new Vec2(-0.8f, 0.6f) * radius;
    }

    public static AsteroidField CreateDefault()
    {
        // The hand-placed cluster below was authored around its own local (150,150) in a small
        // (300x300) space - shifted here onto ClusterCenter (offset clear of the real star, M50)
        // instead of the field's bare centre, the same "recentre the old fixed layout onto wherever
        // it actually needs to sit now" migration RecenterOffsetM40/M48 already did twice before.
        var clusterCenter = ClusterCenter;
        var asteroids = new[]
        {
            new Asteroid("asteroid-1", 80f, 60f, 5f),
            new Asteroid("asteroid-2", 180f, 200f, 8f),
            new Asteroid("asteroid-3", 60f, 220f, 4f),
            new Asteroid("asteroid-4", 220f, 100f, 6f),
            new Asteroid("asteroid-5", 150f, 40f, 4f),
        }.Select(a => a with { X = a.X - 150.0 + clusterCenter.X, Y = a.Y - 150.0 + clusterCenter.Y }).ToArray();

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
        }.Select(d => d with { X = d.X - 150.0 + clusterCenter.X, Y = d.Y - 150.0 + clusterCenter.Y }).ToArray();

        // Veins are written down as points on the nominal circle, then pulled onto the rock's real
        // outline (AsteroidShape) - otherwise a deposit sits buried inside a spur or floating off a
        // notch, and the miner can't get within arm's reach of it.
        var onTheSurface = oreDeposits.Select(deposit =>
        {
            var rock = asteroids.First(a => a.Id == deposit.AsteroidId);
            var surface = AsteroidShape.SurfacePoint(rock, deposit.Position, 0f);
            return deposit with { X = surface.X, Y = surface.Y };
        }).ToArray();

        // Real-body-sized (M50), not a fixed 4800x4800 any more - SolFieldSize is however big
        // sol's own generated star/planets/moons actually need (CelestialBodyGenerator.FieldSize).
        // The cluster above is already recentred onto this exact size, so it sits in the middle of
        // the newly opened-up space rather than being redistributed across it; M43's persistent
        // NPC traffic and M44's scanner-revealed contacts are what populate the rest. sol's own
        // real planetary belts are layered in on top, exactly the same way any other system's are
        // - this hand-placed cluster just keeps existing alongside them rather than being replaced.
        var belts = GenerateBeltAsteroids("sol");
        return new AsteroidField(width: SolFieldSize, height: SolFieldSize,
            asteroids.Concat(belts).ToArray(), onTheSurface);
    }

    // Every OTHER system (the 5 hand-authored stubs and the 194 procedural ones, GalaxyMap.cs) -
    // no hand-placed cluster of its own, just whatever CelestialBodyGenerator rolled for it, sized
    // to actually fit its own real planets. Each system gets its OWN distinct AsteroidField
    // instance (not a shared one) so its own belt content can't leak into any other system's.
    public static AsteroidField CreateForSystem(string systemId)
    {
        var size = CelestialBodyGenerator.FieldSize(CelestialBodyGenerator.Generate(systemId));
        return new AsteroidField(width: size, height: size, GenerateBeltAsteroids(systemId).ToArray(), Array.Empty<OreDeposit>());
    }

    // M48 - "с шансом в 25 процентов между любыми 2 орбитами спанвился пояс астероидов... очень
    // много астероидов разного размера, но не слишком плотно чтобы можно было пролететь". No ore
    // (World.cs's _oreDepositHp is only ever seeded from whichever system is current at world
    // construction/ship-purchase time, never re-seeded on warp - a deposit in a system that isn't
    // the starting one would silently read as already-mined-out forever) - these belts are a
    // flight hazard/visual feature, not new mining content.
    private const int BeltAsteroidsMin = 40;
    private const int BeltAsteroidsMax = 70;
    private const float BeltAsteroidRadiusMin = 5f;
    private const float BeltAsteroidRadiusMax = 35f;

    private static List<Asteroid> GenerateBeltAsteroids(string systemId)
    {
        var bodies = CelestialBodyGenerator.Generate(systemId);
        var star = bodies.Single(b => b.ParentId is null);
        var planets = bodies.Where(b => b.ParentId == star.Id).OrderBy(b => b.OrbitRadius).ToList();
        var beltGaps = CelestialBodyGenerator.BeltGaps(systemId, planets.Count);
        var fieldSize = CelestialBodyGenerator.FieldSize(bodies);
        var center = new Vec2(fieldSize / 2f, fieldSize / 2f);
        var result = new List<Asteroid>();

        for (var gap = 0; gap < beltGaps.Count; gap++)
        {
            if (!beltGaps[gap])
                continue;

            // A belt's own random stream, independent of the orbit-layout roll above and of every
            // other belt/system - AsteroidShape.StableHash again, just salted with the gap index
            // so two belts in the same system don't draw identical rocks.
            var random = new Random(AsteroidShape.StableHash($"{systemId}-belt-{gap}"));
            var innerRadius = planets[gap].OrbitRadius;
            var outerRadius = planets[gap + 1].OrbitRadius;
            var count = random.Next(BeltAsteroidsMin, BeltAsteroidsMax + 1);

            for (var i = 0; i < count; i++)
            {
                var angle = (float)(random.NextDouble() * 2 * Math.PI);
                var radius = innerRadius + (float)random.NextDouble() * (outerRadius - innerRadius);
                var size = BeltAsteroidRadiusMin + (float)random.NextDouble() * (BeltAsteroidRadiusMax - BeltAsteroidRadiusMin);
                var position = center + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                result.Add(new Asteroid($"belt-{systemId}-{gap}-{i}", position.X, position.Y, size));
            }
        }

        return result;
    }
}
