using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

/// <summary>Hostile hulls, baked once per class.</summary>
///
/// What was there was an arrowhead in flat colour with a black outline and a few circles on it. It
/// read as a marker for a ship rather than as a ship. Four things fix that and none of them is more
/// polygons:
///
///   * plating. A hull is panels bolted together, so it needs seams - and a seam needs a lit lip on
///     one side, or it is a printed line rather than a joint;
///   * parts that break the outline. A block drawn inside the silhouette is paint; one that sticks
///     out past it is a part, and the eye counts parts to decide it is looking at a ship. Engine
///     pods on outriggers, sponsons on pylons, a mast - all of them hang outside the hull;
///   * lit windows and running lights. The one thing separating a wreck from a crewed vessel, and
///     the only place saturated colour belongs on a hull this dark;
///   * a different shape per class. A raider, a freighter and a gunship do different jobs, and three
///     identical outlines make a fleet read as one enemy repeated.
///
/// Baked with the nose along +X and rotated at draw time, so nothing here may depend on which way
/// the ship happens to be pointing.
public sealed class EnemyHullSkin : IDisposable
{
    public const int CanvasSize = 256;
    public static readonly Vector2 Origin = new(CanvasSize / 2f, CanvasSize / 2f);

    private const float Cx = CanvasSize / 2f;
    private const float Cy = CanvasSize / 2f;
    private const float L = 220f;                   // hull length in texels

    private readonly GraphicsDevice _graphics;
    private readonly Dictionary<(EnemyShipClass Kind, bool Retreating), Texture2D> _cache = new();

    public EnemyHullSkin(GraphicsDevice graphics) => _graphics = graphics;

    public void Dispose()
    {
        foreach (var texture in _cache.Values)
            texture.Dispose();
        _cache.Clear();
    }

    public Texture2D Get(EnemyShipClass kind, bool retreating)
    {
        if (_cache.TryGetValue((kind, retreating), out var cached))
            return cached;
        var baked = Bake(kind, retreating);
        _cache[(kind, retreating)] = baked;
        return baked;
    }

    // ---------------------------------------------------------------- shape

    private static (float T, float W)[] Shape(EnemyShipClass kind) => kind switch
    {
        EnemyShipClass.Freighter => new[]
        {
            (0.00f, 0.30f), (0.08f, 0.34f), (0.62f, 0.34f), (0.74f, 0.26f), (0.88f, 0.20f), (1.00f, 0.12f),
        },
        EnemyShipClass.Gunship => new[]
        {
            (0.00f, 0.26f), (0.10f, 0.34f), (0.34f, 0.32f), (0.60f, 0.28f), (0.82f, 0.20f), (1.00f, 0.06f),
        },
        // Broad amidships where its 3 turrets sit, a proper warship's beam rather than the gunship's
        // narrow wedge - a Corvette-sized hull reads as bigger than everything else in the fleet.
        EnemyShipClass.Frigate => new[]
        {
            (0.00f, 0.24f), (0.10f, 0.36f), (0.42f, 0.40f), (0.68f, 0.34f), (0.86f, 0.22f), (1.00f, 0.08f),
        },
        _ => new[]
        {
            (0.00f, 0.20f), (0.12f, 0.30f), (0.30f, 0.26f), (0.55f, 0.20f), (0.78f, 0.12f), (1.00f, 0.02f),
        },
    };

    private static float HalfAt((float T, float W)[] shape, float t)
    {
        for (var i = 0; i < shape.Length - 1; i++)
        {
            var (t0, w0) = shape[i];
            var (t1, w1) = shape[i + 1];
            if (t < t0 || t > t1)
                continue;
            var u = (t - t0) / (t1 - t0);
            return w0 + (w1 - w0) * (u * u * (3f - 2f * u));
        }
        return shape[^1].W;
    }

