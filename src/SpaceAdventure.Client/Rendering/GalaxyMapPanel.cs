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

    // Must match World.Scanner.cs's own ScannerRangeUnits/ScannerSweepHalfAngleDegrees - purely
    // decorative here (drawing how far/wide the cone reaches), the server is what actually decides
    // what the sweep finds.
    private const float ScannerRangeUnits = 900f;
    private const float ScannerSweepHalfAngleDegrees = 12f;

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset,
        int myPlayerId, float totalSeconds = 0f)
    {
        _starfield.Draw(spriteBatch, totalSeconds);

        spriteBatch.DrawString(_font, $"Карта системы «{snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId).Name}» - ЛКМ тащить (луч сканера), клик по своей метке (поставить на общую карту), за кольцом — прыжок (M), ПКМ тащить (сдвиг), колесо (масштаб)",
            panelOrigin + new Vector2(0, -24), Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var mapOrigin = ComputeMapOrigin(panelOrigin, snapshot.GalaxyPoints, zoom, panOffset);
        DrawSystemBackdrop(spriteBatch, snapshot.GalaxyPoints, mapOrigin, zoom, totalSeconds);

        // The whole edge of the system, not one specific marker (game_design.md - "круг вокруг
        // системы, откуда можно прыгать"): any position past GalaxyMap.WarpZoneRadius from the
        // field's own centre (StarSystemSummary.Width/Height, not the points' bounding box
        // DrawSystemBackdrop uses for its purely decorative rings) arms the jump - colored gold
        // once CanWarpNow actually agrees, dim purple otherwise.
        var currentSystem = snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId);
        var fieldCenterScreen = mapOrigin + new Vector2(currentSystem.Width / 2f, currentSystem.Height / 2f) * PixelsPerUnit * zoom;
        var warpZoneRadiusPixels = GalaxyMap.WarpZoneRadius * PixelsPerUnit * zoom;
        DrawWarpZoneRing(spriteBatch, fieldCenterScreen, warpZoneRadiusPixels, snapshot.CanWarpNow, totalSeconds);

        foreach (var point in snapshot.GalaxyPoints)
        {
            var rect = GetPointRect(point, mapOrigin, zoom);
            var isDocked = point.Id == snapshot.Voyage.DockedPointId;
            var color = point.Kind == GalaxyPointKind.Station ? Color.SteelBlue
                : point.Kind == GalaxyPointKind.AsteroidField ? Color.SaddleBrown
                : Color.OrangeRed;

            // The radius that actually catches the ship by proximity (World.Voyage.cs's
            // TryEngageHostileSector/UpdateNearestStation) - drawn faint and behind the glyph so it
            // reads as "the point's own reach" rather than another clickable marker.
            var captureRadiusPixels = point.CaptureRadius * PixelsPerUnit * zoom;
            HudIcons.DrawRingArc(spriteBatch, _pixel, new Vector2(rect.Center.X, rect.Center.Y), captureRadiusPixels, 0f, 360f, color * 0.35f, 24, 1.5f);

            DrawPointGlyph(spriteBatch, point.Kind, rect, color, totalSeconds);
            DrawRectOutline(spriteBatch, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), FactionColor(point.Faction), 2);
            if (isDocked)
                DrawRectOutline(spriteBatch, new Rectangle(rect.X - 5, rect.Y - 5, rect.Width + 10, rect.Height + 10), Color.LimeGreen, 2);

            var kindLabel = point.Kind switch
            {
                GalaxyPointKind.Station => "станция",
                GalaxyPointKind.AsteroidField => "пояс астероидов",
                _ => "враждебный сектор",
            };
            spriteBatch.DrawString(_font, $"{point.Name} ({kindLabel})", new Vector2(rect.X - 10, rect.Bottom + 2),
                Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        var shipCenter = mapOrigin + new Vector2(snapshot.Voyage.ShipMapPosition.X, snapshot.Voyage.ShipMapPosition.Y) * PixelsPerUnit * zoom;

        // Shared with the whole crew (World.Scanner.cs, M44) - drawn before the ship/contacts so a
        // pin sitting right on top of a live blip still reads as two separate marks.
        foreach (var marker in snapshot.ManualScannerMarkers)
        {
            var markerScreen = mapOrigin + new Vector2(marker.X, marker.Y) * PixelsPerUnit * zoom;
            DrawGlowDiamond(spriteBatch, markerScreen, 10f, Color.Gold);
            spriteBatch.DrawString(_font, "метка", markerScreen + new Vector2(8, -6), Color.Gold, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == myPlayerId);
        if (me is not null)
        {
            // Private to this player (World.Scanner.cs) - frozen at each hull's own last-known
            // position, not necessarily where it actually is right now.
            foreach (var contact in me.ScannerContacts ?? Array.Empty<ScannerContactState>())
            {
                var contactScreen = mapOrigin + new Vector2(contact.X, contact.Y) * PixelsPerUnit * zoom;
                var color = contact.Kind switch
                {
                    NpcShipKind.Cargo => Color.SteelBlue,
                    NpcShipKind.Scout => Color.LightGray,
                    _ => Color.OrangeRed,
                };
                HudIcons.FillCircle(spriteBatch, _pixel, contactScreen, 5f, color * 0.85f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, contactScreen, 8f, 0f, 360f, color, 16, 1.5f);
            }

            // The sweep cone itself, from the ship's own map position - purely a client-side
            // picture of ScannerSweepDegrees; the server (World.Scanner.cs) is the one actually
            // deciding what it finds. A filled fan (centre plus points along the outer arc), the
            // same "sample points around an arc" shape DrawRingArc already uses for a stroke,
            // just closed back to the centre instead of left open.
            var sweepRadiusPixels = ScannerRangeUnits * PixelsPerUnit * zoom;
            const int sweepSegments = 16;
            var fan = new Vector2[sweepSegments + 2];
            fan[0] = shipCenter;
            for (var i = 0; i <= sweepSegments; i++)
            {
                var angle = (me.ScannerSweepDegrees - ScannerSweepHalfAngleDegrees +
                    2f * ScannerSweepHalfAngleDegrees * i / sweepSegments) * (MathF.PI / 180f);
                fan[i + 1] = shipCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * sweepRadiusPixels;
            }
            Primitives.FillPolygon(spriteBatch, _pixel, shipCenter, fan, Color.LimeGreen * 0.12f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, sweepRadiusPixels,
                me.ScannerSweepDegrees - ScannerSweepHalfAngleDegrees, me.ScannerSweepDegrees + ScannerSweepHalfAngleDegrees,
                Color.LimeGreen * 0.5f, 16, 2f);
        }

        spriteBatch.Draw(_pixel, new Rectangle((int)shipCenter.X - 4, (int)shipCenter.Y - 4, 8, 8), Color.White);

        DrawFactionStandings(spriteBatch, snapshot.FactionStandings, panelOrigin + new Vector2(700, 0));
    }

    // Screen-space hit test for the local player's own scanner contacts (Game1.Input.cs's own
    // click handler while the map is open) - a fixed pixel radius, the same "constant on-screen
    // size regardless of zoom" reasoning GetPointRect's own doc comment gives for point markers.
    public static ScannerContactState? HitTestContact(Vector2 screenPoint, CharacterState me, Vector2 mapOrigin, float zoom)
    {
        const float hitRadiusPixels = 10f;
        foreach (var contact in me.ScannerContacts ?? Array.Empty<ScannerContactState>())
        {
            var contactScreen = mapOrigin + new Vector2(contact.X, contact.Y) * PixelsPerUnit * zoom;
            if (Vector2.Distance(screenPoint, contactScreen) <= hitRadiusPixels)
                return contact;
        }
        return null;
    }

    private void DrawGlowDiamond(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
    {
        var half = size / 2f;
        var points = new[]
        {
            center + new Vector2(0, -half), center + new Vector2(half, 0),
            center + new Vector2(0, half), center + new Vector2(-half, 0),
        };
        Primitives.FillPolygon(spriteBatch, _pixel, center, points, color * 0.85f);
        Primitives.StrokePolygon(spriteBatch, _pixel, points, Color.Black * 0.5f, 1.5f);
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

    // A pair of ring arcs spinning opposite ways at the size of the whole warp zone - the same
    // "portal you could actually fall through" idea a single WarpPoint marker used to have, just
    // scaled up to the size of the boundary it now represents instead of one small icon.
    private void DrawWarpZoneRing(SpriteBatch spriteBatch, Vector2 center, float radius, bool armed, float totalSeconds)
    {
        var color = armed ? Color.Gold : Color.MediumPurple;
        var spinOuter = totalSeconds * 20f % 360f;
        var spinInner = -totalSeconds * 28f % 360f;
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius, spinOuter, spinOuter + 300f, color * 0.55f, 64, 2.5f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius * 0.985f, spinInner, spinInner + 300f, Color.White * 0.35f, 64, 1.5f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
