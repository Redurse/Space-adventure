using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The SYSTEM-level map, shown while the navigation console is open (game_design.md section 5 —
// "маршрут выбирает сам игрок"): the current system's own points of interest, free-form clickable
// anywhere in the field to set a travel destination. Replaces the ship-interior view for the
// moment it's open — there's nowhere else to put a map this size. Jumping to a DIFFERENT system is
// a separate view now (GalacticMapPanel, opened with M from anywhere) - this one only ever shows
// and targets the system the ship is already in.
//
// Split across partials by topic (GalaxyMapPanel.Glyphs.cs's own convention, extended in the same
// session that added hull cameras): this file holds construction, the shared geometry helpers
// (ComputeMapOrigin/ScreenToField/FactionColor), the main Draw() orchestration, and the console's
// own outer housing/faction-standings/rect-outline helpers. GalaxyMapPanel.Scanner.cs holds the
// sonar porthole's own geometry/mask/bezel/handle; GalaxyMapPanel.ShipAndStations.cs holds the
// ship/station schematic markers; GalaxyMapPanel.FieldContent.cs holds the system backdrop/warp
// ring/asteroid markers/close-range contacts.
public sealed partial class GalaxyMapPanel
{
    public const float PixelsPerUnit = 6f;
    public const int PointMarkerSize = 20;

    private readonly Texture2D _pixel;
    // "как в KSP" - planets/moons drawn as lit spheres (HudIcons.DrawShadedSphere) instead of flat
    // FillCircle discs.
    private readonly Texture2D _softCircle;
    private readonly Texture2D _shadedSphere;
    private readonly SpriteFont _font;
    private readonly Starfield _starfield;
    // Where pilotView's free-pan camera anchors the star at panOffset=(0,0) (M52 - "изначально
    // спавнилась в центре солнечной системы на солнце") - the same design-canvas centre the
    // starfield backdrop already fills, not a separately hand-picked point.
    private readonly Vector2 _screenCenter;

    // backdrop: the design-canvas area this panel gets drawn into (it takes over the whole
    // ship-interior viewport while open) - the starfield fills exactly that, same idea as
    // ShipRenderer's own constructor param.
    public GalaxyMapPanel(GraphicsDevice graphicsDevice, SpriteFont font, Rectangle backdrop)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _softCircle = HudIcons.CreateSoftCircleTexture(graphicsDevice);
        _shadedSphere = HudIcons.CreateShadedSphereTexture(graphicsDevice);
        _font = font;
        _starfield = new Starfield(_pixel, backdrop, count: 200);
        _screenCenter = new Vector2(backdrop.Center.X, backdrop.Center.Y);
    }

    // Anchors the STAR (not a bounding box of points, which broke the moment a station could ride
    // along a moving planet - obsolete now, M59, every point is a fixed coordinate) at screenCenter,
    // before the player's own zoom/pan camera is applied on top - used identically by Draw() and by
    // Game1's own zoom-toward-cursor math so both agree on exactly where things land. zoom scales
    // PixelsPerUnit; panOffset is a raw screen-pixel nudge from right-drag (Game1.cs) - both purely
    // a client view, never sent to or read from the server.
    public static Vector2 ComputeMapOrigin(Vector2 screenCenter, Vec2 starPosition, float zoom, Vector2 panOffset)
    {
        var scaled = starPosition * (double)PixelsPerUnit * zoom;
        return screenCenter + panOffset - new Vector2((float)scaled.X, (float)scaled.Y);
    }

    // Inverse of the point-placement transform above - what a click on empty map background
    // actually points at in the system's own field space (game_design.md - free-form destination),
    // rather than one of the fixed markers the map draws.
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

    // pilotView: reused wholesale as the helm's own window 1 (M47 follow-up), where sweeping the
    // beam/dropping a marker isn't available (that stays the scanner operator's own job at the
    // console) and window 3 sits in the same top-right corner the faction-standings box used to
    // have to itself - both get dropped/shortened here rather than fought over with an offset.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset,
        int myPlayerId, float totalSeconds = 0f, bool pilotView = false)
    {
        // M48 follow-up - "задний план это сам корабль, а не карта": the console is a HUD overlay
        // widget now (Game1.cs's own BlockKind.Navigation case), drawn on top of the real ship
        // interior/exterior scene rather than replacing it - a full-canvas starfield here would
        // just paint over that real scene everywhere outside the widget's own small housing. The
        // helm's still-full-screen reused copy (pilotView) keeps it exactly as before.
        if (pilotView)
            _starfield.Draw(spriteBatch, totalSeconds);

        // M48 follow-up - "как будто просто открывается 2 панельки (аналогия - игрок заходит в
        // стеллаж), а не начинает видеть всю карту": the circle, its own hint text and the faction
        // standings used to be three separate things scattered across most of the screen's width,
        // reading as "the real scene got replaced by a wall of map info" rather than "one compact
        // instrument" the way RackPanel/ReactorPanel already read. One shared housing behind all
        // three - drawn first, well before the circle's own content - fixes that without changing
        // what any of them actually show.
        if (!pilotView)
            DrawPanelHousing(spriteBatch, panelOrigin);

        // Moved in against the circle's own right edge, inside the shared housing above, rather
        // than out at panelOrigin+700 in what used to be open real-scene space with nothing telling
        // the eye it belonged to this instrument at all.
        var hintOrigin = panelOrigin + new Vector2(pilotView ? 700 : 480, pilotView ? 12 : 20);
        var hint = pilotView
            ? $"Сканер системы «{snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId).Name}»\nПКМ тащить (сдвиг), колесо (масштаб)"
            : $"Сканер системы «{snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId).Name}»\nтащить ручку по ободу (луч сканера)\nклик по своей метке (поставить на карту)\nза кольцом — прыжок (M), колесо (масштаб)\nE/Esc - войти/выйти";
        // Drawn immediately for the pilot's own plain rectangular copy (no bezel there to cover it),
        // but held back for the console's own round screen until AFTER DrawRadarBezel runs, below -
        // the bezel mask paints full-width rows outside the circle, which used to paint right over
        // this same text and silently hide it (M48 follow-up bug: the round screen swallowed its
        // own hint text).
        if (pilotView)
            spriteBatch.DrawString(_font, hint, hintOrigin, Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        // The console's own round screen is ship-locked (M48 follow-up - "привяжи сканер ровно к
        // кораблю, чтобы в менюшке сканера в центре всегда был корабль"): the ship's own screen
        // position is pinned to the circle's centre and everything else (points, planets, contacts)
        // is placed relative to that, rather than the free-pan/bounding-box camera the pilot's own
        // copy of this map (pilotView, still panned by hand at the helm) keeps using. panOffset is
        // simply not read on this branch - right-drag has nothing left to move here. Game1.cs's own
        // input code calls the same ComputeShipLockedMapOrigin so a click always lands on whatever
        // is actually drawn under the cursor.
        // The field's own centre (StarSystemSummary.Width/Height), not a bounding box of points -
        // the sun sits exactly here (M47 - "солнце было в центре"), the same reference point
        // CanWarpNow itself measures distance from AND (M52) the point pilotView's own free-pan
        // camera anchors at screen-centre by default.
        var currentSystem = snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId);
        var fieldCenterReal = new Vec2(currentSystem.Width / 2f, currentSystem.Height / 2f);
        var mapOrigin = pilotView
            ? ComputeMapOrigin(_screenCenter, fieldCenterReal, zoom, panOffset)
            : ComputeShipLockedMapOrigin(panelOrigin, snapshot.Voyage.ShipMapPosition, zoom);
        DrawSystemBackdrop(spriteBatch, mapOrigin, fieldCenterReal, zoom, totalSeconds, currentSystem.Id, panelOrigin, pilotView);

        // The whole edge of the system, not one specific marker (game_design.md - "круг вокруг
        // системы, откуда можно прыгать"): any position past GalaxyMap.WarpZoneRadius from the
        // field's own centre arms the jump - colored gold once CanWarpNow actually agrees, dim
        // purple otherwise.
        var fieldCenterScreen = FieldToScreen(mapOrigin, fieldCenterReal, zoom);
        var warpZoneRadiusPixels = currentSystem.Width / 2f * StarSystem.WarpZoneRadiusFraction * PixelsPerUnit * zoom;
        if (IsRingWithinRadarView(warpZoneRadiusPixels, pilotView))
            DrawWarpZoneRing(spriteBatch, fieldCenterScreen, warpZoneRadiusPixels, snapshot.CanWarpNow, totalSeconds);

        // M48 follow-up - "оставь на картах в виде значков локаций только станции, все остальные
        // убери": hostile sectors and the old single "asteroid field" marker keep working exactly
        // as before server-side (still catch the ship, still gate battles/refusal), they just
        // don't get a marker drawn for them any more - only known infrastructure (stations) does.
        // Real asteroid presence is shown a different way now anyway (DrawLargestAsteroidMarkers).
        foreach (var point in snapshot.GalaxyPoints)
        {
            if (point.Kind != GalaxyPointKind.Station)
                continue;

            // Every point is a plain fixed coordinate now (M59) - no host body to resolve against.
            var pointScreen = FieldToScreen(mapOrigin, point.Position, zoom);
            var rect = new Rectangle((int)pointScreen.X - PointMarkerSize / 2, (int)pointScreen.Y - PointMarkerSize / 2, PointMarkerSize, PointMarkerSize);
            if (!IsWithinRadarView(panelOrigin, new Vector2(rect.Center.X, rect.Center.Y), pilotView))
                continue;

            var isDocked = point.Id == snapshot.Voyage.DockedPointId;
            var color = Color.SteelBlue;

            // Docked ship and station share the exact same map coordinate by design (World.cs's
            // ShipMapPosition doc comment - there's no real map-space berth offset to plot). Drawn
            // as-is, the two would sit exactly on top of each other once real schematics take over
            // (M48 follow-up bug report - "корабль... налезает на станцию" / "не рисуется" once the
            // ship's OWN marker was hidden instead - the wrong fix). Nudging the STATION's own
            // drawn position aside here - not the ship's, which stays the one fixed point the
            // console's whole camera is locked to - keeps both fully visible without pretending
            // there's a real offset between them. Grows with zoom so it keeps pace with the
            // station's own schematic, which also grows with zoom.
            // Sideways rather than straight up (M48 follow-up - "показывалась сбоку станции где сам
            // стыковочный порт") - reads as "docked alongside" the way the berth actually works,
            // rather than as a station that's mysteriously floating above its own ship.
            // Flush against the ship's own hull, not an arbitrary fixed gap (M48 follow-up - "чтобы
            // выглядело как на 2 скриншоте"): half the ship's own schematic width plus half the
            // station's own, both real world units scaled by the same PixelsPerUnit*zoom the
            // schematics themselves draw at, so the two edges touch exactly at any zoom level.
            var display = rect;
            if (isDocked)
            {
                var shipHalfWidthPixels = ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms).X * PixelsPerUnit * zoom;
                var stationHalfWidthPixels = GetStationHalfWidth(point) * PixelsPerUnit * zoom;
                display.X += (int)(shipHalfWidthPixels + stationHalfWidthPixels);
            }

            // The radius that actually catches the ship by proximity (World.Voyage.cs's
            // TryEngageHostileSector/UpdateNearestStation) - drawn faint and behind the glyph so it
            // reads as "the point's own reach" rather than another clickable marker.
            var captureRadiusPixels = point.CaptureRadius * PixelsPerUnit * zoom;
            HudIcons.DrawRingArc(spriteBatch, _pixel, new Vector2(display.Center.X, display.Center.Y), captureRadiusPixels, 0f, 360f, color * 0.35f, 24, 1.5f);

            // "При приближении выдавали свою настоящую отсековую структуру" (M48 follow-up) - the
            // real Rooms for this specific station (M49 - every station has its own generated
            // shape now, not a shared per-kind template), the same way the ship's own hull
            // schematic already replaces its simple marker once zoomed in far enough to read it.
            if (zoom >= ShipSchematicZoomThreshold)
                DrawStationSchematic(spriteBatch, point, new Vector2(display.Center.X, display.Center.Y), zoom);
            else
                DrawPointGlyph(spriteBatch, point.Kind, display, color, totalSeconds);

            DrawRectOutline(spriteBatch, new Rectangle(display.X - 2, display.Y - 2, display.Width + 4, display.Height + 4), FactionColor(point.Faction), 2);
            if (isDocked)
                DrawRectOutline(spriteBatch, new Rectangle(display.X - 5, display.Y - 5, display.Width + 10, display.Height + 10), Color.LimeGreen, 2);

            spriteBatch.DrawString(_font, $"{point.Name} (станция)", new Vector2(display.X - 10, display.Bottom + 2),
                Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        // M55 follow-up, second pass - "все еще дергается" even after totalSeconds itself became
        // smooth: the ship's OWN reported position (snapshot.Voyage.ShipMapPosition) is nothing but
        // whatever the last-received snapshot said, unchanged for however many render frames pass
        // before the next one arrives (60fps render against a 30/s tick rate - every snapshot is
        // shown for roughly two frames, then jumps a whole tick's worth of real motion at once). At
        // ordinary zoom that jump is a fraction of a pixel; zoomed in on the ship's own hull
        // schematic it can be tens of pixels, which reads exactly as "дергается". Extrapolating
        // along the ship's own reported velocity for however long this exact snapshot has already
        // been on screen (totalSeconds, now smooth, minus the tick this snapshot was actually
        // taken at) turns that once-per-tick jump into the same continuous glide real client
        // prediction always uses - cheap and correct to a small fraction of a tick, the only
        // window this ever needs to cover. Skipped whenever ShipMapPosition is a substituted
        // docked/landed position instead of the ship's own real coordinate - there is no
        // meaningful velocity to extrapolate along in either state anyway.
        const float serverTicksPerSecond = 30f;
        var isFreeFlying = snapshot.Voyage.DockedPointId is null && snapshot.Voyage.LandedBodyId is null;
        var extrapolationSeconds = isFreeFlying ? MathF.Max(0f, totalSeconds - snapshot.Tick / serverTicksPerSecond) : 0f;
        var shipRealPosition = isFreeFlying
            ? snapshot.Voyage.ShipMapPosition + new Vec2(snapshot.ShipField.VelocityX, snapshot.ShipField.VelocityY) * extrapolationSeconds
            : snapshot.Voyage.ShipMapPosition;

        var shipCenter = FieldToScreen(mapOrigin, shipRealPosition, zoom);

        // Shared with the whole crew (World.Scanner.cs, M44) - drawn before the ship/contacts so a
        // pin sitting right on top of a live blip still reads as two separate marks.
        foreach (var marker in snapshot.ManualScannerMarkers)
        {
            var markerScreen = FieldToScreen(mapOrigin, new Vec2(marker.X, marker.Y), zoom);
            if (!IsWithinRadarView(panelOrigin, markerScreen, pilotView))
                continue;
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
                var contactScreen = FieldToScreen(mapOrigin, new Vec2(contact.X, contact.Y), zoom);
                if (!IsWithinRadarView(panelOrigin, contactScreen, pilotView))
                    continue;
                var color = contact.Kind switch
                {
                    NpcShipKind.Cargo => Color.SteelBlue,
                    NpcShipKind.Scout => Color.LightGray,
                    _ => Color.OrangeRed,
                };
                HudIcons.FillCircle(spriteBatch, _pixel, contactScreen, 5f, color * 0.85f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, contactScreen, 8f, 0f, 360f, color, 16, 1.5f);
            }

            // Aiming the dial is still free and continuous even though detecting isn't any more
            // (M47 follow-up) - console-operator only, same as the pulse below - the pilot's own
            // copy of this map (pilotView) doesn't aim (this whole block is skipped there).
            // M48 follow-up - "лучевой сигнал как в баротравме, чтобы по границе круга можно было
            // перетаскивать кнопку": the ray now always reaches exactly the rim (RadarCircleRadius,
            // a fixed screen distance - the dial is a physical control on the console's own housing,
            // not a world-space measurement, so it does NOT scale with the map's own zoom the way
            // the pulse below still does), ending at the same draggable handle Game1.cs's own
            // GetScannerHandleScreen hit-tests against.
            if (!pilotView)
            {
                var isCircular = me.ScannerMode == ScannerMode.Circular;
                var handleScreen = GetScannerHandleScreen(panelOrigin, me.ScannerSweepDegrees);
                var aimColor = isCircular ? Color.DeepSkyBlue : Color.LimeGreen;
                spriteBatch.Draw(_pixel, shipCenter, null, aimColor * 0.7f, me.ScannerSweepDegrees * (MathF.PI / 180f),
                    new Vector2(0f, 0.5f), new Vector2((handleScreen - shipCenter).Length(), 2f), SpriteEffects.None, 0f);
                DrawScannerHandle(spriteBatch, handleScreen, aimColor);

                // The armed coverage - a wedge for Directional, a full ring for Circular (M48
                // follow-up - "не лучевой а круговой... но по кругу") - small and close to the ship
                // rather than reaching the rim, so it doesn't fight with the handle/ray for
                // attention; this is "what shape will detect", not the pulse's own real range.
                const float coverageRadius = 36f;
                if (isCircular)
                    HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, coverageRadius, 0f, 360f, aimColor * 0.3f, 32, 1.5f);
                else
                    HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, coverageRadius,
                        me.ScannerSweepDegrees - ScannerSweepHalfAngleDegrees, me.ScannerSweepDegrees + ScannerSweepHalfAngleDegrees,
                        aimColor * 0.35f, 12, 1.5f);

                // The actual detecting pulse (World.Scanner.cs's FireScannerPing) - a sonar-style
                // expanding wave, shown only for a moment right after the toggle switch fires it,
                // not permanently. Derived from the cooldown alone (no separate "just fired" flag
                // needed): elapsed-since-ping is simply how far the cooldown has already counted
                // down from its own max. Circular reaches only half as far (World.Scanner.cs's
                // CircularScannerRangeUnits) but spends the whole 360 degrees doing it.
                var elapsedSincePing = ScannerPingCooldownSeconds - me.ScannerCooldownRemaining;
                if (elapsedSincePing >= 0f && elapsedSincePing < ScannerPingPulseDurationSeconds)
                {
                    var pulseFraction = elapsedSincePing / ScannerPingPulseDurationSeconds;
                    var pulseAlpha = 1f - pulseFraction;
                    if (isCircular)
                    {
                        var pulseRadiusPixels = pulseFraction * (ScannerRangeUnits / 2f) * PixelsPerUnit * zoom;
                        HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, pulseRadiusPixels, 0f, 360f,
                            Color.DeepSkyBlue * pulseAlpha, 32, 3f);
                    }
                    else
                    {
                        var pulseRadiusPixels = pulseFraction * ScannerRangeUnits * PixelsPerUnit * zoom;
                        HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, pulseRadiusPixels,
                            me.ScannerSweepDegrees - ScannerSweepHalfAngleDegrees, me.ScannerSweepDegrees + ScannerSweepHalfAngleDegrees,
                            Color.LimeGreen * pulseAlpha, 16, 3f);
                    }
                }
            }
        }

        // Everything actually close enough to matter without a scan (M47 follow-up - "в полной
        // близости с кораблем на расстоянии как было раньше"): the same trio HelmPanel's own local
        // radar used to plot before this screen absorbed it, now drawn on the system-wide map
        // instead of a separate small dial. Rocks are still capped to CloseRangeUnits - the belt can
        // hold hundreds of them, and at any zoom level far enough out to see a third of the system
        // they'd be indistinguishable clutter - but an already-engaged squadron or a shell in flight
        // is exactly as visible here as it always was, not something a sweep has to find first.
        DrawCloseRangeContacts(spriteBatch, snapshot, shipCenter, zoom, panelOrigin, pilotView);
        DrawLargestAsteroidMarkers(spriteBatch, snapshot, mapOrigin, zoom, panelOrigin, pilotView);

        DrawShipMarker(spriteBatch, snapshot, shipCenter, zoom);

        // Masked down to a round porthole last (M48 follow-up - "круговой обзор был только на
        // сканере а в штурвале его не было"): the console operator's own screen only - the pilot's
        // reused copy (pilotView) keeps the plain rectangular view it always had, on top of
        // everything drawn above but before the standings that sit on the console's own housing
        // rather than behind its screen.
        if (!pilotView)
        {
            DrawRadarBezel(spriteBatch, panelOrigin, totalSeconds);
            spriteBatch.DrawString(_font, hint, hintOrigin, Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
            DrawFactionStandings(spriteBatch, snapshot.FactionStandings, panelOrigin + new Vector2(480, 150));
        }
    }

    // The whole console reads as ONE compact instrument (M48 follow-up - "как будто просто
    // открывается 2 панельки, а не начинает видеть всю карту"): a single dark, bordered housing
    // behind the circle, its own hint text and the faction standings, the same "clearly a terminal,
    // not the real world" framing RackPanel/ReactorPanel already give their own content. Drawn
    // first, well before anything else in this method, so everything else layers on top of it.
    private static readonly Rectangle PanelHousingLocal = new(-20, -20, 900, 520);

    private void DrawPanelHousing(SpriteBatch spriteBatch, Vector2 panelOrigin)
    {
        var rect = new Rectangle((int)(panelOrigin.X + PanelHousingLocal.X), (int)(panelOrigin.Y + PanelHousingLocal.Y),
            PanelHousingLocal.Width, PanelHousingLocal.Height);
        spriteBatch.Draw(_pixel, rect, new Color(14, 16, 14) * 0.97f);
        DrawRectOutline(spriteBatch, rect, new Color(90, 100, 90), 2);
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
