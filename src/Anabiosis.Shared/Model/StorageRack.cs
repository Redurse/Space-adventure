namespace Anabiosis.Shared.Model;

// Ship's cargo shelving (game_design.md section 13 - the personal inventory is a hard carry limit,
// so anything the crew wants to keep has to live somewhere physical). Walk up to it and it opens a
// grid of slots you can drag items to and from; unlike a ToolStation it holds whatever you put in
// it rather than dispensing one fixed item forever.
public sealed record StorageRack(string Id, string RoomId, float X, float Y)
{
    public const int Capacity = 30;

    public Vec2 Position => new(X, Y);
}
