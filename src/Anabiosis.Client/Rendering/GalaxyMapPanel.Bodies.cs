using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The star system itself, under everything the crew adds to the map: the star with its corona and the zone
// where a hull burns, orbit lines, asteroid belts as bands of dust, and the planets and moons at their real
// size - a baked surface (PlanetSkins) turned so its lit side faces the star, an atmosphere glow, rings on
// some of the giants. Bodies never move (the map is static), so every angle here is fixed per body.
public sealed partial class GalaxyMapPanel
{
    private sealed record Belt(float Inner, float Outer, Texture2D Dust, (float Angle, float Radius, float Size, float Shade)[] Rocks);

    private string? _cachedBackdropSystemId;
    private Dictionary<string, CelestialBody> _cachedBackdropById = new();
    private CelestialBody _cachedBackdropStar = null!;
    private List<CelestialBody> _cachedBackdropPlanets = new();
    private Dictionary<string, List<CelestialBody>> _cachedBackdropMoonsByPlanet = new();
    private List<Belt> _belts = new();

    // Where the light comes from in a baked planet surface (PlanetTexture.LightDirection) and in the plain
    // shaded sphere (HudIcons.CreateShadedSphereTexture) - both are turned from here to face the star.
    private static readonly float SkinLightAngle = MathF.Atan2(-0.70f, -0.52f);
    private static readonly float SphereLightAngle = MathF.Atan2(-0.55f, -0.55f);
    // Below this many pixels a baked surface is only shimmer, so the plain sphere is drawn; above the second
    // number the surface has fully taken over.
    private const float SkinFromPixels = 12f;
    private const float SkinFullPixels = 34f;

    private void EnsureBodies(string systemId)
    {
        if (_cachedBackdropSystemId == systemId)
            return;
        _cachedBackdropSystemId = systemId;
        var bodies = CelestialBodyGenerator.Generate(systemId);
        _cachedBackdropById = bodies.ToDictionary(b => b.Id);
        _cachedBackdropStar = bodies.Single(b => b.ParentId is null);
        _cachedBackdropPlanets = bodies.Where(b => b.ParentId == _cachedBackdropStar.Id).OrderBy(b => b.OrbitRadius).ToList();
        _cachedBackdropMoonsByPlanet = _cachedBackdropPlanets.ToDictionary(
            p => p.Id, p => bodies.Where(m => m.ParentId == p.Id).ToList());

        foreach (var belt in _belts)
            belt.Dust.Dispose();
        _belts = new List<Belt>();
        var seed = MapArt.Seed(systemId);
        foreach (var (inner, outer) in CelestialBodyGenerator.BeltBands(systemId))
        {
            var beltSeed = seed + _belts.Count * 977;
            var random = new Random(beltSeed);
            var rocks = new (float, float, float, float)[260];
            for (var i = 0; i < rocks.Length; i++)
            {
                // Two rolls averaged keep most of the rocks toward the middle of the band.
                var across = (random.NextSingle() + random.NextSingle()) / 2f;
                rocks[i] = (random.NextSingle() * MathF.Tau, inner + (outer - inner) * (0.08f + 0.84f * across),
                    1.2f + 1.6f * random.NextSingle(), 0.55f + 0.45f * random.NextSingle());
            }
            _belts.Add(new Belt(inner, outer, MapArt.DustRing(_device, beltSeed, inner / outer), rocks));
        }
    }

    private void DrawSystemBackdrop(SpriteBatch spriteBatch, Vector2 mapOrigin, Vec2 fieldCenter, float zoom, float totalSeconds, string systemId, Vector2 panelOrigin, bool pilotView)
    {
        EnsureBodies(systemId);
        var scale = PixelsPerUnit * zoom;
        var starScreen = FieldToScreen(mapOrigin, fieldCenter, zoom);

        DrawBelts(spriteBatch, starScreen, fieldCenter, scale, zoom);

        foreach (var planet in _cachedBackdropPlanets)
            DrawOrbit(spriteBatch, starScreen, planet.OrbitRadius * scale, 0.11f);

        DrawSun(spriteBatch, starScreen, scale, totalSeconds);

        foreach (var planet in _cachedBackdropPlanets)
        {
            var planetReal = CelestialBodyGenerator.PositionAt(planet, _cachedBackdropById) + fieldCenter;
            var planetScreen = FieldToScreen(mapOrigin, planetReal, zoom);
            var planetPixels = MathF.Max(1f, planet.Radius * scale);
            foreach (var moon in _cachedBackdropMoonsByPlanet[planet.Id])
            {
                if (planetPixels > 8f)
                    DrawOrbit(spriteBatch, planetScreen, moon.OrbitRadius * scale, 0.08f);
                var moonReal = CelestialBodyGenerator.PositionAt(moon, _cachedBackdropById) + fieldCenter;
                DrawBody(spriteBatch, moon, moonReal, FieldToScreen(mapOrigin, moonReal, zoom), MathF.Max(1f, moon.Radius * scale), starScreen);
            }
            DrawBody(spriteBatch, planet, planetReal, planetScreen, planetPixels, starScreen);
        }
    }

