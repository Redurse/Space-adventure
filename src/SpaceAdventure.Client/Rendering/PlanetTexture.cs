using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// A celestial body's own surface, baked once per body id (M55 follow-up - "почему на месте
// планет пустота": the flat shaded circle FieldRenderer.DrawCelestialBodies drew read as nothing
// actually rendered once close enough to fill the screen). Same baked-height-field technique
// AsteroidTexture already uses for rocks, just against a plain circular mask instead of an
// irregular outline (bodies here ARE round) - cratered relief for Rocky/Moon, banded cloud bands
// for GasGiant/IceGiant, and left alone for Star (a flat glow already reads fine for something
// that isn't a solid surface to begin with). Baked on a background task exactly like
// AsteroidTexture now is (FieldRenderer.DrawAsteroid) - a bake at this resolution costs the same
// multi-hundred-ms as a rock's own, and doing it synchronously on the render thread was the exact
// cause of the earlier M51/M55 frame-lag bug already fixed there.
public static class PlanetTexture
{
    private const int MinSide = 256;
    // Capped harder than a rock's own MaxSide (640) - a body fills far more of the screen at close
    // range, so the same texel budget has to stretch further; detail this size still reads fine
    // at the distances a body is actually looked at from (nobody presses their nose to the glass).
    private const int MaxSide = 512;
    private const float PixelsPerUnit = 1f; // resolution follows the body's own radius, same idea as AsteroidTexture

    private const float DomainCos = 0.8253356f;
    private const float DomainSin = 0.5646425f;

    private static readonly Vector3 LightDirection = Vector3.Normalize(new Vector3(-0.52f, -0.70f, 0.49f));
    private static readonly Vector3 HalfwayDirection = Vector3.Normalize(LightDirection + Vector3.UnitZ);
    private static readonly Vector3 SunColor = new(1.06f, 1.00f, 0.89f);
    private static readonly Vector3 SkyColor = new(0.30f, 0.35f, 0.47f);

    public readonly record struct Skin(Texture2D Texture, float HalfExtentUnits);

    public static Skin Bake(GraphicsDevice device, CelestialBody body)
    {
        var (pixels, side, half) = BakePixels(body);
        var texture = new Texture2D(device, side, side);
        texture.SetData(pixels);
        return new Skin(texture, half);
    }

