using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Ship upgrades from the station Mechanic (game_design.md section 9, M13 scope). Discrete levels
// (ShipUpgradeCatalog: 3 per track), crew-wide like Credits, only purchasable while docked - same
// gate as the Trader and the Administrator's quest. Effects apply immediately on purchase and
// persist for the rest of the run (no way to lose a level).
public sealed partial class World
{
    private const float ReactorOutputBonusPerLevel = 15f;
    private const float ReactorEfficiencyMultiplierPerLevel = 0.8f; // multiplicative per level
    private const float WeaponDamageBonusPerLevel = 3f;

    private readonly Dictionary<ShipUpgradeTrack, int> _upgradeLevels =
        Enum.GetValues<ShipUpgradeTrack>().ToDictionary(t => t, _ => 0);

    public IReadOnlyDictionary<ShipUpgradeTrack, int> UpgradeLevels => _upgradeLevels;

    private float WeaponDamageBonus => _upgradeLevels[ShipUpgradeTrack.WeaponDamage] * WeaponDamageBonusPerLevel;

    // No-ops if not docked, the track is already maxed, or the crew can't afford the next level.
    private void TryPurchaseUpgrade(ShipUpgradeTrack track)
    {
        if (Phase != VoyagePhase.Station)
            return;

        var level = _upgradeLevels[track];
        var definition = ShipUpgradeCatalog.Find(track);
        if (level >= definition.MaxLevel)
            return;

        var cost = definition.CostPerLevel[level];
        if (Credits < cost)
            return;

        Credits -= cost;
        _upgradeLevels[track] = level + 1;
        ApplyUpgradeEffects();
    }

    // Reactor bonuses live on the Reactor itself (PowerGrid.Reactor.MaxOutput folds OutputBonus
    // in automatically) - re-derive them from scratch off the current levels rather than
    // incrementing, so this stays correct regardless of purchase order.
    private void ApplyUpgradeEffects()
    {
        PowerGrid.Reactor.OutputBonus = _upgradeLevels[ShipUpgradeTrack.ReactorOutput] * ReactorOutputBonusPerLevel;
        PowerGrid.Reactor.FuelEfficiencyMultiplier =
            MathF.Pow(ReactorEfficiencyMultiplierPerLevel, _upgradeLevels[ShipUpgradeTrack.ReactorEfficiency]);
    }
}
