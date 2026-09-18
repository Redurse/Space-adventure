using System;
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

    // Direct user follow-up ("да давай" - dig into the remaining, unexplained Маска cost after the
    // wall-bleed fringe was confirmed cheap) - CPU time for TileOccluders.Build/RoomLighting.Build/
    // VisibilityMask.Build all measured in the low single-digit milliseconds even at a station-
    // plausible size (ShaderCheck's own "[diagnostic]" checks), nowhere near the report's 48-65ms -
    // so the next suspect was allocation pressure, not raw compute: the same checks found this
    // method alone allocating ~47KB/call (called TWICE every frame, ship then station, neither
    // result cached - only the underlying TileGrid rasterization is), and the screenshot's own F3
    // overlay already showed a suspiciously high "Выд 54МБ/с" (54MB/s allocated) and 4 Gen0
    // collections/sec. A Gen0 pause landing inside the Stopwatch window Game1.cs's own diagPhaseMs
    // measures around BuildVisibilityMask reads as "Маска is slow" regardless of how cheap the
    // actual math underneath is. [ThreadStatic] (not a plain static field) because
    // TestRunner.TileOccluders.cs's own tests call this from Parallel.For across multiple threads -
    // a shared mutable Dictionary there would be a real data race, not just wasted reuse; the real
    // game's own Draw always runs on one thread, so this still gets full reuse there.
    [ThreadStatic] private static Dictionary<int, List<(int From, int To)>>? _horizontalScratch;
    [ThreadStatic] private static Dictionary<int, List<(int From, int To)>>? _verticalScratch;

    private static void ClearScratch(Dictionary<int, List<(int From, int To)>> scratch)
    {
        // Clears each bucket's own List in place (keeping its backing array's capacity) instead of
        // removing dictionary entries - the set of distinct wall lines is near-identical frame to
        // frame for a static hull/station, so after the first call every AddUnitEdge below finds an
        // already-there, already-sized List waiting for it rather than allocating a fresh one.
        foreach (var list in scratch.Values)
            list.Clear();
    }

    public static List<WallSegment> Build(TileGrid tiles, IReadOnlyList<SightGap> gaps)
    {
        // Raw 1-unit boundary edges, bucketed by their fixed axis coordinate (Y for a horizontal
        // edge, X for a vertical one) so touching edges on the same line can be merged into one run
        // before they ever reach the gap-cutting/raycast stage - ShadowCast tests every segment every
        // frame, so leaving hundreds of unmerged unit-length segments would be a real cost, not just
        // untidy.
        var horizontal = _horizontalScratch ??= new Dictionary<int, List<(int From, int To)>>();
        var vertical = _verticalScratch ??= new Dictionary<int, List<(int From, int To)>>();
        ClearScratch(horizontal);
        ClearScratch(vertical);
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
                var neighbor = tiles.CellAt(side.Offset(coord));
                if (!IsOccluding(neighbor))
                {
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
                    continue;
                }

                // Direct user bug report (wall texture visible through what should be shadow, right
                // at a half-thick/full-wall seam) - an ordinary full-thickness neighbor genuinely
                // covers this whole face, correctly leaving no gap below. But a HALF-THICK neighbor
                // (WallOpenSide set) only actually has material over HALF of this shared face -
                // IsOccluding alone can't tell the difference, since it only asks "is this cell solid
                // at all", not "does its solid half actually reach this exact face". Whatever portion
                // of the face the neighbor's FREE half leaves exposed is a genuine solid(this tile)-
                // to-open(neighbor's free half) boundary and still needs its own segment - dropping it
                // (the pre-fix behaviour) let the shadow-cast rays for a viewer standing in that free
                // half sail straight past this tile's corner into whatever lay beyond it.
                if (neighbor is { WallOpenSide: not null })
                {
                    var (from, to) = SolidRangeOnFace(neighbor!, side.Opposite());
                    if (from > 0f) segments.Add(FaceSegment(side, coord, 0f, from));
                    if (to < 1f) segments.Add(FaceSegment(side, coord, to, 1f));
                }
                // else: an ordinary full wall - genuinely interior, no edge (unchanged from before).
            }
        }

        // ClearScratch (above) empties each bucket's own List in place but deliberately leaves the
        // dictionary ENTRY itself behind for reuse - a line that had a wall run in some earlier call
        // on this thread but has none in THIS one (the tile grid changed, or - on the test suite's
        // thread pool - this is simply a different, smaller scene) leaves a stale, empty-but-present
        // bucket. MergeRuns indexes spans[0] unconditionally, so skip anything with nothing in it
        // this time; a real bucket always has at least one span, added by AddUnitEdge above.
        foreach (var (y, spans) in horizontal)
        {
            if (spans.Count == 0) continue;
            foreach (var (from, to) in MergeRuns(spans))
                Occluders.AddHorizontal(segments, y, from, to, gaps);
        }
        foreach (var (x, spans) in vertical)
        {
            if (spans.Count == 0) continue;
            foreach (var (from, to) in MergeRuns(spans))
                Occluders.AddVertical(segments, x, from, to, gaps);
        }

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
        // A perpendicular cap's own sub-range (capFrom,capTo) is only genuinely interior when the
        // neighbor on that side has SOLID material over that entire sub-range too - a neighbor that
        // merely "occludes" (IsOccluding true) isn't enough on its own, since a half-thick neighbor
        // (WallOpenSide set) may only cover the OTHER half of this same face (its own free half
        // landing right where this cap needs to be). Same fix as Build's main loop, just phrased as
        // "does the neighbor cover my required range" instead of "what's left once I subtract theirs"
        // - a cap only ever needs one all-or-nothing answer, never a partial segment of its own.
        bool NeighborCoversCap(TileSide side, float capFrom, float capTo)
        {
            var neighbor = tiles.CellAt(side.Offset(coord));
            if (!IsOccluding(neighbor)) return false;
            var (from, to) = SolidRangeOnFace(neighbor!, side.Opposite());
            return from <= capFrom && to >= capTo;
        }

        switch (solidSide)
        {
            case TileSide.North: // solid half occupies the top of the tile, free half the bottom
                if (!NeighborOccludes(TileSide.North)) AddUnitEdge(horizontal, coord.Y, coord.X);
                segments.Add(new WallSegment(coord.X, coord.Y + 0.5f, coord.X + 1, coord.Y + 0.5f));
                if (!NeighborCoversCap(TileSide.West, 0f, 0.5f)) segments.Add(new WallSegment(coord.X, coord.Y, coord.X, coord.Y + 0.5f));
                if (!NeighborCoversCap(TileSide.East, 0f, 0.5f)) segments.Add(new WallSegment(coord.X + 1, coord.Y, coord.X + 1, coord.Y + 0.5f));
                break;
            case TileSide.South: // solid half occupies the bottom, free half the top
                if (!NeighborOccludes(TileSide.South)) AddUnitEdge(horizontal, coord.Y + 1, coord.X);
                segments.Add(new WallSegment(coord.X, coord.Y + 0.5f, coord.X + 1, coord.Y + 0.5f));
                if (!NeighborCoversCap(TileSide.West, 0.5f, 1f)) segments.Add(new WallSegment(coord.X, coord.Y + 0.5f, coord.X, coord.Y + 1));
                if (!NeighborCoversCap(TileSide.East, 0.5f, 1f)) segments.Add(new WallSegment(coord.X + 1, coord.Y + 0.5f, coord.X + 1, coord.Y + 1));
                break;
            case TileSide.West: // solid half occupies the left, free half the right
                if (!NeighborOccludes(TileSide.West)) AddUnitEdge(vertical, coord.X, coord.Y);
                segments.Add(new WallSegment(coord.X + 0.5f, coord.Y, coord.X + 0.5f, coord.Y + 1));
                if (!NeighborCoversCap(TileSide.North, 0f, 0.5f)) segments.Add(new WallSegment(coord.X, coord.Y, coord.X + 0.5f, coord.Y));
                if (!NeighborCoversCap(TileSide.South, 0f, 0.5f)) segments.Add(new WallSegment(coord.X, coord.Y + 1, coord.X + 0.5f, coord.Y + 1));
                break;
            case TileSide.East: // solid half occupies the right, free half the left
                if (!NeighborOccludes(TileSide.East)) AddUnitEdge(vertical, coord.X + 1, coord.Y);
                segments.Add(new WallSegment(coord.X + 0.5f, coord.Y, coord.X + 0.5f, coord.Y + 1));
                if (!NeighborCoversCap(TileSide.North, 0.5f, 1f)) segments.Add(new WallSegment(coord.X + 0.5f, coord.Y, coord.X + 1, coord.Y));
                if (!NeighborCoversCap(TileSide.South, 0.5f, 1f)) segments.Add(new WallSegment(coord.X + 0.5f, coord.Y + 1, coord.X + 1, coord.Y + 1));
                break;
        }
    }

    // Where a cell's own solid material actually touches one of its four faces, as a (From,To) pair
    // of fractions along that face - 0 at the face's "near" endpoint (Y=the cell's own Y for an
    // East/West face, X=the cell's own X for a North/South face), 1 at the far endpoint. An ordinary
    // full-thickness occluding cell is solid along the entirety of every face: (0,1). A half-thick
    // cell (WallOpenSide - despite the name, the SOLID half's own side, see its own doc comment on
    // TileCell) is solid along the whole of its own solidSide face, not at all along the opposite
    // (free-half) face, and along exactly the near or far HALF of each of the two perpendicular
    // faces - whichever half sits nearer the solid side.
    private static (float From, float To) SolidRangeOnFace(TileCell cell, TileSide side)
    {
        if (cell.WallOpenSide is not { } solid)
            return (0f, 1f);
        if (side == solid) return (0f, 1f);
        if (side == solid.Opposite()) return (0f, 0f);
        return solid is TileSide.North or TileSide.West ? (0f, 0.5f) : (0.5f, 1f);
    }

    // Turns a (From,To) fraction pair along one of `coord`'s own faces (see SolidRangeOnFace) back
    // into world-space endpoints, in the same near-to-far direction those fractions are measured in.
    private static WallSegment FaceSegment(TileSide side, TileCoord coord, float from, float to) => side switch
    {
        TileSide.East => new WallSegment(coord.X + 1, coord.Y + from, coord.X + 1, coord.Y + to),
        TileSide.West => new WallSegment(coord.X, coord.Y + from, coord.X, coord.Y + to),
        TileSide.North => new WallSegment(coord.X + from, coord.Y, coord.X + to, coord.Y),
        _ => new WallSegment(coord.X + from, coord.Y + 1, coord.X + to, coord.Y + 1), // South
    };

    private static void AddUnitEdge(Dictionary<int, List<(int From, int To)>> into, int fixedCoord, int from)
    {
        if (!into.TryGetValue(fixedCoord, out var spans))
            into[fixedCoord] = spans = new List<(int, int)>();
        spans.Add((from, from + 1));
    }

    // Called once per distinct wall line - dozens of times per Build - and its result is only ever
    // walked immediately by the caller's own foreach, never stored past that, so (like the scratch
    // fields just above) it can safely write into one reused-per-thread buffer instead of a fresh
    // List every call. [ThreadStatic] for the same reason as those - TestRunner's own Parallel.For.
    [ThreadStatic] private static List<(int From, int To)>? _mergedRunsScratch;

    // Coalesces touching/overlapping unit spans on the same line into the fewest possible runs -
    // e.g. tile edges [1,2) and [2,3) merge into [1,3). Spans never actually overlap by more than a
    // shared endpoint (each comes from exactly one tile's own unit-wide edge), but sorting first
    // means a single left-to-right sweep is enough regardless of insertion order.
    private static List<(int From, int To)> MergeRuns(List<(int From, int To)> spans)
    {
        spans.Sort((a, b) => a.From.CompareTo(b.From));
        var merged = _mergedRunsScratch ??= new List<(int From, int To)>();
        merged.Clear();
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
