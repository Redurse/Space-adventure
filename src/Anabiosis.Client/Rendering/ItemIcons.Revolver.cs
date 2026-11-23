using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The revolver and its rounds, drawn after Barotrauma's: a gunmetal six-shooter seen from the side (barrel to the right) with a fluted cylinder, a hammer,
// a trigger guard and a brown grip, and a brass cartridge with a grey bullet. Built from the same bars and circles as the rest of the icons.
public static partial class ItemIcons
{
    private static bool HasRevolverIcon(ItemType type) => type is ItemType.Revolver or ItemType.RevolverRounds;

    private static bool DrawRevolverIcon(SpriteBatch spriteBatch, Texture2D pixel, ItemType type, Rectangle rect, float angle)
    {
        if (!HasRevolverIcon(type))
            return false;
        if (type == ItemType.Revolver)
            DrawRevolver(spriteBatch, pixel, rect, angle);
        else
            DrawRevolverRounds(spriteBatch, pixel, rect);
        return true;
    }

    private static void DrawRevolver(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float a)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var steel = new Color(126, 130, 138);
        var steelDark = new Color(78, 82, 90);
        var blued = new Color(46, 48, 54);
        var wood = new Color(132, 84, 46);
        var woodDark = new Color(88, 54, 30);
        var light = Color.White * 0.35f;

        // Grip, leaning back from under the frame, with a darker edge and a lit side.
        Bar(spriteBatch, pixel, origin, a, scale, -0.27f, 0.14f, 0.36f, 0.20f, woodDark, 1.12f);
        Bar(spriteBatch, pixel, origin, a, scale, -0.27f, 0.14f, 0.34f, 0.16f, wood, 1.12f);
        Bar(spriteBatch, pixel, origin, a, scale, -0.30f, 0.09f, 0.26f, 0.03f, Color.White * 0.18f, 1.12f);
        Bar(spriteBatch, pixel, origin, a, scale, -0.22f, 0.31f, 0.13f, 0.04f, blued, 0.25f); // butt cap
        // Barrel, with a top rib and the front sight.
        Bar(spriteBatch, pixel, origin, a, scale, 0.34f, -0.05f, 0.40f, 0.11f, steel);
        Bar(spriteBatch, pixel, origin, a, scale, 0.34f, -0.095f, 0.40f, 0.025f, light);
        Bar(spriteBatch, pixel, origin, a, scale, 0.34f, 0.0f, 0.40f, 0.03f, steelDark); // underlug
        Bar(spriteBatch, pixel, origin, a, scale, 0.50f, -0.135f, 0.05f, 0.05f, blued); // front sight
        Circle(spriteBatch, pixel, origin, a, scale, 0.545f, -0.05f, 0.043f, blued); // muzzle
        // Frame behind and above the cylinder.
        Bar(spriteBatch, pixel, origin, a, scale, -0.12f, -0.075f, 0.34f, 0.11f, steelDark);
        Bar(spriteBatch, pixel, origin, a, scale, -0.12f, -0.115f, 0.30f, 0.025f, light);
        // The cylinder: a fat drum, flutes cut into it, a bright rim.
        Bar(spriteBatch, pixel, origin, a, scale, 0.05f, -0.01f, 0.24f, 0.23f, steel);
        Bar(spriteBatch, pixel, origin, a, scale, 0.05f, 0.075f, 0.24f, 0.04f, steelDark); // shaded underside
        for (var i = 0; i < 3; i++)
            Bar(spriteBatch, pixel, origin, a, scale, -0.03f + i * 0.08f, -0.01f, 0.03f, 0.17f, steelDark);
        for (var i = 0; i < 3; i++)
            Bar(spriteBatch, pixel, origin, a, scale, -0.045f + i * 0.08f, -0.01f, 0.012f, 0.15f, light);
        Bar(spriteBatch, pixel, origin, a, scale, 0.05f, -0.115f, 0.24f, 0.025f, light);
        // Hammer, trigger and the guard around it.
        Bar(spriteBatch, pixel, origin, a, scale, -0.23f, -0.15f, 0.10f, 0.055f, blued, -0.7f);
        Bar(spriteBatch, pixel, origin, a, scale, -0.06f, 0.115f, 0.20f, 0.035f, blued); // guard, lower run
        Bar(spriteBatch, pixel, origin, a, scale, 0.035f, 0.08f, 0.09f, 0.035f, blued, 1.1f); // guard, front
        Bar(spriteBatch, pixel, origin, a, scale, -0.10f, 0.055f, 0.07f, 0.03f, steel, 0.5f); // trigger
    }

    // A stack of rounds: three brass cases with grey bullets, as the stack icon in the inventory reads in Barotrauma. The count itself ("x6") is
    // written over the slot by InventoryPanel.
    private static void DrawRevolverRounds(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var brass = new Color(214, 170, 70);
        var brassDark = new Color(150, 112, 40);
        var brassLight = new Color(250, 222, 140);
        var lead = new Color(186, 190, 198);
        var leadDark = new Color(116, 120, 130);

        // Each round lies along its own axis (the whole icon is built in a frame turned a little), three of them fanned side by side.
        var a = -0.5f;
        for (var i = 0; i < 3; i++)
        {
            var across = -0.2f + i * 0.2f;
            var shift = -0.04f + i * 0.04f;
            Bar(spriteBatch, pixel, origin, a, scale, -0.1f + shift, across, 0.30f, 0.15f, brassDark);          // case outline
            Bar(spriteBatch, pixel, origin, a, scale, -0.1f + shift, across, 0.28f, 0.12f, brass);              // case
            Bar(spriteBatch, pixel, origin, a, scale, -0.12f + shift, across - 0.035f, 0.24f, 0.03f, brassLight); // lit side
            Bar(spriteBatch, pixel, origin, a, scale, -0.255f + shift, across, 0.03f, 0.17f, brassDark);        // rim
            Bar(spriteBatch, pixel, origin, a, scale, 0.115f + shift, across, 0.115f, 0.12f, leadDark);         // bullet
            Bar(spriteBatch, pixel, origin, a, scale, 0.115f + shift, across, 0.105f, 0.095f, lead);
            Circle(spriteBatch, pixel, origin, a, scale, 0.18f + shift, across, 0.052f, lead);                  // rounded nose
        }
    }
}
