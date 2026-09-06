using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

/// <summary>The deck underfoot: the standard plating, plus the two compartments that get their own.
/// </summary>
///
/// The floor used to be one 48px tile stamped everywhere with a one-pixel grid drawn over it. That
/// reads as wallpaper. A repeated stamp is exactly what the eye picks out of a large flat area, and
/// a hairline grid is not a seam - it has no depth, so it looks printed on rather than cut in.
///
/// Three cheap things fix it:
///
///   * real seams, recessed with a lit lip beside them. That one extra line is the whole difference
///     between panels bolted to a ship and a printed surface;
///   * six plate variants and a per-cell tone jitter, indexed in ship space so the pattern does not
///     crawl when the camera moves - the same trick the hull plating already uses;
///   * a different field inside the frame for the two rooms that earn one.
///
/// The frame is deliberately identical across all three - same seams, same bolts, same wear - so
/// they still read as one ship built by one yard. Only what is inside it changes.
public static class DeckPlates
{
    public enum Deck { Standard, Gunnery, Reactor }

    public const int TileSize = TileTextures.FloorTileSize;

    // Six plain panels already kill the obvious stamp, but they are all the same *kind* of panel,
    // so a large room still reads as evenly textured nothing. What a real deck has is the occasional
    // thing that is not floor: a hatch you could lift, a drain, a patch where something was cut out
    // and welded back, a cable run under a cover.
    private const int PlainVariants = 6;
    private const int FeatureVariants = 4;
    private const int Variants = PlainVariants + FeatureVariants;

    /// <summary>Which deck a compartment stands on. Machinery spaces share the reactor's grating:
    /// an engine room is the same kind of place, and giving it the walking deck instead would say
    /// the two are unrelated.</summary>
    public static Deck For(string roomId) => roomId switch
    {
        var id when id.Contains("armory") || id.Contains("weapon") || id.Contains("gun") => Deck.Gunnery,
        var id when id.Contains("reactor") || id.Contains("engine") => Deck.Reactor,
        _ => Deck.Standard,
    };

    // A separate seed from the hull's own variant hash. Sharing it would line the floor pattern up
    // with the plating overhead, and two independent-looking surfaces repeating in lockstep is more
    // noticeable than either repeat on its own.
    /// <summary>Plain most of the time; a feature about one cell in eight. Rarity is the whole
    /// point - a feature on every second panel stops being a feature and becomes a pattern, which is
    /// exactly what this is trying to get away from.</summary>
    public static int VariantAt(int cellX, int cellY)
    {
        var roll = Hash(cellX, cellY, 4919);
        if (roll > 0.875f)
            return PlainVariants + (int)(Hash(cellX, cellY, 3313) * FeatureVariants) % FeatureVariants;
        return (int)(roll / 0.875f * PlainVariants) % PlainVariants;
    }

    public static float ToneAt(int cellX, int cellY) => 0.95f + Hash(cellX, cellY, 7717) * 0.10f;

    public static Texture2D[] Create(GraphicsDevice device, Deck deck)
    {
        var plates = new Texture2D[Variants];
        for (var v = 0; v < Variants; v++)
        {
            var data = new Color[TileSize * TileSize];
            var plain = v < PlainVariants ? v : v - PlainVariants;
            for (var y = 0; y < TileSize; y++)
            for (var x = 0; x < TileSize; x++)
                data[y * TileSize + x] = Pixel(deck, plain, x, y);
            if (v >= PlainVariants)
                Feature(data, v - PlainVariants);
            var texture = new Texture2D(device, TileSize, TileSize);
            texture.SetData(data);
            plates[v] = texture;
        }
        return plates;
    }

    /// <summary>Tiles a room with the deck's plates. cellOrigin is the ship's own origin, so which
    /// plate lands where is a property of the ship rather than of the camera.</summary>
    public static void DrawTiled(SpriteBatch spriteBatch, Texture2D[] plates, Rectangle rect, Color tint,
        Point cellOrigin)
    {
        for (var y = rect.Y; y < rect.Bottom; y += TileSize)
        {
            var h = Math.Min(TileSize, rect.Bottom - y);
            for (var x = rect.X; x < rect.Right; x += TileSize)
            {
                var w = Math.Min(TileSize, rect.Right - x);
                var cellX = (int)MathF.Floor((x - cellOrigin.X) / (float)TileSize);
                var cellY = (int)MathF.Floor((y - cellOrigin.Y) / (float)TileSize);
                var tone = ToneAt(cellX, cellY);
                var shaded = new Color((int)(tint.R * tone), (int)(tint.G * tone), (int)(tint.B * tone), tint.A);
                spriteBatch.Draw(plates[VariantAt(cellX, cellY)], new Rectangle(x, y, w, h),
                    new Rectangle(0, 0, w, h), shaded);
            }
        }
    }

