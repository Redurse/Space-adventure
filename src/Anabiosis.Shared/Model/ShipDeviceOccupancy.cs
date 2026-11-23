namespace Anabiosis.Shared.Model;

// Direct user request ("сделай чтобы нельзя было ходить сквозь все устройства в игре"). The ship's
// runtime TileGrid (Ship.Tiles - what TileMovement and the docked merge actually walk on) used to be
// rasterized from rooms and doors only: the editor's own grid knew every device's footprint, but
// that occupancy never survived the trip through CustomShipDefinition, so only the reactor
// (Ship.DeviceObstacles) and wall terminals/lamps blocked anyone. This stamps every placed device's
// real footprint back onto Ship.Tiles with the same primitives the editor and CompartmentPlacer use
// (PlaceDevice / PlaceHalfWidthDevice, CustomDeviceFootprint's half-width and shaped rules), so
// TileGrid.IsWalkable's existing truth table - half-claimed console tiles included - does the rest.
//
// Only a device whose exported centre sits exactly where the tile editor puts it (anchor tile +
// footprint/2, TileShipBuilder's convention) is stamped. Hand-authored layouts - the frozen default
// hull and most test fixtures - use free floats that don't map back to a real anchor, and guessing
// one already corrupted room connectivity once (see the REVERTED note at the end of
// Ship.FromCustomDefinition); those keep their old behaviour. Tiles that can't take a device (no
// floor, a wall, already claimed) are skipped rather than forced.
public static class ShipDeviceOccupancy
{
    // Not obstacles: wall-mounted (their own WallDeviceObstacles), doorways, floor conduits, and
    // hangar bays (drawn as floor, a pad you walk onto).
    private static readonly HashSet<CustomDeviceKind> Passable = new()
    {
        CustomDeviceKind.Terminal, CustomDeviceKind.WallLamp, CustomDeviceKind.TripleDoor,
        CustomDeviceKind.PowerConduit, CustomDeviceKind.ShuttleHangar, CustomDeviceKind.DroneHangar,
    };

    public static bool Blocks(CustomDeviceKind kind) => !Passable.Contains(kind);

    public static void Apply(TileGrid tiles, IEnumerable<CustomDeviceDef> devices)
    {
        var blocking = devices.Where(d => Blocks(d.Kind)).ToList();
        // Mostly off-grid means a hand-authored layout (the default hull: 2 of ~20), where a centre that
        // happens to land on the grid is a coincidence, not a real anchor - leave it alone. A ship from
        // the editor is (almost) all on-grid; the odd device saved under an older footprint size is skipped.
        if (blocking.Count(d => AnchorOf(d) is not null) * 2 <= blocking.Count)
            return;
        var index = 0;
        foreach (var device in blocking)
        {
            var id = $"occupancy-{index++}";
            if (!Blocks(device.Kind) || AnchorOf(device) is not { } anchor)
                continue;
            foreach (var (tile, openSide) in FootprintTiles(device, anchor))
                Occupy(tiles, tile, openSide, id);
        }
    }

    /// The editor anchor tile behind an exported device centre, or null for a free-placed one.
    public static TileCoord? AnchorOf(CustomDeviceDef device)
    {
        var (width, height) = CustomDeviceFootprint.Size(device.Kind);
        if (device.Rotated)
            (width, height) = (height, width);
        var ax = device.X - width / 2f;
        var ay = device.Y - height / 2f;
        var rx = MathF.Round(ax);
        var ry = MathF.Round(ay);
        if (MathF.Abs(ax - rx) > 0.01f || MathF.Abs(ay - ry) > 0.01f)
            return null;
        return new TileCoord((int)rx, (int)ry);
    }

