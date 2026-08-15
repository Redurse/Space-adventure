using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

public sealed record VoyageState(
    VoyagePhase Phase,
    Vec2 ShipMapPosition,
    string? DockedPointId,
    string? TravelTargetPointId);
