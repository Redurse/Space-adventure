using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// What frames the helm's map without being part of it: the name of the system, a line on how the map is
// worked, a scale bar, and a small legend. Placed clear of the helm's other furniture - the tab switcher sits
// bottom-left, the button widget bottom-right, the diagnostics overlay top-left.
public sealed partial class GalaxyMapPanel
{
    private void DrawHelmChrome(SpriteBatch spriteBatch, string systemName, float zoom, bool captainHelmView)
    {
        var right = _bounds.Right - 24f;
        var y = _bounds.Y + 16f;
        // A dim plate under the block, so a station that happens to sit behind it does not fight the text.
        spriteBatch.Draw(_pixel, new Rectangle((int)right - 342, (int)y - 6, 352, 84), PlateFill * 0.55f);

        void RightAligned(string text, float scale, Color color)
        {
            var size = _font.MeasureString(text) * scale;
            spriteBatch.DrawString(_font, text, new Vector2(right - size.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            y += size.Y;
        }

        RightAligned("КАРТА СИСТЕМЫ", 0.42f, DetailInk);
        var title = systemName.ToUpperInvariant();
        var titleWidth = _font.MeasureString(title).X * 0.78f;
        RightAligned(title, 0.78f, TitleInk);
        spriteBatch.Draw(_pixel, new Rectangle((int)(right - titleWidth), (int)y + 1, (int)titleWidth, 1), new Color(120, 175, 225) * 0.6f);
        y += 6f;
        RightAligned(captainHelmView
            ? "ЛКМ — курс  ·  ПКМ (держать) — навести нос  ·  колесо — масштаб"
            : "ПКМ (тащить) — сдвиг карты  ·  колесо — масштаб", 0.42f, DetailInk);
        y += 8f;

        // Scale bar: a round distance that comes out between about 60 and 150 pixels long.
        var units = RoundDistanceFor(60f, zoom);
        var length = units * PixelsPerUnit * zoom;
        var barColor = new Color(190, 205, 222);
        var label = FormatDistance(units);
        var labelSize = _font.MeasureString(label) * 0.42f;
        spriteBatch.DrawString(_font, label, new Vector2(right - length / 2f - labelSize.X / 2f, y), barColor, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        y += labelSize.Y + 2f;
        spriteBatch.Draw(_pixel, new Rectangle((int)(right - length), (int)y + 3, (int)length, 1), barColor);
        spriteBatch.Draw(_pixel, new Rectangle((int)(right - length), (int)y, 1, 7), barColor);
        spriteBatch.Draw(_pixel, new Rectangle((int)right - 1, (int)y, 1, 7), barColor);

        DrawLegend(spriteBatch, new Vector2(_bounds.X + 16f, _bounds.Y + 170f));
    }

    private void DrawLegend(SpriteBatch spriteBatch, Vector2 origin)
    {
        const float row = 13f;
        var rows = new (string Text, Action<Vector2> Sample)[]
        {
            ("станция", at => HudIcons.DrawRingArc(spriteBatch, _pixel, at, 4f, 0f, 360f, Color.CornflowerBlue, 14, 1.6f)),
            ("находка", at => HudIcons.DrawRingArc(spriteBatch, _pixel, at, 4f, 0f, 360f, Color.Goldenrod, 14, 1.6f)),
            ("неопознанный сигнал", at => HudIcons.DrawRingArc(spriteBatch, _pixel, at, 4f, 0f, 360f, SignalColor, 14, 1.6f)),
            ("пройденный путь", at => HudIcons.DrawLine(spriteBatch, _pixel, at - new Vector2(6f, 0f), at + new Vector2(6f, 0f), TrailColor * 0.8f, 1.6f)),
            ("радиус сенсоров", at =>
            {
                HudIcons.DrawLine(spriteBatch, _pixel, at - new Vector2(6f, 0f), at - new Vector2(1.5f, 0f), SensorRingColor * 0.8f, 1.5f);
                HudIcons.DrawLine(spriteBatch, _pixel, at + new Vector2(1.5f, 0f), at + new Vector2(6f, 0f), SensorRingColor * 0.8f, 1.5f);
            }),
            ("граница прыжка", at => HudIcons.DrawLine(spriteBatch, _pixel, at - new Vector2(6f, 0f), at + new Vector2(6f, 0f), Color.MediumPurple, 2f)),
            ("опасная зона звезды", at => HudIcons.DrawLine(spriteBatch, _pixel, at - new Vector2(6f, 0f), at + new Vector2(6f, 0f), new Color(255, 80, 50), 2f)),
        };

        var width = 0f;
        foreach (var (text, _) in rows)
            width = MathF.Max(width, _font.MeasureString(text).X * 0.42f);
        var plate = new Rectangle((int)origin.X, (int)origin.Y, (int)(width + 32f), (int)(rows.Length * row + 8f));
        spriteBatch.Draw(_pixel, plate, PlateFill * 0.6f);
        DrawRectOutline(spriteBatch, plate, new Color(120, 150, 180) * 0.25f, 1);

        for (var i = 0; i < rows.Length; i++)
        {
            var y = origin.Y + 5f + i * row;
            rows[i].Sample(new Vector2(origin.X + 12f, y + 5f));
            spriteBatch.DrawString(_font, rows[i].Text, new Vector2(origin.X + 24f, y), DetailInk, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        }
    }
}
