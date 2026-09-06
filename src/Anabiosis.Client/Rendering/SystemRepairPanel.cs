using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The Barotrauma-style card a damaged system device shows while the local player is standing next
// to it - title bar with a little gear + dots (pure chrome, matching the reference), the device's
// own name, what's needed to fix it, and a status bar/button. This project has no per-character
// skill levels the way Barotrauma does (repairing here is a plain "have the right tool in hand"
// gate, World.Interact.cs's RepairDeviceWiring), so the requirement line reads the tool instead of
// a skill number, coloured the same red/ready convention the reference uses for "not met" vs "met".
public sealed class SystemRepairPanel
{
    public const int PanelWidth = 250;
    private const int HeaderHeight = 30;
    public const int PanelHeight = 168;
    private const int BorderThickness = 2;
    private static readonly Color PanelBackground = new(24, 28, 24);
    private static readonly Color PanelBorder = new(90, 100, 85);
    private static readonly Color HeaderColor = new(46, 52, 44);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public SystemRepairPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, string deviceName, bool holdingTool, float progressPercent, Vector2 origin)
    {
        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, PanelWidth, PanelHeight);
        spriteBatch.Draw(_pixel, panelRect, PanelBackground * 0.97f);
        DrawRectOutline(spriteBatch, panelRect, PanelBorder, BorderThickness);

        var headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, HeaderHeight);
        spriteBatch.Draw(_pixel, headerRect, HeaderColor);
        HudIcons.DrawRoleGlyph(spriteBatch, _pixel, new Vector2(headerRect.X + 16, headerRect.Center.Y), 0.5f, Color.LightGray, CrewRole.Mechanic);
        for (var i = -1; i <= 1; i++)
            HudIcons.FillCircle(spriteBatch, _pixel, new Vector2(headerRect.Center.X + i * 7, headerRect.Center.Y), 1.6f, Color.LightGray * 0.8f);

        var titleSize = _font.MeasureString(deviceName) * 0.65f;
        spriteBatch.DrawString(_font, deviceName,
            new Vector2(panelRect.Center.X - titleSize.X / 2f, headerRect.Bottom + 12),
            new Color(230, 200, 140), 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        spriteBatch.DrawString(_font, "ТРЕБУЕТСЯ:", new Vector2(panelRect.X + 12, headerRect.Bottom + 44),
            Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        var requirementColor = holdingTool ? Color.LightGreen : Color.OrangeRed;
        spriteBatch.DrawString(_font, "- Гаечный ключ или отвёртка", new Vector2(panelRect.X + 12, headerRect.Bottom + 62),
            requirementColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        // Bar in the right corner, button in the left - a mirror of Barotrauma's own left-bar
        // layout, by request.
        const int barWidth = 130;
        var barRect = new Rectangle(panelRect.Right - 12 - barWidth, headerRect.Bottom + 92, barWidth, 22);
        spriteBatch.Draw(_pixel, barRect, Color.Black * 0.8f);
        // The real fill, from World.SystemRepair.cs's own Percent - a slow passive trickle plus
        // whatever bonus chunks a well-timed extra press has landed.
        var fraction = MathHelper.Clamp(progressPercent / 100f, 0f, 1f);
        var fillWidth = (int)(barRect.Width * fraction);
        if (fillWidth > 0)
        {
            HudIcons.FillCircle(spriteBatch, _pixel, new Vector2(barRect.X + 8, barRect.Center.Y), 7f, new Color(214, 130, 40));
            spriteBatch.Draw(_pixel, new Rectangle(barRect.X + 8, barRect.Y + 3, System.Math.Max(0, fillWidth - 8), barRect.Height - 6), new Color(214, 130, 40));
        }
        DrawRectOutline(spriteBatch, barRect, Color.LightGray * 0.6f, 1);

        var buttonRect = new Rectangle(panelRect.X + 12, barRect.Y - 4, barRect.X - 10 - (panelRect.X + 12), 30);
        spriteBatch.Draw(_pixel, buttonRect, holdingTool ? new Color(70, 110, 60) : new Color(60, 60, 60));
        DrawRectOutline(spriteBatch, buttonRect, Color.Black * 0.5f, 1);
        var buttonLabel = holdingTool ? $"РЕМОНТ\n{progressPercent:0}%" : "РЕМОНТ\nТРЕБУЕТСЯ...";
        var lines = buttonLabel.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineSize = _font.MeasureString(lines[i]) * 0.38f;
            spriteBatch.DrawString(_font, lines[i],
                new Vector2(buttonRect.Center.X - lineSize.X / 2f, buttonRect.Y + 4 + i * 12),
                holdingTool ? Color.White : Color.LightGray, 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);
        }
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
