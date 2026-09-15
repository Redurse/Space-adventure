using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The shape that breaks the word, the way Barotrauma's mudraptor breaks BARO|TRAUMA - direct user
// reference (a screenshot of that exact logo, "вот референс как это сделано").
//
// A small reptile hanging on the ANA|BIOSIS seam and overrunning the cap line and the baseline: a
// head with a snout and a horn, an S-curved spine, two pairs of splayed clawed legs, a row of back
// spikes down the lit flank, and a tail that curls at the tip. Earlier passes drew a smooth
// symmetric pod instead (an insect pupa, tying into what "anabiosis" itself means - see the git
// history on this file) - legible as a shape, but symmetric and limbless, which read as a capsule
// or a drill bit rather than a creature. A creature needs the asymmetry a spine, legs and a head
// give it; that is the whole difference this pass makes.
public static partial class MenuLogo
{
    // Darker than the plate it hangs in front of, deliberately. The first pass matched the steel
    // for value and the two fought: a silhouette has to be the darkest thing in the mark or it stops
    // reading as being in front of anything.
    private static readonly Color Shell = new(19, 24, 27);
    private static readonly Color ShellLit = new(94, 108, 110);
    private static readonly Color ShellDeep = new(7, 10, 12);

    // In glyph units, not pixels, so the creature grows with the letters.
    private const float PodHalf = 27f;        // torso half-width reference at its widest point
    private const float PodOver = 40f;        // how far past the letters it runs, top and bottom

