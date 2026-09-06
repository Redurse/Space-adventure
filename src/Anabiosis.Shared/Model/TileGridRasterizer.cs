namespace Anabiosis.Shared.Model;

// M71 (humble-soaring-cat.md) - pure, one-way projection of the existing rectangle hull model
// (Room/Door/AirlockOuterDoor) onto the new TileGrid (TileGrid.cs). Called ADDITIVELY from
// Ship/Station/EnemyShipLayout's own constructors (see each type's `Tiles` property) so both
// representations exist side by side, generated from the SAME source of truth (the old rectangle
// lists), until each dependent system (atmosphere, movement, rendering...) migrates to reading
// TileGrid instead of Rooms/Doors/WallBlocks, one milestone at a time. Nothing production reads
// `Tiles` yet - this class exists purely to prove the projection is safe before anything depends on
// it.
//
// Deliberately does NOT take WallBlock as input, even though the old model has one: WallBlock only
// exists for hull-cutting/breach bookkeeping, and its coverage rule is the OPPOSITE of what this
// rasterizer needs. Station.BuildWallBlocks (reused by EnemyShipLayout) only generates a WallBlock
// for an edge that has NO neighboring room on the other side - a shared interior boundary between
// two rooms gets no WallBlock at all, because the OLD model's interior collision comes purely from
// RoomLayout.MoveAlongAxis clamping a character to its own room's rectangle, never from a WallBlock
// object. The new tile model has no such rectangle-clamp fallback - every room-to-room boundary
// needs an explicit wall/door TILE - so walls are re-derived directly from Room/Door/AirlockOuterDoor
// geometry instead, using the same per-unit-segment adjacency test Station.IsUnitCovered already
// uses for the opposite purpose.
//
// To keep an interior boundary exactly ONE tile thick (not two), each room walls its own LEADING
// edges (Left/Top) unconditionally, but only walls a TRAILING edge (Right/Bottom) when no
// neighboring room's rectangle already covers that unit segment - when one does, that neighbor's own
// Left/Top pass already placed the (single, shared) separating wall tile at that same boundary line.
//
// Rounding note: every hand-authored hull built via Ship.CreateStarter/.Scout/.Cruiser uses whole-
// integer coordinates throughout and rasterizes exactly. Ship.Corvette.cs (and the enemy Frigate/
// Freighter/Gunship classes, and every procedurally generated Station) deliberately place rooms on a
// half-unit grid instead ("room edges land on halves of a unit" - Ship.Corvette.cs's own comment),
// which a 1-unit tile cannot represent exactly; coordinates there round to the nearest tile
// (MidpointRounding.AwayFromZero) rather than being treated as a special case. Callers/tests should
// expect an EXACT tile footprint only for whole-integer hulls - the topology (one region per room,
// doors connecting exactly the right pair) holds regardless.
public static class TileGridRasterizer
{
    public static TileGrid FromRooms(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<Door> doors,
        IReadOnlyList<AirlockOuterDoor> airlockOuterDoors)
    {
        var grid = new TileGrid();

        // Floor first - TileGrid.SetWall requires a floor already at the target coordinate, and a
        // wall/door tile is always also a floor tile in the new model (walls sit ON TOP of floor).
        // Generalized (humble-soaring-cat.md M90) to walk each room's own subrects independently -
        // byte-identical to the old per-room walk whenever every room has exactly one rect (every
        // hand-authored hull/station forever), since room.Rects then has exactly one element equal
        // to the bbox.
        foreach (var room in rooms)
            foreach (var rect in room.Rects)
            {
                var left = RoundToInt(rect.X);
                var right = RoundToInt(rect.Right);
                var top = RoundToInt(rect.Y);
                var bottom = RoundToInt(rect.Bottom);
                for (var x = left; x < right; x++)
                    for (var y = top; y < bottom; y++)
                        grid.SetFloor(new TileCoord(x, y), true);
            }

        foreach (var room in rooms)
            foreach (var rect in room.Rects)
            {
                var left = RoundToInt(rect.X);
                var right = RoundToInt(rect.Right);
                var top = RoundToInt(rect.Y);
                var bottom = RoundToInt(rect.Bottom);

                // Leading edges (Left/Top) are no longer unconditional on their own - a multi-rect
                // room's own subrect can have ANOTHER of its own pieces sitting flush against this
                // exact edge (an internal seam, e.g. an L-shape's lower arm has its own Top edge
                // exactly where the upper arm's Bottom edge already sits) - that never gets a wall
                // tile at all, on either side, since it's one continuous floor. Genuinely facing a
                // DIFFERENT room still gets the wall unconditionally here, same as before (that
                // room's own trailing-side check below is what stays silent for its half).
                for (var y = top; y < bottom; y++)
                    if (!Station.IsUnitCoveredBySameRoom(room, rect, EdgeSide.Left, y))
                        WallTile(grid, new TileCoord(left, y));
                for (var x = left; x < right; x++)
                    if (!Station.IsUnitCoveredBySameRoom(room, rect, EdgeSide.Top, x))
                        WallTile(grid, new TileCoord(x, top));

                for (var y = rect.Y; y < rect.Bottom; y += 1f)
                    if (!Station.IsUnitCoveredBySameRoom(room, rect, EdgeSide.Right, y) && !Station.IsUnitCoveredByOtherRoom(rooms, room, rect, EdgeSide.Right, y))
                        WallTile(grid, new TileCoord(right - 1, RoundToInt(y)));
                for (var x = rect.X; x < rect.Right; x += 1f)
                    if (!Station.IsUnitCoveredBySameRoom(room, rect, EdgeSide.Bottom, x) && !Station.IsUnitCoveredByOtherRoom(rooms, room, rect, EdgeSide.Bottom, x))
                        WallTile(grid, new TileCoord(RoundToInt(x), bottom - 1));
            }

        // A Door/AirlockOuterDoor is a StandardSpanUnits(2)-wide rectangle centered on the wall it
        // sits in; whichever of Width/Height equals 1 tells you the wall's orientation (Door.cs).
        // Rasterized last so an opening always overrides whatever wall tile was placed above.
        //
        // Naively rounding a door's own center/width symmetrically (independent of any room) looks
        // like it should always land on the same tile as the wall it replaces, since the door sits
        // AT that boundary - but it only actually agrees for a LEADING edge (Left/Top, walled at
        // RoundToInt(edge) - the same thing symmetric rounding of a centered door produces) on a
        // WHOLE-INTEGER boundary specifically. It disagrees in two real cases, both hit live: (1) a
        // TRAILING edge (Right/Bottom) that's genuinely exterior - its wall sits one tile further IN,
        // at RoundToInt(edge)-1, not RoundToInt(edge) (every starter-hull test exiting through the
        // stern airlock at x=26, airlock-chamber's own unshared trailing edge, hit exactly this - the
        // door rasterized one tile away from its own wall, leaving it floating and unreachable); (2) a
        // half-unit boundary (Ship.Corvette.cs's hulls) even on a LEADING edge - RoundToInt(9.5-0.5) =
        // RoundToInt(9.0) = 9 for the door's own math, but RoundToInt(9.5) = 10 for the wall's, because
        // subtracting 0.5 before rounding and rounding the raw edge value diverge exactly on a .5
        // input (World_Eva_CorvetteCrewGoesOutThroughABeamPort hit this). So every door/airlock is
        // rasterized against whichever room(s) actually border it, using the exact same
        // leading/trailing rule the walls above just used - never its own independent center/width
        // rounding on the perpendicular (wall-thickness) axis.
        foreach (var door in doors)
            RasterizeDoor(grid, rooms, door.X, door.Y, door.Width, door.Height);
        foreach (var airlock in airlockOuterDoors)
            RasterizeDoor(grid, new[] { rooms.First(r => r.Id == airlock.RoomId) }, airlock.X, airlock.Y, airlock.Width, airlock.Height);

        return grid;
    }

