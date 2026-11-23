using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// Direct user request ("сделай картинку приятнее, ближе к качеству Баротравмы", item 1) - what glues the
// machines to the deck instead of leaving them lying on it like stickers:
//   * a soft drop shadow under every device, cast down-right (the same upper-left key light every baked
//     face and texture in the game is lit from);
//   * ambient occlusion where the floor meets a wall - a dark band fading out from the wall face, which
//     also darkens the corners for free where two bands overlap.
//
// The shadows have to go down BEFORE any device is drawn, or a shadow would fall across a neighbour
// drawn earlier. Rather than teach every one of the ~30 device draw methods to report its rect up front,
// each one just registers the rect it actually drew (CastShadow), and the next frame's pass draws them
// all first. Devices never move relative to the hull, so the one-frame lag is invisible; the rects are
// kept relative to the frame's origin so camera motion between the two frames doesn't drag them.
public sealed partial class ShipRenderer
{
    private readonly List<Rectangle> _shadowCasters = new();
    private Vector2 _shadowCasterOrigin;
    private Texture2D? _softShadow;
    private Texture2D? _wallOcclusion;

    // Drop shadow offset and spread, in world units, and how dark it is at its heart.
    private const float ShadowOffsetX = 0.10f, ShadowOffsetY = 0.16f, ShadowSpread = 0.22f;
    private const float ShadowOpacity = 0.34f; // softened on request ("менее заметными... размытыми")
    // How far the wall's occlusion reaches onto the floor, and how dark it is right at the wall face.
    private const float WallOcclusionDepth = 0.6f;
    private const float WallOcclusionOpacity = 0.3f;

    /// A device registers the rect it was drawn into; its shadow appears under it from the next frame on.
    private void CastShadow(Rectangle rect)
    {
        if (_shadowCasters.Count > 4096)
            _shadowCasters.Clear(); // drawn outside the ship view (editor previews) and never flushed
        _shadowCasters.Add(rect);
    }

    // Draws last frame's casters under this frame's devices, then starts collecting afresh.
    private void DrawContactShadows(SpriteBatch spriteBatch, Vector2 origin)
    {
        var shift = origin - _shadowCasterOrigin;
        var texture = _softShadow ??= CreateSoftShadow(spriteBatch.GraphicsDevice);
        var offset = new Point((int)MathF.Round(ShadowOffsetX * PixelsPerUnit + shift.X), (int)MathF.Round(ShadowOffsetY * PixelsPerUnit + shift.Y));
        var spread = (int)MathF.Round(ShadowSpread * PixelsPerUnit);
        var border = Math.Max(6, (int)(0.5f * PixelsPerUnit)); // a wide fade, so no shadow has a visible edge
        foreach (var caster in _shadowCasters)
        {
            var rect = new Rectangle(caster.X + offset.X - spread, caster.Y + offset.Y - spread,
                caster.Width + spread * 2, caster.Height + spread * 2);
            DrawNineSlice(spriteBatch, texture, rect, Math.Min(border, Math.Min(rect.Width, rect.Height) / 2), Color.White * ShadowOpacity);
        }
        _shadowCasters.Clear();
        _shadowCasterOrigin = origin;
    }

    // A dark band fading out from every wall face onto the floor next to it. Walks the same live tile grid
    // the walls are drawn from, so it hugs half-blocks, door frames and odd shapes exactly.
    private void DrawWallOcclusion(SpriteBatch spriteBatch, TileGrid tiles, Vector2 origin)
    {
        var band = _wallOcclusion ??= CreateOcclusionBand(spriteBatch.GraphicsDevice);
        var depth = (int)MathF.Round(WallOcclusionDepth * PixelsPerUnit);
        var tile = (int)PixelsPerUnit;
        var tint = Color.White * WallOcclusionOpacity;
        foreach (var (coord, cell) in tiles.Cells)
        {
            if (!cell.HasFloor || cell.Wall != TileWallKind.None)
                continue;
            var x = (int)MathF.Round(origin.X + coord.X * PixelsPerUnit);
            var y = (int)MathF.Round(origin.Y + coord.Y * PixelsPerUnit);
            foreach (var side in TileSideExtensions.All)
            {
                if (tiles.CellAt(side.Offset(coord)) is not { Wall: TileWallKind.Solid } wall || wall.WallHp <= 0)
                    continue;
                // The band texture fades top (dark) to bottom (clear); rotate it so the dark edge is on
                // the wall's side of this floor tile.
                var (rect, rotation) = side switch
                {
                    TileSide.North => (new Rectangle(x, y, tile, depth), 0f),
                    TileSide.South => (new Rectangle(x + tile, y + tile, tile, depth), MathF.PI),
                    TileSide.West => (new Rectangle(x, y + tile, tile, depth), -MathF.PI / 2f),
                    _ => (new Rectangle(x + tile, y, tile, depth), MathF.PI / 2f),
                };
                spriteBatch.Draw(band, new Vector2(rect.X, rect.Y), null, tint, rotation, Vector2.Zero,
                    new Vector2(rect.Width / (float)band.Width, rect.Height / (float)band.Height), SpriteEffects.None, 0f);
            }
        }
    }

