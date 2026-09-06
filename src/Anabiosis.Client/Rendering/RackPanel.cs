using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The ship's storage rack, opened by clicking it while standing at it. Deliberately drawn with the
// same slot look as InventoryPanel (same size, same colours), because the whole point is that you
// drag items straight between the two - two different-looking grids would read as two unrelated
// systems.
public sealed class RackPanel
{
    // 6 x 5 rather than a 10-wide strip: StorageRack.Capacity is 30, so this is the layout that
    // actually reads as a rack of shelves instead of one long row. Both the drawing and the click
    // handling go through GetSlotRect, so this single number moves them together.
    public const int Columns = 6;
    private const int SlotSize = InventoryPanel.SlotSize;
    private const int SlotSpacing = InventoryPanel.SlotSpacing;
    private const int GridTop = 20;

    private const int GridWidth = Columns * (SlotSize + SlotSpacing) - SlotSpacing;
    private const int GridHeight = StorageRack.Capacity / Columns * (SlotSize + SlotSpacing) - SlotSpacing;

    // Its own size rather than DevicePanelChrome.Standard: a 6x5 grid of 44px slots is taller
    // than it is wide, and the standard terminal box is the opposite shape. Published so Game1
    // can centre it - the caller cannot guess a size the grid constants decide.
    public static Point PanelSize => new(GridWidth + 28, GridTop + GridHeight + 64);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public RackPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetSlotRect(int index, Vector2 panelOrigin)
    {
        var x = (int)panelOrigin.X + index % Columns * (SlotSize + SlotSpacing);
        var y = (int)panelOrigin.Y + GridTop + index / Columns * (SlotSize + SlotSpacing);
        return new Rectangle(x, y, SlotSize, SlotSize);
    }

    // offset: where this particular shelf's 30-slot band starts in the snapshot's flat RackSlots
    // array (World.Storage.cs's RackFor) - a hull carries two shelves now, so this draws whichever
    // one Game1's CurrentOpenRackOffset says is actually open, not always the first.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, int offset, float totalSeconds)
    {
        var used = 0;
        for (var i = 0; i < StorageRack.Capacity; i++)
            if (offset + i < snapshot.RackSlots.Count && snapshot.RackSlots[offset + i] is not null)
                used++;

        const int rows = StorageRack.Capacity / Columns;
        const int gridWidth = GridWidth;
        const int gridHeight = GridHeight;

        var bounds = new Rectangle((int)origin.X - DevicePanelChrome.OriginInsetX,
            (int)origin.Y - DevicePanelChrome.OriginInsetY, PanelSize.X, PanelSize.Y);
        var phosphor = new Color(226, 198, 122);
        DevicePanelChrome.Draw(spriteBatch, _font, bounds, "СТЕЛЛАЖ", "ST-05", phosphor, totalSeconds);

        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(0, -8),
            "ЗАНЯТО", $"{used}", $"/ {StorageRack.Capacity}",
            used >= StorageRack.Capacity ? new Color(236, 140, 96) : phosphor);

        // A rail under every row, and a well behind every slot. This is what separates a rack from a
        // grid drawn on a wall: the items are standing on something, and each one is in its own
        // compartment rather than floating in a shared field.
        for (var row = 0; row < rows; row++)
        {
            var railY = (int)origin.Y + GridTop + row * (SlotSize + SlotSpacing) + SlotSize + 1;
            spriteBatch.Draw(_pixel, new Rectangle((int)origin.X - 3, railY, gridWidth + 6, 2), new Color(96, 104, 120));
            spriteBatch.Draw(_pixel, new Rectangle((int)origin.X - 3, railY, gridWidth + 6, 1), new Color(150, 160, 178));
            spriteBatch.Draw(_pixel, new Rectangle((int)origin.X - 3, railY + 2, gridWidth + 6, 2), Color.Black * 0.45f);
        }

        for (var i = 0; i < StorageRack.Capacity; i++)
        {
            var rect = GetSlotRect(i, origin);
            // The well: recessed dark backing with a shadow along its top-left, which is the same
            // lit-from-top-left convention every other housing in the game uses.
            spriteBatch.Draw(_pixel, new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2), Color.Black * 0.55f);
            spriteBatch.Draw(_pixel, rect, new Color(26, 30, 38));
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.Black * 0.6f);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Color.Black * 0.6f);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.White * 0.10f);

            var globalIndex = offset + i;
            var item = globalIndex < snapshot.RackSlots.Count ? snapshot.RackSlots[globalIndex] : null;
            InventoryPanel.DrawSlot(spriteBatch, _pixel, _font, rect, item, string.Empty);
        }

        spriteBatch.DrawString(_font, "ПЕРЕТАЩИТЕ МЫШЬЮ ИЛИ ДВОЙНОЙ КЛИК",
            new Vector2(origin.X, origin.Y + GridTop + gridHeight + 8), new Color(120, 132, 148),
            0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }
}
