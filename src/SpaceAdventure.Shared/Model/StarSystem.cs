namespace SpaceAdventure.Shared.Model;

// One star system: its own local field (asteroids/ore, the same space ships/characters already
// fly and mine in) plus the points of interest reachable inside it (stations, hostile sectors,
// asteroid belts - GalaxyMap.cs). Systems themselves are connected to each other by warp, not by
// flying through local field space (World.StarSystems.cs, once that lands) - this class only
// holds one system's own local content.
public sealed class StarSystem
{
    public string Id { get; }
    public string Name { get; }
    public IReadOnlyList<GalaxyPoint> Points { get; }
    public AsteroidField Field { get; }
    // Where this system's own node sits on the GALACTIC map (GalaxyMap.cs's WarpJumpRadius circle) -
    // a fixed, hand-placed or procedurally-generated layout position, unrelated to GalaxyPoint.X/Y
    // (which live in this system's own local field space). Kept apart so the galactic map (a
    // handful of systems, reachability by distance) and a system's own detailed map (real field
    // coordinates, free-form click targets) never share a coordinate space by accident.
    public float GalaxyX { get; }
    public float GalaxyY { get; }
    // Whoever holds this system as their own territory - independent of any one GalaxyPoint's own
    // Faction (a system can be "Consortium space" while still having an Independent-run outpost in
    // it). Null means contested/unclaimed - no single faction keeps the peace there, so it
    // generates rougher than a controlled one (GalaxyMap.cs's GenerateProceduralSystems) and its
    // own station(s) never get the "stands down unless you're actually hostile" treatment a
    // controlling faction's do (World.Voyage.cs's Arrive).
    public FactionId? ControllingFaction { get; }

    // The star, its planets, and any moons (M50 - CelestialBody.cs) - generated once here, purely
    // from this system's own Id, the same determinism AsteroidField's own belts already rely on.
    // Never mutated: a body's actual position at any moment is CelestialBodyGenerator.PositionAt,
    // a pure function of time, not something stored per-tick.
    public IReadOnlyList<CelestialBody> Bodies { get; }
    public IReadOnlyDictionary<string, CelestialBody> BodiesById { get; }

    // How far from the field's own centre the ship has to fly before a jump becomes possible
    // (World.StarSystems.cs's CanWarpNow) - kept at the same fraction of the field's own half-size
    // this constant has always used (2208/2400 = 0.92, from the field's own doubling history
    // before per-system sizing existed), just derived from THIS system's own (possibly much
    // bigger, body-driven) field instead of a single shared constant.
    public const float WarpZoneRadiusFraction = 0.92f;
    public float WarpZoneRadius => (float)(Field.Center.X * WarpZoneRadiusFraction);

    public StarSystem(string id, string name, IReadOnlyList<GalaxyPoint> points, AsteroidField field,
        float galaxyX = 0f, float galaxyY = 0f, FactionId? controllingFaction = null)
    {
        Id = id;
        Name = name;
        // Stamped here rather than repeated on every GalaxyPoint literal - a point only ever
        // belongs to the system it was handed to.
        Points = points.Select(p => p with { SystemId = id }).ToArray();
        Field = field;
        GalaxyX = galaxyX;
        GalaxyY = galaxyY;
        ControllingFaction = controllingFaction;
        Bodies = CelestialBodyGenerator.Generate(id);
        BodiesById = Bodies.ToDictionary(b => b.Id);
    }
}
