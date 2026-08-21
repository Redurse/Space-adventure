using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client;

// The two things behind and around the menu's scene: the starfield and the star's glare.
//
// Both used to be part of the backdrop image, and being part of it was the problem: a painted star
// cannot change and a painted glare cannot react. Neither needs a shader; both needed to stop being
// pixels in a still.
public partial class Game1
{
    // Deterministic, so a star is in the same place every frame without a list to keep, and the whole
    // field costs three hashes per star and no memory at all.
    private static float StarHash(int index, int salt)
    {
        var n = (uint)(index * 374761393 + salt * 668265263);
        n ^= n >> 13;
        n *= 1274126177u;
        return ((n ^ (n >> 16)) & 0xffff) / 65535f;
    }

    // Magnitude classes. These used to be depth layers with their own drift speeds; with the field
    // held still what is left of them is what a real sky has anyway - a great many faint stars, fewer
    // ordinary ones, and a handful of bright ones.
    //
    // The last number is how hard each class twinkles, and it falls as they get brighter. That is
    // what it looks like: a bright star holds steady while the faint ones flutter, because the same
    // swing in brightness is a far larger share of a dim star than of a bright one. Giving every star
    // the same twinkle reads as an effect applied to the screen rather than as stars.
    private static readonly (int Count, float Dim, float Bright, int Size, float Twinkle)[] StarLayers =
    {
        (520, 0.20f, 0.46f, 1, 0.62f),
        (240, 0.40f, 0.74f, 1, 0.42f),
        (90, 0.62f, 1.00f, 2, 0.24f),
    };

    private void DrawMenuStars(Rectangle pane, float totalSeconds)
    {
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
            transformMatrix: _renderScale);
        var index = 0;
        foreach (var (count, dim, bright, size, twinkle) in StarLayers)
        {
            for (var k = 0; k < count; k++, index++)
            {
                var x = pane.X + (int)(StarHash(index, 1) * pane.Width);
                var y = pane.Y + (int)(StarHash(index, 2) * pane.Height);
                var shade = dim + StarHash(index, 3) * (bright - dim);

                // Two sines at unrelated rates rather than one. A single rate gives every star a
                // clean breath with a findable period, and a field of them ends up pulsing together
                // no matter how the phases are scattered; summing a second incommensurable rate
                // breaks the period long enough that the eye stops counting it.
                var rate = 0.55f + StarHash(index, 5) * 1.9f;
                var phase = StarHash(index, 6) * MathF.Tau;
                var flutter = MathF.Sin(totalSeconds * rate + phase) * 0.62f
                            + MathF.Sin(totalSeconds * rate * (1.43f + StarHash(index, 7) * 0.8f) + phase * 1.7f) * 0.38f;

                // How much each individual star swings, on top of its class. Without this spread the
                // whole field flutters by the same amount and reads as one animation.
                var amplitude = twinkle * (0.35f + StarHash(index, 8));

                // Floored well above zero: a star that reaches black and comes back is not twinkling,
                // it is a dead pixel.
                var lit = shade * MathHelper.Clamp(1f + flutter * amplitude, 0.30f, 1.85f);

                var tint = StarHash(index, 4);
                var colour = tint > 0.93f ? new Color(255, 226, 190)
                    : tint < 0.07f ? new Color(200, 222, 255)
                    : Color.White;

                _spriteBatch.Draw(_pixel, new Rectangle(x, y, size, size), colour * lit);

                // At its peak a bright star throws four short arms. In pixel art this is what actually
                // says "twinkle" - brightness alone reads as a fade, while a shape that appears and
                // goes is unmistakable. Only the bright class, and only near the top of its swing.
                if (size < 2 || flutter < 0.82f)
                    continue;
                var spark = colour * (lit * (flutter - 0.82f) / 0.18f * 0.55f);
                _spriteBatch.Draw(_pixel, new Rectangle(x - 1, y, 1, size), spark);
                _spriteBatch.Draw(_pixel, new Rectangle(x + size, y, 1, size), spark);
                _spriteBatch.Draw(_pixel, new Rectangle(x, y - 1, size, 1), spark);
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + size, size, 1), spark);
            }
        }
        _spriteBatch.End();
    }

    // Glare from the star. There is a bright point in frame and at the moment the camera does not
    // react to it at all, which is the single thing that stops this reading as a photograph of
    // somewhere.
    //
    // Drawn as sprites rather than as a post pass on purpose. A real anamorphic streak is a
    // directional blur of everything bright, and with exactly one bright source in the frame that
    // pass buys nothing but another render target, another blur and more instructions in a composite
    // that is already large. The one thing it would add - city lights and exhaust streaking too - is
    // not wanted here.
    //
    // Kept sparse. A full flare chain of a dozen rings is the fastest way to turn a space vista into
    // a screensaver.
    private void DrawMenuSunGlare(Rectangle pane, float totalSeconds)
    {
        var soft = _scenePost.Blob;
        var origin = new Vector2(soft.Width * 0.5f, soft.Height * 0.5f);
        var sun = new Vector2(pane.X + pane.Width * SunX, pane.Y + pane.Height * SunY);

        // Ghosts sit on the line from the light through the centre of the frame - that is where a
        // lens puts them, because they are reflections between elements on the optical axis.
        var centre = new Vector2(DesignWidth * 0.5f, DesignHeight * 0.5f);
        var toCentre = centre - sun;

        // Two slow, unrelated beats. One rate would read as a pulse; two make it look like the
        // aperture is just breathing.
        var breath = 1f
            + MathF.Sin(totalSeconds * 0.37f) * 0.05f
            + MathF.Sin(totalSeconds * 1.13f) * 0.02f;

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            transformMatrix: _renderScale);

        // The horizontal streak. Half-width is held well inside the art pane: the button column
        // starts at MenuPaneX and a streak reaching over it would light up the UI.
        var reach = MathF.Min(150f, sun.X - pane.X - 24f);
        for (var s = 0; s < 3; s++)
        {
            var scale = 1f - s * 0.28f;
            _spriteBatch.Draw(soft, sun, null, new Color(255, 240, 214) * (0.16f * breath), 0f, origin,
                new Vector2(reach * 2f * scale / soft.Width, (6f + s * 9f) / soft.Height),
                SpriteEffects.None, 0f);
        }

        // The core, and one wide halo under it.
        _spriteBatch.Draw(soft, sun, null, new Color(255, 246, 226) * (0.40f * breath), 0f, origin,
            new Vector2(46f * breath / soft.Width, 46f * breath / soft.Height), SpriteEffects.None, 0f);
        _spriteBatch.Draw(soft, sun, null, new Color(214, 176, 128) * (0.13f * breath), 0f, origin,
            new Vector2(190f / soft.Width, 190f / soft.Height), SpriteEffects.None, 0f);

        // Three ghosts, along and past the centre. Alternating warm and cold, because the coatings
        // that cause them are what tint them.
        var ghosts = new[]
        {
            (Along: 0.46f, Size: 26f, Alpha: 0.075f, Tint: new Color(255, 214, 168)),
            (Along: 0.82f, Size: 15f, Alpha: 0.055f, Tint: new Color(168, 226, 255)),
            (Along: 1.24f, Size: 38f, Alpha: 0.040f, Tint: new Color(255, 232, 196)),
        };
        foreach (var (along, size, alpha, tint) in ghosts)
        {
            var pos = sun + toCentre * along;
            _spriteBatch.Draw(soft, pos, null, tint * (alpha * breath), 0f, origin,
                new Vector2(size / soft.Width, size / soft.Height), SpriteEffects.None, 0f);
        }
        _spriteBatch.End();
    }
}
