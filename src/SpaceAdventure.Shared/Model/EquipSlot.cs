namespace SpaceAdventure.Shared.Model;

// The equipment slots ahead of the main inventory row (game_design.md section 13), Barotrauma-
// style. Suit is what the suit-locker equip/unequip flow (World.Movement.cs's SuitActionEquipping)
// actually fills - kept off the generic drag-and-drop path entirely (World.Storage.cs's
// IsSlotReachable) so a player can't skip the timed "putting it on" action by just dragging the
// suit in. Clothing/Headwear are shown for the same reason Barotrauma's own row has them, but
// nothing in the game wears either yet - EquipSlotDefinitions has no ItemType mapped to them, so
// they simply refuse every drop until a real garment item exists. BeltBag/IdCard are the two new
// ones that actually do something (BeltBag opens Inventory.BeltBagSlots; IdCard just sits there).
public enum EquipSlot
{
    Headset,
    Suit,
    Clothing,
    Headwear,
    BeltBag,
    IdCard,
}
