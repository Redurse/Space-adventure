namespace Anabiosis.Shared.Model;

// A locker to put on/take off a spacesuit (game_design.md section 2 — "скафандры висят в
// шлюзовых шкафчиках в определённых местах корабля").
public sealed record SuitLocker(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
