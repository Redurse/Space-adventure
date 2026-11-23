using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Audio;

// The ship's own soundscape - what makes a dark room feel inhabited. It is fed the newest snapshot every frame and plays, on top of the one-shot effects of
// Game1.Sound.cs:
//   * beds that never stop: the drone of the ship, the air in the ducts, the reactor (louder the nearer you stand to it);
//   * the state of the ship: the power cutting out and coming back, an alarm in a fight or a room without air, a hiss at a breach, sparks from a shorted junction;
//   * the people: footsteps on the deck plates (none in a vacuum), the welder, the cutter, a jetpack - each for whoever is near enough to be heard;
//   * now and then something unexplained: a creak of the hull, a drip, a knock in a pipe, a blow far away - more often the worse the hull is hurt.
// Outside in a suit there is none of it: space is silent.
public sealed partial class ShipAmbience : IDisposable
{
    private const string HumLoop = "ship_hum_loop";
    private const string VentLoop = "vent_loop";

    private readonly GameSounds _sounds;
    private readonly GameAudioEngine _engine;
    private readonly Random _random = new();

    private readonly LoopChannel _hum = new();
    private readonly LoopChannel _vent = new();
    private readonly LoopChannel _reactor = new();
    private readonly LoopChannel _alarm = new();
    private bool _loopsCreated;

    private double _lastNow = double.NaN;
    private long _lastTick = long.MinValue;
    private bool? _hadPower;

    private readonly RandomEventTimer _creak;
    private readonly RandomEventTimer _drip;
    private readonly RandomEventTimer _thud;
    private readonly RandomEventTimer _knock;
    private readonly RandomEventTimer _spark;
    private readonly RandomEventTimer _hiss;

    public ShipAmbience(GameSounds sounds, GameAudioEngine engine)
    {
        _sounds = sounds;
        _engine = engine;
        _creak = new RandomEventTimer(_random, 18, 50);
        _drip = new RandomEventTimer(_random, 5, 20);
        _thud = new RandomEventTimer(_random, 45, 130);
        _knock = new RandomEventTimer(_random, 70, 200);
        _spark = new RandomEventTimer(_random, 2.5, 7);
        _hiss = new RandomEventTimer(_random, 5, 8);
    }

    private void EnsureLoops()
    {
        if (_loopsCreated)
            return;
        _loopsCreated = true;
        _hum.Loop = _sounds.CreateLoop(HumLoop, 1f);
        _vent.Loop = _sounds.CreateLoop(VentLoop, 1f);
        _reactor.Loop = _sounds.CreateLoop(GameSounds.ReactorHumLoop, 1f);
        _alarm.Loop = _sounds.CreateLoop(GameSounds.AlarmLoop, 1f);
    }

    // Everything fades out: the round is over, or the player is in the menu.
    public void Stop()
    {
        _hum.Silence();
        _vent.Silence();
        _reactor.Silence();
        _alarm.Silence();
        StopCharacterLoops();
        _hadPower = null;
        _lastTick = long.MinValue;
        _lastNow = double.NaN;
    }

    public void Dispose()
    {
        Stop();
        _hum.Dispose();
        _vent.Dispose();
        _reactor.Dispose();
        _alarm.Dispose();
        DisposeCharacterLoops();
    }

    // Called every frame during a round with the newest snapshot (null: no round).
    public void Update(WorldSnapshot? snapshot, int localPlayerId, double now)
    {
        if (snapshot is null)
        {
            Stop();
            return;
        }
        EnsureLoops();
        var dt = double.IsNaN(_lastNow) ? 0f : (float)Math.Clamp(now - _lastNow, 0.0, 0.25);
        _lastNow = now;
        var bus = _engine.SoundVolume * _engine.DuckMultiplier;

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == localPlayerId);
        var outside = me?.IsOutside ?? false;
        var powered = snapshot.Power.ReactorRunning;

        // The ship's lights going out and coming back are heard as well as seen.
        if (_hadPower is bool had && had != powered && !outside)
            _sounds.Play(powered ? "power_up" : "power_down", now, volume: 0.55f, pitchSpread: 0.02f);
        _hadPower = powered;

