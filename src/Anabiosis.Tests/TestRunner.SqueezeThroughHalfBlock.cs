using Anabiosis.Shared.Model;

// Direct user request ("чтобы персонаж проходил боком через свободный полубок, который появляется у полублочной стены"): the free half of a
// half-block wall is 0.5 wide and the clearance box 0.7, so a person could never get into it. Now they turn sideways.
internal static partial class TestRunner
{
    // A partition across row 2 with one slot in it: tiles 0-1 and 3-4 are solid, tile 2 is a half-block wall whose solid half is the East one
    // (x 2.5-3.0), so the slot is its free West half, x 2.0-2.5: half a tile wide, one tile long.
    private static TileGrid BuildHalfBlockSlotGrid()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 6; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        for (var x = 0; x < 5; x++)
            grid.SetWall(new TileCoord(x, 2), TileWallKind.Solid);
        grid.SetWallOpenSide(new TileCoord(2, 2), TileSide.East);
        return grid;
    }

    private static Vec2 WalkUp(TileGrid grid, Vec2 start, int steps)
    {
        var position = start;
        for (var i = 0; i < steps; i++)
            position = TileMovement.MoveAlongAxis(grid, position, new Vec2(0, -0.1));
        return position;
    }

    // Straight at the middle of the slot: through it and out the other side.
    private static bool TileMovement_MoveAlongAxis_SqueezesSidewaysThroughHalfBlockSlot()
    {
        var end = WalkUp(BuildHalfBlockSlotGrid(), new Vec2(2.25, 4.5), 40);
        return end.Y < 1.6 && Math.Abs(end.X - 2.25) < 0.3;
    }

    // Aimed a little to one side: the character is brought to the opening instead of standing stuck on the corner of the wall.
    private static bool TileMovement_MoveAlongAxis_IsGuidedIntoTheSlotWhenAimedAlongsideIt()
    {
        var grid = BuildHalfBlockSlotGrid();
        foreach (var startX in new[] { 2.05, 2.15, 2.4, 2.5 })
        {
            var end = WalkUp(grid, new Vec2(startX, 4.5), 60);
            if (end.Y >= 1.6)
                return false;
        }
        return true;
    }

    // A gap that really is closed stays closed, and so does a plain wall: turning sideways does not mean walking through walls.
    private static bool TileMovement_MoveAlongAxis_StillBlocksAWallAndAGapTooNarrowEvenSideways()
    {
        var grid = BuildHalfBlockSlotGrid();
        // Aimed at the solid East half of the half-block wall, well away from the slot (x 2.5-3.0 is solid, 3.0-5.0 full tiles).
        var atWall = WalkUp(grid, new Vec2(3.75, 4.5), 40);
        if (atWall.Y < 3f + RoomLayout.CharacterRadius - 0.01)
            return false;

        // Close the slot with a full-thickness tile: nothing to squeeze through.
        var closed = BuildHalfBlockSlotGrid();
        closed.SetWallOpenSide(new TileCoord(2, 2), null);
        var atClosed = WalkUp(closed, new Vec2(2.25, 4.5), 40);
        return atClosed.Y >= 3f + RoomLayout.CharacterRadius - 0.01;
    }

    // Direct user report: after squeezing into a half-block slot "я смог ходить сквозь текстуры". Inside the slot, pushing sideways (into its
    // walls) must stop at them, however long it is held, and the way out along the slot stays open.
    private static bool TileMovement_MoveAlongAxis_InsideTheSlotSidewaysPushesStopAtTheWalls()
    {
        var grid = BuildHalfBlockSlotGrid();
        var memory = new TileMovement.MoveMemory();        // what the game keeps for every character
        var position = new Vec2(2.25, 4.5);
        for (var i = 0; i < 40; i++)                       // up to the middle of the slot
        {
            position = TileMovement.MoveAlongAxis(grid, position, new Vec2(0, -0.1), null, memory);
            if (position.Y < 2.55)
                break;
        }
        if (position.Y > 2.7 || position.Y < 2.3)
            return false;                                  // not in the slot: the setup, not the behaviour under test

        foreach (var direction in new[] { 1.0, -1.0, 1.0 })
            for (var i = 0; i < 80; i++)
                position = TileMovement.MoveAlongAxis(grid, position, new Vec2(0.1 * direction, 0), null, memory);
        if (position.X < 1.99 || position.X > 2.51)
            return false;                                  // walked through the slot's side walls

        // Alternate pushes along and across while shuffling inside the slot: still nothing may leak through its walls.
        for (var i = 0; i < 120; i++)
        {
            position = TileMovement.MoveAlongAxis(grid, position, new Vec2(0.1, 0), null, memory);
            position = TileMovement.MoveAlongAxis(grid, position, new Vec2(0, i % 2 == 0 ? -0.05 : 0.05), null, memory);
        }
        return position.X >= 1.99 && position.X <= 2.51;
    }
}