using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// A procedural wall terminal face, same baked-in-code approach as ReactorTexture. Direct user
// request after the first two passes - no perspective/tilt (a plain flat rectangle, not the
// trapezoid "leaning back" look tried earlier) and small enough to sit on a single wall tile
// (1 game unit) rather than spanning 2 - so this is deliberately simple: a screen with a small bar
// graph and grid, and a short row of keys below it, no buttons/levers/knob (there is no room left
// to draw those legibly at this size).
public static class TerminalTexture
{
    // 1 game unit at ShipRenderer.PixelsPerUnit (48) - drawn 1:1, no up/downscaling.
    public const int Size = 48;

    public static Texture2D Create(GraphicsDevice device)
    {
        var texture = new Texture2D(device, Size, Size);
        var data = new Color[Size * Size];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                data[y * Size + x] = PixelAt(x, y);
        texture.SetData(data);
        return texture;
    }

    private static readonly Color Bezel = new(132, 140, 150);
    private static readonly Color BezelDark = new(96, 103, 112);
    private static readonly Color Highlight = new(196, 202, 210);
    private static readonly Color Shadow = new(8, 9, 11);
    private static readonly Color ScreenBg = new(10, 16, 22);
    private static readonly Color GridLine = new(24, 40, 46);
    private static readonly Color BarCold = new(46, 168, 150);
    private static readonly Color BarHot = new(140, 226, 120);
    private static readonly Color KeyColor = new(58, 64, 72);
    private static readonly Color KeyLit = new(70, 200, 190);
    private static readonly Color StatusLight = new(90, 230, 150);

    private const int BezelWidth = 3;
    private const int ScreenLeft = BezelWidth, ScreenRight = Size - BezelWidth, ScreenTop = BezelWidth, ScreenBottom = 32;
    private const int KeyTop = 35, KeyBottom = Size - BezelWidth;

    private static readonly int[] BarHeights = { 2, 5, 3, 7, 4, 9, 5, 8, 3, 6, 4, 7, 2, 5 };

    private static Color PixelAt(int x, int y)
    {
        if (EdgeDistance(x, y) < 1f) return Shadow;

        if (x >= ScreenLeft && x < ScreenRight && y >= ScreenTop && y < ScreenBottom)
            return ScreenPixel(x - ScreenLeft, y - ScreenTop, ScreenRight - ScreenLeft, ScreenBottom - ScreenTop);

        if (y >= KeyTop && y < KeyBottom && x >= ScreenLeft && x < ScreenRight)
            return KeyPixel(x - ScreenLeft, y - KeyTop, ScreenRight - ScreenLeft);

        return BezelPixel(x, y);
    }

    private static float EdgeDistance(int x, int y) => MathF.Min(MathF.Min(x, Size - 1 - x), MathF.Min(y, Size - 1 - y));

    // The metal casing - a top-lit bevel, a status light, two corner rivets, and a seam line where
    // the screen frame meets the key row - small touches rather than a busy surface, since there
    // isn't room here for much more than that.
    private static Color BezelPixel(int x, int y)
    {
        var edge = EdgeDistance(x, y);
        var shade = edge < 1.5f ? 0.16f : 0f;
        if (y > Size - 2) shade -= 0.14f;
        if (y == ScreenBottom + 1) shade += 0.12f;
        if (y == KeyTop - 1) shade -= 0.12f;

        var color = Shade(Bezel, shade);

        var dx = x - (Size - 5);
        var dy = y - 3;
        if (dx * dx + dy * dy <= 2) color = StatusLight;

        foreach (var (rx, ry) in RivetSpots)
        {
            var rdx = x - rx;
            var rdy = y - ry;
            if (rdx * rdx + rdy * rdy <= 1) color = Shade(Bezel, rdx + rdy < 0 ? 0.2f : -0.2f);
        }

        return color;
    }

    private static readonly (int X, int Y)[] RivetSpots = { (2, 2), (Size - 3, Size - 3), (2, Size - 3) };

    // A dark readout screen: a faint grid, a denser bar graph, and a faint diagonal glare - the
    // same small "active display" vocabulary as the larger version, packed a bit tighter to stay
    // busy at this size instead of reading as three or four fat blocks.
    private static Color ScreenPixel(int x, int y, int w, int h)
    {
        var color = ScreenBg;

        if (x % 4 == 0 || y % 4 == 0) color = Color.Lerp(color, GridLine, 0.5f);

        const int barWidth = 1, barGap = 1, baseline = 1;
        var barIndex = x / (barWidth + barGap);
        if (barIndex < BarHeights.Length && x % (barWidth + barGap) < barWidth)
        {
            var barHeight = BarHeights[barIndex];
            var fromBottom = h - 1 - baseline - y;
            if (fromBottom >= 0 && fromBottom < barHeight)
            {
                var t = fromBottom / (float)barHeight;
                color = Color.Lerp(BarCold, BarHot, t);
            }
        }

        var diag = x - y;
        if (diag is >= 6 and <= 8) color = Color.Lerp(color, Highlight, 0.08f);

        return color;
    }

    // A short row of 4 flat keys, one lit - deliberately plain next to the busier screen above it,
    // rather than trying to cram in the buttons/levers/knob the larger 2-unit draft had room for.
    private static Color KeyPixel(int x, int y, int w)
    {
        const int cols = 4;
        var cellW = w / (float)cols;
        var col = (int)(x / cellW);
        var localX = x - col * cellW;
        const float margin = 0.8f;

        if (localX < margin || y < margin || localX > cellW - margin || y > KeyBottom - KeyTop - 1 - margin)
            return Shade(BezelDark, -0.1f);

        return col == 2 ? KeyLit : KeyColor;
    }

    private static Color Shade(Color baseTone, float shade)
    {
        var t = MathHelper.Clamp(0.5f + shade * 2.2f, 0f, 1f);
        return t > 0.5f ? Color.Lerp(baseTone, Highlight, (t - 0.5f) * 2f) : Color.Lerp(Shadow, baseTone, t * 2f);
    }
}
