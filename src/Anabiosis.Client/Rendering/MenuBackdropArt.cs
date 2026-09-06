using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

/// <summary>The main menu's art pane: the hand-drawn Textures/MenuBackdrop.png with a painted-in-code
/// scene built out behind it.</summary>
///
/// The drawn image has one plane in it - a ship and a star, and flat dark space everywhere else -
/// so it reads as a picture rather than as a place, however much detail went into the hull. What a
/// painted key art does about that is mostly not detail:
///   - three planes whose contrast falls with distance: a near-black foreground crag with no
///     internal detail at all, the ship in the middle, and a far wall that is nearly a flat fill;
///   - rock and dust at several sizes and brightnesses, because distance is read from how much air
///     is in front of a thing;
///   - two colours and a pinch of a third.
///
/// So all of that is painted here, and the drawn art is laid over the top of it.
///
/// The ship itself is emphatically NOT painted here. It was, for one iteration - a procedural lit
/// hull - and that was wrong twice over: it sat where the menu draws its live planet and vanished
/// behind it, and once moved it was still a generic shape standing in for authored art that has
/// engine bells, lit ports and panel modules in it. The drawn ship is the ship.
public static partial class MenuBackdropArt
{
    // Matches Textures/MenuBackdrop.png exactly, so the overlay lands pixel for pixel with no
    // resampling. It is not the 286x186 the draw-site comment claims - that number is wrong, and
    // baking at it threw away half the drawn art's resolution.
    public const int Width = 573;
    public const int Height = 373;

    // Everything painted here is mixed out of these five. Keeping the count this low is most of why
    // it reads as one picture - a backdrop starts looking cheap the moment a sixth hue turns up.
    private static readonly Color Void = new(6, 11, 14);
    private static readonly Color Nebula = new(20, 42, 48);
    private static readonly Color Haze = new(28, 62, 68);
    private static readonly Color Rock = new(14, 26, 30);
    private static readonly Color Ember = new(214, 122, 54);   // the only warm note in the frame

    // The star, at the same pane fractions Game1.MenuScene.cs feeds the planet shader (SunX/SunY).
    // Everything lit here is lit from there, so the painted layers, the drawn art and the planet all
    // agree about where the light is instead of each picking its own.
    private static readonly Vector2 Sun = new(0.30f * Width, 0.10f * Height);

    /// <summary>How far the drawn art is turned down before it is laid over the painted scene.</summary>
    ///
    /// This one number is the whole compositing rule. The drawn background has to land below the
    /// painted background or it wins the "lighter" test everywhere and covers the work; the drawn
    /// ship has to stay well above it. Turning it down also buys back headroom the menu's post chain
    /// spends - that pane is graded with high exposure and bloom, and art that looks right as a flat
    /// image comes out chalky on screen.
    private const float DrawnArtLevel = 0.70f;

    public static Texture2D Bake(GraphicsDevice graphics, Texture2D drawnArt)
    {
        var c = new PixelCanvas(Width, Height);

        PaintVoid(c);
        PaintFarWall(c);
        PaintStars(c);
        PaintLightShafts(c);
        PaintDebrisField(c);
        PaintDepthHaze(c);       // everything above this line is behind the air; nothing below it is
        PaintEscorts(c);
        LayDrawnArtOver(c, drawnArt);
        PaintForeground(c);      // the crag is in front of the ship, so it goes on after it
        PaintDust(c);
        PaintVignette(c);

        return c.ToTexture(graphics);
    }

    private static void LayDrawnArtOver(PixelCanvas c, Texture2D drawnArt)
    {
        var data = new Color[drawnArt.Width * drawnArt.Height];
        drawnArt.GetData(data);

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            // Nearest-sampled rather than assumed 1:1: the two are the same size today, and a
            // repainted .png at some other size should still land in the right place.
            var sx = drawnArt.Width == Width ? x : x * drawnArt.Width / Width;
            var sy = drawnArt.Height == Height ? y : y * drawnArt.Height / Height;
            var s = data[sy * drawnArt.Width + sx];
            c.Max(x, y, new Color(
                (int)(s.R * DrawnArtLevel),
                (int)(s.G * DrawnArtLevel),
                (int)(s.B * DrawnArtLevel)));
        }
    }

    private static Color Mix(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (int)(a.R + (b.R - a.R) * t),
            (int)(a.G + (b.G - a.G) * t),
            (int)(a.B + (b.B - a.B) * t));
    }
}
