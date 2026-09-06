using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Fixed shot-selection order for a raider's gun (enemy/weapon overhaul - the user's own order:
// "вначале двигатели, потом орудия, потом реактор, потом мостик, потом система кислорода").
// World.EnemyFleet.cs's EnemyPriorityOrder walks this list top to bottom every time a raider needs
// a new target; EnemyShipRuntime.TargetPriority is what makes it sticky in between.
public enum EnemyTargetPriority
{
    Engines,
    Weapons,
    Reactor,
    Bridge,
    Oxygen,
}

// Hostile sectors as a real fight in real space (game_design.md sections 2/11/12) rather than an
// HP bar with a damage timer. The whole squadron defending the sector is in the field at once,
// each hull chasing the player's ship, holding a firing distance and shooting shells that have to
// physically arrive (World.Projectiles.cs). The consequences of a hit are unchanged - shield
// first, then a turret, a severed wire or a hull breach (World.EnemyAi.cs) - only now they're
// caused by something that crossed the gap and could have missed.
public sealed partial class World
{
    private const float EnemyMaxHp = 100f;
    // Standoff is measured from the player's hull *centre* and has to clear its half-length (the
    // starter hull is ~26 units nose to tail), or raiders would try to hold station inside the
    // ship they're shooting at.
    // Bumped up a bit ("враги держались чуть подальше от корабля при стрельбе") - keeps the same
    // margin under EnemyWeaponRangeUnits as before so raiders still settle within their own range
    // once on station.
    private const float EnemyStandoffDistance = 27f; // where a raider prefers to sit and shoot from
    private const float EnemyFormationAngleSpacingDegrees = 6f; // angular gap between wingmen on the same orbit
    private const float EnemyMaxSpeed = 3.4f;        // deliberately under the player's 8: you can outrun them
    private const float EnemyAccelerationPerSecond = 2.2f;
    private const float EnemyTurnDegreesPerSecond = 120f;
    private const float EnemyWeaponRangeUnits = 31f; // just outside their standoff: they shoot once settled
    // Per ship, so a squadron still hits harder than a lone raider without three of them turning
    // the hull into scrap faster than a crew can weld it.
    private const float EnemyFireIntervalSeconds = 7f;
    // Magnetic-armed raiders shoot noticeably more often, matching the cannon's own "стреляет
    // реально быстро" flavor (TurretBalance) even though enemies don't track a magazine.
    private const float EnemyMagneticFireIntervalSeconds = 2.5f;
    private const int EnemyMachineGunPelletsPerBurst = 4;
    private const float EnemyMachineGunSpreadDegrees = 6f;
    private const float EnemyOpeningDelaySeconds = 4f; // a beat to close in before the first volley
    private const float EnemySpawnDistance = 38f;    // far enough that the fight opens with an approach
    public const float EnemyHullRadius = 3.5f;       // what a shell has to hit, and what its own shots clear
    // Weaving evasion (game_design.md enemy overhaul - "не стояли на одном месте а пытались
    // уворачиваться от снарядов игрока"): a smooth extra swing on top of the steady orbit below,
    // active for as long as the player is shooting at all (StepEnemyFleet's own isPlayerFiring
    // check) and taking priority over holding a perfect firing position.
    private const float EnemyDodgeAngleAmplitudeDegrees = 12f;
    private const float EnemyDodgeFrequencyHz = 0.5f;
    // Continuous orbiting (game_design.md enemy overhaul - "летали вокруг корабля и пытались
    // маневрировать"): a raider never actually parks, it keeps circling the ship at its standoff
    // distance - which, as a side effect, is also what keeps its firing angle drifting over time
    // (a blocked shot down one line doesn't mean a permanently blocked target, since the orbit
    // carries it to a different line soon enough). The angular speed itself dips (not stops) near
    // whichever side actually lines up with the current priority target (ApproachAxisSignFor), so
    // it lingers there a bit longer for firing opportunities without ever fully parking.
    private const float EnemyOrbitDegreesPerSecond = 11f; // ~33s for a full lap at neutral speed
    private const float EnemyOrbitPullStrength = 0.55f;   // how much slower/faster near vs away from the ideal side

