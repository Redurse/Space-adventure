using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The rock's surface, baked once per asteroid into a texture instead of being re-assembled every
// frame out of a few dozen shaded quads. The quad version could only ever say "lighter on this
// side, darker on that one"; a baked image can carry a real relief - a height field lit per texel,
// with craters, fissures, mineral veins and a weathered crust - which is what makes Barotrauma's
// walls read as stone rather than as a coloured polygon.
//
// Everything derives from the asteroid's id, so a rock looks the same in every session, and the
// mask is AsteroidShape's own radius function, so the picture and the collision are still the same
// outline by construction - the whole point of the polygon rework.
public static class AsteroidTexture
{
    // The image covers a bit more than the nominal radius, because the outline pushes out past it.
    public const float PaddingFactor = 1.25f;
    private const int MinSide = 224;
    private const int MaxSide = 640;

    // Sunlight is baked in world space, so it stays put while the field swings around the (always
    // upright) ship - a star doesn't orbit the helm.
    // cos/sin of 0.6 rad, the turn applied to the noise domain between octaves.
    private const float DomainCos = 0.8253356f;
    private const float DomainSin = 0.5646425f;

    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(-0.52f, -0.70f, 0.49f));
    // Halfway between the star and the camera, which looks straight down at the field.
    private static readonly Vector3 HalfwayDirection = Vector3.Normalize(LightDirection + Vector3.UnitZ);
    private static readonly Vector3 SunColor = new(1.06f, 1.00f, 0.89f);
    private static readonly Vector3 SkyColor = new(0.30f, 0.35f, 0.47f);
    private static readonly Vector3 DarkRock = new(54 / 255f, 50 / 255f, 47 / 255f);
    private static readonly Vector3 LightRock = new(152 / 255f, 141 / 255f, 124 / 255f);
    private static readonly Vector3 IronStain = new(128 / 255f, 84 / 255f, 50 / 255f);
    private static readonly Vector3 ColdBasalt = new(88 / 255f, 94 / 255f, 104 / 255f);
    private static readonly Vector3 MineralVein = new(166 / 255f, 186 / 255f, 178 / 255f);

    public readonly record struct Skin(Texture2D Texture, float HalfExtentUnits);

    public static Skin Bake(GraphicsDevice device, Asteroid asteroid)
    {
        var (pixels, side, half) = BakePixels(asteroid);
        var texture = new Texture2D(device, side, side);
        texture.SetData(pixels);
        return new Skin(texture, half);
    }

    // The image itself, with no graphics device involved - which is also what lets it be rendered
    // out to a file and looked at without launching the game.
    public static (Color[] Pixels, int Side, float HalfExtentUnits) BakePixels(Asteroid asteroid)
    {
        var factors = AsteroidShape.RadiusFactors(asteroid.Id);
        var seed = Seed(asteroid.Id);
        var half = asteroid.Radius * PaddingFactor;
        // Resolution follows the rock's real size on screen, so a boulder doesn't get the same
        // budget as a mountain - and is capped, because baking is CPU work done at the moment the
        // field appears.
        var side = Math.Clamp((int)(asteroid.Radius * ShipRenderer.PixelsPerUnit * 1.6f), MinSide, MaxSide);
        var unitsPerTexel = half * 2f / side;

        var frequency = 2.4f / asteroid.Radius; // a couple of big masses across the rock, then detail below them
        var relief = asteroid.Radius * 0.13f;
        var craters = MakeCraters(asteroid, seed);

        // Pass one: the height field, computed everywhere including outside the outline, so slopes
        // at the very edge are taken from real neighbours instead of falling off a cliff.
        var heights = new float[side * side];
        var reliefOnly = new float[side * side];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var p = TexelToUnits(x, y, side, half);
                var noise = Relief(p, frequency, relief, seed);

                foreach (var crater in craters)
                    noise += CraterHeight(crater, p, frequency, seed);

                reliefOnly[y * side + x] = noise;
                var radial = p.Length();
                var surface = SurfaceRadius(asteroid, factors, p);
                // A gentle dome under the crags, so the rock is a body with a curved back rather
                // than a flat slab that happens to be jagged at the edges. Kept shallow on purpose:
                // any more and the surface detail reads as decals stuck onto a balloon.
                var dome = radial >= surface ? 0f : MathF.Sqrt(1f - radial * radial / (surface * surface)) * asteroid.Radius * 0.17f;
                heights[y * side + x] = noise + dome;
            }
        }

        // Pass two: light it.
        var pixels = new Color[side * side];
        var crustDepth = asteroid.Radius * 0.07f;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var index = y * side + x;
                var p = TexelToUnits(x, y, side, half);
                var radial = p.Length();
                var surface = SurfaceRadius(asteroid, factors, p);

                // One texel of feathering at the outline - the silhouette is the shape everything
                // else in the game agrees on, so it should be crisp but not stair-stepped.
                var coverage = MathHelper.Clamp((surface - radial) / unitsPerTexel + 0.5f, 0f, 1f);
                if (coverage <= 0f)
                {
                    pixels[index] = Color.Transparent;
                    continue;
                }

                var normal = NormalAt(heights, side, x, y, unitsPerTexel);
                var diffuse = MathF.Max(0f, Vector3.Dot(normal, LightDirection));
                // Cavities sit in their own shade: the crater floors and fissure bottoms stay dark
                // even where they happen to face the sun.
                var ambientOcclusion = MathHelper.Clamp(0.78f + reliefOnly[index] / (relief * 3.6f), 0.60f, 1.06f);
                // A dull mineral sheen on the crests facing the star - stone isn't a mirror, but a
                // surface with no highlight at all never quite stops looking like paper.
                var specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, HalfwayDirection)), 22f) * 0.16f;

                var albedo = AlbedoAt(p, frequency, seed);
                var color = albedo * (SkyColor * 0.78f * ambientOcclusion + SunColor * diffuse * ambientOcclusion)
                            + SunColor * specular * ambientOcclusion;

                // Weathered crust: a dark rind just inside the outline reads as thickness, and is
                // what keeps rocks legible against each other when two overlap on screen.
                var crust = MathHelper.Clamp((surface - radial) / crustDepth, 0f, 1f);
                color *= 0.56f + 0.44f * crust;

                // Sunward edges catch a bright rim - the single cheapest thing that separates a
                // rock from the black behind it.
                if (radial > 0.0001f)
                {
                    var facing = MathF.Max(0f, Vector2.Dot(p / radial, new Vector2(LightDirection.X, LightDirection.Y)));
                    var edge = (1f - crust) * (1f - crust);
                    color += SunColor * facing * edge * 0.62f;
                }

                pixels[index] = ToPremultiplied(color, coverage);
            }
        }

        return (pixels, side, half);
    }

    // Stone is lumpy, not wavy. The mass of the rock comes from billow noise, which folds the
    // waveform and piles it into rounded knots; the crags come from a ridged layer above it; the
    // tooth of the surface from a fine grain that only shows up as roughness once the light hits
    // it. Plain fBm on its own gives smooth dunes, and a wide thresholded ridge - which is what
    // this was first - gives long connected channels that read as a jigsaw, not as rock.
    private static float Relief(Vector2 p, float frequency, float amplitude, int seed)
    {
        // Each layer is sampled on its own turned axes for the same reason the octaves are: layers
        // that share a grid pile their artifacts on top of each other.
        var mp = p * frequency;
        var cp = Rotate(p, 0.9f) * frequency * 2.7f;
        var gp = Rotate(p, 2.1f) * frequency * 13f;
        var fp = Rotate(p, 1.5f) * frequency * 1.8f;

        var mass = (Fractal(mp.X, mp.Y, seed, 4, NoiseShape.Billow) - 0.42f) * amplitude * 1.2f;
        var crags = (Fractal(cp.X, cp.Y, seed + 211, 4, NoiseShape.Ridge) - 0.5f) * amplitude * 0.5f;
        var grain = (Fractal(gp.X, gp.Y, seed + 733, 2, NoiseShape.Value) - 0.5f) * amplitude * 0.15f;

        // Fissures: only the very crest of a ridged field, raised to a high power so what survives
        // is a hairline fracture rather than a valley, and gated by a second field so the cracks
        // run in patches across the rock instead of webbing all of it.
        var ridge = Fractal(fp.X, fp.Y, seed + 401, 2, NoiseShape.Ridge);
        var gate = Fractal(p.X * frequency * 0.9f, p.Y * frequency * 0.9f, seed + 911, 2, NoiseShape.Value);
        var fissure = MathF.Pow(MathHelper.Clamp((ridge - 0.58f) / 0.42f, 0f, 1f), 10f)
                      * MathHelper.Clamp((gate - 0.38f) / 0.28f, 0f, 1f);

        return mass + crags + grain - fissure * amplitude * 0.9f;
    }

    private static Vector2 TexelToUnits(int x, int y, int side, float half) =>
        new(((x + 0.5f) / side * 2f - 1f) * half, ((y + 0.5f) / side * 2f - 1f) * half);

    // AsteroidShape.RadiusAt, with the per-id factors hoisted out of the loop: it rebuilds and
    // re-smooths the whole ring on every call, which is fine for a physics query and ruinous for a
    // couple of hundred thousand texels.
    private static float SurfaceRadius(Asteroid asteroid, float[] factors, Vector2 fromCenter)
    {
        var angle = MathF.Atan2(fromCenter.Y, fromCenter.X);
        if (angle < 0)
            angle += MathF.PI * 2f;

        var slot = angle / (MathF.PI * 2f / AsteroidShape.VertexCount);
        var low = (int)MathF.Floor(slot) % AsteroidShape.VertexCount;
        var high = (low + 1) % AsteroidShape.VertexCount;
        var blend = slot - MathF.Floor(slot);
        return asteroid.Radius * (factors[low] * (1f - blend) + factors[high] * blend);
    }

    private static Vector3 NormalAt(float[] heights, int side, int x, int y, float unitsPerTexel)
    {
        var left = heights[y * side + Math.Max(0, x - 1)];
        var right = heights[y * side + Math.Min(side - 1, x + 1)];
        var up = heights[Math.Max(0, y - 1) * side + x];
        var down = heights[Math.Min(side - 1, y + 1) * side + x];
        return Vector3.Normalize(new Vector3(
            -(right - left) / (2f * unitsPerTexel),
            -(down - up) / (2f * unitsPerTexel),
            1f));
    }

    // Stone colour before any light hits it: a mottled base, rusty staining in patches, and the
    // occasional pale mineral seam running through it.
    private static Vector3 AlbedoAt(Vector2 p, float frequency, int seed)
    {
        var broad = Fbm(p.X * frequency * 0.7f, p.Y * frequency * 0.7f, seed + 31, 3);
        var grit = Fbm(p.X * frequency * 5f, p.Y * frequency * 5f, seed + 57, 2);
        var color = Vector3.Lerp(DarkRock, LightRock, MathHelper.Clamp(broad * 0.78f + grit * 0.22f, 0f, 1f));

        // Two mineralogies rather than one tint over everything: rust where the rock has been
        // weathered, cold grey basalt where it hasn't. Sampled from the same field at opposite
        // ends, so a rock is never both in the same place.
        var iron = Fbm(p.X * frequency * 1.3f, p.Y * frequency * 1.3f, seed + 91, 3);
        if (iron > 0.58f)
            color = Vector3.Lerp(color, IronStain, (iron - 0.58f) / 0.42f * 0.7f);
        else if (iron < 0.40f)
            color = Vector3.Lerp(color, ColdBasalt, (0.40f - iron) / 0.40f * 0.6f);

        var vein = 1f - MathF.Abs(ValueNoise(p.X * frequency * 1.9f, p.Y * frequency * 1.9f, seed + 137) * 2f - 1f);
        if (vein > 0.87f)
            color = Vector3.Lerp(color, MineralVein, (vein - 0.87f) / 0.13f * 0.5f);

        return color;
    }

    private static (Vector2 Center, float Radius, float Depth)[] MakeCraters(Asteroid asteroid, int seed)
    {
        var random = new Random(seed ^ 0x51ED27);
        var craters = new (Vector2, float, float)[2 + random.Next(3)];
        for (var i = 0; i < craters.Length; i++)
        {
            var angle = (float)(random.NextDouble() * MathF.PI * 2);
            var reach = MathF.Sqrt((float)random.NextDouble()) * asteroid.Radius * 0.58f;
            var radius = asteroid.Radius * (0.13f + (float)random.NextDouble() * 0.16f);
            craters[i] = (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach, radius, radius * 0.26f);
        }
        return craters;
    }

    // A bowl with a raised lip - the lip is what makes an impact read as a hole punched into the
    // surface rather than a dark smudge painted onto it. The radius is worried by noise before the
    // profile is evaluated, because a mathematically round crater is the fastest way to make a rock
    // look like a cartoon.
    private static float CraterHeight((Vector2 Center, float Radius, float Depth) crater, Vector2 p, float frequency, int seed)
    {
        var offset = p - crater.Center;
        var distance = offset.Length();
        if (distance >= crater.Radius * 1.35f)
            return 0f;

        var wobble = 0.78f + Fractal(p.X * frequency * 3.4f, p.Y * frequency * 3.4f, seed + 617, 2, NoiseShape.Value) * 0.44f;
        var t = distance / (crater.Radius * wobble);
        if (t >= 1f)
            return 0f;

        var bowl = -crater.Depth * (1f - t * t);
        var lip = t > 0.74f ? crater.Depth * 0.5f * (1f - MathF.Abs(t - 0.87f) / 0.13f) : 0f;
        return bowl + MathF.Max(0f, lip);
    }

    private static Color ToPremultiplied(Vector3 color, float alpha)
    {
        var r = MathHelper.Clamp(color.X, 0f, 1f) * alpha;
        var g = MathHelper.Clamp(color.Y, 0f, 1f) * alpha;
        var b = MathHelper.Clamp(color.Z, 0f, 1f) * alpha;
        return new Color(r, g, b, alpha);
    }

    private enum NoiseShape
    {
        Value, // smooth hills
        Billow, // folded at the midline: rounded knots and lumps
        Ridge, // folded and inverted: sharp crests with wide valleys
    }

    private static float Fbm(float x, float y, int seed, int octaves) =>
        Fractal(x, y, seed, octaves, NoiseShape.Value);

    private static float Fractal(float x, float y, int seed, int octaves, NoiseShape shape)
    {
        var sum = 0f;
        var amplitude = 0.5f;
        var total = 0f;
        var px = x;
        var py = y;
        for (var i = 0; i < octaves; i++)
        {
            var n = ValueNoise(px, py, seed + i * 7919);
            n = shape switch
            {
                NoiseShape.Billow => MathF.Abs(n * 2f - 1f),
                NoiseShape.Ridge => 1f - MathF.Abs(n * 2f - 1f),
                _ => n,
            };
            sum += n * amplitude;
            total += amplitude;
            amplitude *= 0.5f;

            // Each octave is rotated as well as scaled. Value noise is built on a square grid, so
            // stacking octaves on the same axes lines their diamonds up and the surface comes out
            // looking woven - a fabric weave, which is exactly what stone must not look like.
            // Turning the domain between octaves scatters that alignment. The 2.03 rather than a
            // flat 2 keeps the grids from re-registering either.
            var rx = px * DomainCos - py * DomainSin;
            var ry = px * DomainSin + py * DomainCos;
            px = rx * 2.03f + 17.31f;
            py = ry * 2.03f - 9.77f;
        }
        return sum / total;
    }

    private static Vector2 Rotate(Vector2 p, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos);
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);

        var top = MathHelper.Lerp(Hash01(x0, y0, seed), Hash01(x0 + 1, y0, seed), fx);
        var bottom = MathHelper.Lerp(Hash01(x0, y0 + 1, seed), Hash01(x0 + 1, y0 + 1, seed), fx);
        return MathHelper.Lerp(top, bottom, fy);
    }

    private static float Hash01(int x, int y, int seed)
    {
        unchecked
        {
            var h = seed + x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7FFFFF) / (float)0x7FFFFF;
        }
    }

    // Same stable hash the shape itself uses - string.GetHashCode is randomised per process, which
    // would hand every rock a new face on each launch.
    public static int Seed(string id)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in id)
                hash = (hash ^ c) * 16777619;
            return (hash & 0x7FFFFFFF) ^ 0x5F5F5F;
        }
    }
}
