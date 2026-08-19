namespace SpaceAdventure.Shared.Protocol;

// Equip: Index is (int)EquipSlot. BeltBag: Index is 0..5 into Inventory.BeltBagSlots, only
// reachable while a BeltBag is actually worn (World.Storage.cs's IsSlotReachable).
public enum ItemSlotKind { Main, Rack, Equip, BeltBag }

// Names one item slot for a drag-and-drop move: a slot in the character's own carried row, one on
// the ship's storage rack, a worn equipment slot, or a slot inside a worn belt bag. Deliberately
// one type for all four, so the move command is a single from/to pair rather than a separate
// command per container combination.
public readonly record struct SlotRef(ItemSlotKind Kind, int Index);
