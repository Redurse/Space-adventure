using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

/// <summary>The reactor's baked art: three wear stages of the machine plus one emissive mask.</summary>
public sealed record ReactorArt(Texture2D[] Stages, Texture2D Glow, Texture2D WreckGlow)
{
    public Texture2D GlowFor(int stage) => stage >= 2 ? WreckGlow : Glow;
}

// Direct user request ("переделаем текстуру реактора под такие текстуры", with Barotrauma reactor
// sprites as the style reference - original art, not a trace). The old face was flat posterized
// pixel art; this one is painted per pixel like ItemSkin/DeviceSkin: every part has a lit 3D
// normal (cylinders, domes, bevelled plates), then the whole machine gets value-noise grime, drips
// and a dark outline, so it reads as worn steel rather than a vector drawing.
//
// Three stages share one base painting (ReactorTexture.Body.cs) and differ only by the damage
// passes laid over it (ReactorTexture.Damage.cs): 0 = in service, 1 = battered (rust, dents, a
// cracked window, torn fins), 2 = wrecked (the core blown open). The light is NOT in the pixels:
// a separate mask (ReactorTexture.Glow.cs) is drawn additively on top in teal while the reactor
// runs and shifts to red as the plasma overheats, so one bake serves every state.
//
// Painted at 384px for a 4-unit (192px at zoom 1) block: at zoom 1 linear sampling averages each
// 2x2 block into clean antialiasing, and zooming in still has real detail to show.
public static partial class MachineArt
{
    private const int ReactorSize = 384;
    private const float Units = 64f;            // the reactor's design grid

    // The canvas being painted right now (Begin): its size in pixels and pixels per design unit. Every
    // painter works in its own design units; the reactor's are 64 across 384px.
    private static int _w, _h;
    private static float _px;
    private static float CanvasUnitsW => _w / _px;
    private static float CanvasUnitsH => _h / _px;

    private static void Begin(int width, int height, float pixelsPerUnit)
    {
        _w = width;
        _h = height;
        _px = pixelsPerUnit;
        _rgb = new Vector3[_w * _h];
        _mat = new Mat[_w * _h];
    }
    public const float GlowMargin = 8f;         // the glow mask spills this many units past each edge
    private const int GlowSize = 240;           // (64 + 2*8) units at 3 px

    // Material per pixel, so the damage passes know glass from steel from a painted label.
    private enum Mat : byte { None, Metal, Dark, Glass, Label, Lamp, Rubber }

    private static Vector3[] _rgb = Array.Empty<Vector3>();
    private static Mat[] _mat = Array.Empty<Mat>();

    public static ReactorArt CreateArt(GraphicsDevice device)
    {
        Begin(ReactorSize, ReactorSize, ReactorSize / Units);
        PaintBody();
        Weather(0.0f);

        var stages = new Texture2D[3];
        var baseRgb = (Vector3[])_rgb.Clone();
        var baseMat = (Mat[])_mat.Clone();
        stages[0] = Bake(device);

        Batter();
        Weather(0.55f);
        var batteredRgb = (Vector3[])_rgb.Clone();
        var batteredMat = (Mat[])_mat.Clone();
        stages[1] = Bake(device);

        _rgb = batteredRgb;
        _mat = batteredMat;
        Wreck();
        stages[2] = Bake(device);

        _rgb = baseRgb;
        _mat = baseMat;
        var glow = BakeGlow(device, wrecked: false);
        var wreckGlow = BakeGlow(device, wrecked: true);
        _rgb = Array.Empty<Vector3>();
        _mat = Array.Empty<Mat>();
        return new ReactorArt(stages, glow, wreckGlow);
    }

    // Wear stage for a reactor room's integrity (0..1, World.RoomIntegrity).
    public static int StageFor(float integrity) => integrity > 0.66f ? 0 : integrity > 0.36f ? 1 : 2;

    private static Texture2D Bake(GraphicsDevice device)
    {
        var data = new Color[_w * _h];
        for (var y = 0; y < _h; y++)
        for (var x = 0; x < _w; x++)
        {
            var i = y * _w + x;
            if (_mat[i] == Mat.None) continue;
            // A one-pixel ink line wherever the silhouette meets empty space.
            var edge = x == 0 || y == 0 || x == _w - 1 || y == _h - 1
                || _mat[i - 1] == Mat.None || _mat[i + 1] == Mat.None || _mat[i - _w] == Mat.None || _mat[i + _w] == Mat.None;
            var c = edge ? new Vector3(0.035f, 0.035f, 0.04f) : Grade(_rgb[i]);
            data[i] = new Color(c.X, c.Y, c.Z, 1f);
        }
        var texture = new Texture2D(device, _w, _h);
        texture.SetData(data);
        return texture;
    }

    // Same restraint as ItemSkin.Finish: pull a little saturation, keep the darks dark.
    private static Vector3 Grade(Vector3 c)
    {
        var grey = (c.X + c.Y + c.Z) / 3f;
        c = Vector3.Lerp(c, new Vector3(grey), 0.18f) * 0.94f;
        return Vector3.Clamp(c, Vector3.Zero, Vector3.One);
    }