    private static void WallTile(TileGrid grid, TileCoord coord)
    {
        EnsureFloor(grid, coord);
        grid.SetWall(coord, TileWallKind.Solid);
    }

    private static void RasterizeDoor(TileGrid grid, IReadOnlyList<Room> rooms, float centerX, float centerY, float width, float height)
    {
        foreach (var coord in DoorTileCoords(rooms, centerX, centerY, width, height))
        {
            EnsureFloor(grid, coord);
            grid.SetWall(coord, TileWallKind.Door);
        }
    }

    // M72/M73 (World.TileSync.cs) reuses this to find which live tile(s) a Door/AirlockOuterDoor's
    // id maps to, without needing a stored mapping. `rooms` should be every room that might border
    // this door - the ship/station's full room list for a regular Door, or a single-element list
    // with just the airlock's own RoomId for an AirlockOuterDoor (see FromRooms's own comment on why
    // this needs to be room-aware rather than a pure function of the door's own X/Y/Width/Height).
    public static IEnumerable<TileCoord> DoorTileCoords(IReadOnlyList<Room> rooms, float centerX, float centerY, float width, float height)
    {
        var left = RoundToInt(centerX - width / 2f);
        var right = RoundToInt(centerX + width / 2f);
        var top = RoundToInt(centerY - height / 2f);
        var bottom = RoundToInt(centerY + height / 2f);

        if (width <= height) // thin in X - a door through a Left/Right wall; X is the perpendicular
                              // (wall-thickness) axis that needs room-edge correction, Y is the span
        {
            var column = PerpendicularCoord(rooms, centerX, vertical: true);
            left = column;
            right = column + 1;
        }
        else
        {
            var row = PerpendicularCoord(rooms, centerY, vertical: false);
            top = row;
            bottom = row + 1;
        }

        for (var x = left; x < right; x++)
            for (var y = top; y < bottom; y++)
                yield return new TileCoord(x, y);
    }

