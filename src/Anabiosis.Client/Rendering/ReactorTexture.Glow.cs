using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The emissive mask: where light comes out of the machine while it runs - the core window (fuel
// rods standing dark against it), the coolant columns, the indicator lamps, a faint bleed between
// the radiator fins and through the intake vanes - plus a soft halo spilling past the housing.
// White, premultiplied; ShipRenderer draws it with a zero-alpha tint, which under the default
// premultiplied AlphaBlend adds the tint colour on top of whatever is below (teal / red).
// Covers GlowMargin units beyond the block on every side, so draw it into the inflated rect.
public static partial class MachineArt
{
    private static Texture2D BakeGlow(GraphicsDevice device, bool wrecked)
    {
        const float span = Units + 2f * GlowMargin;
        var data = new Color[GlowSize * GlowSize];
        for (var py = 0; py < GlowSize; py++)
        for (var px = 0; px < GlowSize; px++)
        {
            var x = (px + 0.5f) / GlowSize * span - GlowMargin;
            var y = (py + 0.5f) / GlowSize * span - GlowMargin;
            var v = MathHelper.Clamp(GlowAt(x, y, wrecked), 0f, 1f);
            data[py * GlowSize + px] = new Color(v, v, v, v);
        }
        var texture = new Texture2D(device, GlowSize, GlowSize);
        texture.SetData(data);
        return texture;
    }

    private static float GlowAt(float x, float y, bool wrecked)
    {
        var inside = 0f;

        // Wrecked: the breach itself smoulders (brighter deep inside), the shattered column is dark.
        if (wrecked)
        {
            var hdx = x - HoleX; var hdy = y - HoleY; var hd = MathF.Sqrt(hdx * hdx + hdy * hdy);
            var hr = HoleRadius(MathF.Atan2(hdy, hdx));
            if (hd < hr - 0.4f)
                inside = 0.35f + 0.5f * Smooth(hr, hr * 0.2f, hd) * (0.6f + 0.4f * Fbm(x * 0.9f, y * 0.9f, 86));
        }
        else

        // Core window: brightest in the middle, rods in silhouette.
        if (InRoundRect(x, y, WinX0 + 0.9f, WinY0 + 0.9f, WinX1 - 0.9f, WinY1 - 0.9f, WinR - 0.9f))
        {
            var nx = (x - (WinX0 + WinX1) / 2f) / ((WinX1 - WinX0) / 2f);
            var ny = (y - (WinY0 + WinY1) / 2f) / ((WinY1 - WinY0) / 2f);
            inside = 0.95f - 0.4f * MathF.Min(1f, MathF.Sqrt(nx * nx + ny * ny));
            foreach (var rx in RodX)
                if (MathF.Abs(x - rx) < 0.8f && y > WinY0 + 2.1f && y < WinY1 - 2.1f)
                    inside *= 0.45f;
        }

        // Coolant columns.
        foreach (var cx in TubeX)
        {
            if (wrecked && cx == TubeX[0]) continue;
            var n = (x - cx) / TubeR;
            if (MathF.Abs(n) < 0.8f && y > TubeY0 + 1.2f && y < TubeY1 - 1.2f)
                inside = MathF.Max(inside, 0.62f + 0.2f * (1f - MathF.Abs(n + 0.3f)) + 0.06f * MathF.Sin(y * 1.7f));
        }

        // Indicator lamps.
        foreach (var lx in LampX)
            if ((x - lx) * (x - lx) + (y - LampY) * (y - LampY) < 0.45f)
                inside = 1f;

        // Faint bleed between radiator fins and through the intake vanes.
        foreach (var (x0, x1) in Radiators)
        {
            if (x < x0 + 1.2f || x >= x1 - 1.2f || y < RadY0 + 1.2f || y >= RadY1 - 1.2f) continue;
            var local = (x - x0 - 1.2f) / FinPitch;
            if (local - MathF.Floor(local) > 0.72f)
                inside = MathF.Max(inside, 0.16f * Smooth(RadY0, (RadY0 + RadY1) / 2f, y) * Smooth(RadY1, (RadY0 + RadY1) / 2f, y) * 2f);
        }
        {
            var dx = x - FanX; var dy = y - FanY; var d = MathF.Sqrt(dx * dx + dy * dy);
            if (d > 1.1f && d < FanR - 1f)
            {
                var s = (MathF.Atan2(dy, dx) + d * 0.35f) / (MathF.PI * 2f / 9f);
                if (s - MathF.Floor(s) >= 0.55f) inside = MathF.Max(inside, 0.38f);
            }
        }

        // Halos.
        var wx = MathF.Max(0f, MathF.Max(WinX0 - x, x - WinX1));
        var wy = MathF.Max(0f, MathF.Max(WinY0 - y, y - WinY1));
        var dw = MathF.Sqrt(wx * wx + wy * wy);
        var halo = (wrecked ? 0.3f : 0.5f) * MathF.Exp(-(dw / 3.2f) * (dw / 3.2f));
        foreach (var cx in TubeX)
        {
            if (wrecked && cx == TubeX[0]) continue;
            var tdx = MathF.Max(0f, MathF.Abs(x - cx) - TubeR);
            var tdy = MathF.Max(0f, MathF.Max(TubeY0 - y, y - TubeY1));
            var dt = MathF.Sqrt(tdx * tdx + tdy * tdy);
            halo += 0.3f * MathF.Exp(-(dt / 2f) * (dt / 2f));
        }
        var bx = x - 32f; var by = y - 26f;
        halo += 0.2f * MathF.Exp(-(bx * bx + by * by) / (24f * 24f));

        return MathF.Max(inside, MathF.Min(0.85f, halo));
    }
}
