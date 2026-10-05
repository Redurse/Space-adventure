using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Storage pods, abandoned ships and ship graveyards (SalvagePoints.cs) drawn in the flight field:
// a small procedural silhouette per kind, its pickup ring, a label, and a one-line "+credits"
// notice after one is looted.
public sealed partial class FieldRenderer
{
    private void DrawSalvagePoints(SpriteBatch spriteBatch, WorldSnapshot snapshot, Func<Vec2, Vector2> worldToScreen,
        Vector2 viewportOrigin, Vector2 viewportSize)
    {
        var looted = snapshot.SalvagedPointIds;
        foreach (var point in snapshot.GalaxyPoints)
        {
            if (!SalvagePoints.IsSalvage(point.Kind) || (looted is not null && looted.Contains(point.Id)))
                continue;

            var screen = worldToScreen(point.Position);
            var ringPx = point.CaptureRadius * ShipRenderer.PixelsPerUnit;
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, ringPx, 0f, 360f, Color.Goldenrod * 0.45f, 40, 1.5f);
            DrawSalvageGlyph(spriteBatch, point.Kind, screen, MathF.Max(10f, ringPx * 0.45f));
            spriteBatch.DrawString(_font, point.Name, screen + new Vector2(-point.Name.Length * 3.2f, ringPx + 4f),
                Color.Goldenrod, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            DrawOffScreenMarker(spriteBatch, screen, viewportOrigin, viewportSize, point.Name, Color.Goldenrod);
        }
    }

    private void DrawSalvageGlyph(SpriteBatch spriteBatch, GalaxyPointKind kind, Vector2 center, float size)
    {
        var hull = new Color(120, 118, 112);
        var dark = new Color(60, 58, 54);
        switch (kind)
        {
            case GalaxyPointKind.StoragePod:
                spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size / 2), (int)(center.Y - size / 3), (int)size, (int)(size * 0.66f)), hull);
                spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size / 2), (int)(center.Y - 1), (int)size, 2), dark);
                spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - 1), (int)(center.Y - size / 3), 2, (int)(size * 0.66f)), dark);
                break;
            case GalaxyPointKind.AbandonedShip:
                var hullPoly = new[]
                {
                    center + new Vector2(size, 0f), center + new Vector2(-size * 0.7f, -size * 0.55f),
                    center + new Vector2(-size * 0.4f, 0f), center + new Vector2(-size * 0.7f, size * 0.55f),
                };
                Primitives.FillPolygon(spriteBatch, _pixel, center, hullPoly, hull);
                Primitives.StrokePolygon(spriteBatch, _pixel, hullPoly, dark, 1.5f);
                break;
            default: // ShipGraveyard: a scatter of broken hull plates
                for (var i = 0; i < 5; i++)
                {
                    var offset = new Vector2(MathF.Cos(i * 1.9f), MathF.Sin(i * 1.9f)) * size * 0.9f;
                    var plate = new[]
                    {
                        center + offset + new Vector2(-size * 0.3f, -size * 0.15f), center + offset + new Vector2(size * 0.3f, -size * 0.25f),
                        center + offset + new Vector2(size * 0.2f, size * 0.25f), center + offset + new Vector2(-size * 0.25f, size * 0.2f),
                    };
                    Primitives.FillPolygon(spriteBatch, _pixel, center + offset, plate, i % 2 == 0 ? hull : dark);
                }
                break;
        }
    }

    private void DrawSalvageNotice(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 viewportOrigin, Vector2 viewportSize)
    {
        if (snapshot.SalvageNotice is not { } notice)
            return;
        var width = _font.MeasureString(notice).X * 0.7f;
        var position = new Vector2(viewportOrigin.X + viewportSize.X / 2f - width / 2f, viewportOrigin.Y + 70f);
        spriteBatch.DrawString(_font, notice, position + new Vector2(1, 1), Color.Black, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, notice, position, Color.Gold, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }
}
