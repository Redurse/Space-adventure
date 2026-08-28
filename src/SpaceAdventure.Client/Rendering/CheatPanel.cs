using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Dev-only cheat panel (Ё/OemTilde toggles it, Game1.cs) - a fast way to get a live raider in the
// field for testing hit resolution (World.EnemyAi.cs's positional damage) without waiting through
// a normal encounter's approach. Deliberately bare-bones, same PanelFrame material as every other
// overlay but with a single button - this is a testing tool, not a piece of game content.
public sealed class CheatPanel
{
    public const int PanelWidth = 300;
    public const int PanelHeight = 152;
    private const int ButtonWidth = 260;
    private const int ButtonHeight = 44;
    private const int TopPadding = 38;
    private const int ButtonGap = 12;
    private const int BorderThickness = 2;
    private static readonly Color PanelBackground = new(40, 20, 20);
    private static readonly Color PanelBorder = new(150, 70, 70);
    private static readonly Color ButtonFill = new(210, 208, 200);
    private static readonly Color ButtonFillHover = new(232, 230, 222);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CheatPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetSpawnEnemyButtonRect(Vector2 panelOrigin) => new(
        (int)panelOrigin.X + (PanelWidth - ButtonWidth) / 2,
        (int)panelOrigin.Y + TopPadding,
        ButtonWidth, ButtonHeight);

    // Second button, same "testing tool, not a game action" shape as the enemy spawner above -
    // 100 credits at a time so content-каталог отсеков's own expensive catalog entries (reactor
    // 900cr, cockpit 700cr, ...) can be tested live without grinding real trades up to that first.
    public static Rectangle GetAddCreditsButtonRect(Vector2 panelOrigin) => new(
        (int)panelOrigin.X + (PanelWidth - ButtonWidth) / 2,
        (int)panelOrigin.Y + TopPadding + ButtonHeight + ButtonGap,
        ButtonWidth, ButtonHeight);

    public void Draw(SpriteBatch spriteBatch, Vector2 panelOrigin, Point hoverPoint)
    {
        var panelRect = new Rectangle((int)panelOrigin.X, (int)panelOrigin.Y, PanelWidth, PanelHeight);
        PanelFrame.Draw(spriteBatch, _pixel, panelRect, PanelBackground, PanelBorder, 0.97f, BorderThickness);

        spriteBatch.DrawString(_font, "ЧИТ-ПАНЕЛЬ (Ё)", new Vector2(panelOrigin.X + 14, panelOrigin.Y + 10),
            Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        DrawButton(spriteBatch, GetSpawnEnemyButtonRect(panelOrigin), hoverPoint, "Заспавнить врага рядом");
        DrawButton(spriteBatch, GetAddCreditsButtonRect(panelOrigin), hoverPoint, "Выдать 100 кредитов");
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle buttonRect, Point hoverPoint, string label)
    {
        spriteBatch.Draw(_pixel, buttonRect, buttonRect.Contains(hoverPoint) ? ButtonFillHover : ButtonFill);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, buttonRect, new Color(40, 40, 40), 1);
        var size = _font.MeasureString(label) * 0.45f;
        spriteBatch.DrawString(_font, label, new Vector2(buttonRect.Center.X - size.X / 2f, buttonRect.Center.Y - size.Y / 2f),
            Color.Black, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }
}
