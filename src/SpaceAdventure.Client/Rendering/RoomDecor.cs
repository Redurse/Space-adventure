using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// What makes a compartment look lived-in rather than like a rectangle with a label: a painted
// walkway down the deck, a colour that says at a glance which compartment you are standing in,
// rounded corners where the bulkheads meet, and a pool of light from the ceiling.
//
// None of it is collision - the walking and the sight lines still use the compartment's rectangle.
// That separation is the whole trick: the room can be drawn as any shape at all as long as the
// drawn shape stays inside the real one.
public static class RoomDecor
{
    private const int FilletSegments = 7;

    // Compartments are colour-coded by what they are for, in the SS13 tradition of painting a
    // department's floor. Matched on the id so every hull class gets the same colours for the same
    // kind of room without a table per ship.
    public static Color Accent(string roomId) => roomId switch
    {
        var id when id.Contains("cockpit") || id.Contains("bridge") => new Color(86, 148, 196),
        var id when id.Contains("armory") || id.Contains("weapon") => new Color(190, 96, 84),
        var id when id.Contains("reactor") || id.Contains("engine") => new Color(214, 148, 62),
        var id when id.Contains("shield") => new Color(88, 190, 186),
        var id when id.Contains("life") || id.Contains("oxygen") || id.Contains("med") => new Color(104, 184, 120),
        var id when id.Contains("airlock") => new Color(150, 122, 200),
        var id when id.Contains("cargo") || id.Contains("storage") => new Color(176, 146, 96),
        _ => new Color(126, 138, 156),
    };

    // A painted walkway down the compartment's long axis, edged in the department colour. Deck
    // markings are how a real ship tells you where to walk, and they give the floor a direction -
    // an unmarked floor reads as empty space no matter how much grating is drawn on it.
    public static void DrawDeckMarkings(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent)
    {
        var horizontal = rect.Width >= rect.Height;
        var band = horizontal
            ? new Rectangle(rect.X + 8, rect.Center.Y - rect.Height / 6, rect.Width - 16, rect.Height / 3)
            : new Rectangle(rect.Center.X - rect.Width / 6, rect.Y + 8, rect.Width / 3, rect.Height - 16);
        if (band.Width <= 4 || band.Height <= 4)
            return;

        spriteBatch.Draw(pixel, band, Color.White * 0.035f);
        if (horizontal)
        {
            spriteBatch.Draw(pixel, new Rectangle(band.X, band.Y, band.Width, 2), accent * 0.36f);
            spriteBatch.Draw(pixel, new Rectangle(band.X, band.Bottom - 2, band.Width, 2), accent * 0.36f);
            for (var x = band.X + 14; x < band.Right - 6; x += 34)
                spriteBatch.Draw(pixel, new Rectangle(x, band.Center.Y - 1, 16, 2), accent * 0.28f);
        }
        else
        {
            spriteBatch.Draw(pixel, new Rectangle(band.X, band.Y, 2, band.Height), accent * 0.36f);
            spriteBatch.Draw(pixel, new Rectangle(band.Right - 2, band.Y, 2, band.Height), accent * 0.36f);
            for (var y = band.Y + 14; y < band.Bottom - 6; y += 34)
                spriteBatch.Draw(pixel, new Rectangle(band.Center.X - 1, y, 2, 16), accent * 0.28f);
        }
    }

    // Ceiling light: concentric translucent rectangles, brightest in the middle. Flat rooms lit
    // evenly to the corners look like diagrams; a pool of light with darker edges looks like a
    // place, and it costs a handful of quads.
    public static void DrawLightPool(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent)
    {
        const int layers = 6;
        for (var i = 0; i < layers; i++)
        {
            var t = i / (float)layers;
            var inset = (int)(MathF.Min(rect.Width, rect.Height) * 0.5f * t * 0.92f);
            var pool = new Rectangle(rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);
            if (pool.Width <= 2 || pool.Height <= 2)
                break;
            spriteBatch.Draw(pixel, pool, Color.Lerp(Color.White, accent, 0.35f) * 0.038f);
        }

        // The fixture itself, so the light has a source on the ceiling above the deck.
        var lamp = new Rectangle(rect.Center.X - 9, rect.Center.Y - 3, 18, 6);
        spriteBatch.Draw(pixel, lamp, Color.Lerp(Color.White, accent, 0.3f) * 0.5f);
    }

    // Rounded inside corners. The compartment is a rectangle to everything that matters, but the
    // eye reads a filled quarter-arc at each corner as a curved bulkhead - which is what the ship
    // should look like, and what no amount of extra rectangles would achieve.
    public static void DrawCornerFillets(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, float radiusPixels)
    {
        var radius = MathF.Min(radiusPixels, MathF.Min(rect.Width, rect.Height) / 3f);
        if (radius < 2f)
            return;

        DrawFillet(spriteBatch, pixel, new Vector2(rect.X, rect.Y), radius, MathF.PI / 2f, color);
        DrawFillet(spriteBatch, pixel, new Vector2(rect.Right, rect.Y), radius, MathF.PI, color);
        DrawFillet(spriteBatch, pixel, new Vector2(rect.Right, rect.Bottom), radius * 1f, -MathF.PI / 2f, color);
        DrawFillet(spriteBatch, pixel, new Vector2(rect.X, rect.Bottom), radius, 0f, color);
    }

    // Fans the wedge between a square corner and the arc that rounds it off. startAngle points at
    // the first of the two edges meeting there; the sweep always runs a quarter turn from it.
    private static void DrawFillet(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, float radius, float startAngle, Color color)
    {
        var previous = corner + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * radius;
        for (var i = 1; i <= FilletSegments; i++)
        {
            var angle = startAngle + i * (MathF.PI / 2f) / FilletSegments;
            var point = corner + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            Primitives.FillTriangle(spriteBatch, pixel, corner, previous, point, color);
            previous = point;
        }
    }

    // Wall lamps: a bright sliver on the inside face of each bulkhead, with the glow it throws onto
    // the deck below it.
    public static void DrawWallLamps(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent, bool alarmed)
    {
        var color = (alarmed ? new Color(255, 120, 96) : Color.Lerp(Color.White, accent, 0.4f)) * 0.65f;
        var quarter = rect.Width / 4;
        var quarterH = rect.Height / 4;

        for (var i = 1; i <= 3; i += 2)
        {
            spriteBatch.Draw(pixel, new Rectangle(rect.X + quarter * i - 7, rect.Y + 2, 14, 3), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + quarter * i - 7, rect.Bottom - 5, 14, 3), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, rect.Y + quarterH * i - 7, 3, 14), color);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 5, rect.Y + quarterH * i - 7, 3, 14), color);
        }
    }
}
