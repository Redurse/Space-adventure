namespace SpaceAdventure.Shared.Model;

// A real, physical body in a star system's own local field space (M50 - "реальные масштабы
// солнечной системы... планеты были огромными"), replacing SystemOrbits.cs's old purely-decorative
// orbit rings (no position in the real field, not interactive, only ever used to scatter belt
// asteroids and to draw a fake client-side orbiting dot).
//
// M59 - "убрать орбитальную механику, вернуть статичную карту в духе Cosmoteer": bodies no longer
// move at all - a body's position is fixed the moment Generate rolls it (OrbitRadius + PhaseOffset,
// same procedural placement math as before), not a function of time any more. AngularSpeed/
// Eccentricity/ArgumentOfPeriapsis (M53/M57's real Kepler-ellipse motion) are gone along with the
// gravity model they served - PositionAt below is now a trivial, still-deterministic lookup, not a
// per-tick orbital solve.
public enum BodyMassTier { Moon, Rocky, IceGiant, GasGiant, Star }

public sealed record CelestialBody(
    string Id,
    string? ParentId,      // null only for the system's own star - every other body orbits it directly or via a parent planet
    float OrbitRadius,     // fixed distance from ParentId's own centre; 0 for the star itself
    float PhaseOffset,     // fixed angle (radians) from ParentId's own centre - where this body sits, forever
    float Radius,          // both visual size and physical collision footprint (HullOverlapsCelestialBody)
    BodyMassTier MassTier);

public static class CelestialBodyGenerator
{
    public const int MinPlanets = 3;
    public const int MaxPlanets = 6;

    // M59 - reverted from M56's literal KSP-metre scale (star ~200-300 million, moons up to 300
    // thousand) back down to a small, Cosmoteer-adjacent scale a ship can actually cross in seconds,
    // not simulated days - the same order of magnitude the field itself sat at before M52's push to
    // real-world scale (M40/M97-102's 4800x4800 field).
    private const float StarRadiusMin = 150f;
    private const float StarRadiusMax = 220f;
    private const float RockyRadiusMin = 15f;
    private const float RockyRadiusMax = 45f;
    private const float GiantRadiusMin = 60f;
    private const float GiantRadiusMax = 100f;
    private const float MoonRadiusMin = 5f;
    private const float MoonRadiusMax = 15f;

    // M59 follow-up - replaces GravityModel.SoiRadius (deleted alongside the rest of the gravity
    // model): without gravity there's no physical sphere of influence to keep clear of a neighbour,
    // just a plain visual/flight-room buffer scaled off the body's own radius, so orbits still read
    // as "spaced out", not stacked on top of each other. Every place that used to compute a body's
    // own SoiRadius for spacing/containment purposes now calls this instead - purely cosmetic/
    // navigational, not physical.
    private const float ClearanceRadiusFactor = 3f;
    // Public - GalaxyMap.cs's own station/point placement needs the same buffer to keep hand-placed
    // points clear of a body's footprint, the same role GravityModel.SoiRadius played before.
    public static float ClearanceRadius(CelestialBody body) => body.Radius * ClearanceRadiusFactor;

    // Orbit spacing no longer has to clear a gravity SOI (M52's patched-conics is gone) - just a
    // margin on top of the plain clearance-radius buffer above, so floating-point placement never
    // lets two neighbours' buffers actually touch.
    private const float OrbitClearanceMarginFactor = 1.25f;

    // Real solar systems grow multiplicatively from orbit to orbit, not by a fixed additive gap
    // (Mercury->Venus->Earth->Mars step up by roughly 1.4-1.9x each; the jump from the inner rocky
    // planets out to the first gas giant is a much bigger ~2.5-3.5x) - unchanged from before M56's
    // KSP-literal push, this is pure layout structure, independent of absolute scale.
    private const float PlanetGrowthFactorMin = 1.3f;
    private const float PlanetGrowthFactorMax = 2.0f;
    private const float TierJumpGrowthFactorMin = 1.5f;
    private const float TierJumpGrowthFactorMax = 2.0f;
    private const float MoonGrowthFactorMin = 1.3f;
    private const float MoonGrowthFactorMax = 2.0f;
    // A moon's own clearance buffer must stay fully inside its parent planet's own allotted space -
    // otherwise two planets' moons could end up contesting the same territory the way two planets
    // themselves are barred from doing above. Comfortably under 1.0 rather than right up against it.
    private const float MoonContainmentFactor = 0.85f;

