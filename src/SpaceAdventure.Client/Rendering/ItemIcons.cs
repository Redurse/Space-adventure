using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// Recognizable tool/tank silhouettes for the handful of item types a player actually looks at
// constantly (the hotbar, the held-item chip beside a character) - this project has no image
// assets, so each is a handful of rectangles in the same single-pixel style as everything else
// (HullSkin, RoomDecor, ComponentRenderer). Anything not covered here falls back to
// InventoryPanel's plain coloured square + short label, which is why callers check HasIcon first.
public static class ItemIcons
{
    public static bool HasIcon(ItemType type) => type is ItemType.Wrench or ItemType.Screwdriver
        or ItemType.WeldingTool or ItemType.Cutter or ItemType.OxygenTank or ItemType.WeldingTank;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, ItemType type, Rectangle rect)
    {
        switch (type)
        {
            case ItemType.Screwdriver: DrawScrewdriver(spriteBatch, pixel, rect); break;
            case ItemType.Wrench: DrawWrench(spriteBatch, pixel, rect); break;
            // Nozzle tinted the same colour each tool's own flame already is
            // (FieldRenderer.DrawWeldingFlame/DrawCuttingFlame) - the icon and the beam it fires
            // read as the same tool.
            case ItemType.WeldingTool: DrawGunTool(spriteBatch, pixel, rect, new Color(255, 140, 40)); break;
            case ItemType.Cutter: DrawGunTool(spriteBatch, pixel, rect, new Color(90, 150, 255)); break;
            case ItemType.OxygenTank: DrawTank(spriteBatch, pixel, rect, new Color(200, 206, 212), new Color(70, 140, 200)); break;
            case ItemType.WeldingTank: DrawTank(spriteBatch, pixel, rect, new Color(176, 158, 96), new Color(214, 90, 40)); break;
        }
    }

    // Fractional coordinates (0..1 across `rect`) rather than hand-picked pixel offsets, so every
    // icon still holds its proportions whether it lands in a 34px hotbar slot or a bigger held-item
    // chip.
    private static Rectangle Frac(Rectangle rect, float fx, float fy, float fw, float fh) => new(
        rect.X + (int)MathF.Round(fx * rect.Width), rect.Y + (int)MathF.Round(fy * rect.Height),
        Math.Max(1, (int)MathF.Round(fw * rect.Width)), Math.Max(1, (int)MathF.Round(fh * rect.Height)));

    private static void DrawScrewdriver(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        spriteBatch.Draw(pixel, Frac(rect, 0.44f, 0.40f, 0.12f, 0.52f), new Color(205, 208, 214)); // shaft
        spriteBatch.Draw(pixel, Frac(rect, 0.46f, 0.88f, 0.08f, 0.06f), Color.White * 0.8f); // tip highlight
        spriteBatch.Draw(pixel, Frac(rect, 0.40f, 0.36f, 0.20f, 0.07f), new Color(55, 55, 60)); // ferrule
        spriteBatch.Draw(pixel, Frac(rect, 0.28f, 0.06f, 0.44f, 0.32f), new Color(205, 70, 40)); // handle
        spriteBatch.Draw(pixel, Frac(rect, 0.32f, 0.10f, 0.10f, 0.24f), Color.White * 0.25f); // handle highlight
    }

    private static void DrawWrench(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var metal = new Color(195, 199, 205);
        spriteBatch.Draw(pixel, Frac(rect, 0.42f, 0.30f, 0.16f, 0.42f), metal); // shaft

        // A closed ring at the top - four thin bars round a hollow centre, rather than a filled
        // square with a hole punched in whatever the caller's own background happens to be.
        var frame = Frac(rect, 0.22f, 0.02f, 0.56f, 0.32f);
        var t = Math.Max(2, frame.Width / 5);
        spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Y, frame.Width, t), metal);
        spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Bottom - t, frame.Width, t), metal);
        spriteBatch.Draw(pixel, new Rectangle(frame.X, frame.Y, t, frame.Height), metal);
        spriteBatch.Draw(pixel, new Rectangle(frame.Right - t, frame.Y, t, frame.Height), metal);

        // An open jaw at the bottom - two prongs with a gap between them, the classic wrench mouth.
        spriteBatch.Draw(pixel, Frac(rect, 0.30f, 0.70f, 0.40f, 0.10f), metal);
        spriteBatch.Draw(pixel, Frac(rect, 0.20f, 0.78f, 0.18f, 0.16f), metal);
        spriteBatch.Draw(pixel, Frac(rect, 0.62f, 0.78f, 0.18f, 0.16f), metal);
    }

    // Shared silhouette for the welder and the cutter - a gripped tool with a barrel and a hot tip,
    // differing only in the tip's colour (which flame it lights).
    private static void DrawGunTool(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color nozzle)
    {
        var metal = new Color(120, 125, 136);
        var dark = new Color(45, 45, 52);
        spriteBatch.Draw(pixel, Frac(rect, 0.18f, 0.54f, 0.16f, 0.36f), dark); // grip
        spriteBatch.Draw(pixel, Frac(rect, 0.30f, 0.30f, 0.42f, 0.30f), metal); // body/receiver
        spriteBatch.Draw(pixel, Frac(rect, 0.30f, 0.24f, 0.42f, 0.08f), Color.White * 0.2f); // top highlight
        spriteBatch.Draw(pixel, Frac(rect, 0.30f, 0.58f, 0.10f, 0.12f), dark); // trigger guard
        spriteBatch.Draw(pixel, Frac(rect, 0.64f, 0.40f, 0.24f, 0.14f), new Color(80, 85, 96)); // barrel
        spriteBatch.Draw(pixel, Frac(rect, 0.86f, 0.38f, 0.12f, 0.18f), nozzle); // hot tip
    }

    private static void DrawTank(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color body, Color band)
    {
        spriteBatch.Draw(pixel, Frac(rect, 0.42f, 0.02f, 0.16f, 0.10f), new Color(50, 50, 56)); // valve
        spriteBatch.Draw(pixel, Frac(rect, 0.30f, 0.12f, 0.40f, 0.72f), body); // cylinder
        spriteBatch.Draw(pixel, Frac(rect, 0.34f, 0.16f, 0.08f, 0.62f), Color.White * 0.25f); // side highlight
        spriteBatch.Draw(pixel, Frac(rect, 0.30f, 0.52f, 0.40f, 0.12f), band); // identifying colour band
    }
}
