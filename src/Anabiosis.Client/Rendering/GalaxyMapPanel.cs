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
    private readonly GraphicsDevice _device;
    // The design-canvas area the helm's map fills.
    private readonly Rectangle _bounds;
    // Baked once at startup (MapArt.cs): glows, the star, planet rings, fog cloud, the round screen's shading.
    private readonly Texture2D _glow;
    private readonly Texture2D _flare;
    private readonly Texture2D _corona;
    private readonly Texture2D _sunDisc;
    private readonly Texture2D _heatZone;
    private readonly Texture2D _planetRing;
    private readonly Texture2D _cloudPuff;
    private readonly Texture2D _vignette;
    // Set at the top of every Draw: which of the two looks is being drawn, where the pointer is, where the
    // round screen's centre is, and where the ship is on the map.
    private bool _sonar;
    private readonly SnapshotAge _snapshotAge = new();
    private const float MaxExtrapolationSeconds = 0.1f;
    private Vector2? _pointer;
    private Vector2 _radarCenter;
    private Vec2 _shipMapPosition;
    private WorldSnapshot _shown = null!;
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
        _device = graphicsDevice;
        _bounds = backdrop;
        _glow = MapArt.Glow(graphicsDevice);
        _flare = MapArt.Flare(graphicsDevice);
        _corona = MapArt.Corona(graphicsDevice);
        _sunDisc = MapArt.SunDisc(graphicsDevice);
        _heatZone = MapArt.HeatZone(graphicsDevice);
        _planetRing = MapArt.PlanetRing(graphicsDevice);
        _cloudPuff = MapArt.CloudPuff(graphicsDevice);
        _vignette = MapArt.Vignette(graphicsDevice);
        _font = font;
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

    // One panel drawn two ways. pilotView is the helm's map: the whole canvas, deep space behind it, dragged
    // and zoomed freely, in full colour. Without it this is the navigation console's round screen: a compact
    // instrument locked on the ship, drawn in phosphor green (GalaxyMapPanel.SonarScreen.cs), where the
    // operator aims the scanner beam and drops marks.
    // captainHelmView: true only for the Captain tab's copy of the helm map (Game1.cs) - right mouse there aims
    //   the ship's nose instead of dragging the map, so its hint line says so.
    // batchTransform: the matrix the caller began this SpriteBatch with. The console's screen needs it to clip
    //   its content to the porthole (a scissor rectangle is in device pixels, and the batch has to be begun
    //   again with the same matrix). Without it the content is only culled point by point.
    // pointer: where the mouse is, in the same coordinates as panelOrigin - whatever it is over shows a fuller
    //   name plate.
    // showChrome: the helm's own title, scale bar and legend; off where another screen frames the map itself.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset,
        int myPlayerId, float totalSeconds = 0f, bool pilotView = false, bool captainHelmView = false,
        Matrix? batchTransform = null, Vector2? pointer = null, bool showChrome = true)
    {
        _sonar = !pilotView;
        // The ship's ghost at the destination shows only while the pilot holds the right mouse button to aim the nose (Game1.cs), not all through the flight.
        _aimingNose = captainHelmView && Microsoft.Xna.Framework.Input.Mouse.GetState().RightButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        _pointer = pointer;
        _hoverLabel = null;
        _placedLabels.Clear();
        _shown = snapshot;
        _radarCenter = panelOrigin + RadarCircleLocalCenter;
        _shipMapPosition = snapshot.Voyage.ShipMapPosition;
        UpdateTrail(snapshot);

        var currentSystem = snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId);
        // The field's own centre, not a bounding box of points: the star sits exactly here, it is what
        // CanWarpNow measures distance from, and it is what the helm's free camera centres on by default.
        var fieldCenterReal = new Vec2(currentSystem.Width / 2f, currentSystem.Height / 2f);

        if (pilotView)
        {
            DrawSpace(spriteBatch, currentSystem.Id, panOffset, totalSeconds);
        }
        else
        {
            // The console reads as one compact instrument - a single housing behind the screen, its help text
            // and the faction standings - laid over the real ship scene, which stays visible around it.
            DrawPanelHousing(spriteBatch, panelOrigin);
            BeginSonarClip(spriteBatch, batchTransform);
            DrawSonarBase(spriteBatch, zoom);
        }

        // The console's screen is locked on the ship: the ship is pinned to the centre of the circle and
        // everything else is placed around it; panOffset is not read there. Game1.cs's input code calls the
        // same ComputeShipLockedMapOrigin, so a click lands on whatever is drawn under the cursor.
        var mapOrigin = pilotView
            ? ComputeMapOrigin(_screenCenter, fieldCenterReal, zoom, panOffset)
            : ComputeShipLockedMapOrigin(panelOrigin, snapshot.Voyage.ShipMapPosition, zoom);

        DrawSystemBackdrop(spriteBatch, mapOrigin, fieldCenterReal, zoom, totalSeconds, currentSystem.Id, panelOrigin, pilotView);
        DrawFog(spriteBatch, snapshot, mapOrigin, zoom, totalSeconds); // over the system, under everything the crew knows about

        // The whole edge of the system, not one marker: any position past the warp radius arms the jump.
        var fieldCenterScreen = FieldToScreen(mapOrigin, fieldCenterReal, zoom);
        var warpZoneRadiusPixels = currentSystem.Width / 2f * StarSystem.WarpZoneRadiusFraction * PixelsPerUnit * zoom;
        if (_clipped || IsRingWithinRadarView(warpZoneRadiusPixels, pilotView))
            DrawWarpZoneRing(spriteBatch, fieldCenterScreen, warpZoneRadiusPixels, snapshot.CanWarpNow, totalSeconds);

        DrawPointsOfInterest(spriteBatch, snapshot, mapOrigin, zoom, totalSeconds);
        DrawStations(spriteBatch, snapshot, mapOrigin, zoom);
        FlushLabels(spriteBatch);

        // The ship's reported position only changes when a snapshot arrives (30 a second against 60 frames),
        // which reads as a stutter once the map is zoomed in on the hull. Carrying it forward along its own
        // velocity for as long as this snapshot has been on screen smooths that out. Only while flying free:
        // docked or landed, the reported position is already exact and the velocity means something else.
        // How long it has been on screen is measured here, from the moment this tick was first drawn - never from
        // the difference between the server's tick count and a client clock: that difference carried a stale offset
        // into the next game of the same process (and would drift with any slow server tick), so the marker ran
        // ahead of the real ship by speed times the error - minutes of flight, the moment the ship left the berth.
        // Capped at a few ticks, so nothing can make it run further ahead than that.
        var isFreeFlying = snapshot.Voyage.DockedPointId is null && snapshot.Voyage.LandedBodyId is null;
        var extrapolationSeconds = isFreeFlying ? _snapshotAge.Seconds(snapshot.Tick, totalSeconds, MaxExtrapolationSeconds) : 0f;
        var shipRealPosition = isFreeFlying
            ? snapshot.Voyage.ShipMapPosition + new Vec2(snapshot.ShipField.VelocityX, snapshot.ShipField.VelocityY) * extrapolationSeconds
            : snapshot.Voyage.ShipMapPosition;
        var shipCenter = FieldToScreen(mapOrigin, shipRealPosition, zoom);

        DrawTrail(spriteBatch, mapOrigin, zoom, shipCenter);
        DrawManualMarkers(spriteBatch, snapshot, mapOrigin, zoom);

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == myPlayerId);
        if (me is not null)
        {
            DrawScannerContacts(spriteBatch, snapshot, me, mapOrigin, zoom);
            // Aiming the beam is the console operator's job; the helm's copy of the map only shows results.
            if (!pilotView)
                DrawScannerAim(spriteBatch, me, shipCenter, panelOrigin, zoom, totalSeconds);
        }

        DrawCloseRangeContacts(spriteBatch, snapshot, shipCenter, zoom, panelOrigin, pilotView);
        DrawLargestAsteroidMarkers(spriteBatch, snapshot, mapOrigin, zoom, panelOrigin, pilotView);

        DrawSensorRing(spriteBatch, snapshot, shipCenter, zoom);
        DrawShipMarker(spriteBatch, snapshot, shipCenter, zoom);
        DrawShuttles(spriteBatch, snapshot, mapOrigin, zoom, extrapolationSeconds, totalSeconds);
        DrawCourse(spriteBatch, snapshot, mapOrigin, zoom, shipCenter, totalSeconds);
        FlushHoverLabel(spriteBatch);

        if (pilotView)
        {
            if (showChrome)
                DrawHelmChrome(spriteBatch, currentSystem.Name, zoom, captainHelmView);
        }
        else
        {
            // The glass goes over the picture, then the clip ends and the rim covers the square corners of it.
            // The help text is drawn after the rim: the rim's mask paints whole rows around the circle.
            DrawSonarGlass(spriteBatch, totalSeconds);
            EndSonarClip(spriteBatch);
            DrawRadarBezel(spriteBatch, panelOrigin, totalSeconds);
            DrawRectOutline(spriteBatch, HousingRect(panelOrigin), HousingEdge, 2); // the mask above runs over the housing edge
            DrawConsoleText(spriteBatch, snapshot, panelOrigin + new Vector2(480, 20));
            DrawFactionStandings(spriteBatch, snapshot.FactionStandings, panelOrigin + new Vector2(480, 150));
        }
    }

    // Whether something centred here, reaching this far around itself, can show on the view being drawn.
    private bool IsOnScreen(Vector2 point, float margin)
    {
        if (_sonar)
            return Vector2.Distance(point, _radarCenter) <= RadarCircleRadius + (_clipped ? margin : 0f);
        // The window can be a different shape from the design canvas, so the canvas is not a hard edge.
        const float slack = 200f;
        return point.X >= _bounds.Left - margin - slack && point.X <= _bounds.Right + margin + slack
            && point.Y >= _bounds.Top - margin - slack && point.Y <= _bounds.Bottom + margin + slack;
    }

    // The whole console reads as ONE compact instrument (M48 follow-up - "как будто просто
    // открывается 2 панельки, а не начинает видеть всю карту"): a single dark, bordered housing
    // behind the circle, its own hint text and the faction standings, the same "clearly a terminal,
    // not the real world" framing RackPanel/ReactorPanel already give their own content. Drawn
    // first, well before anything else in this method, so everything else layers on top of it.
    private static readonly Rectangle PanelHousingLocal = new(-20, -20, 900, 520);

    private static readonly Color HousingFill = new(14, 16, 14);
    private static readonly Color HousingEdge = new(90, 100, 90);

    private static Rectangle HousingRect(Vector2 panelOrigin) => new((int)(panelOrigin.X + PanelHousingLocal.X), (int)(panelOrigin.Y + PanelHousingLocal.Y),
        PanelHousingLocal.Width, PanelHousingLocal.Height);

    private void DrawPanelHousing(SpriteBatch spriteBatch, Vector2 panelOrigin)
    {
        var rect = HousingRect(panelOrigin);
        spriteBatch.Draw(_pixel, rect, HousingFill * 0.97f);
        DrawRectOutline(spriteBatch, rect, HousingEdge, 2);
    }

    // The number and its two known thresholds (FactionDefinitions.StandingLabel) already tell the
    // whole story - no separate legend needed for what "42" or "враждебны" means.
    private void DrawFactionStandings(SpriteBatch spriteBatch, IReadOnlyList<FactionStandingState> standings, Vector2 origin)
    {
        spriteBatch.DrawString(_font, "Отношения фракций", origin, new Color(200, 240, 215), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

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
