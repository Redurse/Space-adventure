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

    public StarSystem(string id, string name, IReadOnlyList<GalaxyPoint> points, AsteroidField field)
    {
        Id = id;
        Name = name;
        // Stamped here rather than repeated on every GalaxyPoint literal - a point only ever
        // belongs to the system it was handed to.
        Points = points.Select(p => p with { SystemId = id }).ToArray();
        Field = field;
    }
}
