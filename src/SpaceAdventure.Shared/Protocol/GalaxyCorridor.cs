namespace SpaceAdventure.Shared.Protocol;

// One edge of the galactic map's warp graph (GalaxyMap.Corridors) - sent over the wire as a small
// record rather than a raw tuple so it serializes with named fields like everything else here.
public sealed record GalaxyCorridor(string SystemAId, string SystemBId);
