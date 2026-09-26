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
    // Direct user request ("как в cosmoteer... как замедлиться") - a classic Reynolds "Arrive"
    // steering behavior replaces the old discrete seek-then-brake state machine below: desired SPEED
    // (not just direction) ramps linearly from ShipMaxSpeed down to 0 as distance shrinks inside this
    // radius, instead of switching abruptly between "full seek" and "full EngageAutoStabilize brake"
    // at a computed braking-distance threshold. That discrete switch needed its own sticky
    // _autopilotBraking latch plus a from-scratch un-stick rule to avoid oscillating in an endless
    // orbit or freezing short of the target (both found live via scratch Diagnostic() traces) -
    // continuous speed control never enters either failure mode, since "how hard to brake" is
    // recomputed smoothly every tick from the actual velocity error rather than a one-time decision.
    private const float AutopilotSlowingRadius = 130f;
    // How much velocity error (units/s) saturates the throttle/strafe stick to full - below this,
    // the stick scales down proportionally instead of slamming to ±1, which is what actually makes
    // the final approach smooth rather than bang-bang. Reached whenever the ship is already close to
    // its own desired velocity (cruising well, or nearly arrived) - a small fraction of ShipMaxSpeed.
    private const float AutopilotVelocityErrorSaturation = 15f;
    // Close enough, and slow enough, to call it arrived - the same order of magnitude as
    // NpcShipRuntime's own NpcWaypointArriveRadius (World.NpcShips.cs), a little tighter since the
    // player's own destination click is a precise point, not a wide station/waypoint.
    private const float AutopilotArriveRadius = 6f;
    private const float AutopilotArriveSpeed = 1.5f;
    // Above this speed, a hull with real strafe/reverse capability (hasRealEngines below) trusts its
    // own current velocity direction for the default facing rather than the raw bearing to target -
    // found live via a scratch Diagnostic() trace: right at AutopilotArriveSpeed(1.5) the ship's own
    // small residual settling wobble near arrival was still enough to swing the nose wildly; a wider
    // margin only matters for how much of that last-second wobble leaks into facing, not for the
    // main cruise/brake phase (which stays well above this either way).
    private const float AutopilotVelocityHeadingThreshold = 8f;
    // Direct user request ("около конца маршрута призрак начинает поворачиваться, а так ведь быть
    // не должно") - throttleVector's own bearing (the facing target for a hull with no real
    // strafe/reverse capability) gets genuinely noisy right near arrival: the velocity-error vector
    // it's derived from is small there, so tiny per-tick fluctuations in the ship's own residual
    // drift swing its DIRECTION disproportionately, even though its magnitude barely matters any
    // more. Found live via a scratch Diagnostic() trace on an off-axis destination (not perfectly
    // aligned with the ship's own start heading, closer to a real map click): the real ship's own
    // RotationDegrees oscillated ~13° for several seconds near arrival, chasing that noise with
    // HelmTurnAngularAccelerationPerSecond's own strong angular response. This exponential smoothing
    // factor (how much of the raw-vs-smoothed gap closes each tick) turns that into a calmer, damped
    // signal ComputeTurnInput can actually settle against, without touching that bang-bang's own
    // already-tuned logic.
    private const float AutopilotFacingSmoothingPerTick = 0.12f;

    private Vec2? _autopilotDestination;
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
    // Last desiredBearingDegrees StepAutopilot actually steered toward - relayed to the client via
    // CreateAutopilotState() so the destination ghost's predicted facing matches reality instead of
    // a separately-guessed (and near-arrival, unstable) approximation. Stays at whatever it last
    // was while idle (no destination) - harmless, since AutopilotState.IsActive gates whether the
    // client draws the ghost at all.
    private float? _autopilotLastPredictedFacingDegrees;
    // Exponentially-smoothed throttleVector bearing (no-real-engines facing target only) - see
    // AutopilotFacingSmoothingPerTick's own doc comment. Reset whenever a fresh destination is set
    // so a new pursuit doesn't inherit a stale smoothed heading from the last one.
    private float? _autopilotSmoothedNoEnginesBearing;
    // Direct user bug report ("призрак поворачивается вместе с кораблём... конечный поворот
    // меняется, а не должен") - a hasRealEngines hull's own default (non-RMB-override) facing target,
    // committed ONCE (the first tick after a fresh destination, or the instant RMB lets go) instead
    // of recomputed every tick from the live travel/velocity direction, both of which genuinely
    // change over the course of a real flight. Only ever set/read for a hull with real strafe -
    // see StepAutopilot's own doc comment on why a no-real-engines hull keeps its existing adaptive
    // behavior instead. Reset alongside _autopilotSmoothedNoEnginesBearing above.
    private float? _autopilotCommittedFacingDegrees;

    public bool IsAutopilotActive => _autopilotDestination is not null;
    public Vec2? AutopilotDestination => _autopilotDestination;

    private AutopilotState CreateAutopilotState() =>
        new(_autopilotDestination is not null, (float?)_autopilotDestination?.X, (float?)_autopilotDestination?.Y, _autopilotLastPredictedFacingDegrees);

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
        // Only a genuinely NEW point resets the smoothed facing below - re-affirming the SAME
        // destination (TestRunner.Core.cs's own SteerToward resends it every tick, inherited from
        // the old dumb-pilot convention) must not repeatedly wipe out smoothing that's already
        // converging, which would defeat the whole point of it.
        if (_autopilotDestination is not { } current || (current - clamped).Length() > 0.01)
        {
            _autopilotSmoothedNoEnginesBearing = null;
            _autopilotCommittedFacingDegrees = null;
        }
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
            EngageAutoStabilize();
            return;
        }

        var seek = distance > 0.01 ? toTarget.Normalized() : Vec2.Zero;
        var avoidance = ComputeAvoidance(seek);
        var blended = seek + avoidance * AutopilotAvoidanceWeight;
        var travelDirection = blended.Length() > 0.01 ? blended.Normalized() : seek;

        // Arrive steering (Reynolds) - desired SPEED ramps down linearly with distance instead of a
        // discrete seek/brake switch, so slowing down falls out of the same velocity-error feedback
        // every tick recomputes, rather than a one-time "start braking now" decision (this file's own
        // top-of-file doc comment has the full reasoning for dropping that older approach).
        var desiredSpeed = MathF.Min(ShipMaxSpeed, (float)distance / AutopilotSlowingRadius * ShipMaxSpeed);
        var desiredVelocity = travelDirection * desiredSpeed;
        var steering = desiredVelocity - _shipVelocity;
        var steeringMagnitude = steering.Length();
        var throttleVector = steeringMagnitude > AutopilotVelocityErrorSaturation
            ? steering.Normalized()
            : steering * (1f / AutopilotVelocityErrorSaturation);

        // Facing: the RMB override if the player is actively holding it. Otherwise it depends on
        // whether this hull can actually realize a sideways correction: a hull with real Ship.Engines
        // fixtures (built via the ship editor) answers to strafe as genuine force from its own
        // side-facing engines (World.Engines.cs's ComputeEngineForces/EffectiveControl), so the nose
        // can stay pointed along the direction of travel while strafe does the correcting - direct
        // user request ("летел боком не опрокидываясь"). A hull with none (every hand-authored hull,
        // World.Engines.cs's own top-of-file doc comment: "entirely unaffected" by that model) has
        // no real force behind strafe at all - only the legacy throttle-only thrust term responds -
        // so any sideways component of the needed correction that strafe would carry is silently
        // lost whenever the nose ISN'T already aligned with it. Found live via a scratch
        // Diagnostic() trace on exactly this hull: aiming the nose at travelDirection while thrust
        // chased the (usually different) velocity-error direction spiralled the ship away from the
        // destination in a widening arc, since the unrealized strafe component never actually
        // cancelled anything. Aligning the nose with throttleVector itself instead makes throttle
        // alone carry the FULL correction (strafe becomes ~0 as the turn catches up), which is
        // exactly correct for a hull that can't strafe for real - it does mean turning to face
        // whichever way it needs to brake/correct, same as a Cosmoteer ship built with only
        // forward-facing thrusters would have to.
        // travelDirection itself is unstable right at/near the destination - "bearing to target"
        // flips a full 180° the instant the ship's own overshoot carries it past the point, which
        // spun the nose right through the arrival phase even on a hull that could otherwise brake
        // without turning at all (found live via a scratch Diagnostic() trace on the omnidirectional
        // fixture below). The ship's own CURRENT VELOCITY direction never flips discontinuously like
        // that - it just shrinks smoothly toward zero under Arrive steering - so it's the stabler
        // reference once actually moving; only falls back to travelDirection from a near-standstill,
        // where velocity direction is itself noise (speed too small to mean anything).
        var hasRealEngines = Ship.Engines.Count > 0;
        var headingReferenceDirection = hasRealEngines && speed > AutopilotVelocityHeadingThreshold
            ? _shipVelocity.Normalized()
            : travelDirection;

        // Direct user request ("зачем корабль разворачивается... если он может просто сдать назад
        // двигателями без разворота") - a hull with no real strafe still doesn't need to swing all
        // the way around just because throttleVector happens to point behind it: reverse throttle
        // from the CURRENT nose realizes the exact same force (throttle*nose, just negative) as
        // turning 180° and pushing forward would, for a fraction of the rotation. Whichever of
        // throttleVector's own bearing or its 180°-flip is CLOSER to the nose's current heading is
        // the one actually chased below - once nose converges to either, decomposing throttleVector
        // onto it still drives strafe to ~0 and throttle to the full (now correctly signed)
        // magnitude, so this loses nothing the "always face throttleVector directly" version had.
        var throttleBearing = MathF.Atan2((float)throttleVector.Y, (float)throttleVector.X) * (180f / MathF.PI);
        var currentNoseBearing = _shipRotationDegrees + Ship.ForwardDegrees;
        var rawNoEnginesBearing = MathF.Abs(ShortestAngle(throttleBearing - currentNoseBearing)) > 90f
            ? throttleBearing + 180f
            : throttleBearing;

        // Exponential smoothing (AutopilotFacingSmoothingPerTick's own doc comment) - throttleVector
        // is genuinely noisy near arrival (a small velocity-error vector's DIRECTION swings a lot
        // for very little real magnitude), which otherwise fed straight into the turn controller and
        // made the ship visibly wobble in place for several seconds. ShortestAngle's own wraparound
        // handling, not a naive lerp, so this closes across the 180°/-180° seam correctly.
        var smoothingStep = ShortestAngle(rawNoEnginesBearing - (_autopilotSmoothedNoEnginesBearing ?? rawNoEnginesBearing)) * AutopilotFacingSmoothingPerTick;
        var noEnginesBearing = (_autopilotSmoothedNoEnginesBearing ?? rawNoEnginesBearing) + smoothingStep;
        _autopilotSmoothedNoEnginesBearing = noEnginesBearing;

        // Direct user bug report (screenshots - the destination ghost's own promised heading
        // visibly drifted between two points of the same trip; confirmed via a direct clarifying
        // question that this project's own ships all carry real engines) - a hasRealEngines hull
        // commits to ONE facing target instead of recomputing it every tick from the live travel/
        // velocity direction, which genuinely changes as the ship accelerates, brakes, and steers
        // around obstacles over the course of a real flight. Safe specifically because this hull
        // type has real strafe force: holding a fixed nose direction never costs it the ability to
        // still correct sideways drift, unlike a no-real-engines hull (kept on its existing adaptive
        // noEnginesBearing below, unchanged - see AutopilotFacingSmoothingPerTick's own doc comment
        // for why THAT hull type's nose has to keep actively tracking the live correction direction).
        float desiredBearingDegrees;
        if (_autopilotFacingOverrideDegrees is { } overrideDegrees)
        {
            desiredBearingDegrees = overrideDegrees;
            if (hasRealEngines)
                _autopilotCommittedFacingDegrees = overrideDegrees; // releasing RMB commits whatever was last aimed
        }
        else if (hasRealEngines)
        {
            _autopilotCommittedFacingDegrees ??=
                MathF.Atan2((float)headingReferenceDirection.Y, (float)headingReferenceDirection.X) * (180f / MathF.PI);
            desiredBearingDegrees = _autopilotCommittedFacingDegrees.Value;
        }
        else
        {
            desiredBearingDegrees = noEnginesBearing;
        }
        _autopilotLastPredictedFacingDegrees = desiredBearingDegrees;
        var headingError = ShortestAngle(desiredBearingDegrees - (_shipRotationDegrees + Ship.ForwardDegrees));
        var turn = ComputeTurnInput(headingError);

        // Thrust decomposed onto the ship's OWN current axes (nose/right), not world axes - so
        // holding RMB to look somewhere else still lets the ship strafe sideways toward the
        // destination instead of only ever being able to fly nose-first (direct user request -
        // "летел боком не опрокидываясь", now genuinely possible while aiming elsewhere too).
        var noseWorld = ShipNoseDirection;
        var rightWorld = TurretMount.FromDegrees(_shipRotationDegrees + Ship.ForwardDegrees + 90f);
        var throttle = (float)(throttleVector.X * noseWorld.X + throttleVector.Y * noseWorld.Y);
        var strafe = (float)(throttleVector.X * rightWorld.X + throttleVector.Y * rightWorld.Y);

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
