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
//
// The procedural tail of the galaxy is generated INCREMENTALLY (EnsureGenerated below) rather than
// all at once at startup - only the systems the player has actually explored near exist yet, the
// rest simply haven't been rolled. This is why `Systems`/`Points` are backed by mutable lists
// behind read-only views: growing the galaxy must never invalidate a reference anything already
// holds onto (World's own `GalaxyMap` property is a single long-lived instance, never replaced).
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
    // park on. Every system shares one 4800x4800 field (AsteroidField.CreateDefault/the shared
    // empty stub, M48 doubling M40's own 2400x2400), whose cardinal edges sit exactly 2400 units
    // from centre - kept comfortably under that so the full ring stays reachable in every
    // direction, not just along the diagonals. Scaled ×2 alongside the field's own M48 doubling
    // (was 1104 for the 2400x2400 field, 138 before that for the original 300x300 one) - otherwise
    // the ring would sit at barely half the way out, letting a ship warp away far short of the
    // manually-flown crossing this milestone is actually about.
    public const float WarpZoneRadius = 2208f;

    private readonly List<StarSystem> _systems;
    private readonly List<GalaxyPoint> _points;
    public IReadOnlyList<StarSystem> Systems => _systems;
    public IReadOnlyList<GalaxyPoint> Points => _points;
    public string HomePointId { get; }

    // How many of the procedural tail's own systems exist so far - what World.Save.cs persists so
    // a reloaded session doesn't forget how much of the galaxy the crew had already reached.
    public int GeneratedProceduralCount { get; private set; }

    // The one seeded RNG behind the whole procedural tail, kept alive across every EnsureGenerated
    // call rather than restarted each time - continuing the same sequence chunk by chunk gives the
    // exact same galaxy a single big upfront pass would have, just rolled lazily. A fresh Random
    // each call would still be deterministic per call, but would NOT reproduce the single-pass
    // sequence (each call would restart from the same seed instead of picking up where the last
    // one left off), which is what actually matters here: exploring in a different order/pace must
    // still eventually reveal the same galaxy.
    private readonly Random _proceduralRandom = new(2000_02_00);
    private readonly AsteroidField _proceduralField;

    private GalaxyMap(IReadOnlyList<StarSystem> handAuthoredSystems, AsteroidField proceduralField, string homePointId)
    {
        _systems = handAuthoredSystems.ToList();
        _points = _systems.SelectMany(s => s.Points).ToList();
        HomePointId = homePointId;
        _proceduralField = proceduralField;
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

    // Called wherever a player might act on "which systems can I warp to" (World.StarSystems.cs's
    // CanWarpNow/TryWarpTo, and the galactic-map snapshot) - tops the galaxy up with another chunk
    // of the procedural tail if the given system doesn't yet have enough already-generated company
    // nearby. A no-op once MaxProceduralSystems is reached or the neighbour count is already met,
    // so it's cheap to call unconditionally every time rather than tracking "did I already check
    // this tick" separately.
    public void EnsureGenerated(string nearSystemId, int minReachableNeighbors)
    {
        while (GeneratedProceduralCount < MaxProceduralSystems &&
               SystemsWithinWarpRange(nearSystemId).Count < minReachableNeighbors)
        {
            GenerateProceduralChunk(ProceduralChunkSize);
        }
    }

    // Restoring a save (World.Save.cs): tops the procedural tail back up to at least however far
    // the crew had already explored before the file was written, rather than starting the count
    // over from CreateStarter's own small initial seed - same underlying chunked generator, just
    // driven by a target count instead of a neighbour count.
    public void EnsureAtLeast(int proceduralCount)
    {
        while (GeneratedProceduralCount < Math.Min(proceduralCount, MaxProceduralSystems))
            GenerateProceduralChunk(ProceduralChunkSize);
    }

    public static GalaxyMap CreateStarter()
    {
        // Real positions in this system's own local field (World.Voyage.cs), spread across the
        // full 4800x4800 field (M48, doubling M40's own 2400x2400) rather than sitting in a small
        // cluster near its centre - the field's own centre (2400,2400) is where the sun sits
        // (GalaxyMapPanel's backdrop, anchored to AsteroidField.Center rather than any point
        // average) and CanWarpNow already measures distance from exactly that point, so a system
        // whose points actually reach out toward the WarpZoneRadius(2208) ring is what makes "fly
        // clear of the system to jump" a real, felt journey instead of a formality that was already
        // true two steps off the berth. Every original M47 point is simply doubled in place (its
        // bearing and relative distance from centre are unchanged, only the scale) so the layout
        // everyone already knows just grew, rather than being redrawn from scratch; three new
        // points (sector-zeta, frontier-outpost, independent-relay) fill the space that opened up
        // in between.
        //
        // home-station sits fairly close to the field's own centre (and to the asteroid belt's
        // marker below, which can't move - see its own comment) - four routes out of here are
        // flown for real by tests with a fixed tick budget and no (or limited) obstacle avoidance,
        // and each one bounds how far its own destination can safely sit:
        //  - trade-station: World_Docking_TooFastAtPort_ButtonStaysDisarmed flies there in a flat
        //    60s at full uncapped throttle with zero peel/hazard-avoidance at all.
        //  - outpost-gamma: every ApproachBerth-based test caps speed at 1.5 units/s for the whole
        //    approach ("parked and slow enough to dock"), budgeted at 1000s (TestRunner.
        //    StationDocking.cs, M48 - doubling the distance roughly doubled the required budget).
        //  - sector-alpha: World_Voyage_FreeFormClickNearHostileSectorStillTriggersBattle flies
        //    there for real at FlyToward's own default 120s budget (uncapped speed, no override).
        //  - asteroid-field-epsilon: FlyNearAndStop (TestRunner.HelmAndHull.cs/Doors.cs) is the
        //    same default-120s FlyToward underneath.
        // Sector-beta/delta/zeta and the mining outpost/frontier-outpost/independent-relay are only
        // ever reached via a direct teleport (World.DebugPlaceShip) or an explicit, generous
        // budget, so nothing held those back from genuinely using the new size - pushed out toward
        // the WarpZoneRadius(2208) ring instead, with margin to spare.
        var sol = new StarSystem("sol", "Солнечная система", new[]
        {
            // Faction ownership (game_design.md section 12): home stays neutral so a new crew
            // always has somewhere that treats them the same regardless of reputation; the other
            // two stations belong to the rival powers, as do the sectors their raiders patrol.
            new GalaxyPoint("home-station", "Домашняя станция", 2100f, 2800f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
            new GalaxyPoint("sector-alpha", "Сектор Альфа", 2600f, 3400f, GalaxyPointKind.HostileSector, FactionId.FreeFleet),
            // Beta is a picket of two and Delta a patrol of three - the map's difficulty gradient
            // is squadron size, not per-ship strength (game_design.md section 12).
            new GalaxyPoint("sector-beta", "Сектор Бета", 1000f, 4000f, GalaxyPointKind.HostileSector, FactionId.FreeFleet, SquadronSize: 2),
            new GalaxyPoint("outpost-gamma", "Аванпост Гамма", 2900f, 2100f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Shipyard),
            new GalaxyPoint("sector-delta", "Сектор Дельта", 4100f, 1400f, GalaxyPointKind.HostileSector, FactionId.Consortium, SquadronSize: 3),
            new GalaxyPoint("trade-station", "Торговая станция", 1800f, 3100f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Trade),
            // Left at the field's own centre, alongside the sun it orbits - AsteroidField.
            // CreateDefault's own rocks/ore sit right here too (recentred there by
            // RecenterOffsetM48), so moving just this marker without the physical field itself
            // would point the label somewhere the belt no longer actually is.
            new GalaxyPoint("asteroid-field-epsilon", "Пояс астероидов Эпсилон", 2400f, 2400f, GalaxyPointKind.AsteroidField),
            // The Miners' Guild (game_design.md section 12, Phase 4 - MinersGuild) sits right by
            // the belt it works, staying out of the Consortium/FreeFleet fight entirely.
            new GalaxyPoint("mining-outpost", "Форпост старателей", 3000f, 1800f, GalaxyPointKind.Station, FactionId.MinersGuild, StationKind.Mining),
            // M48's three new points, filling the space the doubled field opened up in the gaps
            // between the M47 layout's own bearings (all of which cluster between roughly "east"
            // and "south" of the sun) - due west, due east, and due north respectively.
            new GalaxyPoint("sector-zeta", "Сектор Дзета", 900f, 1300f, GalaxyPointKind.HostileSector, FactionId.FreeFleet, SquadronSize: 2),
            new GalaxyPoint("frontier-outpost", "Пограничный аванпост", 3900f, 2400f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Outpost),
            new GalaxyPoint("independent-relay", "Независимая станция-ретранслятор", 2400f, 900f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Trade),
            // No single faction actually owns this contested a home system outright - the crew's
            // own neutral turf sits alongside two rivals' sectors and a third guild's own outpost.
        }, AsteroidField.CreateDefault(), galaxyX: 300f, galaxyY: 300f, controllingFaction: null);

        var proceduralField = new AsteroidField(4800f, 4800f, Array.Empty<Asteroid>(), Array.Empty<OreDeposit>());

        // Every hand-authored stub below is controlled by whichever faction its own single point
        // already belongs to - simplest reading of "who actually runs this place".
        var alphaCentauri = new StarSystem("alpha-centauri", "Альфа Центавра", new[]
        {
            new GalaxyPoint("ac-outpost", "Форпост Альфы Центавра", 2400f, 2400f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
        }, proceduralField, galaxyX: 420f, galaxyY: 200f, controllingFaction: FactionId.Independent);

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
            new GalaxyPoint("sirius-trade-post", "Торговый пост Сириуса", 2400f, 2400f, GalaxyPointKind.Station, FactionId.Consortium, StationKind.Trade),
        }, proceduralField, galaxyX: 180f, galaxyY: 200f, controllingFaction: FactionId.Consortium);

        var vega = new StarSystem("vega", "Вега", new[]
        {
            new GalaxyPoint("vega-outpost", "Аванпост Веги", 2400f, 2400f, GalaxyPointKind.Station, FactionId.Independent, StationKind.Outpost),
        }, proceduralField, galaxyX: 60f, galaxyY: 300f, controllingFaction: FactionId.Independent);

        var tauCeti = new StarSystem("tau-ceti", "Тау Кита", new[]
        {
            new GalaxyPoint("tau-ceti-sector", "Сектор Тау Кита", 2400f, 2400f, GalaxyPointKind.HostileSector, FactionId.FreeFleet, SquadronSize: 2),
        }, proceduralField, galaxyX: 540f, galaxyY: 300f, controllingFaction: FactionId.FreeFleet);

        var barnardsStar = new StarSystem("barnards-star", "Звезда Барнарда", new[]
        {
            new GalaxyPoint("barnard-mining-outpost", "Форпост старателей Барнарда", 2400f, 2400f, GalaxyPointKind.Station, FactionId.MinersGuild, StationKind.Mining),
        }, proceduralField, galaxyX: 660f, galaxyY: 200f, controllingFaction: FactionId.MinersGuild);

        var handAuthoredSystems = new[] { sol, alphaCentauri, sirius, vega, tauCeti, barnardsStar };
        var map = new GalaxyMap(handAuthoredSystems, proceduralField, "home-station");
        // Seeds just enough of the procedural tail for each hand-authored system to have a handful
        // of real jump targets from the very start - not the whole 194-system galaxy, which now
        // only fills in as the crew actually explores (EnsureGenerated, called from
        // World.StarSystems.cs's CanWarpNow and the galactic-map snapshot as they fly around).
        foreach (var system in handAuthoredSystems)
            map.EnsureGenerated(system.Id, MinReachableNeighborsAtStart);
        return map;
    }

    // "Большая галактическая карта" - up to 194 more systems on top of the 6 hand-authored ones
    // above, for 200 total, generated in chunks as the crew actually explores rather than all at
    // once (EnsureGenerated). Fixed seed, not the gameplay _random's per-instance sequence, so the
    // galaxy is identical every session (a save's DockedPointId/_currentSystemId would otherwise
    // point into a galaxy that no longer looks the same on reload). Each new system is the same
    // light "stub" template alpha-centauri/sirius/etc. already established above - a single point
    // of interest, no dedicated warp marker (any position past WarpZoneRadius from the shared
    // field's own centre works) - not full Sol-scale content, since hand-tuning 194 asteroid fields
    // is a different project than the map/travel system this generates.
    //
    // Connectivity: each new system is placed by the spiral formula below, but if that spot would
    // land further than WarpJumpRadius from every already-placed system (one of the original 6, or
    // an earlier procedural one), it's pulled in to sit just inside range of whichever placed
    // system is nearest instead. Since every earlier system was itself placed under the same rule,
    // this guarantees by induction that the whole galaxy stays one single warp-reachable component
    // (World_StarSystem_GalaxyHas200SystemsAllReachable) - a plain geometric guarantee, not a
    // hand-authored edge list.
    private const int MaxProceduralSystems = 194;
    private const int ProceduralChunkSize = 20; // how many EnsureGenerated rolls out per top-up
    private const int MinReachableNeighborsAtStart = 3; // CreateStarter's own initial seeding, per hand-authored system
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
    // Most systems answer to somebody - a controlled system generates calmer (see below) than the
    // rarer contested ones, which is the whole point of the distinction existing.
    private const float ControlledSystemChance = 0.85f;
    // A controlled system's own point still occasionally turns up a hostile sector (nobody's
    // territory is perfectly quiet), just far less often than a contested one's coin-flip.
    private const float ControlledSystemHostileSectorChance = 0.1f;
    private const float ContestedSystemHostileSectorChance = 0.5f;

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

    // Rolls the next `count` procedural systems (or however many remain under MaxProceduralSystems)
    // and appends them - called both by CreateStarter (one big eager chunk today) and by
    // EnsureGenerated (small top-up chunks as the crew explores). Always continues _proceduralRandom
    // and GeneratedProceduralCount exactly where the last call left them, so calling this in 20-
    // system chunks ten times in a row produces byte-for-byte the same galaxy as one 200-system call.
    private void GenerateProceduralChunk(int count)
    {
        var random = _proceduralRandom;
        var startIndex = GeneratedProceduralCount;
        var endIndex = Math.Min(startIndex + count, MaxProceduralSystems);

        // Rebuilt fresh each call from the current Systems list rather than kept as a running field -
        // this is O(placed-so-far) per new system, fine at this total scale (at most 200), and means
        // there's no separate bookkeeping list to keep in sync with _systems.
        var placed = _systems.Select(s => (s.GalaxyX, s.GalaxyY)).ToList();

        for (var i = startIndex; i < endIndex; i++)
        {
            // Evenly cycling the arm index (rather than picking one at random per system) spreads
            // the systems ~evenly across all 4 arms instead of leaving some arms sparse and others
            // crowded by chance.
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

            // Most systems answer to one faction, same weight regardless of which - a controlled
            // system's own point belongs to its controller (simplest reading of "whose territory
            // this actually is"), a contested one still gets some faction's flag on its one point
            // (someone's still flying it, just without the rest of the system backing them), picked
            // independently of anyone else's claim.
            var isControlled = random.NextDouble() < ControlledSystemChance;
            var pointFaction = (FactionId)random.Next(Enum.GetValues<FactionId>().Length);
            FactionId? controllingFaction = isControlled ? pointFaction : null;
            var hostileSectorChance = isControlled ? ControlledSystemHostileSectorChance : ContestedSystemHostileSectorChance;

            var poi = random.NextDouble() >= hostileSectorChance
                ? new GalaxyPoint($"{id}-poi", $"База {name}", 2400f, 2400f, GalaxyPointKind.Station, pointFaction,
                    (StationKind)random.Next(Enum.GetValues<StationKind>().Length))
                : new GalaxyPoint($"{id}-poi", $"Сектор {name}", 2400f, 2400f, GalaxyPointKind.HostileSector, pointFaction,
                    SquadronSize: random.Next(1, 4));

            var system = new StarSystem(id, name, new[] { poi }, _proceduralField, galaxyX: x, galaxyY: y, controllingFaction: controllingFaction);
            _systems.Add(system);
            _points.AddRange(system.Points);
            placed.Add((x, y));
        }

        GeneratedProceduralCount = endIndex;
    }
}
