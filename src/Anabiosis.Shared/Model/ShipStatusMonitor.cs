namespace Anabiosis.Shared.Model;

// Direct user request ("монитор состояния корабля... размером с навигационную панель") - a
// full-screen panel showing every room's own oxygen/Hp as a ship-shaped schematic. Same rotatable
// floor-standing shape as NavigationConsole/HelmConsole (see HelmConsole's own doc comment).
public sealed record ShipStatusMonitor(string Id, string RoomId, float X, float Y, bool Rotated = false, TileSide HalfSide = TileSide.East)
{
    public Vec2 Position => new(X, Y);
}
