using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    private static HashSet<TileCoord> Rect(int minX, int minY, int width, int height)
    {
        var tiles = new HashSet<TileCoord>();
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                tiles.Add(new TileCoord(minX + x, minY + y));
        return tiles;
    }

    private static bool CoversExactly(IReadOnlyList<RectilinearDecomposition.Rect> rects, HashSet<TileCoord> tiles)
    {
        var covered = new HashSet<TileCoord>();
        foreach (var rect in rects)
            for (var x = rect.MinX; x <= rect.MaxX; x++)
                for (var y = rect.MinY; y <= rect.MaxY; y++)
                {
                    var coord = new TileCoord(x, y);
                    if (!tiles.Contains(coord) || !covered.Add(coord))
                        return false; // painted outside the shape, or two rects overlapped
                }
        return covered.Count == tiles.Count;
    }

    // The existing rectangular case (every hand-authored/editor-drawn room today) must not regress
    // to more than 1 piece.
    private static bool RectilinearDecomposition_PlainRectangle_YieldsExactlyOnePiece()
    {
        var tiles = Rect(0, 0, 5, 4);
        var (rects, error) = RectilinearDecomposition.Decompose(tiles);
        return error is null && rects is { Count: 1 } && CoversExactly(rects, tiles);
    }

    // An L: a 4x2 arm plus a 2x4 arm sharing a 2x2 corner.
    private static bool RectilinearDecomposition_LShape_DecomposesCleanly()
    {
        var tiles = Rect(0, 0, 4, 2);
        foreach (var t in Rect(0, 2, 2, 4))
            tiles.Add(t);
        var (rects, error) = RectilinearDecomposition.Decompose(tiles);
        return error is null && rects is { Count: <= 2 } && CoversExactly(rects, tiles);
    }

    // A plus/cross: a horizontal bar and a vertical bar crossing in the middle.
    private static bool RectilinearDecomposition_PlusShape_DecomposesCleanly()
    {
        var tiles = Rect(0, 2, 7, 3); // horizontal bar, y in [2,4]
        foreach (var t in Rect(2, 0, 3, 7)) // vertical bar, x in [2,4]
            tiles.Add(t);
        var (rects, error) = RectilinearDecomposition.Decompose(tiles);
        return error is null && rects is { Count: <= 3 } && CoversExactly(rects, tiles);
    }

    // A notched-corner/octagon-ish shape - a big rectangle with a 1x1 tile shaved off each corner,
    // exactly the class of shape the user's reactor-compartment references describe.
    private static bool RectilinearDecomposition_NotchedCorners_DecomposesCleanly()
    {
        var tiles = Rect(0, 0, 6, 6);
        tiles.Remove(new TileCoord(0, 0));
        tiles.Remove(new TileCoord(5, 0));
        tiles.Remove(new TileCoord(0, 5));
        tiles.Remove(new TileCoord(5, 5));
        var (rects, error) = RectilinearDecomposition.Decompose(tiles);
        return error is null && rects is { Count: > 0 } && CoversExactly(rects, tiles);
    }

    // A U-shape (an engine-pylon-style hull cross-section) - more pieces than an L/plus, but still
    // well within MaxPieces.
    private static bool RectilinearDecomposition_UShape_DecomposesCleanly()
    {
        var tiles = Rect(0, 0, 2, 5);
        foreach (var t in Rect(4, 0, 2, 5))
            tiles.Add(t);
        foreach (var t in Rect(0, 3, 6, 2))
            tiles.Add(t);
        var (rects, error) = RectilinearDecomposition.Decompose(tiles);
        return error is null && rects is { Count: > 0 } && CoversExactly(rects, tiles);
    }

    // A checkerboard-like shape needs one rectangle per isolated cell - deliberately pathological,
    // must be rejected cleanly rather than accepted with an absurd piece count.
    private static bool RectilinearDecomposition_TooComplexShape_FailsCleanly()
    {
        var tiles = new HashSet<TileCoord>();
        for (var x = 0; x < 8; x++)
            for (var y = 0; y < 8; y++)
                if ((x + y) % 2 == 0)
                    tiles.Add(new TileCoord(x, y));
        var (rects, error) = RectilinearDecomposition.Decompose(tiles);
        return rects is null && error is not null;
    }

    // Idempotency: re-decomposing the union of an already-produced rect set must reproduce a valid
    // decomposition (each piece already IS a plain rectangle, so this must yield back exactly the
    // same pieces, not merge/split them differently).
    private static bool RectilinearDecomposition_Idempotent_OnItsOwnOutput()
    {
        var tiles = Rect(0, 0, 4, 2);
        foreach (var t in Rect(0, 2, 2, 4))
            tiles.Add(t);
        var (first, error1) = RectilinearDecomposition.Decompose(tiles);
        if (error1 is not null || first is null)
            return false;

        var rebuilt = new HashSet<TileCoord>();
        foreach (var rect in first)
            for (var x = rect.MinX; x <= rect.MaxX; x++)
                for (var y = rect.MinY; y <= rect.MaxY; y++)
                    rebuilt.Add(new TileCoord(x, y));

        var (second, error2) = RectilinearDecomposition.Decompose(rebuilt);
        return error2 is null && second is not null && CoversExactly(second, rebuilt);
    }

    private static bool RectilinearDecomposition_EmptyShape_FailsCleanly()
    {
        var (rects, error) = RectilinearDecomposition.Decompose(new HashSet<TileCoord>());
        return rects is null && error is not null;
    }
}