    // ---- painting --------------------------------------------------------------------------

    private delegate bool Shader(float x, float y, out Vector3 color);

    /// Runs <paramref name="shader"/> over the unit-space box and writes every pixel it claims.
    private static void Paint(float x0, float y0, float x1, float y1, Mat mat, Shader shader)
    {
        var px0 = Math.Max(0, (int)MathF.Floor(x0 * _px));
        var py0 = Math.Max(0, (int)MathF.Floor(y0 * _px));
        var px1 = Math.Min(_w, (int)MathF.Ceiling(x1 * _px));
        var py1 = Math.Min(_h, (int)MathF.Ceiling(y1 * _px));
        for (var py = py0; py < py1; py++)
        for (var px = px0; px < px1; px++)
        {
            if (!shader((px + 0.5f) / _px, (py + 0.5f) / _px, out var c)) continue;
            var i = py * _w + px;
            _rgb[i] = c;
            _mat[i] = mat;
        }
    }

    private delegate float Modifier(float x, float y, Mat mat);

    /// Multiplies already-painted pixels by a factor (1 = unchanged) - grime, soot, dents.
    private static void Multiply(float x0, float y0, float x1, float y1, Modifier factor)
    {
        var px0 = Math.Max(0, (int)MathF.Floor(x0 * _px));
        var py0 = Math.Max(0, (int)MathF.Floor(y0 * _px));
        var px1 = Math.Min(_w, (int)MathF.Ceiling(x1 * _px));
        var py1 = Math.Min(_h, (int)MathF.Ceiling(y1 * _px));
        for (var py = py0; py < py1; py++)
        for (var px = px0; px < px1; px++)
        {
            var i = py * _w + px;
            if (_mat[i] == Mat.None) continue;
            _rgb[i] *= factor((px + 0.5f) / _px, (py + 0.5f) / _px, _mat[i]);
        }
    }

    private delegate float Blend(float x, float y, Mat mat);

    /// Lerps already-painted pixels toward <paramref name="tone"/> by the returned amount.
    private static void Stain(float x0, float y0, float x1, float y1, Vector3 tone, Blend amount)
    {
        var px0 = Math.Max(0, (int)MathF.Floor(x0 * _px));
        var py0 = Math.Max(0, (int)MathF.Floor(y0 * _px));
        var px1 = Math.Min(_w, (int)MathF.Ceiling(x1 * _px));
        var py1 = Math.Min(_h, (int)MathF.Ceiling(y1 * _px));
        for (var py = py0; py < py1; py++)
        for (var px = px0; px < px1; px++)
        {
            var i = py * _w + px;
            if (_mat[i] == Mat.None) continue;
            var t = amount((px + 0.5f) / _px, (py + 0.5f) / _px, _mat[i]);
            if (t > 0f) _rgb[i] = Vector3.Lerp(_rgb[i], tone, MathF.Min(1f, t));
        }
    }

    // ---- shading ---------------------------------------------------------------------------

    private static readonly Vector3 Light = Vector3.Normalize(new Vector3(-0.42f, -0.62f, 0.66f));

    /// Brightness of a surface with the given screen-space normal tilt (0,0 = facing the viewer).
    private static float Lit(float nx, float ny, float spec = 0.25f)
    {
        var nz = MathF.Sqrt(MathF.Max(0f, 1f - nx * nx - ny * ny));
        var n = new Vector3(nx, ny, nz);
        var d = Vector3.Dot(n, Light);
        var r = 2f * d * n - Light;
        var s = MathF.Pow(MathF.Max(0f, r.Z), 14f) * spec;
        return 0.40f + 0.72f * MathF.Max(0f, d) + s;
    }

    private const float FlatLit = 0.40f + 0.72f * 0.66f / 1.0f; // ~0.87, Lit(0,0) without spec

    /// A flat-facing surface of the given tone with brushed streaks and mottling.
    private static Vector3 Metal(Vector3 tone, float x, float y, float nx = 0f, float ny = 0f, float spec = 0.2f)
    {
        var mottle = (Fbm(x * 0.35f, y * 0.35f, 11) - 0.5f) * 0.16f;
        var brush = (Noise(x * 0.25f, y * 5f, 12) - 0.5f) * 0.07f;
        return tone * (Lit(nx, ny, spec) / FlatLit) * (1f + mottle + brush);
    }

    /// Normal of a bevelled box at (x,y): edges tilt out (raised) or in (recessed).
    private static (float Nx, float Ny) Bevel(float x, float y, float x0, float y0, float x1, float y1, float width, bool raised = true)
    {
        var dl = x - x0; var dr = x1 - x; var dt = y - y0; var db = y1 - y;
        var m = MathF.Min(MathF.Min(dl, dr), MathF.Min(dt, db));
        if (m >= width) return (0f, 0f);
        var tilt = 0.62f * (raised ? 1f : -1f);
        if (m == dl) return (-tilt, 0f);
        if (m == dr) return (tilt, 0f);
        if (m == dt) return (0f, -tilt);
        return (0f, tilt);
    }

