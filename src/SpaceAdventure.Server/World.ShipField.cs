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
//
// M59 - "убрать орбитальную механику, вернуть статичную карту в духе Cosmoteer": no gravity, no
// on-rails Kepler coasting, no cruise mode - pure inertia and thrust, the same shape this project's
// own pre-M50 flight already had. Every celestial body is a fixed, physical obstacle (still worth
// flying around, still solid to collide with) but exerts no pull of its own any more.
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
    private const float ShipThrustForcePerSecond = 16f;
    private const float ShipRotationDegreesPerSecond = 90f;
    // Arc mode (M41, the default) - turning banks the nose at a rate tied to current speed instead
    // (IntegrateShipFieldMotion), the way a real vessel carrying real momentum comes about: standing
    // still, the bow doesn't swing at all. Faster acceleration than RCS to make it the more capable
    // mode for actually getting somewhere, trading away the ability to pivot in place - that's what
    // Z (Rcs) is for.
    private const float ArcThrustForcePerSecond = 26f;
    // Yaw rate scales linearly with current speed (below), which makes the turn radius at full
    // throttle a fixed ArcYawReferenceSpeed/(rate in rad/s) regardless of how fast that actually is -
    // widened at M47 (from an original 50deg/s that made a U-turn tighter than the hull itself) to
    // read as a real banked arc rather than "spinning the whole hull" (game_design.md/M47 -
    // "нужно чтобы это было реалистичнее").
    private const float ArcMaxYawRateDegreesPerSecond = 15f;
    // Normalizes the Arc-mode yaw rate against "how much of the ship's own physical capability is
    // currently being used" - full bank rate at or above this speed, scaling down toward zero at a
    // standstill. Set relative to ShipMaxSpeed above (M59 follow-up - used to be calibrated against
    // the old dynamic near-body speed cap before gravity was removed).
    private const float ArcYawReferenceSpeed = 30f;
    private const float ShipAutoStabilizeDecelerationPerSecond = 6f;
    private const float ShipEngineReferencePower = 10f; // same order of magnitude as the "10 power ~= 1 breach" oxygen constant
    // Backing up runs the manoeuvring thrusters, not the main engines - astern is for easing off a
    // berth or out of a rock, not for flying anywhere.
    private const float ShipReverseThrustFraction = 0.45f;

    // M59 - "убрать орбитальную механику": replaces the M50-era dynamic, distance-to-nearest-body
    // speed cap (World.Gravity.cs's own DynamicMaxSpeed, deleted along with the rest of the gravity
    // model) - a flat top speed again, the same shape this project's own pre-M50 flight used, since
    // there's no gravity well left to reason a dynamic cap around.
    private const float ShipMaxSpeed = 60f;

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
    private bool _shipAutoStabilize = true;
    private float _helmThrottle;
    private float _helmTurn;
    public ShipControlMode ControlMode { get; private set; } = ShipControlMode.Arc;

    private void ToggleControlMode() =>
        ControlMode = ControlMode == ShipControlMode.Arc ? ShipControlMode.Rcs : ShipControlMode.Arc;

    // M57 - the captain tab's "Флип" button: a single instant 180° turn for a flip-and-burn
    // maneuver (accelerate nose-first, flip, decelerate tail-first) - a deliberate one-press pilot
    // action, not an autopilot that reorients gradually over several seconds.
    private void FlipHeading() => _shipRotationDegrees = (_shipRotationDegrees + 180f) % 360f;

    // The one place any code should assign _shipFieldPosition (a genuine reposition:
    // docking/undocking, warp arrival, DebugPlaceShip, edge nudges).
    private void SetShipFieldPosition(Vec2 value)
    {
        _shipFieldPosition = value;
    }

    // Where the bow points in world terms, which is the ship's own forward axis turned by its
    // current heading (Ship.ForwardDegrees).
    private Vec2 ShipNoseDirection => TurretMount.FromDegrees(_shipRotationDegrees + Ship.ForwardDegrees);

    // Combat damage (World.EnemyAi.cs's ApplyEnemyAttack, enemy/weapon overhaul - "штурвал... можно
    // было сломать") - a wrecked helm answers to nobody until repaired (World.SystemRepair.cs):
    // World.cs's own IsAtHelm block skips SetHelmInput/EngageAutoStabilize entirely while this is
    // true, freezing whatever thrust/turn was last commanded exactly like a pilot letting go, and
    // World.Interact.cs refuses to seat anyone new at it.
    public bool HelmConsoleBroken { get; set; }

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
        // Content-каталог отсеков - a built RCS room's own TurnBonus (World.ShipBuilding.cs's
        // DevicesForCatalogEntry) flat-adds to whichever base yaw rate the current control mode uses,
        // same "usable in either mode" reasoning the plan settled on rather than real lateral thrust.
        // Zero for every hand-authored hull's own Engine devices, so an unmodified hull turns exactly
        // as before.
        var turnBonus = Ship.SystemDevices.Where(d => d.System == PowerSystemId.Engine).Sum(d => d.TurnBonus);
        if (ControlMode == ShipControlMode.Arc)
        {
            // Normalized against a fixed reference speed (ArcYawReferenceSpeed), not the flat max
            // speed cap below - the yaw rate should read as "how much of the ship's own physical
            // capability is currently being used". Floors at 1.0 past the reference speed rather
            // than growing further - there's still SOME maximum bank rate even at extreme velocity,
            // just not zero.
            var speedFraction = (float)Math.Min(1f, _shipVelocity.Length() / ArcYawReferenceSpeed);
            _shipRotationDegrees += _helmTurn * (ArcMaxYawRateDegreesPerSecond + turnBonus) * speedFraction * dt;
        }
        else
        {
            _shipRotationDegrees += _helmTurn * (ShipRotationDegreesPerSecond + turnBonus) * dt;
        }

        var throttle = _helmThrottle < 0f ? _helmThrottle * ShipReverseThrustFraction : _helmThrottle;
        _shipThrust = ShipNoseDirection * throttle;

        // Content-каталог отсеков - a built marching-engine room's own ThrustBonus flat-adds to the
        // base force before the mass division below, same zero-change-for-hand-authored-hulls shape.
        var thrustBonus = Ship.SystemDevices.Where(d => d.System == PowerSystemId.Engine).Sum(d => d.ThrustBonus);
        var thrustForcePerSecond = (ControlMode == ShipControlMode.Arc ? ArcThrustForcePerSecond : ShipThrustForcePerSecond) + thrustBonus;
        var thrustAccelerationPerSecond = thrustForcePerSecond / ShipCatalog.Mass(CurrentShipKind);
        var decelerationPerSecond = ShipAutoStabilizeDecelerationPerSecond * enginePowerScale;

        if (_shipAutoStabilize)
        {
            var decel = decelerationPerSecond * dt;
            var speed = _shipVelocity.Length();
            _shipVelocity = speed <= decel ? Vec2.Zero : _shipVelocity - _shipVelocity.Normalized() * decel;
        }
        else
        {
            var maxSpeed = _landedBodyId is not null ? SurfaceMaxSpeed : ShipMaxSpeed;
            _shipVelocity += _shipThrust * thrustAccelerationPerSecond * enginePowerScale * dt;
            if (_shipVelocity.Length() > maxSpeed)
                _shipVelocity = _shipVelocity.Normalized() * maxSpeed;
        }

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

            // A planet/moon/star is solid too (M53 follow-up - "почему я смог войти в планету"):
            // nothing here ever checked it as a physical obstacle the way asteroids/stations/enemies
            // already are. Same stop-dead response as ramming an enemy - no wall-block-breach
            // mechanic to reuse here (a body has no interior). Swept across the whole tick's travel,
            // not just tested at the final candidate position: a fast-moving ship could otherwise
            // cross a small body entirely between one tick's position and the next and never
            // register as touched by a point-only test.
            if (SweptOverlapsCelestialBody(_shipFieldPosition, candidatePosition) is { } touchedBody)
            {
                // Snapped to sit exactly at the contact threshold (HullOverlapsCelestialBody's own
                // radius+hullRadius circle) along the ship's CURRENT bearing from the body, rather
                // than left at the tick's unchanged starting position: with velocity always reset
                // to zero on a blocked step, a deterministic physics tick would otherwise recompute
                // and reject the exact same tiny candidate forever, never actually converging into
                // "touching" - which M55's CanLandNow (the same threshold) needs to ever go true.
                var system = GalaxyMap.GetSystem(_currentSystemId);
                var bodyPosition = CelestialBodyGenerator.PositionAt(touchedBody, system.BodiesById) + system.Field.Center;
                var (_, contactHalfExtents) = GetHullLocalBounds();
                var contactRadius = touchedBody.Radius + contactHalfExtents.Length();
                var awayFromBody = (_shipFieldPosition - bodyPosition).Normalized();
                if (awayFromBody == Vec2.Zero)
                    awayFromBody = new Vec2(1f, 0f);
                SetShipFieldPosition(bodyPosition + awayFromBody * (contactRadius - 0.01f));
                _shipVelocity = Vec2.Zero;
                return;
            }

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

    // M55 follow-up - a plain hull-bounding-circle test (the hull's own half-extents diagonal, same
    // as SweptOverlapsCelestialBody below) rather than the exact rotated hull box M53 originally
    // used. Deliberately the SAME threshold the swept movement check and its own resting-position
    // snap use: two different thresholds for "touching" here could leave the ship able to satisfy
    // one but not the other, stuck forever just outside whichever is stricter. Returns the body
    // actually touched rather than a bare bool (M55 - World.PlanetLanding.cs's CanLandNow needs to
    // know WHICH one, to tell a landable rocky world/moon apart from a gas giant/star that should
    // still just stop the ship dead without ever arming a landing button).
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

    // M55 follow-up - the swept pre-check StepShipFieldPhysics actually calls now: HullOverlaps
    // CelestialBody above only ever asks "is the hull touching a body AT this one exact point",
    // which tunnels clean through a small body whenever a single tick's travel carries the
    // candidate position past the whole body in one step. Checked against the closest point on the
    // SEGMENT actually travelled this tick instead, with the hull approximated as a circle (its own
    // half-extents' diagonal) rather than the exact rotated box the resting-position test above
    // still uses - a conservative stand-in that's cheap to sweep and only ever needs to answer "did
    // this straight line cross the body at all".
    private CelestialBody? SweptOverlapsCelestialBody(Vec2 from, Vec2 to)
    {
        var (_, halfExtents) = GetHullLocalBounds();
        var hullRadius = halfExtents.Length();
        var system = GalaxyMap.GetSystem(_currentSystemId);
        var segment = to - from;
        var segmentLengthSq = segment.X * segment.X + segment.Y * segment.Y;

        foreach (var body in system.Bodies)
        {
            var bodyPosition = CelestialBodyGenerator.PositionAt(body, system.BodiesById) + system.Field.Center;
            Vec2 closest;
            if (segmentLengthSq < 0.0001f)
            {
                closest = from;
            }
            else
            {
                var toBody = bodyPosition - from;
                var t = Math.Clamp((toBody.X * segment.X + toBody.Y * segment.Y) / segmentLengthSq, 0f, 1f);
                closest = from + segment * t;
            }
            if ((bodyPosition - closest).Length() < body.Radius + hullRadius)
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
}
