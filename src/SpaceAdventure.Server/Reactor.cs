namespace SpaceAdventure.Server;

// Runs on a limited fuel supply; output cuts to zero once fuel runs out (game_design.md
// section 1 — "ограниченный расход, нужна дозаправка"). Burn rate scales with how much power
// is actually drawn, not a flat idle cost. On top of that, output also requires at least one
// fuel rod physically loaded into one of the 4 slots — an empty reactor produces nothing even
// with fuel left, matching the requested Barotrauma-style rod mechanic. Starts fully loaded so
// the ship is flight-ready by default; pulling every rod is what actually kills output.
public sealed class Reactor
{
    public const int RodSlotCount = 4;

    public float MaxOutput { get; }
    public float MaxFuel { get; }
    public float Fuel { get; private set; }
    public bool[] RodSlots { get; } = { true, true, true, true };

    private readonly float _fuelPerPowerUnitPerSecond;

    public bool HasFuelRod => Array.IndexOf(RodSlots, true) >= 0;
    public float CurrentOutput => Fuel > 0 && HasFuelRod ? MaxOutput : 0;

    public Reactor(float maxOutput, float maxFuel, float fuelPerPowerUnitPerSecond)
    {
        MaxOutput = maxOutput;
        MaxFuel = maxFuel;
        Fuel = maxFuel;
        _fuelPerPowerUnitPerSecond = fuelPerPowerUnitPerSecond;
    }

    public void Step(double deltaSeconds, float totalAllocatedPower)
    {
        if (Fuel <= 0)
            return;

        var consumed = totalAllocatedPower * _fuelPerPowerUnitPerSecond * (float)deltaSeconds;
        Fuel = Math.Max(0, Fuel - consumed);
    }

    // Station stop (game_design.md Phase1: "станция для дозаправки/ремонта") — tops the fuel
    // timer back up, but doesn't magically restock pulled rods; that's still on the crew.
    public void Refuel() => Fuel = MaxFuel;
}
