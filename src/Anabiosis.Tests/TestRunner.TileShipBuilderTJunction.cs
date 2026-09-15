using Anabiosis.Shared.Model;

// Direct user bug report ("отсеки в игре опять не совпадают" - a T-junction whose spine's own cap
// sits above where a perpendicular arm begins) - regression coverage for TileShipBuilder.
// BuildDefinition's own step 3.6 (SupplementalWallTiles/ForcedFloorTiles) and Ship.Custom.cs's own
// application of them after TileGridRasterizer.FromRooms.
internal static partial class TestRunner
{
    // A single room shaped like an upside-down T: a 3-wide "spine" (columns 4-6, rows 0-4) whose
    // own west side has a 3-wide "arm" (columns 1-3) sharing rows 1-5 only - i.e. the arm starts one
    // row BELOW the spine's own topmost row. TileShipBuilder's step 3.5 (per-side, whole-length
    // expansion) can never widen the spine's own west edge at all here (the line is wall for rows
    // 0-1 but floor for rows 2-4, not uniform), leaving the spine's own west edge tile at row 0 - a
    // tile that is genuinely open floor - with no rect covering it at that specific row; without
    // step 3.6, TileGridRasterizer.FromRooms's own per-row same-room-coverage check (which the arm
    // only starts providing at row 1) walls it by mistake.
    private static TileGrid BuildTJunctionGrid()
    {
        var grid = new TileGrid();
        // Spine floor.
        for (var y = 0; y <= 4; y++)
            for (var x = 4; x <= 6; x++)
                grid.SetFloor(new TileCoord(x, y), true);
        // Arm floor (rows 2-4 only - one row narrower than the spine's own row range).
        for (var y = 2; y <= 4; y++)
            for (var x = 2; x <= 3; x++)
                grid.SetFloor(new TileCoord(x, y), true);

        void Wall(int x, int y)
        {
            var coord = new TileCoord(x, y);
            grid.SetFloor(coord, true); // SetWall requires a floor there first, same as the real editor's own tools always have
            grid.SetWall(coord, TileWallKind.Solid);
        }

        // Spine's own north cap and east side.
        for (var x = 4; x <= 6; x++)
            Wall(x, -1);
        for (var y = 0; y <= 4; y++)
            Wall(7, y);
        // The spine's own west side above the arm - rows 0-1, where the arm hasn't started yet.
        // This is the genuine exterior wall the bug loses track of.
        Wall(3, 0);
        Wall(3, 1);
        // Arm's own north cap and west side.
        Wall(2, 1);
        for (var y = 2; y <= 4; y++)
            Wall(1, y);
        // Spine's and arm's own south caps.
        for (var x = 4; x <= 6; x++)
            Wall(x, 5);
        for (var x = 2; x <= 3; x++)
            Wall(x, 5);

        return grid;
    }

    private static bool TileShipBuilder_TJunction_SpineCapAboveArm_StaysOpenAndGetsItsOwnWall()
    {
        var (definition, errors) = BuildTileDefinition(BuildTJunctionGrid());
        if (definition is null || errors.Count > 0)
            return false;

        // The bug's exact tile: genuinely open floor, but no room subrect's own edge covers it at
        // this specific row (the arm only starts one row further down) - must be forced back open.
        var spineCapTile = new TileCoord(4, 0);
        // The fix's own extra wall tile: the true exterior boundary one column further west than
        // the spine's own (uncovered, at this row) edge - never part of any room subrect's bounds.
        var trueWallTile = new TileCoord(3, 0);

        if (!definition.ForcedFloorTiles.Contains(spineCapTile))
            return false;
        if (!definition.SupplementalWallTiles.Contains(trueWallTile))
            return false;

        // End-to-end: rasterize the exported rooms exactly as Ship.Custom.cs does, then apply the
        // same two corrective passes, and confirm the ACTUAL final tiles - not just the definition's
        // own lists - land correctly.
        // A single, fully-connected room needs no doors/airlocks at all - nothing here exercises
        // that part of the model, so both are passed empty rather than converting CustomDoorDef.
        if (definition.Doors.Count > 0)
            return false; // setup problem - this shape should never need one
        var rooms = definition.Rooms.Select(r => new Room(r.Id, r.Name, r.Rects)).ToList();
        var finalTiles = TileGridRasterizer.FromRooms(rooms, new List<Door>(), new List<AirlockOuterDoor>());
        foreach (var coord in definition.SupplementalWallTiles)
        {
            finalTiles.SetFloor(coord, true);
            finalTiles.SetWall(coord, TileWallKind.Solid);
        }
        foreach (var coord in definition.ForcedFloorTiles)
            if (finalTiles.CellAt(coord) is { Wall: not TileWallKind.None })
                finalTiles.SetWall(coord, TileWallKind.None);

        if (finalTiles.CellAt(spineCapTile) is not { Wall: TileWallKind.None })
            return false; // the bug: this genuinely-open tile ended up walled
        if (finalTiles.CellAt(trueWallTile) is not { Wall: TileWallKind.Solid })
            return false; // the fix: the real exterior boundary must actually be solid

        // The rest of the spine (away from the jog) and the arm itself must still read as ordinary
        // open floor - the fix must not have over-corrected anything else in the room.
        return finalTiles.CellAt(new TileCoord(5, 0)) is { Wall: TileWallKind.None }
            && finalTiles.CellAt(new TileCoord(2, 3)) is { Wall: TileWallKind.None };
    }
}