    private const float FirstOrbitToOwnRadiusFactorMin = 10f;
    private const float FirstOrbitToOwnRadiusFactorMax = 18f;
    private const float MoonOrbitRadiusMinFactor = 4f;
    private const double GasGiantMoonChance = 0.8;
    private const double RockyMoonChance = 0.35;
    private const double RockyTierChance = 0.55;
    private const double IceGiantTierChance = 0.8; // cumulative - the remainder rolls GasGiant

    // M48's old "с шансом в 25 процентов между любыми 2 орбитами спанвился пояс астероидов",
    // carried over unchanged from SystemOrbits.cs.
    public const float BeltChance = 0.25f;

    private static bool IsGiant(BodyMassTier tier) => tier is BodyMassTier.IceGiant or BodyMassTier.GasGiant;

    // Deterministic from the system id alone (AsteroidShape.StableHash - not string.GetHashCode,
    // which is randomised per process) - costs nothing to send over the wire, server and client
    // always agree without exchanging a single body's position.
    public static IReadOnlyList<CelestialBody> Generate(string systemId)
    {
        var random = new Random(AsteroidShape.StableHash(systemId));
        var bodies = new List<CelestialBody>();

        var starId = $"{systemId}-star";
        var starRadius = Lerp(StarRadiusMin, StarRadiusMax, random);
        var star = new CelestialBody(starId, null, 0f, 0f, starRadius, BodyMassTier.Star);
        bodies.Add(star);

        var planetCount = MinPlanets + random.Next(MaxPlanets - MinPlanets + 1);
        var previousOrbitRadius = 0f;
        var previousClearance = 0f;
        var previousTier = BodyMassTier.Rocky; // only consulted once i>0, so this initial value is never read

        for (var i = 0; i < planetCount; i++)
        {
            var tierRoll = random.NextDouble();
            var tier = tierRoll < RockyTierChance ? BodyMassTier.Rocky
                : tierRoll < IceGiantTierChance ? BodyMassTier.IceGiant
                : BodyMassTier.GasGiant;
            var (radiusMin, radiusMax) = tier == BodyMassTier.Rocky ? (RockyRadiusMin, RockyRadiusMax) : (GiantRadiusMin, GiantRadiusMax);
            var radius = Lerp(radiusMin, radiusMax, random);
            var clearance = ClearanceRadius(new CelestialBody("", starId, 0f, 0f, radius, tier));

            float orbitRadius;
            if (i == 0)
            {
                var target = radius * Lerp(FirstOrbitToOwnRadiusFactorMin, FirstOrbitToOwnRadiusFactorMax, random);
                orbitRadius = MathF.Max(target, starRadius + clearance * OrbitClearanceMarginFactor);
            }
            else
            {
                var growthFactor = IsGiant(tier) && !IsGiant(previousTier)
                    ? Lerp(TierJumpGrowthFactorMin, TierJumpGrowthFactorMax, random)
                    : Lerp(PlanetGrowthFactorMin, PlanetGrowthFactorMax, random);
                var desired = previousOrbitRadius * growthFactor;
                var minGap = (previousClearance + clearance) * OrbitClearanceMarginFactor;
                orbitRadius = MathF.Max(desired, previousOrbitRadius + minGap);
            }

            var phaseOffset = (float)(random.NextDouble() * 2 * Math.PI);
            var planetId = $"{systemId}-planet-{i}";
            var planet = new CelestialBody(planetId, starId, orbitRadius, phaseOffset, radius, tier);
            bodies.Add(planet);

            var moonChance = tier == BodyMassTier.Rocky ? RockyMoonChance : GasGiantMoonChance;
            var maxMoons = tier == BodyMassTier.Rocky ? 1 : 3;
            var moonCount = random.NextDouble() < moonChance ? 1 + random.Next(maxMoons) : 0;
            var planetClearance = clearance;
            var previousMoonOrbitRadius = 0f;
            var previousMoonClearance = 0f;
            for (var m = 0; m < moonCount; m++)
            {
                var moonRadius = Lerp(MoonRadiusMin, MoonRadiusMax, random);
                var moonClearance = ClearanceRadius(new CelestialBody("", planetId, 0f, 0f, moonRadius, BodyMassTier.Moon));

                float moonOrbitRadius;
                if (m == 0)
                    moonOrbitRadius = radius * MoonOrbitRadiusMinFactor;
                else
                {
                    var moonGrowthFactor = Lerp(MoonGrowthFactorMin, MoonGrowthFactorMax, random);
                    var desiredMoon = previousMoonOrbitRadius * moonGrowthFactor;
                    var minMoonGap = (previousMoonClearance + moonClearance) * OrbitClearanceMarginFactor;
                    moonOrbitRadius = MathF.Max(desiredMoon, previousMoonOrbitRadius + minMoonGap);
                }

                // A moon whose own clearance buffer would poke out past its parent planet's own has
                // nowhere consistent to sit - stop adding further moons rather than placing one
                // anyway (soft degradation, not an error: a planet with fewer moons than it rolled
                // is unremarkable, every other generated system already varies moon counts by chance).
                if (moonOrbitRadius + moonClearance > planetClearance * MoonContainmentFactor)
                    break;

                var moonPhase = (float)(random.NextDouble() * 2 * Math.PI);
                bodies.Add(new CelestialBody($"{planetId}-moon-{m}", planetId, moonOrbitRadius, moonPhase, moonRadius, BodyMassTier.Moon));

                previousMoonOrbitRadius = moonOrbitRadius;
                previousMoonClearance = moonClearance;
            }

            previousOrbitRadius = orbitRadius;
            previousClearance = clearance;
            previousTier = tier;
        }

        return bodies;
    }