        var roomId = me is null ? null : TileMovement.RoomIdAt(snapshot.Rooms, new Vec2(me.X, me.Y));
        var roomAir = roomId is null ? 100f : snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == roomId)?.Oxygen ?? 100f;

        // ---- the beds ----
        var inside = !outside;
        _hum.Update(inside ? (powered ? 0.30f : 0.07f) : 0f, dt, bus, 1.4f);
        _vent.Update(inside ? (powered ? 0.14f : 0.03f) + (roomAir < 30f ? 0.12f : 0f) : 0f, dt, bus, 1.2f);

        var reactorNear = 0f;
        if (inside && me is not null && powered)
        {
            var distance = (float)(new Vec2(me.X, me.Y) - snapshot.ReactorBlock.Position).Length();
            var load = snapshot.Power.ReactorMaxOutput > 0f ? Math.Clamp(snapshot.Power.ReactorDelivered / snapshot.Power.ReactorMaxOutput, 0f, 1f) : 0.5f;
            reactorNear = 0.34f * AmbienceMath.Attenuation(distance, 2f, 22f) * (0.55f + 0.45f * load);
        }
        _reactor.Update(reactorNear, dt, bus, 1.5f);

        // ---- the alarm: a fight, a room without air, a junction burning out ----
        var overheating = snapshot.Wiring.OverheatingJunctions is { Count: > 0 };
        var emergency = inside && (snapshot.Voyage.IsInBattle || roomAir < 30f || overheating);
        _alarm.Update(emergency ? 0.2f : 0f, dt, bus, 2.5f);

        UpdateCharacterLoops(snapshot, me, dt, bus);

        // The rest is decided once per new snapshot, not once per frame.
        if (snapshot.Tick == _lastTick)
            return;
        var firstSnapshot = _lastTick == long.MinValue;
        _lastTick = snapshot.Tick;
        if (firstSnapshot)
        {
            ResetCharacterTracking(snapshot);
            return;
        }

        UpdateFootsteps(snapshot, me, roomAir, now);
        if (inside && me is not null)
            UpdateRandomEvents(snapshot, me, powered, overheating, now);
    }

    // How hurt the hull is, 0 (sound) to 1 (gone): drives how restless the ship is.
    private static float HullDamage(WorldSnapshot snapshot)
    {
        var states = snapshot.WallBlockStates;
        if (states.Count == 0)
            return 0f;
        var sum = 0f;
        foreach (var state in states)
            sum += Math.Clamp(state.Fraction, 0f, 1f);
        return 1f - sum / states.Count;
    }

    private void UpdateRandomEvents(WorldSnapshot snapshot, CharacterState me, bool powered, bool overheating, double now)
    {
        var damage = HullDamage(snapshot);
        var tension = Math.Clamp(damage * 1.5f + (snapshot.Voyage.IsInBattle ? 0.5f : 0f), 0f, 1f);
        float RandomPan() => (float)(_random.NextDouble() * 1.4 - 0.7);
        string Pick(string stem, int count) => $"{stem}_{_random.Next(1, count + 1)}";

        if (_creak.Due(now, tension))
            _sounds.Play(Pick("creak", 4), now, volume: 0.3f + 0.2f * (float)_random.NextDouble(), pitchSpread: 0.1f, pan: RandomPan());
        if (_drip.Due(now, damage))
            _sounds.Play(Pick("drip", 3), now, volume: 0.16f + 0.14f * (float)_random.NextDouble(), pitchSpread: 0.12f, pan: RandomPan());
        if (_thud.Due(now, tension))
            _sounds.Play(Pick("thud_far", 2), now, volume: 0.3f + 0.15f * (float)_random.NextDouble(), pitchSpread: 0.08f, pan: RandomPan());
        if (_knock.Due(now, damage))
            _sounds.Play("pipe_knock", now, volume: 0.22f, pitchSpread: 0.06f, pan: RandomPan());
        if (overheating && _spark.Due(now, 0f))
            _sounds.Play(Pick("spark_crackle", 2), now, volume: 0.32f, pitchSpread: 0.1f, pan: RandomPan());

        // A hole in the hull hisses, louder the nearer you are to it.
        var nearest = NearestBreach(snapshot, me);
        if (nearest is { } hole && _hiss.Due(now, 0f))
        {
            var gain = AmbienceMath.Attenuation((float)hole.Distance, 3f, 32f);
            if (gain > 0.02f)
                _sounds.Play(GameSounds.SteamVent, now, volume: 0.45f * gain, pitchSpread: 0.05f, pan: AmbienceMath.Pan((float)(hole.Position.X - me.X)));
        }
    }

    private static (Vec2 Position, double Distance)? NearestBreach(WorldSnapshot snapshot, CharacterState me)
    {
        if (snapshot.WallBlockStates.Count == 0)
            return null;
        HashSet<string>? breached = null;
        foreach (var state in snapshot.WallBlockStates)
            if (state.Breached)
                (breached ??= new HashSet<string>()).Add(state.Id);
        if (breached is null)
            return null;
        var here = new Vec2(me.X, me.Y);
        (Vec2, double)? best = null;
        foreach (var block in snapshot.WallBlocks)
        {
            if (!breached.Contains(block.Id))
                continue;
            var distance = (block.Position - here).Length();
            if (best is null || distance < best.Value.Item2)
                best = (block.Position, distance);
        }
        return best;
    }
}

// One looping sound whose loudness follows a target smoothly and which only occupies the mixer while it is audible.
public sealed class LoopChannel : IDisposable
{
    private const float AudibleLevel = 0.002f;

    public SoundLoop? Loop;
    private float _level;

    public float Level => _level;

    public void Update(float target, float dt, float bus, float ratePerSecond)
    {
        _level = AmbienceMath.Approach(_level, target, dt, ratePerSecond);
        if (Loop is null)
            return;
        if (_level > AudibleLevel)
        {
            Loop.Volume = _level * bus;
            Loop.Play();
        }
        else
        {
            _level = 0f;
            Loop.Stop();
        }
    }

    // Straight to silence, without a fade: the round has ended.
    public void Silence()
    {
        _level = 0f;
        Loop?.Stop();
    }

    public void Dispose()
    {
        Loop?.Dispose();
        Loop = null;
    }
}
