using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Barotrauma-style inventory HUD (game_design.md section 13): a fixed row of general slots —
// hard cap on carried items — plus separate equipment slots that don't compete with it for space
// (headset/clothing/headwear). Below each main slot sits a thin "hold" strip, exactly like
// Barotrauma's: clicking it equips/unequips that item into a hand.
//
// The two groups are placed independently by the caller (Game1): the carried row sits centred
// along the bottom edge, where the eye goes for the thing you use constantly, and the equipment
// slots live in the bottom-right corner — they change once an hour, so they shouldn't sit in the
// middle of the screen crowding the row that changes every few seconds.
public sealed class InventoryPanel
{
    public const int SlotSize = 34;
    public const int SlotSpacing = 5;
    public const int StripHeight = 8;
    public const int StripGap = 2;

    // What a caller has to reserve for each group, so the origins below can be derived from a
    // screen edge instead of hand-tuned numbers that drift apart from the slots they position.
    public const int RowHeight = SlotSize + StripGap + StripHeight;
    public static int RowWidth(int slotCount) => slotCount * (SlotSize + SlotSpacing) - SlotSpacing;
    public static int EquipRowWidth => RowWidth(EquipSlots.Length);

    internal static readonly (EquipSlot Id, string Label)[] EquipSlots =
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

    public static Rectangle GetSlotRect(int index, Vector2 rowOrigin)
    {
        var slotOrigin = rowOrigin + new Vector2(index * (SlotSize + SlotSpacing), 0);
        return new Rectangle((int)slotOrigin.X, (int)slotOrigin.Y, SlotSize, SlotSize);
    }

    public static Rectangle GetMainSlotRect(int index, Vector2 rowOrigin) => GetSlotRect(index, rowOrigin);

    public static Rectangle GetHoldStripRect(int index, Vector2 rowOrigin)
    {
        var slotRect = GetMainSlotRect(index, rowOrigin);
        return new Rectangle(slotRect.X, slotRect.Bottom + StripGap, SlotSize, StripHeight);
    }

    // The socket an oxygen tank plugs into, hanging under the slot of whatever carries one - a suit
    // or a cutter (OxygenTankDefinitions). Drawn under the hold strip so it never covers it.
    public const int SocketSize = 20;

    // above: the equipment slots sit on the very bottom edge of the screen, so their socket has to
    // hang over the slot instead of under it - drawn below, the suit's socket fell off the screen
    // entirely, which meant a tank could never be put into a worn suit at all.
    public static Rectangle GetSocketRect(Rectangle slotRect, bool above = false) =>
        new(slotRect.X + (slotRect.Width - SocketSize) / 2,
            above ? slotRect.Y - SocketSize - 4 : slotRect.Bottom + StripGap + StripHeight + 3,
            SocketSize, SocketSize);

    // hoveredMainSlotIndex: which row slot's tool socket to reveal this frame (Game1's
    // HoveredToolSlotIndex) - a cutter or welding tool's socket stays hidden until the mouse is
    // over its slot (or the socket band that then appears above it), so the row doesn't show a
    // socket under every tool all the time.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 rowOrigin, Vector2 equipOrigin, int? hoveredMainSlotIndex = null)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        if (me?.Inventory is not { } inventory)
            return;

        for (var i = 0; i < EquipSlots.Length; i++)
        {
            var (id, label) = EquipSlots[i];
            var item = inventory.Equipped.TryGetValue(id, out var equipped) ? equipped : null;
            var rect = GetSlotRect(i, equipOrigin);
            DrawSlot(spriteBatch, _pixel, _font, rect, item, label);
            if (item is { } worn && TankSockets.HasSocket(worn))
                DrawSocket(spriteBatch, GetSocketRect(rect, above: true), inventory.WornSuitTank);
        }

