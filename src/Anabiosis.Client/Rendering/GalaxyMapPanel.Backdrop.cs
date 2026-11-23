using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Deep space behind the helm's map: a nebula in the colours of the current star system and three layers of
// stars that slide at different speeds when the map is dragged. The console's round screen does not use it -
// that one is an instrument, not a window (GalaxyMapPanel.Sonar.cs).
public sealed partial class GalaxyMapPanel
{
    private const int NebulaWidth = 640;
    private const int NebulaHeight = 300;
    // How far the nebula may slide before its edge would show; the texture is drawn this much bigger than the canvas.
    private static readonly Vector2 NebulaMargin = new(40f, 20f);

    private readonly record struct BackStar(Vector2 Position, float Size, float Brightness, float Twinkle, float Speed, float Phase, float Parallax, Color Tint, bool Flare);

    private Texture2D? _nebula;
    private string? _spaceSystemId;
    private BackStar[] _backStars = Array.Empty<BackStar>();

    private void DrawSpace(SpriteBatch spriteBatch, string systemId, Vector2 panOffset, float totalSeconds)
    {
        if (_spaceSystemId != systemId)
        {
            _spaceSystemId = systemId;
            var seed = MapArt.Seed(systemId);
            _nebula?.Dispose();
            _nebula = MapArt.Nebula(_device, seed, NebulaWidth, NebulaHeight);
            _backStars = BuildBackStars(seed, _bounds);
        }

        var slide = Vector2.Clamp(panOffset * 0.012f, -NebulaMargin, NebulaMargin);
        spriteBatch.Draw(_nebula, new Rectangle(
            (int)(_bounds.X - NebulaMargin.X + slide.X), (int)(_bounds.Y - NebulaMargin.Y + slide.Y),
            (int)(_bounds.Width + NebulaMargin.X * 2f), (int)(_bounds.Height + NebulaMargin.Y * 2f)), Color.White);

        foreach (var star in _backStars)
        {
            var light = star.Brightness + star.Twinkle * MathF.Sin(totalSeconds * star.Speed + star.Phase);
            if (light <= 0.03f)
                continue;
            light = MathF.Min(light, 1f);
            var position = WrapInto(star.Position + panOffset * star.Parallax, _bounds);
            if (star.Flare)
                spriteBatch.Draw(_flare, position, null, MapArt.Additive(star.Tint, light), 0f, new Vector2(32f, 32f), star.Size / 64f, SpriteEffects.None, 0f);
            else
                spriteBatch.Draw(_pixel, position, null, star.Tint * light, 0f, new Vector2(0.5f, 0.5f), star.Size, SpriteEffects.None, 0f);
        }
    }

    private static BackStar[] BuildBackStars(int seed, Rectangle bounds)
    {
        var random = new Random(seed);
        var stars = new BackStar[360];
        for (var i = 0; i < stars.Length; i++)
        {
            // Far and faint, nearer and clearer, then a handful of bright ones with rays.
            var (parallax, size, brightness, flare) = i switch
            {
                < 250 => (0.010f + 0.02f * random.NextSingle(), 1f, 0.14f + 0.26f * random.NextSingle(), false),
                < 344 => (0.04f + 0.04f * random.NextSingle(), 1f + 0.7f * random.NextSingle(), 0.35f + 0.3f * random.NextSingle(), false),
                _ => (0.09f + 0.05f * random.NextSingle(), 14f + 20f * random.NextSingle(), 0.6f + 0.3f * random.NextSingle(), true),
            };
            // Star colour runs from blue-white to a warm yellow.
            var warmth = random.NextSingle();
            var tint = Color.Lerp(new Color(175, 205, 255), new Color(255, 226, 190), warmth);
            stars[i] = new BackStar(
                new Vector2(bounds.X + random.NextSingle() * bounds.Width, bounds.Y + random.NextSingle() * bounds.Height),
                size, brightness, 0.08f + 0.2f * random.NextSingle(), 0.5f + 1.6f * random.NextSingle(),
                random.NextSingle() * MathF.Tau, parallax, tint, flare);
        }
        return stars;
    }

    private static Vector2 WrapInto(Vector2 position, Rectangle bounds)
    {
        static float Wrap(float value, float min, float size)
        {
            var offset = (value - min) % size;
            return min + (offset < 0f ? offset + size : offset);
        }
        return new Vector2(Wrap(position.X, bounds.X, bounds.Width), Wrap(position.Y, bounds.Y, bounds.Height));
    }
}
