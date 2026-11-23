using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The local player's own health, in the bottom-right corner: the bar (what is left of 100), a little body diagram with each part
// coloured by how hurt it is (red droplet = bleeding, white mark = broken), and the whole-body conditions in words (unconscious,
// heart stopped, pain, starved of oxygen). Always visible, the same way the inventory row above it is, since knowing you are hurt
// shouldn't require opening a menu.
public sealed class PlayerHealthPanel
{
    public const int BarWidth = 180;
    public const int BarHeight = 18;
    private const int FigureWidth = 46;
    private const int FigureHeight = 78;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public PlayerHealthPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, CharacterState? me, Vector2 origin, float totalSeconds = 0f)
    {
        if (me is null)
            return;

        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y, BarWidth, BarHeight), Color.DimGray);
        var ratio = MathHelper.Clamp(me.Health / 100f, 0f, 1f);
        var color = me.Health > 40 ? Color.LightGreen : Color.IndianRed;
        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y, (int)(BarWidth * ratio), BarHeight), color);

        var suitStatus = me.WearingSuit ? " [скафандр]" : "";
        var bleedingStatus = me.IsBleeding ? " [кровотечение]" : "";
        spriteBatch.DrawString(_font, $"{me.Health:0}/100{suitStatus}{bleedingStatus}", origin + new Vector2(4, 1),
            Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        // Only means anything with a suit actually on - shown whether on or off, since the whole point is telling at a glance which
        // way it is currently set before touching the hull.
        if (me.WearingSuit)
        {
            var bootsLabel = me.MagneticBootsOn ? "Магнитные ботинки: ВКЛ" : "Магнитные ботинки: выкл";
            var bootsColor = me.MagneticBootsOn ? Color.LightGreen : Color.Gray;
            spriteBatch.DrawString(_font, bootsLabel, origin + new Vector2(0, -16), bootsColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        if (me.Body is { } body)
        {
            DrawFigure(spriteBatch, body, new Vector2(origin.X - FigureWidth - 8, origin.Y + BarHeight - FigureHeight), totalSeconds);
            DrawConditions(spriteBatch, body, origin + new Vector2(0, BarHeight + 4));
        }
    }

    // green (whole) -> yellow -> red (ruined)
    private static Color SeverityColor(float severity)
    {
        severity = MathHelper.Clamp(severity, 0f, 1f);
        return severity < 0.5f
            ? Color.Lerp(new Color(80, 190, 90), new Color(230, 200, 60), severity * 2f)
            : Color.Lerp(new Color(230, 200, 60), new Color(220, 60, 50), (severity - 0.5f) * 2f);
    }

    private void DrawFigure(SpriteBatch spriteBatch, BodyState body, Vector2 at, float totalSeconds)
    {
        var backdrop = new Rectangle((int)at.X - 3, (int)at.Y - 3, FigureWidth + 6, FigureHeight + 6);
        spriteBatch.Draw(_pixel, backdrop, Color.Black * 0.45f);

        // Part rectangles inside the figure box (x, y, w, h).
        (BodyPart Part, Rectangle Rect)[] layout =
        {
            (BodyPart.Head, new Rectangle(17, 0, 12, 13)),
            (BodyPart.Torso, new Rectangle(14, 15, 18, 26)),
            (BodyPart.LeftArm, new Rectangle(5, 15, 8, 26)),
            (BodyPart.RightArm, new Rectangle(33, 15, 8, 26)),
            (BodyPart.LeftLeg, new Rectangle(14, 43, 8, 34)),
            (BodyPart.RightLeg, new Rectangle(24, 43, 8, 34)),
        };
        var pulse = 0.55f + 0.45f * MathF.Sin(totalSeconds * 6f);
        foreach (var (part, rect) in layout)
        {
            var state = body.Parts.First(p => p.Part == part);
            var severity = (state.Trauma + state.Burn) / 70f;
            var screen = new Rectangle((int)at.X + rect.X, (int)at.Y + rect.Y, rect.Width, rect.Height);
            spriteBatch.Draw(_pixel, screen, SeverityColor(severity));
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, screen, Color.Black * 0.7f, 1);
            if (state.Fractured)
            {
                // A zig-zag across the part: the break.
                spriteBatch.Draw(_pixel, new Rectangle(screen.X + 1, screen.Y + screen.Height / 2 - 1, screen.Width - 2, 2), Color.White);
                spriteBatch.Draw(_pixel, new Rectangle(screen.X + screen.Width / 2 - 1, screen.Y + screen.Height / 2 - 3, 2, 6), Color.White);
            }
            if (state.Bleeding > 0.5f)
                spriteBatch.Draw(_pixel, new Rectangle(screen.Right - 3, screen.Y + 1, 4, 4), Color.Red * pulse);
        }
    }

    private void DrawConditions(SpriteBatch spriteBatch, BodyState body, Vector2 at)
    {
        var lines = new System.Collections.Generic.List<(string Text, Color Color)>();
        if (body.ArrestSecondsLeft > 0f)
            lines.Add(($"СЕРДЦЕ ОСТАНОВИЛОСЬ: {body.ArrestSecondsLeft:0} с", Color.Red));
        else if (body.Unconscious)
            lines.Add(("Без сознания", Color.OrangeRed));
        if (body.Hypoxia > 10f)
            lines.Add(($"Кислородное голодание: {body.Hypoxia:0}%", Color.LightSkyBlue));
        if (body.Pain > 15f)
            lines.Add(($"Боль: {body.Pain:0}%", Color.Orange));
        if (body.Concussion > 15f)
            lines.Add(($"Сотрясение: {body.Concussion:0}%", Color.Khaki));
        if (body.BloodLoss > 15f)
            lines.Add(($"Потеря крови: {body.BloodLoss:0}%", Color.IndianRed));
        foreach (var (text, color) in lines)
        {
            spriteBatch.DrawString(_font, text, at, color, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
            at += new Vector2(0, 14);
        }
    }

    // A person who is down sees the world go dim: unconscious, or with a stopped heart and the seconds left to be saved.
    // "Give up" while the heart is stopped: dying now rather than waiting out the clock, which leads to the spectator option.
    public static Rectangle GiveUpButtonRect(int width, int height) => new((width - 300) / 2, height / 2 + 46, 300, 36);

    public void DrawDownedOverlay(SpriteBatch spriteBatch, CharacterState? me, int width, int height, Point mouse = default)
    {
        if (me?.Body is not { IsDowned: true } body || me.Health <= 0f)
            return;

        spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), Color.Black * (body.ArrestSecondsLeft > 0f ? 0.88f : 0.7f));
        var title = body.ArrestSecondsLeft > 0f ? "СЕРДЦЕ ОСТАНОВИЛОСЬ" : "ВЫ БЕЗ СОЗНАНИЯ";
        var sub = body.ArrestSecondsLeft > 0f
            ? $"Вас ещё можно спасти: {body.ArrestSecondsLeft:0} с"
            : "Ждите помощи - вас могут вылечить товарищи";
        var titleSize = _font.MeasureString(title) * 1.2f;
        var subSize = _font.MeasureString(sub) * 0.7f;
        spriteBatch.DrawString(_font, title, new Vector2((width - titleSize.X) / 2f, height / 2f - 40), Color.IndianRed, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, sub, new Vector2((width - subSize.X) / 2f, height / 2f + 6), Color.LightGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        if (body.ArrestSecondsLeft > 0f)
        {
            var button = GiveUpButtonRect(width, height);
            spriteBatch.Draw(_pixel, button, button.Contains(mouse) ? new Color(120, 40, 40) : new Color(80, 28, 28));
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, button, new Color(190, 90, 90), 1);
            const string label = "Умереть и стать наблюдателем";
            var labelSize = _font.MeasureString(label) * 0.5f;
            spriteBatch.DrawString(_font, label, new Vector2(button.Center.X - labelSize.X / 2f, button.Center.Y - labelSize.Y / 2f), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
    }
}
