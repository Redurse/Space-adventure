using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Wear laid over the base painting: Weather (grime, drips - every stage, heavier when battered),
// Batter (stage 1: rust, scorch, dents, a cracked window, torn fins, lost bolts) and Wreck
// (stage 2, on top of stage 1: the core housing blown open, a shattered coolant column, soot).
public static partial class MachineArt
{
    private static readonly Vector3 Rust = new(0.34f, 0.19f, 0.09f);
    private static readonly Vector3 Soot = new(0.035f, 0.03f, 0.028f);
    private static readonly Vector3 Dirt = new(0.17f, 0.14f, 0.10f);

    private static void Weather(float extra)
    {
        // Patchy grime, gathering toward the bottom where oil and dust settle.
        Stain(0, 0, CanvasUnitsW, CanvasUnitsH, Dirt, (x, y, mat) =>
        {
            var g = Fbm(x * 0.17f, y * 0.17f, 51) + Smooth(CanvasUnitsH * 0.28f, CanvasUnitsH * 0.94f, y) * 0.22f + extra * 0.12f;
            var dirt = Smooth(0.46f, 0.78f, g) * (0.42f + extra * 0.3f);
            return mat == Mat.Glass ? dirt * 0.4f : mat == Mat.Lamp ? 0f : dirt;
        });
        Multiply(0, 0, CanvasUnitsW, CanvasUnitsH, (x, y, mat) => 1f - Smooth(0.55f, 0.85f, Fbm(x * 0.3f + 9f, y * 0.3f, 52)) * (0.18f + extra * 0.2f));

        // Drips running down from seams and bolts.
        var drips = 22 + (int)(extra * 30f);
        for (var i = 0; i < drips; i++)
        {
            var dx = 2.5f + Rand(i, 53 + (int)(extra * 10)) * (CanvasUnitsW - 5f);
            var dy = 5f + Rand(i, 54 + (int)(extra * 10)) * (CanvasUnitsH * 0.594f);
            var len = 2.5f + Rand(i, 55) * (7f + extra * 6f);
            var w = 0.18f + Rand(i, 56) * 0.35f;
            Multiply(dx - w, dy, dx + w, dy + len, (x, y, mat) =>
            {
                if (mat is Mat.Glass or Mat.Lamp) return 1f;
                var t = (y - dy) / len;
                var width = w * (1f - t * 0.6f);
                return MathF.Abs(x - dx) > width ? 1f : 1f - (0.32f + extra * 0.15f) * (1f - t);
            });
        }

        // Bright scuffs where paint has worn off raised edges.
        var scuffs = 8 + (int)(extra * 6f);
        for (var i = 0; i < scuffs; i++)
        {
            var sx = 3f + Rand(i, 57 + (int)(extra * 10)) * (CanvasUnitsW - 6f);
            var sy = 4f + Rand(i, 58 + (int)(extra * 10)) * (CanvasUnitsH - 14f);
            var angle = Rand(i, 59) * MathF.PI;
            var len = 0.8f + Rand(i, 60) * 2.2f;
            var ex = sx + MathF.Cos(angle) * len; var ey = sy + MathF.Sin(angle) * len * 0.4f;
            StainLine(sx, sy, ex, ey, 0.22f, new Vector3(0.6f, 0.59f, 0.55f), 0.25f, Mat.Metal);
        }
    }

    /// Lerps pixels of one material within a thin line toward a tone.
    private static void StainLine(float ax, float ay, float bx, float by, float width, Vector3 tone, float amount, Mat only)
    {
        var a = new Vector2(ax, ay); var ab = new Vector2(bx - ax, by - ay);
        Stain(MathF.Min(ax, bx) - width, MathF.Min(ay, by) - width, MathF.Max(ax, bx) + width, MathF.Max(ay, by) + width, tone, (x, y, mat) =>
        {
            if (mat != only) return 0f;
            var t = Math.Clamp(Vector2.Dot(new Vector2(x, y) - a, ab) / MathF.Max(1e-5f, ab.LengthSquared()), 0f, 1f);
            return (new Vector2(x, y) - (a + ab * t)).Length() <= width / 2f ? amount : 0f;
        });
    }

    // ---- stage 1 ---------------------------------------------------------------------------

