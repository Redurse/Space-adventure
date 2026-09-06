using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

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
    bool HasNearbyStation,
    // M55 - which body the ship is currently sitting on, mirroring DockedPointId's own shape (null
    // means "in the system's own field", same as docking). The client never needs the body's own
    // geometry sent to it - PlanetSurface.Generate(bodyId) is a pure function, so it derives the
    // exact same terrain from this id alone, the same way it already derives asteroid belts/body
    // positions from ids without either ever crossing the wire.
    string? LandedBodyId = null);
