using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Manual ship piloting inside an AsteroidField (game_design.md Phase 3, M15): a Barotrauma-style
// joystick at the helm sets a persistent thrust vector, the ship accelerates toward it with real
// inertia, and turns to face wherever it's actually heading. Both accelerating and auto-stabilizing
// draw on the Engine system's effective power - lose that, and the ship can only coast on whatever
// momentum it already had, which is exactly the risk of losing engine wiring/power mid-field.
//
// Position/rotation live in the same local coordinate frame every Room/WallBlock already uses (the
// ship's own layout space), just reinterpreted as "where the whole ship currently is inside the
// AsteroidField" instead of a fixed origin - so a collision's contact point needs no extra
// transform to find the nearest WallBlock to breach.
//
// M59 - "убрать орбитальную механику, вернуть статичную карту в духе Cosmoteer": no gravity, no
// on-rails Kepler coasting, no cruise mode - pure inertia and thrust, the same shape this project's
// own pre-M50 flight already had.
//
// Direct user request ("сделай чтобы сквозь планеты можно был прлетать без последствий") - a
// celestial body is no longer a physical obstacle at all: the ship flies straight through one with
// zero effect on its motion, same as it always could through empty space. HullOverlapsCelestialBody
// (World.PlanetLanding.cs) still detects "sitting on a landable body's surface" purely to arm the
// landing button - that's the only thing a body's position still affects server-side.
public sealed partial class World
{
    // Calibrated so a straight run across a small system field takes on the order of seconds to a
    // minute, not hours (M59 follow-up - back to this project's own pre-M50 scale, game_design.md's
    // two-tier map "за минуту он долетал от одного края системы к другому").
    // Renamed from ShipThrustAccelerationPerSecond (M58 - ship mass): this is a fixed FORCE, not an
    // acceleration - IntegrateShipFieldMotion divides by ShipCatalog.Mass(CurrentShipKind) to get
    // F=ma. Frigate's own mass is exactly 1.0 (ShipCatalog.cs), so this number is unchanged from
    // before mass existed - Frigate's feel is preserved exactly, every other hull now accelerates
    // faster/slower by 1/mass.
    // Direct user request ("хочу чтобы режим рсу был всегда включен... полностью удали кнопку") -
    // this used to be the "Rcs" mode's own thrust constant, offered alongside a faster "Arc" mode
    // (banked turning tied to speed, no pivoting in place) toggled by a button/Z key. That toggle
    // and ShipControlMode are gone entirely now - this flat rate is the only one, always on.
    private const float ShipThrustForcePerSecond = 16f;
    // Direct user request ("хочу сделать чтобы гасила" - Q/E should actively fight an unwanted spin,
    // not just add an independent rate on top of it) - the helm's own turn input now feeds
    // _shipAngularVelocity, the SAME accumulator Ship.Engines' own torque already does
    // (EngineTorqueToAngularAcceleration below), instead of setting _shipRotationDegrees directly.
    // Holding the opposite turn direction from an existing spin now genuinely cancels it, the same
    // way a real RCS thruster fighting an unwanted rotation would - previously (ShipRotationDegreesPerSecond,
    // a flat degrees-PER-SECOND rate set directly on release) it just stacked a second, independent
    // rotation on top and left whatever spin the engines had induced completely untouched once
    // released. This is now a genuine degrees-per-second-SQUARED acceleration, same unit family as
    // EngineTorqueToAngularAcceleration's own contribution, not a rate - a real, if hand-tuned
    // (ShipCatalog.Mass's own doc comment's "no real moment-of-inertia model" reasoning), momentum.
    private const float HelmTurnAngularAccelerationPerSecond = 220f;
    private const float ShipAutoStabilizeDecelerationPerSecond = 6f;
    // Direct user request ("тяга зависимая от расположения движков") - converts World.Engines.cs's
    // own ComputeEngineForces NetTorque into an angular ACCELERATION (added to _shipAngularVelocity,
    // not directly to rotation - real inertia, same reasoning ShipCatalog.Mass's own doc comment
    // gives for why the ship's mass is a flat tunable constant rather than a computed one: there's
    // no real per-hull moment-of-inertia model to derive this from, so it's one hand-picked number
    // instead, exactly like mass already is).
    private const float EngineTorqueToAngularAcceleration = 0.6f;
    // The rotational mirror of ShipAutoStabilizeDecelerationPerSecond above - only ever has anything
    // to damp on a hull with Ship.Engines fixtures (a hull with none never accumulates angular
    // velocity in the first place, see ComputeEngineForces' own doc comment), so this is silently a
    // no-op for every hand-authored/pre-existing custom ship.
    private const float ShipAutoStabilizeAngularDecelerationPerSecond = 45f;
    private const float ShipEngineReferencePower = 10f; // same order of magnitude as the "10 power ~= 1 breach" oxygen constant
    // Backing up runs the manoeuvring thrusters, not the main engines - astern is for easing off a
    // berth or out of a rock, not for flying anywhere.
    private const float ShipReverseThrustFraction = 0.45f;

