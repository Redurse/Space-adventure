using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

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
    private const float EnemyStandoffDistance = 22f; // where a raider prefers to sit and shoot from
    private const float EnemyFormationSpacing = 4f;  // sideways gap between wingmen at that distance
    private const float EnemyMaxSpeed = 3.4f;        // deliberately under the player's 8: you can outrun them
    private const float EnemyAccelerationPerSecond = 2.2f;
    private const float EnemyTurnDegreesPerSecond = 120f;
    private const float EnemyWeaponRangeUnits = 26f; // just outside their standoff: they shoot once settled
    // Per ship, so a squadron still hits harder than a lone raider without three of them turning
    // the hull into scrap faster than a crew can weld it.
    private const float EnemyFireIntervalSeconds = 7f;
    private const float EnemyOpeningDelaySeconds = 4f; // a beat to close in before the first volley
    private const float EnemySpawnDistance = 38f;    // far enough that the fight opens with an approach
    public const float EnemyHullRadius = 3.5f;       // what a shell has to hit, and what its own shots clear

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
            _enemyShips.Add(new EnemyShipRuntime($"enemy-{i + 1}", EnemyMaxHp, position, EnemyClassFor(i))
            {
                // A sector opens with raiders closing in, not with a volley at the moment of
                // arrival - the player gets a few seconds to get to a gun.
                FireCooldown = EnemyOpeningDelaySeconds,
            });
        }
        _remainingEnemyShips = count;
    }

    // Which hull each ship of the squadron is. Derived from the sector's own id and the ship's place
    // in the formation, so a given sector always fields the same opposition - travelling back to a
    // fight you ran from must not roll it again - while different sectors differ. The lead ship is
    // never the freighter: the one you meet first should be the one that shoots back.
    private EnemyShipLayout EnemyClassFor(int index)
    {
        var seed = StableSectorSeed(_travelTargetPointId ?? _dockedPointId ?? "sector") + index * 7;
        var classes = EnemyShipLayout.All;
        var pick = classes[Math.Abs(seed) % classes.Count];
        return index == 0 && pick.Kind == EnemyShipClass.Freighter
            ? EnemyShipLayout.Of(EnemyShipClass.Raider)
            : pick;
    }

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
        if (Phase != VoyagePhase.Battle)
            return;

        var alive = _enemyShips.Where(e => e.Alive).ToList();
        var target = _shipFieldPosition;

        for (var i = 0; i < alive.Count; i++)
        {
            var enemy = alive[i];
            SteerEnemy(enemy, target, FormationOffset(i, alive.Count), deltaSeconds);
            TryEnemyFire(enemy, target, deltaSeconds);
        }
    }

    // Wingmen spread out perpendicular to the line of approach, recomputed over the ships still
    // flying so the formation closes up as they're picked off instead of leaving holes.
    private static float FormationOffset(int index, int count) =>
        (index - (count - 1) / 2f) * EnemyFormationSpacing;

    private void SteerEnemy(EnemyShipRuntime enemy, Vec2 target, float formationOffset, double deltaSeconds)
    {
        var dt = (float)deltaSeconds;

        // The firing line forms on the player's stern quarter and moves with the ship, so a raider
        // that loses its wingmen closes back onto the axis instead of parking wherever it happened
        // to drift to. Anchoring it to the hull's own attitude rather than to the current bearing
        // matters: "standoff distance along the line I'm already on" is satisfied by every point on
        // a circle, which is how a wingman ends up loitering forever out on a flank.
        var axis = RotateLocalToWorld(new Vec2(1f, 0f), _shipRotationDegrees);
        var perpendicular = new Vec2(-axis.Y, axis.X);
        var station = target + axis * EnemyStandoffDistance + perpendicular * formationOffset;
        var toStation = station - enemy.Position;
        var approach = (target - enemy.Position).Normalized();

        var desired = toStation.Length() > 0.4f
            ? toStation.Normalized() * EnemyMaxSpeed
            : Vec2.Zero; // parked on station: bleed off speed rather than jittering around it

        var steering = desired - enemy.Velocity;
        var maxDelta = EnemyAccelerationPerSecond * dt;
        enemy.Velocity += steering.Length() <= maxDelta ? steering : steering.Normalized() * maxDelta;
        if (enemy.Velocity.Length() > EnemyMaxSpeed)
            enemy.Velocity = enemy.Velocity.Normalized() * EnemyMaxSpeed;

        enemy.Position += enemy.Velocity * dt;
        SeparateEnemy(enemy);

        // Always facing its target, not its heading - a warship keeps its guns on you while it
        // maneuvers, and the nose is what the player reads as "it's shooting at me".
        var facing = MathF.Atan2(approach.Y, approach.X) * (180f / MathF.PI);
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

    private void TryEnemyFire(EnemyShipRuntime enemy, Vec2 target, double deltaSeconds)
    {
        enemy.FireCooldown = Math.Max(0f, enemy.FireCooldown - (float)deltaSeconds);

        // Same rule the design gives the player: badly hurt raiders break off (EnemyShip's
        // IsRetreating) - they stay shootable and boardable, they just stop shooting back.
        if (enemy.FireCooldown > 0 || enemy.Ship.IsRetreating)
            return;

        var toTarget = target - enemy.Position;
        if (toTarget.Length() > EnemyWeaponRangeUnits || !HasLineOfSight(enemy.Position, target))
            return;

        enemy.FireCooldown = EnemyFireIntervalSeconds;
        var direction = toTarget.Normalized();
        SpawnProjectile(enemy.Position + direction * EnemyHullRadius, direction, fromEnemy: true, isLaser: false, damage: 0f);
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
                e.Ship.Hp, e.Ship.MaxHp, e.Ship.IsRetreating, ReferenceEquals(e, boardable)))
            .ToArray();
    }
}
