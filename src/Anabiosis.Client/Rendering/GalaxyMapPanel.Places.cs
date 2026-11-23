using System;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Everything on the map that has a name: stations, the places the crew has found (PointOfInterest) and the
// marks the scanner operator drops. They all share one look - a round badge ringed in the owner's colour with
// a glyph inside, and a name plate under it. The plate of whatever the pointer is over grows a second line
// (what it is, how far) and is drawn last, on top of its neighbours.
public sealed partial class GalaxyMapPanel
{
    private static readonly Color PlateFill = new(8, 12, 18);
    private static readonly Color TitleInk = new(232, 238, 245);
    private static readonly Color DetailInk = new(150, 166, 184);
    private static readonly Color GlyphInk = new(198, 214, 232);
    private static readonly Color SignalColor = new(150, 190, 205);
    private const float BadgeRadius = 11f;
    private const float HoverRadius = 15f;

    private (Vector2 Top, string Title, string? Detail, Color Accent)? _hoverLabel;
    // Plates already on the map this frame: a new one that would land on an older one is moved down past it.
    private readonly System.Collections.Generic.List<Rectangle> _placedLabels = new();

    private void DrawGlow(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, float strength)
    {
        if (radius < 1f)
            return;
        spriteBatch.Draw(_glow, center, null, MapArt.Additive(Ink(color), strength), 0f, new Vector2(64f, 64f), radius * 2f / 128f, SpriteEffects.None, 0f);
    }

    // Only one thing is under the pointer at a time: the first one drawn that claims it.
    private bool IsHovered(Vector2 center, float radius = HoverRadius) =>
        _pointer is { } pointer && _hoverLabel is null && Vector2.Distance(pointer, center) <= radius;

    private static Rectangle GlyphRect(Vector2 center) => new((int)center.X - 6, (int)center.Y - 6, 12, 12);

    private void DrawBadge(SpriteBatch spriteBatch, Vector2 center, Color accent, bool hovered)
    {
        DrawGlow(spriteBatch, center, BadgeRadius * (hovered ? 2.7f : 2f), accent, hovered ? 0.6f : 0.3f);
        HudIcons.DrawScaledCircle(spriteBatch, _softCircle, center, BadgeRadius, Ink(PlateFill) * 0.92f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, BadgeRadius - 0.5f, 0f, 360f, Ink(accent), 28, hovered ? 2.2f : 1.6f);
    }

    // topCenter is the middle of the plate's top edge. A hovered plate is held back and drawn by
    // FlushHoverLabel once everything else is on the map.
    private void DrawLabel(SpriteBatch spriteBatch, Vector2 topCenter, string title, string? detail, Color accent, bool hovered)
    {
        if (hovered)
        {
            _hoverLabel = (topCenter, title, detail, accent);
            return;
        }
        _pendingLabels.Add((topCenter, title, accent));
    }

    // Plain plates go on after every badge is down, so no badge lands on top of a neighbour's name.
    private readonly System.Collections.Generic.List<(Vector2 Top, string Title, Color Accent)> _pendingLabels = new();

    private void FlushLabels(SpriteBatch spriteBatch)
    {
        foreach (var (top, title, accent) in _pendingLabels)
            DrawPlate(spriteBatch, top, title, null, accent, emphasised: false);
        _pendingLabels.Clear();
    }

    private void FlushHoverLabel(SpriteBatch spriteBatch)
    {
        if (_hoverLabel is { } label)
            DrawPlate(spriteBatch, label.Top, label.Title, label.Detail, label.Accent, emphasised: true);
        _hoverLabel = null;
    }