    // M59 - "убрать орбитальную механику": replaces the M50-era dynamic, distance-to-nearest-body
    // speed cap (World.Gravity.cs's own DynamicMaxSpeed, deleted along with the rest of the gravity
    // model) - a flat top speed again, the same shape this project's own pre-M50 flight used, since
    // there's no gravity well left to reason a dynamic cap around.
    private const float ShipMaxSpeed = 60f;
    // Safety cap on the NEW torque-driven angular velocity (World.Engines.cs's ComputeEngineForces) -
    // the linear-velocity mirror already has ShipMaxSpeed above; a badly-balanced hull whose engines
    // never get corrected (stabilize never pressed) should still top out somewhere sane rather than
    // spin up without bound.
    private const float ShipMaxAngularVelocityDegreesPerSecond = 360f;

    // M55 - landed on a planet's own small (PlanetSurface.Width/Height) local field, a much tighter
    // scale than the system field's own ShipMaxSpeed above. A flat, walking-speed-adjacent cap -
    // crossing the whole 300-unit field at this speed takes ~15s, plenty for looking around without
    // making the field feel enormous.
    private const float SurfaceMaxSpeed = 20f;

    private const float HullContactCooldownSeconds = 1.5f;

    private float _hullContactCooldown;
    // Set by Undock(), cleared the first tick the hull actually reads clear of the station - see
    // StepShipFieldPhysics's own comment on why a live geometry re-check at each tick isn't enough
    // on its own: turning in place, before any net displacement, moves the hull's own corners
    // (HullTouchesStation is corner-based) and can flip "touching" back to true at the very same
    // position that just read as clear, at whatever rotation the pilot happens to line up on
    // first. A live re-check would then treat that as a fresh, blockable approach - straight back
    // into the exact trap this exemption exists to avoid - instead of the single casting-off
    // event it actually still is.
    private bool _justCastOffStation;
    // M54 - "точность позиции корабля на большом масштабе": kept as a plain double Vec2 (the shared
    // rescale to double precision) so repeated velocity*dt accumulation never loses precision even
    // over a long flight - no separate accumulator needed.
    private Vec2 _shipFieldPosition;
    private Vec2 _shipVelocity = Vec2.Zero;
    private Vec2 _shipThrust = Vec2.Zero; // world-space, derived from throttle along the nose - what the exhaust is drawn from
    private float _shipRotationDegrees;
    // Direct user request ("тяга зависимая от расположения движков") - real rotational inertia from
    // Ship.Engines' own net torque (World.Engines.cs's ComputeEngineForces), on top of the existing
    // flat _helmTurn-driven rate below. Stays exactly 0 forever for any hull with no Ship.Engines
    // fixtures (nothing ever adds to it), so this is a pure addition, never a behavior change, for
    // every hand-authored/pre-existing custom ship.
    private float _shipAngularVelocity;
    private bool _shipAutoStabilize = true;
    private float _helmThrottle;
    // Direct user request ("как в Cosmoteer... включались двигатели которые смотрят в
    // противоположную сторону от того куда я хочу") - sideways along the hull's own beam,
    // read by World.Engines.cs's MarchingRawControl the same way _helmThrottle already is.
    // Only the new per-engine model reacts to this; the older flat/legacy thrust bonus
    // (ShipSystemDevice.ThrustBonus) stays exactly what it was, nose-direction-only.
    private float _helmStrafe;
    private float _helmTurn;

    // The one place any code should assign _shipFieldPosition (a genuine reposition:
    // docking/undocking, warp arrival, DebugPlaceShip, edge nudges).
    private void SetShipFieldPosition(Vec2 value)
    {
        _shipFieldPosition = value;
    }

