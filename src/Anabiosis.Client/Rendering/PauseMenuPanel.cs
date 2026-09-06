using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Esc's own menu (Game1.cs) - shown only once nothing else is open (a block/console, a top-bar
// panel, a turret, the helm - all of those take priority and Esc closes them instead, see
// Game1.Update). Four stacked buttons matching the reference layout; the third one is picked out
// in red as the only one here that actually ends something, rather than just toggling a view.
public sealed class PauseMenuPanel
{
    public const int PanelWidth = 320;
    public const int PanelHeight = 268;
    private const int ButtonWidth = 280;
    private const int ButtonHeight = 44;
    private const int Gap = 14;
    private const int TopPadding = 20;
    private const int BorderThickness = 2;
    private static readonly Color PanelBackground = new(20, 26, 22);
    private static readonly Color PanelBorder = new(90, 110, 95);
    private static readonly Color ButtonFill = new(210, 208, 200);
    private static readonly Color ButtonFillHover = new(232, 230, 222);
    private static readonly Color EndRoundFill = new(150, 45, 45);
    private static readonly Color EndRoundFillHover = new(178, 55, 55);

    private static readonly string[] Labels = { "ПРОДОЛЖИТЬ", "НАСТРОЙКИ (скоро)", "ЗАКОНЧИТЬ РАУНД", "ГЛАВНОЕ МЕНЮ" };

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public PauseMenuPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetButtonRect(int index, Vector2 panelOrigin) => new(
        (int)panelOrigin.X + (PanelWidth - ButtonWidth) / 2,
        (int)panelOrigin.Y + TopPadding + index * (ButtonHeight + Gap),
        ButtonWidth, ButtonHeight);

    public void Draw(SpriteBatch spriteBatch, Vector2 panelOrigin, Point hoverPoint)
    {
        var panelRect = new Rectangle((int)panelOrigin.X, (int)panelOrigin.Y, PanelWidth, PanelHeight);
        PanelFrame.Draw(spriteBatch, _pixel, panelRect, PanelBackground, PanelBorder, 0.97f, BorderThickness);

        for (var i = 0; i < Labels.Length; i++)
        {
            var isEndRound = i == 2;
            DrawButton(spriteBatch, GetButtonRect(i, panelOrigin), Labels[i],
                isEndRound ? EndRoundFill : ButtonFill, isEndRound ? EndRoundFillHover : ButtonFillHover,
                isEndRound ? Color.White : Color.Black, hoverPoint);
        }
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string label, Color fill, Color hoverFill, Color textColor, Point hoverPoint)
    {
        spriteBatch.Draw(_pixel, rect, rect.Contains(hoverPoint) ? hoverFill : fill);
        DrawRectOutline(spriteBatch, rect, new Color(40, 40, 40), 1);
        var size = _font.MeasureString(label) * 0.5f;
        spriteBatch.DrawString(_font, label, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f),
            textColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
