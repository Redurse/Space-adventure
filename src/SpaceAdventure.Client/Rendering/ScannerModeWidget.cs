using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// The navigation console's own activation control (M48 follow-up - "вместо кнопки сделай как
// верхнюю менюшку, чтобы радар приводился в действие переключением рычажка как это сделано
// визуально в баротравме"): replaces the old "[Клик] СКАН" button with a two-position toggle
// switch, Barotrauma-style - a single vertical track with a knob that sits at the top for
// Directional or the bottom for Circular. Clicking either label both selects that mode AND fires
// the pulse (Game1.Input.cs) - the switch itself is the trigger, there's no separate button any
// more. A small floating widget the player can drag out of the way, same "own dragged position,
// not tied to _openBlock" treatment HelmButtonsWidget already gets (Game1.PanelDrag.cs).
public sealed class ScannerModeWidget
{
    public static readonly Point Size = new(220, 100);
    public const int TitleBarHeight = 16;

    private static readonly Rectangle DirectionalRowLocal = new(0, TitleBarHeight, 220, 42);
    private static readonly Rectangle CircularRowLocal = new(0, TitleBarHeight + 42, 220, 42);
    private static readonly Rectangle TrackLocal = new(16, TitleBarHeight + 8, 18, 68);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ScannerModeWidget(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetTitleBarRect(Vector2 origin) => new((int)origin.X, (int)origin.Y, Size.X, TitleBarHeight);
    public static Rectangle GetDirectionalRowRect(Vector2 origin) => Offset(DirectionalRowLocal, origin);
    public static Rectangle GetCircularRowRect(Vector2 origin) => Offset(CircularRowLocal, origin);

    private static Rectangle Offset(Rectangle rect, Vector2 origin) =>
        new((int)origin.X + rect.X, (int)origin.Y + rect.Y, rect.Width, rect.Height);

    public void Draw(SpriteBatch spriteBatch, ScannerMode mode, float cooldownRemaining, Vector2 origin)
    {
        var housing = new Rectangle((int)origin.X, (int)origin.Y, Size.X, Size.Y);
        spriteBatch.Draw(_pixel, housing, new Color(20, 24, 30) * 0.92f);
        spriteBatch.Draw(_pixel, GetTitleBarRect(origin), new Color(45, 52, 60));
        spriteBatch.DrawString(_font, "Сканер", origin + new Vector2(6, 1), Color.LightGray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        DrawRow(spriteBatch, GetDirectionalRowRect(origin), "ЛУЧЕВОЙ СОНАР", mode == ScannerMode.Directional);
        DrawRow(spriteBatch, GetCircularRowRect(origin), "КРУГОВОЙ СОНАР", mode == ScannerMode.Circular);

        // One track spanning both rows, the knob sliding to whichever half is currently selected -
        // the same physical toggle-switch look the reference screenshot's own sonar mode control has.
        var track = Offset(TrackLocal, origin);
        spriteBatch.Draw(_pixel, track, new Color(10, 12, 15));
        DrawTrackOutline(spriteBatch, track, new Color(80, 90, 100));
        var knobY = mode == ScannerMode.Directional ? track.Y + track.Width / 2 : track.Bottom - track.Width / 2;
        var knobColor = mode == ScannerMode.Directional ? Color.LimeGreen : Color.DeepSkyBlue;
        HudIcons.FillCircle(spriteBatch, _pixel, new Vector2(track.Center.X, knobY), track.Width / 2f - 1f, knobColor);

        var ready = cooldownRemaining <= 0f;
        var status = ready ? "ГОТОВ" : $"Перезарядка: {cooldownRemaining:0.0}с";
        spriteBatch.DrawString(_font, status, origin + new Vector2(6, Size.Y - 14),
            ready ? Color.LimeGreen : Color.Gray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    private void DrawRow(SpriteBatch spriteBatch, Rectangle rect, string label, bool selected)
    {
        spriteBatch.Draw(_pixel, rect, (selected ? new Color(35, 55, 45) : new Color(28, 30, 34)) * 0.9f);
        spriteBatch.DrawString(_font, label, new Vector2(rect.X + 44, rect.Y + rect.Height / 2f - 6),
            selected ? Color.White : Color.Gray, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);
    }

    private void DrawTrackOutline(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
    }
}
