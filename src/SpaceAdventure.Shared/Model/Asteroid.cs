namespace SpaceAdventure.Shared.Model;

// Indestructible circular obstacle in an AsteroidField (game_design.md Phase 3 — open space /
// mineral mining). Ship and EVA characters alike collide with it; it never takes damage itself.
public sealed record Asteroid(string Id, float X, float Y, float Radius)
{
    public Vec2 Position => new(X, Y);
}
