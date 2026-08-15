namespace SpaceAdventure.Shared.Model;

// X/Y are in the station-panel's own abstract layout space (game_design.md section 10) — not
// tied to the ship's interior coordinates.
public sealed record StationNpc(string Id, string Name, NpcKind Kind, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
