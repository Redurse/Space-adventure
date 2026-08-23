using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;
using SpaceAdventure.Client.Audio;

namespace SpaceAdventure.Client.Rendering;

// Shown while the jukebox block is "open" - a Barotrauma-style control panel: an on/off checkbox,
// a track stepper and a volume stepper, each with its own pair of arrow buttons. All three widgets
// publish their own rectangles the same way ReactorPanel's fuel-rod slots do, so the click handler
// (Game1.Input.cs) and the drawing here can never disagree about where a button actually is.
public sealed class JukeboxPanel
{
    private const int RowHeight = 34;
    private const int ButtonSize = 22;
    private const int ValueWidth = 130;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public JukeboxPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    private static Vector2 RowOrigin(Vector2 origin, int row) => origin + new Vector2(0, 20 + row * RowHeight);

    public static Rectangle GetCheckboxRect(Vector2 origin)
    {
        var at = RowOrigin(origin, 0);
        return new Rectangle((int)at.X, (int)at.Y, ButtonSize, ButtonSize);
    }

    public static Rectangle GetTrackPrevRect(Vector2 origin) => GetStepperButtonRect(origin, 1, prev: true);
    public static Rectangle GetTrackNextRect(Vector2 origin) => GetStepperButtonRect(origin, 1, prev: false);
    public static Rectangle GetVolumeDownRect(Vector2 origin) => GetStepperButtonRect(origin, 2, prev: true);
    public static Rectangle GetVolumeUpRect(Vector2 origin) => GetStepperButtonRect(origin, 2, prev: false);

    private static Rectangle GetStepperButtonRect(Vector2 origin, int row, bool prev)
    {
        var at = RowOrigin(origin, row);
        var x = prev ? at.X + 150 : at.X + 150 + ButtonSize + ValueWidth;
        return new Rectangle((int)x, (int)at.Y, ButtonSize, ButtonSize);
    }

    public void Draw(SpriteBatch spriteBatch, JukeboxState jukebox, Vector2 origin, float totalSeconds)
    {
        var bounds = DevicePanelChrome.StandardBounds(origin);
        var phosphor = new Color(224, 196, 120);
        DevicePanelChrome.Draw(spriteBatch, _font, bounds, "МУЗЫКАЛЬНЫЙ АВТОМАТ", "JB-1", phosphor, totalSeconds);

        DrawCheckboxRow(spriteBatch, origin, jukebox.On);
        var trackTitle = jukebox.TrackIndex >= 0 && jukebox.TrackIndex < JukeboxTracks.All.Length
            ? JukeboxTracks.All[jukebox.TrackIndex].Title
            : "-";
        DrawStepperRow(spriteBatch, origin, 1, "Композиция", $"{jukebox.TrackIndex}", phosphor);
        DrawStepperRow(spriteBatch, origin, 2, "Громкость", $"{jukebox.Volume}", phosphor);

        var titleAt = RowOrigin(origin, 3);
        spriteBatch.DrawString(_font, trackTitle, titleAt, phosphor * 0.85f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawCheckboxRow(SpriteBatch spriteBatch, Vector2 origin, bool on)
    {
        var rect = GetCheckboxRect(origin);
        spriteBatch.Draw(_pixel, rect, Color.DimGray * 0.5f);
        DrawRectOutline(spriteBatch, rect, Color.LightGray, 1);
        if (on)
        {
            var inset = new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8);
            spriteBatch.Draw(_pixel, inset, new Color(120, 220, 140));
        }
        spriteBatch.DrawString(_font, "Вкл.", new Vector2(rect.Right + 8, rect.Y + 4), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawStepperRow(SpriteBatch spriteBatch, Vector2 origin, int row, string label, string value, Color phosphor)
    {
        var at = RowOrigin(origin, row);
        spriteBatch.DrawString(_font, label, at, Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        var prevRect = GetStepperButtonRect(origin, row, prev: true);
        var nextRect = GetStepperButtonRect(origin, row, prev: false);
        DrawSmallButton(spriteBatch, prevRect, "<");
        DrawSmallButton(spriteBatch, nextRect, ">");

        var valueRect = new Rectangle(prevRect.Right, prevRect.Y, ValueWidth, ButtonSize);
        spriteBatch.Draw(_pixel, valueRect, new Color(10, 16, 18));
        DrawRectOutline(spriteBatch, valueRect, Color.DimGray, 1);
        var valueSize = _font.MeasureString(value) * 0.55f;
        spriteBatch.DrawString(_font, value, new Vector2(valueRect.Center.X - valueSize.X / 2f, valueRect.Y + 3), phosphor, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawSmallButton(SpriteBatch spriteBatch, Rectangle rect, string glyph)
    {
        spriteBatch.Draw(_pixel, rect, new Color(50, 56, 66));
        DrawRectOutline(spriteBatch, rect, Color.LightGray, 1);
        var size = _font.MeasureString(glyph) * 0.55f;
        spriteBatch.DrawString(_font, glyph, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
