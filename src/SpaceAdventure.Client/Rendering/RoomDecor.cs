using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// What makes a compartment look lived-in rather than like a rectangle with a label: a painted
// walkway down the deck, a colour that says at a glance which compartment you are standing in,
// and a pool of light from the ceiling.
//
// None of it is collision - the walking and the sight lines still use the compartment's rectangle.
// That separation is the whole trick: the room can be drawn as any shape at all as long as the
// drawn shape stays inside the real one.
public static partial class RoomDecor
{
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
