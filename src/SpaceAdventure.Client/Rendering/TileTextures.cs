using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Small procedurally-generated tileable textures standing in for floor/wall material - this project
// has no image assets (see ShipRenderer.DrawFloorGrating's own comment), so instead of an artist's
// diamond-plate sprite this bakes the same idea (layered value noise, corner rivets, a diagonal
// tread ridge) into a Texture2D once at startup.
//
// Each texture is grayscale, averaging close to white, so the department/alarm colour a caller
// would otherwise have used for a flat spriteBatch.Draw(pixel, rect, color) fill can still be passed
// straight through as the tint in DrawTiled - the grain darkens and lightens around that colour
// instead of replacing it.
//
// Two things drive how the detail here is shaped. First, the noise wraps: a tile drawn next to
// itself has to have no seam, which per-pixel hashing on raw coordinates does not give you. Second,
// ScenePost's relief lighting reads the *gradient* of scene luminance and treats it as surface
// tilt, so a raised detail needs a lit flank and a shadowed one to read as raised. A single dark
// line is a scratch; a dark line beside a bright one is an edge with a height to it.
public static class TileTextures
{
    public const int FloorTileSize = 48; // one world unit at ShipRenderer.PixelsPerUnit
    public const int WallTileSize = 16;
    public const int HullTileSize = 64;
    public const int DeviceTileSize = 16;

    public static Texture2D CreateFloorPlate(GraphicsDevice device) => Build(device, FloorTileSize, FloorPixel);

    public static Texture2D CreateWallPlate(GraphicsDevice device) => Build(device, WallTileSize, WallPixel);

    public static Texture2D CreateHullPlate(GraphicsDevice device) => Build(device, HullTileSize, HullPixel);

    // The face of a machine rather than a floor: a fine, mostly horizontal tooth, no tread and no
    // rivets (ShipRenderer.DrawPanel puts its own at the corners of each panel, not per tile).
    public static Texture2D CreateDevicePlate(GraphicsDevice device) => Build(device, DeviceTileSize, DevicePixel);

