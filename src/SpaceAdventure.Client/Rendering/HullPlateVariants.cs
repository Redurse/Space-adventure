using System;
using Microsoft.Xna.Framework;

namespace SpaceAdventure.Client.Rendering;

// Why the hull looked flat, and what is done about it here.
//
// It was not a shortage of detail inside the tile - the tile already had grain, a bevel, a riveted
// seam, scratches and grime. It was that there was exactly one of it. A single 64-pixel plate was
// repeated across every compartment of every ship, so the eye, which finds repeats faster than it
// finds anything else, read the whole hull as one texture rather than as plating.
//
// So the fix is variation between plates, not more inside them:
//
//   * several tiles instead of one, chosen by the plate's position in the ship, so the same square
//     always looks the same but its neighbour does not
//   * a small tone shift per plate, which is the cheapest and strongest of the three - metal cut from
//     different stock and weathered differently is never quite the same colour
//   * a few plates carrying something the others do not: a stencil, a welded patch. Rare on purpose.
//     A marking on every plate is a pattern; a marking on one plate in fifteen is a repair.
//
// The marks themselves are laid as strokes, the same way the menu's panel is painted - but here they
// have to wrap at the tile edge, because this one does tile. Wrapping the offset into the tile turns
// the square into a torus, and a stroke that runs off one side comes back on the other.
public static class HullPlateVariants
{
    // Six ordinary plates and two with something on them. More ordinary variants would cost almost
    // nothing, but four is already past the point where the eye can hold the set and spot a repeat.
    public const int Count = 8;
    private const int StencilVariant = 6;
    private const int PatchVariant = 7;

    private static uint Hash(int a, int b, int seed)
    {
        var n = (uint)(a * 374761393 + b * 668265263 + seed * 362437);
        n ^= n >> 13;
        n *= 1274126177u;
        return n ^ (n >> 16);
    }

    private static float Rand(int a, int b, int seed) => (Hash(a, b, seed) & 0xffff) / 65535f;

    /// <summary>Which plate goes at this square of the ship. Stable in ship space, so the pattern does
    /// not crawl when the camera moves.</summary>
    public static int VariantAt(int cellX, int cellY)
    {
        var roll = Rand(cellX, cellY, 77);
        if (roll > 0.965f)
            return PatchVariant;
        if (roll > 0.915f)
            return StencilVariant;
        return (int)(Rand(cellX, cellY, 91) * 5.999f);
    }

    /// <summary>A small brightness shift per plate. Subtle deliberately: at any real strength this
    /// stops reading as stock variation and starts reading as a chequerboard.</summary>
    public static float ToneAt(int cellX, int cellY) => 0.94f + Rand(cellX, cellY, 133) * 0.12f;

    /// <summary>Everything this variant adds on top of the shared plate recipe.</summary>
    public static float Extra(int x, int y, int size, int variant)
    {
        var value = Strokes(x, y, size, variant);
        if (variant == StencilVariant)
            value += Stencil(x, y, size);
        else if (variant == PatchVariant)
            value += Patch(x, y, size);
        return value;
    }

