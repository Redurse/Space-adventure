using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// MainSlotTanks runs parallel to MainSlots: the charge of the oxygen tank socketed into whatever is
// in that slot, or null for "nothing socketed". For a slot holding a tank itself, it is that tank's
// own charge - a slot holds an item type rather than an item, so the one piece of per-item state
// the game needs travels beside it (OxygenTankDefinitions).
public sealed record InventoryState(
    IReadOnlyList<ItemType?> MainSlots,
    IReadOnlyDictionary<EquipSlot, ItemType?> Equipped,
    IReadOnlyList<int> HeldMainSlotIndices,
    IReadOnlyList<float?> MainSlotTanks,
    float? WornSuitTank,
    // A worn BeltBag's own small sub-inventory (Inventory.BeltBagSlots) - always sent, empty or
    // not, the same way MainSlots always sends all 10 regardless of how many are actually full.
    IReadOnlyList<ItemType?> BeltBagSlots);
