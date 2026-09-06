namespace Anabiosis.Server;

// Charges from whatever reactor output isn't allocated to a system; the emergency power source
// if the reactor's own output falls short of what's allocated (game_design.md section 1) —
// PowerGrid.Step draws from it via Discharge whenever the reactor alone can't cover the sliders.
public sealed class Battery
{
    public float Capacity { get; }
    public float Charge { get; private set; }
    // Combat damage (World.EnemyAi.cs's ApplyEnemyAttack) - a wrecked battery neither stores
    // surplus nor covers a shortfall until repaired (World.SystemRepair.cs), same reasoning as
    // Reactor.Broken zeroing CurrentOutput.
    public bool Broken { get; set; }

    public Battery(float capacity, float initialCharge = 0)
    {
        Capacity = capacity;
        Charge = Math.Clamp(initialCharge, 0, capacity);
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
