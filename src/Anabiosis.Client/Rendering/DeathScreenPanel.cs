using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Barotrauma-style death screen (direct user request, "экран смерти... почти точь в точь как в
// баротравме") - shown once this player's own character's Health reaches 0 (Game1.cs derives that
// straight from the already-networked CharacterState.Health, no protocol change needed). Same
// fixed-rect, Draw+GetButtonRect-share-coordinates pattern as PauseMenuPanel, drawn last over
// everything - the world keeps simulating behind it exactly like the pause menu already does, this
// is a client-only overlay intercepting the click, not a real pause. One button: "НАБЛЮДАТЬ" hands
// off to Game1's own free-roam spectator camera (Game1.Camera.cs) - no respawn exists in this game
// yet, so there is nothing else for this screen to offer.
public sealed class DeathScreenPanel
{
    public const int PanelWidth = 360;
    public const int PanelHeight = 170;
    private const int ButtonWidth = 280;
    private const int ButtonHeight = 44;
    private const int BorderThickness = 2;
    private static readonly Color PanelBackground = new(24, 12, 12);
    private static readonly Color PanelBorder = new(130, 45, 45);
    private static readonly Color TitleColor = new(225, 100, 100);
    private static readonly Color ButtonFill = new(210, 208, 200);
    private static readonly Color ButtonFillHover = new(232, 230, 222);
    private const string Title = "ВЫ ПОГИБЛИ";
    private const string ButtonLabel = "НАБЛЮДАТЬ";

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public DeathScreenPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetButtonRect(Vector2 panelOrigin) => new(
        (int)panelOrigin.X + (PanelWidth - ButtonWidth) / 2,
        (int)panelOrigin.Y + PanelHeight - ButtonHeight - 28,
        ButtonWidth, ButtonHeight);

    public void Draw(SpriteBatch spriteBatch, Vector2 panelOrigin, Point hoverPoint)
    {
        var panelRect = new Rectangle((int)panelOrigin.X, (int)panelOrigin.Y, PanelWidth, PanelHeight);
        PanelFrame.Draw(spriteBatch, _pixel, panelRect, PanelBackground, PanelBorder, 0.97f, BorderThickness);

        var titleSize = _font.MeasureString(Title) * 0.7f;
        spriteBatch.DrawString(_font, Title,
            new Vector2(panelOrigin.X + (PanelWidth - titleSize.X) / 2f, panelOrigin.Y + 30),
            TitleColor, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var buttonRect = GetButtonRect(panelOrigin);
        spriteBatch.Draw(_pixel, buttonRect, buttonRect.Contains(hoverPoint) ? ButtonFillHover : ButtonFill);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, buttonRect, new Color(40, 40, 40), 1);
        var labelSize = _font.MeasureString(ButtonLabel) * 0.5f;
        spriteBatch.DrawString(_font, ButtonLabel,
            new Vector2(buttonRect.Center.X - labelSize.X / 2f, buttonRect.Center.Y - labelSize.Y / 2f),
            Color.Black, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
