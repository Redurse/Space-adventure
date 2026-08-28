using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// M57 - "режим ускорения времени": the captain tab's own 4-button speed selector (World.
// TimeAcceleration.cs's own doc comment explains why this runs extra physics steps per real tick
// instead of scaling deltaSeconds - not this widget's concern, it just shows/sets the level).
public sealed class TimeAccelerationWidget
{
    private static readonly int[] Levels = { 1, 10, 100, 1000 };
    private const int ButtonWidth = 60;
    private const int ButtonHeight = 26;
    private const int Gap = 4;
    // M57 - "Флип": the 1g flip-and-burn maneuver's own one-press 180° turn (World.ShipField.cs's
    // FlipHeading), sitting right after the 4 level buttons on the same row.
    private const int FlipButtonWidth = 70;

    public static readonly Point Size = new(ButtonWidth * Levels.Length + Gap * Levels.Length + FlipButtonWidth, ButtonHeight + 18);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public TimeAccelerationWidget(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetLevelButtonRect(int index, Vector2 origin) =>
        new((int)origin.X + index * (ButtonWidth + Gap), (int)origin.Y + 18, ButtonWidth, ButtonHeight);

    public static Rectangle GetFlipButtonRect(Vector2 origin) =>
        new((int)origin.X + Levels.Length * (ButtonWidth + Gap), (int)origin.Y + 18, FlipButtonWidth, ButtonHeight);

    public static int? LevelAt(int index) => index >= 0 && index < Levels.Length ? Levels[index] : null;

    public void Draw(SpriteBatch spriteBatch, int currentLevel, Vector2 origin)
    {
        spriteBatch.DrawString(_font, "Ускорение времени", origin, Color.LightGray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        for (var i = 0; i < Levels.Length; i++)
        {
            var rect = GetLevelButtonRect(i, origin);
            var active = Levels[i] == currentLevel;
            spriteBatch.Draw(_pixel, rect, active ? new Color(200, 120, 30) : new Color(50, 50, 50));
            var label = $"×{Levels[i]}";
            var size = _font.MeasureString(label) * 0.45f;
            spriteBatch.DrawString(_font, label,
                new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f),
                active ? Color.White : Color.LightGray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }

        var flipRect = GetFlipButtonRect(origin);
        spriteBatch.Draw(_pixel, flipRect, new Color(50, 90, 120));
        var flipSize = _font.MeasureString("Флип") * 0.45f;
        spriteBatch.DrawString(_font, "Флип",
            new Vector2(flipRect.Center.X - flipSize.X / 2f, flipRect.Center.Y - flipSize.Y / 2f),
            Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }
}
