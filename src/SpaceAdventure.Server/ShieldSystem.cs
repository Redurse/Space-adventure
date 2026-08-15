namespace SpaceAdventure.Server;

// Absorbs enemy attacks before they reach the ship (game_design.md section 1 — shields as the
// outer layer of the power grid). Recharges only from power actually routed to the Shields
// slider; starts empty, same as every other system, so it's a real allocation choice rather
// than a free default.
public sealed class ShieldSystem
{
    public const float MaxPoints = 100f;
    private const float DamagePerHit = 34f; // ~3 absorbed attacks to fully deplete
    private const float RechargePerPowerUnitPerSecond = 0.4f; // full power (60) ~= 24/sec

    public float Points { get; private set; }

    public void Step(double deltaSeconds, float shieldsPowerAllocation)
    {
        var recharge = shieldsPowerAllocation * RechargePerPowerUnitPerSecond * (float)deltaSeconds;
        Points = Math.Min(MaxPoints, Points + recharge);
    }

    // Called once per enemy attack, before it's allowed to hit a turret/system/hull. Returns
    // true if the shield had charge to absorb it (the attack is fully negated that cycle).
    public bool TryAbsorbHit()
    {
        if (Points <= 0)
            return false;

        Points = Math.Max(0, Points - DamagePerHit);
        return true;
    }
}
