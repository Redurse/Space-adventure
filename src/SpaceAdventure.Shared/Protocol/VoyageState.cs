using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

public sealed record VoyageState(
    VoyagePhase Phase,
    Vec2 ShipMapPosition,
    string? DockedPointId,
    string? TravelTargetPointId,
    // The actual coordinate the autopilot is steering toward (World.Voyage.cs) - set whenever
    // TravelTargetPointId is (so a named point keeps working exactly as before), but also stands
    // alone for a free-form click with no point behind it, which has no id to look up.
    Vec2? TravelTargetPosition = null);
