using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Everything the system map (GalaxyMapPanel) paints with that is not a flat line or a font glyph: soft glows,
// the nebula behind a system, the star's corona, belt dust, planet rings, fog puffs. All of it is computed
// from noise at startup or once per star system - no image files. Every texture is premultiplied alpha, the
// way SpriteBatch's default blend expects; drawing one with a tint whose alpha is zero (Additive below) adds
// its light to the picture instead of covering it, which is how the glows are done without a second batch.
internal static partial class MapArt
{
    // A tint that adds light instead of covering what is under it.
    internal static Color Additive(Color color, float strength = 1f) =>
        new((int)(color.R * strength), (int)(color.G * strength), (int)(color.B * strength), 0);

    internal static int Seed(string id)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in id)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }

    internal static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            var n = x * 374761393 + y * 668265263 + seed * 1442695041;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
        }
    }

    internal static float Noise(float x, float y, int seed)
    {
        var ix = (int)MathF.Floor(x);
        var iy = (int)MathF.Floor(y);
        var fx = x - ix;
        var fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        return MathHelper.Lerp(
            MathHelper.Lerp(Hash(ix, iy, seed), Hash(ix + 1, iy, seed), fx),
            MathHelper.Lerp(Hash(ix, iy + 1, seed), Hash(ix + 1, iy + 1, seed), fx), fy);
    }

    internal static float Fbm(float x, float y, int seed, int octaves = 4)
    {
        var sum = 0f;
        var amplitude = 0.5f;
        var total = 0f;
        for (var i = 0; i < octaves; i++)
        {
            sum += amplitude * Noise(x, y, seed + i * 31);
            total += amplitude;
            x *= 2.03f;
            y *= 2.03f;
            amplitude *= 0.5f;
        }
        return sum / total;
    }

    internal static float Smooth(float from, float to, float value)
    {
        var t = Math.Clamp((value - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static Color Premultiplied(float r, float g, float b, float a)
    {
        a = Math.Clamp(a, 0f, 1f);
        return new Color(Math.Clamp(r, 0f, 1f) * a, Math.Clamp(g, 0f, 1f) * a, Math.Clamp(b, 0f, 1f) * a, a);
    }

    private static Texture2D Bake(GraphicsDevice device, int width, int height, Func<int, int, Color> texel)
    {
        var pixels = new Color[width * height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            pixels[y * width + x] = texel(x, y);
        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    // Distance from the centre of a square texture, 0 in the middle and 1 at the edge, plus the direction.
    private static (float Distance, float Dx, float Dy) Radial(int x, int y, int size)
    {
        var half = (size - 1) / 2f;
        var dx = (x - half) / half;
        var dy = (y - half) / half;
        return (MathF.Sqrt(dx * dx + dy * dy), dx, dy);
    }

    // A round glow that is brightest in the middle and gone by the edge.
    internal static Texture2D Glow(GraphicsDevice device) => Bake(device, 128, 128, (x, y) =>
    {
        var (d, _, _) = Radial(x, y, 128);
        var a = d >= 1f ? 0f : MathF.Pow(1f - d, 2.2f);
        return Premultiplied(1f, 1f, 1f, a);
    });

    // A bright star: a small core with four thin rays.
    internal static Texture2D Flare(GraphicsDevice device) => Bake(device, 64, 64, (x, y) =>
    {
        var (d, dx, dy) = Radial(x, y, 64);
        var core = MathF.Exp(-d * d * 60f);
        var reach = MathF.Max(0f, 1f - d);
        var rays = MathF.Exp(-MathF.Abs(dy) * 42f) + MathF.Exp(-MathF.Abs(dx) * 42f);
        return Premultiplied(1f, 1f, 1f, core + 0.55f * rays * reach * reach * reach);
    });

    private static readonly (Vector3 A, Vector3 B)[] NebulaHues =
    {
        (new Vector3(0.50f, 0.28f, 0.95f), new Vector3(0.12f, 0.70f, 0.82f)), // violet and teal
        (new Vector3(0.20f, 0.42f, 1.00f), new Vector3(0.90f, 0.26f, 0.62f)), // blue and magenta
        (new Vector3(0.10f, 0.72f, 0.66f), new Vector3(0.95f, 0.58f, 0.22f)), // teal and amber
        (new Vector3(0.92f, 0.24f, 0.30f), new Vector3(0.42f, 0.30f, 0.90f)), // crimson and violet
        (new Vector3(0.18f, 0.78f, 0.48f), new Vector3(0.22f, 0.44f, 0.95f)), // green and blue
    };

    // The deep-space backdrop of one star system: two clouds of its own colours, cut by dark dust lanes.
    // Kept dim on purpose - the map content has to stay readable on top of it.
    internal static Texture2D Nebula(GraphicsDevice device, int seed, int width, int height)
    {
        var (hueA, hueB) = NebulaHues[(int)((uint)seed % (uint)NebulaHues.Length)];
        return Bake(device, width, height, (x, y) =>
        {
            var u = x / 96f;
            var v = y / 96f;
            var wx = u + 1.7f * (Fbm(u * 0.7f, v * 0.7f, seed + 11, 3) - 0.5f);
            var wy = v + 1.7f * (Fbm(u * 0.7f + 5.2f, v * 0.7f + 1.3f, seed + 23, 3) - 0.5f);
            var cloudA = Smooth(0.42f, 0.80f, Fbm(wx, wy, seed, 5));
            var cloudB = Smooth(0.47f, 0.84f, Fbm(wx * 1.3f + 9f, wy * 1.3f + 4f, seed + 57, 5));
            var lanes = Smooth(0.50f, 0.76f, Fbm(wx * 2.1f + 3f, wy * 2.1f + 7f, seed + 91, 4));
            var shade = 1f - 0.6f * lanes;
            var light = (hueA * (cloudA * 0.30f) + hueB * (cloudB * 0.24f)) * shade;
            return new Color(0.027f + light.X, 0.031f + light.Y, 0.052f + light.Z, 1f);
        });
    }

    // The star's corona: light that thins out with distance, combed into rays.
    internal static Texture2D Corona(GraphicsDevice device) => Bake(device, 256, 256, (x, y) =>
    {
        var (d, dx, dy) = Radial(x, y, 256);
        if (d >= 1f)
            return Color.Transparent;
        var length = MathF.Max(d, 0.0001f);
        var cx = dx / length;
        var cy = dy / length;
        var rays = Fbm(cx * 3.2f + 10f, cy * 3.2f + 10f, 7, 3);
        var streamers = MathF.Pow(Noise(cx * 7.5f + 30f, cy * 7.5f + 30f, 19), 3f);
        var fade = 1f - d;
        var a = fade * fade * (0.45f + 0.55f * rays) + MathF.Pow(fade, 1.3f) * streamers * 0.5f;
        return Premultiplied(1f, 0.80f + 0.2f * fade, 0.45f + 0.5f * fade * fade, a);
    });

    // The star itself: white-hot in the middle, orange at the limb, with a grain of convection cells.
    internal static Texture2D SunDisc(GraphicsDevice device) => Bake(device, 192, 192, (x, y) =>
    {
        var (d, dx, dy) = Radial(x, y, 192);
        if (d >= 1f)
            return Color.Transparent;
        var limb = MathF.Sqrt(1f - d * d);
        var cells = Fbm(dx * 9f + 40f, dy * 9f + 40f, 3, 3);
        var heat = 0.55f + 0.45f * limb + (cells - 0.5f) * 0.22f;
        var edge = Smooth(1f, 0.96f, d);
        return Premultiplied(1f, 0.62f + 0.38f * heat, 0.22f + 0.70f * heat * heat, edge);
    });

    // The zone around the star where a hull burns: a red haze that thickens toward its own edge.
    internal static Texture2D HeatZone(GraphicsDevice device) => Bake(device, 256, 256, (x, y) =>
    {
        var (d, _, _) = Radial(x, y, 256);
        if (d >= 1f)
            return Color.Transparent;
        var a = 0.10f + 0.20f * d * d * d + 0.35f * Smooth(0.955f, 0.99f, d);
        return Premultiplied(1f, 0.26f, 0.12f, a * Smooth(1f, 0.992f, d));
    });

    // An asteroid belt seen from above: a band of dust between two orbits, clumped rather than even.
    internal static Texture2D DustRing(GraphicsDevice device, int seed, float innerFraction) => Bake(device, 512, 512, (x, y) =>
    {
        var (d, dx, dy) = Radial(x, y, 512);
        if (d >= 1f || d <= innerFraction)
            return Color.Transparent;
        var across = (d - innerFraction) / (1f - innerFraction);
        var profile = MathF.Pow(MathF.Sin(across * MathF.PI), 0.8f);
        var clumps = Fbm(dx * 7f + 20f, dy * 7f + 20f, seed, 4);
        var streaks = Noise(across * 9f, MathF.Atan2(dy, dx) * 2f, seed + 5);
        var a = profile * (0.18f + 0.62f * Smooth(0.30f, 0.80f, clumps)) * (0.75f + 0.25f * streaks);
        return Premultiplied(0.74f, 0.64f, 0.52f, a * 0.55f);
    });

    // A gas giant's rings, face on; the map squashes them into an ellipse when it draws them.
    internal static Texture2D PlanetRing(GraphicsDevice device) => Bake(device, 256, 256, (x, y) =>
    {
        var (d, _, _) = Radial(x, y, 256);
        const float inner = 0.56f;
        if (d >= 1f || d <= inner)
            return Color.Transparent;
        var across = (d - inner) / (1f - inner);
        var bands = 0.35f + 0.65f * Noise(across * 14f, 0.5f, 77);
        var gap = Smooth(0.40f, 0.46f, across) * Smooth(0.60f, 0.54f, across); // one dark division
        var a = bands * (1f - 0.8f * gap) * Smooth(0f, 0.06f, across) * Smooth(1f, 0.94f, across);
        return Premultiplied(0.92f, 0.86f, 0.74f, a * 0.8f);
    });

    // A lump of cloud, for the fog of war to drift.
    internal static Texture2D CloudPuff(GraphicsDevice device) => Bake(device, 128, 128, (x, y) =>
    {
        var (d, dx, dy) = Radial(x, y, 128);
        if (d >= 1f)
            return Color.Transparent;
        var shape = MathF.Pow(1f - d, 1.4f) * Smooth(0.25f, 0.75f, Fbm(dx * 2.6f + 8f, dy * 2.6f + 8f, 41, 4));
        return Premultiplied(1f, 1f, 1f, shape);
    });

    // The dark corners of a round screen: clear in the middle, shaded toward the rim.
    internal static Texture2D Vignette(GraphicsDevice device) => Bake(device, 256, 256, (x, y) =>
    {
        var (d, _, _) = Radial(x, y, 256);
        return Premultiplied(0f, 0f, 0f, 0.8f * Smooth(0.55f, 1.02f, d));
    });
}
