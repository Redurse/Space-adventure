using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// Recognizable silhouettes for every carryable item type - this project has no image assets, so
// each is built from rotated bars, circles and ring arcs (HudIcons' own primitives) in the same
// single-pixel style as everything else, angled like a held tool rather than drawn flat. Split
// across three files by family: this one plus tools/tanks here, ItemIcons.Gear.cs for weapons/
// consumables/raw goods, ItemIcons.Components.cs for the 14 purchasable wiring parts (one shared
// "chip" template with a per-kind glyph, since they're all small electronics in the same family,
// not 14 unrelated objects). Anything not covered falls back to InventoryPanel's plain coloured
// square + short label, which is why callers check HasIcon first.
public static partial class ItemIcons
{
    // Tools are drawn tilted nose-up-right, the way a held tool actually reads at a glance, rather
    // than dead flat in the slot.
    private const float ToolTilt = -0.58f;

    public static bool HasIcon(ItemType type) => type is ItemType.Wrench or ItemType.Screwdriver
        or ItemType.WeldingTool or ItemType.Cutter or ItemType.OxygenTank or ItemType.WeldingTank
        or ItemType.AmmoCrate or ItemType.Spacesuit or ItemType.Knife or ItemType.Rifle or ItemType.LaserRifle
        or ItemType.FuelRod or ItemType.MedKit or ItemType.WireSpool or ItemType.Mineral
        or ItemType.GateAnd or ItemType.GateOr or ItemType.GateNot or ItemType.GateXor
        or ItemType.Timer or ItemType.Memory or ItemType.Relay
        or ItemType.OxygenSensor or ItemType.BreachSensor or ItemType.PowerLossSensor or ItemType.MotionSensor
        or ItemType.AutoDoorController or ItemType.AlarmKlaxon or ItemType.LightToggle;

