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
    }
}
