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

    // Auto-fits the map's own bounding box to start right at panelOrigin (before the player's own
    // zoom/pan camera is applied on top) — used identically by Draw() and by Game1's mouse
    // hit-testing so click regions always match what's rendered. zoom scales PixelsPerUnit; panOffset
    // is a raw screen-pixel nudge from right-drag (Game1.cs) - both purely a client view, never sent
    // to or read from the server.
    public static Vector2 ComputeMapOrigin(Vector2 panelOrigin, IReadOnlyList<GalaxyPoint> points, float zoom, Vector2 panOffset)
    {
        if (points.Count == 0)
            return panelOrigin + panOffset;

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        return panelOrigin + panOffset - new Vector2(minX, minY) * PixelsPerUnit * zoom;
    }

    // Marker size stays fixed on screen regardless of zoom - shrinking it with the map would make
    // distant points progressively harder to click exactly when zooming out to see more of them.
    public static Rectangle GetPointRect(GalaxyPoint point, Vector2 mapOrigin, float zoom)
    {
        var center = mapOrigin + new Vector2(point.X, point.Y) * PixelsPerUnit * zoom;
        return new Rectangle((int)center.X - PointMarkerSize / 2, (int)center.Y - PointMarkerSize / 2, PointMarkerSize, PointMarkerSize);
    }

    // The inter-system list - every OTHER known system, one clickable row each
    // (World.StarSystems.cs's warp). Below the faction column so neither crowds the map.
    private static readonly Vector2 SystemListOrigin = new(700, 160);
    public static Rectangle GetSystemRect(int index, Vector2 panelOrigin)
    {
        var origin = panelOrigin + SystemListOrigin + new Vector2(0, index * 20);
        return new Rectangle((int)origin.X, (int)origin.Y, 260, 18);
    }

    // Whose territory a point sits in, at a glance, independent of the Station/HostileSector fill
    // color above - drawn as a border rather than replacing the fill so both facts stay visible on
    // the same marker instead of one hiding the other.
    private static Color FactionColor(FactionId faction) => faction switch
    {
        FactionId.Consortium => Color.CornflowerBlue,
        FactionId.FreeFleet => Color.Crimson,
        _ => Color.Gray,
    };

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset)
    {
        spriteBatch.DrawString(_font, "Карта галактики - клик по точке (курс), ПКМ тащить (сдвиг), колесо (масштаб)", panelOrigin + new Vector2(0, -24),
            Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var mapOrigin = ComputeMapOrigin(panelOrigin, snapshot.GalaxyPoints, zoom, panOffset);

        foreach (var point in snapshot.GalaxyPoints)
        {
            var rect = GetPointRect(point, mapOrigin, zoom);
            var isTarget = point.Id == snapshot.Voyage.TravelTargetPointId;
            var isDocked = point.Id == snapshot.Voyage.DockedPointId;
            var color = isTarget ? Color.Gold
                : point.Kind == GalaxyPointKind.Station ? Color.SteelBlue
                : point.Kind == GalaxyPointKind.WarpPoint ? Color.MediumPurple
                : point.Kind == GalaxyPointKind.AsteroidField ? Color.SaddleBrown
                : Color.OrangeRed;

            spriteBatch.Draw(_pixel, rect, color);
            DrawRectOutline(spriteBatch, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), FactionColor(point.Faction), 2);
            if (isDocked)
                DrawRectOutline(spriteBatch, new Rectangle(rect.X - 5, rect.Y - 5, rect.Width + 10, rect.Height + 10), Color.LimeGreen, 2);

            var kindLabel = point.Kind switch
            {
                GalaxyPointKind.Station => "станция",
                GalaxyPointKind.WarpPoint => "граница системы",
                GalaxyPointKind.AsteroidField => "пояс астероидов",
                _ => "враждебный сектор",
            };
            spriteBatch.DrawString(_font, $"{point.Name} ({kindLabel})", new Vector2(rect.X - 10, rect.Bottom + 2),
                Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        var shipCenter = mapOrigin + new Vector2(snapshot.Voyage.ShipMapPosition.X, snapshot.Voyage.ShipMapPosition.Y) * PixelsPerUnit * zoom;
        spriteBatch.Draw(_pixel, new Rectangle((int)shipCenter.X - 4, (int)shipCenter.Y - 4, 8, 8), Color.White);

        DrawFactionStandings(spriteBatch, snapshot.FactionStandings, panelOrigin + new Vector2(700, 0));
        DrawStarSystems(spriteBatch, snapshot, panelOrigin);
    }

    // Every other known system, one row each - "[Клик] Прыжок" only once actually parked at this
    // system's own WarpPoint slowly enough (World.StarSystems.cs's CanWarpNow), same as the
    // helm's "Стыковка" button only lighting up once alongside the berth.
    private void DrawStarSystems(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var origin = panelOrigin + SystemListOrigin;
        spriteBatch.DrawString(_font, "Соседние системы", origin + new Vector2(0, -22),
            Color.Yellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        var others = snapshot.StarSystems.Where(s => s.Id != snapshot.CurrentSystemId).ToList();
        for (var i = 0; i < others.Count; i++)
        {
            var system = others[i];
            var rect = GetSystemRect(i, panelOrigin);
            var label = snapshot.CanWarpNow ? $"[Клик] Прыжок: {system.Name}" : $"{system.Name} (долетите до границы системы)";
            spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Y),
                snapshot.CanWarpNow ? Color.Yellow : Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    // The number and its two known thresholds (FactionDefinitions.StandingLabel) already tell the
    // whole story - no separate legend needed for what "42" or "враждебны" means.
    private void DrawFactionStandings(SpriteBatch spriteBatch, IReadOnlyList<FactionStandingState> standings, Vector2 origin)
    {
        spriteBatch.DrawString(_font, "Отношения фракций", origin, Color.Yellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        for (var i = 0; i < standings.Count; i++)
        {
            var standing = standings[i];
            var label = FactionDefinitions.StandingLabel(standing.Standing);
            var color = standing.Standing >= FactionDefinitions.FriendlyThreshold ? Color.LimeGreen
                : standing.Standing <= FactionDefinitions.HostileThreshold ? Color.OrangeRed
                : Color.LightGray;
            var row = origin + new Vector2(0, 22 + i * 20);
            spriteBatch.Draw(_pixel, new Rectangle((int)row.X, (int)row.Y + 2, 10, 10), FactionColor(standing.Faction));
            spriteBatch.DrawString(_font, $"{standing.Name}: {label} ({standing.Standing})", row + new Vector2(16, 0),
                color, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
