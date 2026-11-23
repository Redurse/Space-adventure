using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The glow of the galaxy itself, and the colours of its stars. Under the stars Stellaris lays a haze that follows the shape of
// the galaxy - warm toward the core, cool toward the rim, thick along the arms and torn by dark dust - so even with no realm
// painted over it the spiral reads. Here the haze is baked once from the positions of all the systems and drawn as light.
// A star takes its own colour from a hash of its index: blue-white, white, yellow, orange, red.
public sealed partial class GalacticMapPanel
{
    private const int HazeSize = 256;
    private const float HazeMargin = 320f;
    private const float HazeSigma = 130f;

    private Texture2D? _haze;
    private int _hazeForSystems = -1;
    private Vector2 _hazeOrigin;
    private Vector2 _hazeExtent;

    private static readonly Color[] StarTints =
    {
        new(170, 200, 255), new(206, 224, 255), new(255, 246, 232), new(255, 246, 232), new(255, 240, 222),
        new(255, 226, 160), new(255, 226, 160), new(255, 178, 110), new(255, 132, 100),
    };

    private static Color StarTint(int index) => StarTints[(int)(Hash01(index, 77) * StarTints.Length) % StarTints.Length];

    private void DrawHaze(SpriteBatch spriteBatch, IReadOnlyList<StarSystemSummary> systems, Vector2 origin, float scale)
    {
        if (_hazeForSystems != systems.Count)
            BakeHaze(systems);
        var topLeft = origin + _hazeOrigin * scale;
        var size = _hazeExtent * scale;
        spriteBatch.Draw(_haze, new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)MathF.Ceiling(size.X), (int)MathF.Ceiling(size.Y)), Color.White);
    }

    private void BakeHaze(IReadOnlyList<StarSystemSummary> systems)
    {
        _hazeForSystems = systems.Count;
        var minX = systems.Min(s => s.GalaxyX) - HazeMargin;
        var minY = systems.Min(s => s.GalaxyY) - HazeMargin;
        var maxX = systems.Max(s => s.GalaxyX) + HazeMargin;
        var maxY = systems.Max(s => s.GalaxyY) + HazeMargin;
        _hazeOrigin = new Vector2(minX, minY);
        _hazeExtent = new Vector2(maxX - minX, maxY - minY);
        var center = new Vector2(systems.Average(s => s.GalaxyX), systems.Average(s => s.GalaxyY));
        var radius = systems.Max(s => Vector2.Distance(new Vector2(s.GalaxyX, s.GalaxyY), center));

        var density = new float[HazeSize * HazeSize];
        var cellX = _hazeExtent.X / HazeSize;
        var cellY = _hazeExtent.Y / HazeSize;
        var reach = HazeSigma * 2.6f;
        foreach (var system in systems)
        {
            var cx = (system.GalaxyX - minX) / cellX;
            var cy = (system.GalaxyY - minY) / cellY;
            var rx = (int)MathF.Ceiling(reach / cellX);
            var ry = (int)MathF.Ceiling(reach / cellY);
            for (var y = Math.Max(0, (int)cy - ry); y <= Math.Min(HazeSize - 1, (int)cy + ry); y++)
            for (var x = Math.Max(0, (int)cx - rx); x <= Math.Min(HazeSize - 1, (int)cx + rx); x++)
            {
                var dx = (x + 0.5f - cx) * cellX;
                var dy = (y + 0.5f - cy) * cellY;
                density[y * HazeSize + x] += MathF.Exp(-(dx * dx + dy * dy) / (2f * HazeSigma * HazeSigma));
            }
        }

        var pixels = new Color[HazeSize * HazeSize];
        for (var y = 0; y < HazeSize; y++)
        for (var x = 0; x < HazeSize; x++)
        {
            var wx = minX + (x + 0.5f) * cellX;
            var wy = minY + (y + 0.5f) * cellY;
            var thick = 1f - MathF.Exp(-density[y * HazeSize + x] * 0.55f);
            // Dark dust tears the haze into banks.
            var dust = MapArt.Smooth(0.30f, 0.70f, MapArt.Fbm(wx / 210f, wy / 210f, 9917, 4));
            var rim = Math.Clamp(Vector2.Distance(new Vector2(wx, wy), center) / radius, 0f, 1.2f);
            var warm = new Vector3(1.00f, 0.74f, 0.52f);
            var cool = new Vector3(0.40f, 0.58f, 1.00f);
            var tint = Vector3.Lerp(warm, cool, MapArt.Smooth(0.05f, 0.85f, rim));
            var amount = thick * (0.35f + 0.65f * dust) * 0.30f * (1f - 0.55f * MapArt.Smooth(0.7f, 1.2f, rim));
            pixels[y * HazeSize + x] = new Color(tint.X * amount, tint.Y * amount, tint.Z * amount, 0f);
        }
        _haze?.Dispose();
        _haze = new Texture2D(_device, HazeSize, HazeSize);
        _haze.SetData(pixels);
    }
}
