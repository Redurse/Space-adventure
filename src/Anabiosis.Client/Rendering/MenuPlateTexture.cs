using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The material the main menu's left column is made of - painted, not noised.
//
// The first version of this was fractal noise: blotches, streaks and grain. It was better than the
// flat fill it replaced and still wrong, for a reason worth writing down. Value noise has no
// direction and no edges. It can only ever produce soft shapes that melt into each other, and a
// painted surface is nothing but direction and edges - a brush lays a mark that starts, runs one way,
// and stops, with a firm side and a broken tail.
//
// So this lays actual strokes. Four things make them read as a hand rather than as a pattern:
//
//   * families of direction, not random angles. Random angles are a scribble; a hand has two or
//     three habits and everything it does sits a few degrees off one of them.
//   * loaded-then-dry along the length, so a mark breaks up where the brush ran out.
//   * layered passes, dark to light, each one fewer and smaller than the last. Later marks cover
//     earlier ones, which is what gives stacked tone instead of a gradient - and a gradient is what
//     gives procedural work away instantly.
//   * colour, not only brightness. Everything on one hue with only the value moving is the other
//     thing that never happens in paint.
//
// Baked once at load.
public static class MenuPlateTexture
{
    // Baked at twice the design size of the panel so it holds up when the frame is scaled to a 1440p
    // window. It is a texture rather than pixel art, so it is sampled linearly and a non-integer
    // scale costs nothing.
    public const int Scale = 2;

    // The hand's habits. Near-vertical for anything that ran downward, and two diagonals for wear.
    // Every stroke picks one of these and then deviates by a few degrees, never more.
    private static readonly float[] StrokeFamilies = { 1.40f, 0.42f, -0.30f };
    private static readonly float[] FamilyWeights = { 0.30f, 0.40f, 0.30f };
    private const float FamilyJitter = 0.11f;      // radians, about six degrees

    private static uint Hash(int x, int y, int seed)
    {
        var n = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
        n ^= n >> 13;
        n *= 1274126177u;
        return n ^ (n >> 16);
    }

    private static float Rand(int x, int y, int seed) => (Hash(x, y, seed) & 0xffff) / 65535f;

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static float Noise(float x, float y, float cell, int seed)
    {
        var fx = x / cell;
        var fy = y / cell;
        var x0 = (int)MathF.Floor(fx);
        var y0 = (int)MathF.Floor(fy);
        var tx = Smooth(fx - x0);
        var ty = Smooth(fy - y0);
        var a = Rand(x0, y0, seed) * (1 - tx) + Rand(x0 + 1, y0, seed) * tx;
        var b = Rand(x0, y0 + 1, seed) * (1 - tx) + Rand(x0 + 1, y0 + 1, seed) * tx;
        return a * (1 - ty) + b * ty;
    }

    private static float Fbm(float x, float y, float cell, int octaves, int seed)
    {
        float total = 0f, amp = 1f, norm = 0f;
        for (var i = 0; i < octaves; i++)
        {
            total += Noise(x, y, cell, seed + i) * amp;
            norm += amp;
            amp *= 0.5f;
            cell *= 0.5f;
        }
        return total / norm;
    }

    /// <summary>Noise along one axis - what makes a stroke run dry in patches.</summary>
    private static float Noise1(float t, float cell, int seed)
    {
        var f = t / cell;
        var i0 = (int)MathF.Floor(f);
        var frac = Smooth(f - i0);
        return Rand(i0, 0, seed) * (1 - frac) + Rand(i0 + 1, 0, seed) * frac;
    }