    // Same tiles DoorTileCoords resolves to, collapsed into one world-unit rectangle - what the
    // CLIENT should actually render a ship-side door as (ShipRenderer.Draw), so its sprite lines up
    // with DrawShipWalls' own tile-square wall art around it. The door's raw geometric
    // Left/Top/Width/Height sits centered exactly ON the room boundary - half a tile off from where
    // the leading-edge convention above actually places the solid wall tiles flanking it (bug
    // report: "дверь стоит не на своём месте, как будто она съехала"). Station/boarding doors are
    // NOT run through this - StationRenderer/BoardingRenderer still draw walls the old,
    // boundary-centered way (M75's own doc comment), where the door's own raw rect already lines up.
    public static (float Left, float Top, float Width, float Height) DoorTileRect(
        IReadOnlyList<Room> rooms, float centerX, float centerY, float width, float height)
    {
        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;
        foreach (var coord in DoorTileCoords(rooms, centerX, centerY, width, height))
        {
            if (coord.X < minX) minX = coord.X;
            if (coord.Y < minY) minY = coord.Y;
            if (coord.X > maxX) maxX = coord.X;
            if (coord.Y > maxY) maxY = coord.Y;
        }
        if (maxX < minX)
            return (centerX - width / 2f, centerY - height / 2f, width, height);

        return (minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    // Which tile a wall at this exact boundary value would occupy: RoundToInt(edge) if some room's
    // own SUBRECT treats it as a LEADING edge (Left/Top - always walled), else RoundToInt(edge)-1 for
    // a TRAILING edge (Right/Bottom - only walled, one tile further in, when no neighbor covers it -
    // which is guaranteed true here since a door/airlock already sits on this boundary). Generalized
    // (M90) to search every room's flattened subrects instead of its bbox - byte-identical to the
    // old per-room search whenever every room has exactly one rect (every hand-authored hull/station
    // forever).
    private static int PerpendicularCoord(IReadOnlyList<Room> rooms, float edgeValue, bool vertical)
    {
        const float epsilon = 0.01f;
        foreach (var room in rooms)
            foreach (var rect in room.Rects)
                if (vertical ? MathF.Abs(rect.X - edgeValue) < epsilon : MathF.Abs(rect.Y - edgeValue) < epsilon)
                    return RoundToInt(edgeValue);
        foreach (var room in rooms)
            foreach (var rect in room.Rects)
                if (vertical ? MathF.Abs(rect.Right - edgeValue) < epsilon : MathF.Abs(rect.Bottom - edgeValue) < epsilon)
                    return RoundToInt(edgeValue) - 1;
        return RoundToInt(edgeValue); // not on any given room's own edge - fall back
    }

    // M72 (World.TileSync.cs) - unlike a Door, a WallBlock carries no orientation/edge tag of its
    // own, and its coordinate convention doesn't match this rasterizer's leading/trailing rule
    // directly (see the class comment) - so the edge has to be inferred by comparing the block's
    // center against its OWNING room's own boundary; each block matches exactly one edge by
    // construction (Ship.GenerateOuterWallBlocks/.GenerateInteriorWallBlocks/Station.BuildWallBlocks
    // each place every block by stepping along exactly one edge).
    //
    // Bug fix (humble-soaring-cat.md, "стены не имеют коллизии" follow-up) - used to take only the
    // block's OWN room and blindly apply "Left/Top -> no adjustment, Right/Bottom -> -1", the same
    // shortcut that's only valid for a room's OWN outer edges. Ship.GenerateInteriorWallBlocks
    // assigns an interior block's RoomId to whichever of the two rooms happens to come first in the
    // hull's own room list - NOT necessarily the one whose LEADING edge is what actually placed the
    // wall tile (TileGridRasterizer.FromRooms's own leading-always/trailing-only-if-uncovered rule).
    // Whenever the block's room lost that coin flip (its own edge here is really a COVERED trailing
    // edge, the neighbor's leading edge owns the tile instead), the old "-1" blindly fired anyway,
    // computing a coordinate one tile off from where the wall/breach genuinely lives - so a fully cut
    // interior bulkhead never actually opened up in Ship.Tiles (TileMovement kept treating it as
    // solid) and World.Atmosphere.cs's leak-rate read the wrong tile's HP too. Now routed through the
    // same room-list-aware PerpendicularCoord logic the Door/AirlockOuterDoor case above already
    // uses for exactly this leading/trailing ambiguity - needs every room in the hull, not just the
    // block's own, to know whether a neighbor's leading edge actually claimed this boundary first.
    // Generalized (M90) to find which of `room`'s own SUBRECTS this block actually sits on (a
    // block's position always lands exactly on one of a room's own subrect edges - Ship.Custom.cs's
    // BuildWallBlocks/GenerateInteriorWallBlocks never place one anywhere else) instead of assuming
    // the room's bbox edge directly - byte-identical to the old 4-branch check whenever room.Rects
    // has exactly one element equal to the bbox (every hand-authored hull/station forever).
    public static TileCoord WallBlockTileCoord(WallBlock block, IReadOnlyList<Room> rooms, Room room)
    {
        const float epsilon = 0.01f;
        foreach (var rect in room.Rects)
        {
            if (MathF.Abs(block.Y - rect.Y) < epsilon && block.X > rect.X - epsilon && block.X < rect.Right + epsilon)
                return new TileCoord(RoundToInt(block.X - 0.5f), PerpendicularCoord(rooms, rect.Y, vertical: false));
            if (MathF.Abs(block.Y - rect.Bottom) < epsilon && block.X > rect.X - epsilon && block.X < rect.Right + epsilon)
                return new TileCoord(RoundToInt(block.X - 0.5f), PerpendicularCoord(rooms, rect.Bottom, vertical: false));
            if (MathF.Abs(block.X - rect.X) < epsilon && block.Y > rect.Y - epsilon && block.Y < rect.Bottom + epsilon)
                return new TileCoord(PerpendicularCoord(rooms, rect.X, vertical: true), RoundToInt(block.Y - 0.5f));
            if (MathF.Abs(block.X - rect.Right) < epsilon && block.Y > rect.Y - epsilon && block.Y < rect.Bottom + epsilon)
                return new TileCoord(PerpendicularCoord(rooms, rect.Right, vertical: true), RoundToInt(block.Y - 0.5f));
        }
        throw new InvalidOperationException($"WallBlock {block.Id} doesn't lie on any edge of room {room.Id}.");
    }

    // A wall/door tile's rounded coordinate can, for a half-unit-grid hull, land just outside the
    // floor tiles the room rectangle itself produced - keep the projection total (never throw)
    // rather than assume every hull rounds perfectly consistently.
    private static void EnsureFloor(TileGrid grid, TileCoord coord)
    {
        if (grid.CellAt(coord) is not { HasFloor: true })
            grid.SetFloor(coord, true);
    }

    private static int RoundToInt(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);
}
