using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Direct user request ("уберём возможность управлять кораблём игроку... корабль на автопилоте
// летит в эту точку наиболее быстрым способом избегая препятствий, а также... при зажатом ПКМ
// возможность выбрать в какую сторону корабль должен быть наведён") - replaces manual WASD/QE
// piloting with a "phantom pilot" that computes the exact same stick values a human used to, every
// tick, instead of reading them straight off a ClientCommand. World.ShipField.cs's own
// IntegrateShipFieldMotion/StepShipFieldPhysics/SetHelmInput/EngageAutoStabilize are completely
// unchanged - every bit of existing inertia, collision, and hull-breach-on-impact behavior keeps
// working for free, since as far as that code is concerned a stick is a stick regardless of who's
// holding it.
//
// No pathfinding exists anywhere in this codebase (confirmed by a full search - NPC traffic and the
// enemy squadron both fly straight lines with zero obstacle awareness). This is a simple "seek the
// destination, steer away from whatever asteroid is actually in the way" heuristic (a classic
// steering-behavior blend, the same shape World.EnemyFleet.cs's own SegmentHitsCircle already uses
// for line-of-sight, just applied to steering instead), not honest pathfinding. It reliably threads
// a sparse field of rocks; a dense cluster or a concave trap can still catch it out - a real
// visibility-graph/A* planner would fix that, at real added complexity, left for later if this
// turns out not to be good enough in practice.
public sealed partial class World
{
    // Floor for how far ahead of the ship an asteroid is even worth worrying about, at low speed -
    // one well beyond this never factors into steering at all (a system field can hold dozens of
    // them; this keeps the per-tick cost down to the handful that could plausibly matter). At real
    // speed this floor is overridden by ComputeAvoidance's own reaction-time scaling below - found
    // live (a scratch Diagnostic() trace) that a fixed 60 units gives a ship already doing ~25
    // units/s under ShipMaxSpeed(60) barely a second of warning before it's already inside the
    // rock, nowhere near enough distance for real inertia (this session's own thrust/turn overhaul)
    // to actually redirect the ship's built-up velocity in time.
    private const float AutopilotAvoidLookaheadUnits = 60f;
    // How many seconds of travel at the ship's CURRENT speed to look ahead for a threat - scales the
    // lookahead with how fast the ship is actually moving (a coasting ship needs far less warning
    // than one already at speed), the same reasoning as the braking-distance estimate below.
    private const float AutopilotAvoidReactionSeconds = 5f;
    // Clearance to keep past an asteroid's own edge, on top of the ship's own hull radius
    // (GetHullLocalBounds's half-extents length - the same bounding-circle stand-in
    // HullOverlapsCelestialBody already uses).
    private const float AutopilotClearanceUnits = 4f;
    // How strongly a threatened path bends away from an obstacle relative to the pull toward the
    // destination - tuned so one rock in the way produces a clean detour rather than either
    // ignoring it (too small) or over-correcting wildly around it (too large).
    private const float AutopilotAvoidanceWeight = 2.2f;
    // Dead zone before the turn input snaps back to 0 - avoids a visible last-degree jitter once
    // the nose is close enough to the desired bearing.
    private const float AutopilotTurnToleranceDegrees = 2f;
    // Below this angular speed, the dead zone above is allowed to actually hold at 0 - otherwise a
    // ship that drifts into tolerance while still spinning would sit there doing nothing and coast
    // straight back out the other side (found live: a scratch Diagnostic() trace of the RMB facing-
    // override chasing a fixed bearing that oscillated wildly - 116° -> 171° -> 33° -> -29° -> ... -
    // never settling, since plain sign(error) bang-bang keeps accelerating the spin right up until
    // the moment it crosses the target, by which point it's already too fast to stop there).
    private const float AutopilotTurnSettleAngularVelocityDegreesPerSecond = 10f;
    // Safety margin on the braking-distance estimate (speed^2 / (2*decel)) - starts slowing down a
    // little earlier than the bare physics would strictly require, so it never overshoots the
    // destination before EngageAutoStabilize's own flat deceleration can catch up.
    private const float AutopilotBrakingSafetyFactor = 1.3f;
    // Close enough, and slow enough, to call it arrived - the same order of magnitude as
    // NpcShipRuntime's own NpcWaypointArriveRadius (World.NpcShips.cs), a little tighter since the
    // player's own destination click is a precise point, not a wide station/waypoint.
    private const float AutopilotArriveRadius = 6f;
    private const float AutopilotArriveSpeed = 1.5f;

    private Vec2? _autopilotDestination;
    // Once StepAutopilot decides distance is inside braking range, it commits to braking for the
    // rest of this destination rather than re-deciding fresh every tick - found live via a scratch
    // Diagnostic() trace: a fresh distance-vs-brakingDistance comparison every tick let the ship
    // settle into an endless tight orbit a couple of units off the target, because one tick's brake
    // shed enough speed to shrink brakingDistance below the (slightly overshot, due to the ship's
    // own momentum) remaining distance, flipping back to full-seek and re-accelerating toward the
    // target, which immediately re-triggered braking again next tick, forever. Reset whenever a
    // fresh destination is set (SetAutopilotDestination) or the autopilot goes idle (CancelAutopilot/
    // arrival) so the next pursuit gets its own fresh decision.
    private bool _autopilotBraking;
    // Non-null only for the tick(s) the player actually holds RMB - World.cs's own command handler
    // sets this fresh from ClientCommand.DesiredFacingDegrees every tick and clears it the instant
    // they let go, so releasing RMB always falls straight back to "face the direction of travel"
    // below on its own, with no separate "cancel the override" step needed.
    private float? _autopilotFacingOverrideDegrees;
    // Test-only: World.ShipField.cs's own DebugSetHelmInput sets the stick directly for tests that
    // only care about engine activation/collision behavior (TestRunner.Engines.cs and friends), not
    // about the autopilot itself. Without this, StepAutopilot below would stomp that value right
    // back to EngageAutoStabilize (no destination set) or its own steering (destination set) on the
    // very same tick, before StepShipFieldPhysics ever saw it - a real player can never bypass the
    // autopilot this way, but a test exercising the stick in isolation needs to. Consumed once, the
    // same tick it's set, same "one-shot" shape as every edge-triggered ClientCommand field.
    private bool _debugSkipAutopilotThisTick;

    public bool IsAutopilotActive => _autopilotDestination is not null;
    public Vec2? AutopilotDestination => _autopilotDestination;

    private AutopilotState CreateAutopilotState() =>
        new(_autopilotDestination is not null, (float?)_autopilotDestination?.X, (float?)_autopilotDestination?.Y);

    // Direct user request ("игрок сможет указать на карте точку куда корабль должен долететь") -
    // the one entry point a fresh destination click goes through. Snapped into field bounds and, if
    // it landed inside a rock, pulled back out to its surface (AsteroidShape.SurfacePoint, already
    // used elsewhere for exactly this "don't accept a point buried in a rock" reasoning) rather than
    // accepted as-is - a destination the ship can physically never reach would just leave it
    // grinding against the rock forever.
    //
    // Direct user request ("не может долететь до нужной точки") - the snap clearance here has to
    // match ComputeAvoidance's own threatRadius (asteroid.Radius + hull radius + clearance), not
    // just the bare AutopilotClearanceUnits margin used before: a destination clicked anywhere
    // between the rock's raw surface and that wider hull-aware threat radius used to snap only far
    // enough to clear the rock's OWN body, landing right inside the zone ComputeAvoidance still
    // treats as a live threat once the ship actually gets close - "seek" pulling toward the point
    // and "avoidance" pushing away from the very same rock it sits next to then fight forever,
    // orbiting just outside arrival range and never actually settling.
    public void SetAutopilotDestination(Vec2 target)
    {
        var clamped = target.Clamp(0, 0, ActiveFieldWidth, ActiveFieldHeight);
        var (_, halfExtents) = GetHullLocalBounds();
        var clearance = (float)halfExtents.Length() + AutopilotClearanceUnits;
        foreach (var asteroid in ActiveObstacles)
        {
            if (AsteroidShape.DistanceOutside(asteroid, clamped) >= clearance)
                continue;
            clamped = AsteroidShape.SurfacePoint(asteroid, clamped, clearance);
            break; // good enough - a click landing inside two overlapping rocks at once doesn't happen in practice
        }
        // Only a genuinely NEW point resets the braking latch below - re-affirming the SAME
        // destination (TestRunner.Core.cs's own SteerToward resends it every tick, inherited from
        // the old dumb-pilot convention) must not repeatedly un-commit an already-started brake,
        // which is exactly what re-zeroing this on every call used to do (found live via a scratch
        // Diagnostic() trace: the ship never actually settled, because "still chasing the same spot"
        // kept looking identical to "just got a fresh click" every single tick).
        if (_autopilotDestination is not { } current || (current - clamped).Length() > 0.01)
            _autopilotBraking = false;
        _autopilotDestination = clamped;
    }

    // The helm's own "Стоп" - cancels the destination and engages auto-stabilize once, right here,
    // rather than leaving that to StepAutopilot's own idle branch to re-assert every tick forever
    // (which would also stomp on DebugSetShipVelocity's own "auto-stabilize off, on purpose, until
    // something explicitly re-engages it" contract - World.ShipField.cs's own doc comment on it,
    // relied on by TestRunner.StationDocking.cs's ApproachBerth/DockAtStation to hold a station-
    // matched velocity across ticks with no destination active). Same one-shot shape as the
    // "arrived" branch below.
    public void CancelAutopilot()
    {
        _autopilotDestination = null;
        _autopilotBraking = false;
        EngageAutoStabilize();
    }

    // Called from World.Voyage.cs's StepVoyage, immediately before StepShipFieldPhysics - computes
    // exactly the stick values a human pilot would have held this tick and feeds them through the
    // same SetHelmInput/EngageAutoStabilize every manual command used to call directly. With no
    // destination, does nothing at all (not even re-engaging auto-stabilize) - CancelAutopilot and
    // the "arrived" branch just below already engaged it once, and it stays engaged (World.ShipField.
    // cs's own _shipAutoStabilize default) until something explicitly changes it, the same shape the
    // old manual-flight command handler always had.
    private void StepAutopilot(double deltaSeconds)
    {
        if (_debugSkipAutopilotThisTick)
        {
            _debugSkipAutopilotThisTick = false;
            return;
        }

        if (_autopilotDestination is not { } destination)
            return;

        var toTarget = destination - _shipFieldPosition;
        var distance = toTarget.Length();
        var speed = _shipVelocity.Length();

        if (distance < AutopilotArriveRadius && speed < AutopilotArriveSpeed)
        {
            _autopilotDestination = null;
            _autopilotBraking = false;
            EngageAutoStabilize();
            return;
        }

        // Already braked down to a near-stop but still short of the arrival radius - a transient
        // early trigger below (a momentary dip in speed while still far out) must not strand the
        // ship braking forever short of the destination; un-commit and resume seeking instead.
        // Found live via a scratch Diagnostic() trace: without this, the ship parked itself dead
        // stopped ~180 units short of the target and just sat there, "active" forever.
        if (_autopilotBraking && speed < AutopilotArriveSpeed)
            _autopilotBraking = false;

        // Start braking early enough that the flat EngageAutoStabilize deceleration (not a real
        // proportional brake) can still stop by the time the ship actually reaches the destination.
        // Once true, _autopilotBraking stays true until the near-stop check above releases it or
        // arrival clears it - a fresh distance-vs-brakingDistance comparison every tick oscillates
        // instead of converging (this file's own SetAutopilotDestination doc comment has the trace).
        //
        // Matches IntegrateShipFieldMotion's own real stabilize deceleration (World.ShipField.cs),
        // which IS scaled by enginePowerScale (less than full power routed to Engine really does
        // brake weaker) - unlike a first attempt at this fix, this does NOT gate the seek-and-steer
        // branch below on it: that attempt returned early whenever this scale was near zero,
        // which silently disabled the WHOLE autopilot on any hull whose real per-engine thrust
        // (ComputeEngineForces, deliberately NOT gated by this same legacy scale - this file's own
        // top-of-file reasoning) doesn't route power through the legacy Engine slider at all - real
        // gameplay hits that the instant a player clicks a destination right after undocking, before
        // ever touching the reactor slider. Floored well above zero only to avoid dividing by it.
        var enginePowerScale = Math.Min(2f, GetEffectivePower(PowerSystemId.Engine) / ShipEngineReferencePower);
        var brakingDistance = enginePowerScale > 0.01f
            ? speed * speed / (2f * ShipAutoStabilizeDecelerationPerSecond * enginePowerScale) * AutopilotBrakingSafetyFactor
            : 0f; // no real stabilize-braking capability from this scale alone - never trigger it, just keep seeking
        if (distance < brakingDistance)
            _autopilotBraking = true;
        if (_autopilotBraking)
        {
            EngageAutoStabilize();
            return;
        }

        var seek = distance > 0.01 ? toTarget.Normalized() : Vec2.Zero;
        var avoidance = ComputeAvoidance(seek);
        var blended = seek + avoidance * AutopilotAvoidanceWeight;
        var desired = blended.Length() > 0.01 ? blended.Normalized() : seek;

        // Facing: the RMB override if the player is actively holding it, otherwise the direction of
        // travel - the default "nose points where it's going" any autopilot would use on its own.
        var desiredBearingDegrees = _autopilotFacingOverrideDegrees
            ?? MathF.Atan2((float)desired.Y, (float)desired.X) * (180f / MathF.PI);
        var headingError = ShortestAngle(desiredBearingDegrees - (_shipRotationDegrees + Ship.ForwardDegrees));
        var turn = ComputeTurnInput(headingError);

        // Thrust decomposed onto the ship's OWN current axes (nose/right), not world axes - so
        // holding RMB to look somewhere else still lets the ship strafe sideways toward the
        // destination instead of only ever being able to fly nose-first (direct user request -
        // "летел боком не опрокидываясь", now genuinely possible while aiming elsewhere too).
        var noseWorld = ShipNoseDirection;
        var rightWorld = TurretMount.FromDegrees(_shipRotationDegrees + Ship.ForwardDegrees + 90f);
        var throttle = (float)(desired.X * noseWorld.X + desired.Y * noseWorld.Y);
        var strafe = (float)(desired.X * rightWorld.X + desired.Y * rightWorld.Y);

        SetHelmInput(throttle, strafe, turn);
    }

    // Steers the seek direction away from any asteroid that actually threatens the straight-line
    // path to the destination. Not honest pathfinding (this file's own top-of-file doc comment) -
    // just enough to thread a sparse field of rocks.
    private Vec2 ComputeAvoidance(Vec2 seekDirection)
    {
        var (_, halfExtents) = GetHullLocalBounds();
        var clearance = halfExtents.Length() + AutopilotClearanceUnits;
        var avoidance = Vec2.Zero;
        var lookahead = MathF.Max(AutopilotAvoidLookaheadUnits, (float)_shipVelocity.Length() * AutopilotAvoidReactionSeconds);

        foreach (var asteroid in ActiveObstacles)
        {
            var toAsteroid = asteroid.Position - _shipFieldPosition;
            var ahead = toAsteroid.X * seekDirection.X + toAsteroid.Y * seekDirection.Y;
            if (ahead <= 0 || ahead > lookahead)
                continue; // behind the ship, or too far ahead to matter yet

            // Perpendicular distance from the asteroid's own centre to the seek ray.
            var closestPoint = _shipFieldPosition + seekDirection * ahead;
            var offset = closestPoint - asteroid.Position;
            var lateralDistance = offset.Length();
            var threatRadius = asteroid.Radius + clearance;
            if (lateralDistance >= threatRadius)
                continue; // the straight path already clears it

            var pushDirection = lateralDistance > 0.01 ? offset.Normalized() : new Vec2(-seekDirection.Y, seekDirection.X);
            var strength = (threatRadius - lateralDistance) / threatRadius; // 0 at the threat's own edge, 1 dead-centre
            avoidance += pushDirection * strength;
        }

        return avoidance;
    }

    // A plain sign(error) bang-bang oscillates forever once turning feeds real angular inertia
    // (this session's own HelmTurnAngularAccelerationPerSecond rework) - it keeps accelerating the
    // spin right up until the heading crosses the target, by which point the ship is already
    // spinning too fast to stop there, and the same thing happens again on the way back the other
    // way. Same "start braking before you'd actually need to" idea StepAutopilot's own linear
    // arrival already uses (speed^2 / (2*decel)), just applied to the angular axis: once the angle
    // still needed to stop from the CURRENT spin (at HelmTurnAngularAccelerationPerSecond's own
    // deceleration capability) would overshoot the remaining error, brake instead of accelerating
    // further toward it.
    private float ComputeTurnInput(float headingErrorDegrees)
    {
        if (MathF.Abs(headingErrorDegrees) < AutopilotTurnToleranceDegrees &&
            MathF.Abs(_shipAngularVelocity) < AutopilotTurnSettleAngularVelocityDegreesPerSecond)
            return 0f;

        // Degrees still covered by the time the current spin decelerates to a stop, if it started
        // braking right now - the angular mirror of the linear braking-distance estimate above.
        var stoppingAngle = _shipAngularVelocity * _shipAngularVelocity / (2f * HelmTurnAngularAccelerationPerSecond);

        // Same sign means the current spin is headed toward closing the error - if it would carry
        // past the target before it could stop, brake (turn opposite the current spin) instead of
        // accelerating further into the overshoot.
        if (MathF.Sign(headingErrorDegrees) == MathF.Sign(_shipAngularVelocity) && stoppingAngle > MathF.Abs(headingErrorDegrees))
            return -MathF.Sign(_shipAngularVelocity);

        return MathF.Sign(headingErrorDegrees);
    }
}