    // Where the bow points in world terms, which is the ship's own forward axis turned by its
    // current heading (Ship.ForwardDegrees).
    private Vec2 ShipNoseDirection => TurretMount.FromDegrees(_shipRotationDegrees + Ship.ForwardDegrees);

    // Same axis, but in the hull's own LOCAL frame (no current heading added) - what
    // World.Engines.cs's MarchingRawControl compares each engine's own Facing against, since
    // Ship.Engines positions/facings are authored in that same local frame, not world space.
    // Whichever way THIS hull's own nose was authored to face (Ship.ForwardDegrees), not a
    // hardcoded cardinal direction - a custom hull's nose can point any of the 4 ways.
    private Vec2 ShipLocalForward => TurretMount.FromDegrees(Ship.ForwardDegrees);
    // The hull's own starboard (right) side, 90 degrees clockwise from the nose in this project's
    // own screen/world convention (TurretMount.FromDegrees: 0 degrees = local +X, 90 = local +Y,
    // i.e. clockwise in the Y-down frame every Room/WallBlock already lives in).
    private Vec2 ShipLocalRight => TurretMount.FromDegrees(Ship.ForwardDegrees + 90f);

    // Combat damage (World.EnemyAi.cs's ApplyEnemyAttack, enemy/weapon overhaul - "штурвал... можно
    // было сломать") - a wrecked helm answers to nobody until repaired (World.SystemRepair.cs):
    // World.cs's own IsAtHelm block skips SetHelmInput/EngageAutoStabilize entirely while this is
    // true, freezing whatever thrust/turn was last commanded exactly like a pilot letting go, and
    // World.Interact.cs refuses to seat anyone new at it.
    public bool HelmConsoleBroken { get; set; }

    private void SetHelmInput(float throttle, float strafe, float turn)
    {
        _helmThrottle = Math.Clamp(throttle, -1f, 1f);
        _helmStrafe = Math.Clamp(strafe, -1f, 1f);
        _helmTurn = Math.Clamp(turn, -1f, 1f);
        // Direct user request ("хочу сделать чтобы гасила") - turn now feeds real angular inertia
        // (HelmTurnAngularAccelerationPerSecond's own doc comment) exactly like throttle/strafe feed
        // linear inertia, so it has to cancel the brake the same way they already do - while auto-
        // stabilize stays engaged, IntegrateShipFieldMotion's own decay branch runs instead of the
        // one that actually applies _helmTurn, and turning would silently do nothing at all from a
        // standing start (the most common case) if this didn't also take the stick back for it.
        if (_helmThrottle != 0f || _helmStrafe != 0f || _helmTurn != 0f)
            _shipAutoStabilize = false;
    }

    private void EngageAutoStabilize()
    {
        _helmThrottle = 0f;
        _shipThrust = Vec2.Zero;
        _shipAutoStabilize = true;
    }

