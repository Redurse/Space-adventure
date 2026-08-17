using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Jumping between star systems (game_design.md - two-tier map): near-instant, but only once the
// ship has flown to this system's own WarpPoint and parked there slowly enough - the same "parked
// alongside, under the speed limit" gate World.StationDocking.cs's CanDockNow already uses, just
// aimed at a different kind of point. Every system is reachable from every other (a full graph),
// so the only checks are "does this system exist" and "we're not already there".
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
        if (GalaxyMap.Systems.All(s => s.Id != systemId))
            return;

        _currentSystemId = systemId;
        _travelTargetPointId = null;
        // Dropped in the middle, stopped and stable - the same arrival guarantee EnterAsteroidField
        // already gives, so the crew never appears mid-drift in an unfamiliar system.
        _shipFieldPosition = GalaxyMap.GetSystem(systemId).Field.Center;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
    }

    private IReadOnlyList<StarSystemSummary> CreateStarSystemSummaries() =>
        GalaxyMap.Systems.Select(s => new StarSystemSummary(s.Id, s.Name)).ToArray();
}
