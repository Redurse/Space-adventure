namespace SpaceAdventure.Server;

// Charges from whatever reactor output isn't allocated to a system; becomes the emergency
// power source if the reactor fails (game_design.md section 1) — that failover isn't wired
// up yet since nothing consumes power from Battery directly in M4.
public sealed class Battery
{
    public float Capacity { get; }
    public float Charge { get; private set; }

    public Battery(float capacity, float initialCharge = 0)
    {
        Capacity = capacity;
        Charge = Math.Clamp(initialCharge, 0, capacity);
    }

    public void AddCharge(float amount) => Charge = Math.Clamp(Charge + amount, 0, Capacity);
}
