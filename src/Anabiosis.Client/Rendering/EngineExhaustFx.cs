using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The thruster flame, built the way Cosmoteer builds its own (Data/ships/*/thruster_*/particles/*.rules): a stream of
// additive "fire" particles shot away from the nozzle that slow down, widen and run white -> warm -> red -> nothing over
// their life, a turbulent "plume" beam along the axis (cold red outside, hot orange in the middle, a pale core), a
// glow at the nozzle, a few fast sparks and a thin ember haze. The whole effect scales with a smoothed intensity so a
// burn spools up and tails off instead of snapping on and off. Every sprite is generated here in code - nothing is
// copied from the game's art. Particles live in the nozzle's own frame (along / across the thrust axis), so they
// follow the ship rigidly; only called inside an additive SpriteBatch pass.
internal sealed class EngineExhaustFx
{
    private const int FireVariants = 8;
    private const int FireTextureSize = 64;
    private const float IntensityRise = 5.5f;      // per second: how fast a burn spools up
    private const float IntensityFall = 3.2f;      // ...and how fast it tails off
    private const float MaxStep = 0.1f;            // a hitch must not fire a whole burst in one frame

    private enum Kind { Fire, Spark, Haze }

    private struct Particle
    {
        public Kind Kind;
        public float Age, Life;
        public float Along, Across;               // pixels from the nozzle centre: along the thrust, across it
        public float SpeedAlong, SpeedAcross;
        public float Drag;
        public float Rotation, Spin;
        public float SizeStart, SizeEnd;
        public float Brightness;
        public int Variant;
    }

    private sealed class Burner
    {
        public readonly List<Particle> Particles = new();
        public float Intensity;
        public float LastSeconds = float.NaN;
        public float FireDebt, SparkDebt, HazeDebt;
        public float Seed;
    }

    private readonly Texture2D[] _fire = new Texture2D[FireVariants];
    private readonly Texture2D _glow;
    private readonly Texture2D _beam;
    private readonly Texture2D _spark;
    private readonly Dictionary<string, Burner> _burners = new();
    private readonly Random _random = new(1234);

    public EngineExhaustFx(GraphicsDevice graphicsDevice)
    {
        for (var i = 0; i < FireVariants; i++)
            _fire[i] = BuildFire(graphicsDevice, i);
        _glow = BuildGlow(graphicsDevice);
        _beam = BuildBeam(graphicsDevice);
        _spark = BuildSpark(graphicsDevice);
    }

    // One call per engine per frame: steps that engine's particles to `totalSeconds` and draws them.
    // `nozzleWidth` is the nozzle's span in pixels, `direction` the unit vector pointing away from the hull.
    public void Draw(SpriteBatch spriteBatch, string id, bool thrusting, Vector2 nozzleCenter, Vector2 direction, float nozzleWidth, float totalSeconds)
    {
        if (direction.LengthSquared() < 0.001f)
            return;
        if (!_burners.TryGetValue(id, out var burner))
        {
            if (!thrusting)
                return;
            burner = new Burner { Seed = (id.GetHashCode() & 0xFFFF) * 0.001f };
            _burners[id] = burner;
        }

        var dt = float.IsNaN(burner.LastSeconds) ? 0f : Math.Clamp(totalSeconds - burner.LastSeconds, 0f, MaxStep);
        burner.LastSeconds = totalSeconds;
        Step(burner, thrusting, dt, nozzleWidth);

        if (burner.Intensity < 0.01f && burner.Particles.Count == 0)
            return;

        var perp = new Vector2(-direction.Y, direction.X);
        var rotation = MathF.Atan2(direction.Y, direction.X);

        DrawHaze(spriteBatch, burner, nozzleCenter, direction, perp);
        DrawGlow(spriteBatch, burner, nozzleCenter, direction, nozzleWidth, totalSeconds);
        DrawPlume(spriteBatch, burner, nozzleCenter, direction, rotation, nozzleWidth, totalSeconds);
        DrawFire(spriteBatch, burner, nozzleCenter, direction, perp);
        DrawSparks(spriteBatch, burner, nozzleCenter, direction, perp);
    }

    // ---- simulation -------------------------------------------------------------------------------------------

    private void Step(Burner burner, bool thrusting, float dt, float nozzleWidth)
    {
        var target = thrusting ? 1f : 0f;
        var rate = thrusting ? IntensityRise : IntensityFall;
        burner.Intensity += Math.Clamp(target - burner.Intensity, -rate * dt, rate * dt);

        var particles = burner.Particles;
        for (var i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Age += dt;
            if (p.Age >= p.Life)
            {
                particles[i] = particles[^1];
                particles.RemoveAt(particles.Count - 1);
                continue;
            }
            var slow = MathF.Exp(-p.Drag * dt);
            p.SpeedAlong *= slow;
            p.SpeedAcross *= slow;
            p.Along += p.SpeedAlong * dt;
            p.Across += p.SpeedAcross * dt;
            p.Rotation += p.Spin * dt;
            particles[i] = p;
        }

        var intensity = burner.Intensity;
        if (intensity < 0.02f)
            return;
        var half = nozzleWidth * 0.42f;

        burner.FireDebt += 260f * intensity * dt;
        while (burner.FireDebt >= 1f && particles.Count < 420)
        {
            burner.FireDebt -= 1f;
            var across = (Rand() * 2f - 1f) * half;
            particles.Add(new Particle
            {
                Kind = Kind.Fire,
                Life = 0.45f + Rand() * 0.4f,
                Along = Rand() * 4f,
                Across = across,
                SpeedAlong = (330f + Rand() * 190f) * (0.55f + 0.45f * intensity),
                SpeedAcross = across * 0.9f + (Rand() * 2f - 1f) * 14f,
                Drag = 1.8f,
                Rotation = Rand() * MathF.Tau,
                Spin = (Rand() * 2f - 1f) * 3f,
                SizeStart = 26f + Rand() * 10f,
                SizeEnd = 52f + Rand() * 26f,
                Brightness = 0.7f + Rand() * 0.3f,
                Variant = _random.Next(FireVariants),
            });
        }

        burner.SparkDebt += 34f * intensity * dt;
        while (burner.SparkDebt >= 1f && particles.Count < 440)
        {
            burner.SparkDebt -= 1f;
            particles.Add(new Particle
            {
                Kind = Kind.Spark,
                Life = 0.25f + Rand() * 0.3f,
                Along = Rand() * 6f,
                Across = (Rand() * 2f - 1f) * half * 0.6f,
                SpeedAlong = 420f + Rand() * 300f,
                SpeedAcross = (Rand() * 2f - 1f) * 70f,
                Drag = 1.1f,
                SizeStart = 9f + Rand() * 8f,
                SizeEnd = 4f,
                Brightness = 0.8f + Rand() * 0.2f,
            });
        }

        burner.HazeDebt += 16f * intensity * dt;
        while (burner.HazeDebt >= 1f && particles.Count < 460)
        {
            burner.HazeDebt -= 1f;
            particles.Add(new Particle
            {
                Kind = Kind.Haze,
                Life = 0.9f + Rand() * 0.5f,
                Along = 30f + Rand() * 30f,
                Across = (Rand() * 2f - 1f) * half * 0.8f,
                SpeedAlong = 55f + Rand() * 40f,
                SpeedAcross = (Rand() * 2f - 1f) * 22f,
                Drag = 1.2f,
                Rotation = Rand() * MathF.Tau,
                Spin = (Rand() * 2f - 1f) * 1.2f,
                SizeStart = 36f + Rand() * 14f,
                SizeEnd = 80f + Rand() * 30f,
                Brightness = 0.5f + Rand() * 0.3f,
                Variant = _random.Next(FireVariants),
            });
        }
    }

    private float Rand() => (float)_random.NextDouble();

    // ---- drawing ----------------------------------------------------------------------------------------------

    private void DrawGlow(SpriteBatch sb, Burner burner, Vector2 nozzle, Vector2 dir, float nozzleWidth, float t)
    {
        var intensity = burner.Intensity;
        if (intensity < 0.01f)
            return;
        var pulse = 0.92f + 0.08f * MathF.Sin(t * 19f + burner.Seed * 5f);
        var origin = new Vector2(_glow.Width / 2f);
        // A wide red wash, a tighter orange bloom just off the nozzle and a small white-hot kernel in its mouth.
        sb.Draw(_glow, nozzle + dir * 26f, null, new Color(255, 70, 10) * (0.22f * intensity * pulse), 0f, origin,
            (190f * (0.7f + 0.3f * intensity)) / _glow.Width, SpriteEffects.None, 0f);
        sb.Draw(_glow, nozzle + dir * 12f, null, new Color(255, 140, 40) * (0.42f * intensity * pulse), 0f, origin,
            (96f * (0.75f + 0.25f * intensity)) / _glow.Width, SpriteEffects.None, 0f);
        sb.Draw(_glow, nozzle + dir * 4f, null, new Color(255, 240, 190) * (0.55f * intensity * pulse), 0f, origin,
            (nozzleWidth * 0.95f) / _glow.Width, SpriteEffects.None, 0f);
    }

    // The beam: a few stacked layers of short stretched slices, each slice wobbling on its own so the flame breathes.
    private void DrawPlume(SpriteBatch sb, Burner burner, Vector2 nozzle, Vector2 dir, float rotation, float nozzleWidth, float t)
    {
        var intensity = burner.Intensity;
        if (intensity < 0.02f)
            return;
        const int slices = 14;
        var length = (80f + 105f * intensity) * (0.94f + 0.06f * MathF.Sin(t * 11f + burner.Seed));
        var sliceLength = length / slices;
        var origin = new Vector2(0f, _beam.Height / 2f);
        var baseWidth = nozzleWidth * (0.7f + 0.4f * intensity);

        void Layer(Color color, float widthFactor, float alpha, float lengthFactor)
        {
            var count = Math.Max(2, (int)(slices * lengthFactor));
            for (var i = 0; i < count; i++)
            {
                var x = i / (float)(count - 1);
                var wobble = 1f + 0.16f * MathF.Sin(t * 23f - i * 1.35f + burner.Seed * 7f)
                               + 0.09f * MathF.Sin(t * 41f - i * 2.3f + burner.Seed * 3f);
                var width = baseWidth * widthFactor * (1f - 0.55f * x) * wobble;
                var fade = MathF.Pow(1f - x, 1.25f);
                var position = nozzle + dir * (i * sliceLength * lengthFactor);
                sb.Draw(_beam, position, null, color * (alpha * intensity * fade), rotation, origin,
                    new Vector2(sliceLength * lengthFactor * 1.04f / _beam.Width, width / _beam.Height), SpriteEffects.None, 0f);
            }
        }

        Layer(new Color(247, 29, 0), 1.5f, 0.55f, 1f);        // cold edge
        Layer(new Color(255, 145, 28), 1.0f, 0.85f, 0.85f);   // hot body
        Layer(new Color(247, 255, 160), 0.45f, 1.0f, 0.55f);    // pale core
    }

    private void DrawFire(SpriteBatch sb, Burner burner, Vector2 nozzle, Vector2 dir, Vector2 perp)
    {
        foreach (var p in burner.Particles)
        {
            if (p.Kind != Kind.Fire)
                continue;
            var u = p.Age / p.Life;
            var color = FireRamp(u) * (p.Brightness * 0.62f);
            var texture = _fire[p.Variant];
            var size = MathHelper.Lerp(p.SizeStart, p.SizeEnd, MathF.Sqrt(u));
            var position = nozzle + dir * p.Along + perp * p.Across;
            sb.Draw(texture, position, null, color, p.Rotation, new Vector2(texture.Width / 2f), size / texture.Width, SpriteEffects.None, 0f);
        }
    }

    private void DrawHaze(SpriteBatch sb, Burner burner, Vector2 nozzle, Vector2 dir, Vector2 perp)
    {
        foreach (var p in burner.Particles)
        {
            if (p.Kind != Kind.Haze)
                continue;
            var u = p.Age / p.Life;
            var alpha = MathF.Sin(MathF.PI * MathF.Min(u * 1.15f, 1f)) * 0.16f * p.Brightness;   // up, then away
            var color = new Color(255, 96, 40) * alpha;
            var texture = _fire[p.Variant];
            var size = MathHelper.Lerp(p.SizeStart, p.SizeEnd, u);
            var position = nozzle + dir * p.Along + perp * p.Across;
            sb.Draw(texture, position, null, color, p.Rotation, new Vector2(texture.Width / 2f), size / texture.Width, SpriteEffects.None, 0f);
        }
    }

    private void DrawSparks(SpriteBatch sb, Burner burner, Vector2 nozzle, Vector2 dir, Vector2 perp)
    {
        var origin = new Vector2(0f, _spark.Height / 2f);
        foreach (var p in burner.Particles)
        {
            if (p.Kind != Kind.Spark)
                continue;
            var u = p.Age / p.Life;
            var velocity = dir * p.SpeedAlong + perp * p.SpeedAcross;
            var angle = MathF.Atan2(velocity.Y, velocity.X);
            var streak = MathHelper.Clamp(velocity.Length() * 0.045f, 4f, 22f) * (1f - 0.5f * u);
            var color = Color.Lerp(new Color(255, 240, 200), new Color(255, 110, 30), u) * (p.Brightness * (1f - u));
            var position = nozzle + dir * p.Along + perp * p.Across;
            sb.Draw(_spark, position, null, color, angle + MathF.PI, origin,
                new Vector2(streak / _spark.Width, 2.4f / _spark.Height), SpriteEffects.None, 0f);
        }
    }

    // white-hot at birth -> warm -> orange -> red -> gone, as the fire emitter's own colour ramp runs.
    private static Color FireRamp(float u)
    {
        var (r, g, b, a) = u switch
        {
            < 0.12f => Lerp4((1f, 1f, 0.92f, 0.55f), (1f, 0.97f, 0.78f, 1f), u / 0.12f),
            < 0.35f => Lerp4((1f, 0.97f, 0.78f, 1f), (1f, 0.66f, 0.26f, 0.95f), (u - 0.12f) / 0.23f),
            < 0.7f => Lerp4((1f, 0.66f, 0.26f, 0.95f), (0.95f, 0.26f, 0.06f, 0.6f), (u - 0.35f) / 0.35f),
            _ => Lerp4((0.95f, 0.26f, 0.06f, 0.6f), (0.5f, 0.06f, 0.02f, 0f), (u - 0.7f) / 0.3f),
        };
        return new Color(r * a, g * a, b * a, 1f);
    }

    private static (float, float, float, float) Lerp4((float, float, float, float) a, (float, float, float, float) b, float t) =>
        (a.Item1 + (b.Item1 - a.Item1) * t, a.Item2 + (b.Item2 - a.Item2) * t, a.Item3 + (b.Item3 - a.Item3) * t, a.Item4 + (b.Item4 - a.Item4) * t);

    // ---- sprite baking ----------------------------------------------------------------------------------------

    // A soft round puff with a ragged, noisy edge - eight different ones so a stream of them doesn't repeat.
    private static Texture2D BuildFire(GraphicsDevice device, int variant)
    {
        var size = FireTextureSize;
        var data = new Color[size * size];
        var offset = variant * 17.31f;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var nx = (x + 0.5f) / size * 2f - 1f;
                var ny = (y + 0.5f) / size * 2f - 1f;
                var r = MathF.Sqrt(nx * nx + ny * ny);
                var noise = Fbm(nx * 2.2f + offset, ny * 2.2f - offset * 0.7f);
                var edge = MathHelper.Clamp(1f - r * (0.85f + 0.55f * noise), 0f, 1f);
                var v = edge * edge * (3f - 2f * edge);                 // smoothstep falloff
                v *= 0.6f + 0.55f * noise;
                v = MathHelper.Clamp(v, 0f, 1f);
                data[y * size + x] = new Color(v, v, v, v);
            }
        return Make(device, size, size, data);
    }

    private static Texture2D BuildGlow(GraphicsDevice device)
    {
        const int size = 64;
        var data = new Color[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var nx = (x + 0.5f) / size * 2f - 1f;
                var ny = (y + 0.5f) / size * 2f - 1f;
                var d = MathF.Min(1f, MathF.Sqrt(nx * nx + ny * ny));
                var v = MathF.Pow(1f - d, 2.2f);
                data[y * size + x] = new Color(v, v, v, v);
            }
        return Make(device, size, size, data);
    }

    // One slice of the beam: brightest on the axis, melting away to the sides (stretched along it when drawn).
    private static Texture2D BuildBeam(GraphicsDevice device)
    {
        const int width = 8, height = 64;
        var data = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            var ny = (y + 0.5f) / height * 2f - 1f;
            var v = MathF.Exp(-ny * ny * 4.2f);
            for (var x = 0; x < width; x++)
                data[y * width + x] = new Color(v, v, v, v);
        }
        return Make(device, width, height, data);
    }

    // A short streak: bright head on the right end, fading tail towards the left (origin is at the left end,
    // so the sprite is drawn pointing along the velocity with its head leading).
    private static Texture2D BuildSpark(GraphicsDevice device)
    {
        const int width = 32, height = 8;
        var data = new Color[width * height];
        for (var y = 0; y < height; y++)
        {
            var ny = (y + 0.5f) / height * 2f - 1f;
            var across = MathF.Exp(-ny * ny * 3.5f);
            for (var x = 0; x < width; x++)
            {
                var along = MathF.Pow(1f - x / (float)(width - 1), 1.6f);   // head at x=0
                var v = across * along;
                data[y * width + x] = new Color(v, v, v, v);
            }
        }
        return Make(device, width, height, data);
    }

    private static Texture2D Make(GraphicsDevice device, int width, int height, Color[] data)
    {
        var texture = new Texture2D(device, width, height);
        texture.SetData(data);
        return texture;
    }

    // Small value-noise fBm, enough for a ragged puff edge.
    private static float Fbm(float x, float y)
    {
        var sum = 0f;
        var amplitude = 0.5f;
        for (var i = 0; i < 4; i++)
        {
            sum += amplitude * ValueNoise(x, y);
            x *= 2.03f; y *= 2.03f; amplitude *= 0.5f;
        }
        return sum / 0.9375f;
    }

    private static float ValueNoise(float x, float y)
    {
        var x0 = MathF.Floor(x); var y0 = MathF.Floor(y);
        var fx = x - x0; var fy = y - y0;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        var a = Hash((int)x0, (int)y0); var b = Hash((int)x0 + 1, (int)y0);
        var c = Hash((int)x0, (int)y0 + 1); var d = Hash((int)x0 + 1, (int)y0 + 1);
        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    private static float Hash(int x, int y)
    {
        var h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }
}
