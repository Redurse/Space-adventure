using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// M57 - the 3 buttons that switch which of the helm screen's 3 windows (HelmTab) is currently
// shown. Fixed position, not draggable like HelmButtonsWidget - it's a mode switch, not an
// instrument, so it stays put at the top of the screen the same way ShipSchematicPanel's own
// category tabs stay fixed relative to that panel.
public sealed class HelmTabBar
{
    // M57 follow-up - "3 квадратные кнопки" (square, not the original wide rectangles) so they
    // read as a compact mode-switch rather than competing with the actual instrument panels for
    // width.
    private const int ButtonWidth = 44;
    private const int ButtonHeight = 44;
    private const int Gap = 6;
    private const int TabCount = 3;

    public static readonly Point Size = new(ButtonWidth * TabCount + Gap * (TabCount - 1), ButtonHeight);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public HelmTabBar(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetTabRect(HelmTab tab, Vector2 origin) =>
        new((int)origin.X + (int)tab * (ButtonWidth + Gap), (int)origin.Y, ButtonWidth, ButtonHeight);

    // Short enough to actually fit a 44x44 square at a legible scale - the full role name shows
    // instead as a tooltip-style label drawn just above whichever button the mouse is over.
    private static string ShortLabel(HelmTab tab) => tab switch
    {
        HelmTab.Captain => "КАП",
        HelmTab.Scientist => "УЧ",
        HelmTab.Engineer => "ИНЖ",
        _ => "?",
    };

    private static string FullLabel(HelmTab tab) => tab switch
    {
        HelmTab.Captain => "Капитан",
        HelmTab.Scientist => "Учёный",
        HelmTab.Engineer => "Инженер",
        _ => tab.ToString(),
    };

    public void Draw(SpriteBatch spriteBatch, HelmTab current, Vector2 origin, Point mouse)
    {
        foreach (var tab in new[] { HelmTab.Captain, HelmTab.Scientist, HelmTab.Engineer })
        {
            var rect = GetTabRect(tab, origin);
            var active = tab == current;
            var hovered = rect.Contains(mouse);
            spriteBatch.Draw(_pixel, rect, active ? new Color(60, 100, 140) : hovered ? new Color(55, 60, 66) : new Color(35, 38, 42) * 0.92f);
            var label = ShortLabel(tab);
            var size = _font.MeasureString(label) * 0.42f;
            spriteBatch.DrawString(_font, label,
                new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f),
                active ? Color.White : Color.LightGray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

            if (hovered)
            {
                var fullLabel = FullLabel(tab);
                var fullSize = _font.MeasureString(fullLabel) * 0.42f;
                spriteBatch.DrawString(_font, fullLabel,
                    new Vector2(rect.Center.X - fullSize.X / 2f, rect.Y - fullSize.Y - 4),
                    Color.LightGray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
            }
        }
    }
}
