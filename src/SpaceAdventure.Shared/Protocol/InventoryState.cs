using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

public sealed record InventoryState(
    IReadOnlyList<ItemType?> MainSlots,
    IReadOnlyDictionary<EquipSlot, ItemType?> Equipped,
    IReadOnlyList<int> HeldMainSlotIndices);
