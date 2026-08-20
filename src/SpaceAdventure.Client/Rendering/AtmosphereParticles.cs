using System;
using System.Collections.Generic;
using System.Linq;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

public enum AtmosphereKind
{
    Steam,
    Spark,
    Ember,
}

// One drifting wisp/spark. Unlike TransientEffect (a one-shot flash for a moment that already
// happened - a weld landing, a repair finishing), these keep spawning for as long as the state that
// causes them holds: a breach still open, a system still damaged, a reactor still starved of fuel.
public sealed class AtmosphereParticle
{
    public AtmosphereKind Kind { get; }
    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; set; }
    public float Size { get; }
    public float TotalSeconds { get; }
    public float RemainingSeconds { get; set; }

    public AtmosphereParticle(AtmosphereKind kind, Vec2 position, Vec2 velocity, float size, float totalSeconds)
    {
        Kind = kind;
        Position = position;
        Velocity = velocity;
        Size = size;
        TotalSeconds = totalSeconds;
        RemainingSeconds = totalSeconds;
    }

    // 0 right when it spawns, 1 right before it's removed - same convention as TransientEffect.
    public float Progress => 1f - RemainingSeconds / TotalSeconds;
}

// Ambient, continuous particles read straight off the current snapshot's state (a breach that's
// still open, a system that's still damaged, a reactor that's still starved) rather than a
// transition between two snapshots - EffectTracker already owns "something just happened", this
// owns "something is still wrong". Spawn rates are capped by a per-emitter cooldown instead of a
// chance roll every Update tick, so the rate a room fills with steam doesn't depend on frame rate.
public sealed class AtmosphereField
{
    private const float SteamInterval = 0.12f;
    private const float SparkInterval = 0.35f;
    private const float EmberInterval = 0.18f;
    private const float ReactorCriticalFuelFraction = 0.15f;
    // A runaway emitter (many breaches at once) still can't blow the frame budget - mutable (not
    // const) so the Settings screen's own "Максимальное количество частиц" slider (Game1.Settings.cs)
    // can lower it on a slower machine, same knob Barotrauma's own graphics tab exposes.
    public static int MaxParticles = 400;

    private readonly List<AtmosphereParticle> _particles = new();
    private readonly Dictionary<string, float> _cooldowns = new();
    private readonly Random _random = new();

    public IEnumerable<AtmosphereParticle> Particles => _particles;

    public void Step(float deltaSeconds, WorldSnapshot? snapshot)
    {
        foreach (var particle in _particles)
        {
            particle.RemainingSeconds -= deltaSeconds;
            particle.Position += particle.Velocity * deltaSeconds;
            // Steam rises and accelerates a little as it goes, the way a real gas plume does;
            // sparks and embers just fly/drift on their own initial velocity.
            if (particle.Kind == AtmosphereKind.Steam)
                particle.Velocity += new Vec2(0f, -1.4f) * deltaSeconds;
        }
        _particles.RemoveAll(p => p.RemainingSeconds <= 0f);

        foreach (var key in _cooldowns.Keys.ToList())
        {
            var remaining = _cooldowns[key] - deltaSeconds;
            if (remaining <= 0f)
                _cooldowns.Remove(key);
            else
                _cooldowns[key] = remaining;
        }

        if (snapshot is null || _particles.Count >= MaxParticles)
            return;

        foreach (var state in snapshot.WallBlockStates)
        {
            if (!state.Breached)
                continue;
            var block = snapshot.WallBlocks.FirstOrDefault(b => b.Id == state.Id);
            if (block is not null)
                EmitIfReady($"breach:{state.Id}", SteamInterval, () => SpawnSteam(block.Position));
        }

        foreach (var state in snapshot.SystemStates)
        {
            if (!state.Damaged)
                continue;
            var device = snapshot.SystemDevices.FirstOrDefault(d => d.Id == state.DeviceId);
            if (device is not null)
                EmitIfReady($"spark:{state.DeviceId}", SparkInterval, () => SpawnSpark(device.Position));
        }

        // The reactor's own dying embers - present even with the room lights fully out, same trigger
        // RoomLighting's reactor glow flickers on (Game1.BuildShipRoomLights).
        var reactorRoom = snapshot.Rooms.FirstOrDefault(r => r.Id.Contains("reactor") || r.Id.Contains("engine"));
        if (reactorRoom is not null && snapshot.Power.ReactorMaxFuel > 0f && snapshot.Power.ReactorOutput > 0f
            && snapshot.Power.ReactorFuel / snapshot.Power.ReactorMaxFuel < ReactorCriticalFuelFraction)
            EmitIfReady("reactor-embers", EmberInterval, () => SpawnEmber(reactorRoom.Center));
    }

    private void EmitIfReady(string key, float interval, Action spawn)
    {
        if (_cooldowns.ContainsKey(key))
            return;
        spawn();
        _cooldowns[key] = interval;
    }

    private void SpawnSteam(Vec2 position)
    {
        var angle = _random.NextDouble() * MathF.PI * 2f;
        var speed = 0.5f + (float)_random.NextDouble() * 0.8f;
        var velocity = new Vec2(MathF.Cos((float)angle), MathF.Sin((float)angle)) * speed;
        _particles.Add(new AtmosphereParticle(AtmosphereKind.Steam, position, velocity,
            0.22f + (float)_random.NextDouble() * 0.3f, 1.1f + (float)_random.NextDouble() * 0.7f));
    }

    private void SpawnSpark(Vec2 position)
    {
        var angle = _random.NextDouble() * MathF.PI * 2f;
        var speed = 1.4f + (float)_random.NextDouble() * 2.2f;
        var velocity = new Vec2(MathF.Cos((float)angle), MathF.Sin((float)angle)) * speed;
        _particles.Add(new AtmosphereParticle(AtmosphereKind.Spark, position, velocity,
            0.05f, 0.2f + (float)_random.NextDouble() * 0.2f));
    }

    private void SpawnEmber(Vec2 roomCenter)
    {
        var jitter = new Vec2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f);
        var velocity = new Vec2(((float)_random.NextDouble() - 0.5f) * 0.3f, -0.35f - (float)_random.NextDouble() * 0.4f);
        _particles.Add(new AtmosphereParticle(AtmosphereKind.Ember, roomCenter + jitter, velocity,
            0.1f + (float)_random.NextDouble() * 0.08f, 0.7f + (float)_random.NextDouble() * 0.5f));
    }
}
