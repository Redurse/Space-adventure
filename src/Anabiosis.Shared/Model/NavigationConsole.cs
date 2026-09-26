namespace Anabiosis.Shared.Model;

// Physical block on the bridge (game_design.md section 5) — click it to bring up the galaxy map
// and pick where to fly next.
//
// An ordinary rotatable floor-standing device - see HelmConsole's own doc comment (same shape,
// same rolled-back wall-mounted-panel history).
// HalfSide - see HelmConsole's own doc comment (same shape, same reasoning).
public sealed record NavigationConsole(string Id, string RoomId, float X, float Y, bool Rotated = false, TileSide HalfSide = TileSide.East)
{
    public Vec2 Position => new(X, Y);
}
