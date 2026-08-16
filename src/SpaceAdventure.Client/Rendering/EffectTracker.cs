using System.Collections.Generic;
using System.Linq;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Detects welding/cutting/repair "moments" purely by diffing consecutive WorldSnapshots - the
// server already tells us exactly when a breach clears, an ore deposit takes a hit, or a system
// gets repaired, so no protocol change is needed to turn those edges into a brief client-only
// visual (game_design.md Phase 3 visual pass - Barotrauma-style tool feedback on top of the
// existing instant server actions).
public sealed class EffectTracker
{
    private const float WeldEffectSeconds = 0.6f;
    private const float CutEffectSeconds = 0.3f;
    private const float RepairEffectSeconds = 0.5f;

    private readonly List<TransientEffect> _effects = new();

    public IEnumerable<TransientEffect> Effects => _effects;

    public void Step(float deltaSeconds)
    {
        foreach (var effect in _effects)
            effect.RemainingSeconds -= deltaSeconds;
        _effects.RemoveAll(e => e.RemainingSeconds <= 0f);
    }

    public void Detect(WorldSnapshot? previous, WorldSnapshot current)
    {
        if (previous is null)
            return;

        foreach (var state in current.WallBlockStates)
        {
            var before = previous.WallBlockStates.FirstOrDefault(s => s.Id == state.Id);
            if (before is { Breached: true } && !state.Breached)
            {
                var block = current.WallBlocks.FirstOrDefault(b => b.Id == state.Id);
                if (block is not null)
                    _effects.Add(new TransientEffect(EffectKind.Weld, block.Position, WeldEffectSeconds));
            }
        }

        foreach (var state in current.OreDepositStates)
        {
            var before = previous.OreDepositStates.FirstOrDefault(s => s.DepositId == state.DepositId);
            // Only the moment a block finally comes apart, not every tick the flame is on it -
            // cutting is continuous now, and the flame itself is what shows the work in progress.
            if (before is not null && before.Hp > 0f && state.Hp <= 0f)
            {
                var deposit = current.OreDeposits.FirstOrDefault(d => d.Id == state.DepositId);
                if (deposit is not null)
                    _effects.Add(new TransientEffect(EffectKind.Cut, deposit.Position, CutEffectSeconds));
            }
        }

        foreach (var state in current.SystemStates)
        {
            var before = previous.SystemStates.FirstOrDefault(s => s.DeviceId == state.DeviceId);
            if (before is { Damaged: true } && !state.Damaged)
            {
                var device = current.SystemDevices.FirstOrDefault(d => d.Id == state.DeviceId);
                if (device is not null)
                    _effects.Add(new TransientEffect(EffectKind.Repair, device.Position, RepairEffectSeconds));
            }
        }
    }
}
