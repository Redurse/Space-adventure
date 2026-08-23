using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The system field's own ambient content - the decorative sun/orbiting planets/warp-zone ring
// (DrawSystemBackdrop/DrawWarpZoneRing), the always-visible largest-asteroid markers, and the
// close-range "local radar" trio (nearby rocks/engaged enemies/shots in flight) - split out of
// GalaxyMapPanel.cs since these are all "what's physically out there" content, independent of the
// scanner porthole's own geometry (GalaxyMapPanel.Scanner.cs) or the ship/station schematics
// (GalaxyMapPanel.ShipAndStations.cs).
public sealed partial class GalaxyMapPanel
{
    // M48 follow-up - "при спавне пояса отметь на карте размазано 10 самых больших астероидов":
    // always visible (unlike the close-range-only real outlines DrawCloseRangeContacts draws) -
    // a belt's rough shape/location is meant to read as known terrain, not something to scan for.
    private const int LargestAsteroidMarkerCount = 10;

    private void DrawLargestAsteroidMarkers(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, float zoom, Vector2 panelOrigin, bool pilotView)
    {
        foreach (var asteroid in snapshot.Field.Asteroids.OrderByDescending(a => a.Radius).Take(LargestAsteroidMarkerCount))
        {
            var screen = mapOrigin + new Vector2(asteroid.X, asteroid.Y) * PixelsPerUnit * zoom;
            if (!IsWithinRadarView(panelOrigin, screen, pilotView))
                continue;
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 4f, new Color(150, 120, 92));
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 6f, 0f, 360f, new Color(96, 74, 56), 12, 1.5f);
        }
    }

    // Matches the old HelmPanel.RadarRangeUnits exactly (M47 - "как было раньше") - the pilot's
    // close-in situational awareness didn't get any better or worse when it moved onto this map,
    // just bigger and shared with the long-range scanner picture.
    private const float CloseRangeUnits = 50f;

    private void DrawCloseRangeContacts(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, Vector2 shipCenter, float zoom, Vector2 panelOrigin, bool pilotView)
    {
        var shipWorldPos = new Vector2(snapshot.ShipField.X, snapshot.ShipField.Y);

        foreach (var asteroid in snapshot.Field.Asteroids)
        {
            if ((new Vector2(asteroid.X, asteroid.Y) - shipWorldPos).Length() > CloseRangeUnits)
                continue;

            var center = mapOrigin + new Vector2(asteroid.X, asteroid.Y) * PixelsPerUnit * zoom;
            if (!IsWithinRadarView(panelOrigin, center, pilotView))
                continue;

            var outline = AsteroidShape.Outline(asteroid);
            var points = new Vector2[outline.Length];
            for (var i = 0; i < outline.Length; i++)
                points[i] = mapOrigin + new Vector2(outline[i].X, outline[i].Y) * PixelsPerUnit * zoom;

            Primitives.FillPolygon(spriteBatch, _pixel, center, points, new Color(96, 74, 56));
            Primitives.StrokePolygon(spriteBatch, _pixel, points, new Color(150, 120, 92));
        }

        // A squadron already fighting the player, or a shell already in flight, is not intel to be
        // discovered - it exists because the player is right there, so it's drawn unconditionally
        // rather than gated on CloseRangeUnits or a scan (same reasoning HelmPanel's old radar used).
        foreach (var enemy in snapshot.EnemyShip.Ships)
        {
            var screen = mapOrigin + new Vector2(enemy.X, enemy.Y) * PixelsPerUnit * zoom;
            if (!IsWithinRadarView(panelOrigin, screen, pilotView))
                continue;
            var color = enemy.IsRetreating ? Color.Goldenrod : Color.OrangeRed;
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 5f, color * 0.9f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 8f, 0f, 360f, color, 16, 1.5f);
        }

        foreach (var shot in snapshot.Projectiles)
        {
            var screen = mapOrigin + new Vector2(shot.X, shot.Y) * PixelsPerUnit * zoom;
            if (!IsWithinRadarView(panelOrigin, screen, pilotView))
                continue;
            spriteBatch.Draw(_pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 3, 3), shot.FromEnemy ? Color.Red : Color.Gold);
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
    // systemId: drives SystemOrbits.Generate the same way the server's own AsteroidField.CreateForSystem
    // does (M48 - "в звездных системах планеты спавнились в количествах от 3 до 6 штук") - client
    // and server never talk to agree on this, they just both hash the same string the same way, so
    // these decorative rings always line up with wherever a real belt (if any) actually sits.
    private void DrawSystemBackdrop(SpriteBatch spriteBatch, Vector2 centerScreen, float zoom, float totalSeconds, string systemId, Vector2 panelOrigin, bool pilotView)
    {
        var layout = SystemOrbits.Generate(systemId);

        // Purely decorative planets (M47 - "вокруг него вращались несколько планет, в реальном
        // времени") orbiting the sun at real time, each on its own ring - not GalaxyPoints, not
        // interactive, not the same thing as an AsteroidField's own physical rocks (drawn
        // separately, DrawLargestAsteroidMarkers). Speed/phase are deterministic functions of the
        // orbit's own index, not random - slower and later-phased further out, same "reads as a
        // planet without several real minutes of watching" reasoning the old fixed 4-planet set had.
        for (var i = 0; i < layout.PlanetCount; i++)
        {
            var radius = layout.OrbitRadii[i] * PixelsPerUnit * zoom;
            if (IsRingWithinRadarView(radius, pilotView))
                HudIcons.DrawRingArc(spriteBatch, _pixel, centerScreen, radius, 0f, 360f, Color.SlateGray * 0.22f, 48, 1f);

            var palette = PlanetPalette[i % PlanetPalette.Length];
            var angularSpeed = 2f * MathF.PI / (20f + i * 25f);
            var phaseOffset = i * 1.7f;
            var angle = totalSeconds * angularSpeed + phaseOffset;
            var planetScreen = centerScreen + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            if (IsWithinRadarView(panelOrigin, planetScreen, pilotView))
                HudIcons.FillCircle(spriteBatch, _pixel, planetScreen, palette.SizePixels * zoom, palette.Color);
        }

        if (!IsWithinRadarView(panelOrigin, centerScreen, pilotView))
            return;

        var pulse = 0.75f + 0.25f * MathF.Sin(totalSeconds * 1.3f);
        for (var i = 3; i >= 1; i--)
            HudIcons.FillCircle(spriteBatch, _pixel, centerScreen, 3f + i * 3f * pulse, Color.LightYellow * (0.1f * i));
        HudIcons.FillCircle(spriteBatch, _pixel, centerScreen, 4f, Color.LightYellow * 0.9f);
    }

    private readonly record struct PlanetLook(float SizePixels, Color Color);

    // Cycled by orbit index (mod length) rather than one entry per possible planet count, so
    // SystemOrbits.MaxPlanets can change later without this needing to grow in lockstep.
    private static readonly PlanetLook[] PlanetPalette =
    {
        new(3.5f, new Color(178, 132, 94)),
        new(5f, new Color(150, 172, 201)),
        new(6.5f, new Color(203, 163, 112)),
        new(8f, new Color(181, 201, 191)),
        new(4.5f, new Color(160, 200, 160)),
        new(7f, new Color(210, 140, 140)),
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
}
