using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The machine itself, front view on a 64-unit grid: a riveted steel cabinet on an I-beam skid;
// a ribbed radiator bank on each flank; a raised core housing in the middle with a turbine intake
// on top and a thick gasketed window onto the fuel rods; two glass coolant columns on brass collars
// whose feed pipes bend up out of the top; and a cluttered service bay along the bottom (pump,
// manifold with a valve, junction box with a dial, cable runs, a tank with a hand wheel).
public static partial class MachineArt
{
    private static readonly Vector3 SteelMid = new(0.42f, 0.42f, 0.40f);
    private static readonly Vector3 SteelLight = new(0.47f, 0.46f, 0.43f);
    private static readonly Vector3 Gunmetal = new(0.24f, 0.25f, 0.26f);
    private static readonly Vector3 FinTone = new(0.20f, 0.205f, 0.21f);
    private static readonly Vector3 Recess = new(0.06f, 0.065f, 0.07f);
    private static readonly Vector3 Brass = new(0.54f, 0.45f, 0.29f);
    private static readonly Vector3 GlassTone = new(0.10f, 0.17f, 0.18f);
    private static readonly Vector3 RubberTone = new(0.09f, 0.09f, 0.10f);
    private static readonly Vector3 Paper = new(0.60f, 0.58f, 0.51f);
    private static readonly Vector3 Hazard = new(0.64f, 0.52f, 0.17f);
    private static readonly Vector3 Ink = new(0.07f, 0.065f, 0.06f);

    // Layout shared with the glow mask and the damage passes.
    private const float CoreX0 = 19f, CoreX1 = 45f, CoreY0 = 5f, CoreY1 = 41f;
    private const float WinX0 = 25f, WinX1 = 39f, WinY0 = 18f, WinY1 = 32f, WinR = 2.6f;
    private static readonly float[] TubeX = { 17.2f, 46.8f };
    private const float TubeY0 = 11.5f, TubeY1 = 35.5f, TubeR = 2.1f;
    private const float FanX = 32f, FanY = 11.2f, FanR = 4.4f;
    private static readonly (float X0, float X1)[] Radiators = { (3.5f, 14.6f), (49.4f, 60.5f) };
    private const float RadY0 = 7f, RadY1 = 41f, FinPitch = 1.55f;
    private static readonly float[] LampX = { 27.4f, 29.7f, 32f, 34.3f, 36.6f };
    private const float LampY = 35.6f;

    private static void PaintBody()
    {
        PaintSkid();
        PaintCabinet();
        foreach (var (x0, x1) in Radiators) PaintRadiator(x0, x1);
        PaintCoreHousing();
        PaintServiceBay();
        foreach (var cx in TubeX) PaintCoolantColumn(cx);
    }

