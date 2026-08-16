namespace SpaceAdventure.Shared.Model;

// Shared room/door collision algorithm - used by both Ship and Station, which each have their own
// independent Rooms/Doors list but need the exact same "stay inside the room, cross only through
// an open aligned door" movement rule (see Ship.MoveAlongAxis's own doc comment for the rule
// itself). Extracted here once both types needed it, rather than duplicating it.
public static class RoomLayout
{
    public static Room GetRoom(IReadOnlyList<Room> rooms, string roomId) => rooms.First(r => r.Id == roomId);

    public static (Vec2 Position, string RoomId) MoveAlongAxis(
        IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors, Vec2 position, string roomId, Vec2 delta, Func<string, bool> isDoorOpen)
    {
        var room = GetRoom(rooms, roomId);
        var next = position + delta;

        if (room.Contains(next))
            return (next, roomId);

        var door = doors.FirstOrDefault(d => d.Connects(roomId) && d.Contains(next) && isDoorOpen(d.Id));
        if (door is not null)
            return (next, door.OtherRoom(roomId));

        return (new Vec2(
            Math.Clamp(next.X, room.Left, room.Right),
            Math.Clamp(next.Y, room.Top, room.Bottom)), roomId);
    }
}