    private float _enemyDodgeClock;

    private readonly List<EnemyShipRuntime> _enemyShips = new();
    // Stands in outside a battle, so Enemy is never null for the HP readout or a test that pokes
    // at it between fights.
    private readonly EnemyShip _idleEnemy = new(EnemyMaxHp);

    // "The enemy" for everything that needs exactly one: the HP bar, boarding, the crew. It's the
    // first hull still flying - kill it and the next of the squadron takes its place, which is the
    // same progression the sector always had, just with the others already on the board.
    public EnemyShip Enemy => BoardableEnemy?.Ship ?? _enemyShips.LastOrDefault()?.Ship ?? _idleEnemy;

    private EnemyShipRuntime? BoardableEnemy => _enemyShips.FirstOrDefault(e => e.Alive);

    private void SpawnEnemySquadron(int count)
    {
        _enemyShips.Clear();
        for (var i = 0; i < count; i++)
        {
            // Off the stern quarter, strung out in a line so they arrive one after another rather
            // than as a wall. _shipFieldPosition is the hull's centre in field space; +X is the
            // side the guns and the airlock are on (TurretMount), so the fight happens where the
            // ship can answer it and where a boarding party can reach it.
            var position = _shipFieldPosition + new Vec2(EnemySpawnDistance + i * 6f, 0f);
            var layout = EnemyClassFor(i);
            var ship = new EnemyShipRuntime($"enemy-{i + 1}", EnemyMaxHp, position, layout, WeaponLoadoutFor(layout, i))
            {
                // Own random phase so a whole squadron doesn't weave in lockstep (SteerEnemy's dodge).
                DodgePhaseSeed = (float)(_random.NextDouble() * 1000.0),
                // Starts its orbit exactly where it already is, spinning either way at random.
                OrbitAngleDegrees = BearingDegrees(position - _shipFieldPosition),
                OrbitDirection = _random.Next(2) == 0 ? 1f : -1f,
            };
            // A sector opens with raiders closing in, not with a volley at the moment of arrival -
            // the player gets a few seconds to get to a gun. Applies to every turret this hull has.
            for (var t = 0; t < ship.TurretFireCooldowns.Length; t++)
                ship.TurretFireCooldowns[t] = EnemyOpeningDelaySeconds;
            _enemyShips.Add(ship);
        }
        _remainingEnemyShips = count;
    }

    // Dev cheat panel only (World.cs's DebugSpawnEnemyPressed) - drops one more raider in
    // immediately at normal firing distance, already settled and ready to shoot, instead of the
    // usual far-off approach (SpawnEnemySquadron's EnemySpawnDistance). Opens a battle itself if
    // one isn't already running, since StepEnemyFleet/TryEnemyFire are both no-ops outside
    // IsInBattle - a fast way to get a live target for testing hit resolution.
    private void DebugSpawnEnemyNearby()
    {
        if (_battleSectorPointId is null && _battleNpcShipId is null)
        {
            // _battleSectorPointId has to be a real GalaxyPoint id - StepVoyage's own battle
            // bookkeeping (ResolveEnemyLosses, HasFledTheSector) looks it up every tick regardless
            // of how the battle started, and a made-up id crashes on the very next tick. Any point
            // in the current system works; unlike StartBattle this deliberately skips
            // SpawnEnemySquadron (which would wipe any enemies already in the field) and the ship's
            // own flight-state reset - this is one extra test target, not a fresh engagement.
            var anyPoint = GalaxyMap.GetSystem(_currentSystemId).Points.FirstOrDefault()
                ?? GalaxyMap.GetPoint(GalaxyMap.HomePointId);
            _battleFaction = OwnerOf(anyPoint.Id);
            _battleSectorPointId = anyPoint.Id;
        }

        var axis = RotateLocalToWorld(new Vec2(1f, 0f), _shipRotationDegrees);
        var position = _shipFieldPosition + axis * EnemyStandoffDistance;
        var index = _enemyShips.Count;
        var layout = EnemyClassFor(index);
        _enemyShips.Add(new EnemyShipRuntime($"debug-enemy-{index + 1}", EnemyMaxHp, position, layout, WeaponLoadoutFor(layout, index))
        {
            OrbitAngleDegrees = BearingDegrees(position - _shipFieldPosition),
            OrbitDirection = _random.Next(2) == 0 ? 1f : -1f,
        });
        _remainingEnemyShips = _enemyShips.Count;
    }

