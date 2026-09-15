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
}
