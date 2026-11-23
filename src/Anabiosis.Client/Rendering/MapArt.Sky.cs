using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The galactic map's own art: a nearly black sky with grey drifting dust (not the coloured nebulae of a single system),
// and the crests of the realms drawn on it. All baked from maths at startup, no image files.
internal static partial class MapArt
{
    // The sky behind the galaxy: black, with banks of cool grey dust and a few warm ones, thinned by dark gaps.
    internal static Texture2D DustSky(GraphicsDevice device, int seed, int width, int height) => Bake(device, width, height, (x, y) =>
    {
        var u = x / 110f;
        var v = y / 110f;
        var wx = u + 1.9f * (Fbm(u * 0.6f, v * 0.6f, seed + 5, 3) - 0.5f);
        var wy = v + 1.9f * (Fbm(u * 0.6f + 3.1f, v * 0.6f + 8.7f, seed + 17, 3) - 0.5f);
        var cool = Smooth(0.46f, 0.82f, Fbm(wx, wy, seed, 5));
        var warm = Smooth(0.52f, 0.86f, Fbm(wx * 1.2f + 7f, wy * 1.2f + 2f, seed + 41, 5));
        var gaps = Smooth(0.50f, 0.78f, Fbm(wx * 2.2f + 1f, wy * 2.2f + 5f, seed + 77, 4));
        var shade = 1f - 0.55f * gaps;
        var light = (new Vector3(0.40f, 0.44f, 0.52f) * (cool * 0.115f) + new Vector3(0.55f, 0.47f, 0.38f) * (warm * 0.085f)) * shade;
        return new Color(0.010f + light.X, 0.012f + light.Y, 0.020f + light.Z, 1f);
    });

    // A line profile for smooth thin lines: a strip 4 long and 16 across, solid in the middle and fading to nothing at the
    // sides, so a rotated, stretched copy of it is antialiased by the texture filter instead of stepping.
    internal static Texture2D SoftLine(GraphicsDevice device) => Bake(device, 4, 16, (x, y) =>
    {
        var v = MathF.Abs((y + 0.5f) / 8f - 1f);
        return Premultiplied(1f, 1f, 1f, 1f - Smooth(0.30f, 1f, v));
    });

    private static float Edge(float distance, float soft = 0.045f) => 1f - Smooth(-soft, soft, distance);

    // The crest of the Colonial Union: a ring around an eight-pointed star, with four lights between the points.
    internal static Texture2D UnionCrest(GraphicsDevice device) => Bake(device, 128, 128, (x, y) =>
    {
        var (r, dx, dy) = Radial(x, y, 128);
        var angle = MathF.Atan2(dy, dx);
        var ring = Edge(MathF.Abs(r - 0.88f) - 0.065f);
        var spike = 0.20f + 0.58f * MathF.Pow(MathF.Abs(MathF.Cos(angle * 4f)), 2.6f);
        var star = Edge(r - spike) * (1f - Edge(r - 0.11f)); // the star with a small round hole in its middle
        var lights = 0f;
        for (var k = 0; k < 4; k++)
        {
            var a = MathF.PI / 4f + k * MathF.PI / 2f;
            var d = MathF.Sqrt(MathF.Pow(dx - MathF.Cos(a) * 0.62f, 2) + MathF.Pow(dy - MathF.Sin(a) * 0.62f, 2));
            lights = MathF.Max(lights, Edge(d - 0.055f));
        }
        return Premultiplied(1f, 1f, 1f, MathF.Max(MathF.Max(ring, star), lights));
    });

    // The crest of the virus: three claws curling round a core.
    internal static Texture2D VirusCrest(GraphicsDevice device) => Bake(device, 128, 128, (x, y) =>
    {
        var (r, dx, dy) = Radial(x, y, 128);
        var angle = MathF.Atan2(dy, dx);
        var claws = 0f;
        for (var k = 0; k < 3; k++)
        {
            var around = angle - (k * MathF.Tau / 3f + r * 1.15f);
            around = MathF.Atan2(MathF.Sin(around), MathF.Cos(around));
            var half = 0.62f * MathF.Max(0f, 1f - r * 0.95f) + 0.03f;
            if (r > 0.20f && r < 0.96f)
                claws = MathF.Max(claws, Edge(MathF.Abs(around) - half, 0.06f) * Smooth(0.18f, 0.26f, r) * (1f - Smooth(0.9f, 0.97f, r)));
        }
        var core = Edge(r - 0.17f);
        var halo = Edge(MathF.Abs(r - 0.28f) - 0.018f, 0.03f) * 0.7f;
        return Premultiplied(1f, 1f, 1f, MathF.Max(MathF.Max(claws, core), halo));
    });
}
