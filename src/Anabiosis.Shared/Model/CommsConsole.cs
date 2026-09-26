namespace Anabiosis.Shared.Model;

// Direct user request ("консоль связи... размером с навигационную панель") - a full-screen panel
// showing the current system's own map plus a stub "Установить связь" button that does nothing yet
// (a placeholder for a not-yet-designed feature). Same rotatable floor-standing shape as
// NavigationConsole/HelmConsole (see HelmConsole's own doc comment).
public sealed record CommsConsole(string Id, string RoomId, float X, float Y, bool Rotated = false, TileSide HalfSide = TileSide.East)
{
    public Vec2 Position => new(X, Y);
}
