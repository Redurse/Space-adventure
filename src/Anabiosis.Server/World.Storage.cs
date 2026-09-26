using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The ship's storage rack (game_design.md section 13) plus the one operation that moves an item
// between any two slots - the character's carried row and the rack are the same kind of thing as
// far as a drag is concerned, so there's one move rule rather than "stow", "retrieve" and
// "rearrange" as three separate commands.
public sealed partial class World
{
    private ItemType?[] _rackSlots = Array.Empty<ItemType?>();

    public IReadOnlyList<ItemType?> RackSlots => _rackSlots;

    // Direct user request ("уменьшить стартовые инструменты до 2 шт") - dropped from 3 to 2 units
    // each, freeing enough shelf space for RackMaterials below to actually fit (2 of every material
    // type needs 30 slots; at the old 3-per-type count only 9 slots were ever free).
    private static readonly ItemType[] RackToolsAndTanks =
    {
        ItemType.Wrench, ItemType.Wrench,
        ItemType.Screwdriver, ItemType.Screwdriver,
        ItemType.Cutter, ItemType.Cutter,
        ItemType.WeldingTool, ItemType.WeldingTool,
        ItemType.OxygenTank, ItemType.OxygenTank,
        ItemType.WeldingTank, ItemType.WeldingTank,
        ItemType.Axe, ItemType.Axe,
        ItemType.GoshaScrewdriver, ItemType.GoshaScrewdriver,
    };

    private static readonly ItemType[] RackSuppliesAndWeapons =
    {
        ItemType.FuelRod, ItemType.FuelRod,
        ItemType.MedKit, ItemType.MedKit,
        ItemType.WireSpool, ItemType.WireSpool,
        ItemType.Knife, ItemType.Knife,
        ItemType.Rifle, ItemType.Rifle,
        ItemType.LaserRifle, ItemType.LaserRifle,
        ItemType.BeltBag, ItemType.BeltBag,
        ItemType.IdCard, ItemType.IdCard,
        // Direct user request - radio voice needs a worn item now (ItemType.Radio's own doc
        // comment), so the starter kit has to actually carry some or nobody could ever use it.
        ItemType.Radio, ItemType.Radio,
    };

    // Direct user request ("выдавай в стелажах по 2 материала каждого типа") - 2 units of every ore/
    // refined material added this session (ItemType.cs's own Materials region), same enum-declaration
    // order used everywhere else that lists them (ItemDefinitions.IsRawOre, ItemIcons.Materials.cs).
    private static readonly ItemType[] RackMaterials =
    {
        ItemType.IronOre, ItemType.IronOre,
        ItemType.NickelOre, ItemType.NickelOre,
        ItemType.ZincOre, ItemType.ZincOre,
        ItemType.CopperOre, ItemType.CopperOre,
        ItemType.TitaniumOre, ItemType.TitaniumOre,
        ItemType.UraniumOre, ItemType.UraniumOre,
        ItemType.Silicon, ItemType.Silicon,
        ItemType.Carbon, ItemType.Carbon,
        ItemType.AluminumOre, ItemType.AluminumOre,
        ItemType.PlastalloyOre, ItemType.PlastalloyOre,
        ItemType.SteelPlate, ItemType.SteelPlate,
        ItemType.TitaniumPlate, ItemType.TitaniumPlate,
        ItemType.CopperCable, ItemType.CopperCable,
        ItemType.Plastic, ItemType.Plastic,
        ItemType.Plastalloy, ItemType.Plastalloy,
    };

    // Every hull's starter kit (game_design.md section 13): 2 units of every hand tool/tank/weapon/
    // consumable that used to be scattered across the ship as individual ToolStation pickups, split
    // evenly across the two shelves every hull carries - tools+tanks in the first, supplies+weapons
    // in the second, regardless of which rooms those two shelves happen to sit in on this hull.
    // Called from InitializeShipState (constructor + every ship purchase), same as
    // InitializeComponentMounts - a bought hull's shelves start full again, not carrying over
    // whatever the previous hull's shelves happened to hold.
    private void InitializeRackSlots()
    {
        _rackSlots = new ItemType?[Ship.StorageRacks.Count * StorageRack.Capacity];
        for (var i = 0; i < RackToolsAndTanks.Length; i++)
            _rackSlots[i] = RackToolsAndTanks[i];

        if (Ship.StorageRacks.Count > 1)
        {
            var secondShelfOffset = StorageRack.Capacity;
            for (var i = 0; i < RackSuppliesAndWeapons.Length; i++)
                _rackSlots[secondShelfOffset + i] = RackSuppliesAndWeapons[i];
        }

        // Spills into whatever slots the fixed tool/supply layout above left free, flat across every
        // shelf in order (starting with shelf 1's own leftover slots, then shelf 2's, then a 3rd+
        // shelf if this hull has one) rather than a 3rd fixed shelf offset - a hull with only 1
        // StorageRack (every custom-built test/player ship in this session) still gets as many
        // material types as its own leftover space allows instead of none at all.
        //
        // Deliberately leaves each shelf's own LAST slot empty rather than packing every last one:
        // (1) TestRunner.Storage.cs's World_Rack_DragFromInventory_StowsItem/
        // World_Rack_AwayFromTheRack_MoveIsRefused both hard-rely on "StorageRack.Capacity - 1" being
        // a guaranteed-empty destination to drag an item onto (their own doc comments explain why a
        // fixed index instead of searching for one); (2) a starter rack with every single slot
        // pre-filled would leave a freshly-spawned crew nowhere to stow the very first thing they
        // mine, defeating the whole point of adding minable materials in the first place.
        var materialIndex = 0;
        for (var slot = 0; slot < _rackSlots.Length && materialIndex < RackMaterials.Length; slot++)
        {
            if (_rackSlots[slot] is not null)
                continue;
            if ((slot + 1) % StorageRack.Capacity == 0)
                continue;
            _rackSlots[slot] = RackMaterials[materialIndex];
            materialIndex++;
        }
    }