    // rotation: the angle to actually draw the tool at, in the same frame ShipRenderer's
    // HeldToolOffset/facing already uses - null keeps the fixed "as if held" tilt every inventory
    // slot/dragged-item draw wants (there's no facing to speak of sitting in a hotbar). The in-world
    // held-item chip passes the character's real facing instead, so the tool visibly turns to point
    // where they're aiming rather than always reading the same way on screen.
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, ItemType type, Rectangle rect, float? rotation = null)
    {
        var angle = rotation ?? ToolTilt;
        if (ComponentDefinitions.ComponentKindFor(type) is { } componentKind)
        {
            DrawComponentChip(spriteBatch, pixel, rect, componentKind);
            return;
        }

        switch (type)
        {
            case ItemType.Screwdriver: DrawScrewdriver(spriteBatch, pixel, rect, angle); break;
            case ItemType.Wrench: DrawWrench(spriteBatch, pixel, rect, angle); break;
            // Tank/nozzle tinted the same colour each tool's own flame already is
            // (FieldRenderer.DrawWeldingFlame/DrawCuttingFlame) - the icon and the beam it fires
            // read as the same tool.
            case ItemType.WeldingTool: DrawGunTool(spriteBatch, pixel, rect, angle, new Color(214, 130, 40), new Color(255, 170, 60)); break;
            case ItemType.Cutter: DrawGunTool(spriteBatch, pixel, rect, angle, new Color(70, 150, 90), new Color(110, 200, 255)); break;
            case ItemType.OxygenTank: DrawTank(spriteBatch, pixel, rect, new Color(196, 202, 210), new Color(70, 140, 200)); break;
            case ItemType.WeldingTank: DrawTank(spriteBatch, pixel, rect, new Color(170, 152, 92), new Color(214, 90, 40)); break;
            case ItemType.AmmoCrate: DrawAmmoCrate(spriteBatch, pixel, rect); break;
            case ItemType.Spacesuit: DrawSpacesuit(spriteBatch, pixel, rect); break;
            case ItemType.Knife: DrawKnife(spriteBatch, pixel, rect, angle); break;
            case ItemType.Rifle: DrawRifle(spriteBatch, pixel, rect, angle, laser: false); break;
            case ItemType.LaserRifle: DrawRifle(spriteBatch, pixel, rect, angle, laser: true); break;
            case ItemType.FuelRod: DrawFuelRod(spriteBatch, pixel, rect); break;
            case ItemType.MedKit: DrawMedKit(spriteBatch, pixel, rect); break;
            case ItemType.WireSpool: DrawWireSpool(spriteBatch, pixel, rect); break;
            case ItemType.Mineral: DrawMineral(spriteBatch, pixel, rect); break;
        }
    }

    // Where DrawGunTool's own muzzle sits for the exact rect/rotation it would be drawn with - so a
    // tool's flame can start precisely at its texture's barrel tip instead of a separately-computed
    // offset that only approximately lines up with it. Null for anything that isn't a gun tool.
    public static Vector2? GetMuzzleWorldPosition(ItemType type, Rectangle rect, float rotation)
    {
        if (type is not (ItemType.WeldingTool or ItemType.Cutter))
            return null;
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        return Point(origin, rotation, scale, 0.47f, 0f);
    }

    // A simplified top-down silhouette for the two gun tools - what you'd actually see looking
    // straight down at a held cutter/welder (no side-view grip/trigger guard, since those are hidden
    // under the hand from directly above): a barrel pointing along `rotation`, a tank straddling it,
    // and a glowing muzzle. Used for the in-world held-item display, which - unlike the side-view
    // Draw() above (still used by the hotbar/dragged-item, where a "held in hand" side profile is
    // the established look) - draws no backdrop or hand glyphs of its own around these two.
    public static void DrawGunToolTopDown(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, float rotation, float scale, ItemType type)
    {
        var (tank, nozzle) = type == ItemType.Cutter
            ? (new Color(70, 150, 90), new Color(110, 200, 255))
            : (new Color(214, 130, 40), new Color(255, 170, 60));
        var metal = new Color(96, 100, 110);
        var dark = new Color(38, 38, 44);

        Bar(spriteBatch, pixel, origin, rotation, scale, -0.12f, 0f, 0.46f, 0.20f, metal); // body
        Bar(spriteBatch, pixel, origin, rotation, scale, -0.12f, -0.055f, 0.38f, 0.045f, Color.White * 0.22f); // highlight
        Bar(spriteBatch, pixel, origin, rotation, scale, 0.08f, 0f, 0.30f, 0.30f, tank); // tank straddling the body
        Circle(spriteBatch, pixel, origin, rotation, scale, 0.08f, -0.10f, 0.032f, dark); // valve knob
        RingArc(spriteBatch, pixel, origin, rotation, scale, 0.08f, -0.10f, 0.032f, 0f, 360f, Color.White * 0.3f, 0.01f, 8);
        Bar(spriteBatch, pixel, origin, rotation, scale, 0.34f, 0f, 0.26f, 0.115f, new Color(64, 68, 78)); // barrel
        Circle(spriteBatch, pixel, origin, rotation, scale, 0.5f, 0f, 0.075f, nozzle); // muzzle
        RingArc(spriteBatch, pixel, origin, rotation, scale, 0.5f, 0f, 0.075f, 0f, 360f, Color.White * 0.5f, 0.016f, 12); // muzzle rim
    }

    // Companion to DrawGunToolTopDown - the exact same alongAxis its own muzzle circle sits at.
    public static Vector2 GetTopDownMuzzleWorldPosition(Vector2 origin, float rotation, float scale) =>
        Point(origin, rotation, scale, 0.5f, 0f);

    // Every part of a tool is placed in its own local frame - `alongAxis` runs along the tilt,
    // `acrossAxis` perpendicular to it, both fractions of `scale` - so the whole tool rotates and
    // scales together as one rigid body instead of being placed in absolute pixels.
    private static Vector2 Point(Vector2 origin, float baseAngle, float scale, float alongAxis, float acrossAxis)
    {
        var cos = MathF.Cos(baseAngle);
        var sin = MathF.Sin(baseAngle);
        var x = alongAxis * scale;
        var y = acrossAxis * scale;
        return origin + new Vector2(x * cos - y * sin, x * sin + y * cos);
    }

    // The whole gallery of tools is designed against a comfortably large canvas, but the actual
    // destination is a ~30px hotbar slot - a fraction like "0.02 of scale" that reads fine at design
    // size rounds down to a fraction of a screen pixel there and disappears into mush. Every bar and
    // ring line is floored to this many actual screen pixels regardless of what its own fraction
    // would otherwise give, so fine linework stays legible instead of sub-pixel.
    private const float MinPixels = 1.6f;
    private const float MinRadiusPixels = 1.1f;

    private static void Bar(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, float baseAngle, float scale,
        float alongAxis, float acrossAxis, float length, float thickness, Color color, float extraRotation = 0f)
    {
        var world = Point(origin, baseAngle, scale, alongAxis, acrossAxis);
        var thicknessPixels = MathF.Max(thickness * scale, MinPixels);
        spriteBatch.Draw(pixel, world, null, color, baseAngle + extraRotation, new Vector2(0.5f, 0.5f),
            new Vector2(MathF.Max(length * scale, thicknessPixels), thicknessPixels), SpriteEffects.None, 0f);
    }

    private static void Circle(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, float baseAngle, float scale,
        float alongAxis, float acrossAxis, float radius, Color color) =>
        HudIcons.FillCircle(spriteBatch, pixel, Point(origin, baseAngle, scale, alongAxis, acrossAxis),
            MathF.Max(radius * scale, MinRadiusPixels), color);

    // A ring/arc - startDegrees/endDegrees are measured in the tool's own rotated frame (0 points
    // toward the nose), so a partial arc (a trigger guard's opening, a carry handle's gap) turns
    // together with the tool instead of always facing the same way on screen.
    private static void RingArc(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, float baseAngle, float scale,
        float alongAxis, float acrossAxis, float radius, float startDegrees, float endDegrees, Color color, float thickness, int segments = 12)
    {
        var tiltDegrees = baseAngle * (180f / MathF.PI);
        HudIcons.DrawRingArc(spriteBatch, pixel, Point(origin, baseAngle, scale, alongAxis, acrossAxis),
            MathF.Max(radius * scale, MinRadiusPixels), startDegrees + tiltDegrees, endDegrees + tiltDegrees, color, segments,
            MathF.Max(thickness * scale, MinPixels));
    }

    private static void DrawScrewdriver(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float a)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var handle = new Color(206, 62, 36);
        var handleDark = new Color(150, 40, 22);

        // A clearly oblong handle - one bar noticeably longer than it is thick, capped with a
        // rounded end each side - rather than round ends big enough to read as a ball on their own.
        Bar(spriteBatch, pixel, origin, a, scale, -0.29f, 0f, 0.38f, 0.22f, handle);
        Circle(spriteBatch, pixel, origin, a, scale, -0.48f, 0f, 0.11f, handleDark); // back cap
        Circle(spriteBatch, pixel, origin, a, scale, -0.10f, 0f, 0.10f, handle); // front cap
        Bar(spriteBatch, pixel, origin, a, scale, -0.32f, -0.065f, 0.28f, 0.045f, Color.White * 0.3f); // highlight

        // Two moulded grip rings crossing the handle, sized to sit on it rather than bulge past it.
        RingArc(spriteBatch, pixel, origin, a, scale, -0.34f, 0f, 0.105f, 0f, 360f, Color.Black * 0.3f, 0.022f, 16);
        RingArc(spriteBatch, pixel, origin, a, scale, -0.20f, 0f, 0.095f, 0f, 360f, Color.Black * 0.28f, 0.02f, 16);

        Bar(spriteBatch, pixel, origin, a, scale, -0.02f, 0f, 0.09f, 0.15f, new Color(40, 40, 44)); // ferrule
        Bar(spriteBatch, pixel, origin, a, scale, 0.24f, 0f, 0.48f, 0.078f, new Color(214, 217, 222)); // shaft
        Bar(spriteBatch, pixel, origin, a, scale, 0.20f, -0.018f, 0.36f, 0.026f, Color.White * 0.55f); // shaft highlight
        Circle(spriteBatch, pixel, origin, a, scale, 0.48f, 0f, 0.045f, Color.White * 0.9f); // tip
    }

    // An adjustable spanner, not a ring-and-open-end combination wrench: a plain rounded handle,
    // a shaft, and a wide head with a jaw notch cut into the front plus the little knurled wheel
    // that winds the jaw open and shut - the shape Barotrauma's own wrench tool reads as.
    private static void DrawWrench(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float a)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var metal = new Color(198, 202, 208);
        var shade = new Color(116, 120, 128);
        var dark = new Color(40, 40, 46);

        // Plain rounded handle at the back, with a small hanging hole rather than a full ring.
        Circle(spriteBatch, pixel, origin, a, scale, -0.42f, 0f, 0.135f, metal);
        Bar(spriteBatch, pixel, origin, a, scale, -0.27f, 0f, 0.32f, 0.24f, metal);
        Bar(spriteBatch, pixel, origin, a, scale, -0.30f, -0.055f, 0.24f, 0.05f, Color.White * 0.35f); // highlight
        Circle(spriteBatch, pixel, origin, a, scale, -0.42f, 0f, 0.05f, dark); // hanging hole
        RingArc(spriteBatch, pixel, origin, a, scale, -0.42f, 0f, 0.05f, 0f, 360f, Color.Black * 0.3f, 0.014f, 10);

        Bar(spriteBatch, pixel, origin, a, scale, 0.06f, 0f, 0.36f, 0.13f, metal); // shaft
        Bar(spriteBatch, pixel, origin, a, scale, 0.04f, -0.035f, 0.32f, 0.03f, Color.White * 0.4f); // shaft highlight

        // The adjustable head: one big block, a wide dark notch bitten out of its front face, and
        // two jaws either side of the notch reaching well past the block's own edge - kept large and
        // uncluttered rather than several small shapes crammed together, so the "open mouth" is
        // unmistakable even shrunk down to a 30px slot.
        Bar(spriteBatch, pixel, origin, a, scale, 0.34f, 0f, 0.30f, 0.32f, metal); // head block
        Bar(spriteBatch, pixel, origin, a, scale, 0.30f, -0.09f, 0.22f, 0.06f, Color.White * 0.3f); // head highlight
        Bar(spriteBatch, pixel, origin, a, scale, 0.50f, 0f, 0.20f, 0.15f, dark); // jaw notch (bitten out)
        Bar(spriteBatch, pixel, origin, a, scale, 0.44f, -0.115f, 0.28f, 0.10f, metal); // fixed jaw (upper)
        Bar(spriteBatch, pixel, origin, a, scale, 0.44f, 0.135f, 0.28f, 0.09f, shade); // movable jaw (lower, shadowed)

        // The knurled adjustment wheel, clear of the jaw entirely so it doesn't merge into it.
        Circle(spriteBatch, pixel, origin, a, scale, 0.20f, 0.19f, 0.065f, dark);
        RingArc(spriteBatch, pixel, origin, a, scale, 0.20f, 0.19f, 0.065f, 0f, 360f, shade, 0.016f, 10);
    }

    // Shared silhouette for the welder and the cutter - a gripped tool, barrel out front, a tank
    // mounted on top and a hot tip at the muzzle - differing only in colour (which tool, which flame).
    private static void DrawGunTool(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float a, Color tank, Color nozzle)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var metal = new Color(96, 100, 110);
        var dark = new Color(38, 38, 44);

        // Front grip, angled down from the body with a rounded heel rather than a flat-cut end.
        Bar(spriteBatch, pixel, origin, a, scale, -0.22f, 0.20f, 0.24f, 0.155f, dark, 1.15f);
        Circle(spriteBatch, pixel, origin, a, scale, -0.30f, 0.35f, 0.078f, dark);
        Bar(spriteBatch, pixel, origin, a, scale, -0.24f, 0.15f, 0.15f, 0.04f, Color.White * 0.12f, 1.15f); // grip highlight

        // A second handle at the tail, in line with the body rather than hanging below it - both
        // hands needed to hold the tool up (ItemDefinitions.HandsRequired), the rear one steadying it
        // the way a rifle's stock does. Shaped the same way the front grip is (a bar plus a rounded
        // end cap), not a bare circle, so it actually reads as a handle.
        Bar(spriteBatch, pixel, origin, a, scale, -0.42f, 0.05f, 0.22f, 0.135f, dark);
        Circle(spriteBatch, pixel, origin, a, scale, -0.53f, 0.09f, 0.072f, dark); // rounded butt
        Bar(spriteBatch, pixel, origin, a, scale, -0.44f, 0.01f, 0.16f, 0.035f, Color.White * 0.15f); // handle highlight

        Bar(spriteBatch, pixel, origin, a, scale, -0.06f, 0f, 0.36f, 0.27f, metal); // body/receiver
        Bar(spriteBatch, pixel, origin, a, scale, -0.06f, -0.08f, 0.30f, 0.06f, Color.White * 0.22f); // top highlight
        Circle(spriteBatch, pixel, origin, a, scale, -0.20f, 0.05f, 0.035f, Color.Black * 0.4f); // rivet
        Circle(spriteBatch, pixel, origin, a, scale, 0.06f, 0.05f, 0.035f, Color.Black * 0.4f); // rivet

        // A trigger guard as an actual open ring rather than a filled rectangle.
        RingArc(spriteBatch, pixel, origin, a, scale, 0.00f, 0.16f, 0.09f, 20f, 340f, dark, 0.035f, 10);

        // Tank mounted on top, capped at both ends like a real cylinder instead of a flat-topped box.
        Bar(spriteBatch, pixel, origin, a, scale, -0.02f, -0.24f, 0.22f, 0.19f, tank);
        Circle(spriteBatch, pixel, origin, a, scale, -0.13f, -0.24f, 0.095f, tank);
        Circle(spriteBatch, pixel, origin, a, scale, 0.09f, -0.24f, 0.095f, tank);
        Circle(spriteBatch, pixel, origin, a, scale, 0.02f, -0.31f, 0.035f, dark); // valve knob
        RingArc(spriteBatch, pixel, origin, a, scale, -0.02f, -0.24f, 0.088f, -150f, -40f, Color.White * 0.45f, 0.025f, 8); // sheen

        Bar(spriteBatch, pixel, origin, a, scale, 0.30f, 0f, 0.28f, 0.115f, new Color(64, 68, 78)); // barrel
        Bar(spriteBatch, pixel, origin, a, scale, 0.30f, -0.05f, 0.24f, 0.02f, Color.White * 0.3f); // barrel highlight
        Circle(spriteBatch, pixel, origin, a, scale, 0.47f, 0f, 0.078f, nozzle); // hot tip, rounded
        RingArc(spriteBatch, pixel, origin, a, scale, 0.47f, 0f, 0.078f, 0f, 360f, Color.White * 0.5f, 0.018f, 12); // tip rim
    }

    private static void DrawTank(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color body, Color band)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f; // tanks stand upright rather than tilt like a held tool
        const float halfWidth = 0.21f;

        // A true rounded cylinder: a bar for the straight run plus a circular cap at each end,
        // instead of a flat-topped rectangle.
        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.05f, 0.62f, halfWidth * 2f, body, MathF.PI / 2f);
        Circle(spriteBatch, pixel, origin, a, scale, 0f, -0.36f, halfWidth, body);
        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0.26f, halfWidth, body);

        Bar(spriteBatch, pixel, origin, a, scale, -0.09f, -0.05f, 0.40f, 0.07f, Color.White * 0.28f, MathF.PI / 2f); // side highlight
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, -0.36f, halfWidth * 0.8f, 130f, 230f, Color.White * 0.3f, 0.025f, 8); // cap sheen
        Bar(spriteBatch, pixel, origin, a, scale, 0.13f, -0.05f, 0.40f, 0.05f, Color.Black * 0.22f, MathF.PI / 2f); // side shadow

        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.10f, 0.42f, 0.13f, band); // identifying colour band

        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.42f, 0.16f, 0.10f, new Color(48, 48, 54)); // valve body
        Circle(spriteBatch, pixel, origin, a, scale, 0f, -0.47f, 0.05f, new Color(60, 60, 66)); // valve knob

        // A carrying handle - a partial ring standing off the top of the tank, open at the bottom.
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, -0.50f, 0.10f, 200f, 340f, new Color(70, 70, 78), 0.028f, 10);
    }
}