    public static Texture2D Create(GraphicsDevice device, int designWidth, int designHeight)
    {
        var w = designWidth * Scale;
        var h = designHeight * Scale;
        var buf = new float[w * h * 3];

        Underpaint(buf, w, h);

        // Dark to light, each pass fewer and smaller. The counts matter as much as the colours: a
        // highlight pass as dense as the underpaint stops being a highlight and becomes a texture.
        Pass(buf, w, h, seed: 101, count: 110, minLen: 0.16f, maxLen: 0.42f, minWide: 16f, maxWide: 44f,
            tone: 0.55f, toneSpread: 0.30f, warm: -0.15f, minAlpha: 0.22f, maxAlpha: 0.50f);
        Pass(buf, w, h, seed: 211, count: 120, minLen: 0.10f, maxLen: 0.28f, minWide: 9f, maxWide: 26f,
            tone: 0.85f, toneSpread: 0.45f, warm: 0.05f, minAlpha: 0.16f, maxAlpha: 0.36f);
        Pass(buf, w, h, seed: 307, count: 24, minLen: 0.05f, maxLen: 0.16f, minWide: 6f, maxWide: 17f,
            tone: 1.25f, toneSpread: 0.55f, warm: 0.30f, minAlpha: 0.20f, maxAlpha: 0.40f);

        // The confident marks a painter puts down last - very few, short, and nearly opaque. These
        // are what the eye lands on, and without them the surface stays a mush of half-tones.
        Pass(buf, w, h, seed: 419, count: 8, minLen: 0.03f, maxLen: 0.09f, minWide: 5f, maxWide: 13f,
            tone: 1.75f, toneSpread: 0.8f, warm: 0.45f, minAlpha: 0.40f, maxAlpha: 0.62f);

        Scratches(buf, w, h);

        var pixels = new Color[w * h];
        for (var i = 0; i < w * h; i++)
        {
            pixels[i] = new Color(
                (int)MathHelper.Clamp(buf[i * 3], 0f, 255f),
                (int)MathHelper.Clamp(buf[i * 3 + 1], 0f, 255f),
                (int)MathHelper.Clamp(buf[i * 3 + 2], 0f, 255f));
        }

        var texture = new Texture2D(device, w, h);
        texture.SetData(pixels);
        return texture;
    }

