using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // Direct user request ("не угловые клетки занимали только половину блока которая была ближе к
    // космосу... через неё можно ходить") - a direct regression test for the actual player-facing
    // behavior change, through TileMovement's real corner-sampling MoveAlongAxis (not just the
    // position-aware IsWalkable unit tests in TestRunner.TileGrid.cs). A straight Top-edge wall
    // tile's own North half stays solid, but its South half is now walkable - a character starting
    // well inside the room and walking straight up should stop half a tile earlier than the old
    // full-thickness model would have allowed.
    private static bool TileMovement_MoveAlongAxis_WalksIntoHalfThickWallsFreeHalf()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        // Every tile of row 0 is a straight Top-edge run except the two corners (0,0)/(4,0).
        for (var x = 0; x < 5; x++)
        {
            grid.SetWall(new TileCoord(x, 0), TileWallKind.Solid);
            if (x is not (0 or 4))
                grid.SetWallOpenSide(new TileCoord(x, 0), TileSide.North);
        }

        var next = TileMovement.MoveAlongAxis(grid, new Vec2(2.5, 3), new Vec2(0, -3));
        return Math.Abs(next.Y - (0.5 + RoomLayout.CharacterRadius)) < 0.01;
    }

    // The same wall tile, but at a genuine corner (no WallOpenSide) - full thickness, unchanged from
    // before this feature existed. A regression guard against the free-half logic accidentally
    // leaking onto corners.
    private static bool TileMovement_MoveAlongAxis_StillBlocksFullyAtACorner()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        // Same row-0 wall as the free-half test above, but WallOpenSide left null everywhere - as
        // if every tile were a corner.
        for (var x = 0; x < 5; x++)
            grid.SetWall(new TileCoord(x, 0), TileWallKind.Solid);

        var next = TileMovement.MoveAlongAxis(grid, new Vec2(2.5, 3), new Vec2(0, -3));
        return Math.Abs(next.Y - (1f + RoomLayout.CharacterRadius)) < 0.01;
    }

    // Direct user bug report ("через малую сторону полублочной стены можно пройти внутрь") - a lone
    // half-block wall in open floor, its solid East half (x 2.5-3.0) only 0.5 thick. Walking head-on
    // into its narrow South end with the character centred on that strip used to put the clearance
    // box's two corner columns (x 2.4 and 3.1) either side of the strip, so nothing was sampled
    // inside it and the character walked straight through. Must now stop at the strip's end face.
    private static bool TileMovement_MoveAlongAxis_BlocksHalfThickWallsNarrowEnd()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        grid.SetWall(new TileCoord(2, 2), TileWallKind.Solid);
        grid.SetWallOpenSide(new TileCoord(2, 2), TileSide.East);

        // Small per-tick steps like real movement - MoveAlongAxis only checks where a move lands, so
        // one 3-unit jump would hop clean over a 1-tile wall regardless of this fix.
        var position = new Vec2(2.75, 4.5);
        for (var i = 0; i < 30; i++)
            position = TileMovement.MoveAlongAxis(grid, position, new Vec2(0, -0.1));
        return Math.Abs(position.Y - (3f + RoomLayout.CharacterRadius)) < 0.01;
    }
}
