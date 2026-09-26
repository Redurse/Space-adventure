using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Direct user report ("почему игра так сильно лагает") - RoomLighting.Build calls
// ShadowCast.FilterNearby once PER LAMP (one per ship room, one per docked station room), and that
// method was a plain linear scan over the WHOLE combined ship+station wall list regardless of how
// far away the lamp actually was. That's O(lamps x walls), and both grow together with the size of
// a player's home station - a heavily built-out station makes the frame cost roughly QUADRATIC in
// its own room count, which is exactly the profile the reporting screenshot showed (docked at
// "Домашняя станция", Маска 71.8мс dominating an 89.5мс Рендер). VisibilityMask's own FilterNearby
// call is untouched - it only runs once or twice per frame for the player's own sight cone
// (ShadowCast.cs's own doc comment), nowhere near this multiplicative blowup.
//
// A uniform grid turns "scan everything" into "scan the handful of cells actually within radius":
// each wall segment is bucketed into every cell its bounding box touches at Rebuild time (once per
// frame, O(walls)), and QueryNearby then only visits the cells overlapping the query circle's own
// bounding box - the same DistanceSquared-to-segment test FilterNearby always did, just run over a
// far smaller candidate set. A wall spanning several cells can be bucketed under more than one, so
// QueryNearby dedupes via a per-wall epoch stamp (cheaper than a HashSet<WallSegment> allocation on
// every single lamp).
public sealed class WallGrid
{
    // Comparable to a typical room-light radius (RoomLighting.cs's own
    // Max(Width,Height)*0.9+1.5-ish formula) - big enough that a query rarely spans more than a
    // handful of cells, small enough that a cell's own wall list stays short even in a dense room.
    private const float CellSize = 8f;

    private readonly Dictionary<(int, int), List<int>> _cells = new();
    private IReadOnlyList<WallSegment> _walls = Array.Empty<WallSegment>();
    private int[] _seenEpoch = Array.Empty<int>();
    private int _epoch;

    // Called once per RoomLighting.Build (not once per lamp) - the whole point of this class.
    public void Rebuild(IReadOnlyList<WallSegment> walls)
    {
        _walls = walls;
        foreach (var list in _cells.Values)
            list.Clear();
        if (_seenEpoch.Length < walls.Count)
            _seenEpoch = new int[Math.Max(walls.Count, _seenEpoch.Length * 2)];
        _epoch = 0;

        for (var i = 0; i < walls.Count; i++)
        {
            var wall = walls[i];
            var minX = CellOf(MathF.Min(wall.Ax, wall.Bx));
            var maxX = CellOf(MathF.Max(wall.Ax, wall.Bx));
            var minY = CellOf(MathF.Min(wall.Ay, wall.By));
            var maxY = CellOf(MathF.Max(wall.Ay, wall.By));
            for (var cx = minX; cx <= maxX; cx++)
            for (var cy = minY; cy <= maxY; cy++)
            {
                var key = (cx, cy);
                if (!_cells.TryGetValue(key, out var list))
                    _cells[key] = list = new List<int>();
                list.Add(i);
            }
        }
    }

    // Exact same result FilterNearby's own linear scan would have produced (same DistanceSquared-
    // to-segment test), just without touching every wall in the scene to get there.
    public void QueryNearby(List<WallSegment> into, Vector2 point, float radius)
    {
        into.Clear();
        _epoch++;
        var radiusSquared = radius * radius;
        var minX = CellOf(point.X - radius);
        var maxX = CellOf(point.X + radius);
        var minY = CellOf(point.Y - radius);
        var maxY = CellOf(point.Y + radius);

        for (var cx = minX; cx <= maxX; cx++)
        for (var cy = minY; cy <= maxY; cy++)
        {
            if (!_cells.TryGetValue((cx, cy), out var indices))
                continue;
            foreach (var i in indices)
            {
                if (_seenEpoch[i] == _epoch)
                    continue;
                _seenEpoch[i] = _epoch;

                var wall = _walls[i];
                var a = new Vector2(wall.Ax, wall.Ay);
                var b = new Vector2(wall.Bx, wall.By);
                var ab = b - a;
                var lengthSquared = ab.LengthSquared();
                var t = lengthSquared > 1e-6f ? Math.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f) : 0f;
                var closest = a + ab * t;
                if (Vector2.DistanceSquared(closest, point) <= radiusSquared)
                    into.Add(wall);
            }
        }
    }

    private static int CellOf(float coord) => (int)MathF.Floor(coord / CellSize);
}
