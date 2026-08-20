using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// The housing every block terminal is drawn inside. One helper rather than fourteen panels each
// styling themselves: with fourteen separate looks the interface stops reading as one ship, and a
// change of mind means fourteen edits. Panels keep only their own content and let this draw the
// metal around it.
//
// The look it is after is a real machine you are standing at rather than a rectangle floating over
// the world: a bevelled steel housing with rivets and a contact shadow, a title strip carrying the
// department colour and a unit code, and a screen genuinely recessed into it - dark bezel, phosphor
// field, scanlines and a slow frame roll.
public static class DevicePanelChrome
{
    private static Texture2D? _pixel;
    private static Texture2D? _plate;
    private static Texture2D? _shade;

    // Every block terminal is the same box. A reactor readout that is half the size of the power
    // one reads as two different pieces of equipment bolted on by two different people - and the
    // player, who opens these by walking into a block, has to re-find the layout each time. Sized
    // to the widest content any of them has (the power panel's 220px allocation bars at x+150).
    public static readonly Point Standard = new(404, 200);

    // How far the housing extends up and left of the content origin panels lay themselves out
    // from. Named rather than repeated as literals because centring a panel means solving for
    // the origin given the housing size, and that only works if both ends agree on the margin.
    public const int OriginInsetX = 14;
    public const int OriginInsetY = 34;

    // Convenience for the usual case: a housing of the standard size, hung off the origin a panel
    // was already laying its content out from.
    public static Rectangle StandardBounds(Vector2 origin) =>
        new((int)origin.X - OriginInsetX, (int)origin.Y - OriginInsetY, Standard.X, Standard.Y);

    // The content origin that puts a housing of `size` in the middle of a screen of `screen`.
    public static Vector2 CentredOrigin(Point size, Point screen) => new(
        (screen.X - size.X) / 2f + OriginInsetX,
        (screen.Y - size.Y) / 2f + OriginInsetY);

    // Height of the title strip along the top of the housing.
    private const int TitleHeight = 22;
    // How far the screen is inset from the housing on each side.
    private const int Inset = 8;

