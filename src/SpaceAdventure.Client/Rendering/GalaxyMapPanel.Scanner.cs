using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The console's own round sonar screen: geometry/constants for the porthole itself
// (RadarCircleLocalCenter/Radius, the ship-locked camera, the draggable bearing handle) and the
// visibility mask every other partial's draw calls cull against (IsWithinRadarView/
// IsRingWithinRadarView) - split out of GalaxyMapPanel.cs itself since this is the one cluster of
// state/geometry every other section of the map (ships, stations, field content) has to consult,
// even though only the scanner sweep/pulse visuals below are scanner-specific content.
public sealed partial class GalaxyMapPanel
{
    // Shared with World.Scanner.cs (ScannerConstants) - purely decorative here (drawing how far/
    // wide the cone reaches), the server is what actually decides what the sweep finds. internal,
    // not private: Game1.cs's own MinConsoleZoom clamp (below) needs it too, so the camera can
    // never zoom out past the beam's own real reach.
    internal const float ScannerRangeUnits = ScannerConstants.RangeUnits;
    private const float ScannerSweepHalfAngleDegrees = ScannerConstants.SweepHalfAngleDegrees;
    // Shared with World.Scanner.cs's own ScannerPingCooldownSeconds (M47 follow-up) - purely
    // decorative here (deriving how long ago the last pulse fired from the cooldown alone), the
    // server is what actually gates when the next one is allowed to fire.
    private const float ScannerPingCooldownSeconds = ScannerConstants.PingCooldownSeconds;
    private const float ScannerPingPulseDurationSeconds = 1.3f;

    // The round sonar screen itself (M48 follow-up, matching the reference Barotrauma screenshot):
    // masks a modest housing box down to a circle, then frames it with a rim ring and compass-style
    // tick marks. Panning/clicks read through ComputeShipLockedMapOrigin/GetScannerHandleScreen
    // below rather than a new input model of their own.
    // Sized as an actual widget (M48 follow-up - "сделай этот сонарный круг в виде виджета... чтобы
    // черный экран не занимал весь экран", then "сделай виджет круга сонара больше") - it used to
    // be sized to fill most of the screen, with the bezel mask (below) darkening the ENTIRE canvas
    // outside it; now both the circle and the mask's own bounds are just big enough for the dial
    // itself, and the real ship scene behind it (Game1.cs's own BlockKind.Navigation HUD-overlay
    // case) shows through everywhere else instead of a dead black void.
    // internal, not private, so Game1.cs's own input code can hit-test the handle/rim and clamp
    // zoom against the exact same geometry this Draw() actually renders.
    internal static readonly Vector2 RadarCircleLocalCenter = new(230f, 230f);
    internal const float RadarCircleRadius = 210f;
    // M48 follow-up - "нельзя было камеру отдалить дальше чем визуальное действие лучевого
    // сонара": below this zoom, the world distance the rim's own fixed screen radius covers would
    // exceed the beam's own real reach (ScannerRangeUnits) - Game1.cs's own scroll-zoom handler
    // clamps to this instead of the helm's much smaller unrelated floor.
    internal const float MinConsoleZoom = RadarCircleRadius / (PixelsPerUnit * ScannerRangeUnits);
    // A modest housing border around the circle itself, not the whole design canvas any more.
    // static readonly, not const - RadarCircleLocalCenter is a Vector2, which C# won't fold into a
    // constant expression even though every value here is fixed at startup and never changes again.
    private const float MaskMargin = 40f;
    private static readonly float MaskLeft = RadarCircleLocalCenter.X - RadarCircleRadius - MaskMargin;
    private static readonly float MaskRight = RadarCircleLocalCenter.X + RadarCircleRadius + MaskMargin;
    private static readonly float MaskTop = RadarCircleLocalCenter.Y - RadarCircleRadius - MaskMargin;
    private static readonly float MaskBottom = RadarCircleLocalCenter.Y + RadarCircleRadius + MaskMargin;

    // Console-operator only (pilotView's own free rectangular view has no circular constraint) -
    // true when a screen point actually falls within the visible porthole. Used to CULL map content
    // outright rather than relying solely on DrawRadarBezel's own housing-box mask, which only
    // covers a modest margin around the circle now that it's a compact widget (M48 follow-up bug
    // report - "игрок видит часть карты которая вне сонара... начинает видеть невидимые ранее
    // зоны") - a distant station or belt asteroid can sit well outside even that margin and would
    // otherwise render completely unmasked, right over the real scene behind the widget.
    private static bool IsWithinRadarView(Vector2 panelOrigin, Vector2 screenPoint, bool pilotView) =>
        pilotView || Vector2.Distance(screenPoint, panelOrigin + RadarCircleLocalCenter) <= RadarCircleRadius;

    // Same idea for a ring/arc instead of a point (the warp zone ring, each orbit ring) - these
    // can't be cheaply clipped to the exact circle boundary, so anything whose own radius already
    // dwarfs the porthole is simply skipped outright rather than drawn and partly leaking past the
    // housing box's own modest margin.
    private static bool IsRingWithinRadarView(float ringRadiusPixels, bool pilotView) =>
        pilotView || ringRadiusPixels <= RadarCircleRadius + MaskMargin;

    // The console's own ship-locked camera (M48 follow-up - "привяжи сканер ровно к кораблю"):
    // solves mapOrigin backward so the ship's own screen position always lands exactly on the
    // circle's centre, at any zoom. Shared between Draw() and Game1.cs's own click/drag handling so
    // both agree on where everything actually is on screen.
    // GalaxyMapPanel follow-up - same double-narrowed-before-scaling fix as ComputeMapOrigin's own
    // doc comment describes (its full root-cause writeup lives there).
    internal static Vector2 ComputeShipLockedMapOrigin(Vector2 panelOrigin, Vec2 shipMapPosition, float zoom)
    {
        var scaled = shipMapPosition * (double)PixelsPerUnit * zoom;
        return panelOrigin + RadarCircleLocalCenter - new Vector2((float)scaled.X, (float)scaled.Y);
    }