    private void DrawPlate(SpriteBatch spriteBatch, Vector2 topCenter, string title, string? detail, Color accent, bool emphasised)
    {
        const float titleScale = 0.5f;
        const float detailScale = 0.42f;
        var titleSize = _font.MeasureString(title) * titleScale;
        var detailSize = detail is null ? Vector2.Zero : _font.MeasureString(detail) * detailScale;
        var width = MathF.Max(titleSize.X, detailSize.X) + 13f;
        var height = titleSize.Y + detailSize.Y + 4f;
        var rect = new Rectangle((int)(topCenter.X - width / 2f), (int)topCenter.Y, (int)MathF.Ceiling(width), (int)MathF.Ceiling(height));
        if (!emphasised)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var blocker = _placedLabels.FindIndex(placed => placed.Intersects(rect));
                if (blocker < 0)
                    break;
                rect.Y = _placedLabels[blocker].Bottom + 2;
            }
            _placedLabels.Add(rect);
        }
        spriteBatch.Draw(_pixel, rect, Ink(PlateFill) * (emphasised ? 0.94f : 0.7f));
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), Ink(accent));
        if (emphasised)
            DrawRectOutline(spriteBatch, rect, Ink(accent) * 0.55f, 1);
        spriteBatch.DrawString(_font, title, new Vector2(rect.X + 8, rect.Y + 2), Ink(TitleInk), 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        if (detail is not null)
            spriteBatch.DrawString(_font, detail, new Vector2(rect.X + 8, rect.Y + 2 + titleSize.Y), Ink(DetailInk), 0f, Vector2.Zero, detailScale, SpriteEffects.None, 0f);
    }

    private static string FormatDistance(double units) => units >= 1000.0
        ? (units / 1000.0).ToString("0.0", CultureInfo.InvariantCulture).Replace('.', ',') + " тыс. ед."
        : ((int)Math.Round(units / 10.0) * 10).ToString(CultureInfo.InvariantCulture) + " ед.";

    private string DistanceFromShip(Vec2 position) => FormatDistance((position - _shipMapPosition).Length()) + " от корабля";

    private static string StationKindName(StationKind kind) => kind switch
    {
        StationKind.Trade => "Торговая станция",
        StationKind.Military => "Военная база",
        StationKind.Mining => "Шахтёрская база",
        StationKind.Shipyard => "Верфь",
        StationKind.Research => "Научная станция",
        _ => "Станция",
    };

    private static float GetStationHalfHeight(GalaxyPoint point)
    {
        var station = GetOrBuildSchematic(point);
        return station.Rooms.Count == 0 ? 0f : (station.Rooms.Max(r => r.Bottom) - station.Rooms.Min(r => r.Top)) / 2f;
    }

    private void DrawStations(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, float zoom)
    {
        var scale = PixelsPerUnit * zoom;
        foreach (var point in snapshot.GalaxyPoints)
        {
            // Only known infrastructure gets a marker; hostile sectors still work server-side without one.
            if (point.Kind != GalaxyPointKind.Station)
                continue;
            var center = FieldToScreen(mapOrigin, point.Position, zoom);

            // A docked ship and its station share one map coordinate, so the station is drawn pushed aside by
            // half of each hull - flush against the ship, the way the berth actually sits - rather than on top
            // of it. The ship stays put: it is the point the console's camera is locked to.
            var isDocked = point.Id == snapshot.Voyage.DockedPointId;
            // Close in, a docked station is drawn where it REALLY is: its rooms are in the ship's own frame (the docked frame), so they
            // go through exactly the ship's transform - the ship then sits against the berth it is mated to (the start outpost is
            // entered through its N/S ports, so a plain "to the right, centres level" nudge showed the ship at the wrong height).
            var realDockedLayout = isDocked && zoom >= ShipSchematicZoomThreshold && snapshot.Rooms.Count > 0 && snapshot.Station.Rooms.Count > 0;
            Vector2 dockedHullCenterOnScreen = default;
            if (realDockedLayout)
            {
                var minX = snapshot.Station.Rooms.Min(r => r.Left);
                var maxX = snapshot.Station.Rooms.Max(r => r.Right);
                var minY = snapshot.Station.Rooms.Min(r => r.Top);
                var maxY = snapshot.Station.Rooms.Max(r => r.Bottom);
                var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
                dockedHullCenterOnScreen = FieldToScreen(mapOrigin, snapshot.Voyage.ShipMapPosition, zoom);
                center = dockedHullCenterOnScreen + new Vector2((minX + maxX) / 2f - (float)hullCenter.X, (minY + maxY) / 2f - (float)hullCenter.Y) * scale;
            }
            else if (isDocked)
                center.X += (float)(ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms).X + GetStationHalfWidth(point)) * scale;
            if (!IsOnScreen(center, 60f))
                continue;

            var accent = FactionColor(point.Faction);
            var hovered = IsHovered(center);

            // How close the ship has to come for the station to catch it.
            var reach = point.CaptureRadius * scale;
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, reach, 0f, 360f, Ink(accent * 0.28f), (int)Math.Clamp(reach / 4f, 24f, 160f), 1.2f);

            float below;
            if (zoom >= ShipSchematicZoomThreshold)
            {
                // Close in, the station shows its real rooms instead of a badge, like the ship does.
                if (realDockedLayout)
                {
                    DrawRealDockedStation(spriteBatch, snapshot, dockedHullCenterOnScreen, zoom);
                    below = snapshot.Station.Rooms.Max(r => r.Bottom) - snapshot.Station.Rooms.Min(r => r.Top);
                    below = below / 2f * scale + 6f;
                }
                else
                {
                    DrawStationSchematic(spriteBatch, point, center, zoom);
                    below = GetStationHalfHeight(point) * scale + 6f;
                }
            }
            else
            {
                DrawBadge(spriteBatch, center, accent, hovered);
                DrawStationGlyph(spriteBatch, GlyphRect(center), Ink(GlyphInk));
                if (isDocked)
                    HudIcons.DrawRingArc(spriteBatch, _pixel, center, BadgeRadius + 3.5f, 0f, 360f, Ink(Color.LimeGreen), 32, 1.6f);
                below = BadgeRadius + 5f;
            }

            var owner = snapshot.FactionStandings.FirstOrDefault(f => f.Faction == point.Faction)?.Name;
            var detail = StationKindName(point.StationKind) + (owner is null ? "" : " · " + owner)
                + "\n" + (isDocked ? "корабль пристыкован" : DistanceFromShip(point.Position));
            DrawLabel(spriteBatch, center + new Vector2(0f, below), point.Name, detail, accent, hovered);
        }
    }

    private void DrawPointsOfInterest(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, float zoom, float totalSeconds)
    {
        foreach (var poi in snapshot.PointsOfInterest ?? Array.Empty<PoiState>())
        {
            if (poi.Looted)
                continue;
            var center = FieldToScreen(mapOrigin, poi.Position, zoom);
            if (!IsOnScreen(center, 60f))
                continue;
            var hovered = IsHovered(center);

            if (poi.Kind is not { } kind)
            {
                // Something the sonar caught but nobody has been close enough to name: a pulsing question mark.
                var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 3f);
                DrawBadge(spriteBatch, center, SignalColor * (0.55f + 0.45f * pulse), hovered);
                HudIcons.DrawRingArc(spriteBatch, _pixel, center, BadgeRadius + 3f + 5f * pulse, 0f, 360f, Ink(SignalColor) * (0.45f * (1f - pulse)), 28, 1.2f);
                var mark = _font.MeasureString("?") * 0.62f;
                spriteBatch.DrawString(_font, "?", center - mark / 2f, Ink(SignalColor), 0f, Vector2.Zero, 0.62f, SpriteEffects.None, 0f);
                DrawLabel(spriteBatch, center + new Vector2(0f, BadgeRadius + 5f), poi.Name,
                    "подлетите ближе, чтобы опознать\n" + DistanceFromShip(poi.Position), SignalColor, hovered);
                continue;
            }

            var color = kind switch
            {
                PoiKind.AsteroidCluster => new Color(206, 168, 116),
                PoiKind.AbandonedStation => new Color(156, 164, 176), // a station, but a dead grey one
                _ => Color.Goldenrod,
            };
            var reach = poi.Radius * PixelsPerUnit * zoom;
            if (reach > BadgeRadius * 1.4f)
                for (var i = 0; i < 24; i++)
                    HudIcons.DrawRingArc(spriteBatch, _pixel, center, reach, i * 15f, i * 15f + 8f, Ink(color * 0.4f), 3, 1.3f);
            DrawBadge(spriteBatch, center, color, hovered);
            DrawPointGlyph(spriteBatch, GlyphKindFor(kind), GlyphRect(center), Ink(color), totalSeconds);
            DrawLabel(spriteBatch, center + new Vector2(0f, BadgeRadius + 5f), poi.Name, DistanceFromShip(poi.Position), color, hovered);
        }
    }

    internal static GalaxyPointKind GlyphKindFor(PoiKind kind) => kind switch
    {
        PoiKind.StoragePod => GalaxyPointKind.StoragePod,
        PoiKind.AbandonedShip or PoiKind.Wreck => GalaxyPointKind.AbandonedShip,
        PoiKind.ShipGraveyard => GalaxyPointKind.ShipGraveyard,
        PoiKind.AbandonedStation => GalaxyPointKind.Station,
        _ => GalaxyPointKind.AsteroidField,
    };

    private void DrawManualMarkers(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, float zoom)
    {
        foreach (var marker in snapshot.ManualScannerMarkers)
        {
            var screen = FieldToScreen(mapOrigin, new Vec2(marker.X, marker.Y), zoom);
            if (!IsOnScreen(screen, 20f))
                continue;
            DrawGlow(spriteBatch, screen, 14f, Color.Gold, 0.4f);
            DrawGlowDiamond(spriteBatch, screen, 10f, Ink(Color.Gold));
            spriteBatch.DrawString(_font, "метка", screen + new Vector2(8, -6), Ink(Color.Gold), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }
    }
}
