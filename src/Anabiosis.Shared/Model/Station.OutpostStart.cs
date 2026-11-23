using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anabiosis.Shared.Model;

// The start station is not a procedural ring but the outpost the author assembled in the Ship Editor from the outpost compartments
// ("аванпост1": admin - hub - hub - docking in a row, the engineering above and the reactor below the first hub). A ship docks ONLY through
// the docking compartment ("стыковочный шлюз аванпоста"), which has two ports - at the two ends of its long corridor, one facing north and one
// south as drawn. The station is turned (never mirrored) so that the port facing the ship's airlock is the one used: north for a ship whose
// airlock is on its bottom, south for one on its top, and the same north port turned a quarter either way for the ship's right and left side.
// The compartments keep their shape (cut corners) - the rooms are multi-rect like a hull's - and are joined by the doors drawn between them.
// Who stands where is decided here, not in the drawing: the administration has the administrator, the recruiter and the shipwright, the first hub the
// trader, the second hub (nearest the docking compartment) the guard, the engineering the mechanic.
public sealed partial class Station
{
    // The point this layout is for (the game starts docked here).
    public const string StartStationPointId = "home-station";

    private const string DockingCompartmentName = "Стыковочный шлюз аванпоста";

    private static readonly JsonSerializerOptions OutpostStartOptions = new() { Converters = { new JsonStringEnumConverter() } };
    private static CustomShipDefinition? _outpostStartDefinition;
    // The drawing built as a ship once (its device lists are read, never changed) - building it costs tens of milliseconds, and the station is rebuilt
    // whenever the hull is swapped and for every test world.
    private static readonly Lazy<Ship> DrawnOutpostShip = new(() => Ship.FromCustomDefinition(OutpostStartDefinition, validate: false));
    private static CustomShipDefinition OutpostStartDefinition =>
        _outpostStartDefinition ??= JsonSerializer.Deserialize<CustomShipDefinition>(OutpostStartJson, OutpostStartOptions)!;

    // One door of the drawing, found from the segments of its door edge group: where it stands (the thin rect of the door in the drawing's own
    // frame) and which rooms are on its two sides (null = open space).
    private sealed record DrawnDoor(string Id, float X, float Y, float Width, float Height, bool Vertical, string? RoomAId, string? RoomBId, TileSide Facing);

    private static IReadOnlyList<DrawnDoor> ReadDrawnDoors(CustomShipDefinition def)
    {
        string? RoomAt(int tileX, int tileY)
        {
            var x = tileX + 0.5f;
            var y = tileY + 0.5f;
            return def.Rooms.FirstOrDefault(r => r.Rects.Any(q => x >= q.Left && x < q.Right && y >= q.Top && y < q.Bottom))?.Id;
        }

        var doors = new List<DrawnDoor>();
        foreach (var group in def.DoorEdges.GroupBy(e => e.Id))
        {
            var segments = group.OrderBy(e => e.Y).ThenBy(e => e.X).ToList();
            var first = segments[0];
            var count = segments.Count;
            var vertical = first.Side == TileSide.East; // a door on an East seam is a vertical door in a vertical wall
            var neighbour = first.Side.Offset(new TileCoord(first.X, first.Y));
            var roomA = RoomAt(first.X, first.Y);
            var roomB = RoomAt(neighbour.X, neighbour.Y);
            // Which way it faces out of the room it belongs to (only meaningful where the other side is open space).
            var facing = first.Side == TileSide.East
                ? (roomA is not null ? TileSide.East : TileSide.West)
                : (roomA is not null ? TileSide.South : TileSide.North);
            doors.Add(vertical
                ? new DrawnDoor(group.Key, first.X + 1f, segments.Min(e => e.Y) + count / 2f, 1.0f, count, true, roomA, roomB, facing)
                : new DrawnDoor(group.Key, segments.Min(e => e.X) + count / 2f, first.Y + 1f, count, 1.0f, false, roomA, roomB, facing));
        }
        return doors;
    }

