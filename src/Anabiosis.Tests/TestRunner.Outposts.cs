using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Outposts keep the procedural layout (the finished ones are put together by hand out of the outpost compartments), but each has a shipwright.
internal static partial class TestRunner
{
    private static readonly string[] OutpostPointIds =
        { "outpost-gamma", "frontier-outpost", "mining-outpost", "ac-outpost", "vega-outpost", "barnard-mining-outpost" };

    // The map is an instance (it can differ per game); one world's is enough for these.
    private static readonly GalaxyMap OutpostTestMap = new World().GalaxyMap;

    private static bool Outposts_AreTheStationsNamedSoAndOnlyThose()
    {
        var allOutposts = OutpostPointIds.All(id => Station.IsOutpostPoint(OutpostTestMap.GetPoint(id)));
        var someOrdinaryStation = OutpostTestMap.Points.FirstOrDefault(p => p.Kind == GalaxyPointKind.Station && !Station.IsOutpostPoint(p));
        return allOutposts && someOrdinaryStation is not null;
    }

    // The five hand-drawn outpost compartments are in the catalog, in a category of their own (own tab, own colour in the shipwright's list).
    private static bool CompartmentCatalog_OutpostCompartments_AreFiveAndHaveTheirOwnCategory()
    {
        var outposts = CompartmentCatalog.Entries.Where(e => e.Type == CompartmentType.Outpost).ToList();
        var names = outposts.Select(e => e.DisplayName).ToHashSet();
        return outposts.Count == 5
            && names.Contains("Переходный отсек аванпоста") && names.Contains("Реакторный отсек аванпоста")
            && names.Contains("Администрация аванпоста") && names.Contains("Инжинерный отсек аванпоста")
            && names.Contains("Стыковочный шлюз аванпоста")
            && outposts.All(e => CompartmentPricing.CategoryOf(e.Type) == RoomCategory.Outpost && CompartmentPricing.Price(e) > 0)
            // No ordinary compartment is filed under it, and it has a tab of its own.
            && CompartmentCatalog.Entries.Where(e => e.Type != CompartmentType.Outpost).All(e => CompartmentPricing.CategoryOf(e.Type) != RoomCategory.Outpost)
            && Anabiosis.Client.Rendering.StationBuildPanel.Categories.Any(c => c.Category == RoomCategory.Outpost);
    }

    private static CompartmentCatalogEntry OutpostEntry(string id) => CompartmentCatalog.Find(id)!;

    // Nothing of the five drawings is left out: every device, every wall lamp and terminal (recessed or standing on the floor), every door.
    private static bool CompartmentCatalog_OutpostCompartments_CarryEverythingFromTheDrawings()
    {
        (string Id, int Devices, int WallDevices, int Floor, int Doors)[] expected =
        {
            ("outpost-hub", 0, 12, 4, 8),
            ("outpost-reactor", 7, 8, 0, 4),
            ("outpost-admin", 8, 8, 0, 4),
            ("outpost-engineering", 3, 6, 0, 4),
            ("outpost-docking", 8, 4, 0, 12),
        };
        return expected.All(x =>
        {
            var e = OutpostEntry(x.Id);
            return e.Devices.Count == x.Devices && e.WallDevices.Count == x.WallDevices
                && e.WallDevices.Count(w => !w.Recessed) == x.Floor && e.DoorEdges.Count == x.Doors;
        });
    }

    private static bool Outposts_StampKeepsDoorwaysOpenAndPutsEveryDoorAndWallDevice()
    {
        var grid = new TileGrid();
        var hub = OutpostEntry("outpost-hub");
        var result = CompartmentPlacer.Stamp(grid, hub, new TileCoord(0, 0), 0, "hub");
        if (!result.Success || grid.DoorEdges.Count != hub.DoorEdges.Count)
            return false;
        // The west doorway (x=0, rows 4-5) is plain floor, not a wall: the door itself is on the seam outside it.
        var doorwayOpen = grid.CellAt(new TileCoord(0, 4)) is { HasFloor: true, Wall: TileWallKind.None }
            && grid.CellAt(new TileCoord(0, 5)) is { HasFloor: true, Wall: TileWallKind.None };
        // The floor-standing wall devices are on the grid (4 of the 12).
        var floorStanding = grid.Cells.Values.Count(c => c.WallDeviceKind is not null && !c.WallDeviceRecessed);
        var all = grid.Cells.Values.Count(c => c.WallDeviceKind is not null);
        // A wide door is one door: the two segments of the west one share an id.
        var westIds = new[] { grid.DoorEdgeAt(new TileCoord(-1, 4), TileSide.East), grid.DoorEdgeAt(new TileCoord(-1, 5), TileSide.East) }.Select(e => e?.Id).ToList();
        return doorwayOpen && floorStanding == 4 && all == 12 && westIds[0] is not null && westIds[0] == westIds[1];
    }

