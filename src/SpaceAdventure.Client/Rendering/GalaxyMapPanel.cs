using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Shown while the navigation console is open (game_design.md section 5 — "маршрут выбирает сам
// игрок"): every known point, free-form, clickable to set a travel destination. Replaces the
// ship-interior view for the moment it's open — there's nowhere else to put a map this size.
public sealed class GalaxyMapPanel
{
    public const float PixelsPerUnit = 6f;
    public const int PointMarkerSize = 16;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public GalaxyMapPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // Auto-fits the map's own bounding box to start right at panelOrigin, so callers never need
    // to know the fixed map's actual coordinate range — used identically by Draw() and by
    // Game1's mouse hit-testing so click regions always match what's rendered.
    public static Vector2 ComputeMapOrigin(Vector2 panelOrigin, IReadOnlyList<GalaxyPoint> points)
    {
        if (points.Count == 0)
            return panelOrigin;

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        return panelOrigin - new Vector2(minX, minY) * PixelsPerUnit;
    }

    public static Rectangle GetPointRect(GalaxyPoint point, Vector2 mapOrigin)
    {
        var center = mapOrigin + new Vector2(point.X, point.Y) * PixelsPerUnit;
        return new Rectangle((int)center.X - PointMarkerSize / 2, (int)center.Y - PointMarkerSize / 2, PointMarkerSize, PointMarkerSize);
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        spriteBatch.DrawString(_font, "Карта галактики - клик по точке, чтобы проложить курс", panelOrigin + new Vector2(0, -24),
            Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var mapOrigin = ComputeMapOrigin(panelOrigin, snapshot.GalaxyPoints);

        foreach (var point in snapshot.GalaxyPoints)
        {
            var rect = GetPointRect(point, mapOrigin);
            var isTarget = point.Id == snapshot.Voyage.TravelTargetPointId;
            var isDocked = point.Id == snapshot.Voyage.DockedPointId;
            var color = isTarget ? Color.Gold : point.Kind == GalaxyPointKind.Station ? Color.SteelBlue : Color.OrangeRed;

            spriteBatch.Draw(_pixel, rect, color);
            if (isDocked)
                DrawRectOutline(spriteBatch, new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), Color.LimeGreen, 2);

            var kindLabel = point.Kind == GalaxyPointKind.Station ? "станция" : "враждебный сектор";
            spriteBatch.DrawString(_font, $"{point.Name} ({kindLabel})", new Vector2(rect.X - 10, rect.Bottom + 2),
                Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        var shipCenter = mapOrigin + new Vector2(snapshot.Voyage.ShipMapPosition.X, snapshot.Voyage.ShipMapPosition.Y) * PixelsPerUnit;
        spriteBatch.Draw(_pixel, new Rectangle((int)shipCenter.X - 4, (int)shipCenter.Y - 4, 8, 8), Color.White);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
