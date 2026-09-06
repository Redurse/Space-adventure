using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Weapons, consumables and raw goods - the carryable items that aren't a tool/tank (ItemIcons.cs)
// or a purchasable wiring part (ItemIcons.Components.cs). Same Bar/Circle/RingArc building blocks;
// the upright ones (crate, suit, kit, spool, mineral) use baseAngle 0 so alongAxis/acrossAxis are
// plain x/y, while the held ones (knife, rifle) take the same rotation angle the tools do.
public static partial class ItemIcons
{
    private static void DrawAmmoCrate(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var wood = new Color(120, 82, 46);
        var dark = new Color(70, 46, 24);
        var metal = new Color(150, 150, 155);

        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.86f, 0.86f, wood); // crate body
        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.30f, 0.80f, 0.09f, dark); // top plank seam
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.30f, 0.80f, 0.09f, dark); // bottom plank seam
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.10f, 0.80f, dark); // vertical brace
        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.42f, 0.66f, 0.06f, Color.Goldenrod * 0.7f); // stencilled band
        Circle(spriteBatch, pixel, origin, a, scale, -0.36f, -0.36f, 0.05f, metal); // corner reinforcements
        Circle(spriteBatch, pixel, origin, a, scale, 0.36f, -0.36f, 0.05f, metal);
        Circle(spriteBatch, pixel, origin, a, scale, -0.36f, 0.36f, 0.05f, metal);
        Circle(spriteBatch, pixel, origin, a, scale, 0.36f, 0.36f, 0.05f, metal);
    }

    private static void DrawSpacesuit(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var suit = new Color(212, 214, 220);
        var suitShade = new Color(160, 162, 168);
        var visor = new Color(80, 160, 210);
        var accent = new Color(214, 90, 40);

        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.16f, 0.34f, 0.56f, suit); // torso, below the helmet
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.12f, 0.28f, 0.10f, accent); // chest accent stripe
        Bar(spriteBatch, pixel, origin, a, scale, -0.24f, 0.06f, 0.10f, 0.40f, suitShade); // arm
        Bar(spriteBatch, pixel, origin, a, scale, 0.24f, 0.06f, 0.10f, 0.40f, suitShade); // arm
        Circle(spriteBatch, pixel, origin, a, scale, 0f, -0.16f, 0.30f, suit); // helmet
        Circle(spriteBatch, pixel, origin, a, scale, 0f, -0.16f, 0.21f, visor); // visor
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, -0.16f, 0.21f, -150f, -50f, Color.White * 0.4f, 0.03f, 8); // sheen
    }

    private static void DrawKnife(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float a)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var blade = new Color(200, 204, 210);
        var handle = new Color(50, 46, 42);

        Bar(spriteBatch, pixel, origin, a, scale, -0.30f, 0f, 0.34f, 0.16f, handle); // handle
        Circle(spriteBatch, pixel, origin, a, scale, -0.46f, 0f, 0.075f, handle); // handle butt
        Bar(spriteBatch, pixel, origin, a, scale, -0.10f, 0f, 0.10f, 0.05f, new Color(120, 120, 124)); // bolster
        Bar(spriteBatch, pixel, origin, a, scale, 0.20f, 0f, 0.48f, 0.14f, blade); // blade body
        Bar(spriteBatch, pixel, origin, a, scale, 0.16f, -0.03f, 0.40f, 0.03f, Color.White * 0.5f); // edge highlight
        Bar(spriteBatch, pixel, origin, a, scale, 0.46f, -0.035f, 0.14f, 0.045f, blade, -0.5f); // tapered tip
        Bar(spriteBatch, pixel, origin, a, scale, 0.46f, 0.035f, 0.14f, 0.045f, blade, 0.5f);
    }

    // Shared with LaserRifle - same silhouette (stock, receiver, magazine, barrel), differing only in
    // whether the barrel is a mechanical one with a muzzle/front sight or a glowing power cell.
    private static void DrawRifle(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float a, bool laser)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var metal = new Color(72, 74, 80);
        var dark = new Color(30, 30, 34);
        var accent = laser ? new Color(90, 170, 230) : new Color(50, 52, 58);

        Bar(spriteBatch, pixel, origin, a, scale, -0.44f, 0.06f, 0.20f, 0.10f, dark, 1.2f); // stock
        Bar(spriteBatch, pixel, origin, a, scale, -0.18f, 0f, 0.34f, 0.16f, metal); // receiver
        Bar(spriteBatch, pixel, origin, a, scale, -0.18f, -0.10f, 0.28f, 0.04f, Color.White * 0.2f); // top highlight
        Bar(spriteBatch, pixel, origin, a, scale, -0.16f, 0.17f, 0.09f, 0.15f, dark); // magazine
        Bar(spriteBatch, pixel, origin, a, scale, 0.28f, 0f, 0.50f, 0.085f, accent); // barrel/power cell

        if (laser)
        {
            Bar(spriteBatch, pixel, origin, a, scale, 0.26f, -0.06f, 0.36f, 0.02f, Color.White * 0.5f); // cell glow line
            Circle(spriteBatch, pixel, origin, a, scale, 0.52f, 0f, 0.05f, new Color(150, 220, 255)); // emitter glow
        }
        else
        {
            Circle(spriteBatch, pixel, origin, a, scale, 0.52f, 0f, 0.035f, dark); // muzzle
            Bar(spriteBatch, pixel, origin, a, scale, 0.44f, -0.07f, 0.10f, 0.03f, metal, -0.3f); // front sight
        }
    }

    private static void DrawFuelRod(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var metal = new Color(150, 154, 160);
        var band = new Color(40, 40, 44);
        var glow = new Color(170, 230, 90);

        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.66f, 0.20f, metal, MathF.PI / 2f); // rod body, vertical
        Circle(spriteBatch, pixel, origin, a, scale, 0f, -0.33f, 0.10f, metal); // end caps
        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0.33f, 0.10f, metal);
        Bar(spriteBatch, pixel, origin, a, scale, -0.045f, 0f, 0.60f, 0.03f, Color.White * 0.3f, MathF.PI / 2f); // highlight
        Bar(spriteBatch, pixel, origin, a, scale, 0f, -0.14f, 0.20f, 0.05f, band); // hazard band
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.10f, 0.20f, 0.05f, band); // hazard band
        Circle(spriteBatch, pixel, origin, a, scale, 0f, -0.30f, 0.045f, glow); // glowing tip
    }

    private static void DrawMedKit(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var box = new Color(230, 230, 232);
        var shade = new Color(190, 190, 194);
        var red = new Color(210, 40, 40);

        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.02f, 0.80f, 0.62f, box); // case body
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0.30f, 0.80f, 0.06f, shade); // bottom shade lip
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, -0.34f, 0.16f, 200f, 340f, new Color(90, 90, 94), 0.05f, 10); // carry handle
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.42f, 0.12f, red); // cross, horizontal arm
        Bar(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.12f, 0.42f, red); // cross, vertical arm
    }

    private static void DrawWireSpool(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        const float a = 0f;
        var reel = new Color(210, 170, 60);
        var wire = new Color(180, 90, 40);
        var core = new Color(70, 70, 74);

        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.40f, reel); // outer flange
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.30f, 0f, 360f, wire, 0.09f, 20); // wound wire band
        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.13f, core); // hub
        Circle(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.05f, Color.Black * 0.4f); // axle hole
        RingArc(spriteBatch, pixel, origin, a, scale, 0f, 0f, 0.40f, -150f, -60f, Color.White * 0.3f, 0.03f, 8); // sheen
    }

    // An irregular chunk rather than a rounded blob, so it reads as raw ore and not a gemstone -
    // Primitives.FillPolygon/FillTriangle (used everywhere else for the same reason - HullSkin's
    // nose, TurretReticle) rather than the Bar/Circle helpers, since a mineral has no rigid "tool
    // axis" to build around.
    private static void DrawMineral(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        var origin = new Vector2(rect.Center.X, rect.Center.Y);
        var scale = MathF.Min(rect.Width, rect.Height);
        var rock = new Color(120, 150, 160);
        var facet = new Color(165, 200, 210);
        var dark = new Color(70, 95, 105);

        var points = new[]
        {
            origin + new Vector2(-0.10f, -0.42f) * scale,
            origin + new Vector2(0.22f, -0.34f) * scale,
            origin + new Vector2(0.40f, -0.02f) * scale,
            origin + new Vector2(0.26f, 0.36f) * scale,
            origin + new Vector2(-0.14f, 0.42f) * scale,
            origin + new Vector2(-0.40f, 0.10f) * scale,
            origin + new Vector2(-0.34f, -0.22f) * scale,
        };
        Primitives.FillPolygon(spriteBatch, pixel, origin, points, rock);
        Primitives.FillTriangle(spriteBatch, pixel, origin, points[6], points[0], facet * 0.85f); // one bright facet
        Primitives.FillTriangle(spriteBatch, pixel, origin, points[0], points[1], facet);
        Primitives.StrokePolygon(spriteBatch, pixel, points, dark, 1.6f);
    }
}