    /// <summary>A soft round smudge, baked once. White with a radial alpha, so drawing it tinted
    /// black simply takes light off whatever is underneath.</summary>
    public static Texture2D CreateGrime(GraphicsDevice device)
    {
        const int size = 64;
        var data = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = (x - size / 2f + 0.5f) / (size / 2f);
            var dy = (y - size / 2f + 0.5f) / (size / 2f);
            var d = MathF.Sqrt(dx * dx + dy * dy);
            var a = d >= 1f ? 0f : (1f - d) * (1f - d);
            // Premultiplied, which is what SpriteBatch's default blending expects.
            data[y * size + x] = new Color(a, a, a, a);
        }
        var texture = new Texture2D(device, size, size);
        texture.SetData(data);
        return texture;
    }

    /// <summary>Dirt pooled across the deck, at a scale larger than one plate.
    ///
    /// This is the part no amount of work inside a tile can do. Six variants and four feature panels
    /// break up the metre-by-metre reading of the floor, but a big room still repeats at the tile
    /// grid, and the only thing that hides a grid is something that ignores it. Seeded from the
    /// compartment's own id, so a room is always dirty in the same places.</summary>
    public static void DrawGrime(SpriteBatch spriteBatch, Texture2D grime, Rectangle rect, string roomId)
    {
        var seed = 0;
        foreach (var ch in roomId)
            seed = unchecked(seed * 31 + ch);

        var pools = 4 + (int)(Hash(seed, 0, 13) * 4);
        for (var i = 0; i < pools; i++)
        {
            var span = (int)((0.35f + Hash(seed, i, 17) * 0.65f) * MathF.Min(rect.Width, rect.Height));
            if (span < 24)
                continue;
            var x = rect.X + (int)(Hash(seed, i, 23) * rect.Width) - span / 2;
            var y = rect.Y + (int)(Hash(seed, i, 29) * rect.Height) - span / 2;
            spriteBatch.Draw(grime, new Rectangle(x, y, span, span), Color.Black * (0.10f + Hash(seed, i, 31) * 0.09f));
        }
    }

    // ---------------------------------------------------------------- the feature panels

    // Applied over a finished plain plate as a brightness multiplier, so a feature keeps the deck's
    // own grain, wear and colour underneath it and only changes the relief. Painting one from
    // scratch would put a patch of clean metal in the middle of a worn floor.
    private static void Feature(Color[] data, int kind)
    {
        // Which of the tile's four 24px panels it occupies, so features do not always land in the
        // same corner of the room.
        var ox = kind % 2 * 24;
        var oy = kind / 2 % 2 * 24;

        void Put(float fx, float fy, float mul)
        {
            int x = ox + (int)MathF.Round(fx), y = oy + (int)MathF.Round(fy);
            if (x < 0 || y < 0 || x >= TileSize || y >= TileSize)
                return;
            var c = data[y * TileSize + x];
            data[y * TileSize + x] = new Color(
                Math.Min(255, (int)(c.R * mul)), Math.Min(255, (int)(c.G * mul)),
                Math.Min(255, (int)(c.B * mul)));
        }

        void Box(float x, float y, float w, float h, float mul)
        {
            for (var yy = (int)y; yy < (int)(y + h); yy++)
            for (var xx = (int)x; xx < (int)(x + w); xx++)
                Put(xx, yy, mul);
        }

        switch (kind)
        {
            case 0:
                // Access hatch. A hatch is a hole with a lid on it, so the gap around it has to be
                // dark enough to read as a gap - drawn gently it came out as a faint dashed square
                // indistinguishable from the welded patch.
                Box(2, 2, 20, 20, 0.52f);
                Box(3, 3, 18, 18, 1.12f);
                Box(3, 3, 18, 1, 1.50f);
                Box(3, 20, 18, 1, 0.50f);
                Box(3, 3, 1, 18, 1.35f);
                Box(20, 3, 1, 18, 0.55f);
                foreach (var (lx, ly) in new[] { (5, 5), (16, 5), (5, 16), (16, 16) })
                {
                    Box(lx, ly, 3, 3, 0.42f);
                    Box(lx, ly, 3, 1, 1.55f);
                }
                Box(8, 11, 9, 4, 0.34f);          // the recessed lift handle
                Box(8, 11, 9, 1, 1.45f);
                Box(9, 12, 7, 2, 0.85f);
                foreach (var hy in new[] { 6, 17 })   // hinges, so which way it opens is obvious
                {
                    Box(1, hy, 3, 3, 0.60f);
                    Box(1, hy, 3, 1, 1.40f);
                }
                break;

            case 1:
                // Drain. Round, so it is instantly not-a-panel, and dark down the middle because
                // there is somewhere for it to go.
                for (var yy = 0; yy < 24; yy++)
                for (var xx = 0; xx < 24; xx++)
                {
                    var dd = MathF.Sqrt((xx - 12) * (xx - 12) + (yy - 12) * (yy - 12));
                    if (dd < 8.5f) Put(xx, yy, dd > 7.2f ? 0.55f : 0.72f);
                    else if (dd < 9.6f) Put(xx, yy, 1.18f);
                }
                for (var i = -2; i <= 2; i++)
                    Box(12 + i * 3 - 1, 6, 2, 12, 0.34f);
                break;

            case 2:
                // A repair: plate cut in and welded round, deliberately not square to the panel it
                // sits in and darker than the deck around it. New metal in an old floor does not
                // match, and damage that lines up with the grid does not read as damage.
                Box(3, 5, 17, 14, 0.86f);
                for (var i = 0; i < 17; i++)
                {
                    var wob = MathF.Sin(i * 1.7f) * 0.7f;
                    Put(3 + i, 5 + wob, 1.55f);
                    Put(3 + i, 6 + wob, 1.20f);
                    Put(3 + i, 18 + wob, 0.45f);
                }
                for (var i = 0; i < 14; i++)
                {
                    var wob = MathF.Sin(i * 2.1f) * 0.7f;
                    Put(3 + wob, 5 + i, 1.45f);
                    Put(19 + wob, 5 + i, 0.48f);
                }
                foreach (var (bx, by) in new[] { (6, 8), (16, 8), (6, 16), (16, 16) })
                {
                    Put(bx, by, 0.45f);
                    Put(bx, by - 1, 1.45f);
                }
                break;

            default:
                // Cable trunk cover, running the full width of the tile - so at least one feature
                // crosses the panel grid instead of respecting it.
                for (var xx = -ox; xx < TileSize - ox; xx++)
                for (var yy = 9; yy < 20; yy++)
                    Put(xx, yy, yy is 9 or 13 or 17 ? 1.34f : yy is 12 or 16 or 19 ? 0.68f : 0.96f);
                foreach (var xx in new[] { 3, 20, 27, 44 })
                foreach (var yy in new[] { 10, 18 })
                    Put(xx - ox, yy, 0.42f);
                break;
        }
    }

    // ---------------------------------------------------------------- the plates

    // The tint ShipRenderer multiplies these by. Baked in here so the plates carry their own colour
    // and a warm patch can actually come out warm rather than being flattened by a grey multiply.
    private static readonly Color Tint = new(35, 40, 47);

    private static float Hash(int x, int y, int s)
    {
        var n = unchecked(x * 374761393 + y * 668265263 + s * 1442695041);
        n = unchecked((n ^ (n >> 13)) * 1274126177);
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // Value noise on a lattice that wraps at `period`, which is what keeps the tile seamless.
    private static float Noise(float fx, float fy, int period, int seed)
    {
        int x0 = (int)MathF.Floor(fx), y0 = (int)MathF.Floor(fy);
        var tx = fx - x0;
        var ty = fy - y0;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);

        float Corner(int ix, int iy) => Hash(((ix % period) + period) % period, ((iy % period) + period) % period, seed);

        var a = Corner(x0, y0) + (Corner(x0 + 1, y0) - Corner(x0, y0)) * tx;
        var b = Corner(x0, y0 + 1) + (Corner(x0 + 1, y0 + 1) - Corner(x0, y0 + 1)) * tx;
        return a + (b - a) * ty;
    }

    private static float Grain(int x, int y, int seed) =>
        (Noise(x / 6f, y / 6f, 8, seed) - 0.5f) * 0.05f +
        (Noise(x / 2f, y / 2f, 24, seed + 1) - 0.5f) * 0.025f;

    private static Color Pixel(Deck deck, int variant, int x, int y)
    {
        var v = Height(deck, variant, x, y, out var warm);
        return new Color(
            Math.Min(255, (int)(Tint.R * v * warm)),
            Math.Min(255, (int)(Tint.G * v * (1f + (warm - 1f) * 0.45f))),
            Math.Min(255, (int)(Tint.B * v)));
    }

    /// <summary>The plate as a height field, before any colour is applied. This is what the deck
    /// actually looks like in relief, so it is also what the floor normal map has to be built from -
    /// deriving normals from some other surface would light the floor as though it were a different
    /// floor.</summary>
    internal static float Height(Deck deck, int variant, int x, int y) => Height(deck, variant, x, y, out _);

    private static float Height(Deck deck, int variant, int x, int y, out float warm)
    {
        var v = 0.93f + Grain(x, y, variant * 17 + 3);
        warm = 1f;

        switch (deck)
        {
            case Deck.Gunnery:
            {
                // Raised studs. A floor people carry shells across is never smooth, and round bumps
                // in a grid read as nothing else - the diamond tread this started as came out as
                // diagonal dashes barely distinguishable from the standard deck's own crest.
                var row = y / 14;
                var ox = (x + (row % 2 == 0 ? 0 : 7)) % 14 - 7;
                var oy = y % 14 - 7;
                var d = MathF.Sqrt(ox * ox + oy * oy);
                if (d <= 4.2f)
                {
                    // Lit on the side facing the light, shadowed on the other. A stud needs a
                    // direction or it reads as a stain rather than as something proud of the deck.
                    v += 0.055f * (-(ox + oy) / 8.4f) + 0.02f;
                    if (d > 3.4f)
                        v -= 0.045f;
                }
                break;
            }
            case Deck.Reactor:
            {
                // Open grating over the machinery below.
                if (y % 8 < 5)
                {
                    if (y % 8 == 0) v += 0.055f;        // the lit top edge of each bar
                    if (y % 8 == 4) v -= 0.075f;        // its shadow falling into the gap
                }
                else
                {
                    v -= 0.22f;                         // the gap: there is a hold under this floor
                }
                if (x % 24 < 2)
                    v += 0.05f;                         // cross ribs carrying the bars

                // Heat, kept faint. Run up hard it stops reading as hot metal and starts reading as
                // rust and mud, which is a different and much worse thing to have on a floor.
                var stain = Noise(x / 11f, y / 11f, 5, 91 + variant);
                if (stain > 0.62f)
                {
                    warm = 1f + (stain - 0.62f) * 0.42f;
                    v -= (stain - 0.62f) * 0.10f;
                }
                break;
            }
            default:
            {
                // The diagonal crest this ship's deck already had - kept, because the point of the
                // pass is a better-made version of this floor, not a different floor.
                var diagonal = (x + y) % 12;
                if (diagonal < 2) v += 0.040f;
                else if (diagonal < 4) v -= 0.050f;
                else if (diagonal is 8 or 9) v += 0.015f;
                break;
            }
        }

        // Seams every 24, so a tile is four panels - the cadence the deck already had, but cut into
        // the metal instead of drawn on top of it.
        int sx = x % 24, sy = y % 24;
        if (sx == 0 || sy == 0)
            v -= 0.20f;
        else if (sx == 1 || sy == 1)
            v += 0.07f;

        // Bolts at the corners of each panel.
        int bx = Math.Min(sx, 23 - sx), by = Math.Min(sy, 23 - sy);
        if (bx <= 3 && by <= 3)
        {
            var d = MathF.Sqrt((bx - 2) * (bx - 2) + (by - 2) * (by - 2));
            if (d < 1.9f)
            {
                v -= 0.12f;
                if (by < 2)
                    v += 0.20f;
            }
        }

        // Wear, different on every variant - which is what stops six plates reading as one plate.
        // Measured with wrap-around distance so a scuff that runs off one edge comes back on the
        // other rather than being cut in half at the seam.
        for (var i = 0; i < 3; i++)
        {
            var wx = Hash(variant, i, 5) * TileSize;
            var wy = Hash(variant, i, 7) * TileSize;
            var r = 5f + Hash(variant, i, 11) * 9f;
            var dx = MathF.Min(MathF.Abs(x - wx), TileSize - MathF.Abs(x - wx));
            var dy = MathF.Min(MathF.Abs(y - wy), TileSize - MathF.Abs(y - wy));
            var d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < r)
                v -= (1f - d / r) * 0.055f;
        }

        return MathHelper.Clamp(v, 0.30f, 1.15f);
    }
}
