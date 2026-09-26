using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Three rooms in a row (World.RoomHp.cs's own doc comment: "тестовые 1000") - "a" holds every
    // required device (Reactor/Distribution/Helm/Navigation/Oxygen/SuitLocker/StorageRack, all in the
    // one room that's never touched by this file's own tests) plus the hull's only airlock, so the
    // shrunk definition TryComputeRoomDetachment builds after "b" explodes still validates even
    // though "b" and "c" both disappear. "b" holds the one real engine fixture (its Bulkhead is this
    // file's own controlled room-hp damage source) and sits between "a" (the reactor room) and "c" (a
    // pure dead end reachable only through "b") - destroying "b" must also cut "c" loose.
    private static CustomShipDefinition BuildThreeRoomEngineCustomShipDefinition() => new(
        "Тестовый корабль для скрытого хп отсека",
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
            // whole detachment rather than produce a ship with no engine whatsoever (the same "refuse
            // rather than corrupt" guard TryComputeRoomDetachment's own doc comment describes).
            new CustomDeviceDef(CustomDeviceKind.Engine, 3, 3),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(5f, 1f, TileSide.South, 20f) });

    private static bool World_RoomHp_StartsAtMaxForEveryRoom()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var states = world.CreateSnapshot().RoomHp!;
        return states.Count == 3 && states.All(s => s.Hp == World.RoomMaxHp && s.MaxHp == World.RoomMaxHp);
    }

    // Direct user request ("скрытое число хп... тратится от урона") - a source already wired into
    // DamageRoom (World.Engines.cs's DamageEngineBulkhead) visibly lowers the one room's own entry
    // and leaves every other room untouched.
    private static bool World_RoomHp_EngineBulkheadDamage_ReducesOnlyItsOwnRoom()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var engineId = world.Ship.Engines.Single().Id;
        var engineRoomId = world.Ship.Engines.Single().RoomId;

        world.DebugBreachEngineBulkhead(engineId); // one shot, World.EnginePartMaxHp (100) worth

        var states = world.CreateSnapshot().RoomHp!;
        var engineRoom = states.Single(s => s.RoomId == engineRoomId);
        return engineRoom.Hp == World.RoomMaxHp - World.EnginePartMaxHp
            && states.Where(s => s.RoomId != engineRoomId).All(s => s.Hp == World.RoomMaxHp);
    }

    // Direct user request ("скрытое число хп... тратится от урона") - ChopDoor (World.Doors.cs)
    // damages BOTH rooms a door borders, same amount each.
    private static bool World_RoomHp_DoorDamage_ReducesBothBorderingRooms()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var door = world.Ship.Doors.First(d => d.RoomAId == "a" && d.RoomBId == "b" || d.RoomAId == "b" && d.RoomBId == "a");

        world.ChopDoor(door.Id, World.AxeChopDamage);

        var states = world.CreateSnapshot().RoomHp!;
        return states.Single(s => s.RoomId == "a").Hp == World.RoomMaxHp - World.AxeChopDamage
            && states.Single(s => s.RoomId == "b").Hp == World.RoomMaxHp - World.AxeChopDamage
            && states.Single(s => s.RoomId == "c").Hp == World.RoomMaxHp;
    }

    // Direct user request, full round-trip ("отсек физически перестает существовать на корабле
    // полностью, он удаляется везде где он используется, а на месте взорванного отсека будут всякие
    // обломки"): a room whose hp reaches 0 disappears from Ship.Rooms/Engines/Doors, gains a static
    // Ship.WreckPatches entry (NOT a flying ShipDebrisFragment - that's still reserved for a genuine
    // wall breach, World.ShipDebris.cs's DestroyRoomAndDetach), ejects whoever was standing in it to
    // EVA, and - since "c" was reachable only through the exploding room "b" - still detaches "c" as
    // a real flying fragment exactly like a wall-breach would (World.RoomHp.cs's ExplodeRoom's own
    // doc comment: "ANY other room that becomes unreachable purely as a side effect... still detaches
    // and flies off exactly as before").
    private static bool World_RoomHp_ReachingZero_ExplodesRoomAndDetachesOrphanedNeighbor()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        var engineId = world.Ship.Engines.Single().Id;
        var wreckPatchesBefore = world.Ship.WreckPatches.Count;

        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 6f, 2f); // inside room "b", the one about to explode

        // Repeated on purpose: DamageEngineBulkhead floors the bulkhead's OWN hp at 0 but still
        // forwards the full `amount` to DamageRoom every time it's called (World.Engines.cs), the
        // same "keeps taking damage" shape a real, sustained combat hit would produce - 10 calls at
        // World.EnginePartMaxHp(100) each exhausts World.RoomMaxHp(1000) exactly.
        for (var i = 0; i < 10; i++)
            world.DebugBreachEngineBulkhead(engineId);

        var roomGone = world.Ship.Rooms.All(r => r.Id != "b" && r.Id != "c") && world.Ship.Rooms.Count == 1;
        var engineGone = world.Ship.Engines.Count == 0;
        // Both interior doors (a/b and b/c) touched the exploded room "b" and are gone with it - but
        // room "a"'s own airlock survives (room "a" itself was never destroyed), and now lives in this
        // SAME Doors list rather than a separate one (humble-soaring-cat.md, "убрать AirlockOuterDoor
        // как отдельный тип"), so this checks "no interior doors left", not "the list is empty".
        var doorsGone = world.Ship.Doors.Count(d => !d.LeadsToVacuum) == 0 && world.Ship.VacuumDoors.Count == 1;
        var gotWreckPatch = world.Ship.WreckPatches.Count == wreckPatchesBefore + 1;

        var snapshot = world.CreateSnapshot();
        var characterEjected = snapshot.Characters.Single(c => c.PlayerId == 1).IsOutside;
        // "c" detaches as a real, independently flying fragment - the exploded room "b" itself does
        // NOT (it stays put as the wreck patch checked above), so exactly one fragment, holding "c".
        var neighborDetachedAsDebris = snapshot.ShipDebris is { Count: 1 } debris && debris[0].Rooms.Count == 1;

        return roomGone && engineGone && doorsGone && gotWreckPatch && characterEjected && neighborDetachedAsDebris;
    }
}
