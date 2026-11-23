using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The look of the console's round screen: an instrument, not a window. Everything on it is drawn in one
// phosphor green (Ink) with red kept for what is dangerous, over a dark screen with range rings; scan lines,
// shaded corners and a glare of glass go on top, and a metal rim with a degree scale frames it. The content
// is clipped to the porthole with a scissor rectangle, so nothing on the map can spill past the rim onto the
// ship scene behind the widget.
public sealed partial class GalaxyMapPanel
{
    private static readonly Color Phosphor = new(110, 255, 170);
    private static readonly Color ScreenDark = new(3, 12, 8);
    private static readonly RasterizerState ScissorOn = new() { ScissorTestEnable = true };

    private bool _clipped;
    private Matrix _clipTransform;
    private Rectangle _clipRestore;

    // Colours go through here on their way to the screen. On the helm's map they pass untouched; on the
    // console everything becomes a shade of phosphor green, except warm colours (hostile contacts, the star's
    // burn zone), which stay red so danger still reads at a glance. Works on premultiplied colours.
    private Color Ink(Color color)
    {
        if (!_sonar)
            return color;
        var peak = Math.Max(color.R, Math.Max(color.G, color.B)) / 255f;
        var luminance = (0.30f * color.R + 0.59f * color.G + 0.11f * color.B) / 255f;
        var value = 0.4f * peak + 0.6f * luminance;
        var warm = color.R > color.G * 1.45f && color.R > color.B * 1.45f;
        return warm
            ? new Color(value, value * 0.36f, value * 0.22f, color.A / 255f)
            : new Color(value * 0.43f, value, value * 0.67f, color.A / 255f);
    }

    private void BeginSonarClip(SpriteBatch spriteBatch, Matrix? batchTransform)
    {
        _clipped = false;
        if (batchTransform is not { } transform)
            return;
        var device = spriteBatch.GraphicsDevice;
        var reach = new Vector2(RadarCircleRadius + 1f);
        var topLeft = Vector2.Transform(_radarCenter - reach, transform);
        var bottomRight = Vector2.Transform(_radarCenter + reach, transform);
        var viewport = device.Viewport;
        var left = Math.Clamp((int)MathF.Floor(topLeft.X), 0, viewport.Width);
        var top = Math.Clamp((int)MathF.Floor(topLeft.Y), 0, viewport.Height);
        var right = Math.Clamp((int)MathF.Ceiling(bottomRight.X), 0, viewport.Width);
        var bottom = Math.Clamp((int)MathF.Ceiling(bottomRight.Y), 0, viewport.Height);
        if (right <= left || bottom <= top)
            return;

        spriteBatch.End();
        _clipRestore = device.ScissorRectangle;
        device.ScissorRectangle = new Rectangle(left, top, right - left, bottom - top);
        spriteBatch.Begin(rasterizerState: ScissorOn, transformMatrix: transform);
        _clipTransform = transform;
        _clipped = true;
    }

    private void EndSonarClip(SpriteBatch spriteBatch)
    {
        if (!_clipped)
            return;
        spriteBatch.End();
        spriteBatch.GraphicsDevice.ScissorRectangle = _clipRestore;
        spriteBatch.Begin(transformMatrix: _clipTransform);
        _clipped = false;
    }