    private void DrawOrbit(SpriteBatch spriteBatch, Vector2 center, float radiusPixels, float strength)
    {
        if (radiusPixels < 4f)
            return;
        // More segments for a bigger circle, so a huge orbit seen up close still reads as a curve.
        var segments = (int)Math.Clamp(radiusPixels / 5f, 40f, 240f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, radiusPixels, 0f, 360f, Ink(new Color(170, 195, 230) * strength), segments, 1f);
    }

    private void DrawBelts(SpriteBatch spriteBatch, Vector2 starScreen, Vec2 fieldCenter, float scale, float zoom)
    {
        // Loose rocks are scenery, not the real asteroids a pilot has to steer around (those are drawn with
        // their true outlines in DrawCloseRangeContacts) - so they fade away once the map is zoomed in far
        // enough for the real ones to matter.
        var rockFade = 1f - MapArt.Smooth(0.25f, 0.6f, zoom);
        foreach (var belt in _belts)
        {
            var outerPixels = belt.Outer * scale;
            spriteBatch.Draw(belt.Dust, starScreen, null, Ink(Color.White * 0.9f), 0f,
                new Vector2(belt.Dust.Width / 2f, belt.Dust.Height / 2f), outerPixels * 2f / belt.Dust.Width, SpriteEffects.None, 0f);
            if (rockFade <= 0.02f)
                continue;
            foreach (var (angle, radius, size, shade) in belt.Rocks)
            {
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                var screen = starScreen + offset * scale;
                if (!IsOnScreen(screen, 4f) || !IsExplored(_shown, fieldCenter + new Vec2(offset.X, offset.Y)))
                    continue;
                spriteBatch.Draw(_pixel, screen, null, Ink(new Color(176, 152, 124) * (shade * rockFade)), angle * 3f,
                    new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            }
        }
    }

    private void DrawSun(SpriteBatch spriteBatch, Vector2 center, float scale, float totalSeconds)
    {
        var star = _cachedBackdropStar;
        var radius = MathF.Max(7f, star.Radius * scale);
        var zone = MathF.Max(radius * 1.25f, CelestialBodyGenerator.SunZoneRadius(star) * scale);
        if (!IsOnScreen(center, MathF.Max(zone, radius * 3f)))
            return;
        var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 1.3f);

        if (_sonar)
        {
            HudIcons.DrawScaledCircle(spriteBatch, _softCircle, center, radius, Ink(new Color(255, 236, 180)) * 0.85f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, center, zone, 0f, 360f, Ink(new Color(255, 80, 50)) * (0.5f + 0.3f * pulse), 96, 1.5f);
            return;
        }

        // The burn zone first, then two layers of corona turning opposite ways, then the disc itself.
        spriteBatch.Draw(_heatZone, center, null, Color.White * (0.8f + 0.2f * pulse), 0f, new Vector2(128f, 128f), zone * 2f / 256f, SpriteEffects.None, 0f);
        var reach = radius * (2.7f + 0.15f * pulse);
        spriteBatch.Draw(_corona, center, null, MapArt.Additive(Color.White, 0.85f), totalSeconds * 0.02f, new Vector2(128f, 128f), reach * 2f / 256f, SpriteEffects.None, 0f);
        spriteBatch.Draw(_corona, center, null, MapArt.Additive(Color.White, 0.55f), -totalSeconds * 0.013f, new Vector2(128f, 128f), reach * 1.5f * 2f / 256f, SpriteEffects.None, 0f);
        spriteBatch.Draw(_sunDisc, center, null, Color.White, 0f, new Vector2(96f, 96f), radius * 2f / 192f, SpriteEffects.None, 0f);
        DrawGlow(spriteBatch, center, radius * 1.5f, new Color(255, 240, 200), 0.45f);
    }

