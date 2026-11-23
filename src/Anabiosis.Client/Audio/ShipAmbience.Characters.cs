using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Audio;

// The people in the soundscape: footsteps (counted from distance walked, none in a vacuum) and the working loops - welder, cutter, jetpack - for everyone near
// enough to hear. The local player is heard at full strength; the others by how far away they are and from which side.
public sealed partial class ShipAmbience
{
    // Metres of deck per step; at the walking speed of 3 units a second that is about two and a half steps a second, more when sprinting.
    private const float StrideLength = 1.2f;
    private const float LocalStepVolume = 0.5f;
    private const float OtherStepVolume = 0.42f;
    private const float VacuumAir = 5f;

    private readonly Dictionary<int, (double X, double Y)> _lastPosition = new();
    private readonly Dictionary<int, StrideCounter> _strides = new();
    private readonly Dictionary<int, float> _lastFuel = new();
    private readonly HashSet<int> _jetting = new();
    private readonly Dictionary<(int Id, int Kind), LoopChannel> _workLoops = new();
    private const int FootstepVariants = 16;
    private readonly Queue<int> _recentSteps = new();

    private enum WorkKind { Welder = 0, Cutter = 1, Jetpack = 2, Repair = 3 }

    // A new round (or the first snapshot of one): positions are recorded, nothing is heard for a jump from nowhere.
    private void ResetCharacterTracking(WorldSnapshot snapshot)
    {
        _lastPosition.Clear();
        _strides.Clear();
        _lastFuel.Clear();
        _jetting.Clear();
        foreach (var c in snapshot.Characters)
        {
            _lastPosition[c.PlayerId] = (c.X, c.Y);
            _lastFuel[c.PlayerId] = c.JetpackFuel;
        }
    }

    // 1 for the local player, falling with distance for others on the same side of the hull; 0 when it cannot be heard at all.
    private static float Audibility(CharacterState? me, CharacterState other, double near, double max)
    {
        if (me is null)
            return 0f;
        if (other.PlayerId == me.PlayerId)
            return 1f;
        if (other.IsOutside != me.IsOutside || other.OnEnemyShip != me.OnEnemyShip)
            return 0f; // another frame of reference: the coordinates do not compare
        var distance = (float)new Vec2(other.X - me.X, other.Y - me.Y).Length();
        return AmbienceMath.Attenuation(distance, (float)near, (float)max);
    }

    // Once per new snapshot: who has walked how far, and who is burning fuel.
    private void UpdateFootsteps(WorldSnapshot snapshot, CharacterState? me, float myRoomAir, double now)
    {
        var present = new HashSet<int>();
        foreach (var c in snapshot.Characters)
        {
            present.Add(c.PlayerId);

            if (c.IsOutside && !c.IsEvaAttached && _lastFuel.TryGetValue(c.PlayerId, out var fuelBefore) && c.JetpackFuel < fuelBefore - 0.002f)
                _jetting.Add(c.PlayerId);
            else
                _jetting.Remove(c.PlayerId);
            _lastFuel[c.PlayerId] = c.JetpackFuel;

            if (c.Health <= 0f || c.IsOutside || c.IsAtHelm)
            {
                _lastPosition.Remove(c.PlayerId); // not walking on a deck now
                continue;
            }

            var steps = 0;
            if (_lastPosition.TryGetValue(c.PlayerId, out var before))
            {
                var distance = (float)Math.Sqrt((c.X - before.X) * (c.X - before.X) + (c.Y - before.Y) * (c.Y - before.Y));
                // A jump of a few units is a teleport (docking, a respawn), not a run.
                if (distance is > 0.0005f and < 3f)
                {
                    if (!_strides.TryGetValue(c.PlayerId, out var counter))
                        _strides[c.PlayerId] = counter = new StrideCounter();
                    steps = counter.Advance(distance, StrideLength);
                }
            }
            _lastPosition[c.PlayerId] = (c.X, c.Y);
            if (steps == 0)
                continue;

            var audible = Audibility(me, c, 2, 22);
            if (audible <= 0.02f)
                continue;
            // No air, no sound: a step in a vacuum is felt in the boots, not heard.
            var air = me is not null && c.PlayerId == me.PlayerId ? myRoomAir : AirAt(snapshot, c);
            if (air < VacuumAir)
                continue;

            var isMe = me is not null && c.PlayerId == me.PlayerId;
            var pan = isMe || me is null ? 0f : AmbienceMath.Pan((float)(c.X - me.X));
            _sounds.Play(NextStepSound(), now, (isMe ? LocalStepVolume : OtherStepVolume) * audible, pitchSpread: 0.1f, pan: pan);
        }

        foreach (var gone in _lastPosition.Keys.Where(id => !present.Contains(id)).ToList())
        {
            _lastPosition.Remove(gone);
            _strides.Remove(gone);
            _lastFuel.Remove(gone);
            _jetting.Remove(gone);
        }
    }

