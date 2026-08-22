using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

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
public sealed partial class World
{
    // Calibrated so a straight run across a 300x300 system (AsteroidField.CreateDefault's own
    // size) takes about a minute (game_design.md - two-tier map, "за минуту он долетал от одного
    // края системы к другому") - the same cruise speed applies whether a human is flying or the
    // autopilot is (World.Voyage.cs's StepTraveling no longer runs the unmanned case on a faster
    // clock than a manned one).
    // RCS mode (M41) - today's original free-rotation flight, unchanged: turning spins the bow in
    // place at a constant rate regardless of speed, useful for precision work (docking, lining up
    // a shot) where the bow has to point somewhere the ship isn't actually travelling.
    private const float ShipMaxSpeed = 5f;
    private const float ShipThrustAccelerationPerSecond = 4f;
    private const float ShipRotationDegreesPerSecond = 90f;
    // Arc mode (M41, the default) - turning banks the nose at a rate tied to current speed instead
    // (IntegrateShipFieldMotion), the way a real vessel carrying real momentum comes about: standing
    // still, the bow doesn't swing at all. Faster top speed and acceleration than RCS to make it
    // the more capable mode for actually getting somewhere, trading away the ability to pivot in
    // place - that's what Z (Rcs) is for.
    private const float ArcMaxSpeed = 9f;
    private const float ArcThrustAccelerationPerSecond = 6.5f;
    // Yaw rate scales linearly with current speed (below), which makes the turn radius at full
    // throttle a fixed ArcMaxSpeed/(rate in rad/s) regardless of how fast that actually is - at the
    // original 50deg/s that worked out to only ~10 units, tighter than the hull itself, so a U-turn
    // finished within half of one lap around its own nose instead of reading as a real banked
    // arc. 15deg/s widens that same-speed radius to ~34 units instead (game_design.md/M47 -
    // "нужно чтобы это было реалистичнее").
    private const float ArcMaxYawRateDegreesPerSecond = 15f;
    private const float ShipAutoStabilizeDecelerationPerSecond = 6f;
    private const float ShipEngineReferencePower = 10f; // same order of magnitude as the "10 power ~= 1 breach" oxygen constant
    // Backing up runs the manoeuvring thrusters, not the main engines - astern is for easing off a
    // berth or out of a rock, not for flying anywhere.
    private const float ShipReverseThrustFraction = 0.45f;

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
    private Vec2 _shipFieldPosition;
    private Vec2 _shipVelocity = Vec2.Zero;
    private Vec2 _shipThrust = Vec2.Zero; // world-space, derived from throttle along the nose - what the exhaust is drawn from
    private float _shipRotationDegrees;
    private bool _shipAutoStabilize = true;
    private float _helmThrottle;
    private float _helmTurn;
    public ShipControlMode ControlMode { get; private set; } = ShipControlMode.Arc;

    private void ToggleControlMode() =>
        ControlMode = ControlMode == ShipControlMode.Arc ? ShipControlMode.Rcs : ShipControlMode.Arc;

    // Where the bow points in world terms, which is the ship's own forward axis turned by its
    // current heading (Ship.ForwardDegrees).
    private Vec2 ShipNoseDirection => TurretMount.FromDegrees(_shipRotationDegrees + Ship.ForwardDegrees);

