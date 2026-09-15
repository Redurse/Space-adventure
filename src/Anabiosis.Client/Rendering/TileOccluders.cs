using System.Collections.Generic;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

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
    // Direct user bug report ("не вижу ничего через стену являющейся иллюминатором") - a Window
    // wall (WallMaterial.cs) is still a real, solid obstacle for MOVEMENT (TileMovement.cs's own
    // IsWalkable never reads WallMaterial at all - a window still blocks walking through it), but
    // it's meant to be seen through, same as a real ship's porthole - excluded here so neither the
    // player's own sight cone nor room lighting treats it as a wall for shadow-casting purposes.
    private static bool IsOccluding(TileCell? cell) =>
        cell is { Wall: TileWallKind.Solid, WallMaterial: not WallMaterial.Window } or { Wall: TileWallKind.Door, DoorOpen: false };

    public static List<WallSegment> Build(TileGrid tiles, IReadOnlyList<SightGap> gaps)
    {
        // Raw 1-unit boundary edges, bucketed by their fixed axis coordinate (Y for a horizontal
        // edge, X for a vertical one) so touching edges on the same line can be merged into one run
        // before they ever reach the gap-cutting/raycast stage - ShadowCast tests every segment every
        // frame, so leaving hundreds of unmerged unit-length segments would be a real cost, not just
        // untidy.
        var horizontal = new Dictionary<int, List<(int From, int To)>>();
        var vertical = new Dictionary<int, List<(int From, int To)>>();
        var segments = new List<WallSegment>();

        foreach (var (coord, cell) in tiles.Cells)
        {
            if (!IsOccluding(cell))
                continue;

            // Direct user request ("свободная половина полублочной стены не должна быть в тени") -
            // a half-thick wall cell's real solid geometry is only half its tile (the same HalfRect
            // ShipRenderer/the editor already draw it as), not the full 1x1 square every other wall
            // tile occupies. Tracing its boundary as an ordinary full tile below would place the
            // occluding edge a whole tile further out than the actual wall surface, swallowing this
            // very cell's own free half into shadow - see AddHalfThickEdges' own doc comment for the
            // actual fix.
            if (cell.WallOpenSide is { } solidSide)
            {
                AddHalfThickEdges(tiles, coord, solidSide, horizontal, vertical, segments);
                continue;
            }

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

        foreach (var (y, spans) in horizontal)
            foreach (var (from, to) in MergeRuns(spans))
                Occluders.AddHorizontal(segments, y, from, to, gaps);
        foreach (var (x, spans) in vertical)
            foreach (var (from, to) in MergeRuns(spans))
                Occluders.AddVertical(segments, x, from, to, gaps);

        // M-doors-as-edges (humble-soaring-cat.md) - a narrow door edge sits BETWEEN two ordinary
        // floor tiles, neither of which is itself an occluding cell (unlike the tile-based Door
        // above), so the loop over `tiles.Cells` never sees it at all. Closed+unbreached (Hp>0), it
        // blocks sight exactly like a shut door tile - open or breached, no segment (same "no wall
        // here at all" treatment the tile-based door gets from its own explicit SightGap upstream,
        // see IsOccluding's own doc comment). One segment spans the FULL edge line (not the half-tile
        // AddHalfThickEdges uses for a half-thick wall) since a door edge is never partial-width.
        foreach (var (key, edge) in tiles.DoorEdges)
        {
            if (edge.Hp <= 0 || edge.Open)
                continue; // breached or open - walkable and see-through, same as a breached/open door tile
            var (coord, side) = key;
            segments.Add(side switch
            {
                TileSide.East => new WallSegment(coord.X + 1, coord.Y, coord.X + 1, coord.Y + 1),
                _ => new WallSegment(coord.X, coord.Y + 1, coord.X + 1, coord.Y + 1),
            });
        }
        return segments;
    }

    // A half-thick cell (TileCell.WallOpenSide set - only ever on a Solid, non-corner straight wall
    // run tile) has three kinds of edge, unlike the four equal full-tile ones the loop above traces:
    //  1. The solidSide edge itself - the tile's real, full-length exterior boundary, no different
    //     from an ordinary full-thickness wall on that same side (same neighbor check, same integer
    //     line) - still routed through the shared `horizontal`/`vertical` dictionaries, so a long
    //     straight run of these still merges into as few raycast segments as an ordinary wall run
    //     would (the whole reason Build buckets by line before raycasting at all).
    //  2. The free-half edge - always occluding, full tile width, unconditional on any neighbor:
    //     this is genuinely where the solid half's own material ends and the free/walkable half of
    //     THIS SAME cell begins (TileCell.WallOpenSide's own doc comment), not a boundary with some
    //     other cell. Sits on the half-integer line through the tile's centre - this is the actual
    //     fix: before WallOpenSide was checked here at all, a half-thick cell fell straight into the
    //     loop above and got its far edge traced a WHOLE TILE further out (at the next cell's own
    //     boundary), which put this line - and with it, this cell's own free half - on the wrong,
    //     "occluded" side of the nearest wall segment. Pushed straight into `segments` (not the merge
    //     dictionaries) since it never lines up with any full-tile-integer edge to merge with anyway.
    //  3. The two edges perpendicular to solidSide - half-length, spanning only the solid half's own
    //     extent, same conditional-on-neighbor test an ordinary full-tile wall already uses on those
    //     sides. Only actually occluding where a straight run ends against open space (mid-run they
    //     border another occluding cell and are skipped, same as today); pushed straight into
    //     `segments` too - a lone half-unit cap has nothing else on its own line to merge with.
    private static void AddHalfThickEdges(TileGrid tiles, TileCoord coord, TileSide solidSide,
        Dictionary<int, List<(int From, int To)>> horizontal, Dictionary<int, List<(int From, int To)>> vertical,
        List<WallSegment> segments)
    {
        bool NeighborOccludes(TileSide side) => IsOccluding(tiles.CellAt(side.Offset(coord)));

        switch (solidSide)
        {
            case TileSide.North: // solid half occupies the top of the tile, free half the bottom
                if (!NeighborOccludes(TileSide.North)) AddUnitEdge(horizontal, coord.Y, coord.X);
                segments.Add(new WallSegment(coord.X, coord.Y + 0.5f, coord.X + 1, coord.Y + 0.5f));
                if (!NeighborOccludes(TileSide.West)) segments.Add(new WallSegment(coord.X, coord.Y, coord.X, coord.Y + 0.5f));
                if (!NeighborOccludes(TileSide.East)) segments.Add(new WallSegment(coord.X + 1, coord.Y, coord.X + 1, coord.Y + 0.5f));
                break;
            case TileSide.South: // solid half occupies the bottom, free half the top
                if (!NeighborOccludes(TileSide.South)) AddUnitEdge(horizontal, coord.Y + 1, coord.X);
                segments.Add(new WallSegment(coord.X, coord.Y + 0.5f, coord.X + 1, coord.Y + 0.5f));
                if (!NeighborOccludes(TileSide.West)) segments.Add(new WallSegment(coord.X, coord.Y + 0.5f, coord.X, coord.Y + 1));
                if (!NeighborOccludes(TileSide.East)) segments.Add(new WallSegment(coord.X + 1, coord.Y + 0.5f, coord.X + 1, coord.Y + 1));
                break;
            case TileSide.West: // solid half occupies the left, free half the right
                if (!NeighborOccludes(TileSide.West)) AddUnitEdge(vertical, coord.X, coord.Y);
                segments.Add(new WallSegment(coord.X + 0.5f, coord.Y, coord.X + 0.5f, coord.Y + 1));
                if (!NeighborOccludes(TileSide.North)) segments.Add(new WallSegment(coord.X, coord.Y, coord.X + 0.5f, coord.Y));
                if (!NeighborOccludes(TileSide.South)) segments.Add(new WallSegment(coord.X, coord.Y + 1, coord.X + 0.5f, coord.Y + 1));
                break;
            case TileSide.East: // solid half occupies the right, free half the left
                if (!NeighborOccludes(TileSide.East)) AddUnitEdge(vertical, coord.X + 1, coord.Y);
                segments.Add(new WallSegment(coord.X + 0.5f, coord.Y, coord.X + 0.5f, coord.Y + 1));
                if (!NeighborOccludes(TileSide.North)) segments.Add(new WallSegment(coord.X + 0.5f, coord.Y, coord.X + 1, coord.Y));
                if (!NeighborOccludes(TileSide.South)) segments.Add(new WallSegment(coord.X + 0.5f, coord.Y + 1, coord.X + 1, coord.Y + 1));
                break;
        }
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
