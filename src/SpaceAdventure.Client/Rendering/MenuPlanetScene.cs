using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// The main menu's right-hand art pane: a planet on a slow orbit with the player's own ship
// circling it, built entirely from this project's usual "no image assets" primitive toolkit - a
// column-by-column gradient fill for the sphere's day/night shading (the same trick HudIcons.
// FillCircle uses, just varying colour across the columns instead of filling them flat), an N-gon
// ellipse for the ring and orbit path, and a handful of animated stars for depth.
public static class MenuPlanetScene
{
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds)
    {
        DrawGradientBackground(spriteBatch, pixel, pane);
        DrawNebula(spriteBatch, pixel, pane);
        DrawStars(spriteBatch, pixel, pane, totalSeconds);

        var planetCenter = new Vector2(pane.X + pane.Width * 0.6f, pane.Y + pane.Height * 0.56f);
        const float planetRadius = 116f;
        const float ringRadiusX = 205f;
        const float ringRadiusY = 44f;
        const float ringTilt = -0.22f;
        const float orbitRadiusX = 300f;
        const float orbitRadiusY = 185f;

        DrawOrbitPath(spriteBatch, pixel, planetCenter, orbitRadiusX, orbitRadiusY);
        DrawRingHalf(spriteBatch, pixel, planetCenter, ringRadiusX, ringRadiusY, ringTilt, front: false);
        DrawPlanet(spriteBatch, pixel, planetCenter, planetRadius, totalSeconds);
        DrawRingHalf(spriteBatch, pixel, planetCenter, ringRadiusX, ringRadiusY, ringTilt, front: true);
        DrawOrbitingShip(spriteBatch, pixel, planetCenter, orbitRadiusX, orbitRadiusY, totalSeconds);
    }

    private static void DrawGradientBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane)
    {
        const int bands = 12;
        var top = new Color(5, 8, 15);
        var bottom = new Color(15, 24, 32);
        for (var i = 0; i < bands; i++)
        {
            var y = pane.Y + pane.Height * i / bands;
            var height = pane.Height / bands + 1;
            spriteBatch.Draw(pixel, new Rectangle(pane.X, y, pane.Width, height), Color.Lerp(top, bottom, i / (float)(bands - 1)));
        }
    }

    // Two big, very soft colour blobs well behind everything else - just enough tint to keep the
    // background from reading as flat black, without competing with the planet for attention.
    private static void DrawNebula(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane)
    {
        DrawSoftGlow(spriteBatch, pixel, new Vector2(pane.X + pane.Width * 0.18f, pane.Y + pane.Height * 0.22f), 220f, new Color(80, 45, 120), 0.12f);
        DrawSoftGlow(spriteBatch, pixel, new Vector2(pane.X + pane.Width * 0.88f, pane.Y + pane.Height * 0.12f), 170f, new Color(30, 95, 115), 0.12f);
    }

    // Concentric circles fading outward - the one "glow" primitive the nebula, the atmosphere halo
    // and the engine flare below are all built from.
    private static void DrawSoftGlow(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color, float peakAlpha)
    {
        const int rings = 6;
        for (var i = rings; i >= 1; i--)
        {
            var r = radius * i / rings;
            var alpha = peakAlpha * (1f - (float)i / rings);
            HudIcons.FillCircle(spriteBatch, pixel, center, r, color * alpha);
        }
    }

    private static void DrawStars(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds)
    {
        const int count = 90;
        for (var i = 0; i < count; i++)
        {
            // Deterministic scatter (no Random instance so stars never jump between frames) - the
            // same pseudo-random-from-the-index trick the wall breach's stars used.
            var x = pane.X + (i * 53 + (i * i * 7) % 41) % pane.Width;
            var y = (i * 97 + (i * i * 3) % 59) % pane.Height;
            var big = i % 4 == 0;
            var twinkle = 0.3f + 0.7f * MathF.Abs(MathF.Sin(totalSeconds * (0.5f + (i % 5) * 0.12f) + i));
            var size = big ? 2 : 1;
            spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), Color.White * twinkle);
        }
    }

    // A dashed ellipse tracing the ship's own path around the planet - drawn once, fully, before
    // the planet body so the far side reads as passing behind it.
    private static void DrawOrbitPath(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radiusX, float radiusY)
    {
        const int segments = 72;
        for (var i = 0; i < segments; i += 2)
        {
            var a0 = i * 2f * MathF.PI / segments;
            var a1 = (i + 1) * 2f * MathF.PI / segments;
            var p0 = center + new Vector2(MathF.Cos(a0) * radiusX, MathF.Sin(a0) * radiusY);
            var p1 = center + new Vector2(MathF.Cos(a1) * radiusX, MathF.Sin(a1) * radiusY);
            HudIcons.DrawLine(spriteBatch, pixel, p0, p1, Color.CadetBlue * 0.22f, 1.2f);
        }
    }

    // The sphere itself, column by column rather than one flat fill - each column's own visible
    // height is clamped to the true circle (same maths as HudIcons.FillCircle), and its colour
    // blends from the night side to the lit side across the disk. That keeps the terminator a
    // clean gradient without ever painting outside the planet's own silhouette, which a simpler
    // "dark circle offset to one side" overlay could not do without a real clip.
    private static void DrawPlanet(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, float totalSeconds)
    {
        DrawSoftGlow(spriteBatch, pixel, center, radius * 1.4f, new Color(120, 175, 220), 0.22f);

        var night = new Color(28, 34, 30);
        var lit = new Color(150, 168, 118);
        var r = (int)MathF.Ceiling(radius);
        for (var dx = -r; dx <= r; dx++)
        {
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - dx * dx));
            if (half < 0.5f)
                continue;
            var t = (dx + radius) / (2f * radius);
            var color = Color.Lerp(night, lit, MathF.Pow(t, 0.8f));
            spriteBatch.Draw(pixel, new Rectangle((int)(center.X + dx), (int)(center.Y - half), 1, (int)MathF.Ceiling(half * 2f)), color);
        }

        // Cloud/continent blotches, small enough to stay clear of the rim, that drift slowly
        // around the sphere - the closest this project gets to a rotating planet texture.
        var seedAngles = new[] { 0.3f, 1.6f, 2.7f, 4.1f, 5.2f };
        for (var i = 0; i < seedAngles.Length; i++)
        {
            var angle = seedAngles[i] + totalSeconds * 0.05f;
            var orbitRadius = radius * (0.3f + 0.08f * (i % 3));
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f) * orbitRadius;
            if (offset.Length() > radius * 0.7f)
                continue;
            var blobRadius = radius * (0.18f + 0.04f * (i % 3));
            var shade = MathF.Sin(angle) > 0f ? new Color(190, 200, 150) * 0.22f : new Color(20, 24, 20) * 0.25f;
            HudIcons.FillCircle(spriteBatch, pixel, center + offset, blobRadius, shade);
        }

        // A small bright limb highlight on the sunlit edge, on top of everything - the one thing
        // that really sells "sphere lit from one side" at a glance.
        HudIcons.FillCircle(spriteBatch, pixel, center + new Vector2(radius * 0.55f, -radius * 0.35f), radius * 0.22f, Color.White * 0.12f);
    }

    // One half of the ring (whichever side of the tilt axis is currently "front" or "back"),
    // drawn as a dashed-ish open arc rather than a closed StrokePolygon loop, so the two halves can
    // be layered on opposite sides of the planet body.
    private static void DrawRingHalf(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radiusX, float radiusY, float tilt, bool front)
    {
        const int segments = 56;
        var cosT = MathF.Cos(tilt);
        var sinT = MathF.Sin(tilt);
        Vector2? previous = null;
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * 2f * MathF.PI / segments;
            var localY = MathF.Sin(angle) * radiusY;
            if ((localY >= 0f) != front)
            {
                previous = null;
                continue;
            }

            var localX = MathF.Cos(angle) * radiusX;
            var rotated = new Vector2(localX * cosT - localY * sinT, localX * sinT + localY * cosT);
            var point = center + rotated;
            if (previous is { } prev)
            {
                var color = front ? new Color(210, 200, 180) * 0.6f : new Color(120, 112, 100) * 0.32f;
                HudIcons.DrawLine(spriteBatch, pixel, prev, point, color, front ? 3f : 2f);
            }
            previous = point;
        }
    }

    private static void DrawOrbitingShip(SpriteBatch spriteBatch, Texture2D pixel, Vector2 planetCenter, float orbitRadiusX, float orbitRadiusY, float totalSeconds)
    {
        var angle = totalSeconds * 0.16f;
        var position = planetCenter + new Vector2(MathF.Cos(angle) * orbitRadiusX, MathF.Sin(angle) * orbitRadiusY);
        // Tangent to the ellipse at this angle (the parametric position's own derivative) - what
        // the ship's nose points along as it travels.
        var tangent = new Vector2(-MathF.Sin(angle) * orbitRadiusX, MathF.Cos(angle) * orbitRadiusY);
        if (tangent.LengthSquared() < 0.01f)
            tangent = Vector2.UnitX;
        tangent.Normalize();
        var rotation = MathF.Atan2(tangent.Y, tangent.X);

        DrawSoftGlow(spriteBatch, pixel, position - tangent * 16f, 14f, new Color(255, 160, 80), 0.55f);
        DrawShipSilhouette(spriteBatch, pixel, position, rotation);
    }

    private static void DrawShipSilhouette(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, float rotation)
    {
        const float hullLength = 30f;
        const float hullWidth = 9f;
        var color = new Color(205, 213, 222);

        spriteBatch.Draw(pixel, position, null, color, rotation, new Vector2(0.5f, 0.5f),
            new Vector2(hullLength * 0.65f, hullWidth), SpriteEffects.None, 0f);

        var forward = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
        var side = new Vector2(-forward.Y, forward.X);
        var noseBase = position + forward * hullLength * 0.32f;
        Primitives.FillTriangle(spriteBatch, pixel,
            noseBase + forward * hullLength * 0.4f,
            noseBase + side * hullWidth * 0.5f,
            noseBase - side * hullWidth * 0.5f,
            color);
    }
}
