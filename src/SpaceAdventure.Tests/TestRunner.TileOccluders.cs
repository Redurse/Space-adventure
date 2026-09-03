using System.Collections.Generic;
using System.Linq;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;

// M78 (humble-soaring-cat.md) - TileOccluders is a pure Client.Rendering utility, tested directly
// against small hand-built TileGrids the same way TestRunner.TileRegionConnectivity.cs (M77) tests
// TileRegionConnectivity, and TestRunner.TileGrid.cs tests TileGrid itself.
internal static partial class TestRunner
{
    private const float TileOccludersEpsilon = 0.01f;

    private static bool SegmentsMatch(WallSegment a, WallSegment b) =>
        System.MathF.Abs(a.Ax - b.Ax) < TileOccludersEpsilon && System.MathF.Abs(a.Ay - b.Ay) < TileOccludersEpsilon &&
        System.MathF.Abs(a.Bx - b.Bx) < TileOccludersEpsilon && System.MathF.Abs(a.By - b.By) < TileOccludersEpsilon;

    private static bool Contains(IReadOnlyList<WallSegment> segments, WallSegment expected) =>
        segments.Any(s => SegmentsMatch(s, expected));

    // True if some segment in the list covers the given point on a horizontal (y fixed) line -
    // used to prove a specific stretch of wall is now ABSENT (nothing covers that point) rather than
    // needing to predict the exact leftover span boundaries.
    private static bool AnyHorizontalCovers(IReadOnlyList<WallSegment> segments, float y, float x) =>
        segments.Any(s => System.MathF.Abs(s.Ay - y) < TileOccludersEpsilon && System.MathF.Abs(s.By - y) < TileOccludersEpsilon &&
            s.Ax - TileOccludersEpsilon <= x && x <= s.Bx + TileOccludersEpsilon);

