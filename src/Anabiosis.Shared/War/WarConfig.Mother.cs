namespace Anabiosis.Shared.War;

// The mother ship: thick armour, slow, hardly armed. It runs when the enemy is this many lanes away.
public sealed partial record WarConfig
{
    public bool MotherEnabled { get; init; } = true;
    public float MotherHp { get; init; } = 40000f;
    public float MotherAttack { get; init; } = 150f;
    // Map units per second: slower than any warship.
    public float MotherSpeed { get; init; } = 1.2f;
    public int MotherFleeHops { get; init; } = 6;
    // How strongly the virus front leans toward the mother ship (a share of the distance from a target to her).
    public float VirusMotherPull { get; init; } = 0.5f;
}