    private static void Batter()
    {
        // Rust blooms on bare steel, heavier low down.
        Stain(0, 0, CanvasUnitsW, CanvasUnitsH, Rust, (x, y, mat) =>
        {
            if (mat is not (Mat.Metal or Mat.Dark)) return 0f;
            var r = Fbm(x * 0.21f, y * 0.21f, 61) + Smooth(28f, 58f, y) * 0.12f;
            return Smooth(0.55f, 0.7f, r) * 0.78f * (0.7f + 0.3f * Noise(x * 3f, y * 3f, 62));
        });
        // Rust weeping down below the blooms.
        for (var i = 0; i < 16; i++)
        {
            var rx = 3f + Rand(i, 63) * 58f; var ry = 8f + Rand(i, 64) * 34f; var len = 3f + Rand(i, 65) * 7f;
            Stain(rx - 0.4f, ry, rx + 0.4f, ry + len, Rust * 0.8f, (x, y, mat) =>
                mat is Mat.Metal or Mat.Dark && MathF.Abs(x - rx) < 0.35f * (1f - (y - ry) / len * 0.5f) ? 0.55f * (1f - (y - ry) / len) : 0f);
        }

        // Scorch marks.
        foreach (var (sx, sy, sr) in new[] { (11f, 31f, 5f), (51f, 13f, 4.5f), (41f, 49f, 4f), (23f, 8f, 3.5f), (55f, 36f, 3f) })
            Scorch(sx, sy, sr, 0.62f);

        // Dents: a concave dimple is shadowed on its lit side and lit on the far side.
        foreach (var (dx0, dy0, dr) in new[] { (42.5f, 37.5f, 2.4f), (8f, 45.5f, 2f), (57f, 20f, 2.6f), (23f, 37.5f, 1.8f) })
        {
            Multiply(dx0 - dr, dy0 - dr, dx0 + dr, dy0 + dr, (x, y, mat) =>
            {
                var dx = x - dx0; var dy = y - dy0; var d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > dr || mat is not (Mat.Metal or Mat.Dark)) return 1f;
                return 1f + 0.45f * ((dx + dy) / (dr * 1.41f)) * (1f - d / dr);
            });
        }

        // A star crack in the window from one impact.
        CrackStar(28.4f, 21.6f, 8, 71);

        // Torn fins in the left radiator: a gap, with a couple of slats bent across it.
        var (lx0, _) = Radiators[0];
        Stain(lx0 + 2.6f, 27.5f, lx0 + 7.4f, 36.5f, Recess, (x, y, mat) =>
        {
            var edge = MathF.Abs(y - 32f) + MathF.Abs(x - (lx0 + 5f)) * 0.9f + (Noise(x * 2f, y * 2f, 73) - 0.5f) * 1.4f;
            return edge < 4.2f ? 1f : 0f;
        });
        Line(lx0 + 3f, 28.4f, lx0 + 6.6f, 33.6f, 0.45f, FinTone * 1.6f, Mat.Metal);
        Line(lx0 + 4.4f, 35.2f, lx0 + 7f, 31f, 0.4f, FinTone * 1.3f, Mat.Metal);

