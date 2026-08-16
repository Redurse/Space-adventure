using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// MainSlotTanks runs parallel to MainSlots: the charge of the oxygen tank socketed into whatever is
// in that slot, or null for "nothing socketed". For a slot holding a tank itself, it is that tank's
// own charge - a slot holds an item type rather than an item, so the one piece of per-item state
// the game needs travels beside it (OxygenTankDefinitions).
public sealed record InventoryState(
    IReadOnlyList<ItemType?> MainSlots,
    IReadOnlyDictionary<EquipSlot, ItemType?> Equipped,
    IReadOnlyList<int> HeldMainSlotIndices,
    IReadOnlyList<float?> MainSlotTanks,
    float? WornSuitTank);