    // Which hull each ship of the squadron is. Derived from the sector's own id and the ship's place
    // in the formation, so a given sector always fields the same opposition - travelling back to a
    // fight you ran from must not roll it again - while different sectors differ. The lead ship is
    // never the freighter: the one you meet first should be the one that shoots back.
    // Test-only override (same "precondition setter" convention as World.WallBlocks.cs's
    // DebugBreachWallBlock) - lets a test force which hull a battle fields instead of hunting
    // through the galaxy for a sector id that happens to hash to the one it needs.
    private EnemyShipClass? _debugForcedEnemyClass;
    public void DebugForceEnemyClass(EnemyShipClass? kind) => _debugForcedEnemyClass = kind;

    private EnemyShipLayout EnemyClassFor(int index)
    {
        if (_debugForcedEnemyClass is { } forced)
            return EnemyShipLayout.Of(forced);

        // _battleSectorPointId is which sector/station the current fight is at (M39's VoyagePhase
        // removal dropped the old _travelTargetPointId this used to read) - falls back to
        // _dockedPointId (rare: a resolved fight can still be ticking down while the ship is
        // already back at a berth) and then a fixed seed so this never throws.
        var seed = StableSectorSeed(_battleSectorPointId ?? _dockedPointId ?? "sector") + index * 7;
        var classes = EnemyShipLayout.All;
        var pick = classes[Math.Abs(seed) % classes.Count];
        return index == 0 && pick.Kind == EnemyShipClass.Freighter
            ? EnemyShipLayout.Of(EnemyShipClass.Raider)
            : pick;
    }

    // A squadron fields the whole arsenal, not a random subset ("у врагов были и лазеры и пулемёты
    // и магнитные пушки в арсенале") - cycling by index rather than an independent hash-per-ship
    // pick guarantees every type shows up at least once in any squadron of 3+, while the per-sector
    // offset still varies which type leads the cycle from one encounter to the next. Enemies don't
    // track ammo or heat like a manned TurretRuntime does; this just picks which of the 3 weapons'
    // rate-of-fire, bolt style and wall damage (TryEnemyFire, TurretBalance) a given raider uses.
    private static readonly TurretWeaponType[] EnemyWeaponChoices =
        { TurretWeaponType.Magnetic, TurretWeaponType.Laser, TurretWeaponType.MachineGun };

    private TurretWeaponType EnemyWeaponFor(int index)
    {
        var offset = Math.Abs(StableSectorSeed(_battleSectorPointId ?? _dockedPointId ?? "sector"));
        return EnemyWeaponChoices[(index + offset) % EnemyWeaponChoices.Length];
    }

    // Most hulls carry exactly the single weapon EnemyWeaponFor hands them by squadron slot; a class
    // with its own EnemyShipLayout.WeaponLoadout (Frigate's 2 magnetic + 1 laser) overrides that and
    // always brings its whole fixed arsenal instead, regardless of formation slot.
    private IReadOnlyList<TurretWeaponType> WeaponLoadoutFor(EnemyShipLayout layout, int index) =>
        layout.WeaponLoadout ?? new[] { EnemyWeaponFor(index) };