    // A round soft shadow under a person, offset with the same key light as the devices'.
    private void DrawBlobShadow(SpriteBatch spriteBatch, Vector2 center, float diameter)
    {
        var texture = _softShadow ??= CreateSoftShadow(spriteBatch.GraphicsDevice);
        var size = (int)MathF.Round(diameter);
        var rect = new Rectangle((int)MathF.Round(center.X - size / 2f + ShadowOffsetX * PixelsPerUnit * 0.6f),
            (int)MathF.Round(center.Y - size / 2f + ShadowOffsetY * PixelsPerUnit * 0.6f), size, size);
        DrawNineSlice(spriteBatch, texture, rect, size / 2, Color.White * 0.26f);
    }

    // Stretches the corners of `texture` at a fixed size and only the middle with the rect, so the soft
    // edge stays the same width whether the device is a small locker or the whole reactor.
    private static void DrawNineSlice(SpriteBatch spriteBatch, Texture2D texture, Rectangle rect, int border, Color color)
    {
        var s = texture.Width / 3;
        int[] dx = { rect.X, rect.X + border, rect.Right - border, rect.Right };
        int[] dy = { rect.Y, rect.Y + border, rect.Bottom - border, rect.Bottom };
        int[] sx = { 0, s, texture.Width - s, texture.Width };
        for (var i = 0; i < 3; i++)
        for (var j = 0; j < 3; j++)
        {
            var dest = new Rectangle(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
            if (dest.Width <= 0 || dest.Height <= 0)
                continue;
            spriteBatch.Draw(texture, dest, new Rectangle(sx[i], sx[j], sx[i + 1] - sx[i], sx[j + 1] - sx[j]), color);
        }
    }

    // A black rounded square whose edge fades to nothing over its outer third - premultiplied.
    private static Texture2D CreateSoftShadow(GraphicsDevice device)
    {
        const int size = 48, edge = 16;
        var data = new Color[size * size];
        for (var y = 0; y < size; y++)
        for (var x = 0; x < size; x++)
        {
            var dx = Math.Max(0f, Math.Max(edge - (x + 0.5f), x + 0.5f - (size - edge)));
            var dy = Math.Max(0f, Math.Max(edge - (y + 0.5f), y + 0.5f - (size - edge)));
            var t = MathHelper.Clamp(MathF.Sqrt(dx * dx + dy * dy) / edge, 0f, 1f);
            var a = MathF.Pow(1f - MathHelper.SmoothStep(0f, 1f, t), 1.4f);
            data[y * size + x] = new Color(0f, 0f, 0f, a);
        }
        var texture = new Texture2D(device, size, size);
        texture.SetData(data);
        return texture;
    }

    // Dark at the top row, clear at the bottom, eased so it reads as a soft corner shadow, not a stripe.
    private static Texture2D CreateOcclusionBand(GraphicsDevice device)
    {
        const int height = 32;
        var data = new Color[height];
        for (var y = 0; y < height; y++)
        {
            var t = (y + 0.5f) / height;
            var a = MathF.Pow(1f - t, 2.2f);
            data[y] = new Color(0f, 0f, 0f, a);
        }
        var texture = new Texture2D(device, 1, height);
        texture.SetData(data);
        return texture;
    }
}

// The fabricator and deconstructor's own painted art (MachineArt.Workshop.cs), shared by the game view and
// the ship editor. Painted portrait; a landscape footprint gets the same picture a quarter turn round
// (counter-clockwise, the same way DeviceSkin turns its portrait bodies).
public sealed partial class ShipRenderer
{
    private WorkshopArt? _workshopArt;
    private bool _framePowered = true;
    private readonly Dictionary<string, float> _frameRoomIntegrity = new();

    internal void SetWorkshopArt(WorkshopArt art) => _workshopArt = art;

    internal bool TryDrawWorkshopMachine(SpriteBatch spriteBatch, CustomDeviceKind kind, Rectangle rect, int stage, bool lit)
    {
        if (_workshopArt?.For(kind, stage, lit) is not { } texture)
            return false;
        if (rect.Width <= rect.Height)
            spriteBatch.Draw(texture, rect, Color.White);
        else
            spriteBatch.Draw(texture, new Vector2(rect.X, rect.Bottom), null, Color.White, -MathF.PI / 2f, Vector2.Zero,
                new Vector2(rect.Height / (float)texture.Width, rect.Width / (float)texture.Height), SpriteEffects.None, 0f);
        return true;
    }
}
