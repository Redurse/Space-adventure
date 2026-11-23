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
    // A dead station (PoiKind.AbandonedStation): dockable like a station, but NOT one for anything that looks
    // for stations - trade routes, quests, patrols, the charts. Only the docking code opts it in.
    AbandonedStation,
}
