using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Jumping between star systems (game_design.md - two-tier map): near-instant, but only once the
// ship has flown far enough from this system's own field centre - past GalaxyMap.WarpZoneRadius,
// a ring around the WHOLE system rather than one specific point to hunt down and park on - and
// slowed down enough, the same "parked alongside, under the speed limit" gate
// World.StationDocking.cs's CanDockNow already uses, just aimed at an area instead of a point. Any
// system within GalaxyMap.WarpJumpRadius of the current one (a circle on the galactic map,
// GalaxyMap.IsWithinWarpRange) is a valid jump target - no hand-authored corridor graph.
public sealed partial class World
{
    private const float WarpMaxSpeed = 2f;
    // How many already-generated jump targets the current system should have on hand at all times
    // - the trigger for GalaxyMap.EnsureGenerated's own lazy top-up (Shared/Model/GalaxyMap.cs).
    // Checked every snapshot (CreateStarSystemSummaries below), so the galactic map always has real
    // targets to show near wherever the crew actually is, without ever pre-rolling the whole thing.
    private const int MinReachableNeighborsWhileExploring = 3;

    // What arms the helm's "Прыжок" button - the client mirrors this to decide whether to draw it.
    public bool CanWarpNow =>
        !IsDocked && !IsInBattle &&
        (_shipFieldPosition - GalaxyMap.GetSystem(_currentSystemId).Field.Center).Length() >= GalaxyMap.WarpZoneRadius &&
        _shipVelocity.Length() < WarpMaxSpeed;

    private void TryWarpTo(string? systemId)
    {
        if (!CanWarpNow || systemId is null || systemId == _currentSystemId)
            return;
        GalaxyMap.EnsureGenerated(_currentSystemId, MinReachableNeighborsWhileExploring);
        if (!GalaxyMap.IsWithinWarpRange(_currentSystemId, systemId))
            return;

        // Arrives on the same side of the new system's field (relative to its own centre) the ship
        // left the old one from, rather than some arbitrary fixed spot regardless of heading - a
        // jump this way reads as "came in from that direction", not a teleport to a magic corner.
        var exitDirection = _shipFieldPosition - GalaxyMap.GetSystem(_currentSystemId).Field.Center;
        var heading = exitDirection.Normalized();
        if (heading == Vec2.Zero)
            heading = new Vec2(0f, -1f);

        _currentSystemId = systemId;
        // Dropped stopped and stable, right at the edge again - not the field's bare centre, which
        // is nowhere near warp range (game_design.md - two-tier map). This also means CanWarpNow is
        // already true again the instant the jump lands (still exactly WarpZoneRadius out, 0
        // speed), so a system with more than one system in range of it can be crossed straight
        // through without first having to fly clear and back.
        _shipFieldPosition = GalaxyMap.GetSystem(systemId).Field.Center + heading * GalaxyMap.WarpZoneRadius;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
    }

    private IReadOnlyList<StarSystemSummary> CreateStarSystemSummaries()
    {
        // Grows the galaxy before reporting it, not after - so a client never sees a "known
        // neighbours" count drop below the threshold for even one frame while flying around.
        GalaxyMap.EnsureGenerated(_currentSystemId, MinReachableNeighborsWhileExploring);
        return GalaxyMap.Systems
            .Select(s => new StarSystemSummary(s.Id, s.Name, s.Field.Width, s.Field.Height, s.GalaxyX, s.GalaxyY, s.ControllingFaction))
            .ToArray();
    }
}
