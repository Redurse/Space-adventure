namespace Anabiosis.Shared.Model;

// Shared grid geometry for the Ship Editor: room-pair adjacency (-> Door candidates/placement) and
// whole-side exterior detection (-> Airlock candidates/placement, and which unit segments of a
// room's boundary become WallBlocks). Used by both the client editor (to show valid click targets)
// and Ship.Custom.cs (to actually build the hull), so the two never disagree about adjacency.
// Everything here relies on room coordinates sitting on a grid (CustomRoomDef.X/Y/Width/Height,
// M60 follow-up - widened from int to float so half-unit hand-authored hulls round-trip too), so
// the touching-boundary checks below are exact equality comparisons, never float epsilon guesses -
// safe because grid-snapped placement (whole OR half units) is always exactly representable in
// IEEE-754 float, the same way whole units always were.
public static class ShipLayoutGeometry
{
    public readonly record struct RoomPairOverlap(
        string RoomAId, string RoomBId, bool Vertical, float At, float OverlapCenter, float OverlapLength);

    // Every touching room-PAIR and where their shared boundary actually overlaps, generalized
    // (humble-soaring-cat.md M89) to run against every room's flattened list of subrects instead of
    // assuming one rectangle per room - a same-RoomId subrect pair (two pieces of the SAME
    // multi-rect room) is skipped outright, never a door/wall candidate (they're already flush by
    // construction, see TileShipBuilder.cs's own M88 doc comment). For every existing single-rect
    // room (every hand-authored hull, procedural station, editor-drawn rectangular room) this is
    // byte-identical to the old per-room math, since Flatten produces exactly one RoomRect per room.
    // "Vertical" means the shared wall is a vertical line (rooms side by side along X, so far/near
    // is a Y range); At is that line's X (or Y when !Vertical).
    public static IReadOnlyList<RoomPairOverlap> FindRoomPairOverlaps(IReadOnlyList<CustomRoomDef> rooms)
    {
        var flat = RoomGeometry.Flatten(rooms);
        var results = new List<RoomPairOverlap>();
        for (var i = 0; i < flat.Count; i++)
        {
            for (var j = i + 1; j < flat.Count; j++)
            {
                var a = flat[i];
                var b = flat[j];
                if (a.RoomId == b.RoomId)
                    continue; // two pieces of the same room - internal seam, never a candidate

                if (a.Rect.Right == b.Rect.X || b.Rect.Right == a.Rect.X)
                {
                    var wallX = a.Rect.Right == b.Rect.X ? a.Rect.Right : b.Rect.Right;
                    var top = Math.Max(a.Rect.Y, b.Rect.Y);
                    var bottom = Math.Min(a.Rect.Bottom, b.Rect.Bottom);
                    if (bottom > top)
                        results.Add(new RoomPairOverlap(a.RoomId, b.RoomId, true, wallX, (top + bottom) / 2f, bottom - top));
                }

                if (a.Rect.Bottom == b.Rect.Y || b.Rect.Bottom == a.Rect.Y)
                {
                    var wallY = a.Rect.Bottom == b.Rect.Y ? a.Rect.Bottom : b.Rect.Bottom;
                    var left = Math.Max(a.Rect.X, b.Rect.X);
                    var right = Math.Min(a.Rect.Right, b.Rect.Right);
                    if (right > left)
                        results.Add(new RoomPairOverlap(a.RoomId, b.RoomId, false, wallY, (left + right) / 2f, right - left));
                }
            }
        }
        return results;
    }

    // Whether ANY portion of `room`'s given side touches another room at all - a side with any
    // touch at all is not available as a whole-side airlock (see CustomAirlockDef's doc comment).
    // Checks against every one of the room's OWN subrects (not just its bbox) - identical to the old
    // single-rect check whenever room.Rects.Count == 1.
    public static bool SideHasNeighbor(CustomRoomDef room, EdgeSide side, IReadOnlyList<RoomPairOverlap> overlaps)
    {
        foreach (var overlap in overlaps)
        {
            if (overlap.RoomAId != room.Id && overlap.RoomBId != room.Id)
                continue;

            var atThisSide = side switch
            {
                EdgeSide.Right => overlap.Vertical && room.Rects.Any(r => r.Right == overlap.At),
                EdgeSide.Left => overlap.Vertical && room.Rects.Any(r => r.X == overlap.At),
                EdgeSide.Bottom => !overlap.Vertical && room.Rects.Any(r => r.Bottom == overlap.At),
                EdgeSide.Top => !overlap.Vertical && room.Rects.Any(r => r.Y == overlap.At),
                _ => false,
            };
            if (atThisSide)
                return true;
        }
        return false;
    }

    // Which of a room's own subrects genuinely reach the room's own bounding-box edge on `side` -
    // for a single-rect room (every existing hand-authored hull/station/editor-drawn rectangular
    // room) this is trivially that one rect; for a multi-rect room, more than one subrect can share
    // the same cardinal-facing bbox edge (e.g. an L-shape's two arms both touch its own top edge).
    // An airlock's side is only geometrically unambiguous when exactly one subrect qualifies -
    // CustomShipValidator rejects any authored airlock where that isn't true, so SideMidpoint/
    // SideLength below can safely assume it.
    public static IReadOnlyList<RectF> SubrectsFacingSide(CustomRoomDef room, EdgeSide side) => side switch
    {
        EdgeSide.Top => room.Rects.Where(r => r.Y == room.Y).ToList(),
        EdgeSide.Bottom => room.Rects.Where(r => r.Bottom == room.Y + room.Height).ToList(),
        EdgeSide.Left => room.Rects.Where(r => r.X == room.X).ToList(),
        EdgeSide.Right => room.Rects.Where(r => r.Right == room.X + room.Width).ToList(),
        _ => Array.Empty<RectF>(),
    };

    private static RectF SingleSubrectFacing(CustomRoomDef room, EdgeSide side)
    {
        var candidates = SubrectsFacingSide(room, side);
        if (candidates.Count != 1)
            throw new InvalidOperationException(
                $"Room '{room.Id}' has {candidates.Count} subrects facing {side} - an airlock needs exactly one (CustomShipValidator should have rejected this before this was ever called).");
        return candidates[0];
    }

    // Center point (world units) of a whole room side - where an airlock's outer door sits.
    public static (float X, float Y) SideMidpoint(CustomRoomDef room, EdgeSide side)
    {
        var rect = SingleSubrectFacing(room, side);
        return side switch
        {
            EdgeSide.Top => (rect.X + rect.Width / 2f, rect.Y),
            EdgeSide.Bottom => (rect.X + rect.Width / 2f, rect.Bottom),
            EdgeSide.Left => (rect.X, rect.Y + rect.Height / 2f),
            EdgeSide.Right => (rect.Right, rect.Y + rect.Height / 2f),
            _ => (rect.X, rect.Y),
        };
    }

    public static float SideLength(CustomRoomDef room, EdgeSide side)
    {
        var rect = SingleSubrectFacing(room, side);
        return side is EdgeSide.Top or EdgeSide.Bottom ? rect.Width : rect.Height;
    }
}
