using System.Collections.Generic;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// M78 (humble-soaring-cat.md) - the tile-native equivalent of Occluders.Build: instead of walking a
// list of Room rectangles, walks a live TileGrid and traces the boundary of its occluding tiles
// (solid walls, and closed doors - see IsOccluding below) directly. Unlike a Room rectangle, a
// TileGrid can represent a non-rectangular hull footprint (free-tile-painted, an L-shaped bay, a
// partially detached section), which the old rectangle-only system had no way to express at all.
//
// Gap-cutting (turning an open door/airlock/window/breach into a hole in the wall) is NOT
// reimplemented here - it's the exact same concern regardless of whether the raw wall run came from
// a Room's rectangle side or a run of tile edges, so this reuses Occluders.AddHorizontal/AddVertical
// (now internal for this purpose) rather than risking the two paths drifting apart on the rule.
public static class TileOccluders
{
    // A tile blocks sight if it's a solid wall, or a door that's currently shut - a shut bulkhead
    // blocks the view exactly like hull (Occluders.cs's own SightGap doc comment). An OPEN door tile
    // is deliberately treated as simply non-occluding (no wall segment at all) rather than as an
    // occluding tile with a gap cut through it: the caller already adds an explicit SightGap for an
    // open door/airlock (Game1.Lighting.cs), so "no wall here" and "wall here, then a gap cut through
    // it" produce the identical surviving-span result - just by a simpler path for the tile case,
    // since a tile has no independent existence as a wall once its door is open.
    private static bool IsOccluding(TileCell? cell) =>
        cell is { Wall: TileWallKind.Solid } or { Wall: TileWallKind.Door, DoorOpen: false };

    public static List<WallSegment> Build(TileGrid tiles, IReadOnlyList<SightGap> gaps)
    {
        // Raw 1-unit boundary edges, bucketed by their fixed axis coordinate (Y for a horizontal
        // edge, X for a vertical one) so touching edges on the same line can be merged into one run
        // before they ever reach the gap-cutting/raycast stage - ShadowCast tests every segment every
        // frame, so leaving hundreds of unmerged unit-length segments would be a real cost, not just
        // untidy.
        var horizontal = new Dictionary<int, List<(int From, int To)>>();
        var vertical = new Dictionary<int, List<(int From, int To)>>();

        foreach (var (coord, cell) in tiles.Cells)
        {
            if (!IsOccluding(cell))
                continue;

            foreach (var side in TileSideExtensions.All)
            {
                if (IsOccluding(tiles.CellAt(side.Offset(coord))))
                    continue; // shared face between two occluding tiles - not a boundary

                switch (side)
                {
                    case TileSide.North: // this tile's own top edge: (x, y) to (x+1, y)
                        AddUnitEdge(horizontal, coord.Y, coord.X);
                        break;
                    case TileSide.South: // bottom edge: (x, y+1) to (x+1, y+1)
                        AddUnitEdge(horizontal, coord.Y + 1, coord.X);
                        break;
                    case TileSide.West: // left edge: (x, y) to (x, y+1)
                        AddUnitEdge(vertical, coord.X, coord.Y);
                        break;
                    case TileSide.East: // right edge: (x+1, y) to (x+1, y+1)
                        AddUnitEdge(vertical, coord.X + 1, coord.Y);
                        break;
                }
            }
        }

        var segments = new List<WallSegment>();
        foreach (var (y, spans) in horizontal)
            foreach (var (from, to) in MergeRuns(spans))
                Occluders.AddHorizontal(segments, y, from, to, gaps);
        foreach (var (x, spans) in vertical)
            foreach (var (from, to) in MergeRuns(spans))
                Occluders.AddVertical(segments, x, from, to, gaps);
        return segments;
    }

    private static void AddUnitEdge(Dictionary<int, List<(int From, int To)>> into, int fixedCoord, int from)
    {
        if (!into.TryGetValue(fixedCoord, out var spans))
            into[fixedCoord] = spans = new List<(int, int)>();
        spans.Add((from, from + 1));
    }

    // Coalesces touching/overlapping unit spans on the same line into the fewest possible runs -
    // e.g. tile edges [1,2) and [2,3) merge into [1,3). Spans never actually overlap by more than a
    // shared endpoint (each comes from exactly one tile's own unit-wide edge), but sorting first
    // means a single left-to-right sweep is enough regardless of insertion order.
    private static List<(int From, int To)> MergeRuns(List<(int From, int To)> spans)
    {
        spans.Sort((a, b) => a.From.CompareTo(b.From));
        var merged = new List<(int From, int To)>();
        var currentFrom = spans[0].From;
        var currentTo = spans[0].To;
        for (var i = 1; i < spans.Count; i++)
        {
            var (from, to) = spans[i];
            if (from <= currentTo)
            {
                if (to > currentTo)
                    currentTo = to;
            }
            else
            {
                merged.Add((currentFrom, currentTo));
                currentFrom = from;
                currentTo = to;
            }
        }
        merged.Add((currentFrom, currentTo));
        return merged;
    }
}
