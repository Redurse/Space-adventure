using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Turning the letter mask into plate.
//
// The first version put a one-pixel highlight along the top edge and called it a bevel. That is not
// what the reference does, and it is why those letters read as flat stencils: on real plate the
// chamfer is a third of the stroke wide, so the eye sees thickness before it sees the shape.
//
// So everything here runs off a distance field rather than off neighbouring pixels. For every pixel
// inside the metal it stores how far the nearest edge is AND which way it lies, which buys three
// things at once: a chamfer of any width; a chamfer that lights correctly on a vertical stem, on the
// slanted leg of an A and on the inside of a counter without knowing which it is on; and a flat
// plateau down the middle of every stroke for the surface texture to live on.
public static partial class MenuLogo
{
    private static readonly Color SteelBright = new(226, 236, 232);
    private static readonly Color SteelMid = new(132, 150, 150);
    private static readonly Color SteelDark = new(58, 72, 76);
    private static readonly Color SteelShadow = new(26, 34, 38);
    private static readonly Color Corrosion = new(78, 88, 68);
    private static readonly Color Rust = new(96, 74, 48);
    private static readonly Color Rim = new(214, 168, 82);
    private static readonly Color Outline = new(6, 9, 11);

    // How far the chamfer runs in from the edge. Roughly a third of the stroke, which is what makes
    // a letter look milled out of plate instead of cut out of paper.
    private const float Chamfer = 4.6f;

    // Up and to the left, and every other highlight in the mark agrees with it.
    private static readonly Vector2 Light = new(-0.55f, -0.835f);

    private static void PaintPlate(PixelCanvas c, bool[] mask)
    {
        var inward = Field(Invert(mask));   // from each metal pixel to the nearest hole
        var outward = Field(mask);          // and from each hole to the nearest metal

        PaintOutside(c, mask, outward);
        PaintSurface(c, mask, inward);
        PaintWear(c, mask, inward);
    }

    private static bool[] Invert(bool[] mask)
    {
        var flipped = new bool[mask.Length];
        for (var i = 0; i < mask.Length; i++)
            flipped[i] = !mask[i];
        return flipped;
    }

    /// <summary>Distance and direction to the nearest seed pixel, for every pixel on the canvas.</summary>
    ///
    /// Two sweeps of the usual eight-point chamfer: each pixel takes the best answer any
    /// already-visited neighbour has, plus the step it took to get there. Carrying the offset and not
    /// only the distance is the whole point - the distance says how deep into the chamfer a pixel is,
    /// and the offset says which way the edge lies, which is what lights it.
    private static (float[] D, float[] Ox, float[] Oy) Field(bool[] seed)
    {
        const float far = 1e9f;
        var n = Width * Height;
        var d2 = new float[n];
        var ox = new float[n];
        var oy = new float[n];
        for (var i = 0; i < n; i++)
            d2[i] = seed[i] ? 0f : far;

        void Relax(int x, int y, int dx, int dy)
        {
            var sx = x + dx;
            var sy = y + dy;
            if (sx < 0 || sy < 0 || sx >= Width || sy >= Height)
                return;
            var s = sy * Width + sx;
            if (d2[s] >= far)
                return;
            var nx = ox[s] + dx;
            var ny = oy[s] + dy;
            var cand = nx * nx + ny * ny;
            var i = y * Width + x;
            if (cand >= d2[i])
                return;
            d2[i] = cand;
            ox[i] = nx;
            oy[i] = ny;
        }

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            Relax(x, y, -1, 0);
            Relax(x, y, 0, -1);
            Relax(x, y, -1, -1);
            Relax(x, y, 1, -1);
        }
        for (var y = Height - 1; y >= 0; y--)
        for (var x = Width - 1; x >= 0; x--)
        {
            Relax(x, y, 1, 0);
            Relax(x, y, 0, 1);
            Relax(x, y, 1, 1);
            Relax(x, y, -1, 1);
        }