    // Wrapping brush marks.
    //
    // Two constraints pull against each other here. The plate's base value is 0.94 and it is tinted
    // by a colour around a fifth of full brightness, so a mark worth a few hundredths of the range
    // arrives on screen as two levels out of 255 - invisible, which is what the first attempt was.
    // But the last time this hull got strong low-frequency wear it read as dented and mangy rather
    // than used. The way out of both is direction: marks are deep enough to see, and they are all
    // strokes, which describe wear along a surface instead of holes in it.
    //
    // Nearly all of them darken. The value clamps at 1.0 and sits at 0.94, so there is a twentieth of
    // the range above and two thirds of it below - anything that has to be seen has to go down.
    private static float Strokes(int x, int y, int size, int variant)
    {
        const int count = 9;
        var total = 0f;
        for (var i = 0; i < count; i++)
        {
            var seed = variant * 131 + i;
            var ox = Rand(i, 1, seed) * size;
            var oy = Rand(i, 2, seed) * size;

            // Mostly along the hull, a few across it - the same "families, not random angles" rule
            // the menu panel uses. An even spread of directions reads as scribble at any scale.
            var angle = Rand(i, 3, seed) < 0.72f
                ? (Rand(i, 4, seed) - 0.5f) * 0.5f
                : MathF.PI / 2f + (Rand(i, 5, seed) - 0.5f) * 0.5f;
            var dx = MathF.Cos(angle);
            var dy = MathF.Sin(angle);

            var halfLength = size * (0.18f + Rand(i, 6, seed) * 0.30f);
            var halfWidth = 1.4f + Rand(i, 7, seed) * 3.6f;
            var amplitude = Rand(i, 8, seed) > 0.80f
                ? 0.02f + Rand(i, 9, seed) * 0.035f
                : -(0.035f + Rand(i, 9, seed) * 0.105f);

            // Wrap the offset into the tile, which makes the square a torus and the stroke seamless.
            var px = Wrap(x - ox, size);
            var py = Wrap(y - oy, size);
            var along = px * dx + py * dy;
            var across = -px * dy + py * dx;
            if (MathF.Abs(along) > halfLength || MathF.Abs(across) > halfWidth)
                continue;

            var taper = MathF.Min(1f, (1f - MathF.Abs(along) / halfLength) * 3.4f);
            var profile = MathF.Pow(1f - MathF.Abs(across) / halfWidth, 0.55f);
            // Loaded, then dry - the break-up is what separates a brush mark from an airbrushed one.
            var dry = Rand((int)(along * 0.7f), 0, seed + 17) > 0.30f ? 1f : 0.35f;
            total += amplitude * taper * profile * dry;
        }
        return total;
    }

    private static float Wrap(float v, int size)
    {
        v -= MathF.Floor(v / size) * size;
        return v > size / 2f ? v - size : v;
    }

    // A stencilled marking: a frame with two bars in it. Not letters - at sixty-four pixels a letter
    // is four pixels tall and comes out as a smudge, while a frame and two bars still reads as
    // something painted on deliberately.
    private static float Stencil(int x, int y, int size)
    {
        var left = size * 6 / 16;
        var top = size * 3 / 16;
        var w = size * 5 / 16;
        var h = size * 4 / 16;
        if (x < left || x >= left + w || y < top || y >= top + h)
            return 0f;

        var lx = x - left;
        var ly = y - top;
        // Dark paint, not light. There is no headroom above the base value, so a stencil that has to
        // be legible has to be sprayed in something darker than the metal - which is also what a
        // service marking on a light-grey hull actually is.
        var onFrame = lx == 0 || ly == 0 || lx == w - 1 || ly == h - 1;
        if (onFrame)
            return -0.20f;
        if (ly == h / 3 && lx > 2 && lx < w - 3)
            return -0.17f;
        if (ly == h * 2 / 3 && lx > 2 && lx < w - 5)
            return -0.17f;
        return -0.02f;
    }

    // A welded patch: a plate laid over a hole, brighter than what is around it because it has not
    // been out there as long, with a bead of weld round its edge.
    private static float Patch(int x, int y, int size)
    {
        var left = size * 2 / 16;
        var top = size * 8 / 16;
        var w = size * 7 / 16;
        var h = size * 5 / 16;
        if (x < left || x >= left + w || y < top || y >= top + h)
            return 0f;

        var lx = x - left;
        var ly = y - top;
        if (lx == 0 || ly == 0 || lx == w - 1 || ly == h - 1)
        {
            // The bead is lumpy, not a line: a weld run is a row of overlapping puddles and a clean
            // outline would read as a drawn rectangle. Mostly shadow with proud spots catching light,
            // which is the only place on this plate a positive value earns its scarce headroom.
            var along = lx == 0 || lx == w - 1 ? ly : lx;
            return along % 3 == 0 ? 0.05f : -0.22f;
        }
        return -0.055f;
    }
}
