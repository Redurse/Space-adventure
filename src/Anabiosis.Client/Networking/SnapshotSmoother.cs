using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// The server makes 30 snapshots a second and the game draws 60-140 frames, so everything that moves used to be drawn in 30 Hz steps: with the ship
// doing 55 units/s that is a visible jump of a couple of units (tens of pixels at ship scale) every third frame or so - "the ship teleports". Here
// each frame shows the latest snapshot pushed forward by however far into the current server tick the frame falls, using how far each thing moved
// between the last two snapshots. Positions snap back to the real values the moment the next snapshot arrives, and since the push was roughly one
// tick of motion that lands on the same spot.
//
// Only the things that move quickly and are drawn against the stars are touched: the ship's own position and heading, other ships, shots and people.
// Everything else in the snapshot is passed through untouched (the very same objects), and the result is a new snapshot object every frame, so code
// that wants to know "is this still the same server snapshot" must not use the object itself - Tick plus the DoorStates list identify a raw one.
public sealed class SnapshotSmoother
{
    private const double TickSeconds = 1.0 / 30.0;
    // Phase correction per arrival (how fast the estimated arrival time follows the real one) and the lateness beyond which it simply re-syncs.
    private const double PhaseGain = 0.1;
    private const double ResyncSeconds = 0.1;
    // Never carry things further than this many snapshot intervals past the last real one (the server stalled, the connection hiccuped).
    private const double MaxExtrapolationIntervals = 1.5;
    // A move per snapshot bigger than this is a jump (docking, a respawn, entering a sector), not travel - shown as it is.
    private const double CharacterJumpUnits = 12;
    private const double ObjectJumpUnits = 40;

    private WorldSnapshot? _previous;
    private WorldSnapshot? _current;
    private double _arrivalPhase;
    private double _intervalSeconds = TickSeconds;

    public WorldSnapshot? Current { get; private set; }

    // Test/diagnostic switch: show raw snapshots (what the game did before smoothing existed).
    public static bool Disabled;

    // A snapshot from the server (call once per new one, in order).
    public void Accept(WorldSnapshot snapshot, double nowSeconds)
    {
        if (_current is not null && snapshot.Tick == _current.Tick)
            return;

        var ticks = _current is null ? 1 : Math.Max(1L, snapshot.Tick - _current.Tick);
        var level = Math.Max(1, snapshot.TimeAccelerationLevel);
        _intervalSeconds = Math.Clamp(ticks / (double)level * TickSeconds, TickSeconds / 2, 1.0);

        var predicted = _arrivalPhase + _intervalSeconds;
        _arrivalPhase = _current is null || Math.Abs(nowSeconds - predicted) > ResyncSeconds
            ? nowSeconds
            : predicted + (nowSeconds - predicted) * PhaseGain;

        _previous = _current;
        _current = snapshot;
    }

    // What to draw at this moment.
    public WorldSnapshot? Advance(double nowSeconds)
    {
        if (_current is null)
            return Current = null;
        if (_previous is null || Disabled)
            return Current = _current;

        var fraction = Math.Clamp((nowSeconds - _arrivalPhase) / _intervalSeconds, 0.0, MaxExtrapolationIntervals);
        if (fraction <= 0.0)
            return Current = _current;

        var shipField = Push(_previous.ShipField, _current.ShipField, fraction, _intervalSeconds * Math.Max(1, _current.TimeAccelerationLevel));
        var characters = PushCharacters(_previous.Characters, _current.Characters, fraction);
        var npcShips = PushNpcShips(_previous.NpcShips, _current.NpcShips, fraction);
        var projectiles = PushProjectiles(_previous.Projectiles, _current.Projectiles, fraction);
        return Current = _current with { ShipField = shipField, Characters = characters, NpcShips = npcShips, Projectiles = projectiles };
    }

    private static ShipFieldState Push(ShipFieldState before, ShipFieldState now, double fraction, double simSecondsPerSnapshot)
    {
        var dx = now.X - before.X;
        var dy = now.Y - before.Y;
        // The ship's own velocity says how far a tick is expected to carry it; a move far beyond that is a relocation (docking, a new system).
        var expected = Math.Sqrt((double)now.VelocityX * now.VelocityX + (double)now.VelocityY * now.VelocityY);
        if (Math.Sqrt(dx * dx + dy * dy) > expected * simSecondsPerSnapshot * 3 + 5)
            return now;
        var turn = ShortestAngle(now.RotationDegrees - before.RotationDegrees);
        if (Math.Abs(turn) > 90)
            return now with { X = now.X + dx * fraction, Y = now.Y + dy * fraction };
        return now with { X = now.X + dx * fraction, Y = now.Y + dy * fraction, RotationDegrees = now.RotationDegrees + (float)(turn * fraction) };
    }

    private static IReadOnlyList<CharacterState> PushCharacters(IReadOnlyList<CharacterState> before, IReadOnlyList<CharacterState> now, double fraction)
    {
        if (now.Count == 0)
            return now;
        var earlier = new Dictionary<int, CharacterState>();
        foreach (var c in before)
            earlier[c.PlayerId] = c;
        var result = new CharacterState[now.Count];
        for (var i = 0; i < now.Count; i++)
        {
            var c = now[i];
            result[i] = earlier.TryGetValue(c.PlayerId, out var p) && p.IsOutside == c.IsOutside && p.OnStation == c.OnStation && p.OnEnemyShip == c.OnEnemyShip
                && Distance(p.X, p.Y, c.X, c.Y) is var moved && moved > 0 && moved < CharacterJumpUnits
                    ? c with { X = c.X + (c.X - p.X) * fraction, Y = c.Y + (c.Y - p.Y) * fraction }
                    : c;
        }
        return result;
    }

    private static IReadOnlyList<NpcShipFieldState> PushNpcShips(IReadOnlyList<NpcShipFieldState> before, IReadOnlyList<NpcShipFieldState> now, double fraction)
    {
        if (now.Count == 0)
            return now;
        var earlier = new Dictionary<string, NpcShipFieldState>();
        foreach (var s in before)
            earlier[s.Id] = s;
        var result = new NpcShipFieldState[now.Count];
        for (var i = 0; i < now.Count; i++)
        {
            var s = now[i];
            result[i] = earlier.TryGetValue(s.Id, out var p) && Distance(p.X, p.Y, s.X, s.Y) is var moved && moved > 0 && moved < ObjectJumpUnits
                ? s with { X = s.X + (s.X - p.X) * fraction, Y = s.Y + (s.Y - p.Y) * fraction }
                : s;
        }
        return result;
    }

    private static IReadOnlyList<ProjectileState> PushProjectiles(IReadOnlyList<ProjectileState> before, IReadOnlyList<ProjectileState> now, double fraction)
    {
        if (now.Count == 0)
            return now;
        var earlier = new Dictionary<string, ProjectileState>();
        foreach (var s in before)
            earlier[s.Id] = s;
        var result = new ProjectileState[now.Count];
        for (var i = 0; i < now.Count; i++)
        {
            var s = now[i];
            result[i] = earlier.TryGetValue(s.Id, out var p) && Distance(p.X, p.Y, s.X, s.Y) is var moved && moved > 0 && moved < ObjectJumpUnits
                ? s with { X = s.X + (s.X - p.X) * fraction, Y = s.Y + (s.Y - p.Y) * fraction }
                : s;
        }
        return result;
    }

    private static double Distance(double x1, double y1, double x2, double y2) => Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

    private static double ShortestAngle(double degrees) => ((degrees % 360.0) + 540.0) % 360.0 - 180.0;

    public static double Now() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
