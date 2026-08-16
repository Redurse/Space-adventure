using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

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
            var (_, halfExtents) = GetHullLocalBounds();
            // Generous circle over the player's hull rather than its oriented box: the hull is a
            // long thin thing and a shell clipping its nose should still count.
            var hullRadius = Math.Max(halfExtents.X, halfExtents.Y);
            if (!SegmentHitsCircle(from, to, _shipFieldPosition, hullRadius))
                return false;

            ApplyEnemyAttack();
            return true;
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
                MathF.Atan2(p.Velocity.Y, p.Velocity.X) * (180f / MathF.PI), p.FromEnemy, p.IsLaser))
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
}
