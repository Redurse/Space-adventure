using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// What the Shipwright's build tools draw over the ship itself, in the ship's own frame (1 tile = ShipRenderer.PixelsPerUnit): the
// tile grid around the cursor, the compartment ghost (green where it can go, red where it cannot), the door preview, the hovered
// compartment when demolishing, and a short message saying why a spot is refused.
public sealed class ShipBuildOverlay
{
    private static readonly Color Good = new(90, 160, 110);
    private static readonly Color Bad = new(170, 80, 80);
    private const int GridRadiusTiles = 14;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ShipBuildOverlay(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    private static Rectangle TileRect(TileCoord tile, Vector2 origin) =>
        new((int)(origin.X + tile.X * ShipRenderer.PixelsPerUnit), (int)(origin.Y + tile.Y * ShipRenderer.PixelsPerUnit),
            (int)ShipRenderer.PixelsPerUnit, (int)ShipRenderer.PixelsPerUnit);

    // A faint grid of tiles around the cursor, so the player sees what the placement snaps to.
    public void DrawGrid(SpriteBatch spriteBatch, TileCoord cursor, Vector2 origin)
    {
        var size = (int)ShipRenderer.PixelsPerUnit;
        var color = Color.White * 0.07f;
        for (var i = -GridRadiusTiles; i <= GridRadiusTiles + 1; i++)
        {
            var x = (int)(origin.X + (cursor.X + i) * size);
            var y = (int)(origin.Y + (cursor.Y + i) * size);
            var fade = 1f - System.Math.Abs(i - 0.5f) / (GridRadiusTiles + 1);
            spriteBatch.Draw(_pixel, new Rectangle(x, (int)(origin.Y + (cursor.Y - GridRadiusTiles) * size), 1, (2 * GridRadiusTiles + 1) * size), color * fade);
            spriteBatch.Draw(_pixel, new Rectangle((int)(origin.X + (cursor.X - GridRadiusTiles) * size), y, (2 * GridRadiusTiles + 1) * size, 1), color * fade);
        }
    }

    // The compartment's footprint tiles - green if the placement is allowed, red if not.
    public void DrawCompartmentGhost(SpriteBatch spriteBatch, IReadOnlyList<TileCoord> tiles, bool valid, Vector2 origin)
    {
        var color = (valid ? Good : Bad) * 0.45f;
        foreach (var tile in tiles)
            spriteBatch.Draw(_pixel, TileRect(tile, origin), color);
        var set = tiles.ToHashSet();
        // An outline round the whole footprint.
        foreach (var tile in tiles)
        {
            var rect = TileRect(tile, origin);
            if (!set.Contains(new TileCoord(tile.X, tile.Y - 1)))
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), Color.White * 0.8f);
            if (!set.Contains(new TileCoord(tile.X, tile.Y + 1)))
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), Color.White * 0.8f);
            if (!set.Contains(new TileCoord(tile.X - 1, tile.Y)))
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), Color.White * 0.8f);
            if (!set.Contains(new TileCoord(tile.X + 1, tile.Y)))
                spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), Color.White * 0.8f);
        }
    }

    // The door about to be placed: the wall tiles it opens (green) and the door itself across the opening; or, if it cannot go,
    // the tile under the cursor in red.
    public void DrawDoorPreview(SpriteBatch spriteBatch, TileCoord cursor, JunctionDoorPlan? plan, Vector2 origin)
    {
        if (plan is null)
        {
            spriteBatch.Draw(_pixel, TileRect(cursor, origin), Bad * 0.5f);
            return;
        }
        foreach (var tile in plan.WallTiles)
            spriteBatch.Draw(_pixel, TileRect(tile, origin), Good * 0.5f);
        foreach (var (coord, side) in plan.Edges)
        {
            var rect = TileRect(coord, origin);
            var bar = side switch
            {
                TileSide.East => new Rectangle(rect.Right - 4, rect.Y, 8, rect.Height),
                TileSide.West => new Rectangle(rect.X - 4, rect.Y, 8, rect.Height),
                TileSide.South => new Rectangle(rect.X, rect.Bottom - 4, rect.Width, 8),
                _ => new Rectangle(rect.X, rect.Y - 4, rect.Width, 8),
            };
            spriteBatch.Draw(_pixel, bar, Color.White * 0.9f);
        }
    }

    // A door already placed in-game: marked so it can be removed with a right click.
    public void DrawRemovableDoor(SpriteBatch spriteBatch, IEnumerable<ShipDoorEdge> edges, Vector2 origin)
    {
        foreach (var edge in edges)
        {
            var rect = TileRect(edge.Coord, origin);
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, Color.Orange * 0.8f, 1);
        }
    }

    public void DrawRoomHighlight(SpriteBatch spriteBatch, Room room, Vector2 origin)
    {
        var rect = new Rectangle((int)(origin.X + room.Left * ShipRenderer.PixelsPerUnit), (int)(origin.Y + room.Top * ShipRenderer.PixelsPerUnit),
            (int)(room.Width * ShipRenderer.PixelsPerUnit), (int)(room.Height * ShipRenderer.PixelsPerUnit));
        spriteBatch.Draw(_pixel, rect, Bad * 0.3f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, Color.OrangeRed, 3);
    }

    // The reason a spot is refused, next to the cursor.
    public void DrawMessage(SpriteBatch spriteBatch, string text, Vector2 screenPosition)
    {
        var size = _font.MeasureString(text) * 0.5f;
        var rect = new Rectangle((int)screenPosition.X + 16, (int)screenPosition.Y + 16, (int)size.X + 12, (int)size.Y + 8);
        spriteBatch.Draw(_pixel, rect, Color.Black * 0.8f);
        spriteBatch.DrawString(_font, text, new Vector2(rect.X + 6, rect.Y + 4), Color.OrangeRed, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
