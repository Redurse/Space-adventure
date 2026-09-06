namespace Anabiosis.Shared.Model;

public sealed record ShipUpgradeDefinition(ShipUpgradeTrack Track, string Name, int MaxLevel, IReadOnlyList<int> CostPerLevel);

// Fixed catalog offered by every station's Mechanic (game_design.md section 9) - same 3 levels
// and prices everywhere for now, same M13 simplification as the Trader's catalog (TradeCatalog).
public static class ShipUpgradeCatalog
{
    public static readonly IReadOnlyList<ShipUpgradeDefinition> Tracks = new[]
    {
        new ShipUpgradeDefinition(ShipUpgradeTrack.ReactorOutput, "Выработка реактора", MaxLevel: 3, CostPerLevel: new[] { 200, 400, 800 }),
        new ShipUpgradeDefinition(ShipUpgradeTrack.ReactorEfficiency, "Экономичность реактора", MaxLevel: 3, CostPerLevel: new[] { 200, 400, 800 }),
        new ShipUpgradeDefinition(ShipUpgradeTrack.WeaponDamage, "Урон орудий", MaxLevel: 3, CostPerLevel: new[] { 200, 400, 800 }),
    };

    public static ShipUpgradeDefinition Find(ShipUpgradeTrack track) => Tracks.First(t => t.Track == track);
}