    // Deliberately survives a change of hull (World.ShipPurchase.cs): the cargo is the crew's, and
    // silently binning it because they traded up would be a nasty surprise. Only a fresh World
    // starts with the seeded starter kit above.
    public void LoadRackSlots(IReadOnlyList<ItemType?> slots)
    {
        var totalCapacity = Ship.StorageRacks.Count * StorageRack.Capacity;
        _rackSlots = new ItemType?[totalCapacity];
        for (var i = 0; i < Math.Min(slots.Count, totalCapacity); i++)
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

        // Unequipping a worn BeltBag while it's still carrying anything would strand those items
        // nowhere - it has to be emptied out first, the same way a rack move refuses to strand a
        // plugged-in tank above.
        if (from.Kind == ItemSlotKind.Equip && (EquipSlot)from.Index == EquipSlot.BeltBag &&
            Array.Exists(character.Inventory.BeltBagSlots, s => s is not null))
            return;

        var destination = ReadSlot(character, to);

        // An equip slot only ever holds the one item type it's defined for (or nothing) - checked
        // for both ends up front, before either slot is actually written, so a swap can never
        // strand one side's item because the other end turned out to be invalid partway through.
        if (!IsAcceptable(to, source) || !IsAcceptable(from, destination))
            return;

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

    private static bool IsAcceptable(SlotRef slot, ItemType? item) =>
        item is null || slot.Kind != ItemSlotKind.Equip || EquipSlotDefinitions.SlotFor(item.Value) == (EquipSlot)slot.Index;

    // A drag that ended over empty space instead of another slot: same reachability rule as an
    // ordinary move (you can't drop what you can't otherwise touch), same tank-first safeguard as
    // moving onto the rack (a plugged tank never just vanishes), but the item lands on the floor at
    // the character's own feet as a DroppedItem instead of landing in another slot.
    private void TryDropItem(Character character, SlotRef from)
    {
        if (!IsSlotReachable(character, from))
            return;

        var item = ReadSlot(character, from);
        if (item is null)
            return;

        if (from.Kind == ItemSlotKind.Main && character.Inventory.TankCharge(from.Index) is not null &&
            !character.Inventory.TryDetachTank(from.Index))
            return;

        if (from.Kind == ItemSlotKind.Equip && (EquipSlot)from.Index == EquipSlot.BeltBag &&
            Array.Exists(character.Inventory.BeltBagSlots, s => s is not null))
            return;

        if (!WriteSlot(character, from, null))
            return;
        if (from.Kind == ItemSlotKind.Main)
            character.Inventory.HeldSlotIndices.Remove(from.Index);

        _droppedItems.Add(new DroppedItem($"drop-{_nextDroppedItemId++}", item.Value,
            character.Position.X, character.Position.Y, character.RoomId));
    }

    // A rack slot's global index (0..RackSlots.Count) maps back to a physical shelf by which
    // StorageRack.Capacity-sized band it falls in - SlotRef itself never needed a "which shelf"
    // field this way, only this one lookup did.
    private StorageRack RackFor(int globalSlotIndex) => Ship.StorageRacks[globalSlotIndex / StorageRack.Capacity];

    private bool IsSlotReachable(Character character, SlotRef slot) => slot.Kind switch
    {
        ItemSlotKind.Main => slot.Index >= 0 && slot.Index < Inventory.MainSlotCount &&
                             !character.OnEnemyShip && !character.IsOutside,
        ItemSlotKind.Rack => slot.Index >= 0 && slot.Index < _rackSlots.Length &&
                             !character.OnStation && !character.OnEnemyShip && !character.IsOutside &&
                             (RackFor(slot.Index).Position - character.Position).Length() < InteractionRadius,
        // Suit is deliberately excluded - reachable only through the suit-locker's own timed
        // equip/unequip action (World.Movement.cs), never through a plain drag.
        ItemSlotKind.Equip => Enum.IsDefined((EquipSlot)slot.Index) && (EquipSlot)slot.Index != EquipSlot.Suit &&
                              !character.OnEnemyShip && !character.IsOutside,
        ItemSlotKind.BeltBag => slot.Index >= 0 && slot.Index < Inventory.BeltBagSlotCount &&
                                character.Inventory.Equipped[EquipSlot.BeltBag] == ItemType.BeltBag &&
                                !character.OnEnemyShip && !character.IsOutside,
        _ => false,
    };

    private ItemType? ReadSlot(Character character, SlotRef slot) => slot.Kind switch
    {
        ItemSlotKind.Main => character.Inventory.MainSlots[slot.Index],
        ItemSlotKind.Rack => _rackSlots[slot.Index],
        ItemSlotKind.Equip => character.Inventory.Equipped[(EquipSlot)slot.Index],
        ItemSlotKind.BeltBag => character.Inventory.BeltBagSlots[slot.Index],
        _ => null,
    };

    private bool WriteSlot(Character character, SlotRef slot, ItemType? item)
    {
        switch (slot.Kind)
        {
            case ItemSlotKind.Main:
                character.Inventory.MainSlots[slot.Index] = item;
                break;
            case ItemSlotKind.Rack:
                _rackSlots[slot.Index] = item;
                break;
            case ItemSlotKind.Equip:
                character.Inventory.Equipped[(EquipSlot)slot.Index] = item;
                break;
            case ItemSlotKind.BeltBag:
                character.Inventory.BeltBagSlots[slot.Index] = item;
                break;
        }
        return true;
    }
}
