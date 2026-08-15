namespace SpaceAdventure.Shared.Model;

// Rectangular footprint in world units (meters). No wall/door collision yet — M2 is an open
// floor plan; per-room collision and doors are a later milestone (see game_design.md Phase 1).
public sealed record Room(string Id, string Name, float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;
    public Vec2 Center => new(X + Width / 2, Y + Height / 2);

    public bool Contains(Vec2 p) => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;
}
