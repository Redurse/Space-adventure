using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// The distribution block: continuous sliders whose sum is capped by the reactor's current
// output (game_design.md section 1 — "сумма всех слайдеров ограничена текущей мощностью
// реактора"). Surplus charges the battery; if reactor output drops (fuel out) below what's
// currently allocated, the battery discharges to cover the gap first, and only whatever it
// can't cover gets scaled back down.
public sealed class PowerGrid
{
    private const float AdjustRatePerSecond = 20f;

    private static readonly PowerSystemId[] Systems = Enum.GetValues<PowerSystemId>();

    public Reactor Reactor { get; }
    public Battery Battery { get; }
    // Combat damage (World.EnemyAi.cs's ApplyEnemyAttack) - a wrecked distribution block freezes
    // every slider exactly where it was until repaired (World.SystemRepair.cs); allocation itself
    // still runs normally (Reactor output, the battery's own charge/discharge), just nobody can
    // move a slider while it's out.
    public bool DistributionBroken { get; set; }

    private readonly Dictionary<PowerSystemId, float> _allocated;
    // Per player, not a single shared "currently held" slot - two crew members adjusting sliders
    // in the same tick used to stomp on each other (whichever player's command World.cs's
    // ApplyCommand happened to process last silently overwrote the other's), which in practice
    // meant only one specific player's input ever actually stuck. Each player's own held
    // direction now applies independently every Step, so two people can run different sliders (or
    // even the same one) at once.
    private readonly Dictionary<int, (int Index, float Direction)> _adjustByPlayer = new();

    public PowerGrid()
    {
        // Burn rate: at 0.05 a full load of rods lasted under three minutes of flight at full draw,
        // which turned the reactor into a chore you fed every other jump instead of a system you
        // occasionally look after. An eighth of that puts a full set at something like twenty
        // minutes wide open, and far longer at the draw a ship actually cruises on.
        Reactor = new Reactor(maxOutput: 60f, maxFuel: 500f, fuelPerPowerUnitPerSecond: 0.006f);
        Battery = new Battery(capacity: 200f);
        _allocated = Systems.ToDictionary(s => s, _ => 0f);
    }

    /// <summary>Hands every system an equal share of the reactor.
    ///
    /// Called when a run begins - not from the constructor. A ship that boots with nothing allocated
    /// has no oxygen, no engines and no shields until somebody walks to the distribution block, which
    /// is a puzzle rather than a start; but a PowerGrid is also constructed by every test that needs
    /// a world, and handing those a fully powered ship changes what they are testing. Where a rule
    /// applies is as much part of it as what it says.</summary>
    public void SplitEvenly()
    {
        var share = Reactor.MaxOutput / Systems.Length;
        foreach (var system in Systems)
            _allocated[system] = share;
    }

    public void ApplyInput(int playerId, int systemIndex, float direction) =>
        _adjustByPlayer[playerId] = (systemIndex, direction);

    // Raw slider allocation only — PowerGrid no longer knows about wire damage at all (M14 moved
    // that into World.Wiring.cs, since "is this system actually connected" now depends on the
    // wiring graph, not a single per-system flag). Callers that care whether power actually
    // reaches a system must go through World's wiring-aware accessor instead of this directly.
    public float GetAllocation(PowerSystemId system) =>
        _allocated.TryGetValue(system, out var value) ? value : 0f;

    public void Step(double deltaSeconds)
    {
        // Every player's own held slider applies independently this tick - recomputing
        // othersTotal/maxForThis fresh per player (rather than once up front) means an adjustment
        // already applied earlier in this same loop is accounted for, so two players pushing the
        // same system at once both actually add up instead of one clobbering the other's math.
        foreach (var (index, direction) in _adjustByPlayer.Values)
        {
            if (DistributionBroken || direction == 0 || index < 0 || index >= Systems.Length)
                continue;

            var system = Systems[index];
            var othersTotal = _allocated.Where(kv => kv.Key != system).Sum(kv => kv.Value);
            var maxForThis = Math.Max(0, Reactor.CurrentOutput - othersTotal);
            var next = _allocated[system] + direction * AdjustRatePerSecond * (float)deltaSeconds;
            _allocated[system] = Math.Clamp(next, 0, maxForThis);
        }

        Reactor.Step(deltaSeconds, _allocated.Values.Sum());

        // If the reactor alone can't cover what's allocated (fuel ran low, a slider was pushed up
        // faster than the reactor caught up), the battery covers the shortfall before anything gets
        // rescaled down - the whole point of an emergency reserve is to smooth over exactly this,
        // not just look pretty while sliders get clipped anyway.
        var shortfall = Math.Max(0, _allocated.Values.Sum() - Reactor.CurrentOutput);
        var suppliedPower = 0f;
        if (shortfall > 0 && deltaSeconds > 0)
        {
            var suppliedEnergy = Battery.Discharge(shortfall * (float)deltaSeconds);
            suppliedPower = suppliedEnergy / (float)deltaSeconds;
        }

        var availableOutput = Reactor.CurrentOutput + suppliedPower;
        RescaleIfOverBudget(availableOutput);

        var surplus = Math.Max(0, availableOutput - _allocated.Values.Sum());
        Battery.AddCharge(surplus * (float)deltaSeconds);
    }

    private void RescaleIfOverBudget(float availableOutput)
    {
        var total = _allocated.Values.Sum();
        if (total <= availableOutput || total <= 0)
            return;

        var scale = availableOutput / total;
        foreach (var system in Systems)
            _allocated[system] *= scale;
    }

    public PowerState CreateState() => new(
        Reactor.MaxOutput,
        Reactor.CurrentOutput,
        Reactor.Fuel,
        Reactor.MaxFuel,
        Battery.Charge,
        Battery.Capacity,
        new Dictionary<PowerSystemId, float>(_allocated));
}
