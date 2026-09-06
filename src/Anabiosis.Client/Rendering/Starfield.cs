using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Space itself, behind the ship - otherwise this game draws flat black there (no image
// backgrounds anywhere, per this whole client's own convention of building everything from one
// white pixel). Fixed to the screen rather than the world in the sense that nothing here is a real
// AsteroidField position - but unlike the old single flat layer, every star and nebula patch now
// carries its own Parallax (0 = infinitely far, barely drifts; near 1 = drifts almost with the
// ship), and ShipRenderer.Draw feeds in the ship's own real travelled distance (rotated into its
// local frame, same convention as ShipLocalFrame's EVA/aim conversions) each frame - so the drift
// is actual flight, not a fake ambient scroll, and holds still exactly when the ship does.
public sealed class Starfield
{
    private readonly struct Star
    {
        public readonly Vector2 Position;
        public readonly float Size;
        public readonly float BaseBrightness;
        public readonly float TwinkleAmount;
        public readonly float TwinkleSpeed;
        public readonly float Phase;
        public readonly float Parallax;

        public Star(Vector2 position, float size, float baseBrightness, float twinkleAmount, float twinkleSpeed, float phase, float parallax)
        {
            Position = position;
            Size = size;
            BaseBrightness = baseBrightness;
            TwinkleAmount = twinkleAmount;
            TwinkleSpeed = twinkleSpeed;
            Phase = phase;
            Parallax = parallax;
        }
    }

    private readonly struct Nebula
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly Color Tint;
        public readonly float Parallax;

        public Nebula(Vector2 position, float radius, Color tint, float parallax)
        {
            Position = position;
            Radius = radius;
            Tint = tint;
            Parallax = parallax;
        }
    }

    private readonly Texture2D _pixel;
    private readonly Rectangle _bounds;
    private readonly Star[] _stars;
    private readonly Nebula[] _nebulae;

    // A fixed seed rather than the shared gameplay Random: the sky itself should be the same
    // every time the game launches (only the twinkle and the parallax drift animate), not reroll
    // on every session the way an asteroid field or an enemy roll would.
    public Starfield(Texture2D pixel, Rectangle bounds, int count = 160)
    {
        _pixel = pixel;
        _bounds = bounds;
        var random = new Random(20260818);
        _stars = new Star[count];
        for (var i = 0; i < count; i++)
        {
            var position = RandomPointIn(bounds, random);
            // 3 depth bands rather than one flat layer - most stars sit far back (tiny, dim,
            // almost no drift), a handful sit close (bigger, brighter, visibly slide past as the
            // ship actually moves) - what actually reads as depth instead of a wallpaper texture.
            var band = random.NextDouble();
            var (parallaxMin, parallaxMax, sizeMin, sizeMax, brightMin, brightMax) = band switch
            {
                < 0.6 => (0.03f, 0.12f, 1f, 1f, 0.12f, 0.35f),
                < 0.9 => (0.16f, 0.32f, 1f, 1.6f, 0.3f, 0.55f),
                _ => (0.4f, 0.75f, 1.8f, 3f, 0.55f, 0.85f),
            };
            var parallax = parallaxMin + (float)random.NextDouble() * (parallaxMax - parallaxMin);
            var size = sizeMin + (float)random.NextDouble() * (sizeMax - sizeMin);
            var baseBrightness = brightMin + (float)random.NextDouble() * (brightMax - brightMin);
            var twinkleAmount = 0.15f + (float)random.NextDouble() * 0.3f;
            var twinkleSpeed = 0.5f + (float)random.NextDouble() * 1.6f;
            var phase = (float)random.NextDouble() * MathF.PI * 2f;
            _stars[i] = new Star(position, size, baseBrightness, twinkleAmount, twinkleSpeed, phase, parallax);
        }

        // A few huge, very soft tinted patches well behind even the farthest stars - breaks the
        // flat black without reading as "clouds" (low alpha, barely-there parallax, drawn first so
        // every star sits on top of them).
        var hues = new[] { new Color(70, 60, 120), new Color(40, 90, 110), new Color(110, 60, 70) };
        _nebulae = new Nebula[hues.Length];
        for (var i = 0; i < _nebulae.Length; i++)
        {
            var position = RandomPointIn(bounds, random);
            var radius = bounds.Width * (0.35f + (float)random.NextDouble() * 0.25f);
            var parallax = 0.02f + (float)random.NextDouble() * 0.03f;
            _nebulae[i] = new Nebula(position, radius, hues[i], parallax);
        }
    }

    private static Vector2 RandomPointIn(Rectangle bounds, Random random) => new(
        bounds.X + (float)random.NextDouble() * bounds.Width,
        bounds.Y + (float)random.NextDouble() * bounds.Height);

    // driftPixels: the ship's own real travelled distance so far, rotated into its local (screen)
    // frame and scaled to pixels (ShipRenderer.Draw) - zero while docked/stationary, so the whole
    // field simply holds still exactly when the ship does. Defaults to no drift at all for
    // GalaxyMapPanel's own starfield backdrop, which isn't a flight view - just twinkle there.
    // Deep space itself, not the absence of it - a flat literal (0,0,0) read as a broken/missing
    // background rather than a real backdrop (direct user request, comparing against Barotrauma's
    // own always-slightly-lit void). One solid very-dark fill under everything else is enough -
    // the nebulae/stars already carry all the actual variation.
    private static readonly Color DeepSpace = new(8, 9, 15);

    public void Draw(SpriteBatch spriteBatch, float totalSeconds, Vector2 driftPixels = default)
    {
        spriteBatch.Draw(_pixel, _bounds, DeepSpace);

        foreach (var nebula in _nebulae)
        {
            var pos = Wrap(nebula.Position - driftPixels * nebula.Parallax, _bounds);
            HudIcons.FillCircle(spriteBatch, _pixel, pos, nebula.Radius, nebula.Tint * 0.10f);
            HudIcons.FillCircle(spriteBatch, _pixel, pos, nebula.Radius * 0.6f, nebula.Tint * 0.08f);
            HudIcons.FillCircle(spriteBatch, _pixel, pos, nebula.Radius * 0.3f, nebula.Tint * 0.07f);
        }

        foreach (var star in _stars)
        {
            var alpha = star.BaseBrightness + star.TwinkleAmount * MathF.Sin(totalSeconds * star.TwinkleSpeed + star.Phase);
            if (alpha <= 0.02f)
                continue;
            var pos = Wrap(star.Position - driftPixels * star.Parallax, _bounds);
            spriteBatch.Draw(_pixel, pos, null, Color.White * Math.Clamp(alpha, 0f, 1f), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(star.Size, star.Size), SpriteEffects.None, 0f);
        }
    }

    // Keeps every star/nebula inside the same screen-sized bounds they were seeded in, so a drift
    // that pushes one off the left edge brings it back in on the right rather than losing it - an
    // infinite field approximated by a seamless wraparound of one screen-sized tile.
    private static Vector2 Wrap(Vector2 position, Rectangle bounds) =>
        new(Wrap1D(position.X, bounds.X, bounds.Width), Wrap1D(position.Y, bounds.Y, bounds.Height));

    private static float Wrap1D(float value, float min, float size)
    {
        var offset = (value - min) % size;
        if (offset < 0)
            offset += size;
        return min + offset;
    }
}
