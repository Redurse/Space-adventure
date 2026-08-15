using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// The distribution block: continuous sliders whose sum is capped by the reactor's current
// output (game_design.md section 1 — "сумма всех слайдеров ограничена текущей мощностью
// реактора"). Surplus charges the battery; if reactor output drops (fuel out) below what's
// currently allocated, existing allocations are scaled back down to fit.
public sealed class PowerGrid
{
    private const float AdjustRatePerSecond = 20f;

    private static readonly PowerSystemId[] Systems = Enum.GetValues<PowerSystemId>();

    public Reactor Reactor { get; }
    public Battery Battery { get; }

    private readonly Dictionary<PowerSystemId, float> _allocated;
    private readonly Dictionary<PowerSystemId, bool> _damaged;
    private int _adjustIndex = -1;
    private float _adjustDirection;

    public PowerGrid()
    {
        Reactor = new Reactor(maxOutput: 60f, maxFuel: 500f, fuelPerPowerUnitPerSecond: 0.05f);
        Battery = new Battery(capacity: 200f);
        _allocated = Systems.ToDictionary(s => s, _ => 0f);
        _damaged = Systems.ToDictionary(s => s, _ => false);
    }

    public void ApplyInput(int systemIndex, float direction)
    {
        _adjustIndex = systemIndex;
        _adjustDirection = direction;
    }

    // Effective output — zero while the system's physical block is damaged (game_design.md
    // section 1), regardless of where its distribution slider sits. The slider itself is left
    // alone, so the system resumes at the same allocation once repaired.
    public float GetAllocation(PowerSystemId system) =>
        IsDamaged(system) ? 0f : (_allocated.TryGetValue(system, out var value) ? value : 0f);

    public bool IsDamaged(PowerSystemId system) => _damaged.TryGetValue(system, out var damaged) && damaged;

    public void SetDamaged(PowerSystemId system, bool damaged) => _damaged[system] = damaged;

    public void Step(double deltaSeconds)
    {
        if (_adjustDirection != 0 && _adjustIndex >= 0 && _adjustIndex < Systems.Length)
        {
            var system = Systems[_adjustIndex];
            var othersTotal = _allocated.Where(kv => kv.Key != system).Sum(kv => kv.Value);
            var maxForThis = Math.Max(0, Reactor.CurrentOutput - othersTotal);
            var next = _allocated[system] + _adjustDirection * AdjustRatePerSecond * (float)deltaSeconds;
            _allocated[system] = Math.Clamp(next, 0, maxForThis);
        }

        Reactor.Step(deltaSeconds, _allocated.Values.Sum());
        RescaleIfOverBudget();

        var surplus = Math.Max(0, Reactor.CurrentOutput - _allocated.Values.Sum());
        Battery.AddCharge(surplus * (float)deltaSeconds);
    }

    private void RescaleIfOverBudget()
    {
        var total = _allocated.Values.Sum();
        if (total <= Reactor.CurrentOutput || total <= 0)
            return;

        var scale = Reactor.CurrentOutput / total;
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
