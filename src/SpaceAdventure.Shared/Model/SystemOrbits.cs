namespace SpaceAdventure.Shared.Model;

// A star system's own planets and any asteroid belts between them (M48 - "в звездных системах
// планеты спавнились в количествах от 3 до 6 штук... с шансом в 25 процентов между любыми 2
// орбитами спанвился пояс астероидов"). Deterministic and side-effect-free from a system's own id
// alone (AsteroidShape.StableHash, the same "same string always hashes the same way" trick that
// already drives each asteroid's own jagged outline) - the client (GalaxyMapPanel's decorative
// rings) and the server (AsteroidField's real belt rocks) both call this and always agree on the
// same layout for the same system, with nothing to transmit over the network for it.
public static class SystemOrbits
{
    public const int MinPlanets = 3;
    public const int MaxPlanets = 6;
    // Comfortably inside GalaxyMap.WarpZoneRadius(2208) - the outermost orbit still sits well
    // short of the warp ring, leaving room for it to actually read as "past the planets" too.
    public const float MaxOrbitRadius = 2000f;
    public const float BeltChance = 0.25f;

    public readonly record struct Layout(int PlanetCount, IReadOnlyList<float> OrbitRadii, IReadOnlyList<bool> BeltAfterOrbit);

    // BeltAfterOrbit[i] is the gap between OrbitRadii[i] and OrbitRadii[i+1] - PlanetCount-1 gaps
    // for PlanetCount orbits, the same "count of fenceposts minus one" as adjacent pairs always are.
    public static Layout Generate(string systemId)
    {
        var random = new Random(AsteroidShape.StableHash(systemId));
        var count = MinPlanets + random.Next(MaxPlanets - MinPlanets + 1);

        var radii = new float[count];
        for (var i = 0; i < count; i++)
            radii[i] = MaxOrbitRadius * (i + 1) / (count + 1);

        var belts = new bool[count - 1];
        for (var i = 0; i < belts.Length; i++)
            belts[i] = random.NextDouble() < BeltChance;

        return new Layout(count, radii, belts);
    }
}
