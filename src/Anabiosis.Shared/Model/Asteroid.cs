namespace Anabiosis.Shared.Model;

// Indestructible circular obstacle in an AsteroidField (game_design.md Phase 3 — open space /
// mineral mining). Ship and EVA characters alike collide with it; it never takes damage itself.
// X/Y are double, not float (M58 follow-up - matching Vec2's own M56 conversion): at KSP-real
// field scale (hundreds of billions of units) a float32 position can't distinguish two asteroids
// even a few hundred units apart - AsteroidField.CreateDefault's whole hand-placed cluster (5 rocks
// spread across ~150 units) was silently collapsing onto one single point, since the small offset
// from ClusterCenter vanished the moment it got narrowed to float, leaving the ship permanently
// "colliding" with a pile of rocks sitting exactly on top of it the instant it arrived there
// (World.ShipField.cs's TryFindHullCollision), unable to ever move - the root cause behind most of
// the test suite's own sweeping failures (asteroid-field-epsilon is the shared "ship at rest" test
// scaffolding point almost every helm/EVA/mining test starts from).
public sealed record Asteroid(string Id, double X, double Y, float Radius)
{
    public Vec2 Position => new(X, Y);
}