    private static bool InRoundRect(float x, float y, float x0, float y0, float x1, float y1, float r)
    {
        if (x < x0 || x >= x1 || y < y0 || y >= y1) return false;
        var cx = Math.Clamp(x, x0 + r, x1 - r);
        var cy = Math.Clamp(y, y0 + r, y1 - r);
        var dx = x - cx; var dy = y - cy;
        return dx * dx + dy * dy <= r * r;
    }

    /// A domed bolt head at (cx,cy).
    private static void Bolt(float cx, float cy, float r, Vector3 tone)
    {
        Paint(cx - r - 0.4f, cy - r - 0.4f, cx + r + 0.4f, cy + r + 0.4f, Mat.Metal, (float x, float y, out Vector3 c) =>
        {
            var dx = x - cx; var dy = y - cy;
            var d = MathF.Sqrt(dx * dx + dy * dy);
            c = default;
            if (d > r + 0.35f) return false;
            if (d > r) { c = new Vector3(0.06f); return true; }            // seated shadow ring
            c = tone * Lit(dx / r * 0.85f, dy / r * 0.85f, 0.45f) / FlatLit;
            return true;
        });
    }

    /// A tube along a polyline, shaded as a cylinder lit from the upper left.
    private static void Pipe(Vector2[] path, float r, Vector3 tone, Mat mat = Mat.Metal, float spec = 0.35f)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var p in path)
        {
            minX = MathF.Min(minX, p.X); minY = MathF.Min(minY, p.Y);
            maxX = MathF.Max(maxX, p.X); maxY = MathF.Max(maxY, p.Y);
        }
        Paint(minX - r - 1, minY - r - 1, maxX + r + 1, maxY + r + 1, mat, (float x, float y, out Vector3 c) =>
        {
            var best = float.MaxValue; var off = Vector2.Zero;
            for (var k = 0; k + 1 < path.Length; k++)
            {
                var a = path[k]; var ab = path[k + 1] - a;
                var t = Math.Clamp(Vector2.Dot(new Vector2(x, y) - a, ab) / MathF.Max(1e-5f, ab.LengthSquared()), 0f, 1f);
                var o = new Vector2(x, y) - (a + ab * t);
                if (o.LengthSquared() < best) { best = o.LengthSquared(); off = o; }
            }
            c = default;
            if (best > r * r) return false;
            var n = off / r;
            c = tone * Lit(n.X * 0.92f, n.Y * 0.92f, spec) / FlatLit;
            if (best > r * r * 0.8f) c *= 0.55f;                               // contour shadow
            return true;
        });
    }

    private static Vector2[] Arc(float cx, float cy, float radius, float fromDeg, float toDeg, int steps = 10)
    {
        var points = new Vector2[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var a = MathHelper.ToRadians(MathHelper.Lerp(fromDeg, toDeg, i / (float)steps));
            points[i] = new Vector2(cx + MathF.Cos(a) * radius, cy + MathF.Sin(a) * radius);
        }
        return points;
    }

    /// A thin straight line (cracks, wires, scratches).
    private static void Line(float ax, float ay, float bx, float by, float width, Vector3 tone, Mat mat)
    {
        var a = new Vector2(ax, ay); var ab = new Vector2(bx - ax, by - ay);
        Paint(MathF.Min(ax, bx) - width, MathF.Min(ay, by) - width, MathF.Max(ax, bx) + width, MathF.Max(ay, by) + width, mat,
            (float x, float y, out Vector3 c) =>
            {
                var t = Math.Clamp(Vector2.Dot(new Vector2(x, y) - a, ab) / MathF.Max(1e-5f, ab.LengthSquared()), 0f, 1f);
                c = tone;
                return (new Vector2(x, y) - (a + ab * t)).Length() <= width / 2f;
            });
    }

    // ---- noise -----------------------------------------------------------------------------

    private static float Hash(int x, int y, int seed)
    {
        var n = x * 374761393 + y * 668265263 + seed * 362437;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    private static float Noise(float x, float y, int seed)
    {
        var ix = (int)MathF.Floor(x); var iy = (int)MathF.Floor(y);
        var fx = x - ix; var fy = y - iy;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        var a = MathHelper.Lerp(Hash(ix, iy, seed), Hash(ix + 1, iy, seed), fx);
        var b = MathHelper.Lerp(Hash(ix, iy + 1, seed), Hash(ix + 1, iy + 1, seed), fx);
        return MathHelper.Lerp(a, b, fy);
    }

    private static float Fbm(float x, float y, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (var o = 0; o < 4; o++)
        {
            sum += Noise(x, y, seed + o * 31) * amp;
            norm += amp;
            x *= 2.03f; y *= 2.03f; amp *= 0.5f;
        }
        return sum / norm;
    }

    private static float Smooth(float a, float b, float v) => MathHelper.SmoothStep(0f, 1f, Math.Clamp((v - a) / (b - a), 0f, 1f));

    /// A deterministic random in [0,1) for placing scattered details.
    private static float Rand(int i, int seed) => Hash(i, i * 7 + 3, seed);
}
