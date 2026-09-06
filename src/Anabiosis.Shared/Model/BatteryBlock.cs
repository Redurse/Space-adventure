namespace Anabiosis.Shared.Model;

// The battery's physical position (game_design.md section 1 — emergency power storage next to
// the reactor/distribution block). Clicking it "enters" it, showing charge/capacity.
public sealed record BatteryBlock(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