    // The tonal composition, decided rather than generated: light arrives from the upper left, where
    // the buttons are, and the surface falls away from it towards the lower right. A soft radial blob
    // in the middle - which is what was here before - decides nothing, and a surface where nobody
    // chose the light is exactly what reads as procedural.
    private static void Underpaint(float[] buf, int w, int h)
    {
        var cool = new Vector3(7f, 12f, 16f);
        var warm = new Vector3(13f, 14f, 13f);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var u = x / (float)w;
                var v = y / (float)h;

                // The decision: a diagonal ramp, plus one pool of light up where the top group of
                // buttons sits so that corner is the brightest thing on the panel.
                var ramp = 1f - MathHelper.Clamp(u * 0.55f + v * 0.75f, 0f, 1f);
                var pool = MathF.Exp(-((u - 0.30f) * (u - 0.30f) * 5.5f + (v - 0.16f) * (v - 0.16f) * 3.0f));
                var lightness = 0.42f + ramp * 0.55f + pool * 0.40f;

                // Slow mottle underneath everything, so the strokes have something uneven to sit on.
                var mottle = (Fbm(x, y, 230f, 4, 11) - 0.5f) * 0.55f;

                var tint = MathHelper.Clamp(0.35f + ramp * 0.5f, 0f, 1f);
                var c = Vector3.Lerp(cool, warm, tint * 0.35f) * MathF.Max(0.2f, lightness + mottle);
                buf[(y * w + x) * 3] = c.X;
                buf[(y * w + x) * 3 + 1] = c.Y;
                buf[(y * w + x) * 3 + 2] = c.Z;
            }
        }
    }

    private static float PickAngle(float roll, float jitter)
    {
        float acc = 0f;
        for (var i = 0; i < StrokeFamilies.Length; i++)
        {
            acc += FamilyWeights[i];
            if (roll <= acc || i == StrokeFamilies.Length - 1)
                return StrokeFamilies[i] + (jitter - 0.5f) * 2f * FamilyJitter;
        }
        return StrokeFamilies[0];
    }

    // One pass of strokes. Lengths are fractions of the panel's height so the marks stay the same
    // size relative to the panel whatever it is baked at.
    private static void Pass(float[] buf, int w, int h, int seed, int count,
        float minLen, float maxLen, float minWide, float maxWide,
        float tone, float toneSpread, float warm, float minAlpha, float maxAlpha)
    {
        for (var i = 0; i < count; i++)
        {
            var x0 = Rand(i, 1, seed) * w;
            var y0 = Rand(i, 2, seed) * h;
            var angle = PickAngle(Rand(i, 3, seed), Rand(i, 4, seed));
            var length = (minLen + Rand(i, 5, seed) * (maxLen - minLen)) * h;
            var width = (minWide + Rand(i, 6, seed) * (maxWide - minWide)) * (Scale * 0.5f + 0.5f);
            var alpha = minAlpha + Rand(i, 7, seed) * (maxAlpha - minAlpha);

            // Tone and temperature per stroke. Mixed paint is never twice the same colour, and a
            // surface where only the brightness moves reads as one colour lit unevenly.
            var value = tone * (1f - toneSpread * 0.5f + Rand(i, 8, seed) * toneSpread);
            var temperature = warm * (0.4f + Rand(i, 9, seed));
            var colour = new Vector3(
                (8f + temperature * 9f) * value,
                (13f + temperature * 3f) * value,
                (17f - temperature * 6f) * value);

            Stroke(buf, w, h, x0, y0, angle, length, width, colour, alpha, seed * 31 + i);
        }
    }

    private static void Stroke(float[] buf, int w, int h, float x0, float y0, float angle,
        float length, float width, Vector3 colour, float alpha, int seed)
    {
        var dx = MathF.Cos(angle);
        var dy = MathF.Sin(angle);
        var px = -dy;
        var py = dx;
        var half = width * 0.5f;

        for (var s = 0f; s < length; s += 0.5f)
        {
            var t = s / length;

            // Tapered ends: a brush touches down and lifts off, it does not start and stop square.
            var taper = MathF.Min(1f, MathF.Min(t, 1f - t) * 7f);

            // Loaded, then dry. Where this drops the mark breaks up, and that break is most of what
            // separates a brush from an airbrush.
            var load = Noise1(s, 34f, seed);
            var dry = MathHelper.Clamp((load - 0.28f) * 2.2f, 0f, 1f);
            var along = taper * (0.35f + 0.65f * dry);
            if (along <= 0.01f)
                continue;

            for (var o = -half; o <= half; o += 0.5f)
            {
                var x = (int)(x0 + dx * s + px * o);
                var y = (int)(y0 + dy * s + py * o);
                if (x < 0 || x >= w || y < 0 || y >= h)
                    continue;

                var v = o / half;
                // Firm through the middle and a quick fall at the edge, rather than a bell. A bell
                // is an airbrush; the sharper shoulder is what gives a stroke a side you can see.
                var across = MathF.Pow(MathF.Max(0f, 1f - MathF.Abs(v)), 0.55f);

                // Bristles: fine streaks along the mark, because a brush is not one edge but many.
                var bristle = 0.72f + 0.28f * Noise1(o * 3.1f + seed, 2.6f, seed + 7);

                var a = alpha * along * across * bristle;
                if (a <= 0.004f)
                    continue;

                // Paint covers - it does not add. Adding is what makes procedural grime glow.
                var k = (y * w + x) * 3;
                buf[k] += (colour.X - buf[k]) * a;
                buf[k + 1] += (colour.Y - buf[k + 1]) * a;
                buf[k + 2] += (colour.Z - buf[k + 2]) * a;
            }
        }
    }

    // Scratches survive, thinned right down. They are a different language - damage, not paint - and
    // at the old count they argued with the strokes instead of sitting on them.
    private static void Scratches(float[] buf, int w, int h)
    {
        const int count = 85;
        for (var i = 0; i < count; i++)
        {
            var x0 = Rand(i, 1, 401) * w;
            var y0 = Rand(i, 2, 401) * h;
            var angle = PickAngle(Rand(i, 3, 401), Rand(i, 4, 401));
            var length = 8f + Rand(i, 5, 401) * 90f;
            var bright = Rand(i, 6, 401) > 0.70f;
            var strength = (0.25f + Rand(i, 7, 401) * 0.75f) * (bright ? 17f : -11f);

            var dx = MathF.Cos(angle);
            var dy = MathF.Sin(angle);
            for (var s = 0; s < (int)length; s++)
            {
                var x = (int)(x0 + dx * s);
                var y = (int)(y0 + dy * s);
                if (x < 0 || x >= w || y < 0 || y >= h)
                    break;
                var t = s / length;
                var fade = MathF.Min(1f, MathF.Min(t, 1f - t) * 6f);
                var add = strength * fade;
                var k = (y * w + x) * 3;
                buf[k] += add;
                buf[k + 1] += add;
                buf[k + 2] += add;
            }
        }
    }
}
