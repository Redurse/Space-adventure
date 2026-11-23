using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The fight's feedback, read off the same snapshot diff as the tool effects: sparks where a shell lands, and the
// shudder of the screen (CameraShake) that goes with a hit, a blast or a firing gun. None of it is sent over the wire -
// the server already says what changed, this only decides how loud it should look.
public sealed partial class EffectTracker
{
    private const float HullHitEffectSeconds = 0.45f;
    private const float ImpactEffectSeconds = 0.5f;
    // Plating damage below this is the slow kind (fire, the sun's heat), not a blow - it smokes, it does not flash.
    private const float HullHitMinDamage = 4f;
    private const int MaxHullHitsPerFrame = 4;
    public const float ShipDestroyedTrauma = 0.5f;
    private const float FiredGunTrauma = 0.28f;
    private const float CompartmentBlastTrauma = 0.55f;
    private const float ReactorBlastTrauma = 0.95f;
    // A blast already this old when first seen was set off before we were looking (joining mid-fight): no jolt for it.
    private const float StaleBlastSeconds = 0.5f;

    private readonly HashSet<string> _shakenBlasts = new();
    private readonly Random _combatRandom = new();

    private void DetectCombat(WorldSnapshot previous, WorldSnapshot current)
    {
        DetectPlatingHits(previous, current);
        DetectShipBlasts(current);
        DetectShotsFired(previous, current);
        DetectHitsOnOtherShips(previous, current);
    }

    // A shell (or anything else with a punch) knocking plating off the player's own ship.
    private void DetectPlatingHits(WorldSnapshot previous, WorldSnapshot current)
    {
        var effects = 0;
        var trauma = 0f;
        var before = previous.WallBlockStates.ToDictionary(s => s.Id);
        foreach (var state in current.WallBlockStates)
        {
            if (!before.TryGetValue(state.Id, out var was) || was.Hp <= state.Hp)
                continue;
            var lost = was.Hp - state.Hp;
            if (lost < HullHitMinDamage)
                continue;

            trauma += Math.Clamp(lost / MathF.Max(1f, state.MaxHp) * 1.2f, 0.12f, 0.4f);
            if (was.Hp > 0f && state.Hp <= 0f)
                trauma += 0.15f; // the plate gave way
            if (effects < MaxHullHitsPerFrame && current.WallBlocks.FirstOrDefault(b => b.Id == state.Id) is { } block)
            {
                _effects.Add(new TransientEffect(EffectKind.HullHit, block.Position, HullHitEffectSeconds));
                effects++;
            }
        }
        // Several plates in one frame is one big hit, not several small ones stacked past the ceiling.
        Shake.Add(MathF.Min(trauma, 0.7f));
    }

    private void DetectShipBlasts(WorldSnapshot current)
    {
        foreach (var blast in current.ShipBlasts ?? Array.Empty<ShipBlastState>())
        {
            if (!_shakenBlasts.Add(blast.Id) || blast.Age > StaleBlastSeconds)
                continue;
            Shake.Add(blast.Kind == ShipBlastKind.Reactor ? ReactorBlastTrauma : CompartmentBlastTrauma);
        }
        if (_shakenBlasts.Count > 64)
            _shakenBlasts.Clear();
    }

    // A friendly shell leaving the barrel kicks the view a little - the feel of a gun, even where the sound is far away.
    private void DetectShotsFired(WorldSnapshot previous, WorldSnapshot current)
    {
        var known = previous.Projectiles.Select(p => p.Id).ToHashSet();
        if (current.Projectiles.Any(p => !p.FromEnemy && !known.Contains(p.Id)))
            Shake.Add(FiredGunTrauma);
    }

    // Sparks where a shell lands on a hostile or neutral hull - read from its hull points falling between two snapshots.
    private void DetectHitsOnOtherShips(WorldSnapshot previous, WorldSnapshot current)
    {
        foreach (var ship in current.EnemyShip.Ships)
        {
            var was = previous.EnemyShip.Ships.FirstOrDefault(e => e.Id == ship.Id);
            if (was is not null && was.Hp - ship.Hp >= 1f)
                AddImpact(ship.X, ship.Y, was.Hp - ship.Hp, ship.MaxHp);
        }

        foreach (var npc in current.NpcShips)
        {
            var was = previous.NpcShips.FirstOrDefault(n => n.Id == npc.Id);
            if (was is not null && npc.MaxHp > 0f && was.Hp - npc.Hp >= 1f)
                AddImpact(npc.X, npc.Y, was.Hp - npc.Hp, npc.MaxHp);
        }

        // A hurt ambient ship that has left the list was shot to pieces.
        foreach (var gone in previous.NpcShips)
        {
            if (gone.MaxHp <= 0f || gone.Hp >= gone.MaxHp || current.NpcShips.Any(n => n.Id == gone.Id))
                continue;
            _effects.Add(new TransientEffect(EffectKind.Explosion, new Vec2(gone.X, gone.Y), ExplosionEffectSeconds));
            Shake.Add(ShipDestroyedTrauma);
        }
    }

    private void AddImpact(double x, double y, float lost, float maxHp)
    {
        // Somewhere on the hull rather than dead centre - the exact landing point is not in the snapshot.
        var spot = new Vec2(x + (_combatRandom.NextDouble() - 0.5) * 6.0, y + (_combatRandom.NextDouble() - 0.5) * 6.0);
        _effects.Add(new TransientEffect(EffectKind.Impact, spot, ImpactEffectSeconds));
        Shake.Add(Math.Clamp(lost / MathF.Max(1f, maxHp) * 2f, 0.06f, 0.2f));
    }
}
