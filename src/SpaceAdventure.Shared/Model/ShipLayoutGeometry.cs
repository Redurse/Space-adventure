namespace SpaceAdventure.Shared.Model;

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

    // Every touching room pair and where their shared boundary actually overlaps. "Vertical" means
    // the shared wall is a vertical line (rooms side by side along X, so far/near is a Y range);
    // At is that line's X (or Y when !Vertical).
    public static IReadOnlyList<RoomPairOverlap> FindRoomPairOverlaps(IReadOnlyList<CustomRoomDef> rooms)
    {
        var results = new List<RoomPairOverlap>();
        for (var i = 0; i < rooms.Count; i++)
        {
            for (var j = i + 1; j < rooms.Count; j++)
            {
                var a = rooms[i];
                var b = rooms[j];

                if (a.X + a.Width == b.X || b.X + b.Width == a.X)
                {
                    var wallX = a.X + a.Width == b.X ? a.X + a.Width : b.X + b.Width;
                    var top = Math.Max(a.Y, b.Y);
                    var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
                    if (bottom > top)
                        results.Add(new RoomPairOverlap(a.Id, b.Id, true, wallX, (top + bottom) / 2f, bottom - top));
                }

                if (a.Y + a.Height == b.Y || b.Y + b.Height == a.Y)
                {
                    var wallY = a.Y + a.Height == b.Y ? a.Y + a.Height : b.Y + b.Height;
                    var left = Math.Max(a.X, b.X);
                    var right = Math.Min(a.X + a.Width, b.X + b.Width);
                    if (right > left)
                        results.Add(new RoomPairOverlap(a.Id, b.Id, false, wallY, (left + right) / 2f, right - left));
                }
            }
        }
        return results;
    }

    // Whether ANY portion of `room`'s given side touches another room at all - a side with any
    // touch at all is not available as a whole-side airlock (see CustomAirlockDef's doc comment).
    public static bool SideHasNeighbor(CustomRoomDef room, EdgeSide side, IReadOnlyList<RoomPairOverlap> overlaps)
    {
        foreach (var overlap in overlaps)
        {
            if (overlap.RoomAId != room.Id && overlap.RoomBId != room.Id)
                continue;

            var atThisSide = side switch
            {
                EdgeSide.Right => overlap.Vertical && overlap.At == room.X + room.Width,
                EdgeSide.Left => overlap.Vertical && overlap.At == room.X,
                EdgeSide.Bottom => !overlap.Vertical && overlap.At == room.Y + room.Height,
                EdgeSide.Top => !overlap.Vertical && overlap.At == room.Y,
                _ => false,
            };
            if (atThisSide)
                return true;
        }
        return false;
    }

    // Center point (world units) of a whole room side - where an airlock's outer door sits.
    public static (float X, float Y) SideMidpoint(CustomRoomDef room, EdgeSide side) => side switch
    {
        EdgeSide.Top => (room.X + room.Width / 2f, room.Y),
        EdgeSide.Bottom => (room.X + room.Width / 2f, room.Y + room.Height),
        EdgeSide.Left => (room.X, room.Y + room.Height / 2f),
        EdgeSide.Right => (room.X + room.Width, room.Y + room.Height / 2f),
        _ => (room.X, room.Y),
    };

    public static float SideLength(CustomRoomDef room, EdgeSide side) =>
        side is EdgeSide.Top or EdgeSide.Bottom ? room.Width : room.Height;
}
