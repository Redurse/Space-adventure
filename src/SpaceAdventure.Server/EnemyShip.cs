namespace SpaceAdventure.Server;

// Abstract HP only (game_design.md section 2: "по вражеским кораблям (пока) — абстрактный общий
// HP"). No position tie-in yet. One instance is reused across encounters — Reset() respawns it
// full-health whenever the ship arrives at a new HostileSector point (see World.Voyage.cs).
public sealed class EnemyShip
{
    private const float RetreatHpFraction = 0.2f; // game_design.md section 11: retreats at low HP

    public float MaxHp { get; }
    public float Hp { get; private set; }

    // Stops attacking (see World.StepEnemyAi) but isn't destroyed — still shootable.
    public bool IsRetreating => Hp > 0 && Hp <= MaxHp * RetreatHpFraction;

    public EnemyShip(float maxHp)
    {
        MaxHp = maxHp;
        Hp = maxHp;
    }

    public void ApplyDamage(float amount) => Hp = Math.Max(0, Hp - amount);

    public void Reset() => Hp = MaxHp;
}
