using System;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

/// <summary>The game's wordmark, built out of letter shapes and baked once at load.</summary>
///
/// The menu drew its title with the debug spritefont at scale 1.7 with a coloured glow behind it.
/// That reads as placeholder text however it is dressed, because it is placeholder text: a UI font
/// stretched up. A wordmark is a different thing - drawn shapes, with a surface and an edge.
///
/// Three layers make it, and none of them is the letters themselves:
///   - a mask, from the glyph outlines below. Everything after works off the mask, not off the
///     shapes, which is what keeps the bevel and the outline consistent across every letter;
///   - a surface: pitted steel, darker down the run, with corrosion and vertical weather streaks;
///   - an edge: a lit bevel up and left, a shadowed one down and right, a warm rim just outside the
///     metal, and a dark stroke outside that so the whole thing holds against any backdrop.
///
/// Glyphs are written as axis-aligned rectangles and quads in a 140-unit-tall cell, filled by an
/// even-odd scanline. Only the six letters of the name exist - this is a wordmark, not a font, and
/// building the other twenty for a string that never changes would be work spent on nothing.
public static partial class MenuLogo
{
    private const string Word = "ANABIOSIS";

    // Cell metrics, in glyph units. Stroke is heavy on purpose: at this weight the counters close
    // up into slots and the whole word reads as plate rather than as type.
    private const float CellHeight = 140f;
    private const float Stroke = 31f;
    // Tight. The reference sets its letters almost touching, and the gap is the difference
    // between a wordmark and a row of separate signs.
    private const float LetterGap = 2f;

    // Units to texture pixels. Sized so the finished wordmark fills the width the menu has free
    // between the title's left edge and the right side of the art pane.
    private const float UnitScale = 0.62f;

    // Room for the rim, the outer stroke and the shadow they sit in. Taller than it is wide: the
    // mark on the ANA|BIOSIS seam is meant to overrun the cap and the baseline, and it needs the
    // canvas to do it in.
    private const int Pad = 10;
    private const int PadY = 26;

    private static Texture2D? _baked;

    /// <summary>Baked once and kept - the wordmark never changes, and the surface noise in it is
    /// expensive enough per pixel that redrawing it live would be silly.</summary>
    public static Texture2D Get(GraphicsDevice graphics) => _baked ??= Bake(graphics);

    /// <summary>Distance from the top of the texture down to the top of the letters.</summary>
    ///
    /// The pupa runs past the cap line, so the image is taller than the word. Anything positioning
    /// the mark wants to line up the letters, not the bitmap, or the whole thing hangs low by
    /// however far the pod happens to stick out this week.
    public const int LetterInset = PadY;

    public static int Width { get; private set; }
    public static int Height { get; private set; }

    private static Texture2D Bake(GraphicsDevice graphics)
    {
        var advance = 0f;
        foreach (var ch in Word)
            advance += Advance(ch) + LetterGap;
        advance -= LetterGap;

        Width = (int)MathF.Round(advance * UnitScale) + Pad * 2;
        Height = (int)MathF.Round(CellHeight * UnitScale) + PadY * 2;

        var mask = new bool[Width * Height];
        var pen = (float)Pad;
        foreach (var ch in Word)
        {
            Stamp(mask, ch, pen);
            pen += (Advance(ch) + LetterGap) * UnitScale;
        }

        var c = new PixelCanvas(Width, Height);
        PaintPlate(c, mask);        // MenuLogo.Metal.cs
        PaintPupa(c);               // MenuLogo.Pupa.cs
        return c.ToTexture(graphics);
    }

    private static void Stamp(bool[] mask, char ch, float penX)
    {
        var (_, solids, holes) = GlyphOf(ch);
        foreach (var shape in solids)
            FillPolygon(mask, shape, penX, true);
        foreach (var shape in holes)
            FillPolygon(mask, shape, penX, false);
    }

    /// <summary>Even-odd scanline fill, in glyph units, writing straight into the mask.</summary>
    ///
    /// No antialiasing anywhere in here. A soft edge would be wrong twice over: the rest of this game
    /// is hard-edged pixel art, and the bevel pass downstream works by asking whether a neighbouring
    /// pixel is in or out - a half-covered one has no answer to give.
    private static void FillPolygon(bool[] mask, float[] pts, float penX, bool value)
    {
        var n = pts.Length / 2;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        for (var i = 0; i < n; i++)
        {
            var y = pts[i * 2 + 1];
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;
        }

        var y0 = Math.Max(0, (int)MathF.Floor(minY * UnitScale) + PadY);
        var y1 = Math.Min(Height - 1, (int)MathF.Ceiling(maxY * UnitScale) + PadY);
        Span<float> crossings = stackalloc float[16];

        for (var py = y0; py <= y1; py++)
        {
            // Sampled through the middle of the row, so a vertex landing exactly on a boundary does
            // not open or close the span twice.
            var uy = (py + 0.5f - PadY) / UnitScale;
            var hits = 0;
            for (var i = 0; i < n && hits < crossings.Length; i++)
            {
                var ax = pts[i * 2];
                var ay = pts[i * 2 + 1];
                var bx = pts[(i + 1) % n * 2];
                var by = pts[(i + 1) % n * 2 + 1];
                if (ay == by || uy < MathF.Min(ay, by) || uy >= MathF.Max(ay, by))
                    continue;
                crossings[hits++] = ax + (uy - ay) / (by - ay) * (bx - ax);
            }
            if (hits < 2)
                continue;

            var span = crossings[..hits];
            span.Sort();
            for (var k = 0; k + 1 < hits; k += 2)
            {
                var px0 = Math.Max(0, (int)MathF.Round(penX + span[k] * UnitScale));
                var px1 = Math.Min(Width - 1, (int)MathF.Round(penX + span[k + 1] * UnitScale) - 1);
                for (var px = px0; px <= px1; px++)
                    mask[py * Width + px] = value;
            }
        }
    }
}