        for (var i = 0; i < inventory.MainSlots.Count; i++)
        {
            var rect = GetMainSlotRect(i, rowOrigin);
            var item = inventory.MainSlots[i];
            DrawSlot(spriteBatch, _pixel, _font, rect, item, string.Empty);

            if (item is { } itemType && ItemDefinitions.IsHoldable(itemType))
            {
                var held = inventory.HeldMainSlotIndices.Contains(i);
                DrawHands(spriteBatch, _pixel, rect, ItemDefinitions.HandsRequired(itemType), held);
                var stripRect = GetHoldStripRect(i, rowOrigin);
                spriteBatch.Draw(_pixel, stripRect, held ? Color.LimeGreen : Color.DarkGoldenrod);
            }

            if (item is { } socketed && TankSockets.HasSocket(socketed) && hoveredMainSlotIndex == i)
                DrawSocket(spriteBatch, GetSocketRect(rect, above: true), inventory.MainSlotTanks[i]);
        }
    }

    // Empty socket: an outlined hole. Filled: a bar of what's left in the bottle, which is the only
    // number that matters when you are outside.
    private void DrawSocket(SpriteBatch spriteBatch, Rectangle rect, float? charge)
    {
        spriteBatch.Draw(_pixel, rect, Color.Black * 0.6f);
        DrawRectOutline(spriteBatch, _pixel, rect, charge is null ? Color.DimGray : Color.CadetBlue, 1);
        if (charge is not { } left)
            return;

        var fraction = MathHelper.Clamp(left / OxygenTankDefinitions.FullCharge, 0f, 1f);
        var height = System.Math.Max(1, (int)((rect.Height - 6) * fraction));
        var color = fraction > 0.3f ? Color.CadetBlue : Color.OrangeRed;
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + 3, rect.Bottom - 3 - height, rect.Width - 6, height), color);
    }

    // internal + static so RackPanel draws its 30 shelves through the exact same slot, and so
    // Game1 can draw the item currently under the cursor mid-drag without a panel at all.
    internal static void DrawSlot(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle rect, ItemType? item, string emptyLabel)
    {
        var origin = new Vector2(rect.X, rect.Y);
        spriteBatch.Draw(pixel, rect, Color.DimGray * 0.5f);
        DrawRectOutline(spriteBatch, pixel, rect, Color.LightGray, 1);

        if (item is { } pictured && ItemIcons.HasIcon(pictured))
        {
            const int margin = 3;
            ItemIcons.Draw(spriteBatch, pixel, pictured, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2));
            return;
        }

        var (fill, letter) = item switch
        {
            { } type => (ItemColor(type), ItemDefinitions.ShortLabel(type)),
            null => (Color.Transparent, emptyLabel),
        };

        if (item is not null)
        {
            const int margin = 4;
            spriteBatch.Draw(pixel, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), fill);
        }

        // Centred rather than pinned to the corner - the slots are big enough now that a label
        // hugging the top-left would read as a stray mark instead of the item's name.
        if (letter.Length > 0)
        {
            var size = font.MeasureString(letter) * 0.7f;
            var textOrigin = origin + new Vector2((rect.Width - size.X) / 2f, (rect.Height - size.Y) / 2f);
            spriteBatch.DrawString(font, letter, textOrigin, item is null ? Color.Gray : Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }
    }

    private const int HandWidth = 8;
    private const int HandHeight = 9;

    // Barotrauma's hand indicators: how many hands this item takes to use is drawn in its own slot,
    // one glyph for a one-handed tool and two for something like the welding rig or a rifle. Dim
    // while the item is just carried, lit once it's actually in hand — so "why can't I pick up the
    // cutter" answers itself when the two-handed welder next to it is showing both hands lit.
    // internal so ShipRenderer's held-item chip (always "held" by definition, so always lit) draws
    // the exact same hands gripping the item in-world, not just in the hotbar slot.
    internal static void DrawHands(SpriteBatch spriteBatch, Texture2D pixel, Rectangle slot, int handsRequired, bool held)
    {
        var color = held ? Color.LimeGreen : Color.Gainsboro * 0.4f;
        var y = slot.Bottom - HandHeight - 2;

        if (handsRequired >= 2)
        {
            DrawHand(spriteBatch, pixel, new Rectangle(slot.X + 3, y, HandWidth, HandHeight), color, mirrored: false);
            DrawHand(spriteBatch, pixel, new Rectangle(slot.Right - HandWidth - 3, y, HandWidth, HandHeight), color, mirrored: true);
        }
        else
        {
            DrawHand(spriteBatch, pixel, new Rectangle(slot.X + (slot.Width - HandWidth) / 2, y, HandWidth, HandHeight), color, mirrored: false);
        }
    }

    // A palm block, three finger nubs above it and a thumb off one side - about the least that still
    // reads as a hand at 8x9 pixels, built from the same single white pixel as everything else here.
    private static void DrawHand(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, Color color, bool mirrored)
    {
        const int palmHeight = 4;
        var palm = new Rectangle(box.X + 1, box.Bottom - palmHeight, box.Width - 2, palmHeight);
        spriteBatch.Draw(pixel, palm, color);

        var fingerHeight = box.Height - palmHeight - 1;
        for (var f = 0; f < 3; f++)
            spriteBatch.Draw(pixel, new Rectangle(palm.X + f * 2 + 1, palm.Y - fingerHeight, 1, fingerHeight), color);

        var thumbX = mirrored ? box.Right - 1 : box.X;
        spriteBatch.Draw(pixel, new Rectangle(thumbX, palm.Y + 1, 1, palmHeight - 1), color);
    }

    // The item riding the cursor while a drag is in flight - same square as a slot's contents, no
    // slot frame, so it reads as "picked up" rather than "in a slot that moved".
    internal static void DrawDraggedItem(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Point cursor, ItemType item)
    {
        var rect = new Rectangle(cursor.X - SlotSize / 2, cursor.Y - SlotSize / 2, SlotSize, SlotSize);
        const int margin = 4;
        DrawRectOutline(spriteBatch, pixel, rect, Color.White, 1);

        if (ItemIcons.HasIcon(item))
        {
            ItemIcons.Draw(spriteBatch, pixel, item, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2));
            return;
        }

        spriteBatch.Draw(pixel, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), ItemColor(item) * 0.9f);
        var label = ItemDefinitions.ShortLabel(item);
        var size = font.MeasureString(label) * 0.7f;
        spriteBatch.DrawString(font, label, new Vector2(rect.X + (rect.Width - size.X) / 2f, rect.Y + (rect.Height - size.Y) / 2f), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    // internal so ShipRenderer's held-item icon (drawn beside a character, not in a panel slot at
    // all) uses the exact same colour a player already knows that item by from their own inventory.
    internal static Color ItemColor(ItemType type) => type switch
    {
        ItemType.AmmoCrate => Color.SaddleBrown,
        ItemType.Spacesuit => Color.CadetBlue,
        ItemType.Knife or ItemType.Rifle or ItemType.LaserRifle => Color.DarkRed,
        ItemType.MedKit => Color.Crimson,
        _ => Color.DarkKhaki, // Wrench, Screwdriver, WeldingTool, Cutter
    };

    private static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
