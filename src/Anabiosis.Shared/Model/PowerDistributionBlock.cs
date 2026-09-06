namespace Anabiosis.Shared.Model;

// The distribution block's physical position (game_design.md section 1 — "Distribution-блок
// рядом с реактором"). Clicking it "enters" it, exposing the per-system sliders.
public sealed record PowerDistributionBlock(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
