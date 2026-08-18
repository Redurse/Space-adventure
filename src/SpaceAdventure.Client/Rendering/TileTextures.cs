using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Small procedurally-generated tileable textures standing in for floor/wall material - this project
// has no image assets (see ShipRenderer.DrawFloorGrating's own comment), so instead of an artist's
// diamond-plate sprite this bakes the same idea (per-pixel value noise, corner rivets, a diagonal
// tread ridge) into a Texture2D once at startup.
//
// Each texture is grayscale, averaging close to white, so the department/alarm colour a caller
// would otherwise have used for a flat spriteBatch.Draw(pixel, rect, color) fill can still be passed
// straight through as the tint in DrawTiled - the grain darkens and lightens around that colour
// instead of replacing it.
public static class TileTextures
{
    public const int FloorTileSize = 48; // one world unit at ShipRenderer.PixelsPerUnit
    public const int WallTileSize = 16;
    public const int HullTileSize = 64;

    public static Texture2D CreateFloorPlate(GraphicsDevice device) => Build(device, FloorTileSize, FloorPixel);

    public static Texture2D CreateWallPlate(GraphicsDevice device) => Build(device, WallTileSize, WallPixel);

    public static Texture2D CreateHullPlate(GraphicsDevice device) => Build(device, HullTileSize, HullPixel);

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
    private static float Hash(int x, int y)
    {
        var n = x * 374761393 + y * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // Near-white base plus: a diagonal tread ridge (sheet metal tread plate catching light along
    // one diagonal and shadowing the other), a soft grain, and a rivet dimple at each corner -
    // ShipRenderer.DrawRivets' own corner-plate convention, echoed here at tile scale.
    private static float FloorPixel(int x, int y)
    {
        var value = 0.92f + (Hash(x / 3, y / 3) - 0.5f) * 0.14f;

        var diagonal = (x + y) % 12;
        if (diagonal < 2) value += 0.06f;
        else if (diagonal > 9) value -= 0.07f;

        var counterDiagonal = ((x - y) % 12 + 12) % 12;
        if (counterDiagonal < 2) value += 0.03f;

        if (IsNearCorner(x, y, FloorTileSize))
            value -= 0.32f;

        return value;
    }

    // Vertical brushed-metal streaks rather than the floor's diagonal tread - a bulkhead panel is
    // rolled sheet, not tread plate.
    private static float WallPixel(int x, int y)
    {
        var streak = (Hash(x, 0) - 0.5f) * 0.12f;
        var grain = (Hash(x / 2, y / 2) - 0.5f) * 0.08f;
        return 0.88f + streak + grain;
    }

    // A raised armour panel, not a flat plate: a bevelled edge (bright top/left, dark bottom/right,
    // the same convention ShipRenderer's own DrawPanel uses), a rivet at each corner, fine grain, and
    // the odd larger weathered patch - a hull that's been out flying looks used, not showroom-new,
    // which is most of what separates a cartoon pixel-art ship from a flat colour cutout.
    private static float HullPixel(int x, int y)
    {
        var value = 0.94f + (Hash(x / 2, y / 2) - 0.5f) * 0.05f;

        const int bevel = 4;
        if (x < bevel || y < bevel) value += 0.05f;
        if (x >= HullTileSize - bevel || y >= HullTileSize - bevel) value -= 0.07f;

        // Coarse low-frequency patches - a wide hash lookup per 20px block, so the weathering reads
        // as a handful of dull patches rather than per-pixel speckle.
        if (Hash(x / 20, y / 20) < 0.16f)
            value -= 0.1f;

        if (IsNearCorner(x, y, HullTileSize))
            value -= 0.34f;

        return value;
    }

    private static bool IsNearCorner(int x, int y, int size)
    {
        const int inset = 5;
        return IsNear(x, inset) && IsNear(y, inset)
            || IsNear(x, size - inset) && IsNear(y, inset)
            || IsNear(x, inset) && IsNear(y, size - inset)
            || IsNear(x, size - inset) && IsNear(y, size - inset);
    }

    private static bool IsNear(int a, int b) => Math.Abs(a - b) <= 1;
}
