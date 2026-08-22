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

    public static Texture2D CreateHullPlate(GraphicsDevice device) => BuildColor(device, HullTileSize, (x, y) => HullColor(x, y, null));

    /// <summary>The hull's plates - several of them, because one repeated across a whole ship is what
    /// made the armour read as a texture instead of as plating. See HullPlateVariants.</summary>
    public static Texture2D[] CreateHullPlates(GraphicsDevice device)
    {
        var plates = new Texture2D[HullPlateVariants.Count];
        for (var v = 0; v < plates.Length; v++)
        {
            var variant = v;
            plates[v] = BuildColor(device, HullTileSize, (x, y) => HullColor(x, y, variant));
        }
        return plates;
    }

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
    /// <summary>Tiles a set of plates instead of one, picking which goes where from the square's
    /// position in ship space - `cellOrigin` is what makes that position stable, since `rect` moves
    /// with the camera and indexing off it would make the pattern crawl across the hull as you
    /// scroll. Each plate also gets its own slight tone, which on its own does more to break the
    /// repeat than the extra tiles do.</summary>
    public static void DrawTiled(SpriteBatch spriteBatch, Texture2D[] plates, int tileSize, Rectangle rect,
        Color tint, Point cellOrigin)
    {
        for (var y = rect.Y; y < rect.Bottom; y += tileSize)
        {
            var h = Math.Min(tileSize, rect.Bottom - y);
            for (var x = rect.X; x < rect.Right; x += tileSize)
            {
                var w = Math.Min(tileSize, rect.Right - x);
                var cellX = (int)MathF.Floor((x - cellOrigin.X) / (float)tileSize);
                var cellY = (int)MathF.Floor((y - cellOrigin.Y) / (float)tileSize);
                var plate = plates[HullPlateVariants.VariantAt(cellX, cellY) % plates.Length];
                var tone = HullPlateVariants.ToneAt(cellX, cellY);
                var shaded = new Color((int)(tint.R * tone), (int)(tint.G * tone), (int)(tint.B * tone), tint.A);
                spriteBatch.Draw(plate, new Rectangle(x, y, w, h), new Rectangle(0, 0, w, h), shaded);
            }
        }
    }

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

    // Same idea as Build, but for the hull plate: a real gunmetal colour baked into the texture
    // itself instead of a grayscale height field meant to be multiplied by an external tint. No
    // clamp-to-grey here - HullColor already returns a finished colour.
    private static Texture2D BuildColor(GraphicsDevice device, int size, Func<int, int, Color> colorAt)
    {
        var texture = new Texture2D(device, size, size);
        var data = new Color[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                data[y * size + x] = colorAt(x, y);
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
    // sheet, not tread plate - plus a bevel, two corner rivets and a proper weld bead where one
    // rolled panel meets the next, the same fabricated-plating language HullPixel uses on the
    // armour. This is the one tile that is actually inside the lit interior a player walks
    // through (DrawRoomWalls straddles the room edge, half in, half out - unlike the hull's own
    // margin bezel, which sits entirely outside the room's own light/visibility polygon and never
    // reads as more than a flat tone), so it is worth more detail than a bare brushed-noise field.
    private static float WallPixel(int x, int y)
    {
        // Fine across, almost uniform along: that anisotropy is the whole look of brushed metal.
        var brushed = (Noise(x * 8f / WallTileSize, y * 0.5f / WallTileSize, 8, 5) - 0.5f) * 0.09f;
        var mottle = (Fbm(x, y, WallTileSize, 3, 4, seed: 11) - 0.5f) * 0.045f;
        var value = 0.89f + brushed + mottle;

        // A bevel round the whole tile, same lit-top-left/dark-bottom-right convention as the hull
        // plate - a wall panel reads as bolted-on rather than painted flat.
        const int bevel = 2;
        if (x < bevel || y < bevel) value += 0.045f;
        if (x >= WallTileSize - bevel || y >= WallTileSize - bevel) value -= 0.05f;

        // The panel seam as a short weld bead - per-column speckle across a 3px band - instead of
        // one flat row, the same idea as HullWeldBead just scaled to this tile's own size.
        var seamY = WallTileSize / 2;
        var rowOffset = y - seamY;
        if (rowOffset is >= -1 and <= 1)
        {
            var bright = Hash(x, seamY, 71) > 0.5f;
            value += rowOffset switch
            {
                -1 => -0.025f,
                0 => bright ? 0.045f : -0.055f,
                _ => 0.03f,
            };
        }

        // Two corner rivets rather than four - a wall panel this size does not need to look
        // armoured, just fabricated.
        value += Rivet(x, y, 4, 4);
        value += Rivet(x, y, WallTileSize - 5, WallTileSize - 5);

        // A rare scratch - sparingly, this is a corridor bulkhead, not a hull that has taken fire.
        if (Noise(x * 9f / WallTileSize, y * 2f / WallTileSize, 9, seed: 61) > 0.965f)
            value -= 0.035f;

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

    // The armour plate as three physically stacked slabs rather than one flat sheet with fasteners
    // scattered across it - closer to Barotrauma's hull, where a segment reads as a few big, clearly
    // stepped panels bolted together. An outer mounting flange, the main plate, and an inset
    // inspection core, each its own tone, each boundary a real lit/shadow ledge rather than a line -
    // plus a real gunmetal colour baked into the tile itself instead of a grayscale height field
    // that only reads as metal once multiplied by an external tint.
    private const int HullFrameWidth = 6;
    private const int HullCoreLeft = 17, HullCoreTop = 17;
    private const int HullCoreRight = HullTileSize - 18, HullCoreBottom = HullTileSize - 18;
    private const int HullSeamX = HullTileSize / 2;

    private static readonly Color HullFrameColor = new(40, 46, 53);
    private static readonly Color HullPlateColor = new(64, 71, 80);
    private static readonly Color HullCoreColor = new(78, 87, 97);
    private static readonly Color HullHighlight = new(170, 178, 186);
    private static readonly Color HullShadow = new(22, 26, 31);

    // Which of the three slabs a pixel falls in, that slab's own tone, and the lit/shadow ledges at
    // whichever boundaries it sits on - the frame's outer and inner edges, or the plate's two steps
    // down to the flange and up to the core.
    private static void HullZone(int x, int y, out Color baseTone, out float layerShade, out bool inPlateZone)
    {
        var inFrame = x < HullFrameWidth || y < HullFrameWidth || x >= HullTileSize - HullFrameWidth || y >= HullTileSize - HullFrameWidth;
        var inCore = x >= HullCoreLeft && x <= HullCoreRight && y >= HullCoreTop && y <= HullCoreBottom;
        inPlateZone = !inFrame && !inCore;
        layerShade = 0f;

        if (inFrame)
        {
            baseTone = HullFrameColor;
            if (x == 0 || y == 0 || x == HullTileSize - 1 || y == HullTileSize - 1) layerShade += 0.11f;
            if (x == HullFrameWidth - 1 || y == HullFrameWidth - 1 || x == HullTileSize - HullFrameWidth || y == HullTileSize - HullFrameWidth)
                layerShade -= 0.13f;
        }
        else if (inCore)
        {
            baseTone = HullCoreColor;
            if (x == HullCoreLeft || y == HullCoreTop) layerShade += 0.10f;
            if (x == HullCoreRight || y == HullCoreBottom) layerShade -= 0.11f;
        }
        else
        {
            baseTone = HullPlateColor;
            if (x == HullFrameWidth || y == HullFrameWidth) layerShade += 0.10f;
            if (x == HullTileSize - HullFrameWidth - 1 || y == HullTileSize - HullFrameWidth - 1) layerShade -= 0.07f;
            if (x == HullCoreLeft - 1 || y == HullCoreTop - 1) layerShade += 0.07f;
            if (x == HullCoreRight + 1 || y == HullCoreBottom + 1) layerShade -= 0.09f;
            layerShade += HullWeldBead(x, y);
        }
    }

    // The seam through the main plate as an actual weld bead - per-row irregular speckle along a
    // 3px band, so it reads as overlapping weld-pool ripples instead of a ruled line. Only runs
    // through the mid plate; it stops at the flange and the core rather than cutting through either.
    private static float HullWeldBead(int x, int y)
    {
        var offset = x - HullSeamX;
        if (offset < -1 || offset > 2)
            return 0f;
        var bright = Hash(y, HullSeamX, 77) > 0.55f;
        return offset switch
        {
            -1 => -0.03f,
            0 => bright ? 0.05f : -0.06f,
            1 => bright ? 0.07f : -0.02f,
            _ => 0.02f,
        };
    }

    // A small handful of rivets - four holding the inspection core down, two on the seam, four more
    // set into the flange's own corners - instead of a fastener at every few pixels.
    private static float HullFrameCornerRivets(int x, int y)
    {
        var inset = HullFrameWidth / 2 + 1;
        return Rivet(x, y, inset, inset) + Rivet(x, y, HullTileSize - 1 - inset, inset)
             + Rivet(x, y, inset, HullTileSize - 1 - inset) + Rivet(x, y, HullTileSize - 1 - inset, HullTileSize - 1 - inset);
    }

    private static float HullCoreAndSeamRivets(int x, int y, bool inPlateZone)
    {
        var value = Rivet(x, y, HullCoreLeft + 2, HullCoreTop + 2) + Rivet(x, y, HullCoreRight - 2, HullCoreTop + 2)
                  + Rivet(x, y, HullCoreLeft + 2, HullCoreBottom - 2) + Rivet(x, y, HullCoreRight - 2, HullCoreBottom - 2);
        if (inPlateZone)
        {
            value += Rivet(x, y, HullSeamX, HullFrameWidth + 4);
            value += Rivet(x, y, HullSeamX, HullTileSize - HullFrameWidth - 5);
        }
        return value;
    }

    // A rivet every `spacing` pixels along all four edges, at a fixed depth into the frame. In the
    // game itself the tile is normally only seen cropped down to its own outer edge (HullSkin's
    // DrawHullPlating, ShipRenderer's exterior wall bands) - a long run of open hull still needs a
    // rivet passing by every so often as that crop repeats along it, not just the sparse
    // corner/seam/core rivets meant for a single plate seen whole and in isolation (e.g. a
    // ship-editor preview).
    private static float HullBorderRivets(int x, int y)
    {
        const int depth = 8;
        const int spacing = 22;
        var value = 0f;
        for (var p = spacing; p < HullTileSize; p += spacing)
        {
            value += Rivet(x, y, p, depth);
            value += Rivet(x, y, p, HullTileSize - 1 - depth);
            value += Rivet(x, y, depth, p);
            value += Rivet(x, y, HullTileSize - 1 - depth, p);
        }
        return value;
    }

    // A thin joint mark crossing the border every `period` pixels along its length, only within the
    // depth the margin bezel actually shows - the seam between one length of plating and the next,
    // the border's own equivalent of HullWeldBead.
    private static float HullBorderJoints(int x, int y)
    {
        const int period = 48;
        const int visibleDepth = 17;
        var value = 0f;
        if (y < visibleDepth || y >= HullTileSize - visibleDepth)
        {
            var alongX = Wrap(x, period);
            if (alongX == 0) value -= 0.035f;
            else if (alongX == 1) value += 0.02f;
        }
        if (x < visibleDepth || x >= HullTileSize - visibleDepth)
        {
            var alongY = Wrap(y, period);
            if (alongY == 0) value -= 0.035f;
            else if (alongY == 1) value += 0.02f;
        }
        return value;
    }

    // Sparse round dimples, distinct from the elongated scratches below - a dent has a dark cup and
    // a small bright rim catching the light on the side the blow came from, not a scraped streak.
    private static float HullMicroDents(int x, int y)
    {
        const int cell = 9;
        var cx = (x / cell) * cell + 4;
        var cy = (y / cell) * cell + 4;
        if (Hash(x / cell, y / cell, 63) < 0.9f)
            return 0f;

        float dx = x - cx, dy = y - cy;
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance > 2.2f)
            return 0f;
        var rim = MathHelper.Clamp((-dx - dy) / 3f, 0f, 0.1f);
        return distance > 1.3f ? rim : -0.09f;
    }

    // Two independent scratch fields rather than one - real wear is not a single noise sample. Kept
    // separate from HullShadeSum below because scratches blend straight toward the highlight/shadow
    // colour rather than nudging the same additive height field the layering and rivets share.
    private static float HullScratch(int x, int y)
    {
        var scratchA = Noise(x * 11f / HullTileSize, y * 3f / HullTileSize, 11, seed: 41);
        var scratch = 0f;
        if (scratchA > 0.96f) scratch = -0.042f;
        else if (scratchA < 0.02f) scratch = 0.035f;

        var scratchB = Noise(x * 19f / HullTileSize, y * 5f / HullTileSize, 19, seed: 97);
        if (scratchB > 0.972f) scratch += 0.045f;
        else if (scratchB < 0.02f) scratch -= 0.02f;
        return scratch;
    }

    // The combined structural height field - layering, grain, rivets, dents - shared between the
    // hull's visible colour (HullColor) and its normal map (CreateHullNormals), the same way
    // FloorPixel/WallPixel double as both for their own tiles.
    private static float HullShadeSum(int x, int y, out Color baseTone)
    {
        HullZone(x, y, out baseTone, out var layerShade, out var inPlateZone);

        var grain = (Fbm(x, y, HullTileSize, 4, 10) - 0.5f) * 0.05f;
        grain += (Noise(x * 16f / HullTileSize, y * 1f / HullTileSize, 16, seed: 5) - 0.5f) * 0.022f;
        grain += HullMicroDents(x, y);
        grain += HullBorderJoints(x, y);

        var rivets = HullCoreAndSeamRivets(x, y, inPlateZone) + HullFrameCornerRivets(x, y) + HullBorderRivets(x, y);
        return grain + layerShade + rivets;
    }

    // Brightness-only reduction of the same recipe HullColor paints with - CreateHullNormals takes
    // central differences of this to build the hull's normal map, same as FloorPixel/WallPixel.
    private static float HullPixel(int x, int y) => 0.94f + HullShadeSum(x, y, out _) + HullScratch(x, y) * 0.4f;

    // The hull plate's actual colour: the shade field above turned into a lit/shadow blend around
    // whichever slab's own tone the pixel falls in, then the scratches painted straight on top.
    // `variant` folds in HullPlateVariants' per-plate stencil/patch marks (see CreateHullPlates);
    // null for the single un-varied plate CreateHullPlate hands out to callers that don't tile a set.
    private static Color HullColor(int x, int y, int? variant)
    {
        var shadeSum = HullShadeSum(x, y, out var baseTone);
        if (variant is { } v)
            shadeSum += HullPlateVariants.Extra(x, y, HullTileSize, v) * 0.5f;

        var t = MathHelper.Clamp(0.5f + shadeSum * 2.2f, 0f, 1f);
        var color = t > 0.5f
            ? Color.Lerp(baseTone, HullHighlight, (t - 0.5f) * 2f)
            : Color.Lerp(HullShadow, baseTone, t * 2f);

        var scratch = HullScratch(x, y);
        if (scratch != 0f)
            color = Color.Lerp(color, scratch > 0 ? HullHighlight : HullShadow, MathF.Abs(scratch) * 6f);

        return color;
    }
}