    // The floor plate as a normal map rather than a brightness map. These are true normals: the
    // height field is the same FloorPixel the visible tile is built from, so the slope is known
    // exactly instead of being guessed from the finished picture the way ScenePost has to guess it
    // for everything else. Encoded the usual way, each axis from -1..1 into 0..1.
    public static Texture2D CreateFloorNormals(GraphicsDevice device)
    {
        const int size = FloorTileSize;
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                // Central differences, wrapped, so the normal map tiles exactly like the plate does.
                var dx = FloorPixel(Wrap(x + 1, size), y) - FloorPixel(Wrap(x - 1, size), y);
                var dy = FloorPixel(x, Wrap(y + 1, size)) - FloorPixel(x, Wrap(y - 1, size));
                // Strength, not a physical constant: the height field is a brightness in 0..1 over a
                // tile a couple of world units across, so the raw slope is far too shallow to see.
                // Kept modest on purpose - too high and every rivet/tread edge reads as a gouge
                // once real per-pixel lighting is actually applied to it.
                var normal = Vector3.Normalize(new Vector3(-dx * 6f, -dy * 6f, 1f));
                data[y * size + x] = new Color(
                    normal.X * 0.5f + 0.5f,
                    normal.Y * 0.5f + 0.5f,
                    normal.Z * 0.5f + 0.5f);
            }
        }
        texture.SetData(data);
        return texture;
    }

    // The hull plate as a normal map, same exact idea as CreateFloorNormals above and built the
    // same way (central differences over the same height field the visible plate is drawn from) -
    // real per-pixel relief for the outer armour instead of ScenePost having to guess a fake
    // normal from the finished picture's own luminance gradient the way it still does for the
    // walls/devices. The panel seam and rivets HullPixel now carries (its own multi-layer doc
    // comment) show up here too - a seam or a rivet is a real slope in this height field, not
    // brightness painted on flat geometry, so this map gives them one for the shader to light.
    public static Texture2D CreateHullNormals(GraphicsDevice device)
    {
        const int size = HullTileSize;
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = HullPixel(Wrap(x + 1, size), y) - HullPixel(Wrap(x - 1, size), y);
                var dy = HullPixel(x, Wrap(y + 1, size)) - HullPixel(x, Wrap(y - 1, size));
                var normal = Vector3.Normalize(new Vector3(-dx * 6f, -dy * 6f, 1f));
                data[y * size + x] = new Color(
                    normal.X * 0.5f + 0.5f,
                    normal.Y * 0.5f + 0.5f,
                    normal.Z * 0.5f + 0.5f);
            }
        }
        texture.SetData(data);
        return texture;
    }

    // A single column of shading stretched over a whole panel, so the face darkens from top to
    // bottom the way a lit box does. Deliberately not baked into the tile above: a gradient inside
    // a tile repeats with the tile and reads as banding, where this one spans the panel once.
    //
    // This is the change that matters most to ScenePost's relief lighting. That pass treats the
    // gradient of scene luminance as surface tilt, and a flat colour fill has a gradient of exactly
    // zero - which is why, before this existed, every device on the ship was the one thing in frame
    // the new lighting could not touch.
    public static Texture2D CreateFaceShade(GraphicsDevice device)
    {
        const int height = 64;
        var texture = new Texture2D(device, 1, height);
        var data = new Color[height];
        for (var y = 0; y < height; y++)
        {
            var t = y / (height - 1f);
            // White over the top third fading out, black creeping in over the bottom third.
            var lift = MathHelper.Clamp(1f - t * 3f, 0f, 1f) * 0.13f;
            var drop = MathHelper.Clamp(t * 3f - 2f, 0f, 1f) * 0.30f;
            data[y] = lift > drop ? Color.White * lift : Color.Black * drop;
        }
        texture.SetData(data);
        return texture;
    }

    // Tiles `texture` across `rect` in `tileSize` cells, tinted uniformly - the same colour the
    // caller's old flat fill used. Edge cells are clipped to `rect` rather than overflowing past it.
    public static void DrawTiled(SpriteBatch spriteBatch, Texture2D texture, int tileSize, Rectangle rect, Color tint)
    {
        for (var y = rect.Y; y < rect.Bottom; y += tileSize)
        {
            var h = Math.Min(tileSize, rect.Bottom - y);
            for (var x = rect.X; x < rect.Right; x += tileSize)
            {
                var w = Math.Min(tileSize, rect.Right - x);
                spriteBatch.Draw(texture, new Rectangle(x, y, w, h), new Rectangle(0, 0, w, h), tint);
            }
        }
    }

    private static Texture2D Build(GraphicsDevice device, int size, Func<int, int, float> valueAt)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var value = MathHelper.Clamp(valueAt(x, y), 0.3f, 1f);
                data[y * size + x] = new Color(value, value, value);
            }
        texture.SetData(data);
        return texture;
    }

    // Cheap deterministic value noise, no seed state: the same tile comes out the same way every
    // run, so two rooms drawing "the same" texture never shimmer relative to each other or between
    // frames.
    private static float Hash(int x, int y, int seed)
    {
        var n = x * 374761393 + y * 668265263 + seed * 362437;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // Public purely so SpaceAdventure.ShaderCheck can assert the property that everything else in
    // here rests on: the noise lattice has to close on itself, or every tiled surface in the game
    // grows a seam. Checked algebraically rather than by diffing finished pixels - in the finished
    // floor the noise is a few percent of a brightness dominated by the tread ridge, so a broken
    // wrap hides inside it (measured: the pixel-difference version of this test passed happily with
    // wrapping disabled).
    public static bool NoiseWrapsCleanly()
    {
        foreach (var cells in new[] { 2, 3, 4, 6, 8 })
        {
            for (var y = 0; y < cells; y++)
            {
                if (Math.Abs(Noise(0f, y, cells, 0) - Noise(cells, y, cells, 0)) > 1e-6f)
                    return false;
                if (Math.Abs(Noise(y, 0f, cells, 0) - Noise(y, cells, cells, 0)) > 1e-6f)
                    return false;
            }
        }
        return true;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static int Wrap(int a, int period) => (a % period + period) % period;

    // Smoothly interpolated value noise on a lattice of `cells` points that wraps at `cells` - the
    // wrap is what makes the finished tile seamless against a copy of itself.
    private static float Noise(float x, float y, int cells, int seed)
    {
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var tx = Smooth(x - x0);
        var ty = Smooth(y - y0);

        var a = MathHelper.Lerp(Hash(Wrap(x0, cells), Wrap(y0, cells), seed), Hash(Wrap(x0 + 1, cells), Wrap(y0, cells), seed), tx);
        var b = MathHelper.Lerp(Hash(Wrap(x0, cells), Wrap(y0 + 1, cells), seed), Hash(Wrap(x0 + 1, cells), Wrap(y0 + 1, cells), seed), tx);
        return MathHelper.Lerp(a, b, ty);
    }

    // Several octaves of the above, each twice as fine and half as strong. This is what separates
    // "material" from "speckle": one octave of per-pixel noise is television static, four octaves
    // is a surface with both broad mottling and fine tooth.
    private static float Fbm(int x, int y, int size, int octaves, int cells, int seed = 0)
    {
        float sum = 0f, amplitude = 1f, total = 0f;
        for (var i = 0; i < octaves; i++)
        {
            sum += Noise(x * cells / (float)size, y * cells / (float)size, cells, seed + i) * amplitude;
            total += amplitude;
            amplitude *= 0.5f;
            cells *= 2;
        }
        return sum / total;
    }

    // A rivet head at (cx, cy), lit from the top-left like every other bevel in this renderer: the
    // near side catches light, the far side falls into shadow, and a contact shadow rings the whole
    // head so it sits on the plate instead of floating above it.
    private static float Rivet(int x, int y, int cx, int cy)
    {
        float dx = x - cx, dy = y - cy;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance > 2.8f)
            return 0f;

        var lit = MathHelper.Clamp((-dx - dy) / 4f, -0.12f, 0.12f);
        return distance > 1.9f ? lit * 0.4f - 0.09f : lit;
    }

    private static float Rivets(int x, int y, int size)
    {
        const int inset = 5;
        return Rivet(x, y, inset, inset)
             + Rivet(x, y, size - inset, inset)
             + Rivet(x, y, inset, size - inset)
             + Rivet(x, y, size - inset, size - inset);
    }

    // Tread plate: a diagonal ridge with a lit crest and a shadowed back, broad mottling underneath,
    // and a rivet at each corner - ShipRenderer.DrawRivets' own corner-plate convention, echoed here
    // at tile scale.
    private static float FloorPixel(int x, int y)
    {
        var value = 0.93f + (Fbm(x, y, FloorTileSize, 2, 12) - 0.5f) * 0.05f;

        var diagonal = Wrap(x + y, 12);
        if (diagonal < 2) value += 0.04f;          // crest
        else if (diagonal < 4) value -= 0.05f;     // the shadow it casts
        else if (diagonal is 8 or 9) value += 0.015f;

        return value + Rivets(x, y, FloorTileSize);
    }

    // Vertical brushed streaks rather than the floor's diagonal tread - a bulkhead panel is rolled
    // sheet, not tread plate - plus the seam where one rolled panel meets the next.
    private static float WallPixel(int x, int y)
    {
        // Fine across, almost uniform along: that anisotropy is the whole look of brushed metal.
        var brushed = (Noise(x * 8f / WallTileSize, y * 0.5f / WallTileSize, 8, 5) - 0.5f) * 0.11f;
        var mottle = (Fbm(x, y, WallTileSize, 3, 2, seed: 11) - 0.5f) * 0.05f;
        var value = 0.89f + brushed + mottle;

        if (y == 0) value -= 0.09f;                // seam
        else if (y == 1) value += 0.05f;           // and the lit lip below it

        return value;
    }

    // Machined casing: a fine tooth that runs across rather than along, so it reads as a moulded
    // face and not as the floor seen edge-on. Kept quiet - a device is a small rectangle on screen
    // and loud grain at that size turns into fizz.
    private static float DevicePixel(int x, int y)
    {
        var tooth = (Noise(x * 6f / DeviceTileSize, y * 2f / DeviceTileSize, 6, 31) - 0.5f) * 0.07f;
        var grain = (Fbm(x, y, DeviceTileSize, 3, 4, seed: 37) - 0.5f) * 0.05f;
        return 0.95f + tooth + grain;
    }

    // A raised armour panel, not a flat plate - built as several independent layers stacked on top
    // of each other (base grain, a riveted seam splitting the tile into two half-plates, soft wear
    // patches, fine scratches, and grime streaks) rather than one texture doing everything at once.
    // Each layer stays cheap and single-purpose on its own; it's the stack of them together that
    // reads as "this hull has actually been out flying", not a flat colour cutout.
    private static float HullPixel(int x, int y)
    {
        var value = 0.94f;
        value += HullBaseGrain(x, y);
        value += HullBevel(x, y);
        value += HullPanelSeam(x, y);
        value += HullScratches(x, y);
        value += HullGrimeStreaks(x, y);
        return value + Rivets(x, y, HullTileSize);
    }

    // Layer 1: fine mottling underneath everything else - the metal's own grain, before any wear or
    // damage is layered on top of it.
    private static float HullBaseGrain(int x, int y) => (Fbm(x, y, HullTileSize, 2, 10) - 0.5f) * 0.035f;

    // Layer 2: bright top/left, dark bottom/right - the same lit-edge convention ShipRenderer's own
    // DrawPanel uses, so the tile itself reads as one raised plate bolted onto the hull underneath.
    private static float HullBevel(int x, int y)
    {
        const int bevel = 4;
        var value = 0f;
        if (x < bevel || y < bevel) value += 0.05f;
        if (x >= HullTileSize - bevel || y >= HullTileSize - bevel) value -= 0.07f;
        return value;
    }

    // Layer 3: a second, riveted seam splitting the plate itself in two - one full-size armour
    // panel this size would be a single implausibly large casting; two smaller ones bolted together
    // reads as real fabricated plating instead. Vertical rather than diagonal so it never gets
    // confused for the floor's own tread ridge (FloorPixel) at a glance.
    private static float HullPanelSeam(int x, int y)
    {
        const int seamX = HullTileSize / 2;
        var value = 0f;
        if (x == seamX) value -= 0.04f;        // the seam's own shadowed crack
        else if (x == seamX + 1) value += 0.025f; // the lit lip catching light just past it

        // Two small rivets down the seam, a quarter and three-quarters of the way along - the
        // fasteners actually holding the two half-plates together.
        value += Rivet(x, y, seamX, HullTileSize / 4);
        value += Rivet(x, y, seamX, HullTileSize * 3 / 4);
        return value;
    }


    // Layer 5: sparse, elongated flecks rather than a continuous field - a real scratch is a rare,
    // short event, not something covering the whole plate. Anisotropic noise (many cells across,
    // few down) makes each fleck read as a short horizontal scuff instead of a round dot; a bright
    // and a dark threshold on the same field gives both "metal scraped shiny" and "grit dragged
    // through the paint" without needing two separate noise samples.
    private static float HullScratches(int x, int y)
    {
        var scratch = Noise(x * 11f / HullTileSize, y * 3f / HullTileSize, 11, seed: 41);
        if (scratch > 0.96f) return -0.05f;
        if (scratch < 0.02f) return 0.03f;
        return 0f;
    }

    // Layer 6: grime that has run and settled, strongest low on the plate and fading out near the
    // top - the same "dirt drips down, it does not float up" logic real weathering follows. Only
    // ever darkens (MathF.Max(0f, ...) below) - grime is a stain, never a bright fleck.
    private static float HullGrimeStreaks(int x, int y)
    {
        var streak = Noise(x * 5f / HullTileSize, y * 0.6f / HullTileSize, 5, seed: 53) - 0.5f;
        var lowOnThePlate = MathHelper.Clamp((y - HullTileSize * 0.15f) / (HullTileSize * 0.5f), 0f, 1f);
        return -MathF.Max(0f, streak) * 0.04f * lowOnThePlate;
    }
}