        // A body the crew has not been near stays out of sight: the fog over it is thin enough to let the
    // nebula through, so it cannot be relied on to hide a planet.
    private void DrawBody(SpriteBatch spriteBatch, CelestialBody body, Vec2 real, Vector2 screen, float radius, Vector2 starScreen)
    {
        if (!IsOnScreen(screen, radius * 2.6f) || !IsExplored(_shown, real))
            return;
        var tint = TierColor(body.MassTier);
        var towardStar = starScreen - screen;
        var sunAngle = MathF.Atan2(towardStar.Y, towardStar.X);
        var sphereOrigin = new Vector2(_shadedSphere.Width / 2f, _shadedSphere.Height / 2f);

        if (_sonar)
        {
            spriteBatch.Draw(_shadedSphere, screen, null, Ink(tint), sunAngle - SphereLightAngle, sphereOrigin, radius * 2f / _shadedSphere.Width, SpriteEffects.None, 0f);
            return;
        }

        var seed = MapArt.Seed(body.Id);
        var isPlanet = body.MassTier != BodyMassTier.Moon;
        if (isPlanet && radius >= 3f)
        {
            // The glow sits a little toward the star, so the lit limb carries more of it than the dark one.
            var lit = towardStar.LengthSquared() > 1f ? Vector2.Normalize(towardStar) : Vector2.Zero;
            DrawGlow(spriteBatch, screen + lit * radius * 0.18f, radius * 1.9f, AtmosphereColor(body.MassTier), 0.5f);
        }

        var ringed = isPlanet && radius >= 4f && HasRings(body, seed);
        var ringTilt = (MapArt.Hash(seed, 3, 1) - 0.5f) * 1.1f;
        var ringScale = new Vector2(radius * 2.25f * 2f / 256f) * new Vector2(1f, 0.26f + 0.2f * MapArt.Hash(seed, 4, 1));
        var ringTint = Color.Lerp(Color.White, tint, 0.45f) * 0.9f;
        if (ringed) // the far half of the rings goes behind the planet
            spriteBatch.Draw(_planetRing, screen, new Rectangle(0, 0, 256, 128), ringTint, ringTilt, new Vector2(128f, 128f), ringScale, SpriteEffects.None, 0f);

        var surface = radius >= SkinFromPixels && PlanetSkins.TryGet(_device, body, out var skin) ? skin : (PlanetTexture.Skin?)null;
        var blend = surface is null ? 0f : MapArt.Smooth(SkinFromPixels, SkinFullPixels, radius);
        if (blend < 1f)
            spriteBatch.Draw(_shadedSphere, screen, null, tint, sunAngle - SphereLightAngle, sphereOrigin, radius * 2f / _shadedSphere.Width, SpriteEffects.None, 0f);
        if (surface is { } baked && blend > 0f)
            spriteBatch.Draw(baked.Texture, screen, null, Color.White * blend, sunAngle - SkinLightAngle,
                new Vector2(baked.Texture.Width / 2f, baked.Texture.Height / 2f), radius * 2f / baked.Texture.Width, SpriteEffects.None, 0f);

        if (ringed) // and the near half in front of it
            spriteBatch.Draw(_planetRing, screen, new Rectangle(0, 128, 256, 128), ringTint, ringTilt, new Vector2(128f, 0f), ringScale, SpriteEffects.None, 0f);
    }

    private static bool HasRings(CelestialBody body, int seed) => body.MassTier switch
    {
        BodyMassTier.GasGiant => MapArt.Hash(seed, 1, 1) < 0.65f,
        BodyMassTier.IceGiant => MapArt.Hash(seed, 1, 1) < 0.35f,
        _ => false,
    };

    private static Color TierColor(BodyMassTier tier) => tier switch
    {
        BodyMassTier.Rocky => new Color(178, 132, 94),
        BodyMassTier.IceGiant => new Color(181, 201, 221),
        BodyMassTier.GasGiant => new Color(203, 163, 112),
        BodyMassTier.Moon => new Color(150, 148, 144),
        _ => Color.White,
    };

    private static Color AtmosphereColor(BodyMassTier tier) => tier switch
    {
        BodyMassTier.IceGiant => new Color(120, 190, 255),
        BodyMassTier.GasGiant => new Color(255, 196, 130),
        _ => new Color(150, 180, 255),
    };

    // The edge of the system, past which a jump is possible: two arcs turning opposite ways, gold once the
    // ship is actually out there.
    private void DrawWarpZoneRing(SpriteBatch spriteBatch, Vector2 center, float radius, bool armed, float totalSeconds)
    {
        var color = armed ? Color.Gold : Color.MediumPurple;
        var spinOuter = totalSeconds * 20f % 360f;
        var spinInner = -totalSeconds * 28f % 360f;
        var segments = (int)Math.Clamp(radius / 5f, 64f, 240f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius, spinOuter, spinOuter + 300f, Ink(color * 0.55f), segments, 2.5f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, radius * 0.985f, spinInner, spinInner + 300f, Ink(Color.White * 0.35f), segments, 1.5f);
    }
}