    // Where the draggable bearing handle sits on the rim for a given sweep angle (M48 follow-up -
    // "по границе круга можно было перетаскивать кнопку") - a fixed screen distance from the ship
    // (RadarCircleRadius), never scaled by zoom: the dial is a physical control on the console's own
    // housing, not a measurement of the world map currently shown inside it.
    internal static Vector2 GetScannerHandleScreen(Vector2 panelOrigin, float sweepDegrees)
    {
        var angle = sweepDegrees * (MathF.PI / 180f);
        return panelOrigin + RadarCircleLocalCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * RadarCircleRadius;
    }

    private const float ScannerHandleRadius = 9f;

    // Game1.cs's own drag-start hit test - a fixed pixel radius around wherever the handle
    // currently sits, a bit more forgiving than the handle's own drawn size so grabbing it doesn't
    // demand pixel-perfect aim (the same allowance HelmButtonsWidget's own drag band gives its
    // title bar).
    internal static bool HitTestScannerHandle(Vector2 screenPoint, Vector2 panelOrigin, float sweepDegrees) =>
        Vector2.Distance(screenPoint, GetScannerHandleScreen(panelOrigin, sweepDegrees)) <= ScannerHandleRadius + 6f;

    private void DrawRadarBezel(SpriteBatch spriteBatch, Vector2 panelOrigin, float totalSeconds)
    {
        var bezelColor = new Color(9, 11, 9);

        // One bezel-coloured span left of the circle and one right of it per row (or one full-width
        // span for rows entirely above/below it) - the same per-row circle math HudIcons.FillCircle
        // already uses, just inverted to paint everything OUTSIDE the circle instead of inside it.
        for (var y = MaskTop; y < MaskBottom; y += 1f)
        {
            var dy = y - RadarCircleLocalCenter.Y;
            var underRadius = RadarCircleRadius * RadarCircleRadius - dy * dy;
            var rowScreenY = (int)(panelOrigin.Y + y);

            if (underRadius <= 0f)
            {
                spriteBatch.Draw(_pixel, new Rectangle((int)(panelOrigin.X + MaskLeft), rowScreenY, (int)(MaskRight - MaskLeft), 1), bezelColor);
                continue;
            }

            var half = MathF.Sqrt(underRadius);
            var leftEdge = RadarCircleLocalCenter.X - half;
            var rightEdge = RadarCircleLocalCenter.X + half;
            spriteBatch.Draw(_pixel, new Rectangle((int)(panelOrigin.X + MaskLeft), rowScreenY, (int)(leftEdge - MaskLeft), 1), bezelColor);
            spriteBatch.Draw(_pixel, new Rectangle((int)(panelOrigin.X + rightEdge), rowScreenY, (int)(MaskRight - rightEdge), 1), bezelColor);
        }

        var center = panelOrigin + RadarCircleLocalCenter;
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, RadarCircleRadius, 0f, 360f, new Color(130, 150, 130), 96, 3f);

        // Compass-style tick marks around the rim, longer every 30 degrees - purely decorative, the
        // same "reads as a sonar screen at a glance" reasoning DrawSystemBackdrop's own rings have.
        for (var degrees = 0; degrees < 360; degrees += 10)
        {
            var major = degrees % 30 == 0;
            var angle = degrees * (MathF.PI / 180f);
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var outer = center + direction * RadarCircleRadius;
            var inner = center + direction * (RadarCircleRadius - (major ? 14f : 7f));
            HudIcons.DrawLine(spriteBatch, _pixel, inner, outer, major ? Color.LightGray * 0.8f : Color.Gray * 0.5f, major ? 2f : 1f);
        }
    }

    // Screen-space hit test for the local player's own scanner contacts (Game1.Input.cs's own
    // click handler while the map is open) - a fixed pixel radius, the same "constant on-screen
    // size regardless of zoom" reasoning GetPointRect's own doc comment gives for point markers.
    public static ScannerContactState? HitTestContact(Vector2 screenPoint, CharacterState me, Vector2 mapOrigin, float zoom)
    {
        const float hitRadiusPixels = 10f;
        foreach (var contact in me.ScannerContacts ?? Array.Empty<ScannerContactState>())
        {
            // Same double-first-then-narrow requirement as ComputeShipLockedMapOrigin's own doc
            // comment (M58 follow-up) - contact.X/Y are KSP-scale, narrowing to float before
            // multiplying by PixelsPerUnit*zoom would already have lost the offset entirely.
            var scaled = new Vec2(contact.X, contact.Y) * (double)PixelsPerUnit * zoom;
            var contactScreen = mapOrigin + new Vector2((float)scaled.X, (float)scaled.Y);
            if (Vector2.Distance(screenPoint, contactScreen) <= hitRadiusPixels)
                return contact;
        }
        return null;
    }

    // The draggable bearing handle itself (M48 follow-up - "кнопка как на скриншоте"): a small round
    // tag sitting right on the rim, coloured the same as the ray/coverage it belongs to so it reads
    // as one control rather than a separate marker.
    private void DrawScannerHandle(SpriteBatch spriteBatch, Vector2 handleScreen, Color color)
    {
        HudIcons.FillCircle(spriteBatch, _pixel, handleScreen, ScannerHandleRadius, color * 0.9f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, handleScreen, ScannerHandleRadius, 0f, 360f, Color.Black * 0.6f, 16, 2f);
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
}