    // The I-beam the machine stands on, with two bolted feet.
    private static void PaintSkid()
    {
        Paint(0.5f, 53.5f, 63.5f, 60.5f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var tone = y < 55f ? Gunmetal * 1.25f : y < 58.6f ? Gunmetal * 0.62f : Gunmetal;
            var (nx, ny) = y < 55f ? Bevel(x, y, 0.5f, 53.5f, 63.5f, 55f, 0.45f) : y >= 58.6f ? Bevel(x, y, 0.5f, 58.6f, 63.5f, 60.5f, 0.45f) : (0f, 0f);
            c = Metal(tone, x, y, nx, ny);
            return true;
        });
        for (var bx = 4f; bx < 62f; bx += 5.5f) Bolt(bx, 56.8f, 0.55f, Gunmetal * 1.3f);
        foreach (var fx in new[] { 2f, 55f })
        {
            Paint(fx, 58f, fx + 7f, 61.5f, Mat.Metal, (float x, float y, out Vector3 c) =>
            {
                var (nx, ny) = Bevel(x, y, fx, 58f, fx + 7f, 61.5f, 0.5f);
                // hazard chevrons on the feet
                var stripe = ((int)MathF.Floor((x + y) / 1.4f) & 1) == 0;
                c = Metal(stripe ? Hazard * 0.8f : Ink * 1.5f, x, y, nx, ny, 0.1f);
                return true;
            });
        }
    }

    // The back plating: one cabinet with chamfered top corners, plate seams and rivet rows.
    private static void PaintCabinet()
    {
        Paint(2f, 3.5f, 62f, 54f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (x - 2f + (y - 3.5f) < 4.5f || 62f - x + (y - 3.5f) < 4.5f) return false;   // chamfers
            var (nx, ny) = Bevel(x, y, 2f, 3.5f, 62f, 54f, 0.7f);
            var seam = MathF.Abs(y - 41.5f) < 0.18f || MathF.Abs(x - 16.6f) < 0.18f || MathF.Abs(x - 47.4f) < 0.18f;
            c = Metal(SteelMid, x, y, nx, ny);
            if (seam) c *= 0.45f;
            else if (MathF.Abs(y - 41.9f) < 0.2f || MathF.Abs(x - 17f) < 0.2f || MathF.Abs(x - 47.8f) < 0.2f) c *= 1.18f;
            return true;
        });
        for (var y = 6f; y < 41f; y += 3.4f)
        {
            Bolt(2.9f, y, 0.42f, SteelMid);
            Bolt(61.1f, y, 0.42f, SteelMid);
        }
        for (var x = 5f; x < 60f; x += 3.6f) Bolt(x, 42.6f, 0.42f, SteelMid);
    }

    // A recessed bay of vertical cooling fins behind a raised frame, with a bolted tie bar.
    private static void PaintRadiator(float x0, float x1)
    {
        const float frame = 1.2f;
        Paint(x0, RadY0, x1, RadY1, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var inner = x >= x0 + frame && x < x1 - frame && y >= RadY0 + frame && y < RadY1 - frame;
            if (!inner)
            {
                var (nx, ny) = Bevel(x, y, x0, RadY0, x1, RadY1, 0.55f);
                c = Metal(Gunmetal * 1.35f, x, y, nx, ny);
                return true;
            }
            // Fins: each slat a little cylinder, the gaps between them near-black.
            var local = (x - x0 - frame) / FinPitch;
            var f = local - MathF.Floor(local);
            var depth = MathF.Min(y - (RadY0 + frame), RadY1 - frame - y);   // ambient occlusion at top/bottom
            var ao = 0.45f + 0.55f * Smooth(0f, 2.2f, depth);
            if (f > 0.72f) { c = Recess * ao; return true; }
            var n = (f / 0.72f) * 2f - 1f;
            c = FinTone * (Lit(n * 0.9f, 0f, 0.4f) / FlatLit) * ao * (1f + (Noise(x * 2f, y * 0.4f, 21) - 0.5f) * 0.12f);
            return true;
        });
        // tie bar across the fins
        Paint(x0 + 0.6f, 23.4f, x1 - 0.6f, 25.2f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, x0 + 0.6f, 23.4f, x1 - 0.6f, 25.2f, 0.4f);
            c = Metal(Gunmetal * 1.5f, x, y, nx, ny);
            return true;
        });
        Bolt(x0 + 1.5f, 24.3f, 0.5f, Gunmetal * 1.6f);
        Bolt(x1 - 1.5f, 24.3f, 0.5f, Gunmetal * 1.6f);
        foreach (var (bx, by) in new[] { (x0 + 0.6f, RadY0 + 0.6f), (x1 - 0.6f, RadY0 + 0.6f), (x0 + 0.6f, RadY1 - 0.6f), (x1 - 0.6f, RadY1 - 0.6f) })
            Bolt(bx, by, 0.45f, Gunmetal * 1.6f);
    }

    private static void PaintCoreHousing()
    {
        // The raised slab, its top corners clipped.
        Paint(CoreX0, CoreY0, CoreX1, CoreY1, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (x - CoreX0 + (y - CoreY0) < 2.5f || CoreX1 - x + (y - CoreY0) < 2.5f) return false;
            var (nx, ny) = Bevel(x, y, CoreX0, CoreY0, CoreX1, CoreY1, 0.8f);
            if (x - CoreX0 + (y - CoreY0) < 3.3f) (nx, ny) = (-0.45f, -0.45f);
            else if (CoreX1 - x + (y - CoreY0) < 3.3f) (nx, ny) = (0.45f, -0.45f);
            c = Metal(SteelLight, x, y, nx, ny, 0.3f);
            if (MathF.Abs(y - 16f) < 0.15f && x > CoreX0 + 1 && x < CoreX1 - 1) c *= 0.5f;   // panel seam
            return true;
        });

        PaintIntake();

        // Louvred vents flanking the intake.
        foreach (var vx in new[] { 21.6f, 37.8f })
        {
            Paint(vx, 7.6f, vx + 4.6f, 14.4f, Mat.Metal, (float x, float y, out Vector3 c) =>
            {
                var (nx, ny) = Bevel(x, y, vx, 7.6f, vx + 4.6f, 14.4f, 0.4f, raised: false);
                var slot = (y - 8.3f) / 1.25f;
                var f = slot - MathF.Floor(slot);
                c = f < 0.42f && x > vx + 0.5f && x < vx + 4.1f && y > 8.2f && y < 13.9f
                    ? Recess * (1f + f)
                    : Metal(SteelLight * 0.82f, x, y, nx, f > 0.42f && f < 0.6f ? -0.5f : ny);
                return true;
            });
        }

        PaintWindow();

        // Status strip with its row of indicator lamps (lit by the glow mask, dull here).
        Paint(25.6f, 34.2f, 38.4f, 37f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, 25.6f, 34.2f, 38.4f, 37f, 0.35f, raised: false);
            c = Metal(Gunmetal * 0.6f, x, y, nx, ny);
            return true;
        });
        var lampTones = new[] { new Vector3(0.18f, 0.32f, 0.2f), new Vector3(0.18f, 0.32f, 0.2f), new Vector3(0.4f, 0.3f, 0.1f), new Vector3(0.14f, 0.3f, 0.3f), new Vector3(0.38f, 0.12f, 0.1f) };
        for (var i = 0; i < LampX.Length; i++)
        {
            var lx = LampX[i]; var tone = lampTones[i];
            Paint(lx - 0.9f, LampY - 0.9f, lx + 0.9f, LampY + 0.9f, Mat.Lamp, (float x, float y, out Vector3 c) =>
            {
                var dx = x - lx; var dy = y - LampY; var d = MathF.Sqrt(dx * dx + dy * dy);
                c = d > 0.62f ? new Vector3(0.05f) : tone * Lit(dx / 0.62f * 0.8f, dy / 0.62f * 0.8f, 0.8f) / FlatLit;
                return d <= 0.82f;
            });
        }

        // A stencilled data plate on the left, a hazard sticker on the right.
        Paint(20.3f, 19f, 24f, 24.6f, Mat.Label, (float x, float y, out Vector3 c) =>
        {
            var row = (y - 20f) / 0.95f;
            var f = row - MathF.Floor(row);
            var text = row >= 0 && row < 4.6f && f < 0.45f && x > 20.9f && x < 23.4f - (Hash((int)row, 3, 5) * 1.2f)
                && Noise(x * 3.5f, row, 7) > 0.32f;
            c = text ? Ink : Paper * (1f + (Noise(x * 2f, y * 2f, 8) - 0.5f) * 0.1f);
            if (x < 20.5f || x > 23.8f || y < 19.2f || y > 24.4f) c *= 0.7f;
            return true;
        });
        foreach (var by in new[] { 19.4f, 24.2f }) { Bolt(20.75f, by, 0.25f, SteelLight); Bolt(23.55f, by, 0.25f, SteelLight); }
        Paint(40.1f, 19f, 43.9f, 22.8f, Mat.Label, (float x, float y, out Vector3 c) =>
        {
            var dx = x - 42f; var dy = y - 20.9f; var d = MathF.Sqrt(dx * dx + dy * dy);
            var a = MathF.Atan2(dy, dx);
            var sector = (int)MathF.Floor((a + MathF.PI * 2.5f) / (MathF.PI / 3f)) % 2 == 0;
            var mark = d < 0.32f || d > 0.5f && d < 1.4f && sector;
            c = mark ? Ink : Hazard * (1f + (Noise(x * 2f, y * 2f, 9) - 0.5f) * 0.12f);
            if (x < 40.3f || x > 43.7f || y < 19.2f || y > 22.6f) c = Ink * 2f;
            return true;
        });
        // A short warning band under the sticker.
        Paint(40.1f, 23.6f, 43.9f, 24.6f, Mat.Label, (float x, float y, out Vector3 c) =>
        {
            c = ((int)MathF.Floor((x - y) / 0.6f) & 1) == 0 ? Hazard * 0.9f : Ink;
            return true;
        });

        // Housing bolts along its edge.
        for (var y = 8f; y < 40f; y += 3.2f)
        {
            Bolt(CoreX0 + 0.75f, y, 0.38f, SteelLight * 0.95f);
            Bolt(CoreX1 - 0.75f, y, 0.38f, SteelLight * 0.95f);
        }
    }

    // Turbine intake: a raised ring around a recessed bladed rotor and a domed hub.
    private static void PaintIntake()
    {
        Paint(FanX - FanR - 0.5f, FanY - FanR - 0.5f, FanX + FanR + 0.5f, FanY + FanR + 0.5f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var dx = x - FanX; var dy = y - FanY; var d = MathF.Sqrt(dx * dx + dy * dy);
            c = default;
            if (d > FanR + 0.3f) return false;
            if (d > FanR) { c = new Vector3(0.07f); return true; }
            if (d > FanR - 1f)
            {
                var t = (d - (FanR - 1f)) * 2f - 1f;                    // -1 inner edge .. 1 outer edge
                c = Metal(SteelLight * 0.9f, x, y, dx / d * t * 0.7f, dy / d * t * 0.7f, 0.4f);
                return true;
            }
            if (d < 1.1f) { c = Brass * Lit(dx / 1.1f * 0.8f, dy / 1.1f * 0.8f, 0.5f) / FlatLit; return true; }
            // Blades: 9 curved vanes over a dark well.
            var a = MathF.Atan2(dy, dx) + d * 0.35f;
            var s = a / (MathF.PI * 2f / 9f);
            var f = s - MathF.Floor(s);
            var well = 0.5f + 0.5f * Smooth(1.1f, FanR - 1f, d);
            c = f < 0.55f ? Gunmetal * (0.7f + f) * well : Recess * well;
            return true;
        });
    }

    // The inspection window: steel bezel with bolts, black rubber gasket, dark glass, and the
    // fuel-rod assembly visible behind it.
    private static void PaintWindow()
    {
        const float bezel = 1.1f, gasket = 0.9f;
        Paint(WinX0 - bezel, WinY0 - bezel, WinX1 + bezel, WinY1 + bezel, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, WinX0 - bezel, WinY0 - bezel, WinX1 + bezel, WinY1 + bezel, WinR + bezel)) return false;
            if (!InRoundRect(x, y, WinX0, WinY0, WinX1, WinY1, WinR))
            {
                var (nx, ny) = Bevel(x, y, WinX0 - bezel, WinY0 - bezel, WinX1 + bezel, WinY1 + bezel, bezel);
                c = Metal(Gunmetal * 1.5f, x, y, nx * 0.8f, ny * 0.8f, 0.4f);
                return true;
            }
            return false;
        });
        Paint(WinX0, WinY0, WinX1, WinY1, Mat.Rubber, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, WinX0, WinY0, WinX1, WinY1, WinR)) return false;
            if (InRoundRect(x, y, WinX0 + gasket, WinY0 + gasket, WinX1 - gasket, WinY1 - gasket, WinR - gasket)) return false;
            var (nx, ny) = Bevel(x, y, WinX0, WinY0, WinX1, WinY1, gasket, raised: false);
            c = RubberTone * Lit(nx, ny, 0.15f) / FlatLit;
            return true;
        });
        Paint(WinX0 + gasket, WinY0 + gasket, WinX1 - gasket, WinY1 - gasket, Mat.Glass, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, WinX0 + gasket, WinY0 + gasket, WinX1 - gasket, WinY1 - gasket, WinR - gasket)) return false;
            // Behind the glass: three fuel rods between two clamp rings.
            var behind = GlassTone;
            foreach (var rx in RodX)
            {
                var n = (x - rx) / 0.95f;
                if (MathF.Abs(n) < 1f && y > WinY0 + 1.9f && y < WinY1 - 1.9f)
                    behind = Gunmetal * 0.7f * Lit(n * 0.9f, 0f, 0.6f) / FlatLit;
            }
            if (MathF.Abs(y - (WinY0 + 3.2f)) < 0.55f || MathF.Abs(y - (WinY1 - 3.2f)) < 0.55f)
                behind = Gunmetal * 0.85f * (y < (WinY0 + WinY1) / 2 ? 1.1f : 0.9f);
            // Depth: darker towards the frame.
            var edge = MathF.Min(MathF.Min(x - WinX0, WinX1 - x), MathF.Min(y - WinY0, WinY1 - y));
            behind *= 0.55f + 0.45f * Smooth(gasket, gasket + 2.5f, edge);
            // A soft diagonal reflection across the pane.
            var streak = MathF.Abs((x - WinX0) + (y - WinY0) * 0.8f - 6.5f);
            if (streak < 1.1f) behind += new Vector3(0.10f, 0.13f, 0.13f) * (1f - streak / 1.1f);
            c = behind;
            return true;
        });
        foreach (var (bx, by) in WindowBolts()) Bolt(bx, by, 0.36f, Gunmetal * 1.8f);
    }

    private static readonly float[] RodX = { 29.2f, 32f, 34.8f };

    private static (float, float)[] WindowBolts() => new[]
    {
        (WinX0 - 0.45f, WinY0 + 3f), (WinX0 - 0.45f, WinY1 - 3f), (WinX1 + 0.45f, WinY0 + 3f), (WinX1 + 0.45f, WinY1 - 3f),
        (WinX0 + 4f, WinY0 - 0.5f), (WinX1 - 4f, WinY0 - 0.5f), (WinX0 + 4f, WinY1 + 0.5f), (WinX1 - 4f, WinY1 + 0.5f),
    };

    // A glass coolant column on brass collars; its feed pipe bends up and out through the top,
    // its return pipe drops into the service bay.
    private static void PaintCoolantColumn(float cx)
    {
        var outward = cx < 32f ? 1f : -1f;   // the top bend turns toward the middle
        // Top feed: up from the column, then a quarter bend and out through the top edge.
        var bendCx = cx + outward * 4f;
        var top = outward > 0 ? Arc(bendCx, 6.2f, 4f, 180f, 270f) : Arc(bendCx, 6.2f, 4f, 0f, -90f);
        var feed = new Vector2[top.Length + 2];
        feed[0] = new Vector2(cx, TubeY0);
        Array.Copy(top, 0, feed, 1, top.Length);
        feed[^1] = new Vector2(bendCx, -1f);
        Pipe(feed, 1.55f, Gunmetal * 1.15f);
        // Union flange where the feed leaves the cabinet.
        Collar(bendCx, 2.6f, 2.2f, 1f, horizontal: true);

        // Return: straight down into the manifold.
        Pipe(new[] { new Vector2(cx, TubeY1), new Vector2(cx, 46.2f) }, 1.3f, Gunmetal * 1.05f);

        // Glass column.
        Paint(cx - TubeR, TubeY0, cx + TubeR, TubeY1, Mat.Glass, (float x, float y, out Vector3 c) =>
        {
            var n = (x - cx) / TubeR;
            c = default;
            if (MathF.Abs(n) > 1f) return false;
            var liquid = GlassTone * 1.25f * (0.75f + 0.25f * Noise(x * 1.5f, y * 0.6f, 31));
            var body = liquid * Lit(n * 0.6f, 0f, 0f) / FlatLit;
            if (MathF.Abs(n) > 0.82f) body *= 0.55f;                                   // glass wall
            var glint = MathF.Abs(n + 0.45f);
            if (glint < 0.14f) body += new Vector3(0.22f, 0.26f, 0.26f) * (1f - glint / 0.14f);
            c = body;
            return true;
        });
        // Collars top, middle and bottom.
        Collar(cx, TubeY0 + 0.4f, TubeR + 0.55f, 1.5f, horizontal: true);
        Collar(cx, (TubeY0 + TubeY1) / 2f, TubeR + 0.25f, 0.7f, horizontal: true);
        Collar(cx, TubeY1 - 0.4f, TubeR + 0.55f, 1.5f, horizontal: true);
    }

    /// A brass band around a tube - shaded as a short cylinder across the tube's axis.
    private static void Collar(float cx, float cy, float halfLength, float thickness, bool horizontal)
    {
        var hx = horizontal ? halfLength : thickness / 2f;
        var hy = horizontal ? thickness / 2f : halfLength;
        Paint(cx - hx, cy - hy, cx + hx, cy + hy, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var along = horizontal ? (x - cx) / hx : (y - cy) / hy;      // across the tube
            var across = horizontal ? (y - cy) / hy : (x - cx) / hx;     // along the tube
            var nx = horizontal ? along * 0.85f : across * 0.5f;
            var ny = horizontal ? across * 0.5f : along * 0.85f;
            c = Brass * Lit(nx, ny, 0.5f) / FlatLit * (1f + (Noise(x * 3f, y * 3f, 41) - 0.5f) * 0.12f);
            if (MathF.Abs(across) > 0.8f) c *= 0.6f;
            return true;
        });
    }

    // The service bay under the core: a sunken dark well full of pumps, pipes and cables.
    private static void PaintServiceBay()
    {
        Paint(3.4f, 43.6f, 60.6f, 53.4f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            var depth = MathF.Min(MathF.Min(x - 3.4f, 60.6f - x), y - 43.6f);
            var (nx, ny) = Bevel(x, y, 3.4f, 43.6f, 60.6f, 53.4f, 0.5f, raised: false);
            c = Metal(Gunmetal * 0.5f, x, y, nx, ny) * (0.5f + 0.5f * Smooth(0f, 2f, depth));
            return true;
        });

        // Manifold across the bay, a T for each column's return, and a valve in the middle.
        Pipe(new[] { new Vector2(8f, 46.6f), new Vector2(56f, 46.6f) }, 1.15f, Gunmetal * 1.2f);
        foreach (var cx in TubeX) Collar(cx, 46.6f, 1.9f, 1f, horizontal: false);
        Collar(32f, 46.6f, 1.6f, 1.6f, horizontal: false);
        HandWheel(32f, 46.6f, 1.7f, new Vector3(0.45f, 0.13f, 0.1f));

        // Left: a finned pump motor lying on its side.
        Paint(4.6f, 47.6f, 14.2f, 53f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var n = (y - 50.3f) / 2.7f;
            c = default;
            if (MathF.Abs(n) > 1f) return false;
            var fin = ((x - 4.6f) / 0.8f) % 1f;
            var tone = x > 12.8f ? SteelLight * 0.75f : fin < 0.3f ? Recess * 2f : Gunmetal * 1.25f;
            c = tone * Lit(0f, n * 0.9f, 0.4f) / FlatLit;
            return true;
        });
        Bolt(13.5f, 50.3f, 0.9f, SteelLight * 0.7f);

        // Junction box with a dial gauge.
        Paint(27.6f, 48.6f, 36.4f, 53.2f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, 27.6f, 48.6f, 36.4f, 53.2f, 0.45f);
            c = Metal(Gunmetal * 1.4f, x, y, nx, ny);
            return true;
        });
        Paint(30.2f, 49.1f, 33.8f, 52.7f, Mat.Label, (float x, float y, out Vector3 c) =>
        {
            var dx = x - 32f; var dy = y - 50.9f; var d = MathF.Sqrt(dx * dx + dy * dy);
            c = default;
            if (d > 1.75f) return false;
            if (d > 1.4f) { c = Brass * Lit(dx / d * 0.6f, dy / d * 0.6f, 0.4f) / FlatLit; return true; }
            var needle = MathF.Abs(dx * 0.8f + dy * 0.6f) < 0.12f && dx * 0.6f - dy * 0.8f > -0.2f && dx < 0.4f;
            var tick = d > 1.05f && ((int)MathF.Floor((MathF.Atan2(dy, dx) + 4f) / 0.35f) & 1) == 0 && dy < 0.6f;
            c = needle ? new Vector3(0.45f, 0.1f, 0.08f) : tick ? Ink : Paper * (0.85f + 0.15f * (1f - d / 1.4f));
            return true;
        });
        Bolt(28.4f, 49.4f, 0.3f, SteelLight); Bolt(35.6f, 49.4f, 0.3f, SteelLight);
        Bolt(28.4f, 52.4f, 0.3f, SteelLight); Bolt(35.6f, 52.4f, 0.3f, SteelLight);

        // Cable runs sagging from the box out to both sides.
        Cable(36.4f, 50f, 47.5f, 49.4f, 1.6f, new Vector3(0.08f, 0.08f, 0.085f));
        Cable(36.4f, 51.6f, 48.5f, 51.2f, 1.1f, new Vector3(0.36f, 0.12f, 0.09f));
        Cable(27.6f, 50.4f, 15.2f, 49.2f, 1.4f, new Vector3(0.08f, 0.08f, 0.085f));
        Cable(27.6f, 52f, 16f, 51.4f, 0.9f, new Vector3(0.42f, 0.36f, 0.14f));

        // Right: a pressure tank with a hand wheel.
        Paint(49.2f, 47.6f, 59.6f, 53.1f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, 49.2f, 47.6f, 59.6f, 53.1f, 2.4f)) return false;
            var n = (y - 50.35f) / 2.75f;
            c = Metal(SteelMid * 1.05f, x, y, 0f, n * 0.85f, 0.4f);
            if (MathF.Abs(x - 51.6f) < 0.25f || MathF.Abs(x - 57.2f) < 0.25f) c *= 0.55f;   // weld seams
            return true;
        });
        HandWheel(54.4f, 50.35f, 1.9f, new Vector3(0.45f, 0.13f, 0.1f));
    }

    private static void Cable(float ax, float ay, float bx, float by, float sag, Vector3 tone)
    {
        var points = new Vector2[12];
        for (var i = 0; i < points.Length; i++)
        {
            var t = i / (points.Length - 1f);
            points[i] = new Vector2(MathHelper.Lerp(ax, bx, t), MathHelper.Lerp(ay, by, t) + sag * 4f * t * (1f - t));
        }
        Pipe(points, 0.42f, tone, Mat.Rubber, 0.5f);
    }

    // A spoked valve wheel: rim, four spokes, hub.
    private static void HandWheel(float cx, float cy, float r, Vector3 tone)
    {
        Paint(cx - r - 0.3f, cy - r - 0.3f, cx + r + 0.3f, cy + r + 0.3f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var dx = x - cx; var dy = y - cy; var d = MathF.Sqrt(dx * dx + dy * dy);
            c = default;
            if (d > r) return false;
            var rim = d > r - 0.5f;
            var hub = d < 0.45f;
            var spoke = MathF.Abs(dx - dy) < 0.28f || MathF.Abs(dx + dy) < 0.28f;
            if (!rim && !hub && !spoke) return false;
            var t = rim ? ((d - (r - 0.25f)) / 0.25f) : 0f;
            c = tone * Lit(rim ? dx / d * t * 0.7f : 0f, rim ? dy / d * t * 0.7f : 0f, 0.5f) / FlatLit;
            return true;
        });
    }
}
