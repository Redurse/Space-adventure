namespace SpaceAdventure.Shared.Model;

// The pilot's console (game_design.md Phase 3 — open space movement): stand here to take manual
// control of the ship. Brings up a Barotrauma-style joystick schematic instead of the ship view.
public sealed record HelmConsole(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
