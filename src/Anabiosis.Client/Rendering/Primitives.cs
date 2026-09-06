using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Shapes this project's SpriteBatch pipeline has no primitive for. Everything in the game is drawn
// from one 1x1 white pixel, so a triangle is a fan of thin quads swept from its apex and a polygon
// is a fan of those - the same trick the wiring panel uses to draw a line at an angle.
public static class Primitives
{
    public static void FillTriangle(SpriteBatch spriteBatch, Texture2D pixel, Vector2 apex, Vector2 a, Vector2 b, Color color)
    {
        var steps = (int)MathF.Ceiling((b - a).Length() / 2f) + 1;
        for (var i = 0; i <= steps; i++)
        {
            var edgePoint = Vector2.Lerp(a, b, i / (float)steps);
            var spoke = edgePoint - apex;
            var length = spoke.Length();
            if (length < 0.01f)
                continue;
            spriteBatch.Draw(pixel, apex, null, color, MathF.Atan2(spoke.Y, spoke.X), new Vector2(0f, 0.5f),
                new Vector2(length, 3f), SpriteEffects.None, 0f);
        }
    }

    // Convex-ish outlines only - it fans from the given centre, which is what every polygon in this
    // game (asteroid rings, hull plating) is built around anyway.
    public static void FillPolygon(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, ReadOnlySpan<Vector2> points, Color color)
    {
        for (var i = 0; i < points.Length; i++)
            FillTriangle(spriteBatch, pixel, center, points[i], points[(i + 1) % points.Length], color);
    }

    public static void StrokePolygon(SpriteBatch spriteBatch, Texture2D pixel, ReadOnlySpan<Vector2> points, Color color, float thickness = 1.5f)
    {
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var edge = points[(i + 1) % points.Length] - a;
            var length = edge.Length();
            if (length < 0.01f)
                continue;
            spriteBatch.Draw(pixel, a, null, color, MathF.Atan2(edge.Y, edge.X), new Vector2(0f, 0.5f),
                new Vector2(length, thickness), SpriteEffects.None, 0f);
        }
    }
}
