namespace SpaceAdventure.Shared.Model;

public enum GalaxyPointKind
{
    Station,
    HostileSector,
    AsteroidField,
    // Where a system can be left from (World.StarSystems.cs) - flying here and parking slowly
    // enough arms the warp jump, the same way parking at a berth arms docking.
    WarpPoint,
}
