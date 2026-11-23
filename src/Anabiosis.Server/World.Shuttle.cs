using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Shuttles, as in Space Haven: a shuttle hangar bay (the "Ангар шаттла" of the ship editor) holds one small craft. A launch opens
// the bay doors - the room behind them is open to space until the shuttle is clear - and sends the shuttle out into the system, where
// it flies where the crew sends it (a point on the helm's map) or brakes to a stop, and comes home on a recall: it homes on the bay,
// which moves with the ship, and docks when it gets there slowly enough. Nobody rides it yet: it is flown by orders from the helm's
// map. While a shuttle is out the ship may not dock, land or jump (AllShuttlesHome), so none is left behind.
//
// Shuttles are not saved: a loaded game starts with every shuttle home in its bay.
public sealed partial class World
{
    // How long the doors give the room to be cleared before they open, how fast the craft is and how quickly it can change course.
    private const double ShuttleDoorWarningSeconds = 4.0;
    private const double ShuttleMaxSpeed = 36.0;
    private const double ShuttleAcceleration = 16.0;
    private const float ShuttleTurnDegreesPerSecond = 220f;
    // The kick out of the bay, the range at which a returning shuttle makes the doors open and the one past which they close behind it,
    // and what counts as home: close to the bay and slow against the ship.
    private const double ShuttleExitSpeed = 10.0;
    private const double ShuttleBayOpenRange = 70.0;
    private const double ShuttleBayCloseRange = 30.0;
    private const double ShuttleDockRadius = 9.0;
    private const double ShuttleDockRelativeSpeed = 14.0;
    // Open doors drain the room's air this fast (oxygen per second), on top of making it full vacuum for anyone not suited.
    private const float BayVentLeakPerSecond = 40f;
    // How close to the bay a crew member has to be to launch from the panel.
    private const double ShuttleOrderReach = 9.0;
    private const double ShuttleFieldEdgeMargin = 20.0;

    private readonly Dictionary<string, ShuttleRuntime> _shuttles = new();

    // True when every shuttle is back in its bay (and so also when the ship has none).
    public bool AllShuttlesHome => _shuttles.Values.All(s => s.Phase == ShuttlePhase.Docked);

    // Test and snapshot access.
    public IReadOnlyCollection<ShuttleRuntime> Shuttles => _shuttles.Values;

    private IEnumerable<DecorativeDevice> ShuttleBays => Ship.DecorativeDevices.Where(d => d.Kind == CustomDeviceKind.ShuttleHangar);

    // One shuttle for every bay: a bay put in gets its shuttle, a bay taken out loses it.
    private void SyncShuttles()
    {
        foreach (var bay in ShuttleBays)
            if (!_shuttles.ContainsKey(bay.Id))
                _shuttles[bay.Id] = new ShuttleRuntime { BayId = bay.Id };
        if (_shuttles.Count > 0)
            foreach (var gone in _shuttles.Keys.Where(id => ShuttleBays.All(b => b.Id != id)).ToList())
                _shuttles.Remove(gone);
    }

    private Vec2 BayWorldPosition(DecorativeDevice bay)
    {
        var (hullCenter, _) = GetHullLocalBounds();
        return _shipFieldPosition + RotateLocalToWorld(bay.Position - hullCenter, _shipRotationDegrees);
    }

    // The bay doors of this room are open, so the room is open to space.
    private bool IsShuttleBayOpenIn(string roomId) =>
        _shuttles.Count > 0 && ShuttleBays.Any(b => b.RoomId == roomId && _shuttles.TryGetValue(b.Id, out var s) && s.BayDoorOpen);

    private float ShuttleBayVentLeakIn(string roomId) => IsShuttleBayOpenIn(roomId) ? BayVentLeakPerSecond : 0f;

    private bool CanOrderFromBay(Character character, DecorativeDevice bay) =>
        character.IsAtHelm ||
        (!character.IsOutside && !character.OnStation && !character.OnEnemyShip && character.RoomId == bay.RoomId
            && (character.Position - bay.Position).Length() <= ShuttleOrderReach);

