using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// One distinct silhouette per GalaxyPointKind, replacing what used to be a single flat coloured
// square for all four - built from the same primitives (FillPolygon/FillCircle/RingArc) every
// other procedural icon in this client already uses, sized to the marker's own fixed-size screen
// rect (PointMarkerSize) so hit-testing never has to know these shapes exist.
public sealed partial class GalaxyMapPanel
{
    private void DrawPointGlyph(SpriteBatch spriteBatch, GalaxyPointKind kind, Rectangle rect, Color color, float totalSeconds)
    {
        switch (kind)
        {
            case GalaxyPointKind.Station:
                DrawStationGlyph(spriteBatch, rect, color);
                break;
            case GalaxyPointKind.AsteroidField:
                DrawAsteroidFieldGlyph(spriteBatch, rect, color);
                break;
            default: // HostileSector
                DrawHostileSectorGlyph(spriteBatch, rect, color, totalSeconds);
                break;
        }
    }

    // A hexagonal hull with a lit core and two stub docking arms - reads as "a built structure"
    // next to the organic/animated shapes the other three kinds get.
    private void DrawStationGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var r = rect.Width / 2f;
        var hex = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 6f + i * MathF.PI / 3f;
            hex[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * r;
        }
        Primitives.FillPolygon(spriteBatch, _pixel, center, hex, color * 0.85f);
        Primitives.StrokePolygon(spriteBatch, _pixel, hex, Color.White * 0.6f, 1.5f);
        HudIcons.FillCircle(spriteBatch, _pixel, center, r * 0.32f, Color.White * 0.75f);
        spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - r * 1.3f), (int)center.Y - 1, (int)(r * 0.5f), 2), color);
        spriteBatch.Draw(_pixel, new Rectangle((int)(center.X + r * 0.8f), (int)center.Y - 1, (int)(r * 0.5f), 2), color);
    }

    // Three irregular rocks clustered together rather than one neat shape - the same "not one
    // unbroken outline" idea HullSkin's own flank greebles lean on.
    private void DrawAsteroidFieldGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var r = rect.Width / 2f;
        DrawRock(spriteBatch, center + new Vector2(-r * 0.4f, r * 0.15f), r * 0.55f, color, seed: 1);
        DrawRock(spriteBatch, center + new Vector2(r * 0.35f, r * 0.35f), r * 0.4f, color * 0.85f, seed: 2);
        DrawRock(spriteBatch, center + new Vector2(r * 0.05f, -r * 0.45f), r * 0.42f, color * 1.1f, seed: 3);
    }

    private void DrawRock(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, int seed)
    {
        const int sides = 6;
        var points = new Vector2[sides];
        var random = new Random(seed);
        for (var i = 0; i < sides; i++)
        {
            var angle = i * MathF.PI * 2f / sides;
            var wobble = 0.75f + (float)random.NextDouble() * 0.4f;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * wobble;
        }
        Primitives.FillPolygon(spriteBatch, _pixel, center, points, color);
        Primitives.StrokePolygon(spriteBatch, _pixel, points, Color.Black * 0.35f, 1f);
    }

    // A jagged burst that pulses slightly - reads as a hazard, not a place you'd want to sit still.
    private void DrawHostileSectorGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color, float totalSeconds)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var r = rect.Width / 2f * (0.85f + 0.15f * MathF.Sin(totalSeconds * 3f));
        const int spikes = 7;
        var points = new Vector2[spikes * 2];
        for (var i = 0; i < spikes * 2; i++)
        {
            var angle = i * MathF.PI / spikes;
            var radius = i % 2 == 0 ? r : r * 0.5f;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        Primitives.FillPolygon(spriteBatch, _pixel, center, points, color);
        Primitives.StrokePolygon(spriteBatch, _pixel, points, Color.White * 0.5f, 1.2f);
    }
}
