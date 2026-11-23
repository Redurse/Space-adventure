using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// Raw ore, painted: the formations that sit on an asteroid (three growth stages - the block shrinks as it is cut) and the same
// stone as a single piece for an inventory icon (ItemSkin). Direct user request ("сделай текстуры на основе того что я тебе
// прислал"): the reference sheets show four kinds of stone - crystal clusters, pitted rock studded with coloured nodules, smooth
// layered slabs and clusters of metal beads - sitting on dark rubble; each ore here is one of those, in its own colours.
// Style reference only: every pixel is generated, nothing is copied from the sheets.
// Everything is drawn into a plain Color[] (no graphics device), supersampled, in unit coordinates 0..1 with the base of the
// formation at the bottom - so the same code makes the 96px deposit and the 32px icon.
public static class OreArt
{
    public enum Family { Layered, Porous, Crystal, Beads }

    private sealed record Look(Family Family, Color Dark, Color Mid, Color Light, Color Accent, bool Glow = false);

    // Which stone each ore is, and what colour.
    private static Look? LookOf(ItemType raw) => raw switch
    {
        ItemType.RawIron => new Look(Family.Layered, new Color(44, 24, 22), new Color(112, 58, 44), new Color(170, 98, 72), new Color(214, 140, 108)),
        ItemType.RawCopper => new Look(Family.Layered, new Color(104, 50, 18), new Color(222, 122, 44), new Color(255, 190, 96), new Color(255, 226, 160)),
        ItemType.RawAluminum => new Look(Family.Layered, new Color(70, 78, 90), new Color(150, 160, 172), new Color(220, 228, 236), new Color(248, 250, 255)),
        ItemType.RawCarbon => new Look(Family.Layered, new Color(14, 15, 18), new Color(42, 44, 52), new Color(104, 108, 122), new Color(170, 176, 196)),
        ItemType.RawZinc => new Look(Family.Porous, new Color(30, 46, 82), new Color(70, 104, 156), new Color(130, 168, 210), new Color(90, 214, 232)),
        ItemType.RawUranium => new Look(Family.Porous, new Color(16, 20, 20), new Color(50, 58, 56), new Color(104, 114, 108), new Color(126, 255, 90), Glow: true),
        ItemType.RawNickel => new Look(Family.Beads, new Color(34, 36, 42), new Color(104, 108, 118), new Color(214, 220, 230), new Color(255, 255, 255)),
        ItemType.RawTitanium => new Look(Family.Crystal, new Color(34, 62, 76), new Color(96, 148, 166), new Color(176, 220, 232), new Color(230, 250, 255)),
        ItemType.RawSilicon => new Look(Family.Crystal, new Color(104, 116, 136), new Color(190, 204, 220), new Color(244, 248, 255), new Color(255, 255, 255)),
        ItemType.RawPlastalloy => new Look(Family.Crystal, new Color(46, 30, 100), new Color(116, 78, 196), new Color(186, 140, 255), new Color(226, 200, 255)),
        ItemType.RawHyperium => new Look(Family.Crystal, new Color(14, 78, 130), new Color(52, 178, 232), new Color(150, 238, 255), new Color(232, 255, 255), Glow: true),
        _ => null,
    };

    public static bool Has(ItemType raw) => LookOf(raw) is not null;

    // ------------------------------------------------------------------ cached textures for the field

    public const int DepositSize = 96;
    public const int Variants = 3;
    private static readonly Dictionary<(ItemType, int, int), Texture2D> Cache = new();

    // The formation as it stands on the rock: stage 0 is what is left of it after most of it is cut, 2 is untouched.
    public static Texture2D Deposit(GraphicsDevice graphics, ItemType raw, int stage, int variant)
    {
        stage = Math.Clamp(stage, 0, 2);
        variant = ((variant % Variants) + Variants) % Variants;
        if (Cache.TryGetValue((raw, stage, variant), out var cached))
            return cached;
        var texture = new Texture2D(graphics, DepositSize, DepositSize);
        texture.SetData(Render(raw, DepositSize, stage, variant, rubble: true, zoom: 1f));
        return Cache[(raw, stage, variant)] = texture;
    }

    // ------------------------------------------------------------------ the painter

    // Paints a finished picture into `buffer` (size x size, row-major) - used straight by ItemSkin for the inventory icons.
    public static void Paint(Color[] buffer, int size, ItemType raw)
    {
        var pixels = Render(raw, size, stage: 2, variant: 0, rubble: false, zoom: 1.22f, samples: 1);
        Array.Copy(pixels, buffer, Math.Min(pixels.Length, buffer.Length));
    }

