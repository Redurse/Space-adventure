using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// The painted half of the backdrop: everything that is not the drawn art. Split out of
// MenuBackdropArt.cs so that file stays a readable list of what goes down in what order.
//
// Every size in here is in the 573x373 the canvas is authored at. There is no scale factor: the
// numbers were doubled once, when the canvas moved up from a mistaken 286x186, and carrying a
// multiplier around afterwards would only be an invitation to forget it in one place.
public static partial class MenuBackdropArt
{
    private static void PaintVoid(PixelCanvas c)
    {
        // Fractionally less black up and to the left, where the light is coming from. Far too small
        // a gradient to see as a gradient - it just stops the empty half of the frame reading as a
        // hole punched in the screen.
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var lean = 1f - (x / (float)Width * 0.55f + y / (float)Height * 0.45f);
            c.Px(x, y, Mix(Void, Nebula, 0.03f + 0.09f * lean), 1f);
        }
    }

    private static void PaintFarWall(PixelCanvas c)
    {
        // Big overlapping near-transparent discs. Individually invisible; together they are a cloud
        // bank with no edge anywhere, which is what far away looks like. There have to be a lot of
        // them and they have to be large - with fewer and smaller, each one's own edge shows and the
        // wall turns into a field of soft circles.
        for (var i = 0; i < 130; i++)
        {
            var x = PixelCanvas.Hash(i, 11) * Width;
            var y = PixelCanvas.Hash(i, 12) * Height * 0.92f;
            var r = 60f + PixelCanvas.Hash(i, 13) * 92f;
            var lean = 1f - (x / Width * 0.60f + y / Height * 0.40f);
            c.Disc(x, y, r, Mix(Nebula, Haze, PixelCanvas.Hash(i, 14)), 0.009f + 0.019f * lean);
        }

        // Faint vertical striations. The far wall needs some structure or it turns into a gradient,
        // but any structure with contrast in it would jump forward out of the distance.
        for (var i = 0; i < 22; i++)
        {
            var x = PixelCanvas.Hash(i, 21) * Width;
            var top = PixelCanvas.Hash(i, 22) * Height * 0.5f;
            var h = 60f + PixelCanvas.Hash(i, 23) * 180f;
            var w = 2f + PixelCanvas.Hash(i, 24) * 6f;
            c.Rect(x, top, w, h, Nebula, 0.035f);
        }
    }

    private static void PaintStars(PixelCanvas c)
    {
        for (var i = 0; i < 220; i++)
        {
            var x = PixelCanvas.Hash(i, 31) * Width;
            var y = PixelCanvas.Hash(i, 32) * Height;
            var b = PixelCanvas.Hash(i, 33);
            c.Px(x, y, Mix(Haze, Color.White, b * 0.7f), 0.10f + b * 0.26f);
        }
    }

    private static void PaintLightShafts(PixelCanvas c)
    {
        // Four soft cones fanning down out of the star, thrown by whatever is drifting in front of
        // it. Alpha falls off along the length so they fade before reaching anything solid and never
        // announce where they stop. They were aimed from the top-left corner first, which is the one
        // part of the frame the vignette crushes hardest, so none of them survived to be seen.
        for (var s = 0; s < 4; s++)
        {
            var angle = 1.15f + s * 0.30f + PixelCanvas.Hash(s, 41) * 0.20f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var perp = new Vector2(-dir.Y, dir.X);
            var reach = 220f + PixelCanvas.Hash(s, 43) * 160f;
            var spread = 10f + PixelCanvas.Hash(s, 42) * 14f;
            var steps = (int)reach;
            for (var i = 0; i < steps; i++)
            {
                var t = i / (float)steps;
                var at = Sun + dir * (t * reach);
                var w = spread * (0.4f + t * 1.4f);
                for (var k = -w; k <= w; k += 0.5f)
                    c.Px(at.X + perp.X * k, at.Y + perp.Y * k,
                         Haze, 0.055f * (1f - MathF.Abs(k) / w) * (1f - t) * (1f - t));
            }
        }
    }

    private static void PaintDebrisField(PixelCanvas c)
    {
        // The plane the drawn art has nothing in. Distance is spent on contrast, not on brightness:
        // the far ones barely separate from the wall behind them, the near ones get a lit cap and a
        // shaded belly and become lumps. Painted the other way round first, with the far rocks the
        // brightest things in the frame - they read as smudges on the lens.
        for (var i = 0; i < 16; i++)
        {
            var depth = PixelCanvas.Hash(i, 51);                 // 0 far, 1 near
            var x = 48f + PixelCanvas.Hash(i, 52) * 336f;
            var y = 56f + PixelCanvas.Hash(i, 53) * 224f;
            var r = 5f + depth * depth * 22f;
            var body = Mix(Nebula, Rock, 0.35f + depth * 0.45f);
            Boulder(c, x, y, r, body, 0.20f + depth * 0.74f, i);
            c.Disc(x - r * 0.44f, y - r * 0.46f, r * 0.44f, Mix(body, Color.White, 0.24f), 0.05f + 0.20f * depth * depth);
            c.Disc(x + r * 0.36f, y + r * 0.42f, r * 0.48f, Void, 0.05f + 0.25f * depth);
        }
    }

    // A rock rather than a circle: a disc with smaller ones welded around its rim, so no two come
    // out the same shape and none of them are round.
    private static void Boulder(PixelCanvas c, float x, float y, float r, Color body, float a, int seed)
    {
        c.Disc(x, y, r, body, a);
        for (var k = 0; k < 4; k++)
        {
            var ang = PixelCanvas.Hash(seed, 60 + k) * MathF.Tau;
            var d = r * (0.50f + PixelCanvas.Hash(seed, 70 + k) * 0.70f);
            c.Disc(x + MathF.Cos(ang) * d, y + MathF.Sin(ang) * d,
                   r * (0.28f + PixelCanvas.Hash(seed, 80 + k) * 0.34f), body, a);
        }
    }

    private static void PaintDepthHaze(PixelCanvas c)
    {
        // One wash over everything painted so far and nothing painted after it. That is the whole of
        // atmospheric perspective: the far planes get air in front of them, the ship and the near
        // crag do not, and the eye sorts them without being told.
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var lean = 1f - (x / (float)Width * 0.62f + y / (float)Height * 0.38f);
            c.Px(x, y, Haze, 0.012f + 0.034f * lean);
        }
    }

    // Kept clear of the drawn ship's own corridor, and of the right-hand third where the menu draws
    // its live planet over all of this.
    private static readonly Vector2[] Escorts = { new(116f, 112f), new(172f, 72f), new(80f, 148f) };

    private static void PaintEscorts(PixelCanvas c)
    {
        // Three specks on their way somewhere. They exist to be small: with nothing in the frame
        // whose size is obvious, a hull is whatever size the viewer assumes, which is never big.
        for (var i = 0; i < Escorts.Length; i++)
        {
            var p = Escorts[i];
            var len = 10f + i * 3f;
            c.Line(p.X, p.Y, p.X + len, p.Y - len * 0.42f, Mix(Haze, Color.White, 0.30f), 0.85f);
            c.Line(p.X, p.Y + 2f, p.X + len * 0.7f, p.Y - len * 0.30f, Void, 0.55f);
            c.Px(p.X + len, p.Y - len * 0.42f, Mix(Ember, Color.White, 0.4f), 0.85f);
        }
    }

    // Where the foreground crag sits. Pulled out so the crest pass traces exactly the rocks the body
    // pass drew. It is kept low and to the left: the live engine plume burns out past the bottom-left
    // corner, and the title and traffic ticker sit over the bottom right.
    private static (float X, float Y, float R) Crag(int i)
    {
        var t = i / 25f;
        return (-20f + t * 236f,
                Height + 52f - MathF.Sin(t * 2.4f) * 60f - PixelCanvas.Hash(i, 91) * 18f,
                40f + PixelCanvas.Hash(i, 92) * 32f);
    }

    private static void PaintForeground(PixelCanvas c)
    {
        // Near-black, no internal detail, and running off two edges of the frame. All three are
        // deliberate: a foreground that resolves into detail stops being foreground, and one that
        // fits inside the frame reads as an object floating in the middle distance instead.
        for (var i = 0; i < 26; i++)
        {
            var (x, y, r) = Crag(i);
            c.Disc(x, y, r, Mix(Void, Rock, 0.25f), 1f);
        }

        // A wedge biting in from the top-left, so the eye is boxed in on two sides and the ship sits
        // in the gap between them.
        for (var i = 0; i < 12; i++)
        {
            var t = i / 11f;
            c.Disc(-28f + t * 124f, -36f + t * 68f, 44f - t * 16f, Mix(Void, Rock, 0.18f), 1f);
        }

        PaintCragCrest(c);
    }

    // The faintest light catching the crest. Without it the crag is a hole in the picture; with it it
    // is a rock in the dark, and it costs about two hundred pixels.
    //
    // Traced along the actual silhouette of the union of discs - a column at a time, taking the
    // highest one that covers it - rather than dabbed on top of each disc, which is what the first
    // attempt did and it came out looking like pearls glued to the rock. Broken up at random too: an
    // unbroken bright line around a shape reads as an outline, not as light.
    private static void PaintCragCrest(PixelCanvas c)
    {
        for (var x = 0; x < 264; x++)
        {
            var top = float.MaxValue;
            for (var i = 0; i < 26; i++)
            {
                var (cx, cy, r) = Crag(i);
                var dx = x - cx;
                if (MathF.Abs(dx) >= r)
                    continue;
                var y = cy - MathF.Sqrt(r * r - dx * dx);
                if (y < top)
                    top = y;
            }
            if (top > Height - 1f || top < 0f)
                continue;

            var lit = PixelCanvas.Hash(x, 111);
            if (lit < 0.52f)
                continue;
            c.Px(x, top, Mix(Haze, Color.White, 0.10f), 0.10f + lit * 0.16f);
            c.Px(x, top + 1f, Mix(Rock, Haze, 0.60f), 0.22f * lit);
        }
    }

    private static void PaintDust(PixelCanvas c)
    {
        // In front of everything, at four different sizes. The spread is the point - a field of
        // identical specks is a texture, a field of mixed ones is a volume with the viewer inside it.
        for (var i = 0; i < 44; i++)
        {
            var x = PixelCanvas.Hash(i, 101) * Width;
            var y = PixelCanvas.Hash(i, 102) * Height;
            var near = PixelCanvas.Hash(i, 103);
            c.Disc(x, y, 1f + near * near * 3f, Mix(Haze, Color.White, 0.45f), 0.06f + near * 0.20f);
        }
    }

    private static void PaintVignette(PixelCanvas c)
    {
        var cx = Width * 0.52f;
        var cy = Height * 0.46f;
        var max = MathF.Sqrt(cx * cx + cy * cy);
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / max;
            if (d > 0.36f)
                c.Px(x, y, Void, MathF.Min(0.88f, (d - 0.36f) * 1.9f));
        }
    }
}
