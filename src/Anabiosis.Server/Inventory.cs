using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Barotrauma-style personal inventory (game_design.md section 13): one fixed row of slots —
// a hard cap on carried items, no stacking — plus separate equipment slots that don't compete
// for row space (headset/clothing/headwear).
public sealed class Inventory
{
    public const int MainSlotCount = 10;

    public ItemType?[] MainSlots { get; } = new ItemType?[MainSlotCount];
    public Dictionary<EquipSlot, ItemType?> Equipped { get; } = new()
    {
        [EquipSlot.Headset] = null,
        [EquipSlot.Suit] = null,
        [EquipSlot.Clothing] = null,
        [EquipSlot.Headwear] = null,
        [EquipSlot.BeltBag] = null,
        [EquipSlot.IdCard] = null,
    };

    // A worn BeltBag's own small sub-inventory (World.Storage.cs's TryMoveItem gates every slot
    // here on Equipped[EquipSlot.BeltBag] actually being a BeltBag) - 2x3, the same shape shown in
    // the popup that opens above the bag's own icon when the mouse hovers it.
    public const int BeltBagSlotCount = 6;
    public ItemType?[] BeltBagSlots { get; } = new ItemType?[BeltBagSlotCount];

    // Order matters: the earliest-held item is the first one auto-dropped to make room for a
    // newly-equipped item that needs more hands than are currently free (Barotrauma-style).
    public List<int> HeldSlotIndices { get; } = new();

    // The charge of an oxygen tank socketed into whatever is in that slot, or null for "no tank".
    // A slot still holds an item *type* rather than an item, so the one piece of per-item state the
    // game now needs lives here, alongside the slot, instead of turning every item into an object.
    public float?[] MainSlotTanks { get; } = new float?[MainSlotCount];
    public Dictionary<EquipSlot, float?> EquippedTanks { get; } = new()
    {
        [EquipSlot.Headset] = null,
        [EquipSlot.Suit] = null,
        [EquipSlot.Headwear] = null,
    };

    // Worn suit: index -1 stands for "the suit slot" everywhere a socket is addressed, so one
    // set of methods covers a cutter in the row and the suit on your back.
    public const int WornSuitSlot = -1;

    public float? TankCharge(int slotIndex) =>
        slotIndex == WornSuitSlot ? EquippedTanks[EquipSlot.Suit] : MainSlotTanks[slotIndex];

    private void SetTank(int slotIndex, float? charge)
    {
        if (slotIndex == WornSuitSlot)
            EquippedTanks[EquipSlot.Suit] = charge;
        else
            MainSlotTanks[slotIndex] = charge;
    }

    private ItemType? SocketedItem(int slotIndex) =>
        slotIndex == WornSuitSlot ? Equipped[EquipSlot.Suit] : ItemAt(slotIndex);

    public bool HasWorkingTank(int slotIndex) => TankCharge(slotIndex) > 0f;

    // Moving a tank out of the row and into a socket. Both ends have to be real: a tank in hand or
    // in the row, and something with a socket that hasn't already got one - and the tank has to be
    // the kind that socket actually takes (TankSockets): a welding tank offered to a cutter is
    // refused the same as an empty hand would be.
    public bool TryAttachTank(int sourceSlotIndex, int targetSlotIndex)
    {
        if (sourceSlotIndex < 0 || sourceSlotIndex >= MainSlotCount ||
            MainSlots[sourceSlotIndex] is not { } sourceItem || !TankSockets.IsTank(sourceItem))
            return false;
        if (targetSlotIndex != WornSuitSlot && (targetSlotIndex < 0 || targetSlotIndex >= MainSlotCount))
            return false;
        if (SocketedItem(targetSlotIndex) is not { } target || TankSockets.AcceptedTank(target) != sourceItem)
            return false;
        if (TankCharge(targetSlotIndex) is not null)
            return false;

        SetTank(targetSlotIndex, MainSlotTanks[sourceSlotIndex] ?? TankSockets.FullChargeOf(sourceItem));
        MainSlots[sourceSlotIndex] = null;
        MainSlotTanks[sourceSlotIndex] = null;
        HeldSlotIndices.Remove(sourceSlotIndex);
        return true;
    }

