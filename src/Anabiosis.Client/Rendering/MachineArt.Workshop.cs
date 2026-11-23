using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

/// <summary>Baked art for the fabricator and the deconstructor: [wear stage 0..2, lit 0/1].</summary>
public sealed record WorkshopArt(Texture2D[,] Fabricator, Texture2D[,] Deconstructor)
{
    public Texture2D? For(CustomDeviceKind kind, int stage, bool lit) => kind switch
    {
        CustomDeviceKind.Fabricator => Fabricator[stage, lit ? 1 : 0],
        CustomDeviceKind.Deconstructor => Deconstructor[stage, lit ? 1 : 0],
        _ => null,
    };
}

// Direct user request ("давай переработаем фабрикатор и деконструктор на основе этих спрайтов", with
// Barotrauma's fabricator/deconstructor sheets as the style reference - original art, not a trace).
// Painted with the reactor's painter (lit normals, brushed steel, bolts, hoses, grime, wear stages) on a
// portrait 24x48-unit canvas - the 1.5x3-tile footprint both machines have, at twice its on-screen size.
//   Fabricator: a domed hood carrying the job screen and an intake grille, hoses down into a cabinet
//   with a glass print chamber (gantry, head, a part half-built) over two louvred doors.
//   Deconstructor: a stickered housing with an exhaust grille, a feed hopper with a hot intake slot and
//   two saw blades in its throat, standing on two legs over a scrap bin, a cable down to a junction box.
// Stage 1 adds rust, scorch, dents and a cracked pane; stage 2 a blown-out breach. Lit = powered: the
// screen and the intake glow (and the machine casts its own light - Game1.DeviceLights).
public static partial class MachineArt
{
    private const int WorkshopWidth = 144, WorkshopHeight = 288;
    private const float WorkshopPx = 6f; // 24 x 48 design units

    public static WorkshopArt CreateWorkshopArt(GraphicsDevice device)
    {
        var fabricator = new Texture2D[3, 2];
        var deconstructor = new Texture2D[3, 2];
        for (var lit = 0; lit < 2; lit++)
        {
            BakeStages(device, fabricator, lit, () => PaintFabricator(lit == 1), FabricatorWear);
            BakeStages(device, deconstructor, lit, () => PaintDeconstructor(lit == 1), DeconstructorWear);
        }
        _rgb = Array.Empty<Vector3>();
        _mat = Array.Empty<Mat>();
        return new WorkshopArt(fabricator, deconstructor);
    }

    private static void BakeStages(GraphicsDevice device, Texture2D[,] into, int lit, Action paint, Action<int> wear)
    {
        Begin(WorkshopWidth, WorkshopHeight, WorkshopPx);
        paint();
        Weather(0f);
        into[0, lit] = Bake(device);
        wear(1);
        Weather(0.55f);
        into[1, lit] = Bake(device);
        wear(2);
        Weather(0.4f);
        into[2, lit] = Bake(device);
    }

    // ---- fabricator ------------------------------------------------------------------------