    private void HandleShuttleRequest(Character character, ShuttleRequest request)
    {
        SyncShuttles();
        if (!_shuttles.TryGetValue(request.BayId, out var shuttle) || ShuttleBays.FirstOrDefault(b => b.Id == request.BayId) is not { } bay)
            return;

        switch (request.Order)
        {
            case ShuttleOrder.Launch:
                if (shuttle.Phase != ShuttlePhase.Docked || !CanOrderFromBay(character, bay))
                    return;
                if (IsDocked || IsLandedOnPlanet)
                {
                    Notify("Шаттл нельзя запустить, пока корабль на приколе или на поверхности");
                    return;
                }
                if (IsInBattle)
                {
                    Notify("Шаттл нельзя запустить во время боя");
                    return;
                }
                shuttle.Phase = ShuttlePhase.Opening;
                shuttle.BayDoorOpen = true;
                shuttle.OpeningSecondsLeft = ShuttleDoorWarningSeconds;
                shuttle.Destination = null;
                Notify("Ворота ангара открываются: покиньте помещение или наденьте скафандр");
                break;

            case ShuttleOrder.Recall:
                if (shuttle.Phase == ShuttlePhase.Flying)
                {
                    shuttle.Phase = ShuttlePhase.Returning;
                    shuttle.Destination = null;
                }
                break;

            case ShuttleOrder.MoveTo:
                if (shuttle.Phase is ShuttlePhase.Flying or ShuttlePhase.Returning)
                {
                    var field = GalaxyMap.GetSystem(_currentSystemId).Field;
                    shuttle.Destination = new Vec2(request.TargetX, request.TargetY).Clamp(
                        ShuttleFieldEdgeMargin, ShuttleFieldEdgeMargin, field.Width - ShuttleFieldEdgeMargin, field.Height - ShuttleFieldEdgeMargin);
                    shuttle.Phase = ShuttlePhase.Flying;
                }
                break;

            case ShuttleOrder.Stop:
                if (shuttle.Phase is ShuttlePhase.Flying or ShuttlePhase.Returning)
                {
                    shuttle.Destination = null;
                    shuttle.Phase = ShuttlePhase.Flying;
                }
                break;
        }
    }

    private void StepShuttles(double deltaSeconds)
    {
        SyncShuttles();
        if (_shuttles.Count == 0)
            return;

        foreach (var shuttle in _shuttles.Values)
        {
            var bay = ShuttleBays.First(b => b.Id == shuttle.BayId);
            var bayWorld = BayWorldPosition(bay);

            switch (shuttle.Phase)
            {
                case ShuttlePhase.Docked:
                    shuttle.Position = bayWorld;
                    shuttle.Velocity = _shipVelocity;
                    shuttle.BayDoorOpen = false;
                    break;

                case ShuttlePhase.Opening:
                    shuttle.Position = bayWorld;
                    shuttle.Velocity = _shipVelocity;
                    // A fight or a berth that begins in the meantime cancels the launch: the doors shut again.
                    if (IsInBattle || IsDocked || IsLandedOnPlanet)
                    {
                        shuttle.Phase = ShuttlePhase.Docked;
                        shuttle.BayDoorOpen = false;
                        break;
                    }
                    shuttle.OpeningSecondsLeft -= deltaSeconds;
                    if (shuttle.OpeningSecondsLeft <= 0)
                        LaunchShuttle(shuttle, bayWorld);
                    break;

                default:
                    FlyShuttle(shuttle, bayWorld, deltaSeconds);
                    break;
            }
        }
    }

    // Out of the bay, away from the middle of the ship, with the ship's own speed and a kick on top.
    private void LaunchShuttle(ShuttleRuntime shuttle, Vec2 bayWorld)
    {
        var outward = (bayWorld - _shipFieldPosition).Normalized();
        if (outward == Vec2.Zero)
            outward = ShipNoseDirection;
        shuttle.Velocity = _shipVelocity + outward * ShuttleExitSpeed;
        shuttle.RotationDegrees = (float)(Math.Atan2(outward.Y, outward.X) * 180.0 / Math.PI);
        // Out to a point clear of the bay first, so the doors can close behind it; after that it waits for orders.
        shuttle.Destination = bayWorld + outward * (ShuttleBayCloseRange + 15.0);
        shuttle.Phase = ShuttlePhase.Flying;
    }

