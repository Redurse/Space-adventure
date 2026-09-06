using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

public enum EffectKind
{
    Weld,
    Cut,
    Repair,
    Explosion,
}

// A brief client-only visual (Barotrauma-style spark/flash) spawned at the moment a tool action
// actually lands, purely from diffing snapshots (see EffectTracker) - never sent over the wire.
// Position is in whichever coordinate space the triggering entity already lives in: ship-local
// room coordinates for Weld/Repair (WallBlock/ShipSystemDevice), AsteroidField world coordinates
// for Cut (OreDeposit) - each renderer only ever receives the effects in its own space.
public sealed class TransientEffect
{
    public EffectKind Kind { get; }
    public Vec2 Position { get; }
    public float TotalSeconds { get; }
    public float RemainingSeconds { get; set; }

    public TransientEffect(EffectKind kind, Vec2 position, float totalSeconds)
    {
        Kind = kind;
        Position = position;
        TotalSeconds = totalSeconds;
        RemainingSeconds = totalSeconds;
    }

    // 0 right when it spawns, 1 right before it's removed - drives fade-out/expansion in the renderers.
    public float Progress => 1f - RemainingSeconds / TotalSeconds;
}
