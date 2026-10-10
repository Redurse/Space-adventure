using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Each compartment's own health (World.RoomHp.cs): read off its walls, zero when a third of them is gone,
// restored by repairing them. The destruction itself (blast, chains, the reactor) is TestRunner.ShipBlasts.cs.
internal static partial class TestRunner
{
    // Three rooms in a row - "a" holds every required device (Reactor/Distribution/Helm/Navigation/Oxygen/
    // SuitLocker/StorageRack, all in the one room that's never touched by most of these tests) plus the
    // hull's only airlock, so the shrunk definition TryComputeRoomDetachment builds after "b" explodes still
    // validates even though "b" and "c" both disappear. "b" holds the one real engine fixture and sits between
    // "a" (the reactor room) and "c" (a pure dead end reachable only through "b") - destroying "b" must also
    // cut "c" loose.
    private static CustomShipDefinition BuildThreeRoomEngineCustomShipDefinition() => new(
        "Тестовый корабль для хп отсека",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Двигательный", 4, 0, 4, 4),
            new CustomRoomDef("c", "Тупик", 8, 0, 4, 4),
        },
        new[]
        {
            new CustomDoorDef(4, 2, true, true), // shared wall a/b at X=4
            new CustomDoorDef(8, 2, true, true), // shared wall b/c at X=8
        },
        new[] { new CustomAirlockDef("a", EdgeSide.Left) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 1),
            new CustomDeviceDef(CustomDeviceKind.Helm, 1, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 2, 3),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 3, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 3, 2),
            // Redundant on purpose: room "b"'s own real ShipEngine (below) is the only OTHER "way to
            // move" this hull has - without a second one surviving in "a", destroying "b" would leave
            // the shrunk definition with none at all, and CustomShipValidator (correctly) refuses the
            // whole detachment rather than produce a ship with no engine whatsoever.
            new CustomDeviceDef(CustomDeviceKind.Engine, 3, 3),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(5f, 1f, TileSide.South, 20f) });

    // How many hit points a room's walls hold in total (its wall blocks plus any engine's bulkhead+nozzle).
    private static float RoomWallTotal(World world, string roomId) =>
        world.Ship.WallBlocks.Where(b => b.RoomId == roomId).Sum(b => World.WallBlockMaxHp)
        + 2f * World.EnginePartMaxHp * world.Ship.Engines.Count(e => e.RoomId == roomId);

    private static bool World_RoomHp_StartsAtMaxForEveryRoom()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var states = world.CreateSnapshot().RoomHp!;
        return states.Count == 3 && states.All(s => s.Hp == World.RoomMaxHp && s.MaxHp == World.RoomMaxHp);
    }

    // The engine's bulkhead is one of its compartment's walls: damage to it lowers that room's health by the
    // share of the room's wall hit points it took, and no other room's.
    private static bool World_RoomHp_EngineBulkheadDamage_ReducesOnlyItsOwnRoom()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var engineId = world.Ship.Engines.Single().Id;
        var engineRoomId = world.Ship.Engines.Single().RoomId;
        var total = RoomWallTotal(world, engineRoomId);

        world.DebugBreachEngineBulkhead(engineId); // World.EnginePartMaxHp (100) worth

        var states = world.CreateSnapshot().RoomHp!;
        var expected = World.RoomMaxHp * (1f - World.EnginePartMaxHp / (total * World.RoomDestructionWallShare));
        return Math.Abs(states.Single(s => s.RoomId == engineRoomId).Hp - expected) < 0.5f
            && states.Where(s => s.RoomId != engineRoomId).All(s => s.Hp == World.RoomMaxHp);
    }

    // Doors are not part of the compartment's walls: breaking one leaves the rooms' health alone.
    private static bool World_RoomHp_DoorDamage_DoesNotTouchRoomHp()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var door = world.Ship.Doors.First(d => d.RoomAId == "a" && d.RoomBId == "b" || d.RoomAId == "b" && d.RoomBId == "a");

        world.ChopDoor(door.Id, World.AxeChopDamage);
        world.DamageDoor(door.Id);

        return world.CreateSnapshot().RoomHp!.All(s => s.Hp == World.RoomMaxHp);
    }

    // A wall that is only damaged, not breached, already costs the room part of its health - and welding it
    // back gives exactly that part back (variant "B": a repaired breach returns what it cost).
    private static bool World_RoomHp_PartialWallDamageLowersHp_AndRepairRestoresIt()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var block = world.Ship.WallBlocks.First(b => b.RoomId == "a");
        var total = RoomWallTotal(world, "a");

        world.DebugDamageWallBlockById(block.Id, 40f);
        var hurt = world.CreateSnapshot().RoomHp!.Single(s => s.RoomId == "a").Hp;
        var expected = World.RoomMaxHp * (1f - 40f / (total * World.RoomDestructionWallShare));
        if (Math.Abs(hurt - expected) > 0.5f)
            return false;

        world.DebugRepairWallBlockById(block.Id, 40f);
        return world.CreateSnapshot().RoomHp!.Single(s => s.RoomId == "a").Hp == World.RoomMaxHp;
    }

    // A breach (wall at 0) costs the room one wall's whole share, and patching it gives that share back.
    private static bool World_RoomHp_BreachCostsItsShare_WeldingItReturnsIt()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var block = world.Ship.WallBlocks.First(b => b.RoomId == "a");
        var total = RoomWallTotal(world, "a");

        world.DebugBreachWallBlockById(block.Id);
        var breached = world.CreateSnapshot().RoomHp!.Single(s => s.RoomId == "a").Hp;
        var expected = World.RoomMaxHp * (1f - World.WallBlockMaxHp / (total * World.RoomDestructionWallShare));
        if (Math.Abs(breached - expected) > 0.5f)
            return false;

        world.DebugRepairWallBlockById(block.Id, World.WallBlockMaxHp);
        return world.CreateSnapshot().RoomHp!.Single(s => s.RoomId == "a").Hp == World.RoomMaxHp;
    }

    // The compartment dies exactly when a third of its wall hit points are gone - not before, not after.
    private static bool World_RoomHp_ThirdOfTheWallsGone_DestroysTheCompartment()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var blocks = world.Ship.WallBlocks.Where(b => b.RoomId == "c").ToList();
        var needed = (int)MathF.Ceiling(blocks.Count * World.RoomDestructionWallShare);
        if (needed < 2)
            return false; // setup problem - the room has too few walls for this to tell anything

        for (var i = 0; i < needed - 1; i++)
            world.DebugBreachWallBlockById(blocks[i].Id);
        var stillThere = world.Ship.Rooms.Any(r => r.Id == "c");
        world.DebugBreachWallBlockById(blocks[needed - 1].Id);
        world.Step(RealtimeStep);
        var gone = world.Ship.Rooms.All(r => r.Id != "c");
        return stillThere && gone;
    }

    // Full round-trip: destroying "b" (engine inside, "c" reachable only through it) removes it from
    // Rooms/Engines/Doors, leaves a static wreck patch, ejects whoever stood in it, and - since "c" was cut off -
    // sends "c" off as its own tumbling fragment.
    private static bool World_RoomHp_DestroyedCompartment_LeavesAWreckAndCutsOffTheOrphanedNeighbor()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var wreckPatchesBefore = world.Ship.WreckPatches.Count;

        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 6f, 2f); // inside room "b", the one about to be destroyed

        world.DebugDestroyRoomWallBlocks("b");
        world.Step(RealtimeStep);

        var roomGone = world.Ship.Rooms.All(r => r.Id != "b" && r.Id != "c") && world.Ship.Rooms.Count == 1;
        var engineGone = world.Ship.Engines.Count == 0;
        var doorsGone = world.Ship.Doors.Count(d => !d.LeadsToVacuum) == 0 && world.Ship.VacuumDoors.Count == 1;
        // It vanishes completely: no decal, and where it stood is open space - there is no floor to walk on any more.
        var leftNoWreck = world.Ship.WreckPatches.Count == wreckPatchesBefore
            && !(world.Ship.Tiles.CellAt(new TileCoord(6, 2)) is { HasFloor: true });

        var snapshot = world.CreateSnapshot();
        var characterEjected = snapshot.Characters.Single(c => c.PlayerId == 1).IsOutside;
        // "c" detaches as a real, independently flying fragment - the exploded room "b" itself stays as the wreck.
        var neighborDetachedAsDebris = snapshot.ShipDebris is { Count: 1 } debris && debris[0].Rooms.Count == 1;

        return roomGone && engineGone && doorsGone && leftNoWreck && characterEjected && neighborDetachedAsDebris;
    }
}
