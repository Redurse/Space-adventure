using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool World_ToggleDoor_ViaClientCommand_FlipsState()
    {
        var world = new World();
        world.SpawnCharacter(1);

        var before = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor"));
        var after = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;

        return before && !after;
    }

    private static bool World_Door_Closed_BlocksMovementLikeWall()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor")); // starts open -> closed

        MoveCharacterTo(world, 1, 5f, 3f); // corridor -> reactor, right up against the now-closed door

        for (var i = 0; i < 30; i++) // keep pushing left, into the closed door
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.X >= 4.95f; // never made it into the cockpit (would need X < 5)
    }

    // Isolated from the rest of the ship (inner door closed) so this only exercises the vacuum-sink
    // formula itself - with the inner door left open instead, the rest of the ship's oxygen acts as
    // a large reservoir feeding the chamber back via diffusion and the decay is much slower (see
    // World_OpenInnerDoor_LetsVentedChamberDrainRestOfShip for that coupled scenario).
    private static bool World_AirlockOuterDoor_Open_LeaksChamberToVacuum()
    {
        var world = new World();
        world.SpawnCharacter(1);
        CastOffIntoSpace(world); // docked, that door opens onto the station rather than vacuum
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-engine-airlock")); // starts open -> closed
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 15 * 30; i++)
            world.Step(RealtimeStep);

        var chamberOxygen = world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen;
        return chamberOxygen < 10f;
    }

    private static bool World_AirlockOuterDoor_Closed_ChamberStaysPressurized()
    {
        var world = new World();
        world.SpawnCharacter(1); // door-airlock-vacuum is never touched - stays at its safe default (closed)

        for (var i = 0; i < 15 * 30; i++)
            world.Step(RealtimeStep);

        var chamberOxygen = world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen;
        return chamberOxygen > 99f;
    }

    // The core of M16: venting the chamber to space must not doom the rest of the crew, as long as
    // they close the door between the chamber and the rest of the ship first.
    private static bool World_ClosedInnerDoor_KeepsRestOfShipSealedFromVentedChamber()
    {
        var world = new World();
        world.SpawnCharacter(1);
        CastOffIntoSpace(world); // docked, that door opens onto the station rather than vacuum
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-engine-airlock")); // starts open -> closed
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var chamberOxygen = snapshot.RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen;
        var engineOxygen = snapshot.RoomOxygen.First(o => o.RoomId == "engine").Oxygen;
        return chamberOxygen < 10f && engineOxygen > 99f;
    }

    // Same setup, but the inner door is left at its default (open) - the vent now drags the rest
    // of the ship down too, which is exactly the risk the previous test's closed door avoids.
    private static bool World_OpenInnerDoor_LetsVentedChamberDrainRestOfShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        CastOffIntoSpace(world); // docked, that door opens onto the station rather than vacuum
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var engineOxygen = world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "engine").Oxygen;
        return engineOxygen < 90f;
    }

    // Shared EVA test setup (game_design.md Phase 3, M17).
    private static void EnterAsteroidFieldStationary(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);
    }

    // Suiting up now means suit *and* bottle: an empty suit is a shell that the airlock won't let
    // anyone through in (OxygenTankDefinitions), so every test that goes outside needs the tank as
    // much as it needs the suit. withTank: false is for the tests that check that gate itself.
    private static void EquipSuit(World world, int playerId, bool withTank = true)
    {
        MoveCharacterTo(world, playerId, 20f, 3f); // suit locker, engine room
        world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // start equipping
        for (var i = 0; i < 90; i++) // outlast the 2s equip action
            world.Step(RealtimeStep);

        if (!withTank || playerId != 1)
            return;
        TakeTankFromRack(world);
        AttachTankTo(world, WornSuitSlotIndex);
    }

    // MoveCharacterTo can't be reused for the final approach to the outer door: the instant the
    // crossing happens, CharacterState.X/Y switches from interior to AsteroidField world
    // coordinates (see World.cs CreateSnapshot), so a stale interior target like (27, 3) turns into
    // nonsense and its bang-bang homing would just walk the character right back toward the ship
    // (and potentially back inside). This walks a fixed direction instead, stopping the moment
    // IsOutside flips (or once maxTicks runs out, e.g. when suit/door gating is expected to block it).
    private static void WalkFixedDirection(World world, int playerId, float moveX, float moveY, int maxTicks = 60)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId);
            if (me.IsOutside)
                break;
            world.ApplyCommand(playerId, new ClientCommand(playerId, MoveX: moveX, MoveY: moveY));
            world.Step(RealtimeStep);
        }

        // The real client resends a fresh (usually zero) move vector every tick regardless; a test
        // driving ApplyCommand by hand has to do that explicitly, or the last nonzero direction
        // sent here would keep being applied indefinitely (harmless for interior movement, which
        // is room-clamped, but an EVA character attached to the ship would just keep sliding along
        // the hull on it forever).
        world.ApplyCommand(playerId, new ClientCommand(playerId, MoveX: 0, MoveY: 0));
    }

    private static bool World_Eva_ExitRequiresSuit()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // open it

        MoveCharacterTo(world, 1, 23f, 3f); // corridor -> ... -> engine -> airlock-chamber
        WalkFixedDirection(world, 1, 1f, 0f); // try to walk straight through the open outer door, unsuited

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.IsOutside;
    }

    private static bool World_Eva_ExitSuited_SetsIsOutsideAndAttachesToShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // walk through the open outer door, suited this time

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.IsOutside && me.IsEvaAttached;
    }

    // Walks to the ship's storage rack and picks up one tool along the way, so there's something in
    // the carried row to drag. Returns the slot index the tool landed in.
    // MoveCharacterTo walks both axes at once, which slams into a bulkhead whenever the target
    // isn't on the doors' shared mid-height - so cross the ship along that row first, then step off
    // it. Same routing every other multi-room test does by hand.
    private static void WalkAcrossShipTo(World world, float x, float y)
    {
        const float doorRow = 3f;
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        MoveCharacterTo(world, 1, me.X, doorRow);
        MoveCharacterTo(world, 1, x, doorRow);
        MoveCharacterTo(world, 1, x, y);
    }

    // Finds whichever of the two shelves is currently holding this item type (World.Storage.cs's
    // InitializeRackSlots seeds both at ship start), walks there, and drags one unit into the first
    // free main slot - the standard "get a tool into my hands" setup every test that needs one uses.
    // Returns the main slot it landed in, or -1 if the shelves are out of that type or hands are full.
    private static int TakeFromRack(World world, ItemType item)
    {
        var rackSlots = world.CreateSnapshot().RackSlots;
        var rackSlotIndex = -1;
        for (var i = 0; i < rackSlots.Count; i++)
            if (rackSlots[i] == item) { rackSlotIndex = i; break; }
        if (rackSlotIndex < 0)
            return -1;

        var rack = world.Ship.StorageRacks[rackSlotIndex / StorageRack.Capacity];
        WalkAcrossShipTo(world, rack.X, rack.Y);

        var mainSlots = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots;
        var freeMainSlot = -1;
        for (var i = 0; i < mainSlots.Count; i++)
            if (mainSlots[i] is null) { freeMainSlot = i; break; }
        if (freeMainSlot < 0)
            return -1;

        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Rack, rackSlotIndex), MoveItemTo: new SlotRef(ItemSlotKind.Main, freeMainSlot)));
        return freeMainSlot;
    }

    private static int StandAtRackHolding(World world, ItemType item) => TakeFromRack(world, item);

    private static ItemType? RackSlot(World world, int index) => world.CreateSnapshot().RackSlots[index];

    private static ItemType? MainSlot(World world, int index) =>
        world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots[index];

}
