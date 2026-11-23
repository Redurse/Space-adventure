using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// A real new game begins with the ship free beside the home station, in its own base position (not turned, not mated to anything), instead of
// already docked: the station is shown when the player docks (it is turned to face the ship's airlock then, World.cs's GetOrCreateStation).
public sealed partial class World
{
    // How far from the berth the ship starts - a short, clear gap in front of the station's port (the ship's own length is on top of it).
    private const float StartFreeDistance = 45f;

    private void StartFreeBesideStation()
    {
        var berth = DockBerthPosition; // read while still docked - it is meaningless once cast off
        // The station lies on the ship's airlock side, so the ship starts on the opposite side of the berth.
        var away = (ResolveShipAirlock()?.Side ?? EdgeSide.Right) switch
        {
            EdgeSide.Left => new Vec2(1, 0),
            EdgeSide.Top => new Vec2(0, 1),
            EdgeSide.Bottom => new Vec2(0, -1),
            _ => new Vec2(-1, 0),
        };
        _dockedPointId = null;
        SetShipFieldPosition(berth + away * StartFreeDistance);
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
    }
}
