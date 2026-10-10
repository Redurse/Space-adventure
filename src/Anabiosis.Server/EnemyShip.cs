namespace Anabiosis.Server;

// A hostile ship's overall condition as one number, for the HP readout and for deciding when it breaks off. It is a VIEW
// of the simulated hull: World.EnemyDamage.cs feeds it the share of the hull's compartments still standing
// (SetStructure), while ApplyDamage is the "this ship is simply lost" lever (the whole crew gone, a debug kill) that no
// amount of structure can offset. Hp reaches 0 exactly when the ship is destroyed.
public sealed class EnemyShip
{
    private const float RetreatHpFraction = 0.2f; // game_design.md section 11: retreats at low HP

    private float _structureFraction = 1f;
    private float _directDamage;

    public float MaxHp { get; }
    public float Hp { get; private set; }

    // Stops attacking (see World.StepEnemyAi) but isn't destroyed - still shootable.
    public bool IsRetreating => Hp > 0 && Hp <= MaxHp * RetreatHpFraction;

    public EnemyShip(float maxHp)
    {
        MaxHp = maxHp;
        Hp = maxHp;
    }

    public void ApplyDamage(float amount)
    {
        _directDamage += amount;
        Refresh();
    }

    // How much of the hull is still intact, 0..1. A ship that is already destroyed stays destroyed.
    public void SetStructure(float fraction)
    {
        _structureFraction = Math.Clamp(fraction, 0f, 1f);
        Refresh();
    }

    public void Reset()
    {
        _structureFraction = 1f;
        _directDamage = 0f;
        Hp = MaxHp;
    }

    private void Refresh() => Hp = Math.Max(0f, MaxHp * _structureFraction - _directDamage);
}