        var d = new float[n];
        for (var i = 0; i < n; i++)
            d[i] = d2[i] >= far ? far : MathF.Sqrt(d2[i]);
        return (d, ox, oy);
    }

    /// <summary>The warm rim and the shadow it sits in, both grown outward from the metal.</summary>
    private static void PaintOutside(PixelCanvas c, bool[] mask, (float[] D, float[] Ox, float[] Oy) outward)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var i = y * Width + x;
            if (mask[i])
                continue;
            var d = outward.D[i];
            if (d > 5f)
                continue;

            // Thrown down and to the right, so there is more darkness under a letter than over it.
            // An even ring reads as a drawn border rather than as a cast shadow.
            var lean = 0.5f + 0.5f * Math.Clamp((-outward.Ox[i] * 0.5f - outward.Oy[i] * 0.85f) / 3f, -1f, 1f);
            if (d <= 4.6f)
                c.Px(x, y, Outline, MathF.Max(0f, 1f - (d - 1f) / 4.2f) * lean);

            // Then the rim over the top of it. Laid second on purpose: the other way round, the
            // shadow eats the gold and it survives only in the corners.
            if (d <= 1.5f)
                c.Px(x, y, Rim, 0.95f);
            else if (d <= 2.2f)
                c.Px(x, y, Rim, 0.34f);
        }
    }

    private static void PaintSurface(PixelCanvas c, bool[] mask, (float[] D, float[] Ox, float[] Oy) inward)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var i = y * Width + x;
            if (!mask[i])
                continue;

            var down = (y - PadY) / (CellHeight * UnitScale);

            // The plateau: the flat middle of the stroke, and the only part of a letter the surface
            // texture gets to speak on.
            var plate = Mix(SteelMid, SteelDark, Math.Clamp(down * 0.62f + 0.04f, 0f, 0.72f));

            var d = inward.D[i];
            if (d <= Chamfer)
            {
                // How far up the chamfer, and which way the slope faces. The offset points at the
                // nearest edge, so a pixel near the top of a stem faces up and one near the underside
                // of that same stem faces down - and these three lines light both.
                var len = MathF.Sqrt(inward.Ox[i] * inward.Ox[i] + inward.Oy[i] * inward.Oy[i]);
                var lambert = len > 0.001f
                    ? inward.Ox[i] / len * Light.X + inward.Oy[i] / len * Light.Y
                    : 0f;
                var rise = MathF.Pow(1f - d / Chamfer, 0.75f);

                plate = lambert > 0f
                    ? Mix(plate, SteelBright, rise * lambert * 0.95f)
                    : Mix(plate, SteelShadow, rise * -lambert * 0.85f);

                // The very lip, one pixel of it, at full value. Without this the bevel fades into
                // the rim and the letter loses its hard machined edge.
                if (d <= 1.2f && lambert > 0.25f)
                    plate = Mix(plate, SteelBright, 0.55f);
            }

            c.Px(x, y, plate, 1f);
        }
    }

    /// <summary>Corrosion, drips and scratches - and only on the flat of the stroke.</summary>
    ///
    /// Kept off the chamfer deliberately. Wear laid over a lit edge cancels the edge, and the edge is
    /// doing more work for the shape than any amount of texture can.
    private static void PaintWear(PixelCanvas c, bool[] mask, (float[] D, float[] Ox, float[] Oy) inward)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var i = y * Width + x;
            if (!mask[i])
                continue;
            var shelter = Math.Clamp((inward.D[i] - 1.8f) / 3f, 0f, 1f);
            if (shelter <= 0f)
                continue;

            var down = (y - PadY) / (CellHeight * UnitScale);

            // Two patch layers, one dark and one light, at different scales. One alone gives an
            // evenly dirty surface; it takes both for the plate to look unevenly WORN - rubbed back
            // to bare metal in places and eaten in others, which is what the reference has and what
            // a single corrosion pass can never produce.
            var rot = Blob(x, y, 13) * (0.5f + down * 0.8f);
            if (rot > 0.46f)
                c.Px(x, y, Corrosion, MathF.Min(0.52f, (rot - 0.46f) * 1.35f) * shelter);

            var polish = Blob(x * 2, y * 2, 57);
            if (polish > 0.60f)
                c.Px(x, y, SteelBright, MathF.Min(0.30f, (polish - 0.60f) * 0.75f) * shelter);

            // Drips. A column either weeps or it does not, and where it does the streak starts high
            // and grows downward - which is the difference between weathering and noise.
            if (PixelCanvas.Hash(x, 91) > 0.80f)
            {
                var from = PixelCanvas.Hash(x, 92) * 0.35f;
                if (down > from)
                    c.Px(x, y, Rust, MathF.Min(0.38f, (down - from) * 0.7f) * shelter);
            }

            // Pitting: single dark pixels, sparse enough to be damage rather than texture.
            if (PixelCanvas.Hash(x * 3 + 1, y * 7 + 5) > 0.972f)
                c.Px(x, y, Outline, 0.5f * shelter);
        }

        Scratches(c, mask, inward);
    }

    private static void Scratches(PixelCanvas c, bool[] mask, (float[] D, float[] Ox, float[] Oy) inward)
    {
        // A dozen bright hairlines at a shallow angle. They read as machining marks rather than as
        // damage, and they are the one thing on the plate that crosses letter boundaries - which is
        // what says the whole word was cut from a single sheet.
        for (var k = 0; k < 14; k++)
        {
            var x0 = PixelCanvas.Hash(k, 201) * Width;
            var y0 = PixelCanvas.Hash(k, 202) * Height;
            var len = 12f + PixelCanvas.Hash(k, 203) * 46f;
            var slope = -0.22f + PixelCanvas.Hash(k, 204) * 0.44f;
            for (var t = 0f; t < len; t += 0.5f)
            {
                var x = (int)(x0 + t);
                var y = (int)(y0 + t * slope);
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                    break;
                var i = y * Width + x;
                if (!mask[i] || inward.D[i] < 2.4f)
                    continue;
                c.Px(x, y, SteelBright, 0.16f);
            }
        }
    }

    // Smooth low-frequency blotches, from four hash samples bilinearly mixed. Cheap, and enough
    // structure that corrosion clumps instead of speckling.
    private static float Blob(int x, int y, int seed)
    {
        const float cell = 13f;
        float fx = x / cell, fy = y / cell;
        int ix = (int)MathF.Floor(fx), iy = (int)MathF.Floor(fy);
        float tx = fx - ix, ty = fy - iy;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);

        float a = PixelCanvas.Hash(ix, iy * 31 + seed);
        float b = PixelCanvas.Hash(ix + 1, iy * 31 + seed);
        float d = PixelCanvas.Hash(ix, (iy + 1) * 31 + seed);
        float e = PixelCanvas.Hash(ix + 1, (iy + 1) * 31 + seed);
        var top = a + (b - a) * tx;
        return top + ((d + (e - d) * tx) - top) * ty;
    }

    private static Color Mix(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}
