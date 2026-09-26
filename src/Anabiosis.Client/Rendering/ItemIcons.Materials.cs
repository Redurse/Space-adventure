using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// Direct user request ("добавим множество предметов материалов... иконки буквально почти схоже как
// в баротравме") - 10 raw ores/elements (OreDeposit.OreType, World.Cutting.cs) plus the 5 refined
// materials FabricatorCatalog.cs crafts from a subset of them. "Like Barotrauma" means the same
// VISUAL LANGUAGE that game's own item icons use (a raw ore reads as an irregular rock chunk with a
// couple of bright facets, tinted by what it actually is; a refined metal reads as a flat rolled
// plate; a spool of cable looks like a spool) - not its actual copyrighted art, which this project
// has none of anywhere (ItemIcons.cs's own doc comment) and never will.
public static partial class ItemIcons
{
    // Direct user request ("сделай всем рудам текстуру приближенно как на 1 скрине почти точь в
    // точь") - a small scattered PILE of 3 chunks (one bigger, centered-back; two smaller, front-
    // left/front-right) rather than one single rock, matching the reference screenshot's own
    // cluttered-scrap-pile read rather than a single gemstone silhouette. Only the 3 colours change
    // between ore kinds, same "one shared shape, re-tinted" idea as before.
    private static void DrawOreChunk(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color rock, Color facet, Color dark)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);

        DrawChunkPiece(spriteBatch, pixel, origin + new Vector2(0.02f, -0.10f) * scale, scale * 0.60f, 0.15f, rock, facet, dark);
        DrawChunkPiece(spriteBatch, pixel, origin + new Vector2(-0.30f, 0.22f) * scale, scale * 0.44f, -0.35f, rock * 0.92f, facet, dark);
        DrawChunkPiece(spriteBatch, pixel, origin + new Vector2(0.28f, 0.24f) * scale, scale * 0.42f, 0.55f, rock * 0.96f, facet, dark);
    }

    // One small irregular 6-point chunk, rotated/scaled/placed independently - DrawOreChunk's own
    // building block, three of which make up the pile.
    private static void DrawChunkPiece(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, float rotation, Color rock, Color facet, Color dark)
    {
        Vector2 P(float x, float y)
        {
            var cos = MathF.Cos(rotation);
            var sin = MathF.Sin(rotation);
            return center + new Vector2(x * cos - y * sin, x * sin + y * cos) * scale;
        }

        var points = new[] { P(-0.10f, -0.42f), P(0.30f, -0.28f), P(0.38f, 0.12f), P(0.08f, 0.40f), P(-0.36f, 0.24f), P(-0.38f, -0.14f) };
        Primitives.FillPolygon(spriteBatch, pixel, center, points, rock);
        Primitives.FillTriangle(spriteBatch, pixel, center, points[5], points[0], facet * 0.85f); // one bright facet
        Primitives.FillTriangle(spriteBatch, pixel, center, points[0], points[1], facet);
        Primitives.StrokePolygon(spriteBatch, pixel, points, dark, MathF.Max(1f, scale * 0.045f));
    }

    // Uranium ore alone gets a glowing fleck on top of the ordinary chunk shape - the one raw
    // material that's supposed to read as faintly dangerous/radioactive on sight.
    private static void DrawUraniumOre(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        DrawOreChunk(spriteBatch, pixel, rect, new Color(90, 110, 70), new Color(150, 210, 100), new Color(45, 58, 35));
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        Circle(spriteBatch, pixel, origin, 0f, scale, -0.05f, 0.06f, 0.065f, new Color(170, 255, 120) * 0.85f);
        Circle(spriteBatch, pixel, origin, 0f, scale, -0.05f, 0.06f, 0.13f, new Color(170, 255, 120) * 0.28f); // soft glow halo
    }

    private static void DrawSteelPlate(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var plate = new Color(150, 154, 160);
        var shade = new Color(102, 106, 112);
        var rivet = new Color(70, 72, 78);

        GroundShadow(spriteBatch, pixel, origin, scale, 0.30f, 0.36f);
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.80f, 0.50f, plate); // plate body
        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.16f, 0.74f, 0.10f, Color.White * 0.22f); // rolled sheen
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.18f, 0.74f, 0.08f, shade); // bottom shade lip
        Circle(spriteBatch, pixel, origin, a, scale, -0.30f, -0.14f, 0.045f, rivet);
        Circle(spriteBatch, pixel, origin, a, scale, 0.30f, -0.14f, 0.045f, rivet);
        Circle(spriteBatch, pixel, origin, a, scale, -0.30f, 0.16f, 0.045f, rivet);
        Circle(spriteBatch, pixel, origin, a, scale, 0.30f, 0.16f, 0.045f, rivet);
    }

    // Same plate silhouette as steel, lighter and cooler-toned (titanium's own real-world look) with
    // one extra edge highlight so it doesn't just read as "steel plate but paler".
    private static void DrawTitaniumPlate(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var plate = new Color(196, 200, 206);
        var shade = new Color(140, 146, 154);
        var rivet = new Color(90, 96, 104);

        GroundShadow(spriteBatch, pixel, origin, scale, 0.30f, 0.36f);
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.80f, 0.50f, plate);
        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.16f, 0.74f, 0.10f, Color.White * 0.35f); // brighter rolled sheen
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.18f, 0.74f, 0.08f, shade);
        Bar(spriteBatch, pixel, origin, a, scale, -0.34f, 0f, 0.04f, 0.44f, Color.White * 0.4f); // edge glint
        Circle(spriteBatch, pixel, origin, a, scale, -0.30f, -0.14f, 0.045f, rivet);
        Circle(spriteBatch, pixel, origin, a, scale, 0.30f, -0.14f, 0.045f, rivet);
        Circle(spriteBatch, pixel, origin, a, scale, -0.30f, 0.16f, 0.045f, rivet);
        Circle(spriteBatch, pixel, origin, a, scale, 0.30f, 0.16f, 0.045f, rivet);
    }

    // DrawWireSpool's own exact shape, just copper-toned rather than the generic wiring-spool's
    // orange/gold - a coil of finished cable, not raw ore.
    private static void DrawCopperCable(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var reel = new Color(180, 120, 60);
        var wire = new Color(224, 130, 70);
        var core = new Color(60, 45, 35);

        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.40f, reel);
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.30f, 0f, 360f, wire, 0.09f, 20);
        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.13f, core);
        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.05f, Color.Black * 0.4f);
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.40f, -150f, -60f, Color.White * 0.3f, 0.03f, 8);
    }

    // A soft, slightly translucent pellet rather than a hard-edged chunk - reads as a synthetic
    // material, not a mined one.
    private static void DrawPlastic(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var body = new Color(225, 225, 220) * 0.92f;
        var shade = new Color(180, 180, 176) * 0.92f;

        GroundShadow(spriteBatch, pixel, origin, scale, 0.30f, 0.30f);
        Circle(spriteBatch, pixel, origin, a, scale, -0.10f, 0.02f, 0.26f, body);
        Circle(spriteBatch, pixel, origin, a, scale, 0.16f, 0.08f, 0.20f, body);
        Circle(spriteBatch, pixel, origin, a, scale, 0.10f, 0.20f, 0.14f, shade);
        RingArc(spriteBatch, pixel, origin, a, scale, -0.16f, -0.08f, 0.10f, 0f, 360f, Color.White * 0.5f, 0.03f, 8); // glossy highlight
    }

    // A cast ingot bar - a rolled plate would look identical to steel/titanium at a glance, so
    // plastalloy instead reads as a trapezoid-profile bar with a metallic-purple sheen, the way a
    // cast (not rolled) exotic alloy would.
    private static void DrawPlastalloy(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var body = new Color(130, 100, 175);
        var dark = new Color(80, 58, 115);
        var sheen = new Color(200, 175, 230);

        GroundShadow(spriteBatch, pixel, origin, scale, 0.28f, 0.34f);
        var points = new[]
        {
            origin + new Vector2(-0.38f, 0.20f) * scale,
            origin + new Vector2(-0.24f, -0.20f) * scale,
            origin + new Vector2(0.24f, -0.20f) * scale,
            origin + new Vector2(0.38f, 0.20f) * scale,
        };
        Primitives.FillPolygon(spriteBatch, pixel, origin, points, body);
        Primitives.StrokePolygon(spriteBatch, pixel, points, dark, 1.6f);
        Bar(spriteBatch, pixel, origin, 0f, scale, -0.06f, -0.12f, 0.44f, 0.05f, sheen); // top-face sheen band
    }

    // Every raw ore that only needs DrawOreChunk's shared shape - Uranium is the one exception with
    // its own glow (DrawUraniumOre above).
    public static bool HasMaterialIcon(ItemType type) => type is ItemType.IronOre or ItemType.NickelOre
        or ItemType.ZincOre or ItemType.CopperOre or ItemType.TitaniumOre or ItemType.UraniumOre
        or ItemType.Silicon or ItemType.Carbon or ItemType.AluminumOre or ItemType.PlastalloyOre
        or ItemType.SteelPlate or ItemType.TitaniumPlate or ItemType.CopperCable or ItemType.Plastic or ItemType.Plastalloy;

    private static bool DrawMaterialIcon(SpriteBatch spriteBatch, Texture2D pixel, ItemType type, Rectangle rect)
    {
        switch (type)
        {
            case ItemType.IronOre: DrawOreChunk(spriteBatch, pixel, rect, new Color(140, 100, 80), new Color(195, 145, 115), new Color(90, 60, 45)); return true;
            case ItemType.NickelOre: DrawOreChunk(spriteBatch, pixel, rect, new Color(148, 152, 142), new Color(195, 200, 185), new Color(92, 96, 86)); return true;
            case ItemType.ZincOre: DrawOreChunk(spriteBatch, pixel, rect, new Color(168, 175, 186), new Color(212, 218, 226), new Color(108, 114, 122)); return true;
            case ItemType.CopperOre: DrawOreChunk(spriteBatch, pixel, rect, new Color(182, 112, 62), new Color(224, 154, 94), new Color(120, 70, 36)); return true;
            case ItemType.TitaniumOre: DrawOreChunk(spriteBatch, pixel, rect, new Color(150, 156, 162), new Color(202, 207, 212), new Color(94, 100, 106)); return true;
            case ItemType.UraniumOre: DrawUraniumOre(spriteBatch, pixel, rect); return true;
            case ItemType.Silicon: DrawOreChunk(spriteBatch, pixel, rect, new Color(58, 60, 72), new Color(112, 112, 132), new Color(28, 28, 38)); return true;
            case ItemType.Carbon: DrawOreChunk(spriteBatch, pixel, rect, new Color(34, 34, 38), new Color(72, 72, 78), new Color(14, 14, 16)); return true;
            case ItemType.AluminumOre: DrawOreChunk(spriteBatch, pixel, rect, new Color(196, 199, 204), new Color(232, 234, 237), new Color(140, 142, 146)); return true;
            case ItemType.PlastalloyOre: DrawOreChunk(spriteBatch, pixel, rect, new Color(120, 90, 160), new Color(172, 140, 212), new Color(74, 54, 104)); return true;
            case ItemType.SteelPlate: DrawSteelPlate(spriteBatch, pixel, rect); return true;
            case ItemType.TitaniumPlate: DrawTitaniumPlate(spriteBatch, pixel, rect); return true;
            case ItemType.CopperCable: DrawCopperCable(spriteBatch, pixel, rect); return true;
            case ItemType.Plastic: DrawPlastic(spriteBatch, pixel, rect); return true;
            case ItemType.Plastalloy: DrawPlastalloy(spriteBatch, pixel, rect); return true;
            default: return false;
        }
    }
}
