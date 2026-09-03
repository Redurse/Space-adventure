using System.Collections.Generic;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// One solid, sight-blocking run of wall in world units. Always axis-aligned (rooms are AABBs), so
// the two points only ever differ on one axis.
public readonly record struct WallSegment(float Ax, float Ay, float Bx, float By);

// A rectangle punched out of the walls that sight passes through: an open door, an open airlock,
// the station connector. Closed doors deliberately produce no gap - a shut bulkhead blocks the
// view exactly like hull (same rule movement already follows in RoomLayout.MoveAlongAxis).
public readonly record struct SightGap(float Left, float Top, float Right, float Bottom);

// Turns a room/door layout into the flat list of wall segments VisibilityMask raycasts against.
// Rooms share walls, so adjacent rooms produce two coincident segments on the boundary between
// them - harmless for raycasting (the nearest hit wins either way) and much simpler than trying
// to dedupe shared edges.
public static class Occluders
{
    private const float Epsilon = 0.01f;

    public static List<WallSegment> Build(IReadOnlyList<Room> rooms, IReadOnlyList<SightGap> gaps)
    {
        var segments = new List<WallSegment>();
        foreach (var room in rooms)
        {
            AddHorizontal(segments, room.Top, room.Left, room.Right, gaps);
            AddHorizontal(segments, room.Bottom, room.Left, room.Right, gaps);
            AddVertical(segments, room.Left, room.Top, room.Bottom, gaps);
            AddVertical(segments, room.Right, room.Top, room.Bottom, gaps);
        }
        return segments;
    }

    public static SightGap ToGap(Door door) => new(door.Left, door.Top, door.Right, door.Bottom);

    public static SightGap ToGap(AirlockOuterDoor door) => new(door.Left, door.Top, door.Right, door.Bottom);

    // Shared with TileOccluders.cs (M78, humble-soaring-cat.md) - both the old room-rectangle wall
    // builder above and the new tile-boundary one feed their raw spans through these same two
    // methods, so a room-based run and a tile-based run get cut against a SightGap by the exact same
    // code path and can never drift apart on the rule. Internal rather than private for exactly that
    // reuse; still not meant to be called from outside Rendering.
    internal static void AddHorizontal(List<WallSegment> into, float y, float from, float to, IReadOnlyList<SightGap> gaps)
    {
        var spans = new List<(float From, float To)> { (from, to) };
        foreach (var gap in gaps)
        {
            if (y < gap.Top - Epsilon || y > gap.Bottom + Epsilon)
                continue;
            Cut(spans, gap.Left, gap.Right);
        }

        foreach (var (a, b) in spans)
            if (b - a > Epsilon)
                into.Add(new WallSegment(a, y, b, y));
    }

    internal static void AddVertical(List<WallSegment> into, float x, float from, float to, IReadOnlyList<SightGap> gaps)
    {
        var spans = new List<(float From, float To)> { (from, to) };
        foreach (var gap in gaps)
        {
            if (x < gap.Left - Epsilon || x > gap.Right + Epsilon)
                continue;
            Cut(spans, gap.Top, gap.Bottom);
        }

        foreach (var (a, b) in spans)
            if (b - a > Epsilon)
                into.Add(new WallSegment(x, a, x, b));
    }

    // Removes [cutFrom, cutTo] from every span in place; a cut through the middle of a span leaves
    // the two stubs on either side of the doorway.
    private static void Cut(List<(float From, float To)> spans, float cutFrom, float cutTo)
    {
        for (var i = spans.Count - 1; i >= 0; i--)
        {
            var (from, to) = spans[i];
            if (cutTo <= from + Epsilon || cutFrom >= to - Epsilon)
                continue;

            spans.RemoveAt(i);
            if (cutFrom - from > Epsilon)
                spans.Add((from, cutFrom));
            if (to - cutTo > Epsilon)
                spans.Add((cutTo, to));
        }
    }
}
