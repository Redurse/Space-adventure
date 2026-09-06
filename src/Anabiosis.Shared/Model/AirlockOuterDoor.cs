namespace Anabiosis.Shared.Model;

// The airlock's second door (game_design.md Phase 3, M16) — like Door, but only one side connects
// to a real room; the other side is vacuum (the open sector itself, not a room with an Id or an
// oxygen level of its own). Deliberately its own type rather than a nullable RoomBId on Door: every
// existing Door call site (movement crossing, atmosphere diffusion, rendering) assumes two real
// rooms, and threading a null through all of that would be far messier than one small sibling type.
public sealed record AirlockOuterDoor(string Id, string RoomId, float X, float Y, float Width, float Height)
{
    public float Left => X - Width / 2;
    public float Right => X + Width / 2;
    public float Top => Y - Height / 2;
    public float Bottom => Y + Height / 2;
    public Vec2 Position => new(X, Y);

    public bool Contains(Vec2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;
}