    // Back out of the socket into the row - including an empty one, which is what makes room for a
    // fresh tank. Which item type comes back out is read off the socket's owner, not assumed.
    public bool TryDetachTank(int slotIndex)
    {
        if (slotIndex != WornSuitSlot && (slotIndex < 0 || slotIndex >= MainSlotCount))
            return false;
        if (TankCharge(slotIndex) is not { } charge)
            return false;
        if (SocketedItem(slotIndex) is not { } owner || TankSockets.AcceptedTank(owner) is not { } tankType)
            return false;

        var freeIndex = Array.IndexOf(MainSlots, null);
        if (freeIndex < 0)
            return false;

        MainSlots[freeIndex] = tankType;
        MainSlotTanks[freeIndex] = charge;
        SetTank(slotIndex, null);
        return true;
    }

    public void RefillTank(int slotIndex)
    {
        if (TankCharge(slotIndex) is null)
            return;
        // The slot might hold the tank itself (a spare riding loose in the row) rather than a tool
        // it's plugged into - AcceptedTank falls through to the item itself in that case.
        var item = SocketedItem(slotIndex);
        if (item is null)
            return;
        SetTank(slotIndex, TankSockets.FullChargeOf(TankSockets.AcceptedTank(item.Value) ?? item.Value));
    }

    // Burns oxygen out of a socket; returns false once it's dry, which is what every user of a tank
    // treats as "no tank at all".
    public bool DrainTank(int slotIndex, float amount)
    {
        if (TankCharge(slotIndex) is not { } charge || charge <= 0f)
            return false;

        SetTank(slotIndex, Math.Max(0f, charge - amount));
        return true;
    }

    // The row slot of the first held item of this type - what the tank rules need, since the socket
    // belongs to that particular slot rather than to the item type.
    public int HeldSlotOf(ItemType type) => HeldSlotIndices.FirstOrDefault(i => MainSlots[i] == type, -1);

    private int HandsInUse => HeldSlotIndices.Sum(i => ItemDefinitions.HandsRequired(MainSlots[i]!.Value));

    public bool Has(ItemType type) => Array.IndexOf(MainSlots, (ItemType?)type) >= 0;

    public ItemType? ItemAt(int slotIndex) =>
        slotIndex >= 0 && slotIndex < MainSlotCount ? MainSlots[slotIndex] : null;

    public bool IsHolding(ItemType type) => HeldSlotIndices.Any(i => MainSlots[i] == type);

    // Toggles whether the item in a main slot is held in hand — the server-side counterpart to
    // clicking the hold strip under an inventory slot. No-ops on an empty/non-holdable slot.
    public void ToggleHold(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MainSlotCount)
            return;

        var item = MainSlots[slotIndex];
        if (item is null || !ItemDefinitions.IsHoldable(item.Value))
            return;

        if (HeldSlotIndices.Contains(slotIndex))
        {
            HeldSlotIndices.Remove(slotIndex);
            return;
        }

        var required = ItemDefinitions.HandsRequired(item.Value);
        while (HandsInUse + required > 2 && HeldSlotIndices.Count > 0)
            HeldSlotIndices.RemoveAt(0);

        HeldSlotIndices.Add(slotIndex);
    }

    public bool TryAdd(ItemType type)
    {
        var freeIndex = Array.IndexOf(MainSlots, null);
        if (freeIndex < 0)
            return false; // row full — matches the hard carry limit from the design doc

        MainSlots[freeIndex] = type;
        // A tank picked up off a rack is a full one; anything else arrives with an empty socket.
        MainSlotTanks[freeIndex] = TankSockets.IsTank(type) ? TankSockets.FullChargeOf(type) : null;
        return true;
    }

    public bool TryRemove(ItemType type)
    {
        var index = Array.IndexOf(MainSlots, (ItemType?)type);
        if (index < 0)
            return false;

        MainSlots[index] = null;
        MainSlotTanks[index] = null;
        return true;
    }

    // Like TryRemove, but by slot index rather than item type — used when selling a specific
    // slot to the trader (game_design.md section 6, M10 economy), where the caller already knows
    // which slot was clicked rather than which item it holds.
    public bool TryRemoveAt(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MainSlotCount || MainSlots[slotIndex] is null)
            return false;

        MainSlots[slotIndex] = null;
        MainSlotTanks[slotIndex] = null;
        HeldSlotIndices.Remove(slotIndex);
        return true;
    }

    // Like TryRemove, but only takes an item that's actually held in hand right now (e.g.
    // loading a fuel rod into the reactor) — clears both the slot and its held-hand entry.
    public bool TryTakeHeldItem(ItemType type)
    {
        var index = HeldSlotIndices.FirstOrDefault(i => MainSlots[i] == type, -1);
        if (index < 0)
            return false;

        MainSlots[index] = null;
        MainSlotTanks[index] = null;
        HeldSlotIndices.Remove(index);
        return true;
    }
}
