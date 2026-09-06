using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The 14 purchasable wiring parts (ComponentKind, World.ComponentMounts.cs) - drawn as one shared
// "chip" template (a bevelled body with two pin nubs, the same fill/border colours
// ComponentRenderer.CategoryColors already tints the *installed* version with) plus a small glyph
// per kind, rather than 14 unrelated objects - they really are all small electronics in the same
// family, the way real ICs share a package and differ only in the symbol printed on it.
public static partial class ItemIcons
{
    private static void DrawComponentChip(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, ComponentKind kind)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var (fill, border) = ChipColors(kind);
        var pin = new Color(60, 60, 64);

        Bar(spriteBatch, pixel, origin, 0f, scale, 0f, 0f, 0.66f, 0.66f, border); // border
        Bar(spriteBatch, pixel, origin, 0f, scale, 0f, 0f, 0.58f, 0.58f, fill); // face
        Bar(spriteBatch, pixel, origin, 0f, scale, 0f, -0.20f, 0.46f, 0.05f, Color.White * 0.18f); // highlight
        Bar(spriteBatch, pixel, origin, 0f, scale, -0.36f, 0f, 0.14f, 0.09f, pin); // left pin
        Bar(spriteBatch, pixel, origin, 0f, scale, 0.36f, 0f, 0.14f, 0.09f, pin); // right pin

