using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Jumping between star systems (game_design.md - two-tier map): near-instant, but only once the
// ship has flown to this system's own WarpPoint and parked there slowly enough - the same "parked
// alongside, under the speed limit" gate World.StationDocking.cs's CanDockNow already uses, just
// aimed at a different kind of point. Only systems joined by a corridor (GalaxyMap.Corridors) are
// reachable from here - the galaxy is a limited, non-crossing graph, not a full one.
public sealed partial class World
{
    private const float WarpCaptureRadius = 8f;
    private const float WarpMaxSpeed = 2f;

    private GalaxyPoint? CurrentWarpPoint =>
        GalaxyMap.GetSystem(_currentSystemId).Points.FirstOrDefault(p => p.Kind == GalaxyPointKind.WarpPoint);

    // What arms the helm's "Прыжок" button - the client mirrors this to decide whether to draw it.
    public bool CanWarpNow =>
        Phase == VoyagePhase.Traveling &&
        CurrentWarpPoint is { } warpPoint &&
        (warpPoint.Position - _shipFieldPosition).Length() < WarpCaptureRadius &&
        _shipVelocity.Length() < WarpMaxSpeed;

    private void TryWarpTo(string? systemId)
    {
        if (!CanWarpNow || systemId is null || systemId == _currentSystemId)
            return;
        if (!GalaxyMap.AreConnected(_currentSystemId, systemId))
            return;

        // Dropped right at the new system's own WarpPoint, stopped and stable - not the field's
        // bare centre, which could be far from it (game_design.md - two-tier map). This also means
        // CanWarpNow is already true again the instant the jump lands (0 distance, 0 speed), so a
        // system with more than one corridor can be crossed straight through without first having
        // to fly clear of the capture radius and back.
        var newWarpPoint = GalaxyMap.GetSystem(systemId).Points.FirstOrDefault(p => p.Kind == GalaxyPointKind.WarpPoint);
        _currentSystemId = systemId;
        _travelTargetPointId = null;
        _travelTargetPosition = null;
        _shipFieldPosition = newWarpPoint?.Position ?? GalaxyMap.GetSystem(systemId).Field.Center;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
    }

    private IReadOnlyList<StarSystemSummary> CreateStarSystemSummaries() =>
        GalaxyMap.Systems.Select(s => new StarSystemSummary(s.Id, s.Name, s.Field.Width, s.Field.Height, s.GalaxyX, s.GalaxyY)).ToArray();
}
