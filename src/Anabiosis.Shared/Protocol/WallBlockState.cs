using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// Parallels OreDepositState's Hp/MaxHp/Fraction shape (World.WallBlocks.cs) - a wall block used to
// carry just a bool (breached or not); it now has real hit points, invisible to the player until
// they weld or cut one, at which point the client shows this as a health bar.
//
// Material (direct user request, humble-soaring-cat.md M76 follow-up "варианты стен") defaults to
// Standard - every station/enemy/hand-authored-hull wall block, plus every custom ship built before
// this existed, reports Standard here regardless of MaxHp, which is exactly what they already were.
public sealed record WallBlockState(string Id, float Hp, float MaxHp, WallMaterial Material = WallMaterial.Standard)
{
    public bool Breached => Hp <= 0f;
    public float Fraction => MaxHp > 0f ? Hp / MaxHp : 0f;
}
