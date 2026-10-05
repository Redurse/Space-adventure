namespace Anabiosis.Shared.Model;

public enum GalaxyPointKind
{
    Station,
    HostileSector,
    AsteroidField,
    // Loot-once points of interest (SalvagePoints.cs) - flown into, never fought or docked at.
    StoragePod,
    ShipGraveyard,
    AbandonedShip,
}
