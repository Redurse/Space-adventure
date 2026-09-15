using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The corner-sweep raycasting both VisibilityMask (the player's own sight) and RoomLighting (ship
// power/lamp lighting) need: a fan of rays from an eye point out to the nearest wall, with an extra
// ray nudged either side of every wall corner so the shadow's edge lands exactly on the corner
// instead of stair-stepping across the arc samples.
public static class ShadowCast
{
    public const int ArcSamples = 72;
    private const float CornerNudge = 0.0008f;

    // Fills `offsets` with angles measured from `start`, sorted ascending, reusing the list's
    // backing array across calls (both callers rebuild this every frame). full=true means the sweep
    // wraps all the way around, so corner rays never get clipped against `span`.
    public static void CollectRayOffsets(List<float> offsets, IReadOnlyList<WallSegment> walls, Vector2 eye,
        float start, float span, bool full)
    {
        offsets.Clear();
        for (var i = 0; i <= ArcSamples; i++)
            offsets.Add(span * i / ArcSamples);

        foreach (var wall in walls)
        {
            AddCorner(offsets, wall.Ax - eye.X, wall.Ay - eye.Y, start, span, full);
            AddCorner(offsets, wall.Bx - eye.X, wall.By - eye.Y, start, span, full);
        }

        offsets.Sort();
    }

    private static void AddCorner(List<float> offsets, float dx, float dy, float start, float span, bool full)
    {
        var offset = Wrap(MathF.Atan2(dy, dx) - start);
        if (!full && offset > span)
            return;

        if (offset > CornerNudge)
            offsets.Add(offset - CornerNudge);
        offsets.Add(offset);
        if (full || offset + CornerNudge <= span)
            offsets.Add(offset + CornerNudge);
    }

    // Nearest wall the ray meets, or maxDistance if it reaches that far unobstructed.
    public static float Cast(Vector2 eye, Vector2 direction, IReadOnlyList<WallSegment> walls, float maxDistance)
    {
        var best = maxDistance;
        foreach (var wall in walls)
        {
            var sx = wall.Bx - wall.Ax;
            var sy = wall.By - wall.Ay;
            var denominator = direction.X * sy - direction.Y * sx;
            if (MathF.Abs(denominator) < 1e-6f)
                continue;

            var qx = wall.Ax - eye.X;
            var qy = wall.Ay - eye.Y;
            var t = (qx * sy - qy * sx) / denominator;
            if (t <= 1e-4f || t >= best)
                continue;

            var u = (qx * direction.Y - qy * direction.X) / denominator;
            if (u < -1e-4f || u > 1f + 1e-4f)
                continue;

            best = t;
        }
        return best;
    }

    // Whether a wall stands anywhere on the straight line between two arbitrary points - the voice
    // chat occlusion check (VoicePlayback), not an angular sweep like the two callers above. Reuses
    // Cast unchanged: casting from `a` straight at `b` and checking whether the nearest hit lands
    // short of `b` itself is exactly "is something in the way".
    public static bool IsBlocked(Vector2 a, Vector2 b, IReadOnlyList<WallSegment> walls)
    {
        var toB = b - a;
        var distance = toB.Length();
        if (distance < 1e-4f)
            return false;
        var hit = Cast(a, toB / distance, walls, distance);
        return hit < distance - 0.05f;
    }

    public static float Wrap(float angle)
    {
        const float twoPi = MathF.PI * 2f;
        angle %= twoPi;
        return angle < 0 ? angle + twoPi : angle;
    }

    // Direct user report ("проблема из-за низкого фпс") - both callers (RoomLighting, once per lamp;
    // VisibilityMask, once or twice per frame for the player's own sight) pass Cast the exact same
    // `radius` as its own maxDistance, and Cast already rejects any hit at distance >= maxDistance -
    // so a wall whose CLOSEST point to `point` is already farther than `radius` could never register
    // a hit from this point no matter which direction is tried. Filtering those out before
    // CollectRayOffsets/Cast ever run over them is exact, not an approximation - it only skips work
    // that was always going to be thrown away. This matters most exactly where the original slowdown
    // was found: a single room's own lamp (a few units' reach) shadow-cast against the WHOLE docked
    // ship+station's combined wall list (hundreds of segments) is almost entirely wasted work once
    // this filter is in place, cut down to just the handful of walls actually near that lamp.
    public static void FilterNearby(List<WallSegment> into, IReadOnlyList<WallSegment> walls, Vector2 point, float radius)
    {
        into.Clear();
        var radiusSquared = radius * radius;
        foreach (var wall in walls)
        {
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
