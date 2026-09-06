namespace Anabiosis.Shared.Model;

// Which EquipSlot a worn item is allowed into (World.Storage.cs's TryMoveItem) - deliberately
// excludes Spacesuit, since EquipSlot.Suit is reachable only through the suit-locker's own timed
// equip/unequip action (World.Movement.cs), never through this generic drag-and-drop path.
public static class EquipSlotDefinitions
{
    public static EquipSlot? SlotFor(ItemType type) => type switch
    {
        ItemType.BeltBag => EquipSlot.BeltBag,
        ItemType.IdCard => EquipSlot.IdCard,
        _ => null,
    };
}
