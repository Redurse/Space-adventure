using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Client.Rendering;

namespace Anabiosis.Client;

// The moving parts of the main menu's scene. The backdrop image carries everything static - space,
// dust, stars, the sun, asteroids and the ship's hull - and these two things are drawn live on top
// of it, because they are the two that have to change.
//
// A title screen that does not move reads as a screenshot. It does not need much: one thing turning
// slowly and one thing burning is enough for the eye to accept the whole frame as a place rather
// than a picture.
public partial class Game1
{
    private Texture2D? _planetSurface;
    private Effect? _planetEffect;

    // Where the planet sits inside the art pane, as fractions of it, and how big. Matched to the
    // hole the backdrop was baked with - the planet is deliberately absent from that image.
    private const float PlanetCentreX = 0.78f;
    private const float PlanetCentreY = 0.62f;
    private const float PlanetRadius = 0.30f;

    // One revolution takes this long. Slow enough that it is never distracting, fast enough that a
    // player who sits on the menu for a minute can see it has moved.
    private const float PlanetRotationSeconds = 220f;

    // The sun, in the same pane fractions, so the shader's lighting agrees with the glow painted
    // into the backdrop. It has to sit clear of the planet: the disc spans 0.48 to 1.08 across the
    // pane, and the first attempt put the star at 0.545 - inside its own planet, which lit the world
    // from a point the picture said was behind it.
    private const float SunX = 0.30f;
    private const float SunY = 0.10f;

    // How far beyond the planet the star sits, as a fraction of the planet's radius. The terminator
    // is pushed this far towards the star at the equator and pinned at the poles, so it sets how deep
    // the crescent bows and how much of the globe is night.
    private const float SunDepth = -0.55f;

    // How much bigger than the disc the drawn quad is, matching Planet.fx's DiscRadius so the
    // atmosphere has somewhere to go.
    private const float PlanetQuadPad = 1f / 0.82f;

    private Rectangle MenuArtPane => new(MenuPaneX, 0, DesignWidth - MenuPaneX, DesignHeight);

    private void DrawMenuScene(float totalSeconds)
    {
        var pane = MenuArtPane;
        DrawMenuStars(pane, totalSeconds);
        DrawMenuPlanet(pane, totalSeconds);
        DrawMenuEnginePlume(pane, totalSeconds);
        DrawMenuSunGlare(pane, totalSeconds);
    }

