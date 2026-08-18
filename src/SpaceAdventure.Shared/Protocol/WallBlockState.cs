namespace SpaceAdventure.Shared.Protocol;

// Parallels OreDepositState's Hp/MaxHp/Fraction shape (World.WallBlocks.cs) - a wall block used to
// carry just a bool (breached or not); it now has real hit points, invisible to the player until
// they weld or cut one, at which point the client shows this as a health bar.
public sealed record WallBlockState(string Id, float Hp, float MaxHp)
{
    public bool Breached => Hp <= 0f;
    public float Fraction => MaxHp > 0f ? Hp / MaxHp : 0f;
}
