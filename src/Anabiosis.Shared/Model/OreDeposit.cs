namespace Anabiosis.Shared.Model;

// A block of ore sitting on an asteroid's surface (game_design.md Phase 3, M18). It used to be a
// marker with a number of "cuts" left in it, worked by pressing a key next to it; it is now a small
// structure with its own size and hit points, and the only way to remove it is to hold a cutter's
// flame on it until it comes apart. What drops is one item, lying where the block was.
//
// The position is absolute and fixed - asteroids never move, so nothing here is expressed relative
// to one - and Radius is what the flame has to touch, which is also what the client draws.
// X/Y are double, not float, for the same reason Asteroid's own doc comment gives - a deposit's
// small offset from its parent asteroid's already-huge absolute position needs it too.
// OreType (direct user request, "добавим множество предметов материалов") - which specific
// ItemType.*Ore/element this deposit drops once its Hp reaches 0 (World.Cutting.cs's CutAlongFlame),
// replacing the old one-size-fits-all ItemType.Mineral drop. Defaults to Mineral so nothing outside
// AsteroidField.CreateDefault's own 13 hand-placed deposits (the only real call site) needs updating.
public sealed record OreDeposit(string Id, string AsteroidId, double X, double Y, float MaxHp, float Radius = 0.55f, ItemType OreType = ItemType.Mineral)
{
    public Vec2 Position => new(X, Y);

    // How far a point is from the block's body: 0 anywhere on or inside it. What the cutting flame
    // is tested against, segment sample by segment sample.
    public float DistanceFrom(Vec2 point) => MathF.Max(0f, (float)((point - Position).Length() - Radius));
}