    // Turns a point in the drawing's frame `turns` quarter-turns clockwise (screen axes) about the origin.
    private static (float X, float Y) TurnDrawnPoint(float x, float y, int turns)
    {
        for (var i = 0; i < ((turns % 4) + 4) % 4; i++)
            (x, y) = (-y, x);
        return (x, y);
    }

    private static TileSide TurnSide(TileSide side, int turns)
    {
        for (var i = 0; i < ((turns % 4) + 4) % 4; i++)
            side = side switch { TileSide.North => TileSide.East, TileSide.East => TileSide.South, TileSide.South => TileSide.West, _ => TileSide.North };
        return side;
    }

    public static Station CreateOutpostStart(string pointId, Vec2 connectorAnchor, EdgeSide airlockSide = EdgeSide.Right)
    {
        var def = OutpostStartDefinition;
        var dockRoom = def.Rooms.First(r => r.Name == DockingCompartmentName);
        var drawnDoors = ReadDrawnDoors(def);

        // The docking compartment's ports: its doors to open space at the ends of the corridor, one facing north and one south.
        DrawnDoor Port(TileSide facing) => drawnDoors.First(d => d.RoomAId == dockRoom.Id || d.RoomBId == dockRoom.Id
            ? (d.RoomAId is null || d.RoomBId is null) && d.Facing == facing
            : false);
        var northPort = Port(TileSide.North);
        var southPort = Port(TileSide.South);

        // Which port, and how far the station is turned, so that the port faces the ship: the station sits on the side of the ship its airlock is on.
        var (port, turns) = airlockSide switch
        {
            EdgeSide.Bottom => (northPort, 0), // the station is below the ship: its port faces north
            EdgeSide.Top => (southPort, 0),    // above the ship: faces south
            EdgeSide.Left => (northPort, 1),   // west of the ship: the north port turned to face east
            _ => (northPort, 3),               // east of the ship (Right): the north port turned to face west
        };
        var portFacing = TurnSide(port.Facing, turns);

        // Everything is turned, then moved so the port's centre lands on the ship's airlock.
        var (portX, portY) = TurnDrawnPoint(port.X, port.Y, turns);
        var shiftX = (float)connectorAnchor.X - portX;
        var shiftY = (float)connectorAnchor.Y - portY;
        (float X, float Y) Place(float x, float y)
        {
            var (tx, ty) = TurnDrawnPoint(x, y, turns);
            return (tx + shiftX, ty + shiftY);
        }
        RectF PlaceRect(RectF r)
        {
            var (x1, y1) = Place(r.Left, r.Top);
            var (x2, y2) = Place(r.Right, r.Bottom);
            return new RectF(MathF.Min(x1, x2), MathF.Min(y1, y2), MathF.Abs(x2 - x1), MathF.Abs(y2 - y1));
        }

        // The docking compartment first: it is the dock room (the ship's connector opens into it).
        var ordered = def.Rooms.OrderBy(r => r.Id == dockRoom.Id ? 0 : 1).ToList();
        var rooms = ordered.Select(r => new Room($"{pointId}-{r.Id}", r.Name, r.Rects.Select(PlaceRect).ToList())).ToList();
        string RoomId(string drawnId) => $"{pointId}-{drawnId}";

        var doors = new List<Door>();
        var doorIndex = 0;
        foreach (var d in drawnDoors)
        {
            // Only doors between two different rooms (a door inside one room is just a gap in a partition; one to open space, other than the port
            // in use, is a wall - the second port waits for a ship on the other side).
            if (d.RoomAId is null || d.RoomBId is null || d.RoomAId == d.RoomBId)
                continue;
            var rect = PlaceRect(new RectF(d.X - d.Width / 2f, d.Y - d.Height / 2f, d.Width, d.Height));
            var vertical = turns % 2 == 0 ? d.Vertical : !d.Vertical;
            doors.Add(new Door($"{pointId}-door-{doorIndex++}", RoomId(d.RoomAId), RoomId(d.RoomBId), rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f, rect.Width, rect.Height, vertical));
        }

        var (anchorX, anchorY) = connectorAnchor.AsFloat();
        var connectorVertical = portFacing is TileSide.East or TileSide.West;
        var connector = connectorVertical
            ? new Door($"{pointId}-connector", RoomId(dockRoom.Id), null, anchorX, anchorY, 1.0f, Door.StandardSpanUnits, Vertical: true)
            : new Door($"{pointId}-connector", RoomId(dockRoom.Id), null, anchorX, anchorY, Door.StandardSpanUnits, 1.0f, Vertical: false);

        // ---- the people and the crates ----
        // Floor to stand on: a tile of the room that is not on its walls (every neighbour inside the same room) and has no device on it.
        var occupied = new HashSet<(int X, int Y)>();
        foreach (var device in def.Devices)
        {
            var (w, h) = CustomDeviceFootprint.Size(device.Kind);
            if (device.Rotated)
                (w, h) = (h, w);
            var x0 = (int)MathF.Round(device.X - w / 2f);
            var y0 = (int)MathF.Round(device.Y - h / 2f);
            for (var dx = 0; dx < w; dx++)
                for (var dy = 0; dy < h; dy++)
                    occupied.Add((x0 + dx, y0 + dy));
        }
        var used = new HashSet<(int X, int Y)>();

        (float X, float Y)? FreeSpotIn(string drawnRoomId)
        {
            var room = def.Rooms.First(r => r.Id == drawnRoomId);
            bool In(int x, int y) => room.Rects.Any(q => x + 0.5f >= q.Left && x + 0.5f < q.Right && y + 0.5f >= q.Top && y + 0.5f < q.Bottom);
            var center = RoomGeometry.AreaWeightedCentroid(room.Rects);
            var best = (X: 0, Y: 0, Distance: float.MaxValue);
            foreach (var q in room.Rects)
                for (var x = (int)q.Left; x < (int)q.Right; x++)
                    for (var y = (int)q.Top; y < (int)q.Bottom; y++)
                    {
                        if (!In(x + 1, y) || !In(x - 1, y) || !In(x, y + 1) || !In(x, y - 1) || occupied.Contains((x, y)) || used.Contains((x, y)))
                            continue;
                        var distance = (float)((x + 0.5 - center.X) * (x + 0.5 - center.X) + (y + 0.5 - center.Y) * (y + 0.5 - center.Y));
                        if (distance < best.Distance)
                            best = (x, y, distance);
                    }
            if (best.Distance == float.MaxValue)
                return null;
            used.Add((best.X, best.Y));
            return (best.X + 0.5f, best.Y + 0.5f);
        }

        var npcs = new List<StationNpc>();
        var crates = new List<StationCrate>();
        void Npc(string drawnRoomId, NpcKind kind)
        {
            if (FreeSpotIn(drawnRoomId) is not { } spot)
                return;
            var (px, py) = Place(spot.X, spot.Y);
            npcs.Add(new StationNpc($"{pointId}-npc-{kind.ToString().ToLowerInvariant()}", NpcNameFor(kind), kind, px, py));
        }
        void Crate(string drawnRoomId, ItemType item)
        {
            if (FreeSpotIn(drawnRoomId) is not { } spot)
                return;
            var (px, py) = Place(spot.X, spot.Y);
            crates.Add(new StationCrate($"{pointId}-crate-{drawnRoomId}", RoomId(drawnRoomId), px, py, item));
        }

        var hubs = def.Rooms.Where(r => r.Name == "Переходный отсек аванпоста").OrderBy(r => RoomGeometry.AreaWeightedCentroid(r.Rects).X).ToList();
        var admin = def.Rooms.First(r => r.Name == "Администрация аванпоста");
        var engineering = def.Rooms.First(r => r.Name == "Инжинерный отсек аванпоста");
        var reactor = def.Rooms.First(r => r.Name == "Реакторный отсек аванпоста");
        Npc(hubs[0].Id, NpcKind.Trader);
        Npc(hubs[^1].Id, NpcKind.Security);
        Npc(admin.Id, NpcKind.Administrator);
        Npc(admin.Id, NpcKind.Recruiter);
        Npc(admin.Id, NpcKind.Shipwright);
        Npc(engineering.Id, NpcKind.Mechanic);
        Crate(admin.Id, ItemType.Mineral);
        Crate(reactor.Id, ItemType.WireSpool);
        Crate(engineering.Id, ItemType.WireSpool);

        // ---- the devices the author drew ----
        // Built once, as the ship they were drawn as, then turned and moved exactly as the rooms were: the typed lists (lamps, terminals, ...)
        // come out already resolved (which room, which half-tile side) and in the shape every renderer already draws.
        var drawnShip = DrawnOutpostShip.Value;
        var flip = turns % 2 != 0;
        bool Rot(bool rotated) => flip ? !rotated : rotated;
        TileSide Side(TileSide side) => TurnSide(side, turns);
        string Room(string drawnRoomId) => RoomId(drawnRoomId);
        var fixtures = new StationFixtures(
            drawnShip.WallLamps.Select(l => new WallLamp($"{pointId}-{l.Id}", Room(l.RoomId), Place(l.X, l.Y).X, Place(l.X, l.Y).Y, Side(l.FacingSide))).ToList(),
            drawnShip.Terminals.Select(t => new Terminal($"{pointId}-{t.Id}", Room(t.RoomId), Place(t.X, t.Y).X, Place(t.X, t.Y).Y, Side(t.FacingSide))).ToList(),
            drawnShip.JunctionBoxes.Select(j => new JunctionBox($"{pointId}-{j.Id}", Room(j.RoomId), Place(j.X, j.Y).X, Place(j.X, j.Y).Y, Rot(j.Rotated), Side(j.HalfSide))).ToList(),
            drawnShip.DecorativeDevices.Select(d => new DecorativeDevice($"{pointId}-{d.Id}", Room(d.RoomId), d.Kind, Place(d.X, d.Y).X, Place(d.X, d.Y).Y, Rot(d.Rotated), Side(d.HalfSide))).ToList(),
            drawnShip.CommsConsoles.Select(c => new CommsConsole($"{pointId}-{c.Id}", Room(c.RoomId), Place(c.X, c.Y).X, Place(c.X, c.Y).Y, Rot(c.Rotated), Side(c.HalfSide))).ToList(),
            drawnShip.ShipStatusMonitors.Select(m => new ShipStatusMonitor($"{pointId}-{m.Id}", Room(m.RoomId), Place(m.X, m.Y).X, Place(m.X, m.Y).Y, Rot(m.Rotated), Side(m.HalfSide))).ToList(),
            new[] { drawnShip.ReactorBlock }.Concat(drawnShip.ExtraReactorBlocks)
                .Select(r => new ReactorBlock($"{pointId}-{r.Id}", Room(r.RoomId), Place(r.X, r.Y).X, Place(r.X, r.Y).Y, r.SizeScale)).ToList(),
            new[] { drawnShip.DistributionBlock }.Concat(drawnShip.ExtraDistributionBlocks)
                .Select(d => new PowerDistributionBlock($"{pointId}-{d.Id}", Room(d.RoomId), Place(d.X, d.Y).X, Place(d.X, d.Y).Y)).ToList(),
            new[] { drawnShip.BatteryBlock }.Concat(drawnShip.ExtraBatteryBlocks)
                .Select(b => new BatteryBlock($"{pointId}-{b.Id}", Room(b.RoomId), Place(b.X, b.Y).X, Place(b.X, b.Y).Y)).ToList(),
            new[] { drawnShip.NavigationConsole }.Concat(drawnShip.ExtraNavigationConsoles)
                .Select(n => new NavigationConsole($"{pointId}-{n.Id}", Room(n.RoomId), Place(n.X, n.Y).X, Place(n.X, n.Y).Y, Rot(n.Rotated), Side(n.HalfSide))).ToList(),
            new[] { drawnShip.HelmConsole }.Concat(drawnShip.ExtraHelmConsoles)
                .Select(h => new HelmConsole($"{pointId}-{h.Id}", Room(h.RoomId), Place(h.X, h.Y).X, Place(h.X, h.Y).Y, Rot(h.Rotated), Side(h.HalfSide))).ToList());

        // The drawing's own walls, turned and moved like everything else. Rasterizing the compartments again (TileGridRasterizer.FromRooms) does not give
        // the same walls once the station is turned: a wall shared by two compartments lands on the tile of whichever one "leads", which flips under a
        // quarter turn, so half-block jambs and partitions ended up a tile off or missing. So the station's tiles are corrected to the drawn grid itself:
        // every tile where the drawing's wall (or its absence, or its half-block side) differs from what the rasterizer gave is listed, except the tiles
        // of the doorways - those are the station's own doors (rect doors) or, for a port not in use, a wall.
        // A tile is the unit square from (x, y): turn its two opposite corners and take the smaller one.
        TileCoord Tile(int x, int y)
        {
            var a = Place(x, y);
            var b = Place(x + 1, y + 1);
            return new TileCoord((int)MathF.Round(MathF.Min(a.X, b.X)), (int)MathF.Round(MathF.Min(a.Y, b.Y)));
        }
        var gapTiles = def.DoorEdges
            .SelectMany(e => new[] { new TileCoord(e.X, e.Y), e.Side.Offset(new TileCoord(e.X, e.Y)) })
            .Select(t => Tile(t.X, t.Y))
            .ToHashSet();

        var station = new Station(rooms, doors, connector, npcs, crates, WorldCenter, RoomId(dockRoom.Id)) { Fixtures = fixtures };

        var fixWalls = new List<TileCoord>();
        var fixFloor = new List<TileCoord>();
        var fixHalf = new List<CustomWallOpenSideDef>();
        foreach (var (drawnCoord, drawnCell) in DrawnOutpostShip.Value.Tiles.Cells)
        {
            var coord = Tile(drawnCoord.X, drawnCoord.Y);
            if (!drawnCell.HasFloor || gapTiles.Contains(coord))
                continue;
            var baseCell = station.Tiles.CellAt(coord);
            if (baseCell is { Wall: TileWallKind.Door })
                continue;
            if (drawnCell.Wall == TileWallKind.Solid)
            {
                var halfSide = drawnCell.WallOpenSide is { } open ? Side(open) : (TileSide?)null;
                if (baseCell is { Wall: TileWallKind.Solid } && baseCell.WallOpenSide == halfSide)
                    continue; // already as drawn
                fixWalls.Add(coord);
                if (halfSide is { } openSide)
                    fixHalf.Add(new CustomWallOpenSideDef(coord.X, coord.Y, openSide));
            }
            else if (drawnCell.Wall == TileWallKind.None && baseCell is { Wall: not TileWallKind.None })
                fixFloor.Add(coord);
        }
        station.TileFixes = new StationTileFixes(fixWalls, fixFloor, fixHalf);
        station.TileFixes.Apply(station.Tiles);

        // They are solid: stamped onto the station's tiles the way a ship's devices are onto its own (nobody walks through the reactor).
        var turnedDevices = def.Devices.Select(d =>
        {
            var p = Place(d.X, d.Y);
            return d with
            {
                X = p.X, Y = p.Y, Rotated = Rot(d.Rotated),
                HalfWidthSide = d.HalfWidthSide is { } half ? Side(half) : null,
                WallDeviceFacingSide = d.WallDeviceFacingSide is { } facing ? Side(facing) : null,
            };
        }).ToList();
        ShipDeviceOccupancy.Apply(station.Tiles, turnedDevices);
        return station;
    }
}
