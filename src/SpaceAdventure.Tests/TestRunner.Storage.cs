using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // A drag released over empty space - not a slot, not a socket - lands the item on the floor at
    // the character's own feet instead of silently snapping back (Game1.cs's UpdateItemDrag).
    private static bool World_Storage_DropItem_LandsOnFloorAndClearsSlot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = StandAtRackHolding(world, ItemType.Wrench);
        if (slot < 0)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DropItemFrom: new SlotRef(ItemSlotKind.Main, slot)));

        var snapshot = world.CreateSnapshot();
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        var dropped = snapshot.DroppedItems.FirstOrDefault(d => d.Item == ItemType.Wrench);
        return MainSlot(world, slot) is null && dropped is not null && dropped.RoomId is not null
            && MathF.Abs(dropped.X - me.X) < 0.01f && MathF.Abs(dropped.Y - me.Y) < 0.01f;
    }

    // Same reachability rule as an ordinary move - the rack is a physical shelf, dropping from it
    // needs standing there just as much as stowing onto it does.
    private static bool World_Storage_DropItem_UnreachableRackSlotIsRefused()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, slot), MoveItemTo: new SlotRef(ItemSlotKind.Rack, 0)));
        WalkAcrossShipTo(world, 3f, 3f); // off to the cockpit, far from the rack

        world.ApplyCommand(1, new ClientCommand(1, DropItemFrom: new SlotRef(ItemSlotKind.Rack, 0)));

        return RackSlot(world, 0) == ItemType.Wrench && world.CreateSnapshot().DroppedItems.All(d => d.Item != ItemType.Wrench);
    }

    // Same tank-first safeguard TryMoveItem already applies moving onto the rack - a plugged tank
    // never just vanishes because the tool it's riding in got dropped.
    private static bool World_Storage_DropItem_DetachesTankBeforeDropping()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var cutterSlot = TakeFromRack(world, ItemType.Cutter);

        TakeTankFromRack(world);
        AttachTankTo(world, cutterSlot);

        world.ApplyCommand(1, new ClientCommand(1, DropItemFrom: new SlotRef(ItemSlotKind.Main, cutterSlot)));

        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;
        var droppedCutter = snapshot.DroppedItems.FirstOrDefault(d => d.Item == ItemType.Cutter);
        return droppedCutter is not null && inventory.MainSlots[cutterSlot] is null
            && inventory.MainSlots.Contains(ItemType.OxygenTank); // the tank survived, not lost
    }

    // Click-to-pick-up (World.Mining.cs's TryPickupDroppedItem) works in the ship interior too, not
    // just EVA - it's what closes the loop with World.Storage.cs's new drop-to-floor.
    private static bool World_Storage_PickupDroppedItem_ReturnsItToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, DropItemFrom: new SlotRef(ItemSlotKind.Main, slot)));
        var droppedId = world.CreateSnapshot().DroppedItems.First(d => d.Item == ItemType.Wrench).Id;

        world.ApplyCommand(1, new ClientCommand(1, PickupDroppedItemId: droppedId));

        var snapshot = world.CreateSnapshot();
        return snapshot.DroppedItems.All(d => d.Id != droppedId)
            && snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Contains(ItemType.Wrench);
    }

    // Server re-checks range itself - walking away after dropping something doesn't let a click from
    // across the ship teleport it into your hands.
    private static bool World_Storage_PickupDroppedItem_TooFarAwayIsRefused()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, DropItemFrom: new SlotRef(ItemSlotKind.Main, slot)));
        var droppedId = world.CreateSnapshot().DroppedItems.First(d => d.Item == ItemType.Wrench).Id;

        WalkAcrossShipTo(world, 3f, 3f); // off to the cockpit, far from the rack

        world.ApplyCommand(1, new ClientCommand(1, PickupDroppedItemId: droppedId));

        return world.CreateSnapshot().DroppedItems.Any(d => d.Id == droppedId); // still on the floor
    }

    private static bool World_Rack_DragFromInventory_StowsItem()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        if (from < 0)
            return false;

        // Slot 20 sits past the starter stock's own 18 slots (World.Storage.cs's
        // InitializeRackSlots) so this is a stow onto an empty slot, not a swap with whatever
        // starter item happens to already live there.
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 20)));

        return RackSlot(world, 20) == ItemType.Wrench && MainSlot(world, from) is null;
    }

    // Dropping onto an occupied slot exchanges the two rather than overwriting - losing an item to
    // a slightly-off drop would be a nasty way to destroy the only cutter on the ship.
    private static bool World_Rack_DropOntoOccupiedSlot_SwapsTheTwo()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var wrenchSlot = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, wrenchSlot),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 0)));

        var screwdriverSlot = StandAtRackHolding(world, ItemType.Screwdriver);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, screwdriverSlot),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 0)));

        return RackSlot(world, 0) == ItemType.Screwdriver && MainSlot(world, screwdriverSlot) == ItemType.Wrench;
    }

    // The rack is a physical shelf, not a pocket dimension - you have to be standing at it.
    private static bool World_Rack_AwayFromTheRack_MoveIsRefused()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        WalkAcrossShipTo(world, 3f, 3f); // off to the cockpit, far from the rack

        // Slot 20 sits past the starter stock's own 18 slots (World.Storage.cs's
        // InitializeRackSlots), so it's empty - "is null" only proves anything if it started that way.
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 20)));

        return RackSlot(world, 20) is null && MainSlot(world, from) == ItemType.Wrench;
    }

    // Rearranging your own row needs no rack at all - and anything that moves leaves your hands,
    // since the held-hand list is keyed by slot index.
    private static bool World_Inventory_DragBetweenOwnSlots_MovesAndEmptiesHands()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: from));
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.HeldMainSlotIndices.Contains(from))
            return false;

        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Main, 8)));

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots[8] == ItemType.Wrench && inventory.MainSlots[from] is null &&
               inventory.HeldMainSlotIndices.Count == 0;
    }

    private static bool World_Save_RoundTripsRackContents()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 12)));

        var save = world.CreateSave();
        var restored = new World();
        restored.SpawnCharacter(1);
        restored.ApplySave(save);

        return restored.CreateSnapshot().RackSlots[12] == ItemType.Wrench;
    }

}
