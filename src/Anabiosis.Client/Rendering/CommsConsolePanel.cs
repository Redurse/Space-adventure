using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("консоль связи... карта солнечной системы которую можно отдалять и
// приближать и есть кнопка - установить связь. при нажатии на кнопку пока ничего не происходит,
// добавим потом") - a full-screen takeover (Game1.cs's own _commsConsoleOpen), same "empty
// scene-batch branch, drawn later as an unmasked HUD-batch overlay" treatment as the galactic map.
// The map itself is GalaxyMapPanel's own pilotView (already read-only and system-scoped - see its
// own doc comment, "reused wholesale as the helm's own window 1") drawn straight through by the
// caller; this class only owns the one stub button on top.
public sealed class CommsConsolePanel
{
    private const int ButtonWidth = 240;
    private const int ButtonHeight = 40;
    private static readonly Color ButtonFill = new(210, 208, 200);
    private static readonly Color ButtonFillHover = new(232, 230, 222);
    private const string ButtonLabel = "УСТАНОВИТЬ СВЯЗЬ";

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CommsConsolePanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetButtonRect(Rectangle area) => new(
        area.Center.X - ButtonWidth / 2, area.Bottom - ButtonHeight - 24, ButtonWidth, ButtonHeight);

    // Only the title/button chrome - the map itself is drawn by the caller (Game1.cs already has
    // the GalaxyMapPanel instance and its own zoom/pan fields to pass through).
    public void DrawChrome(SpriteBatch spriteBatch, Rectangle area, Point hoverPoint)
    {
        var title = "КОНСОЛЬ СВЯЗИ";
        var titleSize = _font.MeasureString(title) * 0.6f;
        spriteBatch.DrawString(_font, title, new Vector2(area.Center.X - titleSize.X / 2f, area.Y + 12),
            new Color(220, 170, 90), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        var buttonRect = GetButtonRect(area);
        spriteBatch.Draw(_pixel, buttonRect, buttonRect.Contains(hoverPoint) ? ButtonFillHover : ButtonFill);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, buttonRect, new Color(40, 40, 40), 1);
        var labelSize = _font.MeasureString(ButtonLabel) * 0.42f;
        spriteBatch.DrawString(_font, ButtonLabel,
            new Vector2(buttonRect.Center.X - labelSize.X / 2f, buttonRect.Center.Y - labelSize.Y / 2f),
            Color.Black, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }
}
