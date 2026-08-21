using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Replaces the old VoyagePhase-carrying record (M39): docking/combat/mining are all continuous,
// proximity-driven states now, not an exclusive mode the ship is "in", so there's no single phase
// left to report - just the handful of independent facts a client actually needs to draw the HUD.
public sealed record VoyageState(
    Vec2 ShipMapPosition,
    string? DockedPointId,
    bool IsInBattle,
    // Whether the Station snapshot fields mean anything right now - many procedural systems have
    // no Station point at all (GalaxyMap.cs), and Station otherwise just keeps whatever position
    // it was last continuously tracked to (World.Voyage.cs's UpdateNearestStation), which would
    // read as a station floating in a system that doesn't have one without this.
    bool HasNearbyStation);
