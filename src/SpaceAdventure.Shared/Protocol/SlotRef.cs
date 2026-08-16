namespace SpaceAdventure.Shared.Protocol;

public enum ItemSlotKind { Main, Rack }

// Names one item slot for a drag-and-drop move: either a slot in the character's own carried row
// or one on the ship's storage rack. Deliberately one type for both, so the move command is a
// single from/to pair rather than a separate command per container combination.
public readonly record struct SlotRef(ItemSlotKind Kind, int Index);
