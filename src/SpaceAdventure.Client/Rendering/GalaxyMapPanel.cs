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

    // Fixed relative to the panel's own origin, not the map camera - same reasoning GetPointRect's
    // own doc comment gives for keeping markers a constant on-screen size, just for a HUD button
    // instead of a world marker. Console-operator only (Draw's own pilotView gate).
    public static Rectangle GetScanButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + ScanButtonRectLocal.X, (int)panelOrigin.Y + ScanButtonRectLocal.Y,
            ScanButtonRectLocal.Width, ScanButtonRectLocal.Height);

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
    // Must match World.Scanner.cs's own ScannerPingCooldownSeconds (M47 follow-up) - purely
    // decorative here (deriving how long ago the last pulse fired from the cooldown alone), the
    // server is what actually gates when the next one is allowed to fire.
    private const float ScannerPingCooldownSeconds = 15f;
    private const float ScannerPingPulseDurationSeconds = 1.3f;
    private static readonly Rectangle ScanButtonRectLocal = new(0, -55, 140, 30);

    // pilotView: reused wholesale as the helm's own window 1 (M47 follow-up), where sweeping the
    // beam/dropping a marker isn't available (that stays the scanner operator's own job at the
    // console) and window 3 sits in the same top-right corner the faction-standings box used to
    // have to itself - both get dropped/shortened here rather than fought over with an offset.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset,
        int myPlayerId, float totalSeconds = 0f, bool pilotView = false)
    {
        _starfield.Draw(spriteBatch, totalSeconds);

        var hint = pilotView
            ? $"Сканер системы «{snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId).Name}» - ПКМ тащить (сдвиг), колесо (масштаб)"
            : $"Сканер системы «{snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId).Name}» - ЛКМ тащить (луч сканера), клик по своей метке (поставить на общую карту), за кольцом — прыжок (M), ПКМ тащить (сдвиг), колесо (масштаб), E/Esc - войти/выйти";
        spriteBatch.DrawString(_font, hint, panelOrigin + new Vector2(0, -24), Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var mapOrigin = ComputeMapOrigin(panelOrigin, snapshot.GalaxyPoints, zoom, panOffset);

        // The field's own centre (StarSystemSummary.Width/Height), not the points' own bounding
        // box - the sun sits exactly here (M47 - "солнце было в центре"), the same reference point
        // CanWarpNow itself measures distance from, so the warp ring drawn around it below lines up
        // with the same spot the server actually gates the jump on, rather than wherever this
        // system's own points happen to average out to (which drifted off-centre once they were
        // spread out to use the field's full size instead of huddling near its middle).
        var currentSystem = snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId);
        var fieldCenterScreen = mapOrigin + new Vector2(currentSystem.Width / 2f, currentSystem.Height / 2f) * PixelsPerUnit * zoom;
        DrawSystemBackdrop(spriteBatch, fieldCenterScreen, zoom, totalSeconds);

        // The whole edge of the system, not one specific marker (game_design.md - "круг вокруг
        // системы, откуда можно прыгать"): any position past GalaxyMap.WarpZoneRadius from the
        // field's own centre arms the jump - colored gold once CanWarpNow actually agrees, dim
        // purple otherwise.
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

            // Aiming the dial is still free and continuous even though detecting isn't any more
            // (M47 follow-up) - a short ray at the console's own bearing, so there's still
            // something to see moving while dragging it, without implying the whole cone is
            // actively detecting the way the old permanent fan did. Console-operator only, same as
            // the button/pulse below - the pilot's own copy of this map (pilotView) doesn't aim.
            if (!pilotView)
            {
                const float aimRayLength = 70f;
                var aimAngle = me.ScannerSweepDegrees * (MathF.PI / 180f);
                var aimEnd = shipCenter + new Vector2(MathF.Cos(aimAngle), MathF.Sin(aimAngle)) * aimRayLength * zoom;
                spriteBatch.Draw(_pixel, shipCenter, null, Color.LimeGreen * 0.7f, aimAngle, new Vector2(0f, 0.5f),
                    new Vector2((aimEnd - shipCenter).Length(), 2f), SpriteEffects.None, 0f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, aimRayLength * zoom,
                    me.ScannerSweepDegrees - ScannerSweepHalfAngleDegrees, me.ScannerSweepDegrees + ScannerSweepHalfAngleDegrees,
                    Color.LimeGreen * 0.35f, 12, 1.5f);

                // The actual detecting pulse (World.Scanner.cs's FireScannerPing) - a sonar-style
                // expanding wave along the cone, shown only for a moment right after the "Скан"
                // button fires it, not permanently. Derived from the cooldown alone (no separate
                // "just fired" flag needed): elapsed-since-ping is simply how far the cooldown has
                // already counted down from its own max.
                var elapsedSincePing = ScannerPingCooldownSeconds - me.ScannerCooldownRemaining;
                if (elapsedSincePing >= 0f && elapsedSincePing < ScannerPingPulseDurationSeconds)
                {
                    var pulseFraction = elapsedSincePing / ScannerPingPulseDurationSeconds;
                    var pulseRadiusPixels = pulseFraction * ScannerRangeUnits * PixelsPerUnit * zoom;
                    var pulseAlpha = 1f - pulseFraction;
                    HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, pulseRadiusPixels,
                        me.ScannerSweepDegrees - ScannerSweepHalfAngleDegrees, me.ScannerSweepDegrees + ScannerSweepHalfAngleDegrees,
                        Color.LimeGreen * pulseAlpha, 16, 3f);
                }

                DrawScanButton(spriteBatch, panelOrigin, me.ScannerCooldownRemaining);
            }
        }

        // Everything actually close enough to matter without a scan (M47 follow-up - "в полной
        // близости с кораблем на расстоянии как было раньше"): the same trio HelmPanel's own local
        // radar used to plot before this screen absorbed it, now drawn on the system-wide map
        // instead of a separate small dial. Rocks are still capped to CloseRangeUnits - the belt can
        // hold hundreds of them, and at any zoom level far enough out to see a third of the system
        // they'd be indistinguishable clutter - but an already-engaged squadron or a shell in flight
        // is exactly as visible here as it always was, not something a sweep has to find first.
        DrawCloseRangeContacts(spriteBatch, snapshot, mapOrigin, shipCenter, zoom);

        spriteBatch.Draw(_pixel, new Rectangle((int)shipCenter.X - 4, (int)shipCenter.Y - 4, 8, 8), Color.White);

        if (!pilotView)
            DrawFactionStandings(spriteBatch, snapshot.FactionStandings, panelOrigin + new Vector2(700, 0));
    }

    // The scanner's own trigger (M47 follow-up - "с перезарядкой... при нажатии на кнопку скан") -
    // ready and clickable at 0 cooldown, otherwise shown as a plain countdown with no click to give.
    private void DrawScanButton(SpriteBatch spriteBatch, Vector2 panelOrigin, float cooldownRemaining)
    {
        var rect = GetScanButtonRect(panelOrigin);
        var ready = cooldownRemaining <= 0f;
        spriteBatch.Draw(_pixel, rect, ready ? Color.SeaGreen : new Color(50, 50, 50));
        var label = ready ? "[Клик] СКАН" : $"Скан: {cooldownRemaining:0.0}с";
        spriteBatch.DrawString(_font, label, new Vector2(rect.X + 6, rect.Y + 7), ready ? Color.White : Color.Gray,
            0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Matches the old HelmPanel.RadarRangeUnits exactly (M47 - "как было раньше") - the pilot's
    // close-in situational awareness didn't get any better or worse when it moved onto this map,
    // just bigger and shared with the long-range scanner picture.
    private const float CloseRangeUnits = 50f;

    private void DrawCloseRangeContacts(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, Vector2 shipCenter, float zoom)
    {
        var shipWorldPos = new Vector2(snapshot.ShipField.X, snapshot.ShipField.Y);

        foreach (var asteroid in snapshot.Field.Asteroids)
        {
            if ((new Vector2(asteroid.X, asteroid.Y) - shipWorldPos).Length() > CloseRangeUnits)
                continue;

            var outline = AsteroidShape.Outline(asteroid);
            var points = new Vector2[outline.Length];
            for (var i = 0; i < outline.Length; i++)
                points[i] = mapOrigin + new Vector2(outline[i].X, outline[i].Y) * PixelsPerUnit * zoom;

            var center = mapOrigin + new Vector2(asteroid.X, asteroid.Y) * PixelsPerUnit * zoom;
            Primitives.FillPolygon(spriteBatch, _pixel, center, points, new Color(96, 74, 56));
            Primitives.StrokePolygon(spriteBatch, _pixel, points, new Color(150, 120, 92));
        }

        // A squadron already fighting the player, or a shell already in flight, is not intel to be
        // discovered - it exists because the player is right there, so it's drawn unconditionally
        // rather than gated on CloseRangeUnits or a scan (same reasoning HelmPanel's old radar used).
        foreach (var enemy in snapshot.EnemyShip.Ships)
        {
            var screen = mapOrigin + new Vector2(enemy.X, enemy.Y) * PixelsPerUnit * zoom;
            var color = enemy.IsRetreating ? Color.Goldenrod : Color.OrangeRed;
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 5f, color * 0.9f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 8f, 0f, 360f, color, 16, 1.5f);
        }

        foreach (var shot in snapshot.Projectiles)
        {
            var screen = mapOrigin + new Vector2(shot.X, shot.Y) * PixelsPerUnit * zoom;
            spriteBatch.Draw(_pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 3, 3), shot.FromEnemy ? Color.Red : Color.Gold);
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
    // centerScreen: the field's own centre, already converted to screen space by the caller (M47 -
    // "солнце было в центре") - not derived from the system's own points any more. A system whose
    // points are deliberately spread out to use the field's full size (GalaxyMap.cs's sol layout)
    // would otherwise pull this backdrop's sun off to wherever those points happen to average out
    // to, rather than leaving it where the sun (and CanWarpNow's own distance check) actually is.
    private void DrawSystemBackdrop(SpriteBatch spriteBatch, Vector2 centerScreen, float zoom, float totalSeconds)
    {
        // A fixed span in world units rather than one sized to the system's own points - those can
        // now sit anywhere from right next to the sun (the asteroid belt) to most of the way to the
        // warp zone, and scaling the decorative rings to fit "however far the farthest point happens
        // to be" would make them wildly different sizes from one system to the next for no reason
        // tied to anything the player can see.
        const float maxDistance = 700f;

        // Purely decorative planets (M47 - "вокруг него вращались несколько планет, в реальном
        // времени") orbiting the sun at real time, each on its own ring - not GalaxyPoints, not
        // interactive, not the same thing as an AsteroidField's own physical rocks. angularSpeed is
        // 2*pi/period, baked in rather than computed from a period field since nothing else ever
        // needs the period itself. phaseOffset just keeps them from all starting lined up along the
        // same ray from the sun.
        foreach (var planet in Planets)
        {
            var radius = maxDistance * planet.OrbitFraction * PixelsPerUnit * zoom;
            HudIcons.DrawRingArc(spriteBatch, _pixel, centerScreen, radius, 0f, 360f, Color.SlateGray * 0.22f, 48, 1f);

            var angle = totalSeconds * planet.AngularSpeed + planet.PhaseOffset;
            var planetScreen = centerScreen + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            HudIcons.FillCircle(spriteBatch, _pixel, planetScreen, planet.SizePixels * zoom, planet.Color);
        }

        var pulse = 0.75f + 0.25f * MathF.Sin(totalSeconds * 1.3f);
        for (var i = 3; i >= 1; i--)
            HudIcons.FillCircle(spriteBatch, _pixel, centerScreen, 3f + i * 3f * pulse, Color.LightYellow * (0.1f * i));
        HudIcons.FillCircle(spriteBatch, _pixel, centerScreen, 4f, Color.LightYellow * 0.9f);
    }

    private readonly record struct Planet(float OrbitFraction, float AngularSpeed, float PhaseOffset, float SizePixels, Color Color);

    // Periods deliberately not proportional to distance (real orbital mechanics would make the
    // outer rings crawl too slowly to ever notice moving) - close enough to "a planet" that the
    // eye reads it as one without needing several real minutes of watching to see it move.
    private static readonly Planet[] Planets =
    {
        new(0.15f, 2f * MathF.PI / 22f, 0.4f, 3.5f, new Color(178, 132, 94)),
        new(0.35f, 2f * MathF.PI / 48f, 2.1f, 5f, new Color(150, 172, 201)),
        new(0.65f, 2f * MathF.PI / 95f, 4.4f, 6.5f, new Color(203, 163, 112)),
        new(1f, 2f * MathF.PI / 160f, 5.6f, 8f, new Color(181, 201, 191)),
    };

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