    private static void PaintFabricator(bool lit)
    {
        // Hood: a rounded cap over the frame that holds the screen and the grille.
        Paint(1f, 1f, 23f, 16.4f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, 1f, 1f, 23f, 22f, 4.5f) || y >= 16.4f) return false;
            var (nx, ny) = Bevel(x, y, 1f, 1f, 23f, 16.4f, 0.8f);
            if (y < 5.4f) ny = -0.75f * (1f - (y - 1f) / 4.4f);              // the dome rolls away at the top
            c = Metal(Gunmetal * 1.4f, x, y, nx, ny, 0.4f);
            if (MathF.Abs(y - 5.5f) < 0.16f) c *= 0.45f;                       // cap seam
            return true;
        });
        foreach (var bx in new[] { 4.2f, 12f, 19.8f }) Bolt(bx, bx == 12f ? 2.6f : 3.4f, 0.45f, Gunmetal * 1.8f);

        // Job screen.
        Window(2.6f, 6.6f, 13.4f, 14.6f, 0.9f);
        Paint(3.3f, 7.3f, 12.7f, 13.9f, Mat.Glass, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, 3.3f, 7.3f, 12.7f, 13.9f, 0.5f)) return false;
            var glass = lit ? new Vector3(0.03f, 0.13f, 0.15f) : new Vector3(0.025f, 0.03f, 0.035f);
            if (lit)
            {
                var ink = new Vector3(0.38f, 0.86f, 0.9f);
                var row = (y - 8.1f) / 1.2f;
                var inRow = row >= 0 && row < 3 && row - MathF.Floor(row) < 0.42f && x > 4f && x < 4f + 4.6f * (1f - 0.25f * MathF.Floor(row));
                var bar = y > 12.1f && y < 12.9f && x > 4f && x < 12f;
                var barFill = bar && x < 4f + 8f * 0.64f;
                var part = MathF.Abs(x - 10.6f) < 1.4f && y > 8.4f && y < 11.2f && (MathF.Abs(x - 10.6f) > 1.1f || y < 8.7f || y > 10.9f);
                if (inRow || barFill || part) glass = Vector3.Lerp(glass, ink, 0.85f);
                else if (bar) glass = Vector3.Lerp(glass, ink, 0.25f);
                glass *= 0.85f + 0.15f * MathF.Sin(y * 9f) * MathF.Sin(y * 9f);  // scanlines
            }
            var streak = MathF.Abs((x - 3.3f) * 0.9f + (y - 7.3f) - 4f);
            if (streak < 0.9f) glass += new Vector3(0.07f, 0.09f, 0.09f) * (1f - streak / 0.9f);
            c = glass;
            return true;
        });

        // Intake grille.
        Grille(14.6f, 6.6f, 21.4f, 14.6f, 1.05f);

        // Neck: hoses and two struts between the hood and the cabinet.
        Paint(2.5f, 16.2f, 21.5f, 19.6f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            c = Recess * (0.6f + 0.4f * Smooth(16.2f, 18f, y));
            return true;
        });
        Pipe(new[] { new Vector2(4f, 15.5f), new Vector2(4f, 20f) }, 0.75f, Gunmetal * 1.3f);
        Pipe(new[] { new Vector2(20f, 15.5f), new Vector2(20f, 20f) }, 0.75f, Gunmetal * 1.3f);
        Cable(6.2f, 16f, 9.4f, 19.6f, 0.9f, new Vector3(0.09f, 0.09f, 0.095f));
        Cable(8.6f, 16f, 14.2f, 19.6f, 1.3f, new Vector3(0.36f, 0.12f, 0.09f));
        Cable(15.4f, 16f, 17.6f, 19.6f, 0.7f, new Vector3(0.09f, 0.09f, 0.095f));

        // Cabinet.
        Paint(1f, 19.4f, 23f, 45.6f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, 1f, 19.4f, 23f, 45.6f, 0.8f);
            c = Metal(SteelMid * 0.92f, x, y, nx, ny);
            if (MathF.Abs(y - 29.2f) < 0.15f || (y > 29.2f && MathF.Abs(x - 12f) < 0.15f)) c *= 0.45f;
            return true;
        });

        // Print chamber behind glass.
        Window(2.2f, 20.4f, 21.8f, 28.4f, 0.8f);
        Paint(2.9f, 21.1f, 21.1f, 27.7f, Mat.Glass, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, 2.9f, 21.1f, 21.1f, 27.7f, 0.4f)) return false;
            var inside = new Vector3(0.05f, 0.055f, 0.06f) * (0.7f + 0.3f * Smooth(21.1f, 24f, y));
            if (y > 26.2f && y < 27.1f) inside = SteelMid * 0.7f;                             // bed
            if (MathF.Abs(y - 22.6f) < 0.35f) inside = Gunmetal * 1.6f;                        // gantry rail
            if (x > 14.2f && x < 16.6f && y > 22.2f && y < 24.4f) inside = Gunmetal * 1.9f;    // head
            for (var layer = 0; layer < 4; layer++)                                            // the part, layer by layer
            {
                var half = 2.4f - layer * 0.35f;
                var top = 26.2f - (layer + 1) * 0.55f;
                if (MathF.Abs(x - 9f) < half && y > top && y < top + 0.5f)
                    inside = layer == 3 && lit ? new Vector3(0.95f, 0.55f, 0.22f) : SteelLight * (0.8f - layer * 0.05f);
            }
            if (lit)
            {
                var d = new Vector2(x - 15.4f, y - 24.8f).Length();
                if (d < 0.5f) inside = new Vector3(1f, 0.75f, 0.4f);
                inside += new Vector3(0.24f, 0.12f, 0.04f) * MathF.Max(0f, 1f - d / 3.5f);      // the hot nozzle lights the chamber
            }
            var streak = MathF.Abs((x - 2.9f) * 0.5f + (y - 21.1f) - 3.2f);
            if (streak < 0.7f) inside += new Vector3(0.06f, 0.07f, 0.07f) * (1f - streak / 0.7f);
            c = inside;
            return true;
        });

        // Doors.
        foreach (var (dx0, dx1, handleX) in new[] { (2.2f, 11.5f, 10.6f), (12.5f, 21.8f, 13.4f) })
        {
            Paint(dx0, 30f, dx1, 44.4f, Mat.Metal, (float x, float y, out Vector3 c) =>
            {
                var (nx, ny) = Bevel(x, y, dx0, 30f, dx1, 44.4f, 0.6f);
                c = Metal(SteelMid * 1.05f, x, y, nx, ny);
                var slot = (y - 39.2f) / 0.9f;
                if (slot >= 0 && slot < 5 && slot - MathF.Floor(slot) < 0.45f && x > dx0 + 1.4f && x < dx1 - 1.4f)
                    c = Recess * 1.5f;
                return true;
            });
            Pipe(new[] { new Vector2(handleX, 33f), new Vector2(handleX, 37f) }, 0.32f, SteelLight);
        }
        Sticker(3.4f, 31.2f, 8.4f, 35f, Paper);
        Hazard_Label(17.6f, 31f, 20.8f, 33.4f);
        Hazard_Label(4f, 36f, 6.2f, 37.6f);

        // Plinth and feet.
        Paint(1.4f, 45.4f, 22.6f, 47.4f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, 1.4f, 45.4f, 22.6f, 47.4f, 0.35f);
            c = Metal(Gunmetal * 0.9f, x, y, nx, ny);
            return true;
        });
        foreach (var fx in new[] { 2f, 18.6f }) Chevrons(fx, 46f, fx + 3.4f, 47.8f);
    }

    private static void FabricatorWear(int stage)
    {
        if (stage == 1)
        {
            RustBloom(71);
            Scorch(18f, 38f, 3f, 0.5f);
            Scorch(5f, 22f, 2.4f, 0.4f);
            Dent(16.5f, 34.5f, 1.8f);
            CrackStar(5.4f, 9.2f, 6, 72);
            LostBolt(19.8f, 3.4f);
            return;
        }
        // Wrecked: the grille side of the hood blown open, the chamber glass shattered, the screen dead.
        Stain(3.3f, 7.3f, 12.7f, 13.9f, new Vector3(0.02f), (x, y, mat) => mat == Mat.Glass ? 0.85f : 0f);
        CrackStar(9.5f, 11f, 8, 73);
        CrackStar(12f, 24f, 9, 74);
        CrackStar(6f, 25.5f, 6, 75);
        Breach(17.6f, 9.6f, 3.6f, 76);
        Scorch(9f, 41f, 3.5f, 0.6f);
    }

    // ---- deconstructor ---------------------------------------------------------------------

    private static void PaintDeconstructor(bool lit)
    {
        // Housing with its top-left corner cut away.
        Paint(1f, 1f, 23f, 14.2f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (x - 1f + (y - 1f) < 3.6f) return false;
            var (nx, ny) = Bevel(x, y, 1f, 1f, 23f, 14.2f, 0.8f);
            if (x - 1f + (y - 1f) < 4.4f) (nx, ny) = (-0.45f, -0.45f);
            c = Metal(SteelLight * 0.95f, x, y, nx, ny, 0.3f);
            if (MathF.Abs(x - 11f) < 0.14f) c *= 0.5f;
            return true;
        });
        Hazard_Label(2.6f, 4.4f, 9.4f, 6.4f);
        Sticker(2.6f, 7f, 7.8f, 8.8f, Paper);
        Hazard_Label(2.6f, 9.4f, 8.6f, 11.2f);
        Grille(12.2f, 3.2f, 21.6f, 11.8f, 1.1f);
        foreach (var (bx, by) in new[] { (21.8f, 2.2f), (21.8f, 13f), (2.2f, 13f), (12f, 13f) }) Bolt(bx, by, 0.4f, SteelLight);

        // Brace under the housing.
        Paint(1.6f, 14.2f, 22.4f, 15.8f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, 1.6f, 14.2f, 22.4f, 15.8f, 0.4f);
            c = Metal(Gunmetal * 1.1f, x, y, nx, ny);
            return true;
        });

        // Feed hopper: a funnel narrowing from 2..22 to 6..18.
        static float Left(float y) => MathHelper.Lerp(2f, 6f, Math.Clamp((y - 15.8f) / 14.2f, 0f, 1f));
        static float Right(float y) => MathHelper.Lerp(22f, 18f, Math.Clamp((y - 15.8f) / 14.2f, 0f, 1f));
        Paint(2f, 15.8f, 22f, 30f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            c = default;
            float l = Left(y), r = Right(y);
            if (x < l || x > r) return false;
            var inner = x > l + 1.4f && x < r - 1.4f && y > 16.6f && y < 29f;
            if (!inner)
            {
                var nx = x < (l + r) / 2f ? -0.55f : 0.55f;
                c = Metal(Gunmetal * 1.45f, x, y, nx, y > 28.8f ? 0.5f : 0f, 0.35f);
                return true;
            }
            // The throat: dark, deeper toward the bottom.
            c = Recess * (1.2f - 0.6f * Smooth(17f, 29f, y));
            return true;
        });
        // Two saw blades in the throat.
        foreach (var (sx, spin) in new[] { (9.6f, 0f), (14.4f, 0.4f) })
        {
            Paint(sx - 3.3f, 21f, sx + 3.3f, 28.6f, Mat.Metal, (float x, float y, out Vector3 c) =>
            {
                c = default;
                var dx = x - sx; var dy = y - 24.8f; var d = MathF.Sqrt(dx * dx + dy * dy);
                if (x < Left(y) + 1.4f || x > Right(y) - 1.4f) return false;
                var a = MathF.Atan2(dy, dx) + spin;
                var tooth = (a / (MathF.PI * 2f / 18f)) % 1f;
                if (tooth < 0) tooth += 1f;
                var edge = 2.6f + (tooth < 0.5f ? tooth : 1f - tooth) * 0.9f;
                if (d > edge) return false;
                if (d < 0.5f) { c = Gunmetal * 0.8f; return true; }
                var ring = MathF.Abs(d - 1.3f) < 0.15f;
                c = SteelLight * 1.05f * Lit(dx / edge * 0.5f, dy / edge * 0.5f, 0.6f) / FlatLit * (ring ? 0.7f : 1f);
                c *= 0.55f + 0.45f * Smooth(21f, 24f, y); // the upper half sits in the throat's shadow
                return true;
            });
        }
        // The hot intake slot across the top of the throat.
        Paint(4.2f, 16.8f, 19.8f, 19.8f, Mat.Lamp, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, 4.2f, 16.8f, 19.8f, 19.8f, 1.2f)) return false;
            var t = 1f - MathF.Abs(y - 18.3f) / 1.5f;
            c = lit ? Vector3.Lerp(new Vector3(0.9f, 0.45f, 0.18f), new Vector3(1f, 0.92f, 0.78f), t * t) : Recess * 1.4f;
            return true;
        });

        // Legs, feet and the scrap bin between them.
        foreach (var lx in new[] { 5f, 16f })
        {
            Paint(lx, 30f, lx + 3f, 44.2f, Mat.Metal, (float x, float y, out Vector3 c) =>
            {
                var n = ((x - lx) / 3f) * 2f - 1f;
                c = Metal(Gunmetal * 1.35f, x, y, n * 0.7f, 0f, 0.35f);
                if (MathF.Abs(y - 37f) < 0.15f) c *= 0.5f;
                return true;
            });
            Paint(lx - 1f, 44.2f, lx + 4f, 46.8f, Mat.Metal, (float x, float y, out Vector3 c) =>
            {
                var (nx, ny) = Bevel(x, y, lx - 1f, 44.2f, lx + 4f, 46.8f, 0.4f);
                c = Metal(Gunmetal * 1.1f, x, y, nx, ny);
                return true;
            });
            Bolt(lx + 1.5f, 45.5f, 0.4f, Gunmetal * 1.8f);
        }
        Paint(8.4f, 33f, 15.6f, 43.4f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, 8.4f, 33f, 15.6f, 43.4f, 0.5f, raised: false);
            c = Metal(Recess * 2.2f, x, y, nx, ny);
            return true;
        });
        for (var i = 0; i < 9; i++)
        {
            var px = 9.2f + Rand(i, 201) * 5.6f; var py = 37.5f + Rand(i, 202) * 5f;
            Shard(px, py, Rand(i, 203) * MathF.PI, 0.8f + Rand(i, 204) * 1.4f, 0.4f + Rand(i, 205) * 0.4f,
                Rand(i, 206) < 0.5f ? -0.6f : 0.6f, (i % 3 == 0 ? Brass : SteelLight) * (0.55f + Rand(i, 207) * 0.4f));
        }

        // A cable down the left edge into a junction box.
        Pipe(new[] { new Vector2(2.4f, 15f), new Vector2(2.4f, 33.2f) }, 0.45f, new Vector3(0.09f, 0.09f, 0.095f), Mat.Rubber, 0.5f);
        Paint(0.6f, 33f, 4.6f, 38.4f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var (nx, ny) = Bevel(x, y, 0.6f, 33f, 4.6f, 38.4f, 0.4f);
            c = Metal(Gunmetal * 1.5f, x, y, nx, ny);
            return true;
        });
        var leds = new[] { new Vector3(0.3f, 0.85f, 0.4f), new Vector3(0.95f, 0.65f, 0.2f), new Vector3(0.9f, 0.2f, 0.15f) };
        for (var i = 0; i < 3; i++)
        {
            var ly = 34.3f + i * 1.3f; var tone = lit && i < 2 ? leds[i] : leds[i] * 0.3f;
            Paint(1.8f, ly - 0.5f, 3.4f, ly + 0.5f, Mat.Lamp, (float x, float y, out Vector3 c) =>
            {
                var d = new Vector2(x - 2.6f, y - ly).Length();
                c = d > 0.38f ? new Vector3(0.04f) : tone * Lit((x - 2.6f) / 0.4f * 0.7f, (y - ly) / 0.4f * 0.7f, 0.8f) / FlatLit;
                return d < 0.5f;
            });
        }
    }

    private static void DeconstructorWear(int stage)
    {
        if (stage == 1)
        {
            RustBloom(81);
            Scorch(12f, 30f, 3.4f, 0.55f);
            Scorch(18f, 9f, 2.6f, 0.4f);
            Dent(6.5f, 6f, 1.7f);
            LostBolt(21.8f, 13f);
            return;
        }
        // Wrecked: the grille torn out, the hopper's lip split, soot everywhere around the throat.
        Breach(16.8f, 7.4f, 3.3f, 82);
        Breach(19.4f, 21.5f, 2.2f, 83);
        Scorch(12f, 20f, 5f, 0.7f);
        Stain(4.2f, 16.8f, 19.8f, 19.8f, Soot, (x, y, mat) => mat == Mat.Lamp ? 0.6f : 0f); // the slot half choked
    }

    // ---- shared pieces ---------------------------------------------------------------------

    // A bolted bezel with a rubber gasket inside it, for a screen or a viewing pane.
    private static void Window(float x0, float y0, float x1, float y1, float bezel)
    {
        Paint(x0, y0, x1, y1, Mat.Rubber, (float x, float y, out Vector3 c) =>
        {
            c = default;
            if (!InRoundRect(x, y, x0, y0, x1, y1, bezel)) return false;
            var inner = InRoundRect(x, y, x0 + bezel * 0.55f, y0 + bezel * 0.55f, x1 - bezel * 0.55f, y1 - bezel * 0.55f, bezel * 0.6f);
            var (nx, ny) = Bevel(x, y, x0, y0, x1, y1, bezel * 0.55f);
            c = inner ? RubberTone * Lit(0f, 0f, 0.1f) / FlatLit : Metal(Gunmetal * 1.7f, x, y, nx * 0.8f, ny * 0.8f, 0.4f);
            return true;
        });
    }

    // A recessed frame of horizontal slats over a dark cavity.
    private static void Grille(float x0, float y0, float x1, float y1, float pitch)
    {
        Paint(x0, y0, x1, y1, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var frame = x < x0 + 0.7f || x > x1 - 0.7f || y < y0 + 0.7f || y > y1 - 0.7f;
            if (frame)
            {
                var (nx, ny) = Bevel(x, y, x0, y0, x1, y1, 0.7f);
                c = Metal(Gunmetal * 1.6f, x, y, nx, ny);
                return true;
            }
            var s = (y - y0 - 0.7f) / pitch;
            var f = s - MathF.Floor(s);
            c = f < 0.6f ? FinTone * 1.7f * Lit(0f, (f / 0.6f * 2f - 1f) * 0.8f, 0.5f) / FlatLit : Recess;
            return true;
        });
    }

    private static void Sticker(float x0, float y0, float x1, float y1, Vector3 paper)
    {
        Paint(x0, y0, x1, y1, Mat.Label, (float x, float y, out Vector3 c) =>
        {
            var row = (y - y0 - 0.4f) / 0.75f;
            var f = row - MathF.Floor(row);
            var text = row >= 0 && f < 0.42f && y < y1 - 0.3f && x > x0 + 0.4f && x < x1 - 0.4f - Hash((int)row, 7, 9) * 1.5f
                && Noise(x * 3.5f, row, 11) > 0.3f;
            c = text ? Ink : paper * (0.95f + (Noise(x * 2f, y * 2f, 12) - 0.5f) * 0.12f);
            return true;
        });
    }

    // A yellow warning sticker: a black triangle and a couple of print lines.
    private static void Hazard_Label(float x0, float y0, float x1, float y1)
    {
        var h = y1 - y0;
        Paint(x0, y0, x1, y1, Mat.Label, (float x, float y, out Vector3 c) =>
        {
            var tx = x - (x0 + h * 0.55f); var ty = y - y0;
            var tri = ty > h * 0.15f && ty < h * 0.85f && MathF.Abs(tx) < (ty - h * 0.15f) * 0.6f;
            var lines = x > x0 + h * 1.2f && x < x1 - 0.3f && ((int)MathF.Floor((y - y0) / (h / 3.2f)) % 2 == 1);
            c = tri || lines ? Ink : Hazard * (0.95f + (Noise(x * 2f, y * 2f, 13) - 0.5f) * 0.1f);
            return true;
        });
    }

    private static void Chevrons(float x0, float y0, float x1, float y1)
    {
        Paint(x0, y0, x1, y1, Mat.Label, (float x, float y, out Vector3 c) =>
        {
            c = ((int)MathF.Floor((x + y) / 0.9f) & 1) == 0 ? Hazard * 0.85f : Ink * 1.4f;
            return true;
        });
    }

    private static void RustBloom(int seed)
    {
        Stain(0, 0, CanvasUnitsW, CanvasUnitsH, Rust, (x, y, mat) =>
        {
            if (mat is not (Mat.Metal or Mat.Dark)) return 0f;
            var r = Fbm(x * 0.24f, y * 0.24f, seed) + Smooth(CanvasUnitsH * 0.5f, CanvasUnitsH, y) * 0.12f;
            return Smooth(0.56f, 0.7f, r) * 0.7f * (0.7f + 0.3f * Noise(x * 3f, y * 3f, seed + 1));
        });
        for (var i = 0; i < 7; i++)
        {
            var rx = 1.5f + Rand(i, seed + 2) * (CanvasUnitsW - 3f); var ry = 2f + Rand(i, seed + 3) * (CanvasUnitsH * 0.7f);
            var len = 2f + Rand(i, seed + 4) * 4f;
            Stain(rx - 0.4f, ry, rx + 0.4f, ry + len, Rust * 0.8f, (x, y, mat) =>
                mat is Mat.Metal or Mat.Dark && MathF.Abs(x - rx) < 0.3f * (1f - (y - ry) / len * 0.5f) ? 0.5f * (1f - (y - ry) / len) : 0f);
        }
    }

    private static void Dent(float cx, float cy, float r) =>
        Multiply(cx - r, cy - r, cx + r, cy + r, (x, y, mat) =>
        {
            var dx = x - cx; var dy = y - cy; var d = MathF.Sqrt(dx * dx + dy * dy);
            return d > r || mat is not (Mat.Metal or Mat.Dark) ? 1f : 1f + 0.45f * ((dx + dy) / (r * 1.41f)) * (1f - d / r);
        });

    private static void LostBolt(float bx, float by) =>
        Paint(bx - 0.6f, by - 0.6f, bx + 0.6f, by + 0.6f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            var d = MathF.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by));
            c = d < 0.28f ? new Vector3(0.02f) : Rust * 0.9f;
            return d < 0.55f;
        });

    // A ragged hole blown through the casing: an outward-curled lip, a black cavity with torn metal in it,
    // soot thrown around. The same breach the wrecked reactor has, at any size.
    private static void Breach(float cx, float cy, float radius, int seed)
    {
        float RadiusAt(float dx, float dy, float d)
        {
            var ca = dx / MathF.Max(d, 0.01f); var sa = dy / MathF.Max(d, 0.01f);
            return radius * (1f + 0.35f * (Noise(ca * 1.6f + 20f, sa * 1.6f + 20f, seed) - 0.5f) * 2f
                                + 0.16f * (Noise(ca * 5.5f + 40f, sa * 5.5f + 40f, seed + 1) - 0.5f) * 2f);
        }
        var reach = radius * 2.6f;
        Stain(cx - reach, cy - reach, cx + reach, cy + reach, Soot, (x, y, mat) =>
        {
            var dx = x - cx; var dy = y - cy; var d = MathF.Sqrt(dx * dx + dy * dy);
            var r = RadiusAt(dx, dy, d);
            return Smooth(r + radius * (0.8f + Fbm(x * 0.6f, y * 0.6f, seed + 2)), r, d) * 0.85f;
        });
        var lipMax = radius * 0.4f;
        Paint(cx - radius * 1.8f, cy - radius * 1.8f, cx + radius * 1.8f, cy + radius * 1.8f, Mat.Dark, (float x, float y, out Vector3 c) =>
        {
            var dx = x - cx; var dy = y - cy; var d = MathF.Sqrt(dx * dx + dy * dy);
            var r = RadiusAt(dx, dy, d);
            var lip = lipMax * (0.3f + 0.7f * Noise(dx / MathF.Max(d, 0.01f) * 3f + 50f, dy / MathF.Max(d, 0.01f) * 3f + 50f, seed + 3));
            c = default;
            if (d > r + lip) return false;
            if (d > r)
            {
                var t = (d - r) / lip; var n = -(1f - t) * 0.75f;
                c = SteelMid * 1.1f * Lit(dx / d * n, dy / d * n, 0.5f) / FlatLit * (0.6f + 0.4f * Fbm(x, y, seed + 4));
                return true;
            }
            if (d > r - 0.2f) { c = new Vector3(0.015f); return true; }
            c = new Vector3(0.045f, 0.04f, 0.037f) * (1f - Smooth(r, r * 0.3f, d) * 0.6f) * (0.7f + 0.6f * Fbm(x * 0.9f, y * 0.9f, seed + 5));
            return true;
        });
        for (var i = 0; i < 6; i++)
        {
            var a = Rand(i, seed + 6) * MathF.PI * 2f; var rr = radius * (0.2f + Rand(i, seed + 7) * 0.6f);
            Shard(cx + MathF.Cos(a) * rr, cy + MathF.Sin(a) * rr, Rand(i, seed + 8) * MathF.PI, 0.6f + Rand(i, seed + 9) * 1.2f,
                0.25f + Rand(i, seed + 10) * 0.35f, Rand(i, seed + 11) < 0.5f ? -0.6f : 0.6f, SteelLight * (0.4f + Rand(i, seed + 12) * 0.4f));
        }
    }
}
