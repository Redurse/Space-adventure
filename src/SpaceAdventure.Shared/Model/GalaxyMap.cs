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
            // Faction ownership (game_design.md section 12): home stays neutral so a new crew
            // always has somewhere that treats them the same regardless of reputation; the other
            // two stations belong to the rival powers, as do the sectors their raiders patrol.
            new GalaxyPoint("home-station", "Домашняя станция", 10f, 50f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
            new GalaxyPoint("sector-alpha", "Сектор Альфа", 35f, 30f, GalaxyPointKind.HostileSector, FactionId.FreeFleet),
            // Beta is a picket of two and Delta a patrol of three - the map's difficulty gradient
            // is squadron size, not per-ship strength (game_design.md section 12).
            new GalaxyPoint("sector-beta", "Сектор Бета", 35f, 70f, GalaxyPointKind.HostileSector, FactionId.FreeFleet, SquadronSize: 2),
            new GalaxyPoint("outpost-gamma", "Аванпост Гамма", 60f, 50f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Shipyard),
            new GalaxyPoint("sector-delta", "Сектор Дельта", 80f, 25f, GalaxyPointKind.HostileSector, FactionId.Consortium, SquadronSize: 3),
            new GalaxyPoint("trade-station", "Торговая станция", 90f, 60f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Trade),
            new GalaxyPoint("asteroid-field-epsilon", "Пояс астероидов Эпсилон", 60f, 15f, GalaxyPointKind.AsteroidField),
            // The Miners' Guild (game_design.md section 12, Phase 4 - MinersGuild) sits right by
            // the belt it works, staying out of the Consortium/FreeFleet fight entirely.
            new GalaxyPoint("mining-outpost", "Форпост старателей", 72f, 18f, GalaxyPointKind.Station, FactionId.MinersGuild, StationKind.Mining),
        };

        return new GalaxyMap(points, "home-station");
    }
}
