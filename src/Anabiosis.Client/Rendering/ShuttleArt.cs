using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// A shuttle seen from above, drawn from a few polygons: a short blunt hull, a lit cockpit, two stub wings and, when it is under
// thrust, a glow at the stern. One painter for every place it shows - on the pad of its bay in the ship, out in the field and as the
// marker on the helm's map - so it looks the same wherever it is. Units are the ship's own (a shuttle is about 4 long); the nose
// points along +X at rotation 0.
internal static class ShuttleArt
{
    private static readonly Vector2[] Hull =
    {
        new(2.2f, 0f), new(1.3f, -0.95f), new(-1.3f, -1.15f), new(-1.9f, -0.65f), new(-1.9f, 0.65f), new(-1.3f, 1.15f), new(1.3f, 0.95f),
    };
    private static readonly Vector2[] WingPort = { new(0.5f, -1.0f), new(-1.1f, -1.1f), new(-1.5f, -2.0f), new(-0.2f, -2.0f) };
    private static readonly Vector2[] WingStarboard = { new(0.5f, 1.0f), new(-1.1f, 1.1f), new(-1.5f, 2.0f), new(-0.2f, 2.0f) };
    private static readonly Vector2[] Cockpit = { new(1.5f, 0f), new(0.7f, -0.55f), new(0.3f, 0f), new(0.7f, 0.55f) };

    // scale is pixels per unit; alpha fades the whole thing (a shuttle in the dark, a ghost).
    internal static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float rotationRadians, float scale, bool thrust, float alpha, float time)
    {
        var cos = MathF.Cos(rotationRadians);
        var sin = MathF.Sin(rotationRadians);
        Vector2[] Place(Vector2[] shape)
        {
            var points = new Vector2[shape.Length];
            for (var i = 0; i < shape.Length; i++)
            {
                var p = shape[i] * scale;
                points[i] = center + new Vector2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
            }
            return points;
        }

        if (thrust)
        {
            var flicker = 0.75f + 0.25f * MathF.Sin(time * 40f);
            for (var side = -1; side <= 1; side += 2)
            {
                var from = Place(new[] { new Vector2(-1.9f, 0.45f * side) })[0];
                var tip = Place(new[] { new Vector2(-1.9f - 1.6f * flicker, 0.45f * side) })[0];
                var perpendicular = new Vector2(-sin, cos) * (0.28f * scale);
                Primitives.FillPolygon(spriteBatch, pixel, from, new[] { from + perpendicular, tip, from - perpendicular }, new Color(150, 200, 255) * (0.85f * alpha));
            }
        }
        Primitives.FillPolygon(spriteBatch, pixel, center, Place(WingPort), new Color(86, 96, 112) * alpha);
        Primitives.FillPolygon(spriteBatch, pixel, center, Place(WingStarboard), new Color(86, 96, 112) * alpha);
        var hull = Place(Hull);
        Primitives.FillPolygon(spriteBatch, pixel, center, hull, new Color(150, 160, 176) * alpha);
        Primitives.StrokePolygon(spriteBatch, pixel, hull, new Color(40, 46, 58) * alpha, MathF.Max(1f, scale * 0.12f));
        Primitives.FillPolygon(spriteBatch, pixel, center, Place(Cockpit), new Color(120, 200, 240) * alpha);
    }
}