        DrawComponentGlyph(spriteBatch, pixel, origin, scale, kind, Color.White * 0.92f);
    }

    // Same category grouping/colours as ComponentRenderer.CategoryColors, just as a fill+border pair
    // instead of a single translucent tint - the inventory icon and the panel it plugs into read as
    // the same part.
    private static (Color Fill, Color Border) ChipColors(ComponentKind kind) => kind switch
    {
        ComponentKind.GateAnd or ComponentKind.GateOr or ComponentKind.GateNot or ComponentKind.GateXor
            => (new Color(70, 70, 130), new Color(130, 130, 210)),
        ComponentKind.Timer or ComponentKind.Memory => (new Color(90, 55, 110), new Color(170, 115, 200)),
        ComponentKind.Relay => (new Color(120, 92, 30), new Color(215, 175, 55)),
        ComponentKind.OxygenSensor or ComponentKind.BreachSensor or ComponentKind.PowerLossSensor or ComponentKind.MotionSensor
            => (new Color(35, 95, 70), new Color(95, 185, 145)),
        ComponentKind.AutoDoorController or ComponentKind.AlarmKlaxon or ComponentKind.LightToggle
            => (new Color(140, 85, 25), new Color(225, 170, 65)),
        _ => (new Color(70, 70, 74), new Color(145, 145, 150)),
    };

    private static void DrawComponentGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, float scale, ComponentKind kind, Color color)
    {
        switch (kind)
        {
            case ComponentKind.GateAnd:
                Bar(spriteBatch, pixel, origin, 0f, scale, -0.05f, 0f, 0.18f, 0.20f, color); // flat back
                Circle(spriteBatch, pixel, origin, 0f, scale, 0.04f, 0f, 0.10f, color); // rounded front
                break;

            case ComponentKind.GateOr:
            case ComponentKind.GateNot:
            case ComponentKind.GateXor:
                DrawGateTriangle(spriteBatch, pixel, origin, scale, color);
                if (kind == ComponentKind.GateXor)
                    RingArc(spriteBatch, pixel, origin, 0f, scale, -0.15f, 0f, 0.10f, -75f, 75f, color, 0.025f, 8); // extra curve behind
                if (kind == ComponentKind.GateNot)
                    Circle(spriteBatch, pixel, origin, 0f, scale, 0.17f, 0f, 0.035f, color); // inverter bubble
                break;

            case ComponentKind.Timer:
                RingArc(spriteBatch, pixel, origin, 0f, scale, 0f, 0f, 0.15f, 0f, 360f, color, 0.025f, 14); // clock face
                Bar(spriteBatch, pixel, origin, 0f, scale, 0f, -0.055f, 0.02f, 0.11f, color); // minute hand
                Bar(spriteBatch, pixel, origin, 0f, scale, 0.035f, 0f, 0.02f, 0.09f, color, MathF.PI / 2f); // hour hand
                break;

            case ComponentKind.Memory:
                Bar(spriteBatch, pixel, origin, 0f, scale, 0f, 0f, 0.24f, 0.22f, color); // latch body
                Bar(spriteBatch, pixel, origin, 0f, scale, 0f, 0f, 0.18f, 0.16f, Color.Black * 0.25f); // inset
                Bar(spriteBatch, pixel, origin, 0f, scale, 0f, 0f, 0.20f, 0.02f, Color.White * 0.5f); // divider
                break;

            case ComponentKind.Relay:
                Bar(spriteBatch, pixel, origin, 0f, scale, 0f, 0.02f, 0.20f, 0.11f, color); // switch base
                Bar(spriteBatch, pixel, origin, 0f, scale, 0.02f, -0.03f, 0.13f, 0.03f, Color.White * 0.65f, -0.5f); // lever
                break;

            case ComponentKind.OxygenSensor:
                Circle(spriteBatch, pixel, origin, 0f, scale, 0f, 0.03f, 0.11f, color); // droplet body
                Primitives.FillTriangle(spriteBatch, pixel, Point(origin, 0f, scale, 0f, -0.17f),
                    Point(origin, 0f, scale, -0.07f, -0.02f), Point(origin, 0f, scale, 0.07f, -0.02f), color); // droplet tip
                break;

            case ComponentKind.BreachSensor:
                Primitives.FillTriangle(spriteBatch, pixel, Point(origin, 0f, scale, 0f, -0.15f),
                    Point(origin, 0f, scale, -0.14f, 0.11f), Point(origin, 0f, scale, 0.14f, 0.11f), color); // warning triangle
                Bar(spriteBatch, pixel, origin, 0f, scale, 0f, -0.005f, 0.028f, 0.11f, new Color(30, 30, 34)); // "!" stem
                Circle(spriteBatch, pixel, origin, 0f, scale, 0f, 0.085f, 0.018f, new Color(30, 30, 34)); // "!" dot
                break;

            case ComponentKind.PowerLossSensor:
                Bar(spriteBatch, pixel, origin, 0f, scale, -0.03f, -0.06f, 0.15f, 0.035f, color, 0.9f); // bolt, upper stroke
                Bar(spriteBatch, pixel, origin, 0f, scale, 0.03f, 0.06f, 0.15f, 0.035f, color, 0.9f); // bolt, lower stroke
                break;

            case ComponentKind.MotionSensor:
                RingArc(spriteBatch, pixel, origin, 0f, scale, 0f, 0.06f, 0.06f, -60f, 60f, color, 0.03f, 6); // radar waves
                RingArc(spriteBatch, pixel, origin, 0f, scale, 0f, 0.06f, 0.12f, -60f, 60f, color, 0.026f, 8);
                RingArc(spriteBatch, pixel, origin, 0f, scale, 0f, 0.06f, 0.18f, -60f, 60f, color, 0.022f, 10);
                break;

            case ComponentKind.AutoDoorController:
                Bar(spriteBatch, pixel, origin, 0f, scale, -0.07f, 0f, 0.10f, 0.22f, color); // left door leaf
                Bar(spriteBatch, pixel, origin, 0f, scale, 0.07f, 0f, 0.10f, 0.22f, color); // right door leaf
                break;

            case ComponentKind.AlarmKlaxon:
                Primitives.FillTriangle(spriteBatch, pixel, Point(origin, 0f, scale, 0.15f, 0f),
                    Point(origin, 0f, scale, -0.10f, -0.11f), Point(origin, 0f, scale, -0.10f, 0.11f), color); // speaker cone
                Bar(spriteBatch, pixel, origin, 0f, scale, -0.15f, 0f, 0.06f, 0.11f, color); // base cap
                break;

            case ComponentKind.LightToggle:
                Circle(spriteBatch, pixel, origin, 0f, scale, 0f, -0.05f, 0.10f, color); // bulb
                Bar(spriteBatch, pixel, origin, 0f, scale, 0f, 0.08f, 0.07f, 0.07f, color); // base
                break;
        }
    }

    private static void DrawGateTriangle(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, float scale, Color color)
    {
        var apex = Point(origin, 0f, scale, 0.13f, 0f);
        var top = Point(origin, 0f, scale, -0.09f, -0.12f);
        var bottom = Point(origin, 0f, scale, -0.09f, 0.12f);
        Primitives.FillTriangle(spriteBatch, pixel, apex, top, bottom, color);
    }
}
