namespace SpaceAdventure.Shared.Model;

// The whole galaxy: a small set of star systems (StarSystem.cs), each with its own local points of
// interest. Ids stay globally unique strings across every system (not a compound key) - `Points`/
// `GetPoint` below flatten every system's points into the same single list this class has always
// exposed, so anything that only ever needed "the point with this id" (World.Factions.cs's
// OwnerOf, World.Quests.cs, World.Save.cs, and so on) keeps working unchanged. Only code that
// actually cares about locality (World.StarSystems.cs's warp, in-system quest generation) needs
// `Systems`/`SystemOf` instead.
public sealed class GalaxyMap
{
    public IReadOnlyList<StarSystem> Systems { get; }
    public string HomePointId { get; }
    public IReadOnlyList<GalaxyPoint> Points { get; }

    public GalaxyMap(IReadOnlyList<StarSystem> systems, string homePointId)
    {
        Systems = systems;
        HomePointId = homePointId;
        Points = systems.SelectMany(s => s.Points).ToArray();
    }

    public GalaxyPoint GetPoint(string id) => Points.First(p => p.Id == id);
    public StarSystem GetSystem(string id) => Systems.First(s => s.Id == id);
    public StarSystem SystemOf(string pointId) => Systems.First(s => s.Points.Any(p => p.Id == pointId));

    public static GalaxyMap CreateStarter()
    {
        // Coordinates are real positions in this system's own local field (World.StarSystems.cs,
        // World.Voyage.cs's StepTraveling) - the same 300x300 space AsteroidField.CreateDefault's
        // asteroids already occupy (roughly the 60-220 band on both axes), so every point below
        // sits in one of the open corners/edges instead of inside a rock.
        // Every point below is placed so the straight line from the ship's fixed departure spot
        // (DockBerthPosition, ~(124.5,150) for the starter Frigate) clears every asteroid by a
        // real margin, not just at the point itself - a position that looks clear in isolation can
        // still sit on the flight path to it and wedge the ship against a rock mid-transit
        // (World.Voyage.cs's StepTraveling/AutopilotToward). Checked by direct point-to-segment
        // distance against each asteroid's centre, not eyeballed.
        var sol = new StarSystem("sol", "Солнечная система", new[]
        {
            // Faction ownership (game_design.md section 12): home stays neutral so a new crew
            // always has somewhere that treats them the same regardless of reputation; the other
            // two stations belong to the rival powers, as do the sectors their raiders patrol.
            new GalaxyPoint("home-station", "Домашняя станция", 35f, 141f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
            new GalaxyPoint("sector-alpha", "Сектор Альфа", 52f, 97f, GalaxyPointKind.HostileSector, FactionId.FreeFleet),
            // Beta is a picket of two and Delta a patrol of three - the map's difficulty gradient
            // is squadron size, not per-ship strength (game_design.md section 12).
            new GalaxyPoint("sector-beta", "Сектор Бета", 42f, 187f, GalaxyPointKind.HostileSector, FactionId.FreeFleet, SquadronSize: 2),
            new GalaxyPoint("outpost-gamma", "Аванпост Гамма", 189f, 61f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Shipyard),
            new GalaxyPoint("sector-delta", "Сектор Дельта", 235f, 150f, GalaxyPointKind.HostileSector, FactionId.Consortium, SquadronSize: 3),
            new GalaxyPoint("trade-station", "Торговая станция", 151f, 257f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Trade),
            new GalaxyPoint("asteroid-field-epsilon", "Пояс астероидов Эпсилон", 150f, 150f, GalaxyPointKind.AsteroidField),
            // The Miners' Guild (game_design.md section 12, Phase 4 - MinersGuild) sits right by
            // the belt it works, staying out of the Consortium/FreeFleet fight entirely.
            new GalaxyPoint("mining-outpost", "Форпост старателей", 100f, 237f, GalaxyPointKind.Station, FactionId.MinersGuild, StationKind.Mining),
            new GalaxyPoint("sol-warp-point", "Граница системы", 118f, 60f, GalaxyPointKind.WarpPoint),
        }, AsteroidField.CreateDefault());

        var alphaCentauri = new StarSystem("alpha-centauri", "Альфа Центавра", new[]
        {
            new GalaxyPoint("ac-outpost", "Форпост Альфы Центавра", 150f, 150f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
            new GalaxyPoint("ac-warp-point", "Граница системы", 20f, 20f, GalaxyPointKind.WarpPoint),
        }, new AsteroidField(300f, 300f, Array.Empty<Asteroid>(), Array.Empty<OreDeposit>()));

        return new GalaxyMap(new[] { sol, alphaCentauri }, "home-station");
    }
}
