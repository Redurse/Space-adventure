namespace Anabiosis.Shared.War;

// The numbers of the fighting and of the Union military doctrine; the economy ones are in WarConfig.cs.
public sealed partial record WarConfig
{
    public double CombatRoundSeconds { get; init; } = 10.0;
    public double MilitaryIntervalSeconds { get; init; } = 10.0;

    // Fortifications: hit points by what is built, and how much of that they hit back with in a round.
    public float CapitalFortHp { get; init; } = 60000f;
    public float ColonyFortHp { get; init; } = 2500f;
    public float YardFortHp { get; init; } = 1200f;
    public float MegaYardFortHp { get; init; } = 4000f;
    public float OutpostFortHp { get; init; } = 300f;
    public float FortAttackPerHp { get; init; } = 0.02f;
    // A fortification mends by this fraction of its hit points per second, but only after ten quiet minutes.
    public float FortRegenPerSecond { get; init; } = 0.00005f;
    public double FortRegenAfterSeconds { get; init; } = 600.0;

    // How long an enemy must hold a system with nothing left to oppose it before it is taken.
    public float OutpostOccupationSeconds { get; init; } = 120f;
    public float ColonyOccupationSeconds { get; init; } = 900f;
    public float YardOccupationSeconds { get; init; } = 600f;
    public float CapitalOccupationSeconds { get; init; } = 21600f;

    // Doctrine. A relief force is sent when the enemy is at least this much stronger than the defenders, and it has to
    // reach this multiple of the enemy strength to go; a garrison withdraws when it is this much weaker than the enemy and
    // nothing comes in time; a counterattack goes when the force at hand is this many times the enemy there.
    public float ReliefMargin { get; init; } = 1.35f;
    // A relief that cannot reach even this multiple of the enemy strength is not sent at all.
    public float ReliefFloor { get; init; } = 1.0f;
    public float StrikeMargin { get; init; } = 1.2f;
    public float RetreatRatio { get; init; } = 0.5f;
    public float CounterMargin { get; init; } = 1.5f;
    // The share of a garrison that stays home when ships are taken from it for a relief force.
    public float HomeGuardShare { get; init; } = 0.25f;
    // At war the fleet limit is raised by this fraction, and what the doctrine wants held where (weights, not ships).
    public float WarSupplyBonus { get; init; } = 0.5f;
    public float CapitalGarrisonWeight { get; init; } = 80f;
    public float MegaYardGarrisonWeight { get; init; } = 25f;
    public float YardGarrisonWeight { get; init; } = 12f;
    public float ColonyGarrisonWeight { get; init; } = 8f;
    public float OutpostGarrisonWeight { get; init; } = 1f;
}