    public static Color[] Render(ItemType raw, int size, int stage, int variant, bool rubble, float zoom, int samples = 2)
    {
        var look = LookOf(raw) ?? throw new ArgumentException($"no ore art for {raw}");
        var seed = (int)raw * 7919 + variant * 104729 + 17;
        var parts = BuildParts(look, stage, seed, rubble);
        var result = new Color[size * size];
        var n = samples * samples;
        for (var py = 0; py < size; py++)
        for (var px = 0; px < size; px++)
        {
            float r = 0, g = 0, b = 0, a = 0;
            for (var sy = 0; sy < samples; sy++)
            for (var sx = 0; sx < samples; sx++)
            {
                var u = (px + (sx + 0.5f) / samples) / size;
                var v = (py + (sy + 0.5f) / samples) / size;
                // the zoom crops in on the formation for an icon: its middle sits at (0.5, 0.52)
                var x = 0.5f + (u - 0.5f) / zoom;
                var y = (zoom > 1f ? 0.52f : 0.5f) + (v - 0.5f) / zoom;
                var (cr, cg, cb, ca) = Shade(parts, x, y);
                r += cr * ca; g += cg * ca; b += cb * ca; a += ca;
            }
            if (a <= 0f)
                continue;
            result[py * size + px] = new Color((int)Math.Clamp(r / a, 0, 255), (int)Math.Clamp(g / a, 0, 255),
                (int)Math.Clamp(b / a, 0, 255), (int)Math.Clamp(a / n * 255f, 0, 255));
        }
        return result;
    }

    // One thing painted: a coverage test and the colour at a point. Parts are drawn in order, later ones over earlier ones.
    private delegate bool Part(float x, float y, out float r, out float g, out float b, out float a);

    private static (float R, float G, float B, float A) Shade(List<Part> parts, float x, float y)
    {
        float r = 0, g = 0, b = 0, a = 0;
        foreach (var part in parts)
        {
            if (!part(x, y, out var pr, out var pg, out var pb, out var pa) || pa <= 0f)
                continue;
            r = pr * pa + r * (1f - pa);
            g = pg * pa + g * (1f - pa);
            b = pb * pa + b * (1f - pa);
            a = pa + a * (1f - pa);
        }
        return (r, g, b, a);
    }

    private static List<Part> BuildParts(Look look, int stage, int seed, bool rubble)
    {
        var parts = new List<Part>();
        var scale = stage switch { 0 => 0.52f, 1 => 0.76f, _ => 1f };
        if (rubble)
            parts.Add(RubbleBack(seed));
        if (look.Glow)
            parts.Add(Halo(0.5f, 0.55f, 0.36f * scale + 0.08f, look.Accent, 0.28f));
        switch (look.Family)
        {
            case Family.Layered: AddLayered(parts, look, stage, scale, seed); break;
            case Family.Porous: AddPorous(parts, look, stage, scale, seed); break;
            case Family.Crystal: AddCrystals(parts, look, stage, scale, seed); break;
            default: AddBeads(parts, look, stage, scale, seed); break;
        }
        if (rubble)
            parts.AddRange(RubbleFront(seed));
        return parts;
    }

    // ------------------------------------------------------------------ small helpers

