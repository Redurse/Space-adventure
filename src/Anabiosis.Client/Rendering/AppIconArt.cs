using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

/// <summary>The application icon, drawn at whatever size it is asked for.</summary>
///
/// The icon it replaces was a scanner reticle with a signal blip in it, which belonged to a game
/// called Unidentified Signal. This one speaks the same language as the wordmark: a plate of dark
/// steel with a warm rim, and the pupa on it.
///
/// Drawn per size rather than drawn once and scaled down. An icon is looked at hardest at 16 pixels,
/// where a downscaled 256 turns to mush - so detail is switched off as the canvas shrinks: the
/// segment rings go first, then the corrosion, then the bevel narrows to a single pixel. What has to
/// survive all the way down is the silhouette and the rim, and those are the only two things drawn
/// at every size.
///
/// Baking it is a two-step: this produces one PNG per size, and those are packed into a .ico. See
/// the "the app icon bakes" check in Anabiosis.ShaderCheck for the command.
public static class AppIconArt
{
    public static readonly int[] Sizes = { 16, 24, 32, 48, 64, 128, 256 };

    private static readonly Color PlateLight = new(96, 112, 114);
    private static readonly Color PlateMid = new(40, 52, 56);
    private static readonly Color PlateDark = new(17, 23, 27);
    private static readonly Color PlateShadow = new(7, 10, 13);
    private static readonly Color Corrosion = new(52, 62, 50);
    private static readonly Color Rim = new(214, 168, 82);

    // The pod is the LIT thing, not the silhouette. Drawn dark on a dark plate it vanished at 24
    // pixels and was a splinter at 16; as steel with a rim it is the same object the wordmark is
    // made of, and it separates from the ground at every size.
    private static readonly Color ShellBright = new(214, 228, 226);
    private static readonly Color ShellMid = new(122, 140, 142);
    private static readonly Color ShellDark = new(44, 58, 62);
    private static readonly Color ShellDeep = new(16, 22, 26);

    private static readonly Vector2 Light = new(-0.55f, -0.835f);

    public static Texture2D Bake(GraphicsDevice graphics, int size)
    {
        var c = new PixelCanvas(size, size);
        var s = (float)size;

        // The plate fills almost the whole square. Icons are shown small and cropped by everything
        // that draws them, so margin is wasted pixels.
        var inset = s * 0.045f;
        var radius = s * 0.20f;
        var half = s * 0.5f - inset;
        var bevel = MathF.Max(1f, s * 0.055f);

        float Sdf(float x, float y)
        {
            var qx = MathF.Abs(x - s * 0.5f) - (half - radius);
            var qy = MathF.Abs(y - s * 0.5f) - (half - radius);
            var ax = MathF.Max(qx, 0f);
            var ay = MathF.Max(qy, 0f);
            return MathF.Sqrt(ax * ax + ay * ay) + MathF.Min(MathF.Max(qx, qy), 0f) - radius;
        }

        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var px = x + 0.5f;
            var py = y + 0.5f;
            var d = Sdf(px, py);
            if (d > 1.2f)
                continue;

            // The rim, on the boundary itself. It is the one feature that reads at 16 pixels, so it
            // gets the outermost band rather than sharing it with the bevel.
            if (d > -1.0f)
            {
                c.Px(x, y, Rim, d > 0.2f ? 0.55f : 0.95f);
                continue;
            }

            var depth = -d;
            var down = y / s;
            var col = Mix(PlateMid, PlateDark, Math.Clamp(down * 1.1f - 0.05f, 0f, 1f));

            // Corrosion, and only where there is room for it to look like weathering rather than
            // like a stuck pixel.
            if (size >= 32)
            {
                var rot = Blob(x, y, size, 13) * (0.45f + down * 0.8f);
                if (rot > 0.56f)
                    col = Mix(col, Corrosion, MathF.Min(0.30f, (rot - 0.56f) * 0.9f));
            }

            // Bevel, from the gradient of the same distance function that drew the shape - so the
            // corners round off correctly without a special case for them.
            if (depth < bevel)
            {
                var nx = Sdf(px + 1f, py) - Sdf(px - 1f, py);
                var ny = Sdf(px, py + 1f) - Sdf(px, py - 1f);
                var len = MathF.Sqrt(nx * nx + ny * ny);
                if (len > 0.001f)
                {
                    var lambert = nx / len * Light.X + ny / len * Light.Y;
                    var rise = 1f - depth / bevel;
                    col = lambert > 0f
                        ? Mix(col, PlateLight, rise * lambert * 0.9f)
                        : Mix(col, PlateShadow, rise * -lambert * 0.85f);
                }
            }

            c.Px(x, y, col, 1f);
        }

        Pupa(c, s);
        return c.ToTexture(graphics);
    }

    /// <summary>The same shape the wordmark hangs on the ANA|BIOSIS seam, standing upright here.</summary>
    private static void Pupa(PixelCanvas c, float s)
    {
        var cx = s * 0.5f;
        var top = s * 0.185f;
        var span = s * 0.625f;
        // Wide enough that 16 pixels still gets a body and not a splinter: at the previous width
        // the smallest icon was four pixels across, which is a scratch.
        var maxHalf = s * 0.152f;

        float Half(float t)
        {
            var w = maxHalf * MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(Math.Clamp(t, 0f, 1f), 0.55f)), 0.62f);
            return t > 0.86f ? w * (1f - (t - 0.86f) / 0.14f * 0.75f) : w;
        }

        // Cast onto the plate before the body, so the pod sits on the metal instead of being inlaid
        // into it. Offset, never a halo: a ring of soft darkness fills the shape in and turns the
        // whole thing into a blob, which is exactly what happened the first time in the wordmark.
        var drop = MathF.Max(1f, s * 0.012f);
        for (var i = 0; i <= (int)span; i++)
        {
            var t = i / span;
            var w = Half(t);
            if (w < 0.4f)
                continue;
            c.Rect(cx - w + drop, top + i + drop * 1.3f, w * 2f, 1f, PlateShadow, 0.5f);
        }

        for (var i = 0; i <= (int)span; i++)
        {
            var t = i / span;
            var w = Half(t);
            if (w < 0.4f)
                continue;
            var y = top + i;
            for (var o = -w; o <= w; o += 0.5f)
            {
                var n = o / w;                                   // -1 lit flank, +1 shadow flank
                var col =
                    n < -0.90f ? Rim
                    : n < -0.30f ? Mix(ShellMid, ShellBright, (-n - 0.30f) / 0.60f)
                    : n < 0.40f ? ShellMid
                    : Mix(ShellDark, ShellDeep, (n - 0.40f) / 0.60f);
                c.Px(cx + o, y, col, 1f);
            }
        }

        // Segment rings, and only once there are enough pixels across the body for a ring to be a
        // ring. Below that they are just three darker dots in a row.
        if (s < 48f)
            return;
        for (var k = 0; k < 7; k++)
        {
            var t = 0.40f + k * 0.075f;
            var w = Half(t);
            if (w < 1.5f)
                continue;
            var y = top + t * span;
            for (var o = -w * 0.9f; o <= w * 0.9f; o += 0.5f)
            {
                var n = o / w;
                var bow = (1f - n * n) * (s * 0.016f);
                c.Px(cx + o, y + bow, ShellDeep, 0.55f);
                c.Px(cx + o, y + bow - 1f, ShellBright, 0.30f);
            }
        }
    }

    private static float Blob(int x, int y, int size, int seed)
    {
        var cell = MathF.Max(3f, size / 9f);
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
