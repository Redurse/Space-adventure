using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const float EnemyAttackIntervalSeconds = 6f;
    private const double TurretDamageChance = 0.35; // vs damaging a system or breaching a room
    private const double SystemDamageChance = 0.35; // rolled only if the turret roll misses

    private readonly Random _random = new();
    private float _enemyAttackCooldown = EnemyAttackIntervalSeconds;

    // Simple attacker AI (game_design.md section 11): attacks on a timer, retreats (stops
    // attacking) at low HP. Every attack hits the shield first (game_design.md section 1) — only
    // once it's depleted does an attack disable a turret, knock out a ship system block
    // ("повреждена локальная коробка"), or breach a single outer-hull wall block (several
    // breaches can pile up in the same room) — no projectile/travel-time modeling since the
    // enemy has no position yet (see EnemyShip).
    private void StepEnemyAi(double deltaSeconds)
    {
        if (Phase != VoyagePhase.Battle || Enemy.Hp <= 0 || Enemy.IsRetreating)
            return;

        _enemyAttackCooldown -= (float)deltaSeconds;
        if (_enemyAttackCooldown > 0)
            return;

        _enemyAttackCooldown = EnemyAttackIntervalSeconds;

        if (Shield.TryAbsorbHit())
            return;

        var undamagedTurrets = _turretRuntimes.Values.Where(t => !t.Damaged).ToList();
        if (undamagedTurrets.Count > 0 && _random.NextDouble() < TurretDamageChance)
        {
            undamagedTurrets[_random.Next(undamagedTurrets.Count)].Damaged = true;
            return;
        }

        var undamagedSystems = Ship.SystemDevices.Where(d => !PowerGrid.IsDamaged(d.System)).ToList();
        if (undamagedSystems.Count > 0 && _random.NextDouble() < SystemDamageChance)
        {
            PowerGrid.SetDamaged(undamagedSystems[_random.Next(undamagedSystems.Count)].System, true);
            return;
        }

        var candidates = Ship.WallBlocks.Where(b => !_breachedWallBlockIds.Contains(b.Id)).ToList();
        if (candidates.Count == 0)
            return; // every wall block already breached

        _breachedWallBlockIds.Add(candidates[_random.Next(candidates.Count)].Id);
    }
}
