using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The shape that breaks the word, the way Barotrauma's mudraptor breaks BARO|TRAUMA.
//
// A pupa, hanging on the ANA|BIOSIS seam and overrunning the cap line and the baseline. It is there
// because it is what the word means: anabiosis is life suspended and then resumed - an organism that
// has stopped in every measurable way and is not dead. A pupa is the plainest picture of that, and it
// is also what is waiting in the precursor posts, so the mark states the title twice: once in
// letters, once in a shape.
//
// It was a curled larva first, twice. Both failed for the same reason: a curl is as wide as it is
// tall, and a body thin enough to keep off the next letter was too thin to read as anything but a
// hook. A pod is the shape that survives the constraint - narrow enough to clip only one letter's
// edge, thick enough to carry segments and a lit flank at this size, legible in silhouette alone.
public static partial class MenuLogo
{
    // Darker than the plate it hangs in front of, deliberately. The first pass matched the steel
    // for value and the two fought: a silhouette has to be the darkest thing in the mark or it stops
    // reading as being in front of anything.
    private static readonly Color Shell = new(19, 24, 27);
    private static readonly Color ShellLit = new(94, 108, 110);
    private static readonly Color ShellDeep = new(7, 10, 12);

    // In glyph units, not pixels, so the pod grows with the letters. Written in pixels first, it
    // stayed the same size when the cell was scaled up and shrank to a seed next to the word.
    private const float PodHalf = 27f;        // half-width at the widest point
    private const float PodOver = 40f;        // how far past the letters it runs, top and bottom

    private static void PaintPupa(PixelCanvas c)
    {
        // The seam between the third and fourth letters, worked out from the advances rather than
        // eyeballed, so retuning the letter widths does not leave the mark stranded mid-glyph.
        var seam = (float)Pad;
        for (var i = 0; i < 3; i++)
            seam += (Advance(Word[i]) + LetterGap) * UnitScale;
        // Nudged off the middle of the gap and onto the letter before it. Sitting dead on the seam
        // it took the left stem off the B, and a B without its stem reads as two bumps; the A it
        // covers instead loses part of a splayed leg, which the eye fills in.
        seam -= LetterGap * UnitScale * 0.5f + 9f;

        var top = PadY - PodOver * UnitScale;
        var bottom = PadY + CellHeight * UnitScale + PodOver * UnitScale;
        var span = bottom - top;

        // A slight lean, so it hangs rather than stands. Straight, it reads as a rivet.
        static float Axis(float seam, float t) => seam + MathF.Sin(t * 2.3f) * 3.2f - 1.5f;

        // Widest above centre and drawn to a point at the tail: an insect pupa, not an egg.
        static float Half(float t)
        {
            var w = PodHalf * UnitScale * MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(Math.Clamp(t, 0f, 1f), 0.55f)), 0.62f);
            return t > 0.86f ? w * (1f - (t - 0.86f) / 0.14f * 0.75f) : w;
        }

        DropShadow(c, seam, top, span, Axis, Half);
        Body(c, seam, top, span, Axis, Half);
        Segments(c, seam, top, span, Axis, Half);
        Silk(c, seam, top);
    }

    private static void DropShadow(PixelCanvas c, float seam, float top, float span,
                                   Func<float, float, float> axis, Func<float, float> half)
    {
        // Offset, and drawn once per row. Ringing the outline with soft discs is what turned the
        // first two attempts into a solid black blob: overlapping low-alpha discs saturate.
        for (var i = 0; i <= (int)span; i++)
        {
            var t = i / span;
            var w = half(t);
            if (w < 0.6f)
                continue;
            var cx = axis(seam, t) + 3f;
            c.Rect(cx - w, top + i + 3.5f, w * 2f, 1f, Outline, 0.5f);
        }
    }

    private static void Body(PixelCanvas c, float seam, float top, float span,
                             Func<float, float, float> axis, Func<float, float> half)
    {
        for (var i = 0; i <= (int)span; i++)
        {
            var t = i / span;
            var w = half(t);
            if (w < 0.6f)
                continue;
            var cx = axis(seam, t);
            var y = top + i;

            // Shaded across the pod the same way the hull in the backdrop is shaded across its
            // barrel: a bright edge up-left, body, then shadow. Two values would make a stripe;
            // four make a rounded thing.
            for (var s = -w; s <= w; s += 0.5f)
            {
                var n = s / w;                                  // -1 lit flank, +1 shadow flank
                var col =
                    n < -0.88f ? Mix(ShellLit, Rim, 0.30f)
                    : n < -0.40f ? Mix(Shell, ShellLit, (-n - 0.40f) / 0.48f * 0.75f)
                    : n < 0.40f ? Shell
                    : Mix(Shell, ShellDeep, (n - 0.40f) / 0.60f);

                // Chitin is not smooth. Keyed off position so it stays put between bakes.
                var grain = PixelCanvas.Hash((int)(t * 400f), (int)(n * 24f));
                c.Px(cx + s, y, Mix(col, ShellDeep, grain * 0.20f), 1f);
            }
        }
    }

    private static void Segments(PixelCanvas c, float seam, float top, float span,
                                 Func<float, float, float> axis, Func<float, float> half)
    {
        // Abdominal rings across the lower two thirds, each bowed downward. Flat lines would read as
        // a barcode; the bow is what says the surface they lie on is curved.
        for (var k = 0; k < 7; k++)
        {
            var t = 0.40f + k * 0.075f;
            var w = half(t);
            if (w < 1.2f)
                continue;
            var cx = axis(seam, t);
            var y = top + t * span;
            for (var s = -w * 0.92f; s <= w * 0.92f; s += 0.5f)
            {
                var n = s / w;
                var bow = (1f - n * n) * 2.4f;
                c.Px(cx + s, y + bow, ShellDeep, 0.75f);
                c.Px(cx + s, y + bow - 1f, ShellLit, 0.28f);
            }
        }

        // Wing cases: two long ridges down the lit flank of the upper half, which is the one feature
        // that makes a pod read specifically as a pupa rather than as a seed or a cocoon.
        for (var side = 0; side < 2; side++)
        for (var i = 0; i <= (int)(span * 0.42f); i++)
        {
            var t = 0.16f + i / span;
            var w = half(t);
            if (w < 2f)
                continue;
            var cx = axis(seam, t);
            c.Px(cx + w * (side == 0 ? -0.42f : -0.06f), top + t * span, ShellDeep, 0.34f);
        }
    }

    private static void Silk(PixelCanvas c, float seam, float top)
    {
        // What it is hanging by. Two threads, because one reads as a scratch on the plate.
        var x = seam - 1.5f;
        c.Line(x, top - 6f, x + 1.5f, top + 4f, ShellLit, 0.45f);
        c.Line(x + 3f, top - 6f, x + 1f, top + 4f, ShellLit, 0.30f);
        c.Disc(x + 1.2f, top - 6f, 2.2f, Shell, 0.85f);
    }
}
