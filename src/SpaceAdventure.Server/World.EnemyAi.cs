using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const double TurretDamageChance = 0.35; // vs damaging a system or breaching a room
    private const double SystemDamageChance = 0.35; // rolled only if the turret roll misses

    // The only randomness in the simulation, and it used to be seeded from the clock - which made
    // every fight, and therefore every test that flew through one, a different run: the suite
    // drifted between 177 and 180 passing with nothing changed at all.
    //
    // Seeded from a counter rather than a constant, so both properties hold: a whole run of the
    // suite is reproducible (Worlds are built in a fixed order), while two Worlds built in a row
    // still roll differently - which is what the tests that retry a scenario until it lands are
    // relying on. A real session builds exactly one World, so its fights are as varied as the
    // sequence of rolls within them.
    private static int _seedCounter;
    private readonly Random _random = new(Interlocked.Increment(ref _seedCounter) * 104729);

    // What an enemy shell does once it actually reaches the hull (World.Projectiles.cs). Every hit
    // goes at the shield first (game_design.md section 1) — only once that's depleted does one
    // disable a turret, knock out a ship system block ("повреждена локальная коробка"), or breach a
    // single outer-hull wall block (several breaches can pile up in the same room).
    //
    // This used to fire on a timer with no projectile at all, because the enemy had no position;
    // the consequences are unchanged, but now something had to cross the gap to cause them, which
    // means cover and maneuvering are worth something.
    private void ApplyEnemyAttack()
    {
        if (Shield.TryAbsorbHit())
            return;

        var undamagedTurrets = _turretRuntimes.Values.Where(t => !t.Damaged).ToList();
        if (undamagedTurrets.Count > 0 && _random.NextDouble() < TurretDamageChance)
        {
            undamagedTurrets[_random.Next(undamagedTurrets.Count)].Damaged = true;
            return;
        }

        // A "system hit" severs one currently-intact wire (game_design.md section 1, M14) rather
        // than flipping one flat per-system flag — could be a trunk or a drop, and could land on a
        // wire that's reinforcing an already-covered input (in which case the other, still-intact
        // wire into that same pin keeps it powered - see IsPinPowered).
        var liveWires = _wires.Where(w => !_wireDamaged[w.Id]).ToList();
        if (liveWires.Count > 0 && _random.NextDouble() < SystemDamageChance)
        {
            CutWire(liveWires[_random.Next(liveWires.Count)].Id);
            return;
        }

        var candidates = Ship.WallBlocks.Where(b => !_breachedWallBlockIds.Contains(b.Id) && !IsAtDoorPosition(b)).ToList();
        if (candidates.Count == 0)
            return; // every wall block already breached

        _breachedWallBlockIds.Add(candidates[_random.Next(candidates.Count)].Id);
    }
}
