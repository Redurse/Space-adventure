using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

/// <summary>Painted inventory art for every carryable item, baked per item and pixel size.</summary>
///
/// Direct user request ("улучши все текущие текстуры предметов", after the devices got the same
/// treatment - DeviceSkin). The old icons (ItemIcons.*) were assembled live from one-pixel bars and
/// rings and read as diagrams. These are painted per pixel instead, the way the device faces are:
///   * every shape carries a material - brushed metal with a gradient and a specular streak, rubber,
///     painted plastic, glass, rock - shaded by the shape's own geometry (a cylinder is lit across
///     its width, a sphere from the upper left, a plate from the top);
///   * once the art is down, one pass bevels it (a lit upper-left rim, a shaded lower-right one)
///     and another gives it a dark outline, which is what makes an icon pop off any background.
/// Designed on a 32-unit grid and baked at the exact pixel size it's shown at, so it stays crisp in
/// a 32px trade card and a 40px hotbar slot alike. Tools and guns are painted lying along +X (handle
/// left, business end right) so the caller's rotation - the slot tilt, or a held item's real aim -
/// turns them as one rigid picture; everything else is painted upright and never rotated.
public static partial class ItemSkin
{
    private const float Design = 32f;
    private static readonly Dictionary<(ItemType, int, int), Texture2D> Cache = new();

    // The canvas being painted right now.
    private static Color[] _buf = Array.Empty<Color>();
    private static int _size;
    private static float _u;

    // A tool's slot picture is baked already tilted (see TryDraw) - the painters keep drawing in
    // their own upright frame and every pixel is mapped back into it.
    private static bool _tilted;
    private static float _cos = 1f, _sin;
    private const float TiltShrink = 0.94f; // a tilted tool spans the diagonal - keep it inside the slot

    public static bool Has(ItemType type) => Painter(type) is not null;

    // Tools and guns turn with the hand; everything else stands upright.
    public static bool Rotates(ItemType type) => type is ItemType.Wrench or ItemType.Screwdriver or ItemType.GoshaScrewdriver
        or ItemType.WeldingTool or ItemType.Cutter or ItemType.Knife or ItemType.Axe
        or ItemType.Rifle or ItemType.LaserRifle || GunDefinitions.Of(type) is not null;

    // The design-space to screen scale (Game1.UpdateRenderScale) - 1.6 on a 1920x1080 screen. The whole
    // interface is drawn at design size and scaled up by it, so an icon baked at its design size was being
    // stretched 1.6x with linear filtering - the blur the user saw in the rack (direct user report,
    // "почему в самой игре иконки размыты"). Icons now bake at the real on-screen size and are drawn at
    // 1/scale, snapped so every texel lands on exactly one screen pixel.
    private static float _pixelScale = 1f;
    private static Vector2 _pixelOffset;

    public static void SetPixelGrid(float scale, Vector2 offset)
    {
        _pixelScale = scale > 0f ? scale : 1f;
        _pixelOffset = offset;
    }

    // Design-space position of the screen pixel nearest to `design`.
    private static Vector2 SnapToPixel(Vector2 design) => new(
        (MathF.Round(design.X * _pixelScale + _pixelOffset.X) - _pixelOffset.X) / _pixelScale,
        (MathF.Round(design.Y * _pixelScale + _pixelOffset.Y) - _pixelOffset.Y) / _pixelScale);

