using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The sky behind the galaxy, as Stellaris has it: black, grey dust drifting in banks, and a great many small stars at three
// depths that slide at different speeds when the map is dragged.
public sealed partial class GalacticMapPanel
{
    private const int StarCount = 900;
    // The dust is drawn this much larger than the screen so that dragging can slide it without showing its edge.
    private static readonly Vector2 NebulaMargin = new(60f, 30f);

    private Texture2D _nebula = null!;
    private Vector2[] _stars = Array.Empty<Vector2>();
    private float[] _starBrightness = Array.Empty<float>();
    private float[] _starDepth = Array.Empty<float>();
    private Color[] _starTint = Array.Empty<Color>();

    private void BuildBackdrop()
    {
        _nebula = MapArt.DustSky(_device, 20261114, 640, 300);
        var random = new Random(424242);
        _stars = new Vector2[StarCount];
        _starBrightness = new float[StarCount];
        _starDepth = new float[StarCount];
        _starTint = new Color[StarCount];
        for (var i = 0; i < StarCount; i++)
        {
            _stars[i] = new Vector2(_view.X + random.NextSingle() * _view.Width, _view.Y + random.NextSingle() * _view.Height);
            // Most are faint specks far behind; a few are nearer, brighter and move faster.
            _starDepth[i] = i < 620 ? 0.012f + 0.02f * random.NextSingle() : i < 860 ? 0.04f + 0.04f * random.NextSingle() : 0.08f + 0.06f * random.NextSingle();
            _starBrightness[i] = i < 620 ? 0.18f + 0.30f * random.NextSingle() : i < 860 ? 0.40f + 0.30f * random.NextSingle() : 0.70f + 0.28f * random.NextSingle();
            _starTint[i] = Color.Lerp(new Color(184, 204, 255), new Color(255, 232, 205), random.NextSingle());
        }
    }

    private void DrawBackdrop(SpriteBatch spriteBatch, Vector2 pan)
    {
        spriteBatch.Draw(_pixel, _view, new Color(2, 3, 7));

        var slide = Vector2.Clamp(pan * 0.03f, -NebulaMargin, NebulaMargin);
        spriteBatch.Draw(_nebula, new Rectangle(
            (int)(_view.X - NebulaMargin.X + slide.X), (int)(_view.Y - NebulaMargin.Y + slide.Y),
            (int)(_view.Width + NebulaMargin.X * 2f), (int)(_view.Height + NebulaMargin.Y * 2f)), Color.White);

        for (var i = 0; i < _stars.Length; i++)
        {
            var x = Wrap(_stars[i].X + pan.X * _starDepth[i] - _view.X, _view.Width) + _view.X;
            var y = Wrap(_stars[i].Y + pan.Y * _starDepth[i] - _view.Y, _view.Height) + _view.Y;
            var twinkle = 0.88f + 0.12f * MathF.Sin(_time * (0.6f + i % 7 * 0.2f) + i);
            var brightness = _starBrightness[i] * twinkle;
            if (i >= 860)
            {
                // The brightest few: a soft dot with a small halo, no spikes.
                spriteBatch.Draw(_glow, new Vector2(x, y), null, MapArt.Additive(_starTint[i], brightness * 0.5f), 0f, new Vector2(64f, 64f), 9f / 128f * 2f, SpriteEffects.None, 0f);
                spriteBatch.Draw(_pixel, new Vector2(x, y), null, _starTint[i] * brightness, 0f, new Vector2(0.5f, 0.5f), 2f, SpriteEffects.None, 0f);
            }
            else
            {
                var size = i >= 620 ? 1.6f : 1f;
                spriteBatch.Draw(_pixel, new Vector2(x, y), null, _starTint[i] * brightness, 0f, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            }
        }
    }

    private static float Wrap(float value, float size)
    {
        var wrapped = value % size;
        return wrapped < 0f ? wrapped + size : wrapped;
    }
}
