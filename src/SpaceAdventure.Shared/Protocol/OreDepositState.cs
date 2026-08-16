namespace SpaceAdventure.Shared.Protocol;

// How far through a block of ore the cutting has got. Hp <= 0 means it has already come apart and
// dropped its item, so the client stops drawing it.
public sealed record OreDepositState(string DepositId, float Hp, float MaxHp)
{
    public float Fraction => MaxHp > 0 ? Hp / MaxHp : 0f;
}
