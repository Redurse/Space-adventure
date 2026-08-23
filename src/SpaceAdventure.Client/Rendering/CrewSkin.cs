using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

/// <summary>A crew member, in the Space Station 13 idiom.</summary>
///
/// Everything that made the previous sprite look the way it did is wrong for this style, so none of
/// it survived:
///
///   * a 32x32 logical grid, not 128. The art is small, and smallness is not a limitation here - it
///     is the look. A figure with thirty rows has to say everything with shape;
///   * flat fills, two or three tones per material, no gradients anywhere. Painterly shading is
///     precisely what stops pixel art reading as pixel art;
///   * hard pixels. Nothing is anti-aliased and the grid is enlarged by whole-number blocks, so a
///     pixel stays a pixel instead of dissolving into a filter;
///   * a dark outline, added by dilating the finished silhouette. That one ring is most of why the
///     style reads crisply against any background;
///   * a one-pixel gap between each limb and the body, left transparent so the outline pass fills
///     it. Without those seams the whole figure reads as a single block - which is exactly how the
///     first attempt at this came out.
///
/// Proportions are chunky on purpose: big head, short body, stubby legs. Real proportions at this
/// size give a stick with a pinhead.
///
/// Still three drawings and four facings - front, back, and a side that mirrors.
public sealed class CrewSkin : IDisposable
{
    public enum View { Front, Side, Back }

    // 48 rather than 32.
    //
    // The chunkiness was never really a choice of style - it was the grid. Thirty rows will not hold
    // a person: a head big enough to carry a face eats a third of the height and what is left is a
    // torso and two stubs. Forty rows is a little over five heads, which is stylised but built like
    // an adult rather than like a doll.
    private const int G = 48;
    // Dropped from 3 with the figure's own size, so the texture is not three times larger than it
    // is ever drawn - a 120px bake shown at 60 throws two thirds of itself away in the filter.
    private const int Block = 2;   // texels per art pixel
    private const int TopRow = 4;
    private const int FootRow = 44;
    private const int Mid = 24;

    // The row plan the whole figure hangs off.
    private const int YHead = 4, YChin = 11;
    private const int YSh = 13, YWaist = 21, YHip = 25, YAnkle = 41;

    public const float FigureHeight = (FootRow - TopRow) * Block;

    /// <summary>Feet, centre. A standing sprite hangs off this.</summary>
    public static readonly Vector2 Origin = new(Mid * Block, FootRow * Block);

    private readonly GraphicsDevice _graphics;
    private readonly Dictionary<(uint Body, uint Accent, bool Suited, View View), Texture2D> _cache = new();

    public CrewSkin(GraphicsDevice graphics) => _graphics = graphics;

    public void Dispose()
    {
        foreach (var texture in _cache.Values)
            texture.Dispose();
        _cache.Clear();
    }

    /// <summary>Draws a crewman standing at `feet`, turned to face `facing`.</summary>
    public void Draw(SpriteBatch spriteBatch, Vector2 feet, float height, Color body, Color accent, bool suited,
        Vector2 facing)
    {
        // Sideways wins unless the character is clearly facing up or down the screen: a person
        // walking at any angle reads best in profile, and snapping to front or back too eagerly
        // makes them look like they are pivoting on the spot.
        var view = MathF.Abs(facing.X) >= MathF.Abs(facing.Y) * 0.75f ? View.Side
            : facing.Y >= 0f ? View.Front
            : View.Back;
        var flip = view == View.Side && facing.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

        spriteBatch.Draw(Get(body, accent, suited, view), feet, null, Color.White, 0f, Origin,
            height / FigureHeight, flip, 0f);
    }

    private Texture2D Get(Color body, Color accent, bool suited, View view)
    {
        var key = (body.PackedValue, accent.PackedValue, suited, view);
        if (_cache.TryGetValue(key, out var cached))
            return cached;
        var baked = Bake(body, accent, suited, view);
        _cache[key] = baked;
        return baked;
    }

    // ---------------------------------------------------------------- palette

    private const char Empty = ' ';

    private static Color Darken(Color c, float t) =>
        new((int)(c.R * (1f - t)), (int)(c.G * (1f - t)), (int)(c.B * (1f - t)));

    private static Color Lighten(Color c, float t) => new(
        (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t));

    private static Dictionary<char, Color> Palette(Color body, Color accent) => new()
    {
        ['o'] = new Color(26, 24, 30),
        ['h'] = new Color(38, 34, 40), ['H'] = new Color(58, 54, 62),          // hair, highlight
        ['s'] = new Color(238, 196, 152), ['S'] = new Color(198, 156, 118),    // skin, shadow
        ['b'] = body, ['B'] = Darken(body, 0.26f), ['L'] = Lighten(body, 0.20f),
        ['v'] = Darken(body, 0.42f), ['V'] = Darken(body, 0.55f),              // vest
        ['t'] = Darken(body, 0.50f), ['T'] = Darken(body, 0.64f),              // trousers
        ['f'] = new Color(206, 208, 214), ['F'] = new Color(150, 154, 162),    // boots
        ['a'] = accent,
        ['g'] = new Color(196, 200, 210), ['G'] = new Color(150, 156, 168),    // suit shell
        ['w'] = new Color(46, 96, 126), ['W'] = new Color(150, 206, 232),      // visor, glint
        ['p'] = new Color(28, 32, 38),                                         // panels
        ['c'] = new Color(96, 232, 168),                                       // a lit indicator
    };