    // No GraphicsDevice touched - runs on a background Task, same convention AsteroidTexture's
    // own BakePixels already established.
    public static (Color[] Pixels, int Side, float HalfExtentUnits) BakePixels(CelestialBody body)
    {
        var seed = Seed(body.Id);
        var half = body.Radius;
        var side = Math.Clamp((int)(body.Radius * PixelsPerUnit), MinSide, MaxSide);
        var unitsPerTexel = half * 2f / side;

        var (baseLow, baseHigh) = TierPalette(body.MassTier);
        var banded = body.MassTier is BodyMassTier.GasGiant or BodyMassTier.IceGiant;

        var pixels = new Color[side * side];
        // Cratered relief only matters for a solid, walkable surface - gas/ice giants get pure
        // banded cloud albedo with a smooth dome instead (no rock-style height field at all).
        var relief = banded ? 0f : body.Radius * 0.05f;
        var frequency = 2.6f / body.Radius;
        var craters = banded ? Array.Empty<(Vector2, float, float)>() : MakeCraters(body.Radius, seed);

        var heights = new float[side * side];
        if (!banded)
        {
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    var p = TexelToUnits(x, y, side, half);
                    var radial = p.Length();
                    if (radial > half)
                        continue;
                    var noise = Relief(p, frequency, relief, seed);
                    foreach (var crater in craters)
                        noise += CraterHeight(crater, p, seed);
                    var dome = MathF.Sqrt(MathF.Max(0f, 1f - radial * radial / (half * half))) * body.Radius * 0.6f;
                    heights[y * side + x] = noise + dome;
                }
            }
        }

        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var index = y * side + x;
                var p = TexelToUnits(x, y, side, half);
                var radial = p.Length();

                var coverage = MathHelper.Clamp((half - radial) / unitsPerTexel + 0.5f, 0f, 1f);
                if (coverage <= 0f)
                {
                    pixels[index] = Color.Transparent;
                    continue;
                }

                Vector3 normal;
                if (banded)
                {
                    // A smooth sphere's own implicit normal (no height field sampled at all) - the
                    // dome shading alone is what makes a band-painted disc read as a globe.
                    var nz = MathF.Sqrt(MathF.Max(0f, 1f - radial * radial / (half * half)));
                    normal = Vector3.Normalize(new Vector3(-p.X / half, -p.Y / half, nz + 0.35f));
                }
                else
                {
                    normal = NormalAt(heights, side, x, y, unitsPerTexel);
                }

                var diffuse = MathF.Max(0f, Vector3.Dot(normal, LightDirection));
                var specular = MathF.Pow(MathF.Max(0f, Vector3.Dot(normal, HalfwayDirection)), 18f) * (banded ? 0.06f : 0.14f);
                var ambient = 0.7f;

                var albedo = banded
                    ? BandedAlbedo(p, half, frequency, seed, baseLow, baseHigh)
                    : RockyAlbedo(p, frequency, seed, baseLow, baseHigh);

                var color = albedo * (SkyColor * ambient + SunColor * diffuse) + SunColor * specular;

                if (radial > 0.0001f)
                {
                    var facing = MathF.Max(0f, Vector2.Dot(p / radial, new Vector2(LightDirection.X, LightDirection.Y)));
                    color += SunColor * facing * 0.3f * (1f - radial / half);
                }

                pixels[index] = ToPremultiplied(color, coverage);
            }
        }

        return (pixels, side, half);
    }

    private static (Vector3 Low, Vector3 High) TierPalette(BodyMassTier tier) => tier switch
    {
        BodyMassTier.Rocky => (new Vector3(90, 62, 44) / 255f, new Vector3(176, 148, 118) / 255f),
        BodyMassTier.IceGiant => (new Vector3(120, 150, 190) / 255f, new Vector3(210, 228, 245) / 255f),
        BodyMassTier.GasGiant => (new Vector3(150, 108, 66) / 255f, new Vector3(224, 188, 140) / 255f),
        _ => (new Vector3(110, 110, 112) / 255f, new Vector3(190, 190, 192) / 255f), // Moon
    };

    private static float Relief(Vector2 p, float frequency, float amplitude, int seed)
    {
        var mp = p * frequency;
        var cp = Rotate(p, 0.9f) * frequency * 2.4f;
        var mass = (Fractal(mp.X, mp.Y, seed, 4, NoiseShape.Billow) - 0.42f) * amplitude * 1.3f;
        var crags = (Fractal(cp.X, cp.Y, seed + 211, 4, NoiseShape.Ridge) - 0.5f) * amplitude * 0.5f;
        return mass + crags;
    }

    private static Vector3 RockyAlbedo(Vector2 p, float frequency, int seed, Vector3 low, Vector3 high)
    {
        var broad = Fractal(p.X * frequency * 0.7f, p.Y * frequency * 0.7f, seed + 31, 3, NoiseShape.Value);
        var grit = Fractal(p.X * frequency * 5f, p.Y * frequency * 5f, seed + 57, 2, NoiseShape.Value);
        return Vector3.Lerp(low, high, MathHelper.Clamp(broad * 0.78f + grit * 0.22f, 0f, 1f));
    }

    // Cloud bands: latitude (Y, in the body's own unrotated bake frame) sets the base tone, a
    // turbulent field warps the band edges so they read as flowing weather, not a barcode.
    private static Vector3 BandedAlbedo(Vector2 p, float half, float frequency, int seed, Vector3 low, Vector3 high)
    {
        var latitude = MathHelper.Clamp(p.Y / half, -1f, 1f);
        var warp = (Fractal(p.X * frequency * 1.1f, p.Y * frequency * 0.35f, seed + 41, 3, NoiseShape.Value) - 0.5f) * 0.5f;
        var band = MathF.Sin((latitude + warp) * MathF.PI * 3.5f) * 0.5f + 0.5f;
        var turbulence = Fractal(p.X * frequency * 2.2f, p.Y * frequency * 2.2f, seed + 83, 3, NoiseShape.Billow);
        return Vector3.Lerp(low, high, MathHelper.Clamp(band * 0.7f + turbulence * 0.3f, 0f, 1f));
    }

    private static (Vector2 Center, float Radius, float Depth)[] MakeCraters(float radius, int seed)
    {
        var random = new Random(seed ^ 0x51ED27);
        var craters = new (Vector2, float, float)[3 + random.Next(4)];
        for (var i = 0; i < craters.Length; i++)
        {
            var angle = (float)(random.NextDouble() * MathF.PI * 2);
            var reach = MathF.Sqrt((float)random.NextDouble()) * radius * 0.9f;
            var craterRadius = radius * (0.05f + (float)random.NextDouble() * 0.09f);
            craters[i] = (new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach, craterRadius, craterRadius * 0.3f);
        }
        return craters;
    }

    private static float CraterHeight((Vector2 Center, float Radius, float Depth) crater, Vector2 p, int seed)
    {
        var offset = p - crater.Center;
        var distance = offset.Length();
        if (distance >= crater.Radius * 1.3f)
            return 0f;
        var t = distance / crater.Radius;
        if (t >= 1f)
            return 0f;
        var bowl = -crater.Depth * (1f - t * t);
        var lip = t > 0.74f ? crater.Depth * 0.5f * (1f - MathF.Abs(t - 0.87f) / 0.13f) : 0f;
        return bowl + MathF.Max(0f, lip);
    }

    private static Vector2 TexelToUnits(int x, int y, int side, float half) =>
        new(((x + 0.5f) / side * 2f - 1f) * half, ((y + 0.5f) / side * 2f - 1f) * half);

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

    private static Color ToPremultiplied(Vector3 color, float alpha)
    {
        var r = MathHelper.Clamp(color.X, 0f, 1f) * alpha;
        var g = MathHelper.Clamp(color.Y, 0f, 1f) * alpha;
        var b = MathHelper.Clamp(color.Z, 0f, 1f) * alpha;
        return new Color(r, g, b, alpha);
    }

    private enum NoiseShape { Value, Billow, Ridge }

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

    // Same FNV-1a AsteroidShape.StableHash uses server/shared-side - reimplemented here rather
    // than called across the assembly boundary (that one's internal to Shared), same reason
    // AsteroidTexture keeps its own local copy: string.GetHashCode is randomised per process,
    // which would hand every body a new face on each launch.
    private static int Seed(string id)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in id)
                hash = (hash ^ c) * 16777619;
            return (hash & 0x7FFFFFFF) ^ 0x50A4E7;
        }
    }
}
