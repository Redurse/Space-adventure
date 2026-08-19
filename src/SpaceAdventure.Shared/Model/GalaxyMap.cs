namespace SpaceAdventure.Shared.Model;

// The whole galaxy: a small set of star systems (StarSystem.cs), each with its own local points of
// interest. No corridor graph - a system parked anywhere past its own WarpZoneRadius
// (World.StarSystems.cs's CanWarpNow) can jump to ANY other system within WarpJumpRadius of it on
// the galactic map (StarSystem.GalaxyX/Y), a plain circle around the current system rather than a
// hand-authored set of edges. Ids stay globally unique strings across every system (not a compound
// key) - `Points`/`GetPoint` below flatten every system's points into the same single list this
// class has always exposed, so anything that only ever needed "the point with this id"
// (World.Factions.cs's OwnerOf, World.Quests.cs, World.Save.cs, and so on) keeps working unchanged.
// Only code that actually cares about locality (World.StarSystems.cs's warp, in-system quest
// generation) needs `Systems`/`SystemOf` instead.
public sealed class GalaxyMap
{
    // How far (in StarSystem.GalaxyX/Y units) a single jump can reach - the "warp circle" drawn
    // around the current system on GalacticMapPanel. Systems are packed densely enough (see
    // GenerateProceduralSystems below) that this radius reaches several neighbours from most
    // systems, not just one. Not to be confused with WarpZoneRadius (World.StarSystems.cs) - that
    // one lives in a system's own LOCAL field space (units of AsteroidField.Center) and gates
    // whether a jump can be attempted at all; this one lives in GALACTIC map space and picks which
    // other systems such a jump could reach.
    public const float WarpJumpRadius = 220f;

    // How far (in a system's own LOCAL field-space units, AsteroidField.Center-relative) the ship
    // has to fly from the field's centre before a jump is possible at all - the "edge of the solar
    // system", a plain ring around the whole system rather than one specific point to hunt down and
    // park on. Every system shares one 300x300 field (AsteroidField.CreateDefault/the shared empty
    // stub), whose cardinal edges sit exactly 150 units from centre - kept comfortably under that so
    // the full ring stays reachable in every direction, not just along the diagonals.
    public const float WarpZoneRadius = 138f;

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

    public bool IsWithinWarpRange(string systemIdA, string systemIdB)
    {
        var a = GetSystem(systemIdA);
        var b = GetSystem(systemIdB);
        return Distance(a.GalaxyX, a.GalaxyY, b.GalaxyX, b.GalaxyY) <= WarpJumpRadius;
    }

