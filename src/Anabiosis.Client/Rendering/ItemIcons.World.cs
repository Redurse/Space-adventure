using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// How an item looks lying in (or held in) the scene, as opposed to sitting in a hotbar slot. The slot
// icons were drawn to be read against a plain dark square; on a grey deck they dissolve into the floor.
// Out here every item gets a dark pad behind it, a rim in its category colour, a drop shadow and its
// name underneath, so a welder reads as a welder and not as a patch of similar-looking floor.
public static partial class ItemIcons
{
    public const float WorldItemSize = 36f;

    /// <summary>The rim colour that says what kind of thing this is at a glance.</summary>
    public static Color CategoryColor(ItemType item)
    {
        if (ComponentDefinitions.ComponentKindFor(item) is not null)
            return new Color(176, 128, 236);                                     // wiring parts
        return item switch
        {
            ItemType.Rifle or ItemType.LaserRifle or ItemType.Knife or ItemType.Axe or ItemType.AmmoCrate or ItemType.Magazine
                => new Color(232, 86, 72),                                       // weapons and ammunition
            ItemType.MedKit => new Color(96, 214, 128),                          // medicine
            ItemType.OxygenTank or ItemType.WeldingTank or ItemType.FuelRod or ItemType.Spacesuit
                => new Color(92, 176, 236),                                      // life support and fuel
            ItemType.Wrench or ItemType.Screwdriver or ItemType.WeldingTool or ItemType.Cutter
                or ItemType.GoshaScrewdriver or ItemType.WireSpool
                => new Color(238, 176, 64),                                      // tools
            ItemType.Hyperium => new Color(120, 225, 255),
            _ when ItemDefinitions.IsRawOre(item) => new Color(190, 176, 150),   // ore and raw materials
            _ => new Color(226, 226, 232),
        };
    }

    public static string WorldLabel(ItemType item)
    {
        var name = ItemDefinitions.DisplayName(item);
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>An item lying on the floor / in open space: shadow, pad, rim, icon and (optionally) its name.</summary>
    public static void DrawInWorld(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, ItemType item, Vector2 center,
        float totalSeconds, bool label = true, float size = WorldItemSize)
    {
        var rim = CategoryColor(item);
        var pulse = 0.75f + 0.25f * MathF.Sin(totalSeconds * 3.5f + center.X * 0.05f);
        var radius = size * 0.66f;

        HudIcons.FillCircle(spriteBatch, pixel, center + new Vector2(2f, 3f), radius, Color.Black * 0.35f);            // shadow
        HudIcons.FillCircle(spriteBatch, pixel, center, radius + 3f, rim * (0.16f * pulse));                           // soft glow
        HudIcons.FillCircle(spriteBatch, pixel, center, radius, new Color(104, 112, 130) * 0.92f);                      // pad: mid-tone so both dark and pale icons stand out from it
        HudIcons.DrawRingArc(spriteBatch, pixel, center, radius, 0f, 360f, rim * (0.75f + 0.25f * pulse), 28, 2f);     // rim

        DrawIconOrChip(spriteBatch, pixel, font, item, center, size);

        if (!label)
            return;
        DrawNameplate(spriteBatch, pixel, font, WorldLabel(item), new Vector2(center.X, center.Y + radius + 3f), rim);
    }

    /// <summary>Just a soft shadow under something held in a hand, so it lifts off the deck without a pad
    /// bigger than the person holding it.</summary>
    public static void DrawHeldBacking(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float size)
    {
        HudIcons.FillCircle(spriteBatch, pixel, center + new Vector2(1.5f, 3f), size * 0.36f, Color.Black * 0.22f);
    }

    /// <summary>The item's icon at roughly `size` pixels, or the coloured chip with its short label when no icon exists.</summary>
    public static void DrawIconOrChip(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, ItemType item, Vector2 center, float size)
    {
        var half = (int)(size / 2f);
        var rect = new Rectangle((int)center.X - half, (int)center.Y - half, half * 2, half * 2);
        if (HasIcon(item))
        {
            Draw(spriteBatch, pixel, item, rect);
            return;
        }

        spriteBatch.Draw(pixel, rect, InventoryPanel.ItemColor(item));
        var text = ItemDefinitions.ShortLabel(item);
        if (text.Length == 0)
            return;
        var measured = font.MeasureString(text) * 0.45f;
        spriteBatch.DrawString(font, text, center - measured / 2f, Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    /// <summary>Small text on a dark plate with a coloured underline, centred on `topCenter`.</summary>
    public static void DrawNameplate(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, string text, Vector2 topCenter, Color accent)
    {
        const float scale = 0.5f;
        var measured = font.MeasureString(text) * scale;
        var box = new Rectangle((int)(topCenter.X - measured.X / 2f) - 4, (int)topCenter.Y, (int)measured.X + 8, (int)measured.Y + 3);
        spriteBatch.Draw(pixel, box, new Color(10, 12, 18) * 0.8f);
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Bottom - 2, box.Width, 2), accent * 0.9f);
        spriteBatch.DrawString(font, text, new Vector2(box.X + 4, box.Y + 1), Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
