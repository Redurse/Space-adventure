using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Barotrauma-style personal inventory (game_design.md section 13): one fixed row of slots —
// a hard cap on carried items, no stacking — plus separate equipment slots that don't compete
// for row space (headset/clothing/headwear).
public sealed class Inventory
{
    public const int MainSlotCount = 9;

    public ItemType?[] MainSlots { get; } = new ItemType?[MainSlotCount];
    public Dictionary<EquipSlot, ItemType?> Equipped { get; } = new()
    {
        [EquipSlot.Headset] = null,
        [EquipSlot.Clothing] = null,
        [EquipSlot.Headwear] = null,
    };

    // Order matters: the earliest-held item is the first one auto-dropped to make room for a
    // newly-equipped item that needs more hands than are currently free (Barotrauma-style).
    public List<int> HeldSlotIndices { get; } = new();

    private int HandsInUse => HeldSlotIndices.Sum(i => ItemDefinitions.HandsRequired(MainSlots[i]!.Value));

    public bool Has(ItemType type) => Array.IndexOf(MainSlots, (ItemType?)type) >= 0;

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
        return true;
    }

    public bool TryRemove(ItemType type)
    {
        var index = Array.IndexOf(MainSlots, (ItemType?)type);
        if (index < 0)
            return false;

        MainSlots[index] = null;
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
        HeldSlotIndices.Remove(index);
        return true;
    }
}