    public IReadOnlyList<string> SystemsWithinWarpRange(string systemId) =>
        Systems.Where(s => s.Id != systemId && IsWithinWarpRange(systemId, s.Id)).Select(s => s.Id).ToArray();

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
        }, AsteroidField.CreateDefault(), galaxyX: 300f, galaxyY: 300f);

        var emptyField = new AsteroidField(300f, 300f, Array.Empty<Asteroid>(), Array.Empty<OreDeposit>());

        var alphaCentauri = new StarSystem("alpha-centauri", "Альфа Центавра", new[]
        {
            new GalaxyPoint("ac-outpost", "Форпост Альфы Центавра", 150f, 150f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
        }, emptyField, galaxyX: 420f, galaxyY: 200f);

        // The rest of the chain (game_design.md - "куча систем"): each new system is a light stub,
        // the same shape alpha-centauri already was - a single point of interest, no dedicated warp
        // marker (any position past WarpZoneRadius from the field's own centre works) - not full
        // Sol-scale content, since the point of this milestone is the map/travel system itself, not
        // populating every system. Positions step by 120 units along X (156 units diagonal to
        // sol/alpha-centauri) - well inside WarpJumpRadius for each pair of neighbours, but two
        // steps apart (240 units) falls just outside it, so this hand-placed row still only jumps
        // one hop at a time despite there being no explicit edge list anymore.
        var sirius = new StarSystem("sirius", "Сириус", new[]
        {
            new GalaxyPoint("sirius-trade-post", "Торговый пост Сириуса", 150f, 150f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Trade),
        }, emptyField, galaxyX: 180f, galaxyY: 200f);

        var vega = new StarSystem("vega", "Вега", new[]
        {
            new GalaxyPoint("vega-outpost", "Аванпост Веги", 150f, 150f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
        }, emptyField, galaxyX: 60f, galaxyY: 300f);

        var tauCeti = new StarSystem("tau-ceti", "Тау Кита", new[]
        {
            new GalaxyPoint("tau-ceti-sector", "Сектор Тау Кита", 150f, 150f, GalaxyPointKind.HostileSector, FactionId.FreeFleet, SquadronSize: 2),
        }, emptyField, galaxyX: 540f, galaxyY: 300f);

        var barnardsStar = new StarSystem("barnards-star", "Звезда Барнарда", new[]
        {
            new GalaxyPoint("barnard-mining-outpost", "Форпост старателей Барнарда", 150f, 150f, GalaxyPointKind.Station, FactionId.MinersGuild, StationKind.Mining),
        }, emptyField, galaxyX: 660f, galaxyY: 200f);

        var handAuthoredSystems = new[] { sol, alphaCentauri, sirius, vega, tauCeti, barnardsStar };
        var proceduralSystems = GenerateProceduralSystems(handAuthoredSystems, emptyField);

        return new GalaxyMap(handAuthoredSystems.Concat(proceduralSystems).ToArray(), "home-station");
    }

    // "Большая галактическая карта" - 194 more systems on top of the 6 hand-authored ones above,
    // for 200 total. Fixed seed, not the gameplay _random's per-instance sequence, so the galaxy is
    // identical every session (a save's DockedPointId/_currentSystemId would otherwise point into a
    // galaxy that no longer looks the same on reload). Each new system is the same light "stub"
    // template alpha-centauri/sirius/etc. already established above - a single point of interest, no
    // dedicated warp marker (any position past WarpZoneRadius from the shared field's own centre
    // works), the same shared empty field - not full Sol-scale content, since hand-tuning 194
    // asteroid fields is a different project than the map/travel system this generates.
    //
    // Connectivity: each new system is placed by the spiral formula below, but if that spot would
    // land further than WarpJumpRadius from every already-placed system (one of the original 6, or
    // an earlier procedural one), it's pulled in to sit just inside range of whichever placed
    // system is nearest instead. Since every earlier system was itself placed under the same rule,
    // this guarantees by induction that the whole galaxy stays one single warp-reachable component
    // (World_StarSystem_GalaxyHas200SystemsAllReachable) - a plain geometric guarantee, not a
    // hand-authored edge list.
    private const int ProceduralSystemCount = 194;
    private const float ProceduralMinSpacing = 90f; // keeps galactic-map nodes from overlapping

    // Logarithmic-spiral placement (PULSAR: Lost Colony's own galaxy screen, and every other
    // "looks like an actual galaxy" starmap, reads that way because the nodes sit along a handful
    // of winding arms instead of scattered uniformly at random) - a plain random scatter reads as
    // a formless blob, not a galaxy. GalaxySpiralCenter roughly matches the hand-authored 6
    // systems' own centroid so Sol's little hand-placed cluster nests inside the generated arms
    // instead of floating off to one side of them. Tightened radius (was 3000/250) packs all 194
    // systems into a much smaller area than before, so most systems sit close enough to several
    // neighbours for a single warp jump to actually reach more than one candidate.
    private const int SpiralArmCount = 4;
    private const float SpiralTotalTurns = 1.8f; // how many full winds from core to rim
    private const float SpiralMaxRadius = 1400f;
    private const float SpiralCoreRadius = 150f; // keeps the first few systems per arm off the exact centre
    private const float SpiralAngleJitterRadians = 0.5f; // scatters systems off the bare spiral curve into a visible "arm" band
    private const float GalaxySpiralCenterX = 360f;
    private const float GalaxySpiralCenterY = 250f;
    // How close (inside WarpJumpRadius) a pulled-in system is placed next to its nearest neighbour -
    // comfortably inside the jump circle rather than right on its edge, so floating-point rounding
    // can never push a system that was pulled in for connectivity back outside warp range.
    private const float PulledInDistanceFactor = 0.85f;

    private static readonly string[] ProceduralStarNames =
    {
        "Ригель", "Бетельгейзе", "Альдебаран", "Антарес", "Спика", "Регул", "Поллукс", "Кастор",
        "Процион", "Арктур", "Капелла", "Денеб", "Альтаир", "Фомальгаут", "Ахернар", "Канопус",
        "Мицар", "Алькор", "Альфард", "Альнаир", "Альдерамин", "Денебола", "Альхена", "Мира",
        "Алголь", "Ахирд", "Дубхе", "Мерак", "Фекда", "Мегрез", "Алиот", "Бенетнаш", "Гакрукс",
        "Акрукс", "Мимоза", "Садальмелик", "Наос", "Альфекка",
    };

    private static readonly string[] ProceduralCatalogPrefixes =
        { "HD", "Глизе", "Kepler", "Wolf", "Ross", "Lacaille", "WASP", "LP", "GJ", "K2", "TrES", "EZ" };

    private static string ProceduralSystemName(int index, Random random) =>
        index < ProceduralStarNames.Length
            ? ProceduralStarNames[index]
            : $"{ProceduralCatalogPrefixes[random.Next(ProceduralCatalogPrefixes.Length)]}-{random.Next(100, 9999)}";

    private static float Distance(float x1, float y1, float x2, float y2) =>
        MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

    private static IReadOnlyList<StarSystem> GenerateProceduralSystems(
        IReadOnlyList<StarSystem> handAuthoredSystems, AsteroidField emptyField)
    {
        var random = new Random(2000_02_00); // fixed - see the doc comment above

        var placed = handAuthoredSystems.Select(s => (s.Id, s.GalaxyX, s.GalaxyY)).ToList();
        var systems = new List<StarSystem>(ProceduralSystemCount);

        for (var i = 0; i < ProceduralSystemCount; i++)
        {
            // Evenly cycling the arm index (rather than picking one at random per system) spreads
            // the ProceduralSystemCount systems ~evenly across all 4 arms instead of leaving some
            // arms sparse and others crowded by chance.
            var armAngle = (i % SpiralArmCount) * (2f * MathF.PI / SpiralArmCount);

            var x = 0f;
            var y = 0f;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                // Denser near the core than the rim (radius^1.3, not radius^1 or radius^0.5) - the
                // same "most stars sit close to the centre, fewer way out at the rim" shape a real
                // spiral galaxy's disk has.
                var radius = SpiralCoreRadius + MathF.Pow((float)random.NextDouble(), 1.3f) * (SpiralMaxRadius - SpiralCoreRadius);
                var winding = radius / SpiralMaxRadius * SpiralTotalTurns * 2f * MathF.PI;
                var angle = armAngle + winding + ((float)random.NextDouble() * 2f - 1f) * SpiralAngleJitterRadians;

                x = GalaxySpiralCenterX + radius * MathF.Cos(angle);
                y = GalaxySpiralCenterY + radius * MathF.Sin(angle);
                if (placed.All(p => Distance(p.GalaxyX, p.GalaxyY, x, y) >= ProceduralMinSpacing))
                    break;
            }

            var nearest = placed.OrderBy(p => Distance(p.GalaxyX, p.GalaxyY, x, y)).First();

            // The spiral spot landed too far from anything already placed to ever be warp-reachable
            // - pull it in next to its nearest neighbour instead, trying a handful of angles around
            // it so the pulled-in spot still respects ProceduralMinSpacing against everyone else.
            if (Distance(nearest.GalaxyX, nearest.GalaxyY, x, y) > WarpJumpRadius)
            {
                var pulledInDistance = WarpJumpRadius * PulledInDistanceFactor;
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    var pullAngle = (float)random.NextDouble() * 2f * MathF.PI;
                    var candidateX = nearest.GalaxyX + pulledInDistance * MathF.Cos(pullAngle);
                    var candidateY = nearest.GalaxyY + pulledInDistance * MathF.Sin(pullAngle);
                    x = candidateX;
                    y = candidateY;
                    if (placed.All(p => Distance(p.GalaxyX, p.GalaxyY, candidateX, candidateY) >= ProceduralMinSpacing))
                        break;
                }
            }

            var id = $"sys-{i + 1:000}";
            var name = ProceduralSystemName(i, random);

            var faction = (FactionId)random.Next(Enum.GetValues<FactionId>().Length);
            var poi = random.NextDouble() < 0.7
                ? new GalaxyPoint($"{id}-poi", $"База {name}", 150f, 150f, GalaxyPointKind.Station, faction,
                    (StationKind)random.Next(Enum.GetValues<StationKind>().Length))
                : new GalaxyPoint($"{id}-poi", $"Сектор {name}", 150f, 150f, GalaxyPointKind.HostileSector, faction,
                    SquadronSize: random.Next(1, 4));

            systems.Add(new StarSystem(id, name, new[] { poi }, emptyField, galaxyX: x, galaxyY: y));
            placed.Add((id, x, y));
        }

        return systems;
    }
}
