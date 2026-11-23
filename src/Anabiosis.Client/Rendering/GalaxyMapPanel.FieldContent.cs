using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The system field's own ambient content - the decorative sun/static planets/warp-zone ring
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
        // System-wide (M55 follow-up), not ship-local - the belt's own biggest rocks are meant to
        // read as known terrain no matter how far the ship currently is from them.
        foreach (var asteroid in snapshot.Field.Asteroids.OrderByDescending(a => a.Radius).Take(LargestAsteroidMarkerCount))
        {
            var screen = FieldToScreen(mapOrigin, new Vec2(asteroid.X, asteroid.Y), zoom);
            if (!IsWithinRadarView(panelOrigin, screen, pilotView) || !IsExplored(snapshot, new Vec2(asteroid.X, asteroid.Y)))
                continue;
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 4f, Ink(new Color(150, 120, 92)));
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 6f, 0f, 360f, Ink(new Color(96, 74, 56)), 12, 1.5f);
        }
    }

    // Matches the old HelmPanel.RadarRangeUnits exactly (M47 - "как было раньше") - the pilot's
    // close-in situational awareness didn't get any better or worse when it moved onto this map,
    // just bigger and shared with the long-range scanner picture.
    private const float CloseRangeUnits = 50f;

    // Everything drawn here is ship-local by construction (asteroids capped to CloseRangeUnits, and
    // even the "unconditional" enemy squadron/projectiles only exist within real weapon range,
    // World.EnemyAi.cs) - placed as a plain, delta from the ship's own already-correctly-placed
    // shipCenter (M55 follow-up), not run through a field-wide transform.
    private void DrawCloseRangeContacts(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 shipCenter, float zoom, Vector2 panelOrigin, bool pilotView)
    {
        // Real (double) field coordinates subtracted here, BEFORE ever narrowing to a screen-scale
        // float (M58 follow-up - the same "subtract two huge numbers in float first" bug this
        // session's other KSP-scale fixes already root-caused): the delta itself is always small
        // (CloseRangeUnits-bounded) regardless of how far this whole close-in cluster sits from the
        // field's own origin, so only the delta needs to touch float at all, not the two absolute
        // positions that produce it.
        var shipWorldPos = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        Vector2 RelativeScreen(Vec2 realPos)
        {
            var delta = realPos - shipWorldPos;
            return shipCenter + new Vector2((float)delta.X, (float)delta.Y) * PixelsPerUnit * zoom;
        }

        foreach (var asteroid in snapshot.Field.Asteroids)
        {
            if ((asteroid.Position - shipWorldPos).Length() > CloseRangeUnits)
                continue;

            var center = RelativeScreen(asteroid.Position);
            if (!IsWithinRadarView(panelOrigin, center, pilotView))
                continue;

            var outline = AsteroidShape.Outline(asteroid);
            var points = new Vector2[outline.Length];
            for (var i = 0; i < outline.Length; i++)
                points[i] = RelativeScreen(outline[i]);

            Primitives.FillPolygon(spriteBatch, _pixel, center, points, Ink(new Color(96, 74, 56)));
            Primitives.StrokePolygon(spriteBatch, _pixel, points, Ink(new Color(150, 120, 92)));
        }

        // A squadron already fighting the player, or a shell already in flight, is not intel to be
        // discovered - it exists because the player is right there, so it's drawn unconditionally
        // rather than gated on CloseRangeUnits or a scan (same reasoning HelmPanel's own radar used).
        foreach (var enemy in snapshot.EnemyShip.Ships)
        {
            var screen = RelativeScreen(new Vec2(enemy.X, enemy.Y));
            if (!IsWithinRadarView(panelOrigin, screen, pilotView))
                continue;
            var color = Ink(enemy.IsRetreating ? Color.Goldenrod : Color.OrangeRed);
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 5f, color * 0.9f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 8f, 0f, 360f, color, 16, 1.5f);
        }

        foreach (var shot in snapshot.Projectiles)
        {
            var screen = RelativeScreen(new Vec2(shot.X, shot.Y));
            if (!IsWithinRadarView(panelOrigin, screen, pilotView))
                continue;
            spriteBatch.Draw(_pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 3, 3), Ink(shot.FromEnemy ? Color.Red : Color.Gold));
        }
    }

    // The single shared linear transform every SYSTEM-WIDE real position (a body, a station, the
    // ship, a scanner contact) goes through to land on screen - mapOrigin already anchors the field
    // origin at screen-centre (ComputeMapOrigin), so this is a plain scale-and-offset. M59 -
    // "убрать орбитальную механику, вернуть статичную карту в духе Cosmoteer": bodies and stations
    // are all fixed now, and the field itself shrank back down from KSP scale, so the old log-
    // compression (CompressedUnits) and per-host relative anchoring (ResolveHostedScreenPosition)
    // that scale used to need are both gone - every real position maps to screen the same simple way.
    // Multiplying in double first and narrowing only the final, already screen-pixel-scale offset
    // keeps this to a fraction of a pixel even at the field's own largest real coordinates.
    private static Vector2 FieldToScreen(Vector2 mapOrigin, Vec2 realPosition, float zoom)
    {
        var scaled = realPosition * (double)PixelsPerUnit * zoom;
        return mapOrigin + new Vector2((float)scaled.X, (float)scaled.Y);
    }

}