    // Thrust/drag/turn integration for the ship's own manual flight (World.Voyage.cs's StepVoyage) -
    // only the "what happens on arrival at candidatePosition" part differs by hazard (breach a wall
    // block, ram an enemy hull, bump a station's plating), so that part stays in
    // StepShipFieldPhysics below instead of being duplicated here.
    private Vec2 IntegrateShipFieldMotion(double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        _hullContactCooldown = Math.Max(0f, _hullContactCooldown - dt);
        var enginePowerScale = Math.Min(2f, GetEffectivePower(PowerSystemId.Engine) / ShipEngineReferencePower);

        // Heading is steered, not inferred - the pilot always points the bow on purpose, never has
        // it swing round to face wherever the ship happens to be drifting (with the guns and the
        // airlock bolted to particular sides of the hull, pointing it is the whole job). Direct user
        // request ("хочу чтобы режим рсу был всегда включен... полностью удали кнопку") - can pivot
        // standing still; this used to also offer an "Arc" mode that banked the turn rate to current
        // speed instead (zero at a standstill), toggled with a button/Z key, removed entirely along
        // with ShipControlMode. Content-каталог отсеков - a built RCS room's own TurnBonus (World.
        // ShipBuilding.cs's DevicesForCatalogEntry) flat-adds to the turn's own angular acceleration
        // below (HelmTurnAngularAccelerationPerSecond's own doc comment), same "a stronger thruster"
        // reasoning ThrustBonus already has for straight-line thrust. Zero for every hand-authored
        // hull's own Engine devices, so an unmodified hull's own turn strength is unchanged.
        var turnBonus = Ship.SystemDevices.Where(d => d.System == PowerSystemId.Engine).Sum(d => d.TurnBonus);

        var throttle = _helmThrottle < 0f ? _helmThrottle * ShipReverseThrustFraction : _helmThrottle;
        _shipThrust = ShipNoseDirection * throttle;

        // Content-каталог отсеков - a built marching-engine room's own ThrustBonus flat-adds to the
        // base force before the mass division below, same zero-change-for-hand-authored-hulls shape.
        var thrustBonus = Ship.SystemDevices.Where(d => d.System == PowerSystemId.Engine).Sum(d => d.ThrustBonus);
        var thrustForcePerSecond = ShipThrustForcePerSecond + thrustBonus;
        var mass = ShipCatalog.Mass(CurrentShipKind);
        var thrustAccelerationPerSecond = thrustForcePerSecond / mass;
        var decelerationPerSecond = ShipAutoStabilizeDecelerationPerSecond * enginePowerScale;

        // Direct user request ("тяга зависимая от расположения движков") - real per-engine force/
        // torque (World.Engines.cs's ComputeEngineForces' own doc comment on why it replaced the old
        // flat TotalEngineThrust()/TotalEngineTurn() bonuses above). Pivot is the same stand-in "hull
        // centre" turret/camera code already uses (GetHullLocalBounds) - no real centre-of-mass model
        // exists to compute a better one from. Zero for any hull with no Ship.Engines fixtures.
        var (hullCenter, _) = GetHullLocalBounds();
        var (engineForce, engineTorque) = ComputeEngineForces(hullCenter);

        if (_shipAutoStabilize)
        {
            var decel = decelerationPerSecond * dt;
            var speed = _shipVelocity.Length();
            _shipVelocity = speed <= decel ? Vec2.Zero : _shipVelocity - _shipVelocity.Normalized() * decel;

            // The rotational mirror of the linear decay just above - kills off whatever spin is left
            // (from the helm's own turn input or from Ship.Engines' own torque, now the same
            // accumulator - HelmTurnAngularAccelerationPerSecond's own doc comment) while nobody is
            // actively fighting it. Not scaled by enginePowerScale, same reasoning as engineForce
            // above - the angular velocity this damps was never gated by the legacy Engine
            // subsystem's power in the first place, so damping it with that same gate would leave a
            // hull with no legacy Engine device spinning forever even with stabilize held down.
            var angularDecel = ShipAutoStabilizeAngularDecelerationPerSecond * dt;
            _shipAngularVelocity = Math.Abs(_shipAngularVelocity) <= angularDecel
                ? 0f
                : _shipAngularVelocity - Math.Sign(_shipAngularVelocity) * angularDecel;
        }
        else
        {
            var maxSpeed = _landedBodyId is not null ? SurfaceMaxSpeed : ShipMaxSpeed;
            _shipVelocity += _shipThrust * thrustAccelerationPerSecond * enginePowerScale * dt;
            // Not scaled by enginePowerScale, unlike the flat bonus above - that scale comes from
            // GetEffectivePower(PowerSystemId.Engine), which only counts the OLD flat-bonus
            // CustomDeviceKind.Engine system-devices (WireGraphFactory never gives Ship.Engines'
            // own fixtures a power pin at all - World.Engines.cs's own top-of-file doc comment: "no
            // weapon/collision damages these tiles yet", a deliberately self-contained first pass).
            // Gating the new per-engine force on that would make it silently dead on any hull built
            // purely from the new fixtures (no legacy Engine device at all) - MaxThrust/EffectiveControl
            // and each engine's own intact/broken parts (ComputeEngineForces skips a broken Nozzle)
            // are already this model's own equivalent of "is this engine actually able to push".
            _shipVelocity += RotateLocalToWorld(engineForce, _shipRotationDegrees) * (1f / mass) * dt;
            if (_shipVelocity.Length() > maxSpeed)
                _shipVelocity = _shipVelocity.Normalized() * maxSpeed;

            // Direct user request ("хочу сделать чтобы гасила") - the helm's own turn input and
            // Ship.Engines' own torque both feed the SAME angular-velocity accumulator now
            // (HelmTurnAngularAccelerationPerSecond's own doc comment) - holding the opposite turn
            // direction from an existing spin actively cancels it instead of just adding an
            // independent rate on top that leaves the underlying spin untouched once released.
            _shipAngularVelocity += (_helmTurn * (HelmTurnAngularAccelerationPerSecond + turnBonus) + engineTorque * EngineTorqueToAngularAcceleration) * dt;
            _shipAngularVelocity = Math.Clamp(_shipAngularVelocity,
                -ShipMaxAngularVelocityDegreesPerSecond, ShipMaxAngularVelocityDegreesPerSecond);
        }

        // The one place _shipAngularVelocity (from the helm's own turn input and/or Ship.Engines'
        // own torque, both above) actually turns the hull - always integrated regardless of
        // auto-stabilize, same as the linear _shipVelocity -> _shipFieldPosition integration below.
        _shipRotationDegrees += _shipAngularVelocity * dt;

        // Position accumulates directly in double now that Vec2 itself is double - deltaSeconds
        // (not the already-narrowed dt above), so a real, long flight never compounds float
        // rounding error into its own position.
        return _shipFieldPosition + _shipVelocity * deltaSeconds;
    }

