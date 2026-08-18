using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Space itself, behind the ship - otherwise this game draws flat black there (no image
// backgrounds anywhere, per this whole client's own convention of building everything from one
// white pixel). Fixed to the screen rather than the world: stars read as infinitely far away, so
// unlike every asteroid/ship/dropped item FieldRenderer places at a real position, these don't
// drift as the camera follows the player around the ship or the ship flies through the field -
// only their brightness moves, which is the twinkle.
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

        public Star(Vector2 position, float size, float baseBrightness, float twinkleAmount, float twinkleSpeed, float phase)
        {
            Position = position;
            Size = size;
            BaseBrightness = baseBrightness;
            TwinkleAmount = twinkleAmount;
            TwinkleSpeed = twinkleSpeed;
            Phase = phase;
        }
    }

    private readonly Texture2D _pixel;
    private readonly Star[] _stars;

    // A fixed seed rather than the shared gameplay Random: the sky itself should be the same
    // every time the game launches (only the twinkle animates), not reroll on every session the
    // way an asteroid field or an enemy roll would.
    public Starfield(Texture2D pixel, Rectangle bounds, int count = 160)
    {
        _pixel = pixel;
        var random = new Random(20260818);
        _stars = new Star[count];
        for (var i = 0; i < count; i++)
        {
            var position = new Vector2(
                bounds.X + (float)random.NextDouble() * bounds.Width,
                bounds.Y + (float)random.NextDouble() * bounds.Height);
            // Mostly small and dim, with a handful of bigger, brighter ones standing out - an even
            // spread of identical dots reads as a texture, not a sky.
            var big = random.NextDouble() < 0.12;
            var size = big ? 2f + (float)random.NextDouble() * 1.5f : 1f;
            var baseBrightness = big ? 0.55f + (float)random.NextDouble() * 0.25f : 0.2f + (float)random.NextDouble() * 0.3f;
            var twinkleAmount = 0.2f + (float)random.NextDouble() * 0.35f;
            var twinkleSpeed = 0.6f + (float)random.NextDouble() * 1.8f;
            var phase = (float)random.NextDouble() * MathF.PI * 2f;
            _stars[i] = new Star(position, size, baseBrightness, twinkleAmount, twinkleSpeed, phase);
        }
    }

    public void Draw(SpriteBatch spriteBatch, float totalSeconds)
    {
        foreach (var star in _stars)
        {
            var alpha = star.BaseBrightness + star.TwinkleAmount * MathF.Sin(totalSeconds * star.TwinkleSpeed + star.Phase);
            if (alpha <= 0.02f)
                continue;
            spriteBatch.Draw(_pixel, star.Position, null, Color.White * Math.Clamp(alpha, 0f, 1f), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(star.Size, star.Size), SpriteEffects.None, 0f);
        }
    }
}
