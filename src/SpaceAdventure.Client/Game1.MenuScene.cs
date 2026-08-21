using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Client.Rendering;

namespace SpaceAdventure.Client;

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
        DrawMenuPlanet(pane, totalSeconds);
        DrawMenuEnginePlume(pane, totalSeconds);
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

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            effect: _planetEffect, transformMatrix: _renderScale);
        _spriteBatch.Draw(_planetSurface, quad, Color.White);
        _spriteBatch.End();
    }

    // The engines. The hull is part of the backdrop image, but a plume painted into it would be
    // frozen, and a frozen exhaust is worse than none - it reads as a ship that has stalled.
    private void DrawMenuEnginePlume(Rectangle pane, float totalSeconds)
    {
        // The three nozzle mouths, in pane fractions, taken from where the ship was rendered.
        var mouths = new[]
        {
            new Vector2(0.083f, 0.585f),
            new Vector2(0.097f, 0.618f),
            new Vector2(0.111f, 0.651f),
        };

        // Down and to the left, opposite the way the ship is pointing.
        var dir = new Vector2(-0.63f, 0.78f);
        dir.Normalize();

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            transformMatrix: _renderScale);
        for (var i = 0; i < mouths.Length; i++)
        {
            var origin = new Vector2(pane.X + pane.Width * mouths[i].X, pane.Y + pane.Height * mouths[i].Y);

            // Three unrelated frequencies per nozzle, offset per engine: a plume that pulses on one
            // clean sine reads as a blinking light rather than as combustion, and three engines
            // flickering in step read as one.
            var phase = i * 2.17f;
            var flicker = 0.72f
                + MathF.Sin(totalSeconds * 11.3f + phase) * 0.11f
                + MathF.Sin(totalSeconds * 27.7f + phase * 1.7f) * 0.09f
                + MathF.Sin(totalSeconds * 41.1f + phase * 0.6f) * 0.05f;

            var length = pane.Width * 0.10f * flicker;
            var width = pane.Width * 0.006f;

            // Drawn as a stack of shrinking, fading quads: cheap, and it gives the plume a soft edge
            // without needing a gradient texture.
            const int steps = 22;
            for (var s = 0; s < steps; s++)
            {
                var t = s / (float)steps;
                var pos = origin + dir * (length * t);
                var w = width * (1.0f + t * 2.6f);
                var fade = MathF.Pow(1f - t, 2.0f) * 0.30f * flicker;
                var colour = Color.Lerp(new Color(196, 226, 255), new Color(60, 96, 200), t);
                _spriteBatch.Draw(_pixel,
                    new Rectangle((int)(pos.X - w), (int)(pos.Y - w), (int)(w * 2), (int)(w * 2)),
                    colour * fade);
            }

            // The mouth itself, hottest and steadiest.
            _spriteBatch.Draw(_pixel,
                new Rectangle((int)origin.X - 2, (int)origin.Y - 2, 5, 5),
                new Color(220, 240, 255) * (0.55f * flicker));
        }
        _spriteBatch.End();
    }
}