    // Every physical hazard the field can hold applies at once now (M39) - there's no separate
    // "mode" where only asteroids matter or only a station's hull does, since the ship can be near
    // any combination of them simultaneously.
    private void StepShipFieldPhysics(double deltaSeconds)
    {
        var candidatePosition = IntegrateShipFieldMotion(deltaSeconds).Clamp(0.0, 0.0, ActiveFieldWidth, ActiveFieldHeight);

        if (TryFindHullCollision(candidatePosition, _shipRotationDegrees, out var localContactPoint))
        {
            // Refusing the whole step is what used to wedge the ship against a rock: pressed
            // against one, every direction with any component into it was thrown away too, so
            // there was nothing left to steer out with. Each axis is tried on its own instead, the
            // way the crew's own movement works inside - the ship slides along the rock rather
            // than sticking to it.
            var slid = SlideAlongObstacle(candidatePosition);
            _shipVelocity = slid is null ? Vec2.Zero : ProjectVelocityOnto(slid.Value - _shipFieldPosition);
            if (slid is { } slidPosition)
                SetShipFieldPosition(slidPosition);

            // One breach per impact, not one per tick spent in contact - grinding along a rock
            // used to open a new hole thirty times a second.
            if (_hullContactCooldown <= 0)
            {
                BreachNearestWallBlock(localContactPoint);
                _hullContactCooldown = HullContactCooldownSeconds;
            }
            return;
        }

        // None of what follows exists on a planet's own surface field (M55) - no other ships, no
        // stations, and "the body" is the ground the ship is already sitting on, not a hazard still
        // out ahead of it. TryFindHullCollision above (against ActiveObstacles, this body's own
        // rocks while landed) is the only physical hazard a landed ship still needs to dodge.
        if (_landedBodyId is null)
        {
            // Another ship is not a thing you drive through. Ramming stops both hulls dead rather
            // than holing them: the enemy's plating is a match for yours, and a fight that can be
            // won by steering into the other ship isn't one worth having.
            if (HullOverlapsEnemy(candidatePosition))
            {
                _shipVelocity = Vec2.Zero;
                return;
            }

            // Direct user request ("сделай чтобы сквозь планеты можно был прлетать без
            // последствий") - a planet/moon/star used to be solid (M53 follow-up) and stopped the
            // ship dead on contact; that's gone entirely now. HullOverlapsCelestialBody (still
            // used below by World.PlanetLanding.cs's CanLandNow) still detects "the ship happens
            // to be sitting on a landable body's surface right now" to arm the landing button -
            // it just no longer has any say in whether the ship can keep moving through/past one.

            // The station's own compartments are solid too, whichever one happens to be nearest
            // right now (World.Voyage.cs's UpdateNearestStation) - shoulder into them and the ship
            // stops dead rather than passing through (its hull is sturdier than a lone asteroid, and
            // docking is a deliberate button press, not a drift-in). Suppressed entirely for the
            // single casting-off event (_justCastOffStation, set by Undock()) rather than re-derived
            // from live geometry every tick: the instant after undocking the ship IS still mated to
            // the berth (by construction), and a pilot lines up a heading before ever building any
            // speed - turning in place, with zero net displacement, still moves the hull's own
            // corners (HullTouchesStation is corner-based), which can flip "touching" back to true
            // at whatever rotation the pilot happens to settle on first. Re-deriving "already
            // touching" from that same live check would then treat the settled heading as a fresh,
            // blockable approach and wedge the ship at the berth forever, unable to ever thrust
            // clear - the very trap this exemption exists to avoid. A course that curves back
            // through the same structure later is still correctly blocked once the flag has cleared
            // (the first tick the hull actually reads clear) - this only ever forgives the one
            // casting-off event, not the structure as a whole.
            var touchingStationNow = HullTouchesStation(_shipFieldPosition);
            if (_justCastOffStation)
            {
                if (!touchingStationNow)
                    _justCastOffStation = false;
            }
            else if (!touchingStationNow && HullTouchesStation(candidatePosition))
            {
                _shipVelocity = Vec2.Zero;
                return;
            }
        }

        _shipFieldPosition = candidatePosition;
    }

