using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// The ship's storage rack (game_design.md section 13) plus the one operation that moves an item
// between any two slots - the character's carried row and the rack are the same kind of thing as
// far as a drag is concerned, so there's one move rule rather than "stow", "retrieve" and
// "rearrange" as three separate commands.
public sealed partial class World
{
    private ItemType?[] _rackSlots = new ItemType?[StorageRack.Capacity];

    public IReadOnlyList<ItemType?> RackSlots => _rackSlots;

    // Deliberately survives a change of hull (World.ShipPurchase.cs): the cargo is the crew's, and
    // silently binning it because they traded up would be a nasty surprise. Only a fresh World
    // starts with an empty rack.
    public void LoadRackSlots(IReadOnlyList<ItemType?> slots)
    {
        _rackSlots = new ItemType?[StorageRack.Capacity];
        for (var i = 0; i < Math.Min(slots.Count, StorageRack.Capacity); i++)
            _rackSlots[i] = slots[i];
    }

    // Swaps the two slots' contents (moving onto an empty slot is just a swap with null). The rack
    // end of the move requires standing at the rack - it's a physical shelf, not a pocket
    // dimension - while shuffling your own row works anywhere.
    private void TryMoveItem(Character character, SlotRef from, SlotRef to)
    {
        if (from == to)
            return;
        if (!IsSlotReachable(character, from) || !IsSlotReachable(character, to))
            return;

        var source = ReadSlot(character, from);
        if (source is null)
            return; // nothing to drag

        // The shelf stores item types and nothing else, so anything with a tank in it has to be
        // unplugged before it goes on the shelf - and if there's nowhere to put the tank, the move
        // doesn't happen. Losing a tank inside a shelved cutter would be a silent theft.
        if (from.Kind == ItemSlotKind.Main && to.Kind == ItemSlotKind.Rack &&
            character.Inventory.TankCharge(from.Index) is not null &&
            !character.Inventory.TryDetachTank(from.Index))
            return;

        var destination = ReadSlot(character, to);
        if (!WriteSlot(character, to, source) || !WriteSlot(character, from, destination))
            return;

        // A tank travels with the thing it is plugged into, as long as both ends are the row.
        if (from.Kind == ItemSlotKind.Main && to.Kind == ItemSlotKind.Main)
            (character.Inventory.MainSlotTanks[to.Index], character.Inventory.MainSlotTanks[from.Index]) =
                (character.Inventory.MainSlotTanks[from.Index], character.Inventory.MainSlotTanks[to.Index]);

        // Anything that moved leaves the character's hands: the held-hand list is keyed by slot
        // index, and quietly re-pointing it at whatever landed in that slot would be worse than
        // just putting the item down.
        if (from.Kind == ItemSlotKind.Main)
            character.Inventory.HeldSlotIndices.Remove(from.Index);
        if (to.Kind == ItemSlotKind.Main)
            character.Inventory.HeldSlotIndices.Remove(to.Index);
    }

    private bool IsSlotReachable(Character character, SlotRef slot) => slot.Kind switch
    {
        ItemSlotKind.Main => slot.Index >= 0 && slot.Index < Inventory.MainSlotCount &&
                             !character.OnEnemyShip && !character.IsOutside,
        ItemSlotKind.Rack => slot.Index >= 0 && slot.Index < StorageRack.Capacity &&
                             !character.OnStation && !character.OnEnemyShip && !character.IsOutside &&
                             (Ship.StorageRack.Position - character.Position).Length() < InteractionRadius,
        _ => false,
    };

    private ItemType? ReadSlot(Character character, SlotRef slot) => slot.Kind == ItemSlotKind.Main
        ? character.Inventory.MainSlots[slot.Index]
        : _rackSlots[slot.Index];

    private bool WriteSlot(Character character, SlotRef slot, ItemType? item)
    {
        if (slot.Kind == ItemSlotKind.Main)
            character.Inventory.MainSlots[slot.Index] = item;
        else
            _rackSlots[slot.Index] = item;
        return true;
    }
}
