using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

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

            Primitives.FillPolygon(spriteBatch, _pixel, center, points, new Color(96, 74, 56));
            Primitives.StrokePolygon(spriteBatch, _pixel, points, new Color(150, 120, 92));
        }

        // A squadron already fighting the player, or a shell already in flight, is not intel to be
        // discovered - it exists because the player is right there, so it's drawn unconditionally
        // rather than gated on CloseRangeUnits or a scan (same reasoning HelmPanel's own radar used).
        foreach (var enemy in snapshot.EnemyShip.Ships)
        {
            var screen = RelativeScreen(new Vec2(enemy.X, enemy.Y));
            if (!IsWithinRadarView(panelOrigin, screen, pilotView))
                continue;
            var color = enemy.IsRetreating ? Color.Goldenrod : Color.OrangeRed;
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 5f, color * 0.9f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 8f, 0f, 360f, color, 16, 1.5f);
        }

        foreach (var shot in snapshot.Projectiles)
        {
            var screen = RelativeScreen(new Vec2(shot.X, shot.Y));
            if (!IsWithinRadarView(panelOrigin, screen, pilotView))
                continue;
            spriteBatch.Draw(_pixel, new Rectangle((int)screen.X - 1, (int)screen.Y - 1, 3, 3), shot.FromEnemy ? Color.Red : Color.Gold);
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

    // Generate/ToDictionary/the Where+OrderBy below all allocate (a fresh Random, several Lists, a
    // Dictionary, LINQ enumerators) - drawn every single frame this panel is open with no caching,
    // that's a steady stream of garbage collected 60 times a second, which is real, visible stutter
    // (M50 follow-up - "нереально лагает"), not just wasted CPU. systemId's own body layout is
    // deterministic and never changes while looking at the same system, so this only has to be
    // rebuilt the one time the ship actually warps somewhere else - moons are grouped by their own
    // parent planet id up front too, so the per-planet loop below never allocates its own Where().
    private string? _cachedBackdropSystemId;
    private IReadOnlyList<CelestialBody> _cachedBackdropBodies = Array.Empty<CelestialBody>();
    private Dictionary<string, CelestialBody> _cachedBackdropById = new();
    private CelestialBody _cachedBackdropStar = null!;
    private List<CelestialBody> _cachedBackdropPlanets = new();
    private Dictionary<string, List<CelestialBody>> _cachedBackdropMoonsByPlanet = new();

    private void DrawSystemBackdrop(SpriteBatch spriteBatch, Vector2 mapOrigin, Vec2 fieldCenter, float zoom, float totalSeconds, string systemId, Vector2 panelOrigin, bool pilotView)
    {
        if (_cachedBackdropSystemId != systemId)
        {
            _cachedBackdropSystemId = systemId;
            var bodies = CelestialBodyGenerator.Generate(systemId);
            _cachedBackdropBodies = bodies;
            _cachedBackdropById = bodies.ToDictionary(b => b.Id);
            _cachedBackdropStar = bodies.Single(b => b.ParentId is null);
            _cachedBackdropPlanets = bodies.Where(b => b.ParentId == _cachedBackdropStar.Id).OrderBy(b => b.OrbitRadius).ToList();
            _cachedBackdropMoonsByPlanet = _cachedBackdropPlanets.ToDictionary(
                p => p.Id, p => bodies.Where(m => m.ParentId == p.Id).ToList());
        }

        var planets = _cachedBackdropPlanets;
        var centerScreen = FieldToScreen(mapOrigin, fieldCenter, zoom);

        foreach (var planet in planets)
        {
            var planetReal = CelestialBodyGenerator.PositionAt(planet, _cachedBackdropById) + fieldCenter;
            var planetScreen = FieldToScreen(mapOrigin, planetReal, zoom);

            foreach (var moon in _cachedBackdropMoonsByPlanet[planet.Id])
            {
                var moonReal = CelestialBodyGenerator.PositionAt(moon, _cachedBackdropById) + fieldCenter;
                var moonScreen = FieldToScreen(mapOrigin, moonReal, zoom);
                if (IsWithinRadarView(panelOrigin, moonScreen, pilotView))
                {
                    // Real physical scale (M53 - "чтобы отображался РЕАЛЬНЫЙ размер планеты") - the
                    // exact same radius*PixelsPerUnit*zoom every other real-scale object on this map
                    // uses (ship/asteroids). A tiny 1px floor only guards against a literal zero/
                    // negative-width circle at extreme zoom-out, not a scale hack.
                    // "как в KSP" - a lit sphere (HudIcons.DrawShadedSphere) instead of a flat
                    // FillCircle disc, same pre-baked pseudo-3D shading the planet's own body below
                    // uses.
                    var moonSizePixels = MathF.Max(1f, moon.Radius * PixelsPerUnit * zoom);
                    HudIcons.DrawShadedSphere(spriteBatch, _shadedSphere, moonScreen, moonSizePixels, TierColor(moon.MassTier));
                }
            }

            var sizePixels = MathF.Max(1f, planet.Radius * PixelsPerUnit * zoom);
            if (IsWithinRadarView(panelOrigin, planetScreen, pilotView))
                HudIcons.DrawShadedSphere(spriteBatch, _shadedSphere, planetScreen, sizePixels, TierColor(planet.MassTier));
        }

        if (!IsWithinRadarView(panelOrigin, centerScreen, pilotView))
            return;

        // "как в KSP" - the sun used to be a handful of small flat FillCircle discs (fine as a tiny
        // icon, but the map now draws real relative body sizes elsewhere, GalaxyMapPanel.FieldContent
        // .cs's own moon/planet comments, so a flat dot next to lit spheres read as backwards - the
        // one body that should look brightest instead looked flattest). A warm white-hot core inside
        // a soft golden corona (DrawScaledCircle's own pre-baked soft-edge texture, layered
        // large-to-small so each fainter outer layer shows through) reads as an actual light source;
        // the existing pulse still animates the corona's own reach.
        var pulse = 0.75f + 0.25f * MathF.Sin(totalSeconds * 1.3f);
        HudIcons.DrawScaledCircle(spriteBatch, _softCircle, centerScreen, 26f * pulse, new Color(255, 200, 110) * 0.18f);
        HudIcons.DrawScaledCircle(spriteBatch, _softCircle, centerScreen, 16f * pulse, new Color(255, 180, 90) * 0.3f);
        HudIcons.DrawScaledCircle(spriteBatch, _softCircle, centerScreen, 9f * pulse, new Color(255, 220, 140) * 0.55f);
        HudIcons.FillCircle(spriteBatch, _pixel, centerScreen, 4.5f, new Color(255, 250, 230));
    }

    private static Color TierColor(BodyMassTier tier) => tier switch
    {
        BodyMassTier.Rocky => new Color(178, 132, 94),
        BodyMassTier.IceGiant => new Color(181, 201, 221),
        BodyMassTier.GasGiant => new Color(203, 163, 112),
        _ => Color.White,
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