        // Lost bolts: an empty hole with a rust ring.
        foreach (var (bx, by) in new[] { (2.9f, 22.9f), (61.1f, 29.7f), (CoreX1 - 0.75f, 20.8f), (WinX1 + 0.45f, WinY1 - 3f), (15.8f, 42.6f) })
        {
            Paint(bx - 0.7f, by - 0.7f, bx + 0.7f, by + 0.7f, Mat.Dark, (float x, float y, out Vector3 c) =>
            {
                var d = MathF.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));
                c = d < 0.3f ? new Vector3(0.02f) : Rust * 0.9f;
                return d < 0.6f;
            });
        }

        // The amber lamp is dead and cracked.
        Stain(LampX[2] - 0.8f, LampY - 0.8f, LampX[2] + 0.8f, LampY + 0.8f, new Vector3(0.05f), (x, y, mat) => mat == Mat.Lamp ? 0.8f : 0f);
    }

    private static void Scorch(float sx, float sy, float sr, float strength)
    {
        Stain(sx - sr, sy - sr, sx + sr, sy + sr, Soot, (x, y, mat) =>
        {
            var d = MathF.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy)) / sr;
            d += (Fbm(x * 0.8f, y * 0.8f, 66) - 0.5f) * 0.7f;
            return Smooth(1f, 0.2f, d) * strength;
        });
    }

    /// Jagged cracks radiating from an impact point, confined to glass.
    private static void CrackStar(float cx, float cy, int arms, int seed)
    {
        for (var arm = 0; arm < arms; arm++)
        {
            var angle = arm / (float)arms * MathF.PI * 2f + Rand(arm, seed) * 0.6f;
            float x = cx, y = cy;
            var steps = 3 + (int)(Rand(arm, seed + 1) * 4f);
            for (var s = 0; s < steps; s++)
            {
                angle += (Rand(arm * 10 + s, seed + 2) - 0.5f) * 0.9f;
                var len = 0.9f + Rand(arm * 10 + s, seed + 3) * 1.4f;
                var nx = x + MathF.Cos(angle) * len; var ny = y + MathF.Sin(angle) * len;
                StainLine(x, y, nx, ny, 0.2f, new Vector3(0.55f, 0.63f, 0.63f), 0.8f, Mat.Glass);
                x = nx; y = ny;
            }
        }
        // Rings of the impact.
        Stain(cx - 1.5f, cy - 1.5f, cx + 1.5f, cy + 1.5f, new Vector3(0.6f, 0.68f, 0.68f), (x, y, mat) =>
        {
            if (mat != Mat.Glass) return 0f;
            var d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
            return d < 0.35f || MathF.Abs(d - 1.1f) < 0.1f ? 0.7f : 0f;
        });
    }

    // ---- stage 2 ---------------------------------------------------------------------------

    private const float HoleX = 32f, HoleY = 25.5f;

    private static float HoleRadius(float angle)
    {
        var ca = MathF.Cos(angle); var sa = MathF.Sin(angle);
        return 8.6f + 3.4f * (Noise(ca * 1.6f + 20f, sa * 1.6f + 20f, 81) - 0.5f) * 2f
                    + 1.5f * (Noise(ca * 5.5f + 40f, sa * 5.5f + 40f, 82) - 0.5f) * 2f;
    }

    private static void Wreck()
    {
        Weather(0.6f);

        // Soot thrown out around the breach.
        Stain(HoleX - 22f, HoleY - 22f, HoleX + 22f, HoleY + 22f, Soot, (x, y, mat) =>
        {
            var dx = x - HoleX; var dy = y - HoleY; var d = MathF.Sqrt(dx * dx + dy * dy);
            var r = HoleRadius(MathF.Atan2(dy, dx));
            var reach = r + 6f + Fbm(x * 0.5f, y * 0.5f, 83) * 7f;
            return Smooth(reach, r, d) * 0.88f;
        });
        foreach (var (sx, sy, sr) in new[] { (9f, 18f, 5f), (55f, 44f, 5f), (48f, 6f, 4f), (14f, 48f, 4f) })
            Scorch(sx, sy, sr, 0.75f);

        // The breach: torn, outward-curled lip, then a dark cavity full of wreckage.
        Paint(HoleX - 17f, HoleY - 17f, HoleX + 17f, HoleY + 17f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            var dx = x - HoleX; var dy = y - HoleY; var d = MathF.Sqrt(dx * dx + dy * dy);
            var r = HoleRadius(MathF.Atan2(dy, dx));
            c = default;
            var lip = 0.7f + 1.8f * Noise(dx / MathF.Max(d, 0.01f) * 3f + 50f, dy / MathF.Max(d, 0.01f) * 3f + 50f, 84);
            if (d > r + lip) return false;
            if (d > r)
            {
                // Lip: torn metal curled outward, its width ragged around the breach.
                var t = (d - r) / lip;
                var n = -(1f - t) * 0.75f;
                c = SteelMid * 1.15f * Lit(dx / d * n, dy / d * n, 0.5f) / FlatLit * (0.6f + 0.4f * Fbm(x, y, 85));
                return true;
            }
            if (d > r - 0.35f) { c = new Vector3(0.015f); return true; }
            // Cavity: near-black, a little ash variation and deeper toward the middle.
            var depth = Smooth(r, r * 0.3f, d);
            c = new Vector3(0.045f, 0.04f, 0.037f) * (1f - depth * 0.6f) * (0.7f + 0.6f * Fbm(x * 0.9f, y * 0.9f, 86));
            return true;
        });

        // Wreckage inside: bent plates and spars, two broken fuel-rod stubs, loose wiring.
        for (var i = 0; i < 26; i++)
        {
            var a = Rand(i, 87) * MathF.PI * 2f;
            var rr = HoleRadius(a) * (0.25f + Rand(i, 88) * 0.7f);
            var px = HoleX + MathF.Cos(a) * rr; var py = HoleY + MathF.Sin(a) * rr;
            var dir = Rand(i, 89) * MathF.PI;
            var len = 0.8f + Rand(i, 90) * 2.6f;
            var w = 0.25f + Rand(i, 91) * 0.6f;
            var side = Rand(i, 92) < 0.5f ? -0.6f : 0.6f;
            var tone = (i % 3 == 0 ? SteelLight : Gunmetal * 1.4f) * (0.45f + Rand(i, 93) * 0.5f);
            Shard(px, py, dir, len, w, side, tone);
        }
        foreach (var (rx, top) in new[] { (RodX[0], 27.6f), (RodX[2] - 0.4f, 29.4f) })
        {
            Paint(rx - 1f, top - 1f, rx + 1f, WinY1, Mat.Metal, (float x, float y, out Vector3 c) =>
            {
                var n = (x - rx) / 0.95f;
                var jag = top + (Noise(x * 4f, 0f, 94) - 0.5f) * 1.6f;
                c = Gunmetal * 0.9f * Lit(n * 0.9f, 0f, 0.6f) / FlatLit;
                return MathF.Abs(n) < 1f && y > jag && y < WinY1 - 0.5f;
            });
        }
        Cable(HoleX - 4f, HoleY - 3f, HoleX + 1.5f, HoleY + 1f, 1.5f, new Vector3(0.40f, 0.22f, 0.11f));
        Cable(HoleX + 2f, HoleY - 4.5f, HoleX + 5.5f, HoleY + 3f, -1.2f, new Vector3(0.36f, 0.12f, 0.09f));

        // The left coolant column is shattered: a dark gap with jagged stubs of glass.
        var tx = TubeX[0];
        Paint(tx - TubeR, TubeY0 + 1.5f, tx + TubeR, TubeY1 - 1.5f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            var n = (x - tx) / TubeR;
            var upper = TubeY0 + 3.2f + (Noise(x * 3f, 1f, 95) - 0.5f) * 3f;
            var lower = TubeY1 - 3.6f + (Noise(x * 3f, 2f, 96) - 0.5f) * 3f;
            c = new Vector3(0.04f, 0.04f, 0.045f) * (1f - MathF.Abs(n) * 0.5f);
            return MathF.Abs(n) <= 1f && y > upper && y < lower;
        });
        Scorch(tx, (TubeY0 + TubeY1) / 2f, 4.5f, 0.5f);

        // The right radiator took shrapnel: a few holes punched through the fins.
        var (rx0, rx1) = Radiators[1];
        for (var i = 0; i < 7; i++)
        {
            var hx = rx0 + 2f + Rand(i, 97) * (rx1 - rx0 - 4f);
            var hy = RadY0 + 3f + Rand(i, 98) * (RadY1 - RadY0 - 6f);
            var hr = 0.5f + Rand(i, 99) * 0.7f;
            Paint(hx - hr - 0.4f, hy - hr - 0.4f, hx + hr + 0.4f, hy + hr + 0.4f, Mat.Dark, (float x, float y, out Vector3 c) =>
            {
                var d = MathF.Sqrt((x - hx) * (x - hx) + (y - hy) * (y - hy));
                c = d < hr ? new Vector3(0.02f) : SteelMid * 0.55f;
                return d < hr + 0.3f;
            });
        }

    }

    /// A bent sliver of torn metal: a thin quad lit on one face.
    private static void Shard(float cx, float cy, float dir, float len, float width, float side, Vector3 tone)
    {
        var ax = MathF.Cos(dir); var ay = MathF.Sin(dir);
        var ext = len / 2f + width;
        Paint(cx - ext, cy - ext, cx + ext, cy + ext, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var u = (x - cx) * ax + (y - cy) * ay;
            var v = -(x - cx) * ay + (y - cy) * ax;
            // tapering: wide at one end, a point at the other
            var halfW = width / 2f * (0.3f + 0.7f * (0.5f - u / len));
            c = tone * Lit(-ay * side, ax * side, 0.6f) / FlatLit;
            if (MathF.Abs(v) > halfW * 0.7f) c *= 0.6f;
            return MathF.Abs(u) <= len / 2f && MathF.Abs(v) <= halfW;
        });
    }
}