    // ---------------------------------------------------------------- the grid

    private sealed class Grid
    {
        public readonly char[,] Cells = new char[G, G];

        public Grid()
        {
            for (var y = 0; y < G; y++)
            for (var x = 0; x < G; x++)
                Cells[y, x] = Empty;
        }

        public void Rect(int x0, int y0, int x1, int y1, char k)
        {
            for (var y = Math.Max(0, y0); y <= Math.Min(G - 1, y1); y++)
            for (var x = Math.Max(0, x0); x <= Math.Min(G - 1, x1); x++)
                Cells[y, x] = k;
        }

        /// <summary>Takes cells back out again. Corners come off this way rather than being
        /// avoided while painting: it is far easier to build a shape square and then chamfer it than
        /// to express every rounded edge as a run of rectangles.</summary>
        public void Cut(params (int X, int Y)[] cells)
        {
            foreach (var (x, y) in cells)
                if (x >= 0 && x < G && y >= 0 && y < G)
                    Cells[y, x] = Empty;
        }

        public void Px(int x, int y, char k)
        {
            if (x >= 0 && x < G && y >= 0 && y < G)
                Cells[y, x] = k;
        }

        /// <summary>Dilates the finished silhouette by one and paints the ring dark. Run last, so it
        /// wraps whatever the figure turned out to be rather than being drawn round each part by
        /// hand - and so it fills the deliberate gaps between the limbs and the body.</summary>
        public void Outline()
        {
            var ring = new List<(int X, int Y)>();
            for (var y = 0; y < G; y++)
            for (var x = 0; x < G; x++)
            {
                if (Cells[y, x] != Empty)
                    continue;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= G || ny >= G)
                        continue;
                    if (Cells[ny, nx] is not Empty and not 'o')
                    {
                        ring.Add((x, y));
                        break;
                    }
                }
            }
            foreach (var (x, y) in ring)
                Cells[y, x] = 'o';
        }
    }

    // ---------------------------------------------------------------- the figure

    private Texture2D Bake(Color body, Color accent, bool suited, View view)
    {
        var g = new Grid();
        var side = view == View.Side;
        var back = view == View.Back;

        // Legs first, then boots. Sixteen rows against the torso's thirteen - roughly what an adult
        // actually is, and the one thing the small grid could not afford.
        if (side)
        {
            g.Rect(20, YHip, 27, YAnkle, 'T');
            g.Rect(21, YHip, 26, YAnkle, 't');
            g.Rect(19, YAnkle + 1, 29, FootRow, 'F');
            g.Rect(20, YAnkle + 1, 28, FootRow - 1, 'f');
        }
        else
        {
            g.Rect(19, YHip, 23, YAnkle, 't');
            g.Rect(25, YHip, 29, YAnkle, 'T');
            g.Rect(18, YAnkle + 1, 23, FootRow, 'f');
            g.Rect(25, YAnkle + 1, 30, FootRow, 'F');
        }

        // Arms, one pixel clear of the chest so the outline pass lays a seam between them.
        if (side)
        {
            g.Rect(21, YSh + 1, 26, YHip - 1, 'B');
            g.Rect(21, YHip, 26, YHip + 2, 'S');
        }
        else
        {
            g.Rect(12, YSh, 15, YHip - 1, 'b');
            g.Rect(33, YSh, 36, YHip - 1, 'B');
            g.Rect(12, YHip, 15, YHip + 2, 's');
            g.Rect(33, YHip, 36, YHip + 2, 'S');
        }

        // Trunk: shoulders sloped in at the top, waist drawn in, hips back out.
        var trunk = new (int X0, int X1)[13];
        for (var i = 0; i < trunk.Length; i++)
            trunk[i] = i == 0 ? (side ? (19, 29) : (18, 30))
                : i <= 5 ? (side ? (18, 30) : (17, 31))
                : i <= 9 ? (side ? (19, 29) : (19, 29))
                : (side ? (18, 30) : (18, 30));
        for (var i = 0; i < trunk.Length; i++)
        {
            var y = YSh + i;
            if (y > YHip)
                break;
            var (x0, x1) = trunk[i];
            g.Rect(x0, y, x1, y, 'b');
            g.Rect(x1 - (side ? 1 : 2), y, x1, y, 'B');
            if (!side)
                g.Rect(x0, y, x0 + 1, y, 'L');
        }

        if (suited)
        {
            if (!back)
            {
                g.Rect(21, YSh + 3, 27, YSh + 8, 'p');
                g.Px(23, YSh + 5, 'c');
                if (!side)
                    g.Px(26, YSh + 5, 'a');
            }
            if (back || side)
            {
                if (side)
                {
                    g.Rect(16, YSh + 1, 19, YHip - 2, 'V');
                    g.Rect(16, YSh + 2, 16, YHip - 3, 'G');
                }
                else
                {
                    g.Rect(19, YSh + 1, 29, YHip - 2, 'V');
                    g.Rect(21, YSh + 2, 22, YHip - 3, 'G');
                    g.Rect(26, YSh + 2, 27, YHip - 3, 'G');
                }
            }
        }
        else
        {
            if (side)
            {
                g.Rect(20, YSh + 1, 28, YHip - 1, 'v');
                g.Rect(27, YSh + 1, 28, YHip - 1, 'V');
            }
            else
            {
                g.Rect(18, YSh + 1, 30, YHip - 1, 'v');
                g.Rect(28, YSh + 1, 30, YHip - 1, 'V');
                if (!back)
                {
                    g.Rect(23, YSh + 1, 25, YWaist - 2, 'b');
                    g.Px(24, YWaist, 'a');
                }
            }
            g.Rect(side ? 19 : 18, YHip - 1, side ? 28 : 30, YHip, 'T');
        }

        if (!side)
        {
            g.Px(13, YSh + 1, 'a');
            if (!back)
                g.Px(35, YSh + 1, 'a');
            g.Cut((12, YSh), (36, YSh), (12, YHip + 2), (36, YHip + 2), (18, FootRow), (30, FootRow));
        }
        else
        {
            g.Cut((21, YSh + 1), (26, YSh + 1), (21, YHip + 2), (26, YHip + 2), (19, FootRow), (29, FootRow));
        }

        Head(g, view, suited);
        g.Outline();

        // Blown up in whole blocks. Filtering a small sprite up to size is what turns pixel art into
        // a smear; enlarging it here means the texture already carries hard edges.
        var palette = Palette(body, accent);
        var size = G * Block;
        var data = new Color[size * size];
        for (var y = 0; y < G; y++)
        for (var x = 0; x < G; x++)
        {
            var k = g.Cells[y, x];
            if (k == Empty)
                continue;
            var c = palette[k];
            for (var by = 0; by < Block; by++)
            for (var bx = 0; bx < Block; bx++)
                data[(y * Block + by) * size + x * Block + bx] = c;
        }

        var texture = new Texture2D(_graphics, size, size);
        texture.SetData(data);
        return texture;
    }

    private static void Head(Grid g, View view, bool suited)
    {
        var side = view == View.Side;
        var back = view == View.Back;

        if (suited)
        {
            g.Rect(19, YHead, 29, YChin + 1, 'g');
            g.Cut((19, YHead), (29, YHead), (19, YChin + 1), (29, YChin + 1));
            if (back)
            {
                g.Rect(21, YHead + 2, 27, YChin - 1, 'G');
                return;
            }
            if (side)
            {
                g.Rect(19, YHead, 22, YHead + 1, 'G');
                g.Rect(24, YHead + 2, 28, YHead + 6, 'w');
                g.Rect(25, YHead + 3, 26, YHead + 3, 'W');
                return;
            }
            g.Rect(20, YHead + 2, 28, YHead + 6, 'w');
            g.Rect(21, YHead + 3, 22, YHead + 3, 'W');
            return;
        }

        if (back)
        {
            g.Rect(19, YHead, 29, YChin, 'h');
            g.Rect(21, YHead + 1, 27, YChin - 1, 'H');
        }
        else if (side)
        {
            g.Rect(19, YHead, 29, YHead + 2, 'h');
            g.Rect(19, YHead + 3, 22, YChin, 'h');       // hair down the back of the skull
            g.Rect(23, YHead + 3, 28, YChin, 's');
            g.Rect(29, YHead + 5, 29, YHead + 6, 's');   // the nose
            g.Rect(23, YChin - 1, 27, YChin, 'S');
            g.Px(26, YHead + 5, 'o');
        }
        else
        {
            g.Rect(19, YHead, 29, YHead + 2, 'h');
            g.Rect(19, YHead + 3, 19, YHead + 5, 'h');
            g.Rect(29, YHead + 3, 29, YHead + 5, 'h');
            g.Rect(20, YHead + 3, 28, YChin, 's');
            g.Rect(27, YHead + 6, 28, YChin, 'S');
            g.Px(22, YHead + 5, 'o');
            g.Px(26, YHead + 5, 'o');
            g.Rect(23, YHead + 7, 25, YHead + 7, 'S');
        }
        // A square head is the single loudest thing telling the eye a sprite was made of rectangles.
        g.Cut((19, YHead), (29, YHead), (19, YChin), (29, YChin));
        g.Rect(22, YChin + 1, 26, YSh - 1, 'S');        // neck
    }
}
