using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Direct user request ("курсор мышки как в баротравме"): the OS's own plain system arrow doesn't
// fit this client's whole "everything drawn from one white pixel" convention, and it can't change
// shape to say "there's something here to interact with" the way Baro's own cursor does. Game1
// sets IsMouseVisible = false and draws this instead, every frame, at the very end of the HUD pass
// so it's never covered by any panel.
//
// The arrow is a baked, supersampled texture (Initialize, called once from Game1.LoadContent)
// rather than the live filled-triangle kite this used to be - direct user follow-up chain: "более
// красивой, качественной и детализированной" (baking lets the tip/edges come out antialiased
// instead of hard triangle seams), then styled to match the ship's own gunmetal/rivet/cyan-accent
// device panels rather than a generic flat-UI white arrow, then "сзади не было палочки, а только
// сама стрелочка" (dropped the flag-shaped tail down to a plain arrowhead), then "интереснее" (a
// 3-facet faceted fan instead of one flat tone, plus a small lit sensor standing in for a rivet).
public static class GameCursor
{
    private const int Size = 20;
    private static readonly Color Outline = new(18, 20, 26);
    // The same warm gold this client already uses elsewhere for "you can act on this" (HudIcons'
    // role glyphs, the top bar's own button rings) - reusing it here instead of inventing a new
    // accent keeps "gold = interactive" reading consistently across the whole HUD.
    private static readonly Color InteractiveFill = new(255, 214, 120);

    // Design-space units the arrow polygon is authored in, and how many texels each becomes in the
    // baked texture - the supersampling that lets the antialiased tip/edges survive being drawn
    // back down at 1 texel per TexelsPerUnit on screen instead of looking chunky.
    private const int ArrowDesignSize = 22;
    private const int TexelsPerUnit = 4;
    private const float ArrowDrawScale = 1f / TexelsPerUnit;

    // Plain arrowhead triangle - tip, straight left edge, angled shoulder (direct user request: no
    // tail flag, just the arrowhead itself).
    private static readonly Vector2[] ArrowPoints = { new(0f, 0f), new(0f, 16f), new(12f, 12f) };

    private static readonly Color ArrowBright = new(224, 230, 238);
    private static readonly Color ArrowLit = new(168, 176, 186);
    private static readonly Color ArrowDark = new(96, 104, 116);
    private static readonly Color ArrowAccent = new(70, 205, 216);

    private static Texture2D? _arrowTexture;