    // A plain isolated 4x4 room, walled on all 4 sides (TileGridRasterizer.FromRooms's own "leading
    // edges always walled, trailing edges walled when uncovered" rule, applied to a single room with
    // no neighbors - every edge is a trailing edge that's genuinely exterior, so all four get walled)
    // - the exact hand-construction a real ship room this size would rasterize to.
    private static TileGrid BuildFourByFourWalledRoom()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 4; x++)
            for (var y = 0; y < 4; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        for (var x = 0; x < 4; x++)
        {
            grid.SetWall(new TileCoord(x, 0), TileWallKind.Solid);
            grid.SetWall(new TileCoord(x, 3), TileWallKind.Solid);
        }
        for (var y = 0; y < 4; y++)
        {
            grid.SetWall(new TileCoord(0, y), TileWallKind.Solid);
            grid.SetWall(new TileCoord(3, y), TileWallKind.Solid);
        }
        return grid;
    }

    // The load-bearing equivalence proof: for the common case (an isolated rectangular room), the
    // tile-native occluder must reproduce the exact same 4 wall segments the old Room-rectangle
    // Occluders.Build produces for an equivalent Room(0,0,4,4).
    //
    // One genuine, unavoidable wrinkle, confirmed by hand-tracing rather than assumed: a Room
    // rectangle's walls are zero-thickness (they sit exactly at the rectangle's edge, with the WHOLE
    // rectangle walkable floor on the inside), but a TileGrid's walls are real 1x1 tiles that occupy
    // floor space - so a walled 4x4 room's OWN interior is a smaller 2x2 open pocket, and the wall
    // ring has two occlusion-relevant faces: the outer face (which lines up with the old Room
    // rectangle's edge - the 4 segments this test asserts) and an inner face (facing that 2x2 pocket,
    // one tile further in - 4 more segments no zero-thickness rectangle could ever have had). This
    // isn't a bug: a tile immediately inside that ring really would see solid wall to its own
    // outward side. So this test asserts the 4 expected segments are PRESENT (the actual equivalence
    // claim - see the L-shape test below for a case with no such inner pocket, where the count is
    // exactly provable), not that the produced list's count is exactly 4.
    private static bool TileOccluders_IsolatedRoom_MatchesRoomRectangleOccluders()
    {
        var tiles = BuildFourByFourWalledRoom();
        var tileSegments = TileOccluders.Build(tiles, new List<SightGap>());

        var room = new Room("r", "Room", 0, 0, 4, 4);
        var roomSegments = Occluders.Build(new[] { room }, new List<SightGap>());
        if (roomSegments.Count != 4)
            return false; // setup problem - Occluders.Build's own shape changed

        return roomSegments.All(expected => Contains(tileSegments, expected));
    }

    // Same room, but one top-wall tile is now an OPEN door - proving the "an open door tile is
    // simply non-occluding, no wall segment at all" reasoning (TileOccluders.IsOccluding's own doc
    // comment): the stretch of wall it used to contribute to must now be gone, not just narrower.
    private static bool TileOccluders_OpenDoorTile_RemovesItsSpanFromTheWall()
    {
        var tiles = BuildFourByFourWalledRoom();
        var doorCoord = new TileCoord(1, 0);
        tiles.SetWall(doorCoord, TileWallKind.Door);
        tiles.SetDoorOpen(doorCoord, true);

        var segments = TileOccluders.Build(tiles, new List<SightGap>());

        // The door tile spans x in [1,2) at y=0 - nothing should cover its midpoint any more.
        if (AnyHorizontalCovers(segments, 0f, 1.5f))
            return false;

        // The two flanking stretches should still stand: (0,0)-(1,0) from the corner tile, and
        // (2,0)-(4,0) from the two still-solid tiles past the door.
        return Contains(segments, new WallSegment(0, 0, 1, 0)) && Contains(segments, new WallSegment(2, 0, 4, 0));
    }

    // A genuinely non-rectangular occluding shape (an L: a 4-wide/2-tall arm plus a 2-wide/4-tall
    // arm, sharing a 2x2 corner) - fully solid, no interior open pocket, so the boundary trace is
    // exact and provable to the segment, unlike the isolated-room case above. This is the concrete
    // proof M78 does something a Room-rectangle system structurally could never do: Occluders.Build
    // has no way to describe this footprint at all, only its 4x4 bounding rectangle.
    private static TileGrid BuildLShapedBlock()
    {
        var grid = new TileGrid();
        var coords = new List<TileCoord>();
        for (var x = 0; x < 4; x++)
            for (var y = 0; y < 2; y++)
                coords.Add(new TileCoord(x, y)); // horizontal arm
        for (var x = 0; x < 2; x++)
            for (var y = 0; y < 4; y++)
                coords.Add(new TileCoord(x, y)); // vertical arm

        foreach (var coord in coords.Distinct())
            grid.SetFloor(coord, true);
        foreach (var coord in coords.Distinct())
            grid.SetWall(coord, TileWallKind.Solid);
        return grid;
    }

    private static bool TileOccluders_LShapedRegion_TracesTheActualBoundaryNotABoundingRectangle()
    {
        var tiles = BuildLShapedBlock();
        var segments = TileOccluders.Build(tiles, new List<SightGap>());

        // Hand-traced boundary of the L (see BuildLShapedBlock): the top of the horizontal arm, the
        // notch's two inward-facing edges, the bottom of the vertical arm, the shared left edge (full
        // height), and the horizontal arm's own right edge (only as tall as the arm itself, NOT the
        // full 4-unit bounding-rectangle height - the key proof this isn't just a rectangle).
        var expected = new[]
        {
            new WallSegment(0, 0, 4, 0), // top of the horizontal arm
            new WallSegment(2, 2, 4, 2), // notch: horizontal inward face
            new WallSegment(0, 4, 2, 4), // bottom of the vertical arm
            new WallSegment(0, 0, 0, 4), // shared left edge, full height
            new WallSegment(4, 0, 4, 2), // right edge of the horizontal arm - stops at y=2
            new WallSegment(2, 2, 2, 4), // notch: vertical inward face
        };

        if (segments.Count != expected.Length)
            return false;
        if (!expected.All(e => Contains(segments, e)))
            return false;

        // The bounding rectangle's own right edge would run the full y=[0,4] - proving that's NOT
        // what got produced (only the arm's own y=[0,2] stretch did).
        return !Contains(segments, new WallSegment(4, 0, 4, 4));
    }

    // A long, straight wall band (6 tiles) merges into ONE run before gap-cutting - confirming the
    // shared Occluders.AddHorizontal/Cut logic (refactored to internal for exactly this reuse) still
    // punches a hole correctly when fed a merged multi-tile span, not just the single-tile-wide spans
    // Occluders.Build's own room sides happen to produce today.
    private static bool TileOccluders_GapCutsThroughAMergedMultiTileRun()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 6; x++)
        {
            grid.SetFloor(new TileCoord(x, 0), true);
            grid.SetFloor(new TileCoord(x, 1), true);
            grid.SetWall(new TileCoord(x, 0), TileWallKind.Solid); // the wall row
            // row y=1 stays open floor (TileWallKind.None) - the corridor the wall's North face
            // borders, giving one clean 6-unit merged run at y=0 to cut the gap out of.
        }

        var noGapSegments = TileOccluders.Build(grid, new List<SightGap>());
        if (!Contains(noGapSegments, new WallSegment(0, 0, 6, 0)))
            return false; // setup problem - the 6 tiles didn't merge into one run

        var gaps = new List<SightGap> { new(2.5f, -0.5f, 3.5f, 0.5f) };
        var cutSegments = TileOccluders.Build(grid, gaps);

        if (Contains(cutSegments, new WallSegment(0, 0, 6, 0)))
            return false; // the gap didn't actually cut anything
        if (AnyHorizontalCovers(cutSegments, 0f, 3f))
            return false; // the gap's own middle must be uncovered

        return Contains(cutSegments, new WallSegment(0, 0, 2.5f, 0)) && Contains(cutSegments, new WallSegment(3.5f, 0, 6, 0));
    }
}
