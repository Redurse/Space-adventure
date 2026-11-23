using System.Linq;
using Anabiosis.Client.Audio;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// The sounds of a fight, from the same snapshot diff as everything else in Game1.Sound.cs: guns going off, shells
// landing on the player's plating, a hostile ship coming apart. The clips already existed (laser_shot, rifle_shot,
// explosion) but nothing played them, so the whole battle ran in silence apart from compartments blowing up.
public partial class Game1
{
    // Plating damage below this is the slow kind (fire, heat) and makes no noise of its own.
    private const float HitSoundMinDamage = 4f;

    private void UpdateCombatSounds(WorldSnapshot previous, WorldSnapshot current, double nowSeconds)
    {
        if (_sounds is null)
            return;

        var known = previous.Projectiles.Select(p => p.Id).ToHashSet();
        var ours = false;
        var theirs = false;
        foreach (var shot in current.Projectiles)
        {
            if (known.Contains(shot.Id))
                continue;
            if (shot.FromEnemy)
                theirs = true;
            else
                ours = true;
        }
        if (ours)
            _sounds.Play(GameSounds.LaserShot, nowSeconds, volume: 0.6f, pitchSpread: 0.08f);
        if (theirs)
            _sounds.Play(GameSounds.RifleShot, nowSeconds, volume: 0.4f, pitchSpread: 0.1f);

        // A shell landing on our own plating: a dull crump, quieter than a compartment going up but unmistakable.
        var before = previous.WallBlockStates.ToDictionary(s => s.Id);
        var hurt = current.WallBlockStates.Any(s => before.TryGetValue(s.Id, out var was) && was.Hp - s.Hp >= HitSoundMinDamage);
        if (hurt)
            _sounds.Play(GameSounds.Explosion, nowSeconds, volume: 0.32f, pitchSpread: 0.2f);

        // A hostile ship that has left the list was destroyed (a retreating one stays listed, see EffectTracker).
        if (previous.EnemyShip.Ships.Any(e => current.EnemyShip.Ships.All(n => n.Id != e.Id)))
            _sounds.Play(GameSounds.ExplosionBig, nowSeconds, volume: 0.8f, pitchSpread: 0.05f);
    }
}