    // Two compartments put down so their doors face each other: one door, not two, and the two doorways are open to each other.
    private static bool Outposts_DoorsThatCoincideBecomeOneDoor()
    {
        var grid = new TileGrid();
        CompartmentPlacer.Stamp(grid, OutpostEntry("outpost-hub"), new TileCoord(0, 0), 0, "hub");
        var before = grid.DoorEdges.Count; // 8
        var admin = CompartmentPlacer.Stamp(grid, OutpostEntry("outpost-admin"), new TileCoord(10, 0), 0, "admin");
        if (!admin.Success)
            return false;
        // The hub's east door (9,4)-(9,5) is the admin's west door: merged, only the admin's other door is new.
        var merged = admin.MergedDoorEdges!.Count == 2 && admin.PlacedDoorEdges!.Count == 2 && admin.RemovedDoorEdges!.Count == 0;
        var seamOpen = grid.CellAt(new TileCoord(9, 4)) is { HasFloor: true, Wall: TileWallKind.None }
            && grid.CellAt(new TileCoord(10, 4)) is { HasFloor: true, Wall: TileWallKind.None };
        return merged && seamOpen && grid.DoorEdges.Count == before + 2
            && grid.DoorEdgeAt(new TileCoord(9, 4), TileSide.East) is not null;
    }

    private static bool Outposts_PlacingNearDoorsSnapsSoTheyCoincide()
    {
        var grid = new TileGrid();
        CompartmentPlacer.Stamp(grid, OutpostEntry("outpost-hub"), new TileCoord(0, 0), 0, "hub");
        var admin = OutpostEntry("outpost-admin");
        // A cursor a tile or two off the right spot...
        var snapped = CompartmentBuilder.SnapToDoors(grid, new HashSet<TileCoord>(), admin, new TileCoord(11, 1), 0);
        // ...lands exactly where the doors meet; far from any door nothing moves.
        var farAway = CompartmentBuilder.SnapToDoors(grid, new HashSet<TileCoord>(), admin, new TileCoord(40, 40), 0);
        // And the in-game plan for that spot has only the compartment's own new doors in its fragment.
        var (plan, _) = CompartmentBuilder.Plan(grid, new HashSet<TileCoord>(), admin, snapped, 0);
        return snapped == new TileCoord(10, 0) && farAway == new TileCoord(40, 40)
            && plan is not null && plan.Fragment.DoorEdges.Count == 2 && (plan.RemovedDoorEdges?.Count ?? 0) == 0;
    }

    // A compartment whose wall stands in front of a door of the hull closes it - that door goes, and the doorway beside it is a plain wall again.
    private static bool Outposts_AWallInFrontOfADoorRemovesIt()
    {
        var grid = new TileGrid();
        CompartmentPlacer.Stamp(grid, OutpostEntry("outpost-hub"), new TileCoord(0, 0), 0, "hub");
        // The administration shifted up one row: its west doorway now faces only the lower half of the hub's east door (the two doors meet in
        // one segment - one door), while a wall of the administration stands in front of the upper half, which closes it.
        var admin = CompartmentPlacer.Stamp(grid, OutpostEntry("outpost-admin"), new TileCoord(10, 1), 0, "admin");
        if (!admin.Success)
            return false;
        return admin.RemovedDoorEdges!.Count == 1 && admin.MergedDoorEdges!.Count == 1
            && grid.DoorEdgeAt(new TileCoord(9, 4), TileSide.East) is null
            && grid.DoorEdgeAt(new TileCoord(9, 5), TileSide.East) is not null;
    }

