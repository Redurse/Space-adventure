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
    // Bumped from 34 - the new tool/tank icons (ItemIcons) pack a fair bit of shape into one slot;
    // this buys them real extra pixels rather than just a legibility-floor hack on top of the same
    // cramped size. RackPanel's own grid derives its width from this too and still comfortably fits
    // DesignWidth at 10 columns.
    public const int SlotSize = 44;
    public const int SlotSpacing = 5;
    public const int StripHeight = 8;
    public const int StripGap = 2;

    // What a caller has to reserve for each group, so the origins below can be derived from a
    // screen edge instead of hand-tuned numbers that drift apart from the slots they position.
    public const int RowHeight = SlotSize + StripGap + StripHeight;
    public static int RowWidth(int slotCount) => slotCount * (SlotSize + SlotSpacing) - SlotSpacing;
    public static int EquipRowWidth => RowWidth(EquipSlots.Length);

    // Suit is shown here (and renders whatever's worn) but isn't a drag target - it's filled only
    // through the suit-locker's own timed equip/unequip action (World.Storage.cs's
    // IsSlotReachable excludes it from the generic move path on purpose). Clothing/Headwear stay
    // in the row for the same reason Barotrauma's own has them, even though nothing in the game
    // wears either yet - EquipSlotDefinitions has no item mapped to them, so they just always
    // refuse a drop until a real garment exists.
    internal static readonly (EquipSlot Id, string Label)[] EquipSlots =
    {
        (EquipSlot.BeltBag, "Сум"),
        (EquipSlot.Suit, "С"),
        (EquipSlot.Clothing, "О"),
        (EquipSlot.Headwear, "Г"),
        (EquipSlot.Headset, "Н"),
        (EquipSlot.IdCard, "ID"),
    };

    // 2 columns x 3 rows, opening upward above the worn BeltBag's own icon (game_design.md
    // section 13) - the exact shape asked for, matching Barotrauma's own belt/tool-bag popup.
    public const int BeltBagColumns = 2;
    public const int BeltBagRows = 3;
    private const int BeltBagPopupGap = 10;

    public static Rectangle GetBeltBagSlotRect(int index, Rectangle bagIconRect)
    {
        var gridWidth = BeltBagColumns * (SlotSize + SlotSpacing) - SlotSpacing;
        var gridHeight = BeltBagRows * (SlotSize + SlotSpacing) - SlotSpacing;
        var gridLeft = bagIconRect.X + (bagIconRect.Width - gridWidth) / 2;
        var gridTop = bagIconRect.Y - BeltBagPopupGap - gridHeight;
        var col = index % BeltBagColumns;
        var row = index / BeltBagColumns;
        return new Rectangle(gridLeft + col * (SlotSize + SlotSpacing), gridTop + row * (SlotSize + SlotSpacing), SlotSize, SlotSize);
    }

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

    // Above the slot rather than below it - the row sits flush on the bottom screen edge
    // (Game1.InventoryRowOrigin anchors the *slot's* bottom to it now, not the strip's), so a strip
    // below would hang half off-screen. Above also puts it right next to the number badge's own
    // corner, both landing in the same glance instead of one being on the far edge of the slot.
    public static Rectangle GetHoldStripRect(int index, Vector2 rowOrigin)
    {
        var slotRect = GetMainSlotRect(index, rowOrigin);
        return new Rectangle(slotRect.X, slotRect.Y - StripGap - StripHeight, SlotSize, StripHeight);
    }

    // The socket an oxygen tank plugs into, hanging over the slot of whatever carries one - a suit
    // or a cutter (OxygenTankDefinitions).
    public const int SocketSize = 20;

    // above: hangs over the slot instead of under it - drawn below, the equip row's own socket
    // (right on the bottom screen edge) fell off-screen entirely, and a tank could never be put
    // into a worn suit at all. extraClearance pushes it further up clear of the hold strip, which
    // now occupies the band immediately above a *main-row* slot (equip slots have no strip of
    // their own, so they pass 0).
    public static Rectangle GetSocketRect(Rectangle slotRect, bool above = false, int extraClearance = 0) =>
        new(slotRect.X + (slotRect.Width - SocketSize) / 2,
            above ? slotRect.Y - SocketSize - 4 - extraClearance : slotRect.Bottom + StripGap + StripHeight + 3,
            SocketSize, SocketSize);

    // hoveredMainSlotIndex: which row slot's tool socket to reveal this frame (Game1's
    // HoveredToolSlotIndex) - a cutter or welding tool's socket stays hidden until the mouse is
    // over its slot (or the socket band that then appears above it), so the row doesn't show a
    // socket under every tool all the time.
    // showBeltBag: whether to reveal the worn bag's own 2x3 sub-inventory this frame (Game1's own
    // hover/drag-source check) - hidden the rest of the time so it doesn't sit permanently open
    // above the icon.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 rowOrigin, Vector2 equipOrigin, int? hoveredMainSlotIndex = null, bool showBeltBag = false)
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

            if (id == EquipSlot.BeltBag && item == ItemType.BeltBag && showBeltBag)
                for (var b = 0; b < inventory.BeltBagSlots.Count; b++)
                    DrawSlot(spriteBatch, _pixel, _font, GetBeltBagSlotRect(b, rect), inventory.BeltBagSlots[b], string.Empty);
        }

        for (var i = 0; i < inventory.MainSlots.Count; i++)
        {
            var rect = GetMainSlotRect(i, rowOrigin);
            var item = inventory.MainSlots[i];
            DrawSlot(spriteBatch, _pixel, _font, rect, item, string.Empty);
            DrawHotkeyBadge(spriteBatch, i, rect);

            var isHoldable = item is { } itemType && ItemDefinitions.IsHoldable(itemType);
            if (item is { } heldItemType && isHoldable)
            {
                var held = inventory.HeldMainSlotIndices.Contains(i);
                DrawHands(spriteBatch, _pixel, rect, ItemDefinitions.HandsRequired(heldItemType), held);
                var stripRect = GetHoldStripRect(i, rowOrigin);
                spriteBatch.Draw(_pixel, stripRect, held ? Color.LimeGreen : Color.DarkGoldenrod);
            }

            // A tool's tank always comes with a strip (every socketed item is also holdable), so
            // the hover socket needs to clear it now that the strip sits above the slot too.
            if (item is { } socketed && TankSockets.HasSocket(socketed) && hoveredMainSlotIndex == i)
                DrawSocket(spriteBatch, GetSocketRect(rect, above: true, extraClearance: isHoldable ? StripGap + StripHeight : 0), inventory.MainSlotTanks[i]);

            // Always-on charge readout for whatever tank this slot's own contents carry - the
            // welding tool/cutter's socketed tank, a bare tank sitting loose in the row, or the
            // suit's own bottle - same fraction the hover-only socket bar above already shows, just
            // visible at a glance without having to mouse over it first. Stays below the slot -
            // only the hold strip moved, this didn't need to.
            if (inventory.MainSlotTanks[i] is { } charge && TankTypeFor(item) is { } tankType)
                DrawChargeBar(spriteBatch, new Rectangle(rect.X, rect.Bottom + 2, SlotSize, ChargeBarHeight), charge / TankSockets.FullChargeOf(tankType));
        }
    }

    private const int ChargeBarHeight = 4;

    // The socket's own accepted tank type if this item has one, or the item itself if it's a bare
    // tank riding loose in the row (mirrors Inventory.RefillTank's identical fallback server-side).
    private static ItemType? TankTypeFor(ItemType? item) =>
        item is { } type ? TankSockets.AcceptedTank(type) ?? (TankSockets.IsTank(type) ? type : null) : null;

    private void DrawChargeBar(SpriteBatch spriteBatch, Rectangle rect, float fraction)
    {
        spriteBatch.Draw(_pixel, rect, Color.Black * 0.5f);
        var filled = new Rectangle(rect.X, rect.Y, System.Math.Max(1, (int)(rect.Width * MathHelper.Clamp(fraction, 0f, 1f))), rect.Height);
        var color = fraction > 0.6f ? Color.LimeGreen : fraction > 0.25f ? Color.Orange : Color.OrangeRed;
        spriteBatch.Draw(_pixel, filled, color);
    }

    private const int HotkeyBadgeSize = 15;

    // A folded-corner wedge in the slot's own top-left, the number tucked inside it - not a label
    // floating above the slot, so it rides along with the slot itself rather than needing its own
    // reserved strip of screen space.
    private void DrawHotkeyBadge(SpriteBatch spriteBatch, int index, Rectangle rect)
    {
        var apex = new Vector2(rect.X, rect.Y);
        Primitives.FillTriangle(spriteBatch, _pixel, apex, apex + new Vector2(HotkeyBadgeSize, 0), apex + new Vector2(0, HotkeyBadgeSize), Color.Black * 0.65f);

        var label = index < 9 ? (index + 1).ToString() : "0";
        spriteBatch.DrawString(_font, label, apex + new Vector2(2, 0), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
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
        ItemType.Knife or ItemType.Rifle or ItemType.LaserRifle or ItemType.Axe => Color.DarkRed,
        ItemType.MedKit => Color.Crimson,
        ItemType.BeltBag => Color.SaddleBrown,
        ItemType.IdCard => Color.SteelBlue,
        _ => Color.DarkKhaki, // Wrench, Screwdriver, WeldingTool, Cutter
    };

    private static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    // Full item info on hover: name (plus a tank's remaining charge, if this slot has one) and a
    // one-line description. Anchored so its bottom edge sits just above the slot - the row lives on
    // the bottom edge of the screen, so a tooltip growing downward from the cursor would usually
    // land off-screen entirely.
    public void DrawTooltip(SpriteBatch spriteBatch, ItemType item, float? tankCharge, Vector2 anchorAboveSlot)
    {
        var name = ItemDefinitions.DisplayName(item);
        var percent = tankCharge is { } charge && TankTypeFor(item) is { } tankType
            ? (int?)MathHelper.Clamp(charge / TankSockets.FullChargeOf(tankType) * 100f, 0f, 100f)
            : null;
        var description = ItemDescriptions.Describe(item);

        const float titleScale = 0.55f;
        const float bodyScale = 0.48f;
        var nameSize = _font.MeasureString(name) * titleScale;
        var percentText = percent is { } p ? $" ({p}%)" : "";
        var percentSize = _font.MeasureString(percentText) * titleScale;
        var descSize = description is not null ? _font.MeasureString(description) * bodyScale : Vector2.Zero;

        const float lineGap = 4f;
        var width = System.Math.Max(nameSize.X + percentSize.X, descSize.X) + 20f;
        var height = nameSize.Y + 16f + (description is not null ? descSize.Y + lineGap : 0f);

        var boxRect = new Rectangle((int)anchorAboveSlot.X, (int)(anchorAboveSlot.Y - height), (int)width, (int)height);
        PanelFrame.Draw(spriteBatch, _pixel, boxRect, thickness: 1);

        var textOrigin = new Vector2(boxRect.X + 10, boxRect.Y + 8);
        spriteBatch.DrawString(_font, name, textOrigin, Color.White, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        if (percent is not null)
            spriteBatch.DrawString(_font, percentText, textOrigin + new Vector2(nameSize.X, 0), Color.Orange, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        if (description is not null)
            spriteBatch.DrawString(_font, description, textOrigin + new Vector2(0, nameSize.Y + lineGap), Color.LightGray, 0f, Vector2.Zero, bodyScale, SpriteEffects.None, 0f);
    }
}