    // A plain hull-bounding-circle test (the hull's own half-extents diagonal) - still used by
    // World.PlanetLanding.cs's CanLandNow/TryLandOnPlanet to detect "sitting on a landable body's
    // surface right now" and arm the landing button. No longer used to block movement at all
    // (direct user request, "сквозь планеты можно был прлетать без последствий" - World.ShipField.cs's
    // own top-of-file doc comment) - a body is purely a landing target now, never a physical
    // obstacle, so there's no swept/tunnelling concern left to guard against here either.
    private CelestialBody? HullOverlapsCelestialBody(Vec2 candidateCenter)
    {
        var (_, halfExtents) = GetHullLocalBounds();
        var hullRadius = halfExtents.Length();
        var system = GalaxyMap.GetSystem(_currentSystemId);
        foreach (var body in system.Bodies)
        {
            var bodyPosition = CelestialBodyGenerator.PositionAt(body, system.BodiesById) + system.Field.Center;
            if ((bodyPosition - candidateCenter).Length() < body.Radius + hullRadius)
                return body;
        }
        return null;
    }

    // Whichever single axis of the blocked step is clear, if either is - X first, then Y.
    private Vec2? SlideAlongObstacle(Vec2 blockedCandidate)
    {
        var alongX = new Vec2(blockedCandidate.X, _shipFieldPosition.Y);
        if (!TryFindHullCollision(alongX, _shipRotationDegrees, out _))
            return alongX;

        var alongY = new Vec2(_shipFieldPosition.X, blockedCandidate.Y);
        return TryFindHullCollision(alongY, _shipRotationDegrees, out _) ? null : alongY;
    }

    // Keep only the part of the velocity that survived the slide, so the ship doesn't carry
    // momentum into a wall it isn't moving through any more.
    private Vec2 ProjectVelocityOnto(Vec2 travelled) => new(
        Math.Abs(travelled.X) > 0.0001f ? _shipVelocity.X : 0f,
        Math.Abs(travelled.Y) > 0.0001f ? _shipVelocity.Y : 0f);

    private static float RotateToward(float current, float target, float maxDelta)
    {
        var diff = ((target - current) % 360f + 540f) % 360f - 180f; // shortest signed angle in (-180, 180]
        return MathF.Abs(diff) <= maxDelta ? target : current + MathF.Sign(diff) * maxDelta;
    }

    // The hull's bounding box in ship-local (WallBlock) coordinates - the starter ship is a fixed
    // straight row of rooms, so this is just their combined extent.
    private (Vec2 Center, Vec2 HalfExtents) GetHullLocalBounds()
    {
        var minX = Ship.Rooms.Min(r => r.Left);
        var maxX = Ship.Rooms.Max(r => r.Right);
        var minY = Ship.Rooms.Min(r => r.Top);
        var maxY = Ship.Rooms.Max(r => r.Bottom);
        return (new Vec2((minX + maxX) / 2, (minY + maxY) / 2), new Vec2((maxX - minX) / 2, (maxY - minY) / 2));
    }

    // Hull vs every asteroid, both as the shapes they're drawn as: the hull as the union of its
    // compartments (HullSilhouette) rather than one big box, and the rock as its own jagged
    // outline (AsteroidShape) rather than the circle it used to be approximated by. Rotating the
    // rock into the hull's unrotated frame keeps the whole test in the layout's own coordinates,
    // so localContactPoint comes back ready for BreachNearestWallBlock.
    private bool TryFindHullCollision(Vec2 candidateWorldCenter, float rotationDegrees, out Vec2 localContactPoint)
    {
        var (localCenter, _) = GetHullLocalBounds();
        var radians = rotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);

