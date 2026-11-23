using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Doors have their own strength: the cutter saws through them, and a wrench in hand mends them - in seconds, gradually, door or doorway alike.
internal static partial class TestRunner
{
    private static DoorState DoorStateOf(World world, string id) => world.CreateSnapshot().DoorStates.First(d => d.DoorId == id);

    private static bool World_Door_WrenchMendsAHalfCutDoorGradually()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.Wrench);
        var wrenchSlot = Array.IndexOf(world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.ToArray(), ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        MoveCharacterTo(world, 1, 4.9f, 3f);
        world.ChopDoor("door-cockpit-reactor", 60f); // 40 of 100 left, still standing
        if (DoorStateOf(world, "door-cockpit-reactor").Hp is not (> 39f and < 41f))
            return false;

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        StepFor(world, 3 * 30);
        var partway = DoorStateOf(world, "door-cockpit-reactor").Hp;
        if (partway <= 50f || partway >= 99f)
            return false; // better than before, not yet done

        StepFor(world, 10 * 30);
        return DoorStateOf(world, "door-cockpit-reactor").Hp >= World.DoorMaxHp - 0.01f;
    }

    private static bool World_Door_RepairNeedsAToolInHandAndStaysInReach()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 4.9f, 3f);
        world.ChopDoor("door-cockpit-reactor", 60f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // empty-handed
        StepFor(world, 3 * 30);
        if (DoorStateOf(world, "door-cockpit-reactor").Hp > 41f)
            return false;

        world.DebugGiveItem(1, ItemType.Wrench);
        var wrenchSlot = Array.IndexOf(world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.ToArray(), ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        StepFor(world, 2 * 30);
        var working = DoorStateOf(world, "door-cockpit-reactor").Hp;
        if (working <= 45f)
            return false;

        // Walks off: the work stops where it is. Measured from the moment they are out of reach - the reach is 1.5 now,
        // so the work keeps going for a few steps of the walk away.
        MoveCharacterTo(world, 1, 1.5f, 3f);
        var leftAt = DoorStateOf(world, "door-cockpit-reactor").Hp;
        StepFor(world, 3 * 30);
        return leftAt < World.DoorMaxHp - 0.01f && Math.Abs(DoorStateOf(world, "door-cockpit-reactor").Hp - leftAt) < 1f;
    }

    private static (World World, ShipDoorEdge Edge) DoorEdgeWorld()
    {
        var world = new World(ShipKind.Custom, BuildDoorEdgeShipDefinition());
        world.SpawnCharacter(1);
        world.Step(0.01);
        var edge = world.Ship.DoorEdges.Single();
        return (world, edge);
    }

    private static bool World_DoorEdge_CutterSawsThroughAndWrenchMendsIt()
    {
        var (world, edge) = DoorEdgeWorld();
        world.DebugGiveItem(1, ItemType.Cutter);
        world.DebugGiveItem(1, ItemType.OxygenTank);
        world.DebugGiveItem(1, ItemType.Wrench);
        var slots = world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.ToArray();
        var cutterSlot = Array.IndexOf(slots, ItemType.Cutter);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: Array.IndexOf(slots, ItemType.OxygenTank), AttachTankToSlot: cutterSlot));
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: cutterSlot));

        // Stand just north of the seam, aim straight at it.
        MoveCharacterTo(world, 1, 3.5f, 6.3f);
        if (world.CreateSnapshot().DoorEdgeStates!.First(s => s.Id == edge.Id).IsOpen)
            world.ToggleDoor(edge.Id); // the walking helper opens every doorway on its way; the cutter only works a closed one
        for (var i = 0; i < 6 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: 0f, LookY: 1f));
            world.Step(RealtimeStep);
        }
        var cut = world.CreateSnapshot().DoorEdgeStates!.First(s => s.Id == edge.Id);
        if (!cut.Destroyed || !cut.IsOpen)
            return false;

        // Wrench in hand and work it: it comes back, and answers the controls again.
        var wrenchSlot = Array.IndexOf(world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.ToArray(), ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        StepFor(world, 12 * 30);
        var mended = world.CreateSnapshot().DoorEdgeStates!.First(s => s.Id == edge.Id);
        if (mended.Destroyed || mended.Hp < World.DoorMaxHp - 0.01f)
            return false;
        world.ToggleDoor(edge.Id);
        return !world.CreateSnapshot().DoorEdgeStates!.First(s => s.Id == edge.Id).IsOpen;
    }
}
