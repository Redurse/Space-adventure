using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Barotrauma-style inventory HUD (game_design.md section 13): a fixed row of general slots —
// hard cap on carried items, no drag-and-drop — plus separate equipment slots that don't
// compete with it for space (headset/clothing/headwear). Below each main slot sits a thin
// "hold" strip, exactly like Barotrauma's: clicking it equips/unequips that item into a hand.
public sealed class InventoryPanel
{
    public const int SlotSize = 26;
    public const int SlotSpacing = 4;
    public const int StripHeight = 6;
    private const int EquipToMainGap = 6;
    private const int StripGap = 2;

    private static readonly (EquipSlot Id, string Label)[] EquipSlots =
    {
        (EquipSlot.Headset, "Н"),
        (EquipSlot.Clothing, "О"),
        (EquipSlot.Headwear, "Г"),
    };

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public InventoryPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Vector2 MainRowOrigin(Vector2 panelOrigin) =>
        panelOrigin + new Vector2(0, SlotSize + SlotSpacing + EquipToMainGap);

    public static Rectangle GetMainSlotRect(int index, Vector2 panelOrigin)
    {
        var slotOrigin = MainRowOrigin(panelOrigin) + new Vector2(index * (SlotSize + SlotSpacing), 0);
        return new Rectangle((int)slotOrigin.X, (int)slotOrigin.Y, SlotSize, SlotSize);
    }

    public static Rectangle GetHoldStripRect(int index, Vector2 panelOrigin)
    {
        var slotRect = GetMainSlotRect(index, panelOrigin);
        return new Rectangle(slotRect.X, slotRect.Bottom + StripGap, SlotSize, StripHeight);
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 origin)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        if (me?.Inventory is not { } inventory)
            return;

        for (var i = 0; i < EquipSlots.Length; i++)
        {
            var (id, label) = EquipSlots[i];
            var item = inventory.Equipped.TryGetValue(id, out var equipped) ? equipped : null;
            var slotOrigin = origin + new Vector2(i * (SlotSize + SlotSpacing), 0);
            DrawSlot(spriteBatch, slotOrigin, item, label);
        }

        for (var i = 0; i < inventory.MainSlots.Count; i++)
        {
            var rect = GetMainSlotRect(i, origin);
            var item = inventory.MainSlots[i];
            DrawSlot(spriteBatch, new Vector2(rect.X, rect.Y), item, string.Empty);

            if (item is { } itemType && ItemDefinitions.IsHoldable(itemType))
            {
                var held = inventory.HeldMainSlotIndices.Contains(i);
                var stripRect = GetHoldStripRect(i, origin);
                spriteBatch.Draw(_pixel, stripRect, held ? Color.LimeGreen : Color.DarkGoldenrod);
            }
        }
    }

    private void DrawSlot(SpriteBatch spriteBatch, Vector2 origin, ItemType? item, string emptyLabel)
    {
        var rect = new Rectangle((int)origin.X, (int)origin.Y, SlotSize, SlotSize);
        spriteBatch.Draw(_pixel, rect, Color.DimGray * 0.5f);
        DrawRectOutline(spriteBatch, rect, Color.LightGray, 1);

        var (fill, letter) = item switch
        {
            { } type => (ItemColor(type), ItemDefinitions.ShortLabel(type)),
            null => (Color.Transparent, emptyLabel),
        };

        if (item is not null)
        {
            const int margin = 3;
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), fill);
        }

        if (letter.Length > 0)
            spriteBatch.DrawString(_font, letter, origin + new Vector2(4, 5), item is null ? Color.Gray : Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private static Color ItemColor(ItemType type) => type switch
    {
        ItemType.AmmoCrate => Color.SaddleBrown,
        ItemType.Spacesuit => Color.CadetBlue,
        ItemType.Knife or ItemType.Rifle or ItemType.LaserRifle => Color.DarkRed,
        _ => Color.DarkKhaki, // Wrench, Screwdriver, WeldingTool, Cutter
    };

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