    private void SetHelmInput(float throttle, float turn)
    {
        _helmThrottle = Math.Clamp(throttle, -1f, 1f);
        _helmTurn = Math.Clamp(turn, -1f, 1f);
        // Only the engines cancel a stabilise: swinging the bow while the ship brakes itself is a
        // perfectly reasonable thing to want, and killing the brake for it would be a surprise.
        if (_helmThrottle != 0f)
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
        // airlock bolted to particular sides of the hull, pointing it is the whole job). RCS turns
        // it at a flat rate regardless of speed - can pivot standing still. Arc (the default, M41)
        // ties the rate to current speed instead, zero at a standstill - a real vessel's own
        // momentum resisting a spin in place - which is what actually reads as "banking a turn"
        // rather than "spinning the whole hull".
        if (ControlMode == ShipControlMode.Arc)
        {
            var speedFraction = Math.Min(1f, _shipVelocity.Length() / ArcMaxSpeed);
            _shipRotationDegrees += _helmTurn * ArcMaxYawRateDegreesPerSecond * speedFraction * dt;
        }
        else
        {
            _shipRotationDegrees += _helmTurn * ShipRotationDegreesPerSecond * dt;
        }

        var throttle = _helmThrottle < 0f ? _helmThrottle * ShipReverseThrustFraction : _helmThrottle;
        _shipThrust = ShipNoseDirection * throttle;

        var maxSpeed = ControlMode == ShipControlMode.Arc ? ArcMaxSpeed : ShipMaxSpeed;
        var thrustAccelerationPerSecond = ControlMode == ShipControlMode.Arc ? ArcThrustAccelerationPerSecond : ShipThrustAccelerationPerSecond;

        if (_shipAutoStabilize)
        {
            var decel = ShipAutoStabilizeDecelerationPerSecond * enginePowerScale * dt;
            var speed = _shipVelocity.Length();
            _shipVelocity = speed <= decel ? Vec2.Zero : _shipVelocity - _shipVelocity.Normalized() * decel;
        }
        else
        {
            _shipVelocity += _shipThrust * thrustAccelerationPerSecond * enginePowerScale * dt;
            if (_shipVelocity.Length() > maxSpeed)
                _shipVelocity = _shipVelocity.Normalized() * maxSpeed;
        }

        return _shipFieldPosition + _shipVelocity * dt;
    }

    // Every physical hazard the field can hold applies at once now (M39) - there's no separate
    // "mode" where only asteroids matter or only a station's hull does, since the ship can be near
    // any combination of them simultaneously.
    private void StepShipFieldPhysics(double deltaSeconds)
    {
        var candidatePosition = IntegrateShipFieldMotion(deltaSeconds)
            .Clamp(0, 0, AsteroidField.Width, AsteroidField.Height);

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
                _shipFieldPosition = slidPosition;

            // One breach per impact, not one per tick spent in contact - grinding along a rock
            // used to open a new hole thirty times a second.
            if (_hullContactCooldown <= 0)
            {
                BreachNearestWallBlock(localContactPoint);
                _hullContactCooldown = HullContactCooldownSeconds;
            }
            return;
        }

        // Another ship is not a thing you drive through. Ramming stops both hulls dead rather than
        // holing them: the enemy's plating is a match for yours, and a fight that can be won by
        // steering into the other ship isn't one worth having.
        if (HullOverlapsEnemy(candidatePosition))
        {
            _shipVelocity = Vec2.Zero;
            return;
        }

        // The station's own compartments are solid too, whichever one happens to be nearest right
        // now (World.Voyage.cs's UpdateNearestStation) - shoulder into them and the ship stops dead
        // rather than passing through (its hull is sturdier than a lone asteroid, and docking is a
        // deliberate button press, not a drift-in). Suppressed entirely for the single casting-off
        // event (_justCastOffStation, set by Undock()) rather than re-derived from live geometry
        // every tick: the instant after undocking the ship IS still mated to the berth (by
        // construction), and a pilot lines up a heading before ever building any speed - turning
        // in place, with zero net displacement, still moves the hull's own corners
        // (HullTouchesStation is corner-based), which can flip "touching" back to true at
        // whatever rotation the pilot happens to settle on first. Re-deriving "already touching"
        // from that same live check would then treat the settled heading as a fresh, blockable
        // approach and wedge the ship at the berth forever, unable to ever thrust clear - the very
        // trap this exemption exists to avoid. A course that curves back through the same
        // structure later is still correctly blocked once the flag has cleared (the first tick the
        // hull actually reads clear) - this only ever forgives the one casting-off event, not the
        // structure as a whole.
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

        _shipFieldPosition = candidatePosition;
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
        MathF.Abs(travelled.X) > 0.0001f ? _shipVelocity.X : 0f,
        MathF.Abs(travelled.Y) > 0.0001f ? _shipVelocity.Y : 0f);

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

        foreach (var asteroid in AsteroidField.Asteroids)
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
            (best, bestDistance) = (clamped, distance);
        }
        return best;
    }

    private void BreachNearestWallBlock(Vec2 localContactPoint)
    {
        var nearest = Ship.WallBlocks.OrderBy(b => (b.Position - localContactPoint).Length()).FirstOrDefault();
        if (nearest is not null)
            DamageWallBlock(nearest.Id, WallBlockMaxHp);
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
        _shipFieldPosition = position.Clamp(0, 0, AsteroidField.Width, AsteroidField.Height);
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
    }
}