    private static Color Mix(Color a, Color b, float t)
    {
        t = MathHelper.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    private static void Poly(PixelCanvas c, (float X, float Y)[] pts, Color colour)
    {
        float minY = pts[0].Y, maxY = pts[0].Y;
        foreach (var p in pts)
        {
            minY = MathF.Min(minY, p.Y);
            maxY = MathF.Max(maxY, p.Y);
        }
        for (var y = (int)minY; y <= (int)maxY; y++)
        {
            var xs = new List<float>();
            for (var i = 0; i < pts.Length; i++)
            {
                var (x0, y0) = pts[i];
                var (x1, y1) = pts[(i + 1) % pts.Length];
                if ((y0 <= y && y < y1) || (y1 <= y && y < y0))
                    xs.Add(x0 + (x1 - x0) * (y - y0) / (y1 - y0));
            }
            xs.Sort();
            for (var k = 0; k + 1 < xs.Count; k += 2)
            for (var x = (int)xs[k]; x <= (int)xs[k + 1]; x++)
                c.Px(x, y, colour);
        }
    }

    // ---------------------------------------------------------------- the hull

    private Texture2D Bake(EnemyShipClass kind, bool retreating)
    {
        var c = new PixelCanvas(CanvasSize, CanvasSize);
        float stern = Cx - L * 0.46f, bow = Cx + L * 0.54f;

        var baseColour = kind switch
        {
            EnemyShipClass.Freighter => new Color(86, 82, 74),
            EnemyShipClass.Gunship => new Color(74, 78, 92),
            EnemyShipClass.Frigate => new Color(62, 80, 62),
            _ => new Color(96, 58, 54),
        };
        if (retreating)
            baseColour = Mix(baseColour, new Color(60, 54, 46), 0.5f);
        var lit = Mix(baseColour, Color.White, 0.22f);
        var dark = Mix(baseColour, Color.Black, 0.34f);
        var deep = Mix(baseColour, Color.Black, 0.55f);
        var shape = Shape(kind);

        // The hull, swept along its length so the plating follows the shape rather than being a grid
        // laid over the top of it.
        for (var x = (int)stern; x <= (int)bow; x++)
        {
            var t = (x - stern) / (bow - stern);
            var half = HalfAt(shape, t) * L;
            for (var y = (int)(Cy - half); y <= (int)(Cy + half); y++)
            {
                var d = MathF.Abs(y - Cy) / MathF.Max(1f, half);
                var col = Mix(lit, baseColour, MathF.Min(1f, d * 1.35f));       // domed across the beam
                col = Mix(col, deep, MathF.Max(0f, d - 0.72f) / 0.28f * 0.8f);
                if (x % 26 == 0) col = Mix(col, deep, 0.55f);
                else if (x % 26 == 1) col = Mix(col, lit, 0.35f);
                if (y % 22 == 0) col = Mix(col, deep, 0.40f);
                c.Px(x, y, col);
            }
        }

        void Spine(float x0, float x1, float half, Color col) =>
            c.Rect(x0, Cy - half, x1 - x0, half * 2f, col);

        switch (kind)
        {
            case EnemyShipClass.Freighter:
                // Container racks down the spine and engines on outriggers: a hull built to carry.
                for (var k = 0; k < 4; k++)
                {
                    var x0 = stern + 34f + k * 34f;
                    c.Rect(x0, Cy - 30f, 26f, 60f, dark);
                    c.Rect(x0 + 2f, Cy - 27f, 22f, 24f, Mix(new Color(132, 108, 70), baseColour, 0.25f));
                    c.Rect(x0 + 2f, Cy + 3f, 22f, 24f, Mix(new Color(92, 104, 118), baseColour, 0.25f));
                }
                Spine(bow - 62f, bow - 22f, 20f, Mix(baseColour, Color.White, 0.10f));   // the bridge
                c.Rect(bow - 54f, Cy - 10f, 20f, 20f, new Color(46, 108, 132));
                c.Rect(bow - 52f, Cy - 8f, 8f, 6f, new Color(168, 226, 244));
                for (var s = -1; s <= 1; s += 2)
                {
                    var oy = Cy + s * 84f;
                    c.Rect(stern + 26f, MathF.Min(Cy + s * 58f, oy), 14f, MathF.Abs(oy - (Cy + s * 58f)), dark);
                    c.Rect(stern + 2f, oy - 14f, 42f, 28f, dark);
                    c.Rect(stern + 6f, oy - 10f, 34f, 20f, Mix(baseColour, Color.Black, 0.18f));
                    c.Disc(stern + 10f, oy, 11f, deep);
                    c.Disc(stern + 12f, oy, 7f, retreating ? new Color(170, 112, 52) : new Color(255, 150, 60));
                }
                break;

            case EnemyShipClass.Gunship:
                Poly(c, new[] { (bow - 4f, Cy), (bow - 52f, Cy - 34f), (bow - 66f, Cy), (bow - 52f, Cy + 34f) },
                    Mix(baseColour, Color.White, 0.12f));
                Poly(c, new[] { (bow - 10f, Cy), (bow - 48f, Cy - 24f), (bow - 58f, Cy), (bow - 48f, Cy + 24f) },
                    dark);
                for (var s = -1; s <= 1; s += 2)
                {
                    var oy = Cy + s * 74f;
                    c.Rect(Cx - 34f, MathF.Min(Cy + s * 46f, oy), 28f, MathF.Abs(oy - (Cy + s * 46f)), dark);
                    c.Rect(Cx - 46f, oy - 17f, 62f, 34f, dark);
                    c.Rect(Cx - 42f, oy - 13f, 54f, 26f, Mix(baseColour, Color.White, 0.08f));
                    c.Rect(Cx + 4f, oy - 6f, 54f, 12f, Mix(baseColour, Color.Black, 0.24f));   // the gun
                    c.Rect(Cx + 46f, oy - 9f, 10f, 18f, deep);
                    c.Disc(Cx - 34f, oy, 9f, deep);
                }
                Spine(stern + 26f, Cx + 30f, 22f, Mix(baseColour, Color.Black, 0.18f));
                c.Rect(Cx - 6f, Cy - 14f, 36f, 28f, new Color(44, 96, 122));
                c.Rect(Cx - 2f, Cy - 11f, 14f, 7f, new Color(170, 224, 242));
                for (var s = -1; s <= 1; s += 2)
                {
                    c.Disc(stern + 12f, Cy + s * 22f, 15f, deep);
                    c.Disc(stern + 14f, Cy + s * 22f, 9f,
                        retreating ? new Color(96, 128, 160) : new Color(120, 190, 255));
                    c.Disc(stern + 16f, Cy + s * 22f, 5f, new Color(232, 246, 255));
                }
                break;

            case EnemyShipClass.Frigate:
                // Twin magnetic sponsons on the beam (orange muzzle heat) and one dorsal laser mount
                // forward of them (cyan lens) - the hull's fixed 2-magnetic/1-laser loadout, worn on
                // the outside the same way the raider/gunship's own single gun always is.
                for (var s = -1; s <= 1; s += 2)
                {
                    var oy = Cy + s * 80f;
                    c.Rect(Cx - 20f, MathF.Min(Cy + s * 50f, oy), 24f, MathF.Abs(oy - (Cy + s * 50f)), dark);
                    c.Rect(Cx - 34f, oy - 16f, 60f, 32f, dark);
                    c.Rect(Cx - 30f, oy - 12f, 52f, 24f, Mix(baseColour, Color.White, 0.10f));
                    c.Rect(Cx + 10f, oy - 6f, 44f, 12f, Mix(baseColour, Color.Black, 0.24f));
                    c.Disc(Cx + 50f, oy, 8f, retreating ? new Color(170, 112, 52) : new Color(255, 150, 60));
                }
                Spine(Cx + 40f, bow - 20f, 22f, Mix(baseColour, Color.Black, 0.16f));
                c.Rect(bow - 46f, Cy - 15f, 26f, 30f, dark);
                c.Rect(bow - 42f, Cy - 11f, 18f, 22f, Mix(baseColour, Color.White, 0.10f));
                c.Disc(bow - 30f, Cy, 9f, deep);
                c.Disc(bow - 28f, Cy, 5f, retreating ? new Color(90, 150, 170) : new Color(120, 220, 255));
                Spine(stern + 24f, Cx - 30f, 20f, Mix(baseColour, Color.Black, 0.18f));
                c.Rect(Cx - 20f, Cy - 16f, 32f, 32f, new Color(44, 96, 90));
                c.Rect(Cx - 15f, Cy - 12f, 12f, 8f, new Color(170, 240, 220));
                for (var s = -1; s <= 1; s += 2)
                {
                    c.Disc(stern + 12f, Cy + s * 20f, 12f, deep);
                    c.Disc(stern + 14f, Cy + s * 20f, 7f, retreating ? new Color(96, 128, 160) : new Color(120, 190, 255));
                }
                break;

            default:
                // Scavenged: an off-centre spine gun, a plate riveted over a hole, a mast and a torn
                // fin - all of it asymmetric, because nobody built this on purpose.
                Spine(stern + 30f, bow - 30f, 16f, dark);
                Spine(stern + 34f, bow - 34f, 12f, Mix(baseColour, Color.Black, 0.20f));
                c.Rect(Cx - 10f, Cy - 46f, 56f, 18f, dark);
                for (var i = 0; i < 6; i++)
                    c.Disc(Cx - 4f + i * 9f, Cy - 37f, 2.2f, deep);
                c.Rect(Cx + 20f, Cy - 6f, bow - 6f - (Cx + 20f), 12f, Mix(baseColour, Color.Black, 0.22f));
                c.Rect(bow - 22f, Cy - 9f, 10f, 18f, deep);
                c.Rect(Cx - 30f, Cy - 78f, 8f, 44f, dark);
                c.Rect(Cx - 34f, Cy - 84f, 16f, 10f, Mix(baseColour, Color.White, 0.10f));
                c.Disc(Cx - 26f, Cy - 88f, 5f, new Color(255, 90, 70));
                Poly(c, new[]
                {
                    (stern + 20f, Cy + 44f), (stern + 60f, Cy + 40f), (stern + 74f, Cy + 76f), (stern + 30f, Cy + 70f),
                }, dark);
                c.Disc(stern + 14f, Cy, 20f, deep);
                c.Disc(stern + 16f, Cy, 13f, retreating ? new Color(176, 106, 48) : new Color(255, 128, 54));
                c.Disc(stern + 18f, Cy, 7f, new Color(255, 226, 176));
                break;
        }

        // Running lights: red to port, green to starboard.
        foreach (var (s, col) in new[] { (-1, new Color(255, 70, 70)), (1, new Color(90, 255, 120)) })
        foreach (var t in new[] { 0.28f, 0.62f })
        {
            var x = stern + (bow - stern) * t;
            var half = HalfAt(shape, t) * L;
            c.Disc(x, Cy + s * (half - 4f), 3f, col);
        }

        Outline(c);
        return c.ToTexture(_graphics);
    }

    // A dark ring round the finished silhouette, so the hull reads against the starfield.
    private static void Outline(PixelCanvas c)
    {
        var ring = new List<(int X, int Y)>();
        for (var y = 0; y < CanvasSize; y++)
        for (var x = 0; x < CanvasSize; x++)
        {
            if (c.Alpha(x, y) > 0.5f)
                continue;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                if (c.Alpha(x + dx, y + dy) > 0.5f)
                {
                    ring.Add((x, y));
                    break;
                }
            }
        }
        foreach (var (x, y) in ring)
            c.Px(x, y, new Color(12, 12, 16), 0.85f);
    }
}
