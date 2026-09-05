using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

/// <summary>A small software canvas for baking sprite art once, at load, instead of drawing it with
/// hundreds of one-pixel quads every frame.</summary>
///
/// Anything with real surface detail - brushed metal, wear, bolts, grime - costs more per-pixel work
/// than a sprite batch should ever do at sixty frames a second, and none of it changes. So it is
/// painted here into an array and handed to the GPU as one texture.
///
/// Composites source-over with real alpha, which matters for anything whose background is meant to
/// stay transparent: a turret is a shape on the hull, not a filled square, and getting the operand
/// order wrong here is the sort of bug that renders a perfectly correct drawing as nothing at all.
internal sealed class PixelCanvas
{
    public readonly int Width;
    public readonly int Height;

    private readonly Vector4[] _pixels;    // straight (non-premultiplied) RGBA, 0..1

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new Vector4[width * height];
    }

    public static float Hash(int a, int b = 0)
    {
        var n = unchecked(a * 374761393 + b * 668265263);
        n = unchecked((n ^ (n >> 13)) * 1274126177);
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    public void Px(float fx, float fy, Color c, float a = 1f)
    {
        int x = (int)MathF.Round(fx), y = (int)MathF.Round(fy);
        if (a <= 0f || x < 0 || y < 0 || x >= Width || y >= Height)
            return;

        var s = new Vector3(c.R / 255f, c.G / 255f, c.B / 255f);
        var d = _pixels[y * Width + x];
        var outA = a + d.W * (1f - a);
        if (outA <= 0f)
        {
            _pixels[y * Width + x] = Vector4.Zero;
            return;
        }
        // Source-over, and in that order. Destination-over looks nearly identical on an opaque
        // background and renders as an empty sprite on a transparent one.
        var rgb = (s * a + new Vector3(d.X, d.Y, d.Z) * d.W * (1f - a)) / outA;
        _pixels[y * Width + x] = new Vector4(rgb, outA);
    }

    /// <summary>How covered a pixel is. The outline passes need it: a ring is defined by which
    /// cells are empty next to which are not.</summary>
    public float Alpha(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Height ? 0f : _pixels[y * Width + x].W;

    public void Rect(float x, float y, float w, float h, Color c, float a = 1f)
    {
        for (var yy = (int)MathF.Round(y); yy < (int)MathF.Round(y + h); yy++)
        for (var xx = (int)MathF.Round(x); xx < (int)MathF.Round(x + w); xx++)
            Px(xx, yy, c, a);
    }

    public void Disc(float cx, float cy, float r, Color c, float a = 1f)
    {
        for (var yy = (int)(cy - r) - 1; yy <= (int)(cy + r) + 1; yy++)
        for (var xx = (int)(cx - r) - 1; xx <= (int)(cx + r) + 1; xx++)
        {
            var d = MathF.Sqrt((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy));
            if (d <= r)
                Px(xx, yy, c, a * MathF.Min(1f, r - d + 0.5f));
        }
    }

    public void Ring(float cx, float cy, float r, Color c, float a = 1f, float w = 1f)
    {
        for (var yy = (int)(cy - r) - 2; yy <= (int)(cy + r) + 2; yy++)
        for (var xx = (int)(cx - r) - 2; xx <= (int)(cx + r) + 2; xx++)
        {
            var d = MathF.Abs(MathF.Sqrt((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy)) - r);
            if (d <= w)
                Px(xx, yy, c, a * (1f - d / (w + 0.4f)));
        }
    }

    public void Line(float x0, float y0, float x1, float y1, Color c, float a = 1f)
    {
        var n = (int)(MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) * 2f) + 1;
        for (var i = 0; i <= n; i++)
        {
            var t = i / (float)n;
            Px(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, c, a);
        }
    }

    /// <summary>A horizontal run shaded like a cylinder seen from the side: bright along one edge,
    /// dark along the other. Two lines of shading is the whole difference between a barrel and a
    /// rectangle.</summary>
    public void Tube(float x, float y, float w, float h, Color body, float a = 1f)
    {
        Rect(x, y, w, h, body, a);
        Rect(x, y, w, 1, Color.White, 0.30f * a);
        if (h >= 3f)
            Rect(x, y + 1, w, 1, Color.White, 0.12f * a);
        Rect(x, y + h - 1, w, 1, Color.Black, 0.42f * a);
    }

    /// <summary>Keeps whichever of the two is brighter, per channel, on an already-opaque pixel.</summary>
    ///
    /// Source-over cannot lay one picture over another without a mask saying where one stops, and a
    /// mask cut out of dithered pixel art never comes out clean. Compositing by "whichever is
    /// lighter" needs no mask at all: the dark half of the overlay loses to whatever is underneath
    /// it, and only what is actually drawn in it survives.
    public void Max(float fx, float fy, Color c)
    {
        int x = (int)MathF.Round(fx), y = (int)MathF.Round(fy);
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            return;

        var d = _pixels[y * Width + x];
        _pixels[y * Width + x] = new Vector4(
            MathF.Max(d.X, c.R / 255f),
            MathF.Max(d.Y, c.G / 255f),
            MathF.Max(d.Z, c.B / 255f),
            MathF.Max(d.W, 1f));
    }

    public Texture2D ToTexture(GraphicsDevice graphics)
    {
        var data = new Color[Width * Height];
        for (var i = 0; i < data.Length; i++)
        {
            var p = _pixels[i];
            // SpriteBatch's default blending expects premultiplied alpha.
            data[i] = new Color(p.X * p.W, p.Y * p.W, p.Z * p.W, p.W);
        }
        var texture = new Texture2D(graphics, Width, Height);
        texture.SetData(data);
        return texture;
    }
}