    // Every tile the device claims, with the open half for a half-claimed one (null = whole tile).
    private static IEnumerable<(TileCoord Tile, TileSide? OpenSide)> FootprintTiles(CustomDeviceDef device, TileCoord anchor)
    {
        var shape = CustomDeviceFootprint.ShapedFootprint(device.Kind, CustomDeviceFootprint.ShapedFacing(device.Rotated, device.HalfWidthSide));
        if (shape is not null)
        {
            foreach (var t in shape)
                yield return (new TileCoord(anchor.X + t.Offset.X, anchor.Y + t.Offset.Y), t.HalfSide);
            yield break;
        }

        var (width, height) = CustomDeviceFootprint.Size(device.Kind);
        if (device.Rotated)
            (width, height) = (height, width);
        TileSide? halfSide = CustomDeviceFootprint.IsHalfWidthKind(device.Kind)
            ? CustomDeviceFootprint.ResolveHalfSide(device.HalfWidthSide, device.Rotated)
            : null;
        for (var dx = 0; dx < width; dx++)
        for (var dy = 0; dy < height; dy++)
        {
            var tile = new TileCoord(anchor.X + dx, anchor.Y + dy);
            if (halfSide is { } side && CustomDeviceFootprint.IsHalfTileOfHalfWidthFootprint(tile, anchor, side))
                yield return (tile, CustomDeviceFootprint.HalfOpenSideForHalfWidthDevice(side));
            else
                yield return (tile, null);
        }
    }

    private static void Occupy(TileGrid tiles, TileCoord tile, TileSide? openSide, string id)
    {
        if (tiles.CellAt(tile) is not { HasFloor: true } cell)
            return;
        if (openSide is { } side)
        {
            if (tiles.CanPlaceHalfWidthDevice(tile, side))
                tiles.PlaceHalfWidthDevice(tile, side, id);
            return;
        }
        if (cell.Wall == TileWallKind.None && cell.DeviceId is null)
            tiles.PlaceDevice(tile, id);
    }

    /// The free floor tile centre nearest to <paramref name="from"/> - inside <paramref name="room"/>
    /// when it has one - so nobody is ever put down inside a device or a wall.
    public static Vec2 NearestFreeFloor(TileGrid tiles, Vec2 from, Room? room)
    {
        Vec2? best = null;
        var bestDistance = double.MaxValue;
        foreach (var (coord, cell) in tiles.Cells)
        {
            var center = new Vec2(coord.X + 0.5f, coord.Y + 0.5f);
            if (!TileGrid.IsWalkable(cell) || !IsClearSpot(tiles, center))
                continue;
            if (room is not null && !room.Contains(center))
                continue;
            var distance = (center - from).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = center;
            }
        }
        if (best is null && room is not null)
            return NearestFreeFloor(tiles, from, null);
        return best ?? from;
    }

    // True when any part of a character standing at <paramref name="p"/> would be inside a device.
    public static bool OverlapsDevice(TileGrid tiles, Vec2 p)
    {
        var r = TileMovement.CharacterRadius;
        foreach (var (dx, dy) in new[] { (-r, -r), (r, -r), (-r, r), (r, r), (0f, 0f) })
        {
            var x = p.X + dx;
            var y = p.Y + dy;
            var coord = new TileCoord((int)Math.Floor(x), (int)Math.Floor(y));
            if (tiles.CellAt(coord) is { DeviceId: not null } cell && !TileGrid.IsWalkable(cell, coord, new Vec2(x, y)))
                return true;
        }
        return false;
    }

    // The whole character box (TileMovement.CharacterRadius) fits here - not hugging a device edge.
    public static bool IsClearSpot(TileGrid tiles, Vec2 center)
    {
        var r = TileMovement.CharacterRadius;
        foreach (var (dx, dy) in new[] { (-r, -r), (r, -r), (-r, r), (r, r), (0f, 0f) })
        {
            var x = center.X + dx;
            var y = center.Y + dy;
            var coord = new TileCoord((int)Math.Floor(x), (int)Math.Floor(y));
            if (tiles.CellAt(coord) is not { } cell || !TileGrid.IsWalkable(cell, coord, new Vec2(x, y)))
                return false;
        }
        return true;
    }
}
