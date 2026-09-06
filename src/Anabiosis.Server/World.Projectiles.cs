using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Shots that travel. Every gun in the field - the player's turrets and the raiders' - puts a
// projectile in here instead of applying damage the instant the trigger is pulled, which is what
// makes aiming a skill rather than a formality: a shell crosses the gap at a finite speed, an
// asteroid in the way eats it, and a badly led shot sails past.
public sealed partial class World
{
    private const float ShellSpeed = 22f;
    private const float LaserBoltSpeed = 44f;
    private const float ProjectileLifetimeSeconds = 6f;

    private readonly List<ProjectileRuntime> _projectiles = new();
    private int _nextProjectileId;

    private void SpawnProjectile(Vec2 origin, Vec2 direction, bool fromEnemy, bool isLaser, float damage)
    {
        var heading = direction.Normalized();
        if (heading == Vec2.Zero)
            return;

        _projectiles.Add(new ProjectileRuntime
        {
            Id = $"shot-{++_nextProjectileId}",
            Position = origin,
            Velocity = heading * (isLaser ? LaserBoltSpeed : ShellSpeed),
            Damage = damage,
            FromEnemy = fromEnemy,
            IsLaser = isLaser,
            LifeRemaining = ProjectileLifetimeSeconds,
        });
    }

    // Stepped as a segment from where the shot was to where it's going, not as a point sample:
    // at 44 units a second a laser bolt would otherwise tunnel straight through a hull between
    // two ticks.
    private void StepProjectiles(double deltaSeconds)
    {
        var dt = (float)deltaSeconds;

        for (var i = _projectiles.Count - 1; i >= 0; i--)
        {
            var shot = _projectiles[i];
            var from = shot.Position;
            var to = from + shot.Velocity * dt;
            shot.Position = to;
            shot.LifeRemaining -= dt;

            if (ResolveProjectileHit(shot, from, to) || shot.LifeRemaining <= 0)
                _projectiles.RemoveAt(i);
        }
    }

    private bool ResolveProjectileHit(ProjectileRuntime shot, Vec2 from, Vec2 to)
    {
        // Rock stops everything, whoever fired it.
        foreach (var asteroid in AsteroidField.Asteroids)
            if (SegmentHitsCircle(from, to, asteroid.Position, asteroid.Radius))
                return true;

        if (shot.FromEnemy)
        {
            var (hullLocalCenter, halfExtents) = GetHullLocalBounds();
            // Circle over the player's hull rather than its oriented box - has to be the actual
            // diagonal (the true minimal circle enclosing the whole rectangle), not just the
            // longer of the two half-extents: a shot aimed at a wall block near a corner (the
            // bottom/top edge close to the bow or stern) sits outside a max(X,Y)-radius circle by
            // a small but real margin and would silently miss this broad-phase check before ever
            // reaching the wall-block trace, even though it's a perfectly aimed shot.
            var hullRadius = MathF.Sqrt((float)(halfExtents.X * halfExtents.X + halfExtents.Y * halfExtents.Y));
            if (!SegmentHitsCircle(from, to, _shipFieldPosition, hullRadius))
            {
                shot.LocalEntryPoint = null; // clear of the hull - a later re-entry (rare) starts fresh
                return false;
            }

            // The shield gets exactly one chance per shot, taken the instant it first reaches the
            // hull's broad circle - not once per tick it spends crossing the ship afterward
            // (ApplyEnemyAttack can now be called several ticks in a row for the same shot as it
            // travels through breaches toward the far side, see its own doc comment).
            if (!shot.ShieldChecked)
            {
                shot.ShieldChecked = true;
                if (Shield.TryAbsorbHit())
                    return true;
            }

            // WallBlock/Turret/ShipSystemDevice/door positions all live in the same ship-local
            // ("layout") frame TurretMount.For and World.Combat.cs's TryFire already convert
            // through - inverting that exact transform here is what lets ApplyEnemyAttack compare
            // this shot's path directly against them.
            //
            // localFrom deliberately reuses the PREVIOUS tick's converted endpoint rather than
            // reconverting this tick's own "from" world point fresh - the ship can be actively
            // turning (Arc-mode piloting) while a shot spends several ticks crossing it, and
            // reconverting "from" under this tick's own rotation would only coincide with last
            // tick's "to" (converted under last tick's rotation) if the rotation hadn't changed in
            // between. Any real difference opens a gap between the two ticks' local segments that a
            // wall block sitting right in it would fall through, unnoticed by either tick's check -
            // exactly the "shots occasionally pass straight through" symptom this fixes. Chaining
            // local endpoints tick to tick keeps the traversed path continuous in local space no
            // matter how fast the ship turns underneath it, at the cost of the path no longer being
            // a perfectly straight line in that rotating frame - a curve here is a fully rendered
            // shot, not a skipped one.
            var localFrom = shot.LocalEntryPoint ?? RotateWorldToLocal(from - _shipFieldPosition, _shipRotationDegrees) + hullLocalCenter;
            var localTo = RotateWorldToLocal(to - _shipFieldPosition, _shipRotationDegrees) + hullLocalCenter;
            var hitSomething = ApplyEnemyAttack(localFrom, localTo, shot.Damage);
            shot.LocalEntryPoint = hitSomething ? null : localTo;
            return hitSomething;
        }

        foreach (var enemy in _enemyShips.Where(e => e.Alive))
        {
            if (!SegmentHitsCircle(from, to, enemy.Position, EnemyHullRadius))
                continue;

            enemy.Ship.ApplyDamage(shot.Damage);
            return true;
        }

        return false;
    }

    private IReadOnlyList<ProjectileState> CreateProjectileStates() =>
        _projectiles
            .Select(p => new ProjectileState(p.Id, p.Position.X, p.Position.Y,
                MathF.Atan2((float)p.Velocity.Y, (float)p.Velocity.X) * (180f / MathF.PI), p.FromEnemy, p.IsLaser))
            .ToArray();
}

internal sealed class ProjectileRuntime
{
    public string Id { get; init; } = string.Empty;
    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; init; }
    public float Damage { get; init; }
    public bool FromEnemy { get; init; }
    public bool IsLaser { get; init; }
    public float LifeRemaining { get; set; }
    public bool ShieldChecked { get; set; }
    // Ship-local ("layout") point this shot last resolved to, chained tick to tick so an actively
    // turning ship can't open a gap between two ticks' local segments (World.Projectiles.cs's own
    // doc comment on ResolveProjectileHit's localFrom). Null before the shot ever reaches the hull
    // and again once it's clear of it or has hit something.
    public Vec2? LocalEntryPoint { get; set; }
}
