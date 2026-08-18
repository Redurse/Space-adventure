using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The SYSTEM-level map, shown while the navigation console is open (game_design.md section 5 —
// "маршрут выбирает сам игрок"): the current system's own points of interest, free-form clickable
// anywhere in the field to set a travel destination. Replaces the ship-interior view for the
// moment it's open — there's nowhere else to put a map this size. Jumping to a DIFFERENT system is
// a separate view now (GalacticMapPanel, opened with M from anywhere) - this one only ever shows
// and targets the system the ship is already in.
public sealed partial class GalaxyMapPanel
{
    public const float PixelsPerUnit = 6f;
    public const int PointMarkerSize = 20;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly Starfield _starfield;

    // backdrop: the design-canvas area this panel gets drawn into (it takes over the whole
    // ship-interior viewport while open) - the starfield fills exactly that, same idea as
    // ShipRenderer's own constructor param.
    public GalaxyMapPanel(GraphicsDevice graphicsDevice, SpriteFont font, Rectangle backdrop)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
        _starfield = new Starfield(_pixel, backdrop, count: 200);
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

    // Inverse of the point-placement transform above - what a click on empty map background
    // actually points at in the system's own field space (game_design.md - free-form destination),
    // rather than one of the fixed markers GetPointRect covers.
    public static Vector2 ScreenToField(Vector2 screenPoint, Vector2 mapOrigin, float zoom) =>
        (screenPoint - mapOrigin) / (PixelsPerUnit * zoom);

    // Whose territory a point sits in, at a glance, independent of the Station/HostileSector fill
    // color above - drawn as a border rather than replacing the fill so both facts stay visible on
    // the same marker instead of one hiding the other.
    // Internal rather than private: InfoPanel's Reputation tab reuses the same faction/colour
    // mapping instead of keeping a second copy of it in sync.
    internal static Color FactionColor(FactionId faction) => faction switch
    {
        FactionId.Consortium => Color.CornflowerBlue,
        FactionId.FreeFleet => Color.Crimson,
        _ => Color.Gray,
    };

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset, float totalSeconds = 0f)
    {
        _starfield.Draw(spriteBatch, totalSeconds);

        spriteBatch.DrawString(_font, $"Карта системы «{snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId).Name}» - клик (курс, в любую точку), ПКМ тащить (сдвиг), колесо (масштаб), M - карта галактики",
            panelOrigin + new Vector2(0, -24), Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var mapOrigin = ComputeMapOrigin(panelOrigin, snapshot.GalaxyPoints, zoom, panOffset);
        DrawSystemBackdrop(spriteBatch, snapshot.GalaxyPoints, mapOrigin, zoom, totalSeconds);

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

            DrawPointGlyph(spriteBatch, point.Kind, rect, color, totalSeconds);
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

        // A free-form destination (game_design.md - click anywhere, not just a point of interest)
        // has no marker of its own to highlight gold, so it gets a small crosshair instead.
        if (snapshot.Voyage.TravelTargetPointId is null && snapshot.Voyage.TravelTargetPosition is { } freeTarget)
        {
            var targetScreen = mapOrigin + new Vector2(freeTarget.X, freeTarget.Y) * PixelsPerUnit * zoom;
            spriteBatch.Draw(_pixel, new Rectangle((int)targetScreen.X - 6, (int)targetScreen.Y - 1, 12, 2), Color.Gold);
            spriteBatch.Draw(_pixel, new Rectangle((int)targetScreen.X - 1, (int)targetScreen.Y - 6, 2, 12), Color.Gold);
        }

        DrawFactionStandings(spriteBatch, snapshot.FactionStandings, panelOrigin + new Vector2(700, 0));
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

    // Faint concentric rings and a small pulsing star at the system's own centre - there's no
    // in-fiction sun any of this represents (GalaxyPoints are scattered points of interest, not
    // real orbits), purely there so the map reads as "a system" at a glance instead of a scatter
    // of markers on flat black.
    private void DrawSystemBackdrop(SpriteBatch spriteBatch, IReadOnlyList<GalaxyPoint> points, Vector2 mapOrigin, float zoom, float totalSeconds)
    {
        if (points.Count == 0)
            return;

        var centerWorld = new Vector2(
            (points.Min(p => p.X) + points.Max(p => p.X)) / 2f,
            (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2f);
        var maxDistance = points.Max(p => Vector2.Distance(new Vector2(p.X, p.Y), centerWorld));
        if (maxDistance < 1f)
            maxDistance = 50f;

        var centerScreen = mapOrigin + centerWorld * PixelsPerUnit * zoom;

        foreach (var fraction in RingFractions)
        {
            var radius = maxDistance * fraction * PixelsPerUnit * zoom;
            HudIcons.DrawRingArc(spriteBatch, _pixel, centerScreen, radius, 0f, 360f, Color.SlateGray * 0.22f, 48, 1f);
        }

        var pulse = 0.75f + 0.25f * MathF.Sin(totalSeconds * 1.3f);
        for (var i = 3; i >= 1; i--)
            HudIcons.FillCircle(spriteBatch, _pixel, centerScreen, 3f + i * 3f * pulse, Color.LightYellow * (0.1f * i));
        HudIcons.FillCircle(spriteBatch, _pixel, centerScreen, 4f, Color.LightYellow * 0.9f);
    }

    private static readonly float[] RingFractions = { 0.35f, 0.65f, 1f };

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
