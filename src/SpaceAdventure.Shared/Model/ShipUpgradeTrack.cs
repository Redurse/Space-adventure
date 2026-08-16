namespace SpaceAdventure.Shared.Model;

// Discrete, leveled ship upgrades from the station's Mechanic (game_design.md section 9, M13
// scope). Only 3 of the design doc's full list are wired up so far - each maps to a number that
// already does something in the simulation. Left out deliberately: hull/system "прочность" (there's
// no HP pool for the hull or systems yet, just breached/damaged booleans - nothing numeric to
// scale), engine power (the Engine power slider doesn't affect anything yet either - travel speed
// is a flat constant, game_design.md section 5 - upgrading it would be upgrading a no-op), and
// battery capacity (the design doc's emergency-failover use for the battery was never wired up
// either, per Battery.cs - a bigger capacity for a value nothing spends would be cosmetic only).
public enum ShipUpgradeTrack
{
    ReactorOutput,
    ReactorEfficiency,
    WeaponDamage,
}
