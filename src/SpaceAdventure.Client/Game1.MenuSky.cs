using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client;

// The two things behind and around the menu's scene: the starfield and the star's glare.
//
// Both used to be part of the backdrop image, and being part of it was the problem. Stars baked into
// the same layer as the planet and the ship cannot move relative to them, so the vista had no depth -
// and a painted glare cannot react to anything. Neither needs a shader; both need to stop being
// pixels in a still.
public partial class Game1
{
    // Fractional part, correct for negative input too - the drift runs backwards.
    private static float Wrap(float v) => v - MathF.Floor(v);

    // Deterministic, so a star is in the same place every frame without a list to keep, and the whole
    // field costs three hashes per star and no memory at all.
    private static float StarHash(int index, int salt)
    {
        var n = (uint)(index * 374761393 + salt * 668265263);
        n ^= n >> 13;
        n *= 1274126177u;
        return ((n ^ (n >> 16)) & 0xffff) / 65535f;
    }

    // Depth layers. The near ones are fewer, brighter, bigger and faster - that combination is the
    // whole illusion, and getting any one of the four wrong flattens it. Drift is in pane widths per
    // second, so it is resolution independent.
    private static readonly (int Count, float Speed, float Dim, float Bright, int Size)[] StarLayers =
    {
        (520, 0.0022f, 0.20f, 0.46f, 1),
        (240, 0.0058f, 0.40f, 0.74f, 1),
        (90, 0.0125f, 0.62f, 1.00f, 2),
    };

    // Sideways and very slightly down, so the ship reads as making way rather than as falling. No
    // twinkle: stars twinkle because air moves in front of them, and there is no air out here. It
    // would be pretty and it would be a lie.
    private static readonly Vector2 StarDrift = new(-1f, 0.09f);

    private void DrawMenuStars(Rectangle pane, float totalSeconds)
    {
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
            transformMatrix: _renderScale);
        var index = 0;
        foreach (var (count, speed, dim, bright, size) in StarLayers)
        {
            for (var k = 0; k < count; k++, index++)
            {
                var u = Wrap(StarHash(index, 1) + totalSeconds * speed * StarDrift.X);
                var v = Wrap(StarHash(index, 2) + totalSeconds * speed * StarDrift.Y);
                var shade = dim + StarHash(index, 3) * (bright - dim);

                // A few are warm and a few are blue, most are neither. Hard pixels rather than soft
                // points: the backdrop is pixel art and a blurred star would be the one smooth thing
                // in the frame.
                var tint = StarHash(index, 4);
                var colour = tint > 0.93f ? new Color(255, 226, 190)
                    : tint < 0.07f ? new Color(200, 222, 255)
                    : Color.White;

                _spriteBatch.Draw(_pixel,
                    new Rectangle(pane.X + (int)(u * pane.Width), pane.Y + (int)(v * pane.Height), size, size),
                    colour * shade);
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
