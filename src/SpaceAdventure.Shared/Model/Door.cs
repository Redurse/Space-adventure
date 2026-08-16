namespace SpaceAdventure.Shared.Model;

// A gap in the wall shared by two rooms: a small rectangle straddling the wall (X/Y is its
// center, Width/Height its extent, same convention as Room). A point inside it counts as a
// legal crossing between RoomAId and RoomBId; everywhere else on a shared wall is solid.
public sealed record Door(string Id, string RoomAId, string RoomBId, float X, float Y, float Width, float Height)
{
    public float Left => X - Width / 2;
    public float Right => X + Width / 2;
    public float Top => Y - Height / 2;
    public float Bottom => Y + Height / 2;
    public Vec2 Position => new(X, Y);

    public bool Contains(Vec2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    public string OtherRoom(string roomId) => roomId == RoomAId ? RoomBId : RoomAId;
    public bool Connects(string roomId) => roomId == RoomAId || roomId == RoomBId;
}
