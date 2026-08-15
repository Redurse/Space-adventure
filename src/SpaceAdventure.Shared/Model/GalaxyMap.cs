namespace SpaceAdventure.Shared.Model;

// Fixed starter galaxy (game_design.md section 5) — a small, free-form 2D spread of stations
// and hostile sectors; no fixed lanes between them, the player can head anywhere on the map.
public sealed class GalaxyMap
{
    public IReadOnlyList<GalaxyPoint> Points { get; }
    public string HomePointId { get; }

    public GalaxyMap(IReadOnlyList<GalaxyPoint> points, string homePointId)
    {
        Points = points;
        HomePointId = homePointId;
    }

    public GalaxyPoint GetPoint(string id) => Points.First(p => p.Id == id);

    public static GalaxyMap CreateStarter()
    {
        var points = new[]
        {
            new GalaxyPoint("home-station", "Домашняя станция", 10f, 50f, GalaxyPointKind.Station),
            new GalaxyPoint("sector-alpha", "Сектор Альфа", 35f, 30f, GalaxyPointKind.HostileSector),
            new GalaxyPoint("sector-beta", "Сектор Бета", 35f, 70f, GalaxyPointKind.HostileSector),
            new GalaxyPoint("outpost-gamma", "Аванпост Гамма", 60f, 50f, GalaxyPointKind.Station),
            new GalaxyPoint("sector-delta", "Сектор Дельта", 80f, 25f, GalaxyPointKind.HostileSector),
            new GalaxyPoint("trade-station", "Торговая станция", 90f, 60f, GalaxyPointKind.Station),
        };

        return new GalaxyMap(points, "home-station");
    }
}