    // Through the real commands, on a real hull: build one outpost compartment, then the next with its door on the first one's - they are joined by
    // ONE door, walkable from room to room, and the ship still loads.
    private static bool CompartmentBuild_OutpostCompartmentsBuiltDoorToDoorShareOneDoor()
    {
        var world = CoolShipWorld();
        var first = OutpostEntry("outpost-engineering");
        var second = OutpostEntry("outpost-reactor");

        // An anchor where the first fits and the second, ten tiles east of it, then fits too.
        TileCoord? chosen = null;
        for (var y = -40; y <= 40 && chosen is null; y++)
            for (var x = -40; x <= 50 && chosen is null; x++)
            {
                var anchor = new TileCoord(x, y);
                if (CompartmentBuilder.Plan(world.Ship.Tiles, NoReservedTiles, first, anchor, 0).Plan is null)
                    continue;
                var grid = world.Ship.Tiles.Clone();
                CompartmentPlacer.Stamp(grid, first, anchor, 0, "probe");
                if (CompartmentBuilder.Plan(grid, NoReservedTiles, second, new TileCoord(x + 10, y), 0).Plan is not null)
                    chosen = anchor;
            }
        if (chosen is not { } at)
            return false;

        var roomsBefore = world.Ship.Rooms.Count;
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: new BuildCompartmentRequest(first.Id, at.X, at.Y, 0)));
        world.DebugFastForwardCompartmentBuilds(9999);
        world.Step(RealtimeStep);
        // The second one is put near, not exactly: the snap (as the client does it) brings its door onto the first one's.
        var near = CompartmentBuilder.SnapToDoors(world.Ship.Tiles, NoReservedTiles, second, new TileCoord(at.X + 11, at.Y + 1), 0);
        if (near != new TileCoord(at.X + 10, at.Y))
            return false;
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: new BuildCompartmentRequest(second.Id, near.X, near.Y, 0)));
        world.DebugFastForwardCompartmentBuilds(9999);
        world.Step(RealtimeStep);

        // Two rooms more, and on the seam between them exactly one door (2 segments of one id), not two on top of each other.
        var seamX = at.X + 9;
        var segments = new[] { 4, 5 }.Select(dy => world.Ship.Tiles.DoorEdgeAt(new TileCoord(seamX, at.Y + dy), TileSide.East)).ToList();
        var oneDoor = segments.All(s => s is not null) && segments[0]!.Id == segments[1]!.Id;
        var path = world.Ship.Tiles.CellAt(new TileCoord(seamX, at.Y + 4)) is { HasFloor: true, Wall: TileWallKind.None }
            && world.Ship.Tiles.CellAt(new TileCoord(seamX + 1, at.Y + 4)) is { HasFloor: true, Wall: TileWallKind.None };
        var joined = world.Ship.Rooms.Count == roomsBefore + 2 && oneDoor && path
            && world.Ship.DoorEdges.Count(e => e.RoomAId is not null && e.RoomBId is not null && e.RoomAId != e.RoomBId) >= 1;
        if (!joined)
            return false;

        // Taking the second one down again leaves a ship that still loads - the door on the seam is now a door out of the first. (The compartments
        // here only touch the hull, with no door to it, so the whole definition is rebuilt rather than asking the shipwright to demolish.)
        var secondId = world.Ship.Rooms.First(r => r.Name == second.DisplayName).Id;
        if (!ShipDetachment.TryCompute(world.Ship, secondId, out var shrunk, out _) || shrunk is null)
            return false;
        var reloaded = Ship.FromCustomDefinition(shrunk);
        return reloaded.Rooms.All(r => r.Name != second.DisplayName); // (and the rebuild did not trip over a door left with nothing on its sides)
    }

    // ---- the start station: the outpost the author assembled ----

    // The doors of a station's tile grid opened the way the server's TileSync opens them, so a path can go through.
    private static Station WithDoorsOpen(Station station)
    {
        foreach (var (coord, cell) in station.Tiles.Cells.ToList())
            if (cell.Wall == TileWallKind.Door)
                station.Tiles.SetDoorOpen(coord, true);
        return station;
    }

    private static Station StartStationAt(Vec2 anchor, EdgeSide side) =>
        Station.CreateFor(OutpostTestMap.GetPoint(Station.StartStationPointId), anchor, side);

    private static bool StartStation_IsTheDrawnOutpostWithItsServices()
    {
        var station = StartStationAt(Vec2.Zero, EdgeSide.Right);
        var kinds = station.Npcs.Select(n => n.Kind).ToHashSet();
        var names = station.Rooms.Select(r => r.Name).ToList();
        return station.Rooms.Count == 6 && station.Rooms[0].Name == "Стыковочный шлюз аванпоста" && station.DockRoomId == station.Rooms[0].Id
            && names.Count(n => n == "Переходный отсек аванпоста") == 2
            && names.Contains("Администрация аванпоста") && names.Contains("Реакторный отсек аванпоста") && names.Contains("Инжинерный отсек аванпоста")
            && station.Rooms.All(r => r.Rects.Count == 3) // each compartment is its drawn shape (cut corners), three pieces
            && new[] { NpcKind.Trader, NpcKind.Security, NpcKind.Recruiter, NpcKind.Mechanic, NpcKind.Administrator, NpcKind.Shipwright }.All(kinds.Contains)
            && station.Doors.Count == 5 // admin-hub, hub-hub, hub-docking, hub-engineering, hub-reactor
            && ReachableRoomIds(station.Doors, station.DockRoomId).Count == station.Rooms.Count;
    }

    // Docking only through the docking compartment: its connector is the only way in from outside, whichever side of the ship the airlock is on,
    // and the station lies on that side, turned so a port faces the ship.
    private static bool StartStation_DocksOnlyThroughTheDockingCompartmentOnEverySide()
    {
        var anchor = new Vec2(30, 20);
        foreach (var side in new[] { EdgeSide.Right, EdgeSide.Left, EdgeSide.Top, EdgeSide.Bottom })
        {
            var station = StartStationAt(anchor, side);
            var connector = station.ShipConnector;
            if (Math.Abs(connector.X - anchor.X) > 0.01 || Math.Abs(connector.Y - anchor.Y) > 0.01 || connector.RoomAId != station.DockRoomId || connector.RoomBId is not null)
                return false;
            if (station.Doors.Any(d => d.RoomAId is null || d.RoomBId is null)) // no other door opens to space
                return false;
            // The station is on the ship's airlock side, and the port is a door of the right shape in the wall facing the ship.
            var centre = station.Rooms.Aggregate(Vec2.Zero, (sum, r) => sum + r.Center) * (1.0 / station.Rooms.Count);
            var onTheRightSide = side switch
            {
                EdgeSide.Right => centre.X > anchor.X,
                EdgeSide.Left => centre.X < anchor.X,
                EdgeSide.Bottom => centre.Y > anchor.Y,
                _ => centre.Y < anchor.Y,
            };
            var rightShape = side is EdgeSide.Right or EdgeSide.Left ? connector.IsVertical : !connector.IsVertical;
            if (!onTheRightSide || !rightShape)
                return false;
            // And through that doorway the station is walkable: the dock room opens onto the ship side.
            var tiles = WithDoorsOpen(station).Tiles;
            if (tiles.Cells.Values.Count(c => c.Wall == TileWallKind.Door) < 1)
                return false;
        }
        return true;
    }

    private static bool StartStation_EveryoneStandsOnFloorTheDockCanWalkTo()
    {
        foreach (var side in new[] { EdgeSide.Right, EdgeSide.Left, EdgeSide.Top, EdgeSide.Bottom })
        {
            var station = WithDoorsOpen(StartStationAt(Vec2.Zero, side));
            var dock = station.Rooms.First(r => r.Id == station.DockRoomId);
            var start = new TileCoord((int)Math.Floor(dock.Center.X), (int)Math.Floor(dock.Center.Y));
            foreach (var (x, y) in station.Npcs.Select(n => (X: n.X, Y: n.Y)).Concat(station.Crates.Select(c => (X: c.X, Y: c.Y))))
            {
                var goal = new TileCoord((int)Math.Floor(x), (int)Math.Floor(y));
                if (station.Tiles.CellAt(goal) is not { Wall: TileWallKind.None })
                    return false;
                if (TilePathfinder.FindPath(station.Tiles, start, goal) is null)
                    return false;
            }
        }
        return true;
    }

    private static bool StartStation_IsSealedAndKeepsTheCutCorners()
    {
        var station = StartStationAt(Vec2.Zero, EdgeSide.Right);
        foreach (var (coord, cell) in station.Tiles.Cells)
        {
            if (cell.Wall != TileWallKind.None)
                continue;
            foreach (var side in new[] { TileSide.North, TileSide.South, TileSide.East, TileSide.West })
                if (station.Tiles.CellAt(side.Offset(coord)) is null)
                    return false; // open floor next to open space
        }
        // A corner of a compartment is cut: the bounding box of the first hub has tiles that are not part of the station.
        var hub = station.Rooms.First(r => r.Name == "Переходный отсек аванпоста");
        var corner = new TileCoord((int)hub.Left, (int)hub.Top);
        return station.Tiles.CellAt(corner) is null;
    }

    private static bool World_StartsDockedAtTheOutpost()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var snapshot = world.CreateSnapshot();
        return snapshot.Station.Rooms.Count == 6
            && snapshot.Station.Rooms[0].Name == "Стыковочный шлюз аванпоста"
            && snapshot.Station.Npcs.Any(n => n.Kind == NpcKind.Shipwright);
    }

    private static bool Outposts_EveryOutpostHasAShipwright()
    {
        foreach (var id in OutpostPointIds)
        {
            var station = Station.CreateFor(OutpostTestMap.GetPoint(id), Vec2.Zero);
            var kinds = station.Npcs.Select(n => n.Kind).ToHashSet();
            // The services every station needs are all still there, and the shipwright besides.
            if (!kinds.Contains(NpcKind.Shipwright) || !kinds.Contains(NpcKind.Trader) || !kinds.Contains(NpcKind.Security)
                || !kinds.Contains(NpcKind.Recruiter) || !kinds.Contains(NpcKind.Mechanic) || !kinds.Contains(NpcKind.Administrator)
                || station.Rooms.Count is < 10 or > 20)
                return false;
        }
        return true;
    }
}
