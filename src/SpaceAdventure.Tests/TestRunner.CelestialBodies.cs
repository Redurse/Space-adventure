using SpaceAdventure.Shared.Model;

// Structural checks on CelestialBody.cs's generator, independent of World/GalaxyMap - same
// isolation approach TestRunner.StationProcedural.cs already uses for Station.Procedural.cs: catch
// a generator regression without needing a running World at all.
internal static partial class TestRunner
{
    private static readonly string[] SampleSystemIds = { "sol", "alpha-centauri", "sys-014", "sys-099", "wolf-359" };

    private static bool CelestialBodies_SameSystemIdAlwaysProducesIdenticalLayout()
    {
        foreach (var id in SampleSystemIds)
        {
            var a = CelestialBodyGenerator.Generate(id);
            var b = CelestialBodyGenerator.Generate(id);
            if (a.Count != b.Count)
                return false;
            for (var i = 0; i < a.Count; i++)
                if (a[i] != b[i])
                    return false;
        }
        return true;
    }

    private static bool CelestialBodies_PlanetCountWithinAgreedBand()
    {
        foreach (var id in SampleSystemIds)
        {
            var bodies = CelestialBodyGenerator.Generate(id);
            var star = bodies.Single(b => b.ParentId is null);
            var planetCount = bodies.Count(b => b.ParentId == star.Id);
            if (planetCount < CelestialBodyGenerator.MinPlanets || planetCount > CelestialBodyGenerator.MaxPlanets)
                return false;
        }
        return true;
    }

    // No two bodies' own disks ever overlap: every top-level planet sits strictly further from the
    // star than the previous one's own outer edge, and every moon sits strictly outside its own
    // parent planet's surface.
    private static bool CelestialBodies_NoTwoBodiesOverlap()
    {
        foreach (var id in SampleSystemIds)
        {
            var bodies = CelestialBodyGenerator.Generate(id);
            var star = bodies.Single(b => b.ParentId is null);
            var planets = bodies.Where(b => b.ParentId == star.Id).OrderBy(b => b.OrbitRadius).ToList();

            var previousOuterEdge = star.Radius;
            foreach (var planet in planets)
            {
                if (planet.OrbitRadius - planet.Radius <= previousOuterEdge)
                    return false;
                previousOuterEdge = planet.OrbitRadius + planet.Radius;
            }

            foreach (var planet in planets)
            {
                var moons = bodies.Where(b => b.ParentId == planet.Id);
                foreach (var moon in moons)
                    if (moon.OrbitRadius - moon.Radius <= planet.Radius)
                        return false;
            }
        }
        return true;
    }

    private static bool CelestialBodies_BeltGapsAreDeterministicAndWithinCount()
    {
        foreach (var id in SampleSystemIds)
        {
            var bodies = CelestialBodyGenerator.Generate(id);
            var star = bodies.Single(b => b.ParentId is null);
            var planetCount = bodies.Count(b => b.ParentId == star.Id);

            var a = CelestialBodyGenerator.BeltGaps(id, planetCount);
            var b = CelestialBodyGenerator.BeltGaps(id, planetCount);
            if (a.Count != planetCount - 1 || !a.SequenceEqual(b))
                return false;
        }
        return true;
    }
}
