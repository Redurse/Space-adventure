using Anabiosis.Server;
using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // Every other test in this file exercised one of the fixed hand-authored hull classes
    // (Scout/Cruiser/Corvette) directly - deleted along with those classes (direct user request,
    // "удали все текущие корабли в разделе начать новую игру... полностью удалить из кода"). This
    // one is unrelated (Station.CreateDefault, not a Ship at all) - kept as-is.
    private static bool RoomLayout_MoveAlongAxis_BlocksAtWallWithoutDoor()
    {
        var station = Station.CreateDefault();
        var dockRoomId = station.DockRoomId;
        // Reads the dock room's real bounds rather than a hardcoded corner (M49 - the dock room no
        // longer sits at a fixed (0,0) origin, its size/position come from the procedural
        // generator) - the top wall is guaranteed door-free regardless: the dock room's only two
        // ring neighbours are to its right and below it (Station.Procedural.cs's own doc comment on
        // why Dock always lands on the perimeter's top-left corner).
        var room = station.GetRoom(dockRoomId);
        // room.Top+1f used to be clear of the wall, back when a wall was a zero-width line sitting
        // exactly on the room's own Top edge; now the room's own Top row is itself a real, solid
        // wall tile (TileGridRasterizer.FromRooms - every room's own leading Left/Top edge is
        // walled unconditionally), so the start position has to sit a full tile further in to avoid
        // starting inside that wall tile.
        var start = new Vec2(room.Center.X, room.Top + 2f);
        // Direct user request (humble-soaring-cat.md, "удали механику что если ставим стены в
        // ряд, они почти все превращаются в полублоки") - the Top row is full-thickness again
        // (TileGridRasterizer.FromRooms no longer auto-infers a half-open side for a straight run).
        var (pos, roomId) = station.MoveAlongAxis(start, dockRoomId, new Vec2(0, -1f), _ => true);
        // Clamped CharacterRadius short of the wall's own solid face.
        return roomId == dockRoomId && Math.Abs(pos.Y - (room.Top + 1f + RoomLayout.CharacterRadius)) < 0.01f;
    }
}
