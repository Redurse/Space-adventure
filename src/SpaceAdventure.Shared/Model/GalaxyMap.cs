namespace SpaceAdventure.Shared.Model;

// The whole galaxy: a small set of star systems (StarSystem.cs), each with its own local points of
// interest, connected by a LIMITED set of warp corridors (not a full graph - every system reachable
// from every other used to be the simplification here, deliberately reversed: a hand-authored,
// non-crossing chain instead, so the galactic map (GalacticMapPanel) never has to untangle crossing
// lines). Ids stay globally unique strings across every system (not a compound key) - `Points`/
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
    // Undirected edges between system ids - the only pairs TryWarpTo will actually jump between.
    // A path graph (each system wired to at most two neighbours) rather than anything with a
    // branch or a cycle: guarantees the galactic map's own layout (StarSystem.GalaxyX/Y) can always
    // be drawn without any two corridors crossing, without needing a real graph-planarity solver
    // for what is, at hobby scale, a handful of nodes.
    public IReadOnlyList<(string A, string B)> Corridors { get; }

    public GalaxyMap(IReadOnlyList<StarSystem> systems, string homePointId, IReadOnlyList<(string A, string B)> corridors)
    {
        Systems = systems;
        HomePointId = homePointId;
        Points = systems.SelectMany(s => s.Points).ToArray();
        Corridors = corridors;
    }

    public GalaxyPoint GetPoint(string id) => Points.First(p => p.Id == id);
    public StarSystem GetSystem(string id) => Systems.First(s => s.Id == id);
    public StarSystem SystemOf(string pointId) => Systems.First(s => s.Points.Any(p => p.Id == pointId));

    public bool AreConnected(string systemIdA, string systemIdB) =>
        Corridors.Any(c => (c.A == systemIdA && c.B == systemIdB) || (c.A == systemIdB && c.B == systemIdA));

    public IReadOnlyList<string> ConnectedSystemIds(string systemId) =>
        Corridors.Where(c => c.A == systemId || c.B == systemId).Select(c => c.A == systemId ? c.B : c.A).ToArray();

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
        }, AsteroidField.CreateDefault(), galaxyX: 300f, galaxyY: 300f);

        var emptyField = new AsteroidField(300f, 300f, Array.Empty<Asteroid>(), Array.Empty<OreDeposit>());

        var alphaCentauri = new StarSystem("alpha-centauri", "Альфа Центавра", new[]
        {
            new GalaxyPoint("ac-outpost", "Форпост Альфы Центавра", 150f, 150f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
            new GalaxyPoint("ac-warp-point", "Граница системы", 20f, 20f, GalaxyPointKind.WarpPoint),
        }, emptyField, galaxyX: 420f, galaxyY: 200f);

        // The rest of the chain (game_design.md - "куча систем", limited non-crossing corridors):
        // each new system is a light stub, the same shape alpha-centauri already was - one warp
        // point plus a single point of interest - not full Sol-scale content, since the point of
        // this milestone is the map/travel system itself, not populating every system.
        var sirius = new StarSystem("sirius", "Сириус", new[]
        {
            new GalaxyPoint("sirius-trade-post", "Торговый пост Сириуса", 150f, 150f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Trade),
            new GalaxyPoint("sirius-warp-point", "Граница системы", 20f, 20f, GalaxyPointKind.WarpPoint),
        }, emptyField, galaxyX: 180f, galaxyY: 200f);

        var vega = new StarSystem("vega", "Вега", new[]
        {
            new GalaxyPoint("vega-outpost", "Аванпост Веги", 150f, 150f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
            new GalaxyPoint("vega-warp-point", "Граница системы", 20f, 20f, GalaxyPointKind.WarpPoint),
        }, emptyField, galaxyX: 60f, galaxyY: 300f);

        var tauCeti = new StarSystem("tau-ceti", "Тау Кита", new[]
        {
            new GalaxyPoint("tau-ceti-sector", "Сектор Тау Кита", 150f, 150f, GalaxyPointKind.HostileSector, FactionId.FreeFleet, SquadronSize: 2),
            new GalaxyPoint("tau-ceti-warp-point", "Граница системы", 20f, 20f, GalaxyPointKind.WarpPoint),
        }, emptyField, galaxyX: 540f, galaxyY: 300f);

        var barnardsStar = new StarSystem("barnards-star", "Звезда Барнарда", new[]
        {
            new GalaxyPoint("barnard-mining-outpost", "Форпост старателей Барнарда", 150f, 150f, GalaxyPointKind.Station, FactionId.MinersGuild, StationKind.Mining),
            new GalaxyPoint("barnard-warp-point", "Граница системы", 20f, 20f, GalaxyPointKind.WarpPoint),
        }, emptyField, galaxyX: 660f, galaxyY: 200f);

        // A path, not a full graph: vega - sirius - sol - alpha-centauri - tau-ceti - barnards-star.
        // Every system reaches every other by hopping through its neighbours, but no two corridors
        // ever cross when drawn at the GalaxyX/Y positions above.
        var corridors = new[]
        {
            ("vega", "sirius"),
            ("sirius", "sol"),
            ("sol", "alpha-centauri"),
            ("alpha-centauri", "tau-ceti"),
            ("tau-ceti", "barnards-star"),
        };

        return new GalaxyMap(new[] { sol, alphaCentauri, sirius, vega, tauCeti, barnardsStar }, "home-station", corridors);
    }
}
