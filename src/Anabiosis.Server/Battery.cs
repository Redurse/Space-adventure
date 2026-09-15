namespace Anabiosis.Server;

// Charges from whatever reactor output isn't allocated to a system; the emergency power source
// if the reactor's own output falls short of what's allocated (game_design.md section 1) —
// PowerGrid.Step draws from it via Discharge whenever the reactor alone can't cover the sliders.
public sealed class Battery
{
    // Direct user request ("сделай чтобы на корабле могло быть несколько батарей") - same
    // base+bonus split as Reactor.MaxOutput/OutputBonus: _baseCapacity is whatever this hull was
    // built with, CapacityBonus is re-derived from Ship.BatteryDeviceCount whenever the ship's own
    // structure changes (World.ShipBuilding.cs's RecomputeDeviceBonuses).
    private readonly float _baseCapacity;
    public float CapacityBonus { get; private set; }
    public float Capacity => _baseCapacity + CapacityBonus;
    public float Charge { get; private set; }
    // Combat damage (World.EnemyAi.cs's ApplyEnemyAttack) - a wrecked battery neither stores
    // surplus nor covers a shortfall until repaired (World.SystemRepair.cs), same reasoning as
    // Reactor.Broken zeroing CurrentOutput.
    public bool Broken { get; set; }

    public Battery(float capacity, float initialCharge = 0)
    {
        _baseCapacity = capacity;
        Charge = Math.Clamp(initialCharge, 0, capacity);
    }

    // Losing a battery room (demolition) can shrink Capacity below the charge already stored -
    // clamped immediately rather than left to drift back in line on the next AddCharge, the same
    // "correct the moment the bonus changes" discipline Reactor.OutputBonus's own setter follows.
    public void SetCapacityBonus(float bonus)
    {
        CapacityBonus = bonus;
        Charge = Math.Clamp(Charge, 0, Capacity);
    }

    public void AddCharge(float amount)
    {
        if (!Broken)
            Charge = Math.Clamp(Charge + amount, 0, Capacity);
    }

    // amount/return value are energy (power * time), same units AddCharge already uses - callers
    // wanting an equivalent power figure divide the returned energy back by their own deltaSeconds.
    public float Discharge(float amount)
    {
        if (Broken)
            return 0f;
        var actual = Math.Min(amount, Charge);
        Charge -= actual;
        return actual;
    }
}