    private static float Lerp(float min, float max, Random random) => min + (float)random.NextDouble() * (max - min);

    // Recurses up to the star (ParentId == null), which always sits at the field's own centre -
    // callers offset by that centre themselves (the same way AsteroidField.Center already is a
    // pure function of Width/Height, not a stored value).
    //
    // M59 - a fixed body no longer needs a time argument: this is now a plain polar-to-cartesian
    // lookup (OrbitRadius/PhaseOffset, rolled once in Generate above), not a per-tick Kepler solve.
    public static Vec2 PositionAt(CelestialBody body, IReadOnlyDictionary<string, CelestialBody> byId)
    {
        if (body.ParentId is null)
            return Vec2.Zero;
        var parent = byId[body.ParentId];
        var parentPosition = PositionAt(parent, byId);
        var offset = new Vec2(body.OrbitRadius * Math.Cos(body.PhaseOffset), body.OrbitRadius * Math.Sin(body.PhaseOffset));
        return parentPosition + offset;
    }

    // How big the system's own local field has to be to hold every generated body's full orbit
    // (not just its centre) plus its own clearance buffer, with the same margin OrbitClearanceMarginFactor
    // already uses between consecutive bodies. A moon's own OrbitRadius is relative to its PARENT,
    // not the star, so its true reach from the field's centre folds in the parent planet's own orbit
    // radius too.
    public static float FieldSize(IReadOnlyList<CelestialBody> bodies)
    {
        var byId = bodies.ToDictionary(b => b.Id);
        var maxReach = bodies.Max(b =>
        {
            var parentReach = b.ParentId is null ? 0f : byId[b.ParentId].OrbitRadius;
            return parentReach + b.OrbitRadius + ClearanceRadius(b);
        });
        var margin = ClearanceRadius(bodies.Single(b => b.ParentId is null));
        return 2f * (maxReach + margin);
    }

    // Which bodies a ship can actually land on (M55 - "сесть на планету... собственный
    // ландшафт"): only bodies with a real solid surface. GasGiant/Star have none, and the star's
    // "surface" is already just its own glow, not something to stand a hull on.
    public static bool IsLandable(CelestialBody body) => body.MassTier is BodyMassTier.Rocky or BodyMassTier.Moon;

    // Which gaps between consecutive top-level planet orbits get an asteroid belt
    // (AsteroidField.cs's GenerateBeltAsteroids) - its own independent random stream, salted
    // separately from body generation above, so how many moons/tiers happened to roll never shifts
    // which gaps get a belt.
    public static IReadOnlyList<bool> BeltGaps(string systemId, int planetCount)
    {
        var random = new Random(AsteroidShape.StableHash($"{systemId}-belt-gaps"));
        var gaps = new bool[Math.Max(0, planetCount - 1)];
        for (var i = 0; i < gaps.Length; i++)
            gaps[i] = random.NextDouble() < BeltChance;
        return gaps;
    }
}