    private void DrawMenuPlanet(Rectangle pane, float totalSeconds)
    {
        if (_planetEffect is null || _planetSurface is null)
            return;

        var radius = pane.Width * PlanetRadius;
        var half = radius * PlanetQuadPad;
        var centre = new Vector2(pane.X + pane.Width * PlanetCentreX, pane.Y + pane.Height * PlanetCentreY);
        var quad = new Rectangle((int)(centre.X - half), (int)(centre.Y - half),
            (int)(half * 2), (int)(half * 2));

        // The terminator passes through both poles only when the light lies in the planet's equatorial
        // plane. That is exactly what an equinox is, and on Earth it is the one day of the year the
        // day/night line touches both poles at once. The rotation axis here is drawn vertical, so the
        // light has to be horizontal - and the direction to the painted star is taken and flattened
        // onto the equator to get there. Which side it comes from still follows the star.
        //
        // The star is painted a little above the equator, so this is its direction with the vertical
        // part dropped rather than the direction itself. Reconciling the two exactly would mean
        // re-baking the backdrop with the sun level with the planet.
        var sun = new Vector2(pane.X + pane.Width * SunX, pane.Y + pane.Height * SunY);
        var fromLeft = sun.X < centre.X;

        // Z is the free number: a painted star has no stated depth, and its sign shapes the
        // terminator. Positive lights more than half the disc, zero splits it down a straight line,
        // negative puts the star beyond the planet and bows the boundary into a crescent.
        _planetEffect.Parameters["SunDirection"]?.SetValue(
            new Vector3(fromLeft ? -1f : 1f, 0f, SunDepth));
        _planetEffect.Parameters["Rotation"]?.SetValue(totalSeconds / PlanetRotationSeconds);
        _planetEffect.Parameters["SurfaceTexture"]?.SetValue(_planetSurface);
        _planetEffect.Parameters["CityBrightness"]?.SetValue(1f);
        _planetEffect.Parameters["Time"]?.SetValue(totalSeconds);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            effect: _planetEffect, transformMatrix: _renderScale);
        _spriteBatch.Draw(_planetSurface, quad, Color.White);
        _spriteBatch.End();
    }

    // The three nozzle mouths, in pane fractions, and the axis the exhaust leaves along.
    //
    // These are not read off a screenshot - that is what went wrong the first time, and an exhaust
    // aimed by eye came out across the hull instead of out of it. They are the ship's own geometry:
    // the bake lays the hull from tail (0.10, 0.62) to nose (0.52, 0.20) of the pane and hangs three
    // bells off the back at 0.015 along that axis, offset 30 render pixels either side. Below are
    // those three points, and the axis reversed. The pane and the bake share an aspect ratio of
    // 1.5357, so a direction carries across between them without correction.
    private static readonly Vector2[] NozzleMouths =
    {
        new(0.09678f, 0.59125f),
        new(0.10630f, 0.61370f),
        new(0.11582f, 0.63615f),
    };

    // Bell mouth radii in design pixels. The middle engine is the big one, as it is in the art.
    private static readonly float[] NozzleRadii = { 8.5f, 10.5f, 8.5f };

    private static readonly Vector2 ExhaustAxis = new(-0.837998f, 0.545673f);

    // Cone length as a fraction of the pane. Matches the exhaust the bake used to paint, so the live
    // plume takes over the same space rather than sitting on top of a stump.
    private const float PlumeLength = 0.1534f;

    private Vector2 NozzleMouth(Rectangle pane, int nozzle) =>
        new(pane.X + pane.Width * NozzleMouths[nozzle].X, pane.Y + pane.Height * NozzleMouths[nozzle].Y);

    // Three unrelated frequencies per nozzle, offset per engine: a plume that pulses on one clean sine
    // reads as a blinking light rather than as combustion, and three engines flickering in step read
    // as one. Shared, so the ripple below breathes with the exhaust instead of against it.
    private static float PlumeFlicker(int nozzle, float totalSeconds)
    {
        var phase = nozzle * 2.17f;
        return 1f
            + MathF.Sin(totalSeconds * 11.3f + phase) * 0.11f
            + MathF.Sin(totalSeconds * 27.7f + phase * 1.7f) * 0.09f
            + MathF.Sin(totalSeconds * 41.1f + phase * 0.6f) * 0.05f;
    }

    // Heat shimmer, stamped into the post chain's distortion mask so the stars behind the exhaust
    // bend. Glow can be painted by anything; refraction behind it is the part that says there is hot
    // gas there rather than a bright shape laid over the picture.
    //
    // Called from Draw between the menu and Present, because BeginDistortion switches render target
    // and every switch has to happen before the backbuffer is touched.
    internal void DrawMenuDistortion(float totalSeconds)
    {
        if (_menuScreen != MenuScreen.Main || _menuBackdrop is null || !_scenePost.BeginDistortion())
            return;

        var pane = MenuArtPane;
        var soft = _scenePost.Blob;
        var softOrigin = new Vector2(soft.Width * 0.5f, soft.Height * 0.5f);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            transformMatrix: _renderScale);
        for (var i = 0; i < NozzleMouths.Length; i++)
        {
            var mouth = NozzleMouth(pane, i);
            var flicker = PlumeFlicker(i, totalSeconds);
            var radius = NozzleRadii[i];
            var length = pane.Width * PlumeLength * flicker;

            // Wider and longer than the visible cone: air bends light well past the part of it that
            // is bright enough to see. Same reasoning as the steam in the ship interior.
            const int steps = 12;
            for (var s = 0; s < steps; s++)
            {
                var t = s / (float)steps;
                var pos = mouth + ExhaustAxis * (length * 1.35f * t);
                var w = radius * (1.1f + 3.4f * t);
                _spriteBatch.Draw(soft, pos, null,
                    Color.White * (MathF.Pow(1f - t, 1.5f) * 0.55f * flicker), 0f, softOrigin,
                    new Vector2(w * 2f / soft.Width, w * 2f / soft.Height), SpriteEffects.None, 0f);
            }
        }
        _spriteBatch.End();
        _scenePost.EndDistortion();
    }

    // The engines. The hull is part of the backdrop image, but the plume is not: a painted one would
    // be frozen, and a frozen exhaust is worse than none - it reads as a ship that has stalled. The
    // backdrop is baked with its nozzles empty (orbit2.BAKE_PLUME) precisely so this is the only
    // plume in the frame; two of them, one static, and the static one wins the eye.
    //
    // The shape is the wide, pale, diffuse mass the first version of this screen had, which came out
    // of the bake's narrow blue cone and the first live plume overlapping - together they read as one
    // broad wash rather than as either of them. Matched to that: it opens to roughly three bell radii
    // instead of one and two thirds, and the colour is close to white with only a blue cast, because
    // the saturated blue of the bake on its own reads as a laser rather than as exhaust.
    private void DrawMenuEnginePlume(Rectangle pane, float totalSeconds)
    {
        var rotation = MathF.Atan2(ExhaustAxis.Y, ExhaustAxis.X);

        // Stamped with the soft blob rather than a scaled pixel: a pixel blown up is a solid
        // rectangle, and a row of solid rectangles at falling alpha steps in visible bands instead of
        // fading. The blob carries its own smoothstep falloff.
        var soft = _scenePost.Blob;
        var softOrigin = new Vector2(soft.Width * 0.5f, soft.Height * 0.5f);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            transformMatrix: _renderScale);
        for (var i = 0; i < NozzleMouths.Length; i++)
        {
            var mouth = NozzleMouth(pane, i);
            var flicker = PlumeFlicker(i, totalSeconds);
            var radius = NozzleRadii[i];
            var length = pane.Width * PlumeLength * flicker;

            // Slices laid across the axis rather than square blobs along it: squares stair-step down a
            // diagonal, while slices turned to face the flow give a clean flare.
            const int steps = 32;
            var slice = length / steps * 2.2f;
            for (var s = 0; s < steps; s++)
            {
                var t = s / (float)steps;
                var pos = mouth + ExhaustAxis * (length * t);
                // Opens hard. A cone that stays narrow reads as a beam; the width is most of what
                // made the original look like something burning and spreading.
                var w = radius * (0.6f + 2.7f * t);
                // Additive blending is (SourceAlpha, One), so a tint scaled by f lands as f squared.
                var fade = MathF.Pow(1f - t, 1.9f) * 0.66f * flicker;
                var colour = Color.Lerp(new Color(198, 214, 238), new Color(104, 126, 176), t);
                _spriteBatch.Draw(soft, pos, null, colour * fade, rotation,
                    softOrigin, new Vector2(slice / soft.Width, w * 2f / soft.Height),
                    SpriteEffects.None, 0f);
            }

            // Inside the mouth. Kept modest: the bell's hot interior is still painted into the
            // backdrop - only the cone was taken out of it - so all this has to add is the pulse.
            _spriteBatch.Draw(soft, mouth, null,
                new Color(220, 240, 255) * (0.62f * flicker), rotation, softOrigin,
                new Vector2(radius * 0.7f / soft.Width, radius / soft.Height), SpriteEffects.None, 0f);
        }
        _spriteBatch.End();
    }
}