    private static float AirAt(WorldSnapshot snapshot, CharacterState c)
    {
        var roomId = TileMovement.RoomIdAt(snapshot.Rooms, new Vec2(c.X, c.Y));
        return roomId is null ? 100f : snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == roomId)?.Oxygen ?? 100f;
    }

    // A different plate from the last one, so two steps in a row never sound like the same file.
    private string NextStepSound()
    {
        // Sixteen recordings; none of the last three is repeated, so a run of steps never sounds like the same file twice in a row.
        int variant;
        do variant = _random.Next(1, FootstepVariants + 1);
        while (_recentSteps.Contains(variant));
        _recentSteps.Enqueue(variant);
        while (_recentSteps.Count > 3)
            _recentSteps.Dequeue();
        return $"footstep_metal_{variant}";
    }

    // Every frame: the loops of whoever is welding, cutting or flying, fading in and out with the work.
    private void UpdateCharacterLoops(WorldSnapshot snapshot, CharacterState? me, float dt, float bus)
    {
        var wanted = new HashSet<(int, int)>();
        foreach (var c in snapshot.Characters)
        {
            if (c.Health <= 0f)
                continue;
            if (c.Welding)
                Drive(c, WorkKind.Welder, GameSounds.WeldLoop, 0.4f * Audibility(me, c, 3, 24), wanted, dt, bus);
            if (c.Cutting)
                Drive(c, WorkKind.Cutter, GameSounds.CutterLoop, 0.4f * Audibility(me, c, 3, 24), wanted, dt, bus);
            if (c.Repairing)
                Drive(c, WorkKind.Repair, GameSounds.RepairLoop, 0.35f * Audibility(me, c, 3, 24), wanted, dt, bus);
            if (_jetting.Contains(c.PlayerId))
                Drive(c, WorkKind.Jetpack, GameSounds.JetpackLoop, 0.4f * Audibility(me, c, 3, 30), wanted, dt, bus);
        }

        foreach (var key in _workLoops.Keys.ToList())
        {
            if (wanted.Contains(key))
                continue;
            var channel = _workLoops[key];
            channel.Update(0f, dt, bus, 6f);
            if (channel.Level <= 0f)
            {
                channel.Dispose();
                _workLoops.Remove(key);
            }
        }
    }

    private void Drive(CharacterState c, WorkKind kind, string sound, float target, HashSet<(int, int)> wanted, float dt, float bus)
    {
        var key = (c.PlayerId, (int)kind);
        wanted.Add(key);
        if (!_workLoops.TryGetValue(key, out var channel))
            _workLoops[key] = channel = new LoopChannel { Loop = _sounds.CreateLoop(sound, 1f) };
        channel.Update(target, dt, bus, 8f);
    }

    private void StopCharacterLoops()
    {
        foreach (var channel in _workLoops.Values)
            channel.Silence();
    }

    private void DisposeCharacterLoops()
    {
        foreach (var channel in _workLoops.Values)
            channel.Dispose();
        _workLoops.Clear();
    }
}