    private static float Hash(int x, int y, int seed)
    {
        var n = unchecked(x * 374761393 + y * 668265263 + seed * 1274126177);
        n = unchecked((n ^ (n >> 13)) * 1103515245);
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    private static float Noise(float x, float y, int seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        var top = Hash(x0, y0, seed) + (Hash(x0 + 1, y0, seed) - Hash(x0, y0, seed)) * fx;
        var bottom = Hash(x0, y0 + 1, seed) + (Hash(x0 + 1, y0 + 1, seed) - Hash(x0, y0 + 1, seed)) * fx;
        return top + (bottom - top) * fy;
    }

    private static float Rnd(int seed, int i) => Hash(i, i * 31 + 7, seed);

    private static (float R, float G, float B) Ramp(Look look, float t)
    {
        // dark -> mid -> light
        t = Math.Clamp(t, 0f, 1f);
        var (a, b, f) = t < 0.5f ? (look.Dark, look.Mid, t * 2f) : (look.Mid, look.Light, (t - 0.5f) * 2f);
        return (a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f);
    }

    private static bool Inside(float[] poly, float x, float y)
    {
        var inside = false;
        var count = poly.Length / 2;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            var xi = poly[i * 2]; var yi = poly[i * 2 + 1];
            var xj = poly[j * 2]; var yj = poly[j * 2 + 1];
            if (((yi > y) != (yj > y)) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                inside = !inside;
        }
        return inside;
    }

    private static float EdgeDistance(float[] poly, float x, float y)
    {
        var best = float.MaxValue;
        var count = poly.Length / 2;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            float ax = poly[j * 2], ay = poly[j * 2 + 1], bx = poly[i * 2], by = poly[i * 2 + 1];
            float dx = bx - ax, dy = by - ay;
            var len2 = dx * dx + dy * dy;
            var t = len2 <= 0f ? 0f : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / len2, 0f, 1f);
            var ex = ax + dx * t - x;
            var ey = ay + dy * t - y;
            best = MathF.Min(best, MathF.Sqrt(ex * ex + ey * ey));
        }
        return best;
    }

    private static Part Halo(float cx, float cy, float radius, Color color, float strength) =>
        (float x, float y, out float r, out float g, out float b, out float a) =>
        {
            var d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / radius;
            r = color.R; g = color.G; b = color.B;
            a = d >= 1f ? 0f : strength * (1f - d) * (1f - d);
            return a > 0f;
        };

    // A polygon with a shading function; the edge gets a dark rim so every piece reads as separate from its neighbours.
    private static Part Poly(float[] poly, Func<float, float, float> light, Look look, float edge = 0.012f, float gain = 1f) =>
        (float x, float y, out float r, out float g, out float b, out float a) =>
        {
            r = g = b = 0f;
            a = 0f;
            if (!Inside(poly, x, y))
                return false;
            var t = light(x, y);
            var d = EdgeDistance(poly, x, y);
            if (d < edge)
                t *= 0.45f + 0.55f * (d / edge);
            (r, g, b) = Ramp(look, t * gain);
            a = 1f;
            return true;
        };

    // ------------------------------------------------------------------ rubble: the dark scree every formation stands on

    private static Part RubbleBack(int seed) =>
        (float x, float y, out float r, out float g, out float b, out float a) =>
        {
            r = g = b = a = 0f;
            var nx = (x - 0.5f) / 0.47f;
            var ny = (y - 0.885f) / 0.115f;
            var ragged = 1f + (Noise(x * 22f, y * 22f, seed) - 0.5f) * 0.55f + (Noise(x * 7f, 2f, seed + 4) - 0.5f) * 0.2f;
            if (nx * nx + ny * ny > ragged)
                return false;
            var speck = Noise(x * 60f, y * 60f, seed + 1);
            var grain = 0.5f + 0.75f * speck * speck;
            var lit = 0.8f + 0.3f * (0.885f - y) / 0.115f;
            r = 30 * grain * lit; g = 36 * grain * lit; b = 42 * grain * lit;
            a = 1f;
            return true;
        };

    private static IEnumerable<Part> RubbleFront(int seed)
    {
        for (var i = 0; i < 12; i++)
        {
            var cx = 0.12f + 0.76f * Rnd(seed, i + 300);
            var cy = 0.84f + 0.09f * Rnd(seed, i + 340) - 0.03f * MathF.Abs(cx - 0.5f);
            var rad = 0.022f + 0.03f * Rnd(seed, i + 380);
            var shade = 0.6f + 0.6f * Rnd(seed, i + 420);
            yield return (float x, float y, out float r, out float g, out float b, out float a) =>
            {
                r = g = b = a = 0f;
                var dx = (x - cx) / (rad * 1.3f);
                var dy = (y - cy) / rad;
                var d2 = dx * dx + dy * dy;
                if (d2 > 1f)
                    return false;
                var lit = 0.55f + 0.5f * (-0.5f * dx - 0.7f * dy + 0.5f * MathF.Sqrt(1f - d2));
                var k = shade * Math.Clamp(lit, 0.2f, 1.2f);
                r = 40 * k; g = 46 * k; b = 54 * k;
                a = d2 > 0.8f ? 0.9f : 1f;
                return true;
            };
        }
    }

    // ------------------------------------------------------------------ layered: faceted slabs of rock with strata

    private static void AddLayered(List<Part> parts, Look look, int stage, float scale, int seed)
    {
        var slabs = 1 + stage;
        for (var s = slabs - 1; s >= 0; s--) // the biggest is drawn first, the small ones in front
        {
            var big = s == 0;
            var width = (big ? 0.40f : 0.26f) * scale + 0.05f;
            var height = (big ? 0.58f : 0.36f) * scale + 0.05f;
            var cx = 0.5f + (s == 0 ? 0f : (s == 1 ? -0.22f : 0.22f)) * (0.5f + 0.5f * scale);
            var baseY = 0.86f - (s == 0 ? 0f : 0.01f);
            var j = (int i) => (Rnd(seed + s * 13, i) - 0.5f) * 0.16f;
            var ridgeX = cx + width * (0.04f + j(9));
            var poly = new[]
            {
                cx - width * 0.50f, baseY,
                cx - width * (0.58f + j(1)), baseY - height * 0.42f,
                cx - width * (0.26f + j(2)), baseY - height * 0.86f,
                ridgeX, baseY - height,
                cx + width * (0.40f + j(3)), baseY - height * 0.72f,
                cx + width * (0.58f + j(4)), baseY - height * 0.30f,
                cx + width * 0.50f, baseY,
            };
            var bandSeed = seed + s * 101;
            var top = baseY - height;
            parts.Add(Poly(poly, (x, y) =>
            {
                var facet = x < ridgeX ? 0.80f : (x < ridgeX + width * 0.28f ? 0.50f : 0.36f); // light from the upper left, two facets on the dark side
                var up = 1f - (y - top) / height;                          // brighter toward the top
                var strata = MathF.Sin(y * 40f + Noise(x * 8f, y * 8f, bandSeed) * 7f) * 0.11f;
                var mottle = (Noise(x * 34f, y * 34f, bandSeed + 3) - 0.5f) * 0.22f;
                var crack = MathF.Abs(x - (ridgeX - 0.015f)) < 0.006f ? -0.2f : 0f;
                var fissure = MathF.Abs(Noise(x * 11f + y * 4f, y * 17f, bandSeed + 9) - 0.5f) < 0.022f ? -0.24f : 0f;
                return facet * (0.74f + 0.34f * up) + strata + mottle + crack + fissure;
            }, look));
            // the bright catch along the top edge of the lit facet
            parts.Add(Highlight(ridgeX - width * 0.22f, top + height * 0.12f, ridgeX - width * 0.02f, top + height * 0.02f, look.Accent, 0.55f));
        }
    }

    private static Part Highlight(float x0, float y0, float x1, float y1, Color color, float strength) =>
        (float x, float y, out float r, out float g, out float b, out float a) =>
        {
            r = color.R; g = color.G; b = color.B; a = 0f;
            float dx = x1 - x0, dy = y1 - y0;
            var len2 = dx * dx + dy * dy;
            var t = Math.Clamp(((x - x0) * dx + (y - y0) * dy) / len2, 0f, 1f);
            var ex = x0 + dx * t - x;
            var ey = y0 + dy * t - y;
            var d = MathF.Sqrt(ex * ex + ey * ey);
            const float width = 0.014f;
            if (d > width)
                return false;
            a = strength * (1f - d / width) * (0.4f + 0.6f * MathF.Sin(t * MathF.PI));
            return a > 0f;
        };

    // ------------------------------------------------------------------ porous: a pitted mound with coloured nodules set into it

    private static void AddPorous(List<Part> parts, Look look, int stage, float scale, int seed)
    {
        float cx = 0.5f, cy = 0.66f - 0.04f * (1f - scale);
        var rx = 0.36f * scale + 0.04f;
        var ry = 0.27f * scale + 0.04f;
        const int corners = 18;
        var poly = new float[corners * 2];
        for (var i = 0; i < corners; i++)
        {
            var angle = i * MathF.Tau / corners;
            var wobble = 0.86f + 0.2f * Rnd(seed, i + 50);
            poly[i * 2] = cx + MathF.Cos(angle) * rx * wobble;
            poly[i * 2 + 1] = cy + MathF.Sin(angle) * ry * wobble;
        }
        parts.Add(Poly(poly, (x, y) =>
        {
            var nx = (x - cx) / rx;
            var ny = (y - cy) / ry;
            var d2 = Math.Clamp(nx * nx + ny * ny, 0f, 1f);
            var nz = MathF.Sqrt(1f - d2);
            var lit = 0.32f + 0.7f * Math.Clamp(-0.45f * nx - 0.65f * ny + 0.62f * nz, 0f, 1f);
            var grain = (Noise(x * 40f, y * 40f, seed + 5) - 0.5f) * 0.18f + (Noise(x * 12f, y * 12f, seed + 6) - 0.5f) * 0.2f;
            return lit + grain;
        }, look, edge: 0.014f));

        // pits: dark hollows with a lit lower lip
        var pits = 5 + stage * 4;
        var spots = new List<(float X, float Y, float R)>();
        for (var i = 0; i < pits; i++)
        {
            var angle = Rnd(seed, i + 60) * MathF.Tau;
            var dist = MathF.Sqrt(Rnd(seed, i + 90)) * 0.78f;
            var px = cx + MathF.Cos(angle) * rx * dist;
            var py = cy + MathF.Sin(angle) * ry * dist;
            var pr = (0.034f + 0.034f * Rnd(seed, i + 120)) * (0.6f + 0.4f * scale);
            spots.Add((px, py, pr));
            parts.Add(Pit(px, py, pr, look));
        }

        // nodules: little domes of the accent colour sitting at the edge of some of the pits
        var nodules = 2 + stage * 2;
        for (var i = 0; i < nodules; i++)
        {
            var (px, py, pr) = spots[i % spots.Count];
            var radius = pr * (0.7f + 0.5f * Rnd(seed, i + 150));
            var ox = px + (Rnd(seed, i + 180) - 0.5f) * pr * 2.4f;
            var oy = py + (Rnd(seed, i + 210) - 0.5f) * pr * 1.6f;
            if (look.Glow)
                parts.Add(Halo(ox, oy, radius * 3f, look.Accent, 0.5f));
            parts.Add(Dome(ox, oy, radius, look.Accent, look.Glow));
        }
    }

    private static Part Pit(float cx, float cy, float rad, Look look) =>
        (float x, float y, out float r, out float g, out float b, out float a) =>
        {
            r = g = b = a = 0f;
            var dx = (x - cx) / (rad * 1.15f);
            var dy = (y - cy) / rad;
            var d2 = dx * dx + dy * dy;
            if (d2 > 1.5f)
                return false;
            if (d2 > 1f)                                   // the lit lip, lower edge brighter
            {
                var lip = 0.5f + 0.7f * Math.Clamp(dy, -1f, 1f);
                (r, g, b) = Ramp(look, 0.36f + 0.34f * lip);
                a = 0.9f * (1.5f - d2) / 0.5f;
                return true;
            }
            var depth = 0.04f + 0.12f * d2 + (dy < 0f ? 0.08f * -dy : 0f);
            (r, g, b) = Ramp(look, depth);
            a = 1f;
            return true;
        };

    private static Part Dome(float cx, float cy, float rad, Color color, bool glow) =>
        (float x, float y, out float r, out float g, out float b, out float a) =>
        {
            r = g = b = a = 0f;
            var dx = (x - cx) / rad;
            var dy = (y - cy) / rad;
            var d2 = dx * dx + dy * dy;
            if (d2 > 1f)
                return false;
            var nz = MathF.Sqrt(1f - d2);
            var lit = Math.Clamp(-0.5f * dx - 0.6f * dy + 0.65f * nz, 0f, 1f);
            var k = glow ? 0.7f + 0.55f * lit : 0.35f + 0.8f * lit;
            var spec = MathF.Pow(Math.Clamp(lit, 0f, 1f), 9f) * 0.8f;
            r = Math.Clamp(color.R * k + 255f * spec, 0f, 255f);
            g = Math.Clamp(color.G * k + 255f * spec, 0f, 255f);
            b = Math.Clamp(color.B * k + 255f * spec, 0f, 255f);
            if (d2 > 0.8f)
            {
                r *= 0.55f; g *= 0.55f; b *= 0.55f;
            }
            a = 1f;
            return true;
        };

    // ------------------------------------------------------------------ crystal: prisms with a pointed tip, in a cluster

    private static void AddCrystals(List<Part> parts, Look look, int stage, float scale, int seed)
    {
        var count = 3 + stage * 2;
        var items = new List<(float X, float W, float H, float Lean, float Depth)>();
        for (var i = 0; i < count; i++)
        {
            var spread = (Rnd(seed, i + 10) - 0.5f) * (0.5f + 0.32f * scale);
            var tall = 1f - Math.Abs(spread) * 1.1f;                       // the middle ones grow tallest
            var height = (0.30f + 0.36f * Rnd(seed, i + 20)) * tall * scale + 0.08f;
            var width = (0.10f + 0.07f * Rnd(seed, i + 30)) * (0.6f + 0.4f * scale);
            var lean = (Rnd(seed, i + 40) - 0.5f) * 0.14f + spread * 0.25f;
            items.Add((0.5f + spread, width, height, lean, Rnd(seed, i + 50)));
        }
        items.Sort((p, q) => p.Depth.CompareTo(q.Depth));                  // far ones first
        foreach (var (x0, w, h, lean, depth) in items)
        {
            var baseY = 0.84f + 0.04f * depth;
            var tipX = x0 + lean * 1.6f;
            var tipY = baseY - h;
            var shoulderY = baseY - h * 0.7f;
            var poly = new[]
            {
                x0 - w * 0.5f, baseY,
                x0 - w * 0.5f + lean * 0.4f, shoulderY,
                tipX, tipY,
                x0 + w * 0.5f + lean * 0.4f, shoulderY + h * 0.04f,
                x0 + w * 0.5f, baseY,
            };
            var left = x0 - w * 0.5f;
            parts.Add(Poly(poly, (x, y) =>
            {
                var f = (x - left) / w;                                     // 0..1 across the prism
                var facet = f < 0.34f ? 0.52f : (f < 0.72f ? 0.98f : 0.74f);
                var up = 1f - (y - tipY) / h;
                var inner = (Noise(x * 22f, y * 22f, seed + 7) - 0.5f) * 0.14f;
                var tip = y < shoulderY ? 0.12f : 0f;                       // the pointed cap catches more light
                var depthFade = 0.82f + 0.22f * depth;
                return (facet * (0.78f + 0.3f * up) + inner + tip) * depthFade;
            }, look, edge: 0.01f, gain: 1.04f));
            // a bright streak down the lit facet
            parts.Add(Highlight(x0 - w * 0.02f, baseY - h * 0.12f, tipX - lean * 0.2f, tipY + h * 0.18f, look.Accent, look.Glow ? 0.8f : 0.5f));
        }
    }

    // ------------------------------------------------------------------ beads: a cluster of bright metal drops

    private static void AddBeads(List<Part> parts, Look look, int stage, float scale, int seed)
    {
        var count = 4 + stage * 3;
        var beads = new List<(float X, float Y, float R)>();
        for (var i = 0; i < count; i++)
        {
            var angle = Rnd(seed, i + 10) * MathF.Tau;
            var dist = MathF.Sqrt(Rnd(seed, i + 20)) * (0.2f + 0.2f * scale);
            var rad = (0.09f + 0.07f * Rnd(seed, i + 30)) * (0.55f + 0.45f * scale) + 0.02f;
            beads.Add((0.5f + MathF.Cos(angle) * dist * 1.1f, 0.68f + MathF.Sin(angle) * dist * 0.62f - 0.05f * scale, rad));
        }
        beads.Sort((p, q) => p.Y.CompareTo(q.Y));                           // back to front
        foreach (var (cx, cy, rad) in beads)
        {
            parts.Add((float x, float y, out float r, out float g, out float b, out float a) =>
            {
                r = g = b = a = 0f;
                var dx = (x - cx) / rad;
                var dy = (y - cy) / rad;
                var d2 = dx * dx + dy * dy;
                if (d2 > 1f)
                    return false;
                var nz = MathF.Sqrt(1f - d2);
                var lit = Math.Clamp(-0.5f * dx - 0.62f * dy + 0.6f * nz, 0f, 1f);
                // a metal ball: dark rim, a band of sky reflected low, a hard white glint high on the lit side
                var band = dy > 0.15f ? 0.22f * (1f - Math.Abs(dy - 0.55f) * 2f) : 0f;
                var t = 0.12f + 0.62f * lit + band;
                (r, g, b) = Ramp(look, t);
                var glint = MathF.Pow(lit, 14f);
                r += 255f * glint * 0.9f; g += 255f * glint * 0.9f; b += 255f * glint * 0.9f;
                if (d2 > 0.82f)
                {
                    var shade = 0.55f + 0.45f * (1f - d2) / 0.18f;
                    r *= shade; g *= shade; b *= shade;
                }
                a = 1f;
                return true;
            });
        }
    }
}
