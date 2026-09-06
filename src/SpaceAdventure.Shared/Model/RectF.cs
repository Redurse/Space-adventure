namespace SpaceAdventure.Shared.Model;

// One axis-aligned piece of a room's footprint, world units (meters) - see Room.cs's own doc
// comment (humble-soaring-cat.md M86) for why a room is now a UNION of these instead of exactly
// one. Deliberately as bare-bones as the old single-rect Room/CustomRoomDef fields it replaces -
// no Id of its own (RoomRect below pairs one of these with the owning room's Id where needed).
public readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;
    public float Area => Width * Height;
    public Vec2 Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(Vec2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;
}

// One of a room's constituent rectangles, tagged with the owning room's Id - the flattened form
// every multi-rect-aware geometry function (ShipLayoutGeometry, CustomShipValidator, HullSkin,
// HullSilhouette) operates against instead of a raw Room/CustomRoomDef list. Two RoomRects sharing
// the same RoomId are two pieces of the SAME room (their shared edge is an internal seam - never a
// door/wall candidate); different RoomIds are genuinely different rooms (today's existing rules -
// must not overlap, may share a door - apply unchanged between them).
public readonly record struct RoomRect(string RoomId, RectF Rect);