    // The empty screen: dark, a faint square grid, and rings at round distances from the ship with the
    // distance written on each.
    private void DrawSonarBase(SpriteBatch spriteBatch, float zoom)
    {
        var center = _radarCenter;
        var radius = RadarCircleRadius;
        spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - radius) - 2, (int)(center.Y - radius) - 2, (int)(radius * 2f) + 4, (int)(radius * 2f) + 4), ScreenDark);
        DrawGlow(spriteBatch, center, radius * 1.15f, new Color(60, 255, 150), 0.07f);

        var line = Phosphor * 0.05f;
        for (var offset = -radius + (radius % 42f); offset < radius; offset += 42f)
        {
            spriteBatch.Draw(_pixel, new Rectangle((int)(center.X + offset), (int)(center.Y - radius), 1, (int)(radius * 2f)), line);
            spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - radius), (int)(center.Y + offset), (int)(radius * 2f), 1), line);
        }
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X, (int)(center.Y - radius), 1, (int)(radius * 2f)), Phosphor * 0.1f);
        spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - radius), (int)center.Y, (int)(radius * 2f), 1), Phosphor * 0.1f);

        var step = RoundDistanceFor(48f, zoom);
        var stepPixels = step * PixelsPerUnit * zoom;
        var labelDirection = new Vector2(MathF.Cos(-MathF.PI / 4f), MathF.Sin(-MathF.PI / 4f));
        for (var ring = 1; ring * stepPixels < radius - 6f; ring++)
        {
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, ring * stepPixels, 0f, 360f, Phosphor * 0.16f, 72, 1f);
            var text = (ring * step).ToString("0", CultureInfo.InvariantCulture);
            spriteBatch.DrawString(_font, text, center + labelDirection * (ring * stepPixels) + new Vector2(3f, -2f), Phosphor * 0.5f, 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
        }
    }

    // The smallest round distance, in map units, that is at least this many pixels long at the current zoom.
    private static float RoundDistanceFor(float minPixels, float zoom)
    {
        foreach (var units in new[] { 10f, 20f, 50f, 100f, 200f, 500f, 1000f, 2000f, 5000f, 10000f, 20000f })
            if (units * PixelsPerUnit * zoom >= minPixels)
                return units;
        return 50000f;
    }

    // The glass over the picture: scan lines, a band of brightness rolling slowly down, dark corners, a glare.
    private void DrawSonarGlass(SpriteBatch spriteBatch, float totalSeconds)
    {
        var center = _radarCenter;
        var radius = RadarCircleRadius;
        for (var y = -radius; y < radius; y += 3f)
        {
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - y * y));
            spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - half), (int)(center.Y + y), (int)(half * 2f), 1), Color.Black * 0.17f);
        }

        var roll = totalSeconds * 34f % (radius * 2f + 80f) - radius - 40f;
        spriteBatch.Draw(_glow, center + new Vector2(0f, roll), null, MapArt.Additive(Phosphor, 0.05f), 0f, new Vector2(64f, 64f),
            new Vector2(radius * 2.4f / 128f, 46f / 128f), SpriteEffects.None, 0f);

        spriteBatch.Draw(_vignette, center, null, Color.White, 0f, new Vector2(128f, 128f), (radius * 2f + 6f) / 256f, SpriteEffects.None, 0f);
        spriteBatch.Draw(_glow, center + new Vector2(-radius * 0.40f, -radius * 0.50f), null, MapArt.Additive(new Color(205, 255, 228), 0.11f), -0.62f,
            new Vector2(64f, 64f), new Vector2(radius * 1.25f / 128f, radius * 0.5f / 128f), SpriteEffects.None, 0f);
    }

    // The metal rim: a few rings from dark to light, bolts, and a degree scale just inside the glass.
    private void DrawBezelRim(SpriteBatch spriteBatch, Vector2 center)
    {
        var radius = RadarCircleRadius;
        void Ring(float at, Color color, float thickness) =>
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, at, 0f, 360f, color, 120, thickness);
        Ring(radius + 9.5f, new Color(20, 24, 22), 4f);
        Ring(radius + 6.5f, new Color(66, 76, 70), 3.2f);
        Ring(radius + 4f, new Color(124, 138, 128), 2f);
        Ring(radius + 2f, new Color(52, 60, 54), 2.6f);
        Ring(radius - 0.5f, Color.Black * 0.7f, 2.4f);

        for (var i = 0; i < 8; i++)
        {
            var angle = (i + 0.5f) * MathF.PI / 4f;
            var bolt = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (radius + 6.5f);
            HudIcons.DrawScaledCircle(spriteBatch, _softCircle, bolt, 2.6f, new Color(24, 28, 26));
            HudIcons.DrawScaledCircle(spriteBatch, _softCircle, bolt + new Vector2(-0.5f, -0.5f), 1.3f, new Color(120, 132, 124));
        }

        for (var degrees = 0; degrees < 360; degrees += 10)
        {
            var major = degrees % 30 == 0;
            var angle = degrees * (MathF.PI / 180f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            HudIcons.DrawLine(spriteBatch, _pixel, center + direction * (radius - (major ? 12f : 6f)), center + direction * (radius - 1.5f),
                major ? new Color(150, 200, 165) * 0.85f : new Color(110, 140, 120) * 0.55f, major ? 1.8f : 1f);
            if (!major)
                continue;
            var text = degrees.ToString(CultureInfo.InvariantCulture);
            var size = _font.MeasureString(text) * 0.34f;
            spriteBatch.DrawString(_font, text, center + direction * (radius - 21f) - size / 2f, new Color(140, 190, 155) * 0.7f, 0f, Vector2.Zero, 0.34f, SpriteEffects.None, 0f);
        }
    }

    // The words beside the screen: which system this is, and how the console is worked.
    private void DrawConsoleText(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var system = snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId).Name;
        spriteBatch.DrawString(_font, "СКАНЕР СИСТЕМЫ", origin, new Color(120, 170, 140), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, system, origin + new Vector2(0f, 11f), new Color(200, 240, 215), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y + 32, 300, 1), new Color(110, 160, 130) * 0.5f);
        const string help = "ручка на ободе — направление луча\nклик по своей метке — поставить на карту\nза кольцом — прыжок (M), колесо — масштаб\nE / Esc — войти / выйти";
        spriteBatch.DrawString(_font, help, origin + new Vector2(0f, 40f), new Color(150, 170, 158), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