        foreach (var asteroid in ActiveObstacles)
        {
            var worldOffset = asteroid.Position - candidateWorldCenter;
            // Inverse-rotate (world -> hull-local) since local-to-world would be [cos,-sin; sin,cos].
            var localOffset = new Vec2(worldOffset.X * cos + worldOffset.Y * sin, -worldOffset.X * sin + worldOffset.Y * cos);
            var rockInLayout = localCenter + localOffset;

            // Nearest bit of plating to the rock's centre, then the rock's own reach along that
            // bearing - a spur sticking toward the hull hits, a notch facing it doesn't.
            var contact = NearestHullPoint(rockInLayout);
            var toContact = contact - rockInLayout;
            var reach = toContact.Length() < 0.0001f
                ? asteroid.Radius
                : AsteroidShape.RadiusAt(asteroid, toContact);

            if (toContact.Length() < reach)
            {
                localContactPoint = contact;
                return true;
            }
        }

        localContactPoint = Vec2.Zero;
        return false;
    }

    private Vec2 NearestHullPoint(Vec2 layoutPoint)
    {
        var best = layoutPoint;
        var bestDistance = float.MaxValue;
        foreach (var room in Ship.Rooms)
        {
            var clamped = new Vec2(
                Math.Clamp(layoutPoint.X, room.Left, room.Right),
                Math.Clamp(layoutPoint.Y, room.Top, room.Bottom));
            var distance = (clamped - layoutPoint).Length();
            if (distance >= bestDistance)
                continue;
            (best, bestDistance) = (clamped, (float)distance);
        }
        return best;
    }

    private void BreachNearestWallBlock(Vec2 localContactPoint)
    {
        var nearest = Ship.WallBlocks.OrderBy(b => (b.Position - localContactPoint).Length()).FirstOrDefault();
        if (nearest is not null)
            DamageWallBlock(nearest.Id, MaxHpFor(nearest));
    }

    // Test-only convenience - never called by real gameplay code, no client command reaches it.
    // Instantly relocates the ship as if a perfect pilot had already arrived, stopped dead,
    // skipping the actual flight for setup that isn't itself about piloting. Most of the test
    // suite needs "the ship is docked at X" or "a fight with Y has started" purely as scaffolding
    // for something else entirely (a faction/quest/trade/combat mechanic) - simulating a real,
    // straight-line-pilot flight across a system now scattered with several hostile sectors and
    // multiple stations' own solid hulls (M39/M40) turned out to need actual obstacle-avoidance to
    // do reliably, which is a real feature in its own right, not a side effect of any single
    // milestone here. The handful of tests that ARE about piloting itself (TestRunner.HelmAndHull.cs,
    // TestRunner.Voyage.cs's own manual-flight tests) still fly for real and never call this.
    public void DebugPlaceShip(Vec2 position)
    {
        SetShipFieldPosition(position.Clamp(0, 0, ActiveFieldWidth, ActiveFieldHeight));
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
    }

    // Test-only convenience, same convention as DebugPlaceShip right above - sets a specific
    // velocity directly and turns auto-stabilize off so it actually persists (auto-stabilize, on by
    // default, decelerates toward absolute rest every tick regardless of what this just set).
    public void DebugSetShipVelocity(Vec2 velocity)
    {
        _shipVelocity = velocity;
        _shipAutoStabilize = false;
    }

    // Direct user request ("уберём возможность управлять кораблём игроку... автопилот") - the helm
    // stick (_helmThrottle/_helmStrafe/_helmTurn) is now driven exclusively by World.Autopilot.cs's
    // own StepAutopilot rather than directly from a ClientCommand, so tests that only care about
    // engine activation (which engine fires for a given stick position, TestRunner.Engines.cs/
    // TestRunner.EngineThrustVectors.cs) need a way to set the stick directly, the same test-only
    // "skip the real path, set the state directly" convention DebugPlaceShip/DebugSetShipVelocity
    // already use. Also arms _debugSkipAutopilotThisTick (World.Autopilot.cs) for the very next
    // Step - otherwise StepAutopilot would overwrite this same value before physics ever saw it.
    public void DebugSetHelmInput(float throttle, float strafe, float turn)
    {
        SetHelmInput(throttle, strafe, turn);
        _debugSkipAutopilotThisTick = true;
    }
}
