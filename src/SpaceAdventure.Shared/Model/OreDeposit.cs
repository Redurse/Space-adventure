namespace SpaceAdventure.Shared.Model;

// A block of ore sitting on an asteroid's surface (game_design.md Phase 3, M18). It used to be a
// marker with a number of "cuts" left in it, worked by pressing a key next to it; it is now a small
// structure with its own size and hit points, and the only way to remove it is to hold a cutter's
// flame on it until it comes apart. What drops is one item, lying where the block was.
//
// The position is absolute and fixed - asteroids never move, so nothing here is expressed relative
// to one - and Radius is what the flame has to touch, which is also what the client draws.
public sealed record OreDeposit(string Id, string AsteroidId, float X, float Y, float MaxHp, float Radius = 0.55f)
{
    public Vec2 Position => new(X, Y);

    // How far a point is from the block's body: 0 anywhere on or inside it. What the cutting flame
    // is tested against, segment sample by segment sample.
    public float DistanceFrom(Vec2 point) => MathF.Max(0f, (point - Position).Length() - Radius);
}