    // Draws the housing and returns the recessed screen area the caller should lay its content out
    // in. Everything is derived from `bounds`, so a panel only has to know how big it wants to be.
    public static Rectangle Draw(SpriteBatch spriteBatch, SpriteFont font, Rectangle bounds,
        string title, string code, Color accent, float totalSeconds)
    {
        EnsureTextures(spriteBatch.GraphicsDevice);

        ShipRenderer.DrawPanel(spriteBatch, _pixel!, bounds, new Color(44, 50, 62), accent * 0.85f, 2, _plate, _shade);

        // Title strip: department colour along the top, the panel name in it, and a unit code at the
        // right end. The code is what makes the thing read as equipment somebody installed rather
        // than a window the game opened.
        var strip = new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, TitleHeight);
        spriteBatch.Draw(_pixel, strip, new Color(24, 28, 36));
        spriteBatch.Draw(_pixel, new Rectangle(strip.X, strip.Y, 4, strip.Height), accent);
        spriteBatch.Draw(_pixel, new Rectangle(strip.X, strip.Bottom - 1, strip.Width, 1), Color.Black * 0.5f);
        spriteBatch.DrawString(font, title, new Vector2(strip.X + 10, strip.Y + 4), accent,
            0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        if (code.Length > 0)
        {
            var codeWidth = font.MeasureString(code).X * 0.45f;
            spriteBatch.DrawString(font, code, new Vector2(strip.Right - codeWidth - 8, strip.Y + 6),
                new Color(120, 132, 148), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }

        var screen = new Rectangle(bounds.X + Inset, strip.Bottom + 5,
            bounds.Width - Inset * 2, bounds.Bottom - strip.Bottom - 5 - Inset);
        DrawScreenField(spriteBatch, screen, accent, totalSeconds);
        return new Rectangle(screen.X + 6, screen.Y + 5, screen.Width - 12, screen.Height - 10);
    }

    // The recess itself. Drawn separately from the housing so a panel that wants two screens, or a
    // screen beside a bank of switches, can place them itself.
    public static void DrawScreenField(SpriteBatch spriteBatch, Rectangle screen, Color phosphor, float totalSeconds)
    {
        EnsureTextures(spriteBatch.GraphicsDevice);

        // Bezel first, then the field inside it: the one pixel of black around the glass is what
        // makes it read as recessed rather than painted on.
        spriteBatch.Draw(_pixel, new Rectangle(screen.X - 2, screen.Y - 2, screen.Width + 4, screen.Height + 4), Color.Black * 0.85f);
        spriteBatch.Draw(_pixel, screen, new Color(10, 16, 18));
        spriteBatch.Draw(_pixel, screen, phosphor * 0.06f);

        // Scanlines every third row. Period 3 rather than 2 so they survive at the sizes these
        // panels are actually drawn at instead of turning into a flat grey wash.
        for (var y = screen.Y + 1; y < screen.Bottom; y += 3)
            spriteBatch.Draw(_pixel, new Rectangle(screen.X, y, screen.Width, 1), Color.Black * 0.28f);

        // One brighter band drifting down the field, the way a tube that is very slightly out of
        // sync with its own frame rate rolls. Slow enough not to distract from the readouts.
        var roll = (int)(screen.Y + (totalSeconds * 26f) % (screen.Height + 40) - 20);
        for (var i = 0; i < 10; i++)
        {
            var y = roll + i;
            if (y < screen.Y || y >= screen.Bottom)
                continue;
            spriteBatch.Draw(_pixel, new Rectangle(screen.X, y, screen.Width, 1), phosphor * (0.05f * (1f - i / 10f)));
        }

        // Light bleeding out past the bezel onto the housing. Panels are drawn after the post chain
        // has already been composited (crisp text is worth more here than uniform treatment), so the
        // real bloom cannot reach them - this is that glow done locally, as a few expanding
        // low-alpha frames. Same trick the menu title uses, and at these sizes indistinguishable
        // from the genuine article.
        for (var i = 1; i <= 5; i++)
        {
            var halo = new Rectangle(screen.X - i * 2, screen.Y - i * 2, screen.Width + i * 4, screen.Height + i * 4);
            DrawFrame(spriteBatch, halo, phosphor * (0.05f * (1f - i / 6f)));
        }

        // Glass: a lit top edge and a darkened bottom, which is the cheapest way to suggest a curved
        // surface catching the room light.
        spriteBatch.Draw(_pixel, new Rectangle(screen.X, screen.Y, screen.Width, 1), Color.White * 0.16f);
        spriteBatch.Draw(_pixel, new Rectangle(screen.X, screen.Bottom - 1, screen.Width, 1), Color.Black * 0.4f);
    }

    // A value with its own label and unit, laid out the way an instrument does it: the number large
    // and bright because it is what you came to read, the label small and dim above it, the unit
    // smaller still. Panels that draw `label: value` in one string at one size give the eye nothing
    // to latch onto, which is most of why a wall of readouts becomes unreadable.
    public static void DrawReadout(SpriteBatch spriteBatch, SpriteFont font, Vector2 at,
        string label, string value, string unit, Color phosphor)
    {
        EnsureTextures(spriteBatch.GraphicsDevice);
        spriteBatch.DrawString(font, label, at, new Color(120, 138, 150), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        var valuePos = at + new Vector2(0, 11);
        // The number is the brightest thing on the panel, so it is the one that has to look lit
        // rather than printed: four offset copies at low alpha under the crisp face.
        foreach (var offset in new[] { new Vector2(-1.5f, 0), new Vector2(1.5f, 0), new Vector2(0, -1.5f), new Vector2(0, 1.5f) })
            spriteBatch.DrawString(font, value, valuePos + offset, phosphor * 0.28f, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(font, value, valuePos, phosphor, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        if (unit.Length == 0)
            return;

        var valueWidth = font.MeasureString(value).X * 0.85f;
        spriteBatch.DrawString(font, unit, valuePos + new Vector2(valueWidth + 4, 8),
            phosphor * 0.55f, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    private static void DrawFrame(SpriteBatch spriteBatch, Rectangle rect, Color colour)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), colour);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), colour);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), colour);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), colour);
    }

    private static void EnsureTextures(GraphicsDevice device)
    {
        if (_pixel is not null)
            return;

        _pixel = new Texture2D(device, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _plate = TileTextures.CreateDevicePlate(device);
        _shade = TileTextures.CreateFaceShade(device);
    }
}