    // string.GetHashCode is randomised per process, so it would hand the same sector a different
    // squadron on every launch - the same reason AsteroidShape writes its own hash.
    private static int StableSectorSeed(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in text)
                hash = (hash ^ c) * 16777619;
            return hash & 0x7FFFFFFF;
        }
    }

    private void StepEnemyFleet(double deltaSeconds)
    {
        if (!IsInBattle)
            return;

        _enemyDodgeClock += (float)deltaSeconds;
        // "Постоянно, пока игрок стреляет вообще" - a live player-fired shot in the field is the
        // simplest true signal for that (it lasts as long as the flight time of the shot, which
        // resets to false shortly after the trigger's actually released), with no extra state to
        // track and no dependency on which specific shot might be headed for which raider.
        var isPlayerFiring = _projectiles.Any(p => !p.FromEnemy);

        var alive = _enemyShips.Where(e => e.Alive).ToList();

        for (var i = 0; i < alive.Count; i++)
        {
            var enemy = alive[i];
            var (priority, aimTarget) = ResolveEnemyTarget(enemy);
            enemy.TargetPriority = priority;
            SteerEnemy(enemy, aimTarget, ApproachAxisSignFor(enemy, priority), FormationAngleOffsetDegrees(i, alive.Count), isPlayerFiring, deltaSeconds);
            TryEnemyFire(enemy, aimTarget, deltaSeconds);
        }
    }

    // Wingmen spread out along the same orbit instead of stacking on top of each other, recomputed
    // over the ships still flying so the formation closes up as they're picked off instead of
    // leaving gaps.
    private static float FormationAngleOffsetDegrees(int index, int count) =>
        (index - (count - 1) / 2f) * EnemyFormationAngleSpacingDegrees;

    private static float BearingDegrees(Vec2 vector) => MathF.Atan2((float)vector.Y, (float)vector.X) * (180f / MathF.PI);

    // Fixed priority order the user asked for, evaluated top to bottom: Engines, then Weapons, then
    // Reactor, then Bridge, then Oxygen - every one of them a real disable now (HelmConsoleBroken
    // completes the set), so a long enough fight can in principle work all the way down it.
    private static readonly EnemyTargetPriority[] EnemyPriorityOrder =
    {
        EnemyTargetPriority.Engines, EnemyTargetPriority.Weapons, EnemyTargetPriority.Reactor,
        EnemyTargetPriority.Bridge, EnemyTargetPriority.Oxygen,
    };

    // Ship-local ("layout") aim point for a priority category, or null if there's nothing left
    // there worth shooting at (every device in that category already disconnected/destroyed) - the
    // same frame WallBlock/Turret/ShipSystemDevice positions already live in.
    private Vec2? LocalAimPointFor(EnemyTargetPriority priority) => priority switch
    {
        EnemyTargetPriority.Engines => Ship.SystemDevices
            .Where(d => d.System == PowerSystemId.Engine && IsDeviceConnected(d.Id))
            .Select(d => (Vec2?)d.Position).FirstOrDefault(),
        EnemyTargetPriority.Weapons => Ship.Turrets
            .Where(t => !_turretRuntimes[t.Id].Damaged)
            .Select(t => (Vec2?)TurretMount.For(Ship.Rooms, Ship.Turrets, t).Position).FirstOrDefault(),
        EnemyTargetPriority.Reactor => PowerGrid.Reactor.Broken ? null : Ship.ReactorBlock.Position,
        EnemyTargetPriority.Bridge => HelmConsoleBroken ? null : Ship.HelmConsole.Position,
        EnemyTargetPriority.Oxygen => Ship.SystemDevices
            .Where(d => d.System == PowerSystemId.Oxygen && IsDeviceConnected(d.Id))
            .Select(d => (Vec2?)d.Position).FirstOrDefault(),
        _ => null,
    };

    private Vec2? WorldAimPointFor(EnemyTargetPriority priority)
    {
        if (LocalAimPointFor(priority) is not { } local)
            return null;
        var (hullLocalCenter, _) = GetHullLocalBounds();
        return _shipFieldPosition + RotateLocalToWorld(local - hullLocalCenter, _shipRotationDegrees);
    }

    // Sticky target selection ("держится одной цели, пока не выведена из строя") - keeps whatever
    // this raider was already committed to as long as it's still there, and only walks down the
    // fixed priority order once that stops resolving to anything.
    private (EnemyTargetPriority Priority, Vec2 Position) ResolveEnemyTarget(EnemyShipRuntime enemy)
    {
        if (enemy.TargetPriority is { } current && WorldAimPointFor(current) is { } stillThere)
            return (current, stillThere);

        foreach (var candidate in EnemyPriorityOrder)
            if (WorldAimPointFor(candidate) is { } position)
                return (candidate, position);

        // Unreachable in practice (Reactor/Bridge above never report as gone), kept only so this
        // always returns something rather than throwing.
        return (EnemyTargetPriority.Reactor, _shipFieldPosition);
    }

    // Which side of the hull's own local +X axis a raider should hold station on for its current
    // target: the stern for the engines, the bow for the bridge (matching which end of the ship
    // each actually sits at), and whichever side it's already nearer for everything in between -
    // there's no single "right" side for the weapons/reactor/oxygen and crossing the whole hull to
    // switch sides would cost more than it's worth.
    private float ApproachAxisSignFor(EnemyShipRuntime enemy, EnemyTargetPriority priority)
    {
        if (priority == EnemyTargetPriority.Engines)
            return 1f;
        if (priority == EnemyTargetPriority.Bridge)
            return -1f;

        var local = RotateWorldToLocal(enemy.Position - _shipFieldPosition, _shipRotationDegrees);
        return local.X < 0f ? -1f : 1f;
    }

    private void SteerEnemy(EnemyShipRuntime enemy, Vec2 aimTarget, float approachSign, float formationAngleOffsetDegrees, bool isPlayerFiring, double deltaSeconds)
    {
        var dt = (float)deltaSeconds;

        // The "good" side to be circling on for the current target (ApproachAxisSignFor), in world
        // degrees rather than a local +X/-X sign, so it can be compared directly against the
        // raider's own continuously-advancing orbit angle below.
        var idealAxis = RotateLocalToWorld(new Vec2(approachSign, 0f), _shipRotationDegrees);
        var idealAngleDegrees = BearingDegrees(idealAxis);

        // Never actually parks - the orbit always advances, just slower near the ideal side (where
        // it lingers a while, looking for its shot) and faster on the way past the far side. A pure
        // hard lock onto the ideal side is exactly the "stands still, permanently blocked firing
        // line" problem this replaces.
        var angleFromIdeal = ShortestAngle(enemy.OrbitAngleDegrees - idealAngleDegrees);
        var speedFactor = 1f - EnemyOrbitPullStrength * MathF.Cos(angleFromIdeal * (MathF.PI / 180f));
        enemy.OrbitAngleDegrees += EnemyOrbitDegreesPerSecond * speedFactor * enemy.OrbitDirection * dt;

        // A smooth extra swing on top of the steady orbit while the player's actually shooting -
        // never suppressed by how close the raider is to reaching its station, which is what makes
        // this take priority over holding a perfect firing position rather than just decorating it.
        var dodgeAngle = isPlayerFiring
            ? MathF.Sin((_enemyDodgeClock + enemy.DodgePhaseSeed) * EnemyDodgeFrequencyHz * MathF.Tau) * EnemyDodgeAngleAmplitudeDegrees
            : 0f;

        var orbitDirectionVec = TurretMount.FromDegrees(enemy.OrbitAngleDegrees + formationAngleOffsetDegrees + dodgeAngle);
        var station = _shipFieldPosition + orbitDirectionVec * EnemyStandoffDistance;
        var toStation = station - enemy.Position;
        var approach = (aimTarget - enemy.Position).Normalized();

        var desired = toStation.Length() > 0.4f
            ? toStation.Normalized() * EnemyMaxSpeed
            : Vec2.Zero; // briefly caught up with the (still-moving) station point, not truly parked

        var steering = desired - enemy.Velocity;
        var maxDelta = EnemyAccelerationPerSecond * dt;
        enemy.Velocity += steering.Length() <= maxDelta ? steering : steering.Normalized() * maxDelta;
        if (enemy.Velocity.Length() > EnemyMaxSpeed)
            enemy.Velocity = enemy.Velocity.Normalized() * EnemyMaxSpeed;

        enemy.Position += enemy.Velocity * dt;
        SeparateEnemy(enemy);

        // Always facing its actual target, not its heading or the ship's centre - a warship keeps
        // its guns on exactly what it's shooting at while it maneuvers.
        var facing = BearingDegrees(approach);
        enemy.RotationDegrees = RotateToward(enemy.RotationDegrees, facing, EnemyTurnDegreesPerSecond * dt);
    }

    // Hulls are solid. A raider that flies into the player's ship - or into a wingman - gets pushed
    // back out to touching distance instead of sliding through it, which is what "столкновение"
    // means to anyone watching: two ships meet and stop, they don't overlap into one shape.
    private void SeparateEnemy(EnemyShipRuntime enemy)
    {
        var (_, halfExtents) = GetHullLocalBounds();
        var local = RotateWorldToLocal(enemy.Position - _shipFieldPosition, _shipRotationDegrees);
        var onHull = new Vec2(
            Math.Clamp(local.X, -halfExtents.X, halfExtents.X),
            Math.Clamp(local.Y, -halfExtents.Y, halfExtents.Y));
        var away = local - onHull;
        var gap = away.Length();
        if (gap < EnemyHullRadius)
        {
            // gap == 0 means the centre is inside the box, where "away" has no direction of its
            // own - shove it out through the nearest face instead.
            var normal = gap > 0.001f ? away.Normalized() : NearestFaceNormal(local, halfExtents);
            enemy.Position = _shipFieldPosition + RotateLocalToWorld(onHull + normal * EnemyHullRadius, _shipRotationDegrees);
            enemy.Velocity = Vec2.Zero;
        }

        foreach (var other in _enemyShips)
        {
            if (ReferenceEquals(other, enemy) || !other.Alive)
                continue;
            var between = enemy.Position - other.Position;
            var distance = between.Length();
            const float minimumSeparation = EnemyHullRadius * 2f;
            if (distance >= minimumSeparation)
                continue;
            var normal = distance > 0.001f ? between.Normalized() : new Vec2(1f, 0f);
            enemy.Position = other.Position + normal * minimumSeparation;
        }
    }

    private static Vec2 NearestFaceNormal(Vec2 local, Vec2 halfExtents)
    {
        var toLeft = local.X + halfExtents.X;
        var toRight = halfExtents.X - local.X;
        var toTop = local.Y + halfExtents.Y;
        var toBottom = halfExtents.Y - local.Y;
        var nearest = Math.Min(Math.Min(toLeft, toRight), Math.Min(toTop, toBottom));
        if (nearest == toLeft) return new Vec2(-1f, 0f);
        if (nearest == toRight) return new Vec2(1f, 0f);
        return nearest == toTop ? new Vec2(0f, -1f) : new Vec2(0f, 1f);
    }

    // Whether the player's hull, parked at candidateCenter, would be sitting inside a hostile one.
    private bool HullOverlapsEnemy(Vec2 candidateCenter)
    {
        var (_, halfExtents) = GetHullLocalBounds();
        foreach (var enemy in _enemyShips)
        {
            if (!enemy.Alive)
                continue;
            var local = RotateWorldToLocal(enemy.Position - candidateCenter, _shipRotationDegrees);
            var onHull = new Vec2(
                Math.Clamp(local.X, -halfExtents.X, halfExtents.X),
                Math.Clamp(local.Y, -halfExtents.Y, halfExtents.Y));
            if ((local - onHull).Length() < EnemyHullRadius)
                return true;
        }
        return false;
    }

    // Each turret in enemy.WeaponLoadout fires independently on its own TurretFireCooldowns entry -
    // almost always a single-entry loop (the common one-weapon-per-hull case), but a multi-turret
    // hull like Frigate has each of its 3 guns reload and fire on its own schedule against the same
    // resolved target, rather than the whole ship sharing one clock.
    private void TryEnemyFire(EnemyShipRuntime enemy, Vec2 target, double deltaSeconds)
    {
        // Same rule the design gives the player: badly hurt raiders break off (EnemyShip's
        // IsRetreating) - they stay shootable and boardable, they just stop shooting back.
        if (enemy.Ship.IsRetreating)
            return;

        // Range is judged against the hull's own centre, not the specific priority target - a
        // point deep inside the ship (an engine device, the reactor) can sit further from the
        // enemy than the hull's centre does even while holding a perfectly normal standoff, and
        // gating range on that exact point would leave a raider that's plainly close enough to
        // fight never actually firing at all. Line of sight still checks the real aim line, since
        // that's genuinely about whether this specific shot is blocked.
        var toTarget = target - enemy.Position;
        var inRange = (_shipFieldPosition - enemy.Position).Length() <= EnemyWeaponRangeUnits && HasLineOfSight(enemy.Position, target);

        for (var t = 0; t < enemy.WeaponLoadout.Count; t++)
        {
            enemy.TurretFireCooldowns[t] = Math.Max(0f, enemy.TurretFireCooldowns[t] - (float)deltaSeconds);
            if (enemy.TurretFireCooldowns[t] > 0 || !inRange)
                continue;

            var weapon = enemy.WeaponLoadout[t];
            enemy.TurretFireCooldowns[t] = weapon == TurretWeaponType.Magnetic
                ? EnemyMagneticFireIntervalSeconds
                : EnemyFireIntervalSeconds;

            var direction = toTarget.Normalized();
            var isLaser = weapon == TurretWeaponType.Laser;
            var pellets = weapon == TurretWeaponType.MachineGun ? EnemyMachineGunPelletsPerBurst : 1;

            // Each weapon its own wall damage (TurretBalance.EnemyMagneticWallDamage/EnemyLaserWallDamage/
            // EnemyMachineGunWallDamage) - only WallBlock.Hp actually reads this (World.EnemyAi.cs's
            // ApplyEnemyAttack); every other fixture it can hit is a plain on/off flag that any landed
            // shot disables outright regardless of the number.
            var damage = weapon switch
            {
                TurretWeaponType.Magnetic => TurretBalance.EnemyMagneticWallDamage,
                TurretWeaponType.Laser => TurretBalance.EnemyLaserWallDamage,
                _ => TurretBalance.EnemyMachineGunWallDamage,
            };

            for (var i = 0; i < pellets; i++)
            {
                var jitterDegrees = pellets > 1
                    ? ((float)_random.NextDouble() * 2f - 1f) * EnemyMachineGunSpreadDegrees
                    : 0f;
                var jitterRadians = jitterDegrees * (MathF.PI / 180f);
                var cos = MathF.Cos(jitterRadians);
                var sin = MathF.Sin(jitterRadians);
                var jittered = new Vec2(direction.X * cos - direction.Y * sin, direction.X * sin + direction.Y * cos);
                SpawnProjectile(enemy.Position + jittered * EnemyHullRadius, jittered, fromEnemy: true, isLaser, damage);
            }
        }
    }

    // Nothing solid in the way. Asteroids are the only thing out there big enough to hide behind,
    // which is exactly what makes them worth flying around during a fight instead of just being
    // obstacles to avoid denting the hull on.
    private bool HasLineOfSight(Vec2 from, Vec2 to)
    {
        foreach (var asteroid in AsteroidField.Asteroids)
            if (SegmentHitsCircle(from, to, asteroid.Position, asteroid.Radius))
                return false;
        return true;
    }

    // Closest approach of the segment to the circle's centre, clamped to the segment's ends.
    public static bool SegmentHitsCircle(Vec2 from, Vec2 to, Vec2 center, float radius)
    {
        var segment = to - from;
        var lengthSquared = segment.X * segment.X + segment.Y * segment.Y;
        if (lengthSquared < 1e-6f)
            return (center - from).Length() <= radius;

        var toCenter = center - from;
        var t = Math.Clamp((toCenter.X * segment.X + toCenter.Y * segment.Y) / lengthSquared, 0f, 1f);
        return (from + segment * t - center).Length() <= radius;
    }

    private IReadOnlyList<EnemyShipFieldState> CreateEnemyShipStates()
    {
        var boardable = BoardableEnemy;
        return _enemyShips
            .Where(e => e.Alive)
            .Select(e => new EnemyShipFieldState(e.Id, e.Position.X, e.Position.Y, e.RotationDegrees,
                e.Ship.Hp, e.Ship.MaxHp, e.Ship.IsRetreating, ReferenceEquals(e, boardable), e.Layout.Kind))
            .ToArray();
    }
}
