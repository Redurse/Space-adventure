using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// Recognizable tool/tank silhouettes for the handful of item types a player actually looks at
// constantly (the hotbar, the held-item chip beside a character) - this project has no image
// assets, so each is a handful of rotated bars in the same single-pixel style as everything else
// (HullSkin, RoomDecor, ComponentRenderer's rivets/ribs), angled like a held tool rather than drawn
// flat, to read as close as a procedural silhouette can get to an actual sprite. Anything not
// covered here falls back to InventoryPanel's plain coloured square + short label, which is why
// callers check HasIcon first.
public static class ItemIcons
{
    // Tools are drawn tilted nose-up-right, the way a held tool actually reads at a glance, rather
    // than dead flat in the slot.
    private const float ToolTilt = -0.58f;

    public static bool HasIcon(ItemType type) => type is ItemType.Wrench or ItemType.Screwdriver
        or ItemType.WeldingTool or ItemType.Cutter or ItemType.OxygenTank or ItemType.WeldingTank;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, ItemType type, Rectangle rect)
    {
        switch (type)
        {
            case ItemType.Screwdriver: DrawScrewdriver(spriteBatch, pixel, rect); break;
            case ItemType.Wrench: DrawWrench(spriteBatch, pixel, rect); break;
            // Tank/nozzle tinted the same colour each tool's own flame already is
            // (FieldRenderer.DrawWeldingFlame/DrawCuttingFlame) - the icon and the beam it fires
            // read as the same tool.
            case ItemType.WeldingTool: DrawGunTool(spriteBatch, pixel, rect, new Color(214, 130, 40), new Color(255, 170, 60)); break;
            case ItemType.Cutter: DrawGunTool(spriteBatch, pixel, rect, new Color(70, 150, 90), new Color(110, 200, 255)); break;
            case ItemType.OxygenTank: DrawTank(spriteBatch, pixel, rect, new Color(196, 202, 210), new Color(70, 140, 200)); break;
            case ItemType.WeldingTank: DrawTank(spriteBatch, pixel, rect, new Color(170, 152, 92), new Color(214, 90, 40)); break;
        }
    }

    // A rotated bar in the tool's own local frame: `alongAxis`/`acrossAxis` are fractions of `scale`
    // measured along the tilt and perpendicular to it, `length`/`thickness` likewise - so every part
    // of a tool scales and rotates together as one rigid body instead of being placed in absolute
    // pixels.
    private static void Bar(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, float baseAngle, float scale,
        float alongAxis, float acrossAxis, float length, float thickness, Color color, float extraRotation = 0f)
    {
        var cos = MathF.Cos(baseAngle);
        var sin = MathF.Sin(baseAngle);
        var x = alongAxis * scale;
        var y = acrossAxis * scale;
        var world = origin + new Vector2(x * cos - y * sin, x * sin + y * cos);
        spriteBatch.Draw(pixel, world, null, color, baseAngle + extraRotation, new Vector2(0.5f, 0.5f),
            new Vector2(length * scale, thickness * scale), SpriteEffects.None, 0f);
    }

    private static void DrawScrewdriver(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = ToolTilt;

        Bar(spriteBatch, pixel, origin, a, scale, -0.46f, 0f, 0.12f, 0.30f, new Color(25, 24, 26)); // butt cap
        Bar(spriteBatch, pixel, origin, a, scale, -0.26f, 0f, 0.34f, 0.34f, new Color(206, 62, 36)); // handle
        Bar(spriteBatch, pixel, origin, a, scale, -0.30f, -0.09f, 0.24f, 0.09f, Color.White * 0.3f); // handle highlight
        Bar(spriteBatch, pixel, origin, a, scale, -0.05f, 0f, 0.10f, 0.16f, new Color(40, 40, 44)); // ferrule
        Bar(spriteBatch, pixel, origin, a, scale, 0.24f, 0f, 0.48f, 0.085f, new Color(212, 215, 220)); // shaft
        Bar(spriteBatch, pixel, origin, a, scale, 0.20f, -0.02f, 0.36f, 0.03f, Color.White * 0.5f); // shaft highlight
        Bar(spriteBatch, pixel, origin, a, scale, 0.47f, 0f, 0.06f, 0.11f, Color.White * 0.85f); // flat tip
    }

    private static void DrawWrench(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = ToolTilt;
        var metal = new Color(202, 206, 212);
        var shade = new Color(120, 124, 132);

        Bar(spriteBatch, pixel, origin, a, scale, 0.02f, 0f, 0.42f, 0.15f, metal); // shaft
        Bar(spriteBatch, pixel, origin, a, scale, 0.02f, -0.045f, 0.38f, 0.04f, Color.White * 0.4f); // shaft highlight

        // A closed ring at the back - four bars round a hollow centre, rather than a filled square
        // with a hole punched in whatever the caller's own background happens to be.
        const float ringR = 0.20f;
        const float ringT = 0.075f;
        Bar(spriteBatch, pixel, origin, a, scale, -0.34f, -ringR + ringT / 2f, ringR * 2f, ringT, metal); // top
        Bar(spriteBatch, pixel, origin, a, scale, -0.34f, ringR - ringT / 2f, ringR * 2f, ringT, shade); // bottom (shadowed)
        Bar(spriteBatch, pixel, origin, a, scale, -0.34f - ringR + ringT / 2f, 0f, ringR * 2f, ringT, metal, MathF.PI / 2f); // left
        Bar(spriteBatch, pixel, origin, a, scale, -0.34f + ringR - ringT / 2f, 0f, ringR * 2f, ringT, metal, MathF.PI / 2f); // right

        // An open jaw at the front - two prongs splayed apart from the shaft, the classic open mouth.
        Bar(spriteBatch, pixel, origin, a, scale, 0.30f, 0f, 0.14f, 0.20f, metal); // base of the jaw
        Bar(spriteBatch, pixel, origin, a, scale, 0.42f, -0.10f, 0.20f, 0.09f, metal, -0.45f); // upper prong
        Bar(spriteBatch, pixel, origin, a, scale, 0.42f, 0.10f, 0.20f, 0.09f, shade, 0.45f); // lower prong (shadowed)
    }

    // Shared silhouette for the welder and the cutter - a gripped tool, barrel out front, a tank
    // mounted on top and a hot tip at the muzzle - differing only in colour (which tool, which flame).
    private static void DrawGunTool(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color tank, Color nozzle)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = ToolTilt;
        var metal = new Color(96, 100, 110);
        var dark = new Color(38, 38, 44);

        Bar(spriteBatch, pixel, origin, a, scale, -0.22f, 0.20f, 0.24f, 0.16f, dark, 1.15f); // grip, angled down from the body
        Bar(spriteBatch, pixel, origin, a, scale, -0.06f, 0f, 0.36f, 0.28f, metal); // body/receiver
        Bar(spriteBatch, pixel, origin, a, scale, -0.06f, -0.08f, 0.30f, 0.07f, Color.White * 0.22f); // top highlight
        Bar(spriteBatch, pixel, origin, a, scale, -0.02f, -0.24f, 0.26f, 0.20f, tank); // tank mounted on top
        Bar(spriteBatch, pixel, origin, a, scale, -0.06f, -0.30f, 0.18f, 0.05f, Color.White * 0.3f); // tank highlight
        Bar(spriteBatch, pixel, origin, a, scale, 0.02f, 0.13f, 0.10f, 0.10f, dark); // trigger guard
        Bar(spriteBatch, pixel, origin, a, scale, 0.30f, 0f, 0.30f, 0.12f, new Color(64, 68, 78)); // barrel
        Bar(spriteBatch, pixel, origin, a, scale, 0.48f, 0f, 0.10f, 0.16f, nozzle); // hot tip
    }

    private static void DrawTank(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color body, Color band)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f; // tanks stand upright rather than tilt like a held tool

        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.42f, 0.16f, 0.10f, new Color(48, 48, 54)); // valve
        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.05f, 0.62f, 0.42f, body, MathF.PI / 2f); // cylinder body
        Bar(spriteBatch, pixel, origin, a, scale, -0.09f, -0.05f, 0.40f, 0.09f, Color.White * 0.28f, MathF.PI / 2f); // side highlight
        Bar(spriteBatch, pixel, origin, a, scale, 0.13f, -0.05f, 0.40f, 0.06f, Color.Black * 0.22f, MathF.PI / 2f); // side shadow
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.10f, 0.42f, 0.13f, band); // identifying colour band
    }
}
