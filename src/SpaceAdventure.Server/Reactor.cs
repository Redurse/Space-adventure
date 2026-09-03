namespace SpaceAdventure.Server;

// Runs on a limited fuel supply; output cuts to zero once fuel runs out (game_design.md
// section 1 — "ограниченный расход, нужна дозаправка"). Burn rate scales with how much power
// is actually drawn, not a flat idle cost.
//
// The fuel lives *in the rods*, not in a tank behind them (Barotrauma-style): each of the 4 slots
// holds either nothing or a rod with its own remaining charge, and Fuel is just what's currently
// loaded. That's what makes a fresh rod worth carrying to the reactor — a rod taken off the rack
// arrives full, so swapping a spent one out is the actual refuelling action rather than a cosmetic
// click on top of a separate tank. Rods burn one at a time so a dead one is visible in its slot.
// Starts fully loaded so the ship is flight-ready by default.
public sealed class Reactor
{
    public const int RodSlotCount = 4;

    // MaxOutput folds in the station Mechanic's reactor-output upgrade (game_design.md section 9,
    // M13) — everything that reads MaxOutput (CurrentOutput below, PowerGrid's clamp, the HUD)
    // automatically reflects the upgraded cap without a second field to keep in sync.
    public float MaxOutput => _baseMaxOutput + OutputBonus;
    public float OutputBonus { get; set; }

    // MaxFuel keeps its old meaning — a full set of rods — so the HUD readout is unchanged.
    public float MaxFuel { get; }
    public float RodCapacity => MaxFuel / RodSlotCount;

    // null = empty slot, otherwise the charge left in the rod sitting there (0 = spent but still in).
    private readonly float?[] _rods = new float?[RodSlotCount];
    public IReadOnlyList<float?> Rods => _rods;

    // Multiplies fuel burn — the Mechanic's reactor-efficiency upgrade lowers this below 1
    // (game_design.md section 9: "экономичность реактора, меньше расход стержней").
    public float FuelEfficiencyMultiplier { get; set; } = 1f;

    private readonly float _baseMaxOutput;
    private readonly float _fuelPerPowerUnitPerSecond;

    public float Fuel
    {
        get
        {
            var total = 0f;
            foreach (var rod in _rods)
                total += rod ?? 0f;
            return total;
        }
    }

    public bool HasFuelRod => Array.Exists(_rods, r => r is not null);

    // The reactor's own physical kill-switch lever (game_design.md - reactor levers), independent
    // of the fuel rods: flipping it forces output to zero even with a full load, and restoring it
    // resumes normal fuel-driven output with no other state to reconcile.
    public bool EmergencyShutdown { get; set; }
    // Combat damage (World.EnemyAi.cs's ApplyEnemyAttack, enemy/weapon overhaul - "реактор мог
    // быть сломан") - same all-or-nothing shape as EmergencyShutdown, just triggered by a hit
    // instead of a lever, and cleared the same way every other damaged fixture is: the wrench/
    // screwdriver repair minigame (World.SystemRepair.cs) rather than flipping it back by hand.
    public bool Broken { get; set; }
    // Set externally by World.Upgrades.cs's ApplyUpgradeEffects, from a plain Room.Name comparison
    // against ShipZoneKinds.CanonicalName(ShipZoneKind.ReactorRoom) - direct user request ("если
    // реактор стоит не в своей зоне то он получает дебаф к продуктивности в 10 процентов"). 1f
    // whenever the reactor sits in its own zone (or the player never named one at all - building
    // outside a matching zone is allowed, just penalized, never blocked).
    public float ZonePenaltyMultiplier { get; set; } = 1f;
    public float CurrentOutput => !EmergencyShutdown && !Broken && Fuel > 0 ? MaxOutput * ZonePenaltyMultiplier : 0;

    public Reactor(float maxOutput, float maxFuel, float fuelPerPowerUnitPerSecond)
    {
        _baseMaxOutput = maxOutput;
        MaxFuel = maxFuel;
        _fuelPerPowerUnitPerSecond = fuelPerPowerUnitPerSecond;
        for (var i = 0; i < RodSlotCount; i++)
            _rods[i] = RodCapacity;
    }

    public bool IsRodLoaded(int slotIndex) => _rods[slotIndex] is not null;

    // A rod carried in from the rack is a new one, so it comes in at full charge. Whatever was
    // left in a rod that gets pulled out doesn't follow it into the inventory — an inventory slot
    // holds an item type, not an item, and rods are unlimited off the rack anyway.
    public void InsertRod(int slotIndex) => _rods[slotIndex] = RodCapacity;

    public void RemoveRod(int slotIndex) => _rods[slotIndex] = null;

    public void Step(double deltaSeconds, float totalAllocatedPower)
    {
        var remaining = totalAllocatedPower * _fuelPerPowerUnitPerSecond * FuelEfficiencyMultiplier * (float)deltaSeconds;

        for (var i = 0; i < RodSlotCount && remaining > 0; i++)
        {
            if (_rods[i] is not { } charge || charge <= 0)
                continue;

            var burnt = Math.Min(charge, remaining);
            _rods[i] = charge - burnt;
            remaining -= burnt;
        }
    }

    // Station stop (game_design.md Phase1: "станция для дозаправки/ремонта") — recharges the rods
    // that are actually in the reactor, but doesn't conjure up ones the crew pulled out.
    public void Refuel()
    {
        for (var i = 0; i < RodSlotCount; i++)
            if (_rods[i] is not null)
                _rods[i] = RodCapacity;
    }
}