    private void FlyShuttle(ShuttleRuntime shuttle, Vec2 bayWorld, double deltaSeconds)
    {
        var homing = shuttle.Phase == ShuttlePhase.Returning;
        // Homing is measured against the ship, which is itself moving; a free destination is a fixed point.
        var frame = homing ? _shipVelocity : Vec2.Zero;
        var desired = Vec2.Zero;
        Vec2? target = homing ? bayWorld : shuttle.Destination;
        if (target is { } point)
        {
            var toTarget = point - shuttle.Position;
            var distance = toTarget.Length();
            if (!homing && distance < 3.0)
            {
                shuttle.Destination = null;
            }
            else
            {
                // Arrive: the wanted speed falls with the distance, so it comes in slowly instead of overshooting.
                desired = frame + toTarget.Normalized() * Math.Min(ShuttleMaxSpeed, distance * 0.9);
            }
        }

        var change = desired - shuttle.Velocity;
        var step = ShuttleAcceleration * deltaSeconds;
        var velocity = shuttle.Velocity + (change.Length() > step ? change.Normalized() * step : change);
        var cap = ShuttleMaxSpeed + _shipVelocity.Length();
        if (velocity.Length() > cap)
            velocity = velocity.Normalized() * cap;
        shuttle.Velocity = velocity;

        var next = shuttle.Position + velocity * deltaSeconds;
        var field = GalaxyMap.GetSystem(_currentSystemId).Field;
        next = next.Clamp(ShuttleFieldEdgeMargin, ShuttleFieldEdgeMargin, field.Width - ShuttleFieldEdgeMargin, field.Height - ShuttleFieldEdgeMargin);
        // A rock stops it dead where it is - unless it is already inside one (it came out of a bay that sits against it), when it may
        // move on and so find its way out.
        if (ActiveObstacles.Any(o => AsteroidShape.Contains(o, next)) && !ActiveObstacles.Any(o => AsteroidShape.Contains(o, shuttle.Position)))
            shuttle.Velocity = Vec2.Zero;
        else
            shuttle.Position = next;

        // Nose along the way it is going (relative to the frame it is steering in), turning at a limited rate.
        var heading = shuttle.Velocity - frame;
        if (heading.Length() > 2.0)
        {
            var wanted = (float)(Math.Atan2(heading.Y, heading.X) * 180.0 / Math.PI);
            var error = ((wanted - shuttle.RotationDegrees + 540f) % 360f) - 180f;
            var turn = ShuttleTurnDegreesPerSecond * (float)deltaSeconds;
            shuttle.RotationDegrees += Math.Clamp(error, -turn, turn);
        }

        var distanceToBay = (shuttle.Position - bayWorld).Length();
        if (homing)
        {
            shuttle.BayDoorOpen = distanceToBay < ShuttleBayOpenRange;
            if (distanceToBay <= ShuttleDockRadius && (shuttle.Velocity - _shipVelocity).Length() <= ShuttleDockRelativeSpeed)
            {
                shuttle.Phase = ShuttlePhase.Docked;
                shuttle.Position = bayWorld;
                shuttle.Velocity = _shipVelocity;
                shuttle.Destination = null;
                shuttle.BayDoorOpen = false;
                Notify("Шаттл вернулся в ангар");
            }
        }
        else if (shuttle.BayDoorOpen && distanceToBay > ShuttleBayCloseRange)
        {
            shuttle.BayDoorOpen = false; // the doors close behind it
        }
    }

    private IReadOnlyList<ShuttleState>? CreateShuttleStates() =>
        _shuttles.Count == 0
            ? null
            : _shuttles.Values.Select(s => new ShuttleState(s.BayId, s.Phase, s.Position.X, s.Position.Y, s.RotationDegrees,
                (float)s.Velocity.X, (float)s.Velocity.Y, s.Hp, s.BayDoorOpen, s.Destination is not null,
                s.Destination?.X ?? 0.0, s.Destination?.Y ?? 0.0, (float)Math.Max(0.0, s.OpeningSecondsLeft))).ToList();
}