    public static bool TryDraw(SpriteBatch spriteBatch, ItemType type, Rectangle rect, float? rotation, float defaultAngle)
    {
        if (Painter(type) is not { } paint)
            return false;
        var size = Math.Max(8, Math.Min(rect.Width, rect.Height));
        var real = Math.Max(8, (int)MathF.Round(size * _pixelScale)); // the icon's size in actual screen pixels
        var toDesign = size / (float)real;
        if (Rotates(type) && rotation is { } heldAngle)
        {
            // Held in hand: the real aim turns the picture (the scene is drawn point-sampled).
            var upright = Get(spriteBatch.GraphicsDevice, type, real, paint, 0f);
            spriteBatch.Draw(upright, new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f), null, Color.White,
                heldAngle, new Vector2(real / 2f, real / 2f), toDesign, SpriteEffects.None, 0f);
            return true;
        }
        // In a slot: a tool's tilt is baked into the pixels rather than applied by the sprite
        // batch, so the picture lands one texel per screen pixel instead of being resampled
        // (resampling a rotated texture is what made the first version look smeared).
        var texture = Get(spriteBatch.GraphicsDevice, type, real, paint, Rotates(type) ? defaultAngle : 0f);
        var at = SnapToPixel(new Vector2(rect.X + (rect.Width - size) / 2f, rect.Y + (rect.Height - size) / 2f));
        spriteBatch.Draw(texture, at, null, Color.White, 0f, Vector2.Zero, toDesign, SpriteEffects.None, 0f);
        return true;
    }

    // For the review sheet / tests: the baked pixels themselves.
    public static Texture2D GetTexture(GraphicsDevice graphics, ItemType type, int size) =>
        Painter(type) is { } paint ? Get(graphics, type, Math.Max(8, size), paint, 0f) : throw new ArgumentException($"no art for {type}");

    private static Texture2D Get(GraphicsDevice graphics, ItemType type, int size, Action paint, float tilt)
    {
        var key = (type, size, (int)MathF.Round(tilt * 1000f));
        if (Cache.TryGetValue(key, out var cached))
            return cached;
        // Painted-style art (ItemSkin.Materials.cs - user's reference: realistic, gritty) is drawn
        // at four times the size and averaged down, so its facets, grime and highlights come out as
        // smooth painted detail instead of single hard pixels.
        _ss = Supersampled(type) ? 4 : 1;
        _size = size * _ss;
        _u = _size / Design;
        _buf = new Color[_size * _size];
        _tilted = tilt != 0f;
        _grade = !OreArt.Has(type); // raw ore keeps its own vivid colours
        _cos = MathF.Cos(tilt);
        _sin = MathF.Sin(tilt);
        paint();
        _tilted = false;
        Finish();
        var pixels = _ss == 1 ? _buf : Downsample(size);
        _ss = 1;
        var texture = new Texture2D(graphics, size, size);
        texture.SetData(pixels);
        Cache[key] = texture;
        return texture;
    }

    // Supersampling factor of the bake in progress (1 = the hard-pixel style).
    private static int _ss = 1;
    // Whether Finish pulls the colours toward Barotrauma's muted palette (everything but the raw ore).
    private static bool _grade = true;

    // Box-filter the supersampled canvas down to the real size (premultiplied, so a plain average).
    private static Color[] Downsample(int size)
    {
        var result = new Color[size * size];
        var n = _ss * _ss;
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            int r = 0, g = 0, b = 0, a = 0;
            for (var sy = 0; sy < _ss; sy++)
            for (var sx = 0; sx < _ss; sx++)
            {
                var c = _buf[(y * _ss + sy) * _size + x * _ss + sx];
                r += c.R; g += c.G; b += c.B; a += c.A;
            }
            result[y * size + x] = new Color(r / n, g / n, b / n, a / n);
        }
        return result;
    }

    // Canvas design point -> the painter's own upright frame (inverse of the baked tilt).
    private static (float X, float Y) Untilt(float x, float y)
    {
        const float c = Design / 2f;
        var dx = x - c;
        var dy = y - c;
        return (c + (_cos * dx + _sin * dy) / TiltShrink, c + (-_sin * dx + _cos * dy) / TiltShrink);
    }

    // The painter's upright frame -> canvas design point.
    private static (float X, float Y) Tilt(float x, float y)
    {
        const float c = Design / 2f;
        var dx = x - c;
        var dy = y - c;
        return (c + TiltShrink * (_cos * dx - _sin * dy), c + TiltShrink * (_sin * dx + _cos * dy));
    }

    // ------------------------------------------------------------------ materials and colour

    // Base colour plus how it takes light: Gloss is the specular streak, Grain the brushed noise.
    private readonly record struct Mat(Color Base, float Gloss = 0.3f, float Grain = 0.05f);

    private static Color Scale(Color c, float f) =>
        new(Math.Clamp((int)(c.R * f), 0, 255), Math.Clamp((int)(c.G * f), 0, 255), Math.Clamp((int)(c.B * f), 0, 255));

    private static Color Mix(Color a, Color b, float t) =>
        new((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    private static float Hash(int a, int b)
    {
        var n = unchecked(a * 374761393 + b * 668265263 + _size * 9176);
        n = unchecked((n ^ (n >> 13)) * 1274126177);
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // The final colour of one material pixel: `light` is the shape's own shading (0 dark .. 1 lit),
    // `spec` how much of the specular streak lands here. Barotrauma-toned (user feedback: the first
    // pass was "too bright, ugly"): the lit side only just reaches the base colour, the shade side
    // falls well below it, the specular is a dull sheen rather than a white flash, and every surface
    // carries a little grime.
    private static Color Lit(Mat m, int px, int py, float light, float spec)
    {
        var grain = (Hash(px, py / 2) - 0.5f) * 2f * m.Grain;
        var grime = Hash(px * 3 + 7, py * 5 + 11) < 0.12f ? -0.08f : 0f;
        var c = Scale(m.Base, 0.48f + 0.52f * light + grain + grime);
        return spec > 0f ? Mix(c, new Color(236, 230, 214), Math.Clamp(spec * m.Gloss * 0.45f, 0f, 0.3f)) : c;
    }

    // ------------------------------------------------------------------ the canvas

    private static void Put(int px, int py, Color c, float a = 1f)
    {
        if (a <= 0f || px < 0 || py < 0 || px >= _size || py >= _size)
            return;
        a = MathF.Min(a, 1f);
        var d = _buf[py * _size + px];
        _buf[py * _size + px] = new Color(
            (int)(c.R * a + d.R * (1f - a)), (int)(c.G * a + d.G * (1f - a)), (int)(c.B * a + d.B * (1f - a)),
            (int)(255 * a + d.A * (1f - a)));
    }

    private static void Erase(int px, int py)
    {
        if (px >= 0 && py >= 0 && px < _size && py < _size)
            _buf[py * _size + px] = Color.Transparent;
    }

    // Every shape walks the pixels of its own design-space bounding box and hands each pixel's
    // centre, in design units, to `at`.
    private static void Each(float x0, float y0, float x1, float y1, Action<int, int, float, float> at)
    {
        if (_tilted)
        {
            // A tilted bake: the bounding box no longer lines up with the canvas, so walk all of it.
            for (var py = 0; py < _size; py++)
            for (var px = 0; px < _size; px++)
            {
                var (ux, uy) = Untilt((px + 0.5f) / _u, (py + 0.5f) / _u);
                if (ux >= x0 - 1f && ux <= x1 + 1f && uy >= y0 - 1f && uy <= y1 + 1f)
                    at(px, py, ux, uy);
            }
            return;
        }
        var minX = Math.Max(0, (int)MathF.Floor(x0 * _u) - 1);
        var minY = Math.Max(0, (int)MathF.Floor(y0 * _u) - 1);
        var maxX = Math.Min(_size - 1, (int)MathF.Ceiling(x1 * _u) + 1);
        var maxY = Math.Min(_size - 1, (int)MathF.Ceiling(y1 * _u) + 1);
        for (var py = minY; py <= maxY; py++)
        for (var px = minX; px <= maxX; px++)
            at(px, py, (px + 0.5f) / _u, (py + 0.5f) / _u);
    }

    // Half a pixel, in design units.
    private static float HalfPx => 0.5f / _u;

    // Hard-edged on purpose (user feedback: the first pass's antialiased rims read as blurry) - a
    // pixel is in or out, and the outline pass then gives the silhouette its crisp dark edge, the
    // way Barotrauma's item sprites read.
    private static float Cover(float inside) => inside >= 0f ? 1f : 0f;

    // A plate or box, optionally with rounded corners, lit from the top.
    private static void Box(float x, float y, float w, float h, Mat m, float r = 0f)
    {
        Each(x, y, x + w, y + h, (px, py, dx, dy) =>
        {
            var qx = MathF.Max(MathF.Abs(dx - (x + w / 2)) - (w / 2 - r), 0f);
            var qy = MathF.Max(MathF.Abs(dy - (y + h / 2)) - (h / 2 - r), 0f);
            var outside = MathF.Sqrt(qx * qx + qy * qy) - r;
            var a = Cover(-outside);
            if (a <= 0f)
                return;
            var t = (dy - y) / MathF.Max(h, 0.01f);
            Put(px, py, Lit(m, px, py, 1f - t, t < 0.22f ? (0.22f - t) * 3.5f : 0f), a);
        });
    }

    // A rod between two points - lit across its width like a cylinder, with a specular line.
    private static void Rod(float x0, float y0, float x1, float y1, float r, Mat m)
    {
        var dx0 = x1 - x0;
        var dy0 = y1 - y0;
        var len = MathF.Max(0.001f, MathF.Sqrt(dx0 * dx0 + dy0 * dy0));
        var (nx, ny) = (-dy0 / len, dx0 / len);
        if (ny > 0f || (ny == 0f && nx > 0f))
            (nx, ny) = (-nx, -ny); // normal toward the light (up / left)
        Each(MathF.Min(x0, x1) - r, MathF.Min(y0, y1) - r, MathF.Max(x0, x1) + r, MathF.Max(y0, y1) + r, (px, py, dx, dy) =>
        {
            var t = Math.Clamp(((dx - x0) * dx0 + (dy - y0) * dy0) / (len * len), 0f, 1f);
            var cx = x0 + dx0 * t;
            var cy = y0 + dy0 * t;
            var d = MathF.Sqrt((dx - cx) * (dx - cx) + (dy - cy) * (dy - cy));
            var a = Cover(r - d);
            if (a <= 0f)
                return;
            var s = Math.Clamp(((dx - cx) * nx + (dy - cy) * ny) / r, -1f, 1f); // +1 toward the light
            var spec = MathF.Exp(-MathF.Pow((s - 0.45f) / 0.22f, 2f)) * 1.6f;
            Put(px, py, Lit(m, px, py, 0.5f + 0.5f * s, spec), a);
        });
    }

    // A tapered rod - a cone between two radii (screwdriver handles, torch necks, nozzles), lit
    // across its width like Rod.
    private static void Taper(float x0, float y0, float r0, float x1, float y1, float r1, Mat m)
    {
        var dx0 = x1 - x0;
        var dy0 = y1 - y0;
        var len = MathF.Max(0.001f, MathF.Sqrt(dx0 * dx0 + dy0 * dy0));
        var (nx, ny) = (-dy0 / len, dx0 / len);
        if (ny > 0f || (ny == 0f && nx > 0f))
            (nx, ny) = (-nx, -ny);
        var rMax = MathF.Max(r0, r1);
        Each(MathF.Min(x0, x1) - rMax, MathF.Min(y0, y1) - rMax, MathF.Max(x0, x1) + rMax, MathF.Max(y0, y1) + rMax, (px, py, dx, dy) =>
        {
            var t = ((dx - x0) * dx0 + (dy - y0) * dy0) / (len * len);
            if (t < 0f || t > 1f)
                return;
            var r = r0 + (r1 - r0) * t;
            var cx = x0 + dx0 * t;
            var cy = y0 + dy0 * t;
            var d = MathF.Sqrt((dx - cx) * (dx - cx) + (dy - cy) * (dy - cy));
            if (d > r)
                return;
            var s = Math.Clamp(((dx - cx) * nx + (dy - cy) * ny) / r, -1f, 1f);
            var spec = MathF.Exp(-MathF.Pow((s - 0.45f) / 0.22f, 2f)) * 1.6f;
            Put(px, py, Lit(m, px, py, 0.5f + 0.5f * s, spec));
        });
    }

    // Knurling / diamond grip texture over painted art: alternating darker and lighter pixels in
    // a diagonal grid, only on pixels already painted inside the box.
    private static void Knurl(float x, float y, float w, float h, float strength = 0.35f) =>
        Each(x, y, x + w, y + h, (px, py, dx, dy) =>
        {
            if (dx < x || dx > x + w || dy < y || dy > y + h || _buf[py * _size + px].A < 200)
                return;
            var diag = (px + py) % 3;
            if (diag == 0)
                Put(px, py, Ink, strength);
            else if (diag == 1 && (px - py) % 3 == 0)
                Put(px, py, new Color(236, 230, 214), strength * 0.6f);
        });

    // A ball (sphere = true) or a flat disc lit from the top.
    private static void Ball(float cx, float cy, float r, Mat m, bool sphere = true)
    {
        Each(cx - r, cy - r, cx + r, cy + r, (px, py, dx, dy) =>
        {
            var nx = (dx - cx) / r;
            var ny = (dy - cy) / r;
            var d2 = nx * nx + ny * ny;
            var a = Cover(r - MathF.Sqrt(d2) * r);
            if (a <= 0f)
                return;
            if (!sphere)
            {
                Put(px, py, Lit(m, px, py, 0.5f - ny * 0.5f, 0f), a);
                return;
            }
            var nz = MathF.Sqrt(MathF.Max(0f, 1f - d2));
            var light = Math.Clamp(-0.5f * nx - 0.65f * ny + 0.55f * nz, 0f, 1f);
            var spec = MathF.Pow(Math.Clamp(-0.4f * nx - 0.55f * ny + 0.73f * nz, 0f, 1f), 18f) * 2.2f;
            Put(px, py, Lit(m, px, py, light, spec), a);
        });
    }

    // A ring of the given line width - flanges, rims, coils.
    private static void Ring(float cx, float cy, float r, float width, Mat m)
    {
        Each(cx - r - width, cy - r - width, cx + r + width, cy + r + width, (px, py, dx, dy) =>
        {
            var d = MathF.Sqrt((dx - cx) * (dx - cx) + (dy - cy) * (dy - cy));
            var a = Cover(width / 2f - MathF.Abs(d - r));
            if (a <= 0f)
                return;
            var ny = (dy - cy) / MathF.Max(d, 0.01f);
            Put(px, py, Lit(m, px, py, 0.5f - 0.45f * ny, 0f), a);
        });
    }

    // Any polygon (even-odd fill), lit from the top of its own bounding box.
    private static void Poly(Mat m, params float[] xy)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (var i = 0; i < xy.Length; i += 2)
        {
            minX = MathF.Min(minX, xy[i]); maxX = MathF.Max(maxX, xy[i]);
            minY = MathF.Min(minY, xy[i + 1]); maxY = MathF.Max(maxY, xy[i + 1]);
        }
        Each(minX, minY, maxX, maxY, (px, py, dx, dy) =>
        {
            if (!InsidePoly(xy, dx, dy))
                return;
            var t = (dy - minY) / MathF.Max(maxY - minY, 0.01f);
            Put(px, py, Lit(m, px, py, 1f - t, t < 0.2f ? (0.2f - t) * 3f : 0f));
        });
    }

    private static bool InsidePoly(float[] xy, float x, float y)
    {
        var inside = false;
        for (int i = 0, j = xy.Length - 2; i < xy.Length; j = i, i += 2)
        {
            if ((xy[i + 1] > y) != (xy[j + 1] > y)
                && x < (xy[j] - xy[i]) * (y - xy[i + 1]) / (xy[j + 1] - xy[i + 1]) + xy[i])
                inside = !inside;
        }
        return inside;
    }

    // A fine line of a set PIXEL width (detail: seams, grooves, labels) - doesn't scale up with the
    // icon, so it stays a crisp hairline.
    private static void Line(float x0, float y0, float x1, float y1, Color c, float a = 1f, float pixels = 1f)
    {
        var dx0 = x1 - x0;
        var dy0 = y1 - y0;
        var len2 = MathF.Max(0.0001f, dx0 * dx0 + dy0 * dy0);
        var half = pixels * 0.5f * _ss / _u; // pixel widths are FINAL pixels, whatever the supersampling
        Each(MathF.Min(x0, x1) - half, MathF.Min(y0, y1) - half, MathF.Max(x0, x1) + half, MathF.Max(y0, y1) + half, (px, py, dx, dy) =>
        {
            var t = Math.Clamp(((dx - x0) * dx0 + (dy - y0) * dy0) / len2, 0f, 1f);
            var ex = dx - (x0 + dx0 * t);
            var ey = dy - (y0 + dy0 * t);
            var cover = Cover(half - MathF.Sqrt(ex * ex + ey * ey));
            Put(px, py, c, a * cover);
        });
    }

    // A single lit dot (LEDs, rivets, glints) at least one pixel big.
    private static void Dot(float x, float y, Color c, float a = 1f, float r = 0f)
    {
        if (r * _u < 0.75f)
        {
            var (tx, ty) = _tilted ? Tilt(x, y) : (x, y);
            Put((int)(tx * _u), (int)(ty * _u), c, a);
            return;
        }
        Each(x - r, y - r, x + r, y + r, (px, py, dx, dy) =>
            Put(px, py, c, a * Cover(r - MathF.Sqrt((dx - x) * (dx - x) + (dy - y) * (dy - y)))));
    }

    // A flat, unshaded block of colour (silk-screen lettering, markings) - every pixel whose centre
    // falls inside it, so tiny lettering snaps to whole pixels instead of smearing.
    private static void Solid(float x, float y, float w, float h, Color c) =>
        Each(x, y, x + w, y + h, (px, py, dx, dy) =>
        {
            if (dx >= x && dx < x + w && dy >= y && dy < y + h)
                Put(px, py, c);
        });

    // A glow on an emitter (lit glass, a muzzle) - kept to the inside of the art: it only tints
    // pixels already painted, so it never spills a soft haze past the silhouette (part of what read
    // as blurry), and it stays faint.
    private static void Glow(float x, float y, float r, Color c, float strength = 0.7f)
    {
        r *= 0.6f;
        Each(x - r, y - r, x + r, y + r, (px, py, dx, dy) =>
        {
            var d = MathF.Sqrt((dx - x) * (dx - x) + (dy - y) * (dy - y)) / r;
            if (d < 1f && _buf[py * _size + px].A > 200)
                Put(px, py, c, strength * 0.45f * (1f - d));
        });
    }

    private static void EraseBox(float x, float y, float w, float h) =>
        Each(x, y, x + w, y + h, (px, py, dx, dy) =>
        {
            if (dx >= x && dx <= x + w && dy >= y && dy <= y + h)
                Erase(px, py);
        });

    private static void EraseBall(float cx, float cy, float r) =>
        Each(cx - r, cy - r, cx + r, cy + r, (px, py, dx, dy) =>
        {
            if ((dx - cx) * (dx - cx) + (dy - cy) * (dy - cy) <= r * r)
                Erase(px, py);
        });

    // ------------------------------------------------------------------ finish: bevel + outline

    private static readonly Color Outline = new(18, 16, 14);

    private static void Finish()
    {
        // Colour grade toward Barotrauma's palette: pull every colour a third of the way to its own
        // grey, warm it slightly (rust and old paint, not clean plastic), and take the whole thing
        // down a step - saturated paint stays recognisable but reads as worn.
        for (var i = 0; _grade && i < _buf.Length; i++)
        {
            var c = _buf[i];
            if (c.A == 0)
                continue;
            var lum = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
            float Grade(float v, float warm) => Math.Clamp((v + (lum - v) * 0.34f) * 0.9f * warm, 0f, 255f);
            _buf[i] = new Color((int)Grade(c.R, 1.03f), (int)Grade(c.G, 1f), (int)Grade(c.B, 0.93f), c.A);
        }

        var src = (Color[])_buf.Clone();
        byte A(int x, int y) => x < 0 || y < 0 || x >= _size || y >= _size ? (byte)0 : src[y * _size + x].A;

        for (var y = 0; y < _size; y++)
        for (var x = 0; x < _size; x++)
        {
            var i = y * _size + x;
            var c = src[i];
            // Rim widths are one FINAL pixel: _ss canvas pixels on a supersampled bake.
            bool Clear(int dx, int dy)
            {
                for (var k = 1; k <= _ss; k++)
                    if (A(x + dx * k, y + dy * k) < 90)
                        return true;
                return false;
            }
            bool ArtAt(int dx, int dy)
            {
                for (var k = 1; k <= _ss; k++)
                    if (A(x + dx * k, y + dy * k) >= 140)
                        return true;
                return false;
            }
            if (c.A >= 200)
            {
                // Bevel: the rim facing the light catches it, the one facing away falls into shade.
                if (Clear(-1, 0) || Clear(0, -1))
                    _buf[i] = Mix(c, new Color(220, 212, 196), 0.12f) with { A = c.A };
                else if (Clear(1, 0) || Clear(0, 1))
                    _buf[i] = Scale(c, 0.68f) with { A = c.A };
            }
            else if (c.A < 90)
            {
                // Outline: a transparent pixel next to art becomes the dark edge.
                var touches = ArtAt(-1, 0) || ArtAt(1, 0) || ArtAt(0, -1) || ArtAt(0, 1);
                if (touches)
                {
                    var a = 1f;
                    _buf[i] = new Color((int)(Outline.R * a + c.R * (1 - a)), (int)(Outline.G * a + c.G * (1 - a)),
                        (int)(Outline.B * a + c.B * (1 - a)), (int)(255 * a + c.A * (1 - a)));
                }
            }
        }
    }
}
