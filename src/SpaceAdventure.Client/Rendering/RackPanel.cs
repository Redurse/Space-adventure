using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The ship's storage rack, opened by clicking it while standing at it. Deliberately drawn with the
// same slot look as InventoryPanel (same size, same colours), because the whole point is that you
// drag items straight between the two - two different-looking grids would read as two unrelated
// systems.
public sealed class RackPanel
{
    public const int Columns = 10;
    private const int SlotSize = InventoryPanel.SlotSize;
    private const int SlotSpacing = InventoryPanel.SlotSpacing;
    private const int GridTop = 20;

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

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var used = 0;
        for (var i = 0; i < snapshot.RackSlots.Count; i++)
            if (snapshot.RackSlots[i] is not null)
                used++;

        spriteBatch.DrawString(_font, $"Стеллаж — {used}/{StorageRack.Capacity}   [перетащите мышью или двойной клик]",
            origin, Color.Khaki, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        for (var i = 0; i < StorageRack.Capacity; i++)
        {
            var item = i < snapshot.RackSlots.Count ? snapshot.RackSlots[i] : null;
            InventoryPanel.DrawSlot(spriteBatch, _pixel, _font, GetSlotRect(i, origin), item, string.Empty);
        }
    }
}
