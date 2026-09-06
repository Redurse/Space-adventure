namespace SpaceAdventure.Shared.Model;

// humble-soaring-cat.md M87 (non-rectangular compartments) - decomposes an arbitrary orthogonal
// tile shape (an L, a plus, a rectangle with corners notched out - anything TileGrid.SealedRegion
// can produce) into a small union of non-overlapping axis-aligned rectangles, so TileShipBuilder
// can turn it into a multi-rect CustomRoomDef (Room.cs/CustomShipDefinition.cs's own M86) instead
// of rejecting it outright. Pure, standalone - not called from anywhere yet (that's M88).
public static class RectilinearDecomposition
{
    public readonly record struct Rect(int MinX, int MinY, int MaxX, int MaxY)
    {
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
    }

    // A sanity cap, not a theoretical limit - the scan below is a simple greedy "maximal rectangle
    // from the top-left-most remaining tile" pass, not a minimal decomposition, so a genuinely
    // gnarly hand-painted shape could in principle need more pieces than a smarter algorithm would.
    // Rejecting past this keeps every downstream consumer (wall-ring tracing, HullSkin rendering)
    // bounded rather than silently accepting an arbitrarily complex shape.
    public const int MaxPieces = 8;

    public static (IReadOnlyList<Rect>? Rects, string? Error) Decompose(IReadOnlySet<TileCoord> tiles)
    {
        if (tiles.Count == 0)
            return (null, "Область пуста.");

        var remaining = new HashSet<TileCoord>(tiles);
        var rects = new List<Rect>();

        while (remaining.Count > 0)
        {
            // Deterministic pick (top-left-most by Y then X) - makes the decomposition stable and
            // testable rather than depending on HashSet enumeration order.
            var start = remaining.Aggregate((best, t) => t.Y < best.Y || (t.Y == best.Y && t.X < best.X) ? t : best);

            // Grow right along the starting row while every next column is still open.
            var width = 1;
            while (remaining.Contains(new TileCoord(start.X + width, start.Y)))
                width++;

            // Grow down while the ENTIRE current-width row below is still open - this keeps every
            // produced rectangle a genuine solid block, never overhanging into already-claimed or
            // absent tiles.
            var height = 1;
            while (RowIsFullyOpen(remaining, start.X, start.Y + height, width))
                height++;

            for (var dx = 0; dx < width; dx++)
                for (var dy = 0; dy < height; dy++)
                    remaining.Remove(new TileCoord(start.X + dx, start.Y + dy));

            rects.Add(new Rect(start.X, start.Y, start.X + width - 1, start.Y + height - 1));

            if (rects.Count > MaxPieces)
                return (null, $"Отсек слишком сложной формы (больше {MaxPieces} прямоугольников).");
        }

        return (rects, null);
    }

    private static bool RowIsFullyOpen(HashSet<TileCoord> remaining, int startX, int y, int width)
    {
        for (var dx = 0; dx < width; dx++)
            if (!remaining.Contains(new TileCoord(startX + dx, y)))
                return false;
        return true;
    }
}
