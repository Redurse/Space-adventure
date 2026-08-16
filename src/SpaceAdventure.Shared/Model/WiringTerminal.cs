namespace SpaceAdventure.Shared.Model;

// Physical block next to the distribution block (game_design.md section 1, M14) — click it to
// bring up the wiring schematic (WireNetwork) instead of the ship view.
public sealed record WiringTerminal(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