    public static void Initialize(GraphicsDevice device)
    {
        const int canvas = ArrowDesignSize * TexelsPerUnit;
        var data = new Color[canvas * canvas];
        for (var y = 0; y < canvas; y++)
            for (var x = 0; x < canvas; x++)
                data[y * canvas + x] = ArrowTexel(x, y, canvas);

        _arrowTexture = new Texture2D(device, canvas, canvas);
        _arrowTexture.SetData(data);
    }

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, bool interactive)
    {
        if (interactive)
            DrawHand(spriteBatch, pixel, position);
        else if (_arrowTexture is { } texture)
            spriteBatch.Draw(texture, position, null, Color.White, 0f, Vector2.Zero, ArrowDrawScale, SpriteEffects.None, 0f);
    }

    // 4x4 supersampled per output texel - what actually makes the tip/edges read as smooth instead
    // of stair-stepped once drawn back down at ArrowDrawScale.
    private static Color ArrowTexel(int px, int py, int canvas)
    {
        const int ss = 4;
        float r = 0, g = 0, b = 0, a = 0;
        for (var sy = 0; sy < ss; sy++)
        {
            for (var sx = 0; sx < ss; sx++)
            {
                var x = (px + (sx + 0.5f) / ss) / TexelsPerUnit;
                var y = (py + (sy + 0.5f) / ss) / TexelsPerUnit;
                var c = ArrowPixel(x, y);
                r += c.R; g += c.G; b += c.B; a += c.A;
            }
        }
        const int n = ss * ss;
        return new Color(r / n / 255f, g / n / 255f, b / n / 255f, a / n / 255f);
    }

    // `x`/`y` are design-space units (ArrowPoints' own authoring scale), not texels.
    private static Color ArrowPixel(float x, float y)
    {
        var p = new Vector2(x, y);

        const float outerScale = 1.16f;
        Span<Vector2> outerPoints = stackalloc Vector2[ArrowPoints.Length];
        for (var i = 0; i < ArrowPoints.Length; i++)
            outerPoints[i] = ArrowPoints[i] * outerScale;

        if (!PointInPolygon(p, outerPoints))
            return Color.Transparent;
        if (!PointInPolygon(p, ArrowPoints))
            return Outline;

        // Three facets fanning out from the tip - a faceted, cut-gem read rather than a flat
        // sticker, the same "panel reads as folded metal" bevel language HullSkin/ReactorTexture
        // already use everywhere else on the ship.
        var tip = ArrowPoints[0];
        var baseA = ArrowPoints[1];
        var baseB = ArrowPoints[2];
        var angleA = MathF.Atan2((baseA - tip).Y, (baseA - tip).X);
        var angleB = MathF.Atan2((baseB - tip).Y, (baseB - tip).X);
        var angleP = MathF.Atan2((p - tip).Y, (p - tip).X);
        var sweep = MathHelper.Clamp((angleP - angleA) / (angleB - angleA), 0f, 1f);

        var facet1 = tip + (Vector2.Lerp(baseA, baseB, 0.32f) - tip);
        var facet2 = tip + (Vector2.Lerp(baseA, baseB, 0.66f) - tip);

        var fill = sweep < 0.32f ? ArrowBright : sweep < 0.66f ? ArrowLit : ArrowDark;

        // A cyan seam along the brightest facet's own boundary - the "this is an active HUD
        // element" accent Terminal's own vent stripes/dial lights already use.
        var seamDistance = DistanceToSegment(p, tip + (facet1 - tip) * 0.1f, tip + (facet1 - tip) * 0.95f);
        if (seamDistance < 0.5f)
            fill = Color.Lerp(fill, ArrowAccent, 1f - seamDistance / 0.5f);

        // A second, quieter crease between the middle and darkest facets - a real shadow step
        // rather than a second glowing line.
        var seam2Distance = DistanceToSegment(p, tip + (facet2 - tip) * 0.1f, tip + (facet2 - tip) * 0.95f);
        if (seam2Distance < 0.4f)
            fill = Color.Lerp(fill, Outline, (1f - seam2Distance / 0.4f) * 0.55f);

        // A thin highlight bevel right at the straight left edge.
        var leftEdgeDistance = DistanceToSegment(p, ArrowPoints[0], ArrowPoints[1]);
        if (leftEdgeDistance < 0.8f)
            fill = Color.Lerp(fill, Color.White, 1f - leftEdgeDistance / 0.8f);

        // A small lit sensor rather than a plain bolt - a dark bezel ring with a bright cyan core,
        // echoing Terminal's own status lights/fan hub.
        fill = ApplySensor(fill, p, new Vector2(5f, 9.2f), 0.85f);

        return fill;
    }

    private static Color ApplySensor(Color baseColor, Vector2 p, Vector2 centre, float radius)
    {
        var dist = Vector2.Distance(p, centre);
        if (dist > radius) return baseColor;
        if (dist < radius * 0.45f)
        {
            var innerT = dist / (radius * 0.45f);
            return Color.Lerp(baseColor, ArrowAccent, MathHelper.Lerp(1f, 0.7f, innerT));
        }
        var bezel = 1f - MathHelper.Clamp((dist - radius * 0.45f) / (radius * 0.55f), 0f, 1f);
        return Color.Lerp(baseColor, Outline, bezel * 0.8f);
    }

    private static bool PointInPolygon(Vector2 p, ReadOnlySpan<Vector2> poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var pi = poly[i];
            var pj = poly[j];
            if ((pi.Y > p.Y) != (pj.Y > p.Y) &&
                p.X < (pj.X - pi.X) * (p.Y - pi.Y) / (pj.Y - pi.Y) + pi.X)
                inside = !inside;
        }
        return inside;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var lenSq = ab.LengthSquared();
        var t = lenSq > 0f ? MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    // Hovering something the player can actually act on right now (a device, a door, a dropped
    // item, an NPC - Game1.Input.cs's own ComputeHoveredInteractable). A simple palm-plus-finger
    // silhouette rather than the arrow, so the shape change reads at a glance instead of needing a
    // colour-only tell.
    private static void DrawHand(SpriteBatch spriteBatch, Texture2D pixel, Vector2 tip)
    {
        var palmSize = new Vector2(Size * 0.6f, Size * 0.5f);
        var palm = new Rectangle((int)(tip.X - 1), (int)(tip.Y + Size * 0.32f), (int)palmSize.X, (int)palmSize.Y);
        var finger = new Rectangle((int)(tip.X - 1), (int)tip.Y, (int)(Size * 0.22f), (int)(Size * 0.55f));

        var outlinePalm = new Rectangle(palm.X - 1, palm.Y - 1, palm.Width + 2, palm.Height + 2);
        var outlineFinger = new Rectangle(finger.X - 1, finger.Y - 1, finger.Width + 2, finger.Height + 2);
        spriteBatch.Draw(pixel, outlinePalm, Outline);
        spriteBatch.Draw(pixel, outlineFinger, Outline);
        spriteBatch.Draw(pixel, palm, InteractiveFill);
        spriteBatch.Draw(pixel, finger, InteractiveFill);
    }
}