    private static void PaintPupa(PixelCanvas c)
    {
        // The seam between the third and fourth letters, worked out from the advances rather than
        // eyeballed, so retuning the letter widths does not leave the mark stranded mid-glyph.
        var seam = (float)Pad;
        for (var i = 0; i < 3; i++)
            seam += (Advance(Word[i]) + LetterGap) * UnitScale;
        seam -= LetterGap * UnitScale * 0.5f + 9f;

        var top = PadY - PodOver * UnitScale;
        var bottom = PadY + CellHeight * UnitScale + PodOver * UnitScale;
        var span = bottom - top;
        var maxHalf = PodHalf * UnitScale;

        // An S-curved spine - two frequencies so it reads as a live pose rather than a single
        // lean, with the tail whipping into a tighter curl over its last stretch.
        static float Axis(float seam, float t)
        {
            var curl = t > 0.86f ? (t - 0.86f) / 0.14f : 0f;
            return seam + MathF.Sin(t * 3.1f) * 6.5f + MathF.Sin(t * 1.4f + 0.6f) * 3f - 2f + curl * curl * 10f;
        }

        // Head, torso, tail - a creature's own proportions, not one smooth taper. 0-0.14 is the
        // head narrowing into a neck, 0.14-0.78 the torso (a slight ripple stands in for ribs),
        // 0.78-1 the tail running out to a point.
        float Half(float t)
        {
            if (t < 0.05f) return MathHelper.Lerp(0f, maxHalf * 0.55f, t / 0.05f);
            if (t < 0.14f) return MathHelper.Lerp(maxHalf * 0.55f, maxHalf * 0.42f, (t - 0.05f) / 0.09f);
            if (t < 0.78f)
            {
                var u = (t - 0.14f) / 0.64f;
                var ribs = 1f + MathF.Sin(u * MathF.PI * 3f) * 0.10f;
                return MathHelper.Lerp(maxHalf, maxHalf * 0.5f, u) * ribs;
            }
            var tailT = (t - 0.78f) / 0.22f;
            return MathHelper.Lerp(maxHalf * 0.5f, 0f, MathF.Pow(tailT, 0.7f));
        }

        DropShadow(c, seam, top, span, Axis, Half);
        Body(c, seam, top, span, Axis, Half);
        BackSpikes(c, seam, top, span, Axis, Half);
        Legs(c, seam, top, span, Axis, Half);
        Head(c, seam, top, span, Axis, Half);
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

            // Shaded across the body the same way the hull in the backdrop is shaded across its
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

                // Hide is not smooth. Keyed off position so it stays put between bakes.
                var grain = PixelCanvas.Hash((int)(t * 400f), (int)(n * 24f));
                c.Px(cx + s, y, Mix(col, ShellDeep, grain * 0.20f), 1f);
            }
        }
    }

    // A row of small back spikes down the lit flank of the torso - the one feature that reads as
    // "reptile" rather than "worm" at this size, echoing the reference's own spiked dorsal ridge.
    private static void BackSpikes(PixelCanvas c, float seam, float top, float span,
                                   Func<float, float, float> axis, Func<float, float> half)
    {
        for (var k = 0; k < 6; k++)
        {
            var t = 0.10f + k * 0.10f;
            var w = half(t);
            if (w < 1.5f)
                continue;
            var baseX = axis(seam, t) - w * 0.78f;
            var baseY = top + t * span;
            var spikeLen = 4.5f + w * 0.28f;
            var tipX = baseX - spikeLen * 0.55f;
            var tipY = baseY - spikeLen * 0.85f;
            c.Line(baseX, baseY, tipX, tipY, ShellDeep, 0.9f);
            c.Line(baseX + 0.9f, baseY, tipX + 0.9f, tipY, ShellLit, 0.32f);
        }
    }

    // Two pairs of splayed, clawed legs - what actually turns the silhouette into an animal rather
    // than a fish or a slug. Each is a two-segment capsule ending in a small fan of claw lines.
    private static void Legs(PixelCanvas c, float seam, float top, float span,
                             Func<float, float, float> axis, Func<float, float> half)
    {
        DrawLeg(c, seam, top, span, axis, half, 0.34f, -1f);
        DrawLeg(c, seam, top, span, axis, half, 0.36f, 1f);
        DrawLeg(c, seam, top, span, axis, half, 0.58f, -1f);
        DrawLeg(c, seam, top, span, axis, half, 0.60f, 1f);
    }

    private static void DrawLeg(PixelCanvas c, float seam, float top, float span,
                                Func<float, float, float> axis, Func<float, float> half, float t, float side)
    {
        var w = half(t);
        var originX = axis(seam, t) + w * side * 0.85f;
        var originY = top + t * span;
        var reach = 8f + w * 0.55f;
        var kneeX = originX + side * reach * 0.5f;
        var kneeY = originY + reach * 0.35f;
        var footX = originX + side * reach * 0.95f;
        var footY = originY + reach * 1.05f;

        ThickLine(c, originX, originY, kneeX, kneeY, 2.4f, ShellDeep, 0.92f);
        ThickLine(c, kneeX, kneeY, footX, footY, 1.7f, ShellDeep, 0.92f);
        c.Px(originX - side * 0.6f, originY, ShellLit, 0.28f);

        // Three short claws fanning out from the foot.
        var legAngle = MathF.Atan2(footY - kneeY, footX - kneeX);
        for (var k = -1; k <= 1; k++)
        {
            var clawAngle = legAngle + k * 0.5f;
            var clawX = footX + MathF.Cos(clawAngle) * 3.2f;
            var clawY = footY + MathF.Sin(clawAngle) * 3.2f;
            c.Line(footX, footY, clawX, clawY, ShellDeep, 0.85f);
        }
    }

    // A short jaw line and a single back-swept horn - just enough to say "head", not "blob".
    private static void Head(PixelCanvas c, float seam, float top, float span,
                             Func<float, float, float> axis, Func<float, float> half)
    {
        const float t = 0.05f;
        var cx = axis(seam, t);
        var cy = top + t * span;
        var w = half(t);

        c.Line(cx - w * 0.3f, cy + 1.5f, cx + w * 1.3f, cy + 4f, ShellDeep, 0.8f);
        c.Line(cx - w * 0.6f, cy - 1f, cx - w * 2.4f, cy - 7f, ShellDeep, 0.9f);
        c.Line(cx - w * 0.3f, cy - 1.5f, cx - w * 1.9f, cy - 6.4f, ShellLit, 0.3f);
    }

    // Draws a smooth capsule between two points by stamping discs along it - enough width control
    // for a leg segment without needing a real stroked-polygon primitive.
    private static void ThickLine(PixelCanvas c, float x0, float y0, float x1, float y1, float width, Color color, float a)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f)
        {
            c.Disc(x0, y0, width * 0.5f, color, a);
            return;
        }
        var steps = (int)(len * 1.5f) + 1;
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            c.Disc(x0 + dx * t, y0 + dy * t, width * 0.5f, color, a);
        }
    }
}
