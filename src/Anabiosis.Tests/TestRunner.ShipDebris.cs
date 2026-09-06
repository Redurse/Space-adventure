using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

// M63 - "структурное отделение" (see C:\Users\Andrey\.claude\plans\humble-soaring-cat.md's own M63
// design): fully breaching a room's own exterior wall destroys it and splits off whatever that
// leaves unreachable from the reactor as one free-flying debris fragment.
internal static partial class TestRunner
{
    // A fresh M60-catalog room is the one guaranteed-safe target to destroy in these tests: it
    // carries zero devices, so removing it can never trip any of CustomShipValidator's own "≥1 X"
    // rules the way destroying a piece of the hand-authored hull easily could (M61's own tests hit
    // exactly this same reasoning for build placement). Returns its id once built and completed.
    private static string BuildAndCompleteOneDeadEndRoom(World world)
    {
        var roomsBefore = world.Ship.Rooms.Select(r => r.Id).ToHashSet();
        var entry = world.GetBuildableRoomCatalog()[0];
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        world.DebugFastForwardRoomBuilds(9999);
        world.Step(RealtimeStep);
        return world.Ship.Rooms.Select(r => r.Id).First(id => !roomsBefore.Contains(id));
    }

    private static bool World_ShipDebris_DestroyingRoomsOwnWallBlocks_DetachesItAsDebris()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var builtRoomId = BuildAndCompleteOneDeadEndRoom(world);
        var roomsBefore = world.Ship.Rooms.Count;
        var debrisBefore = world.CreateSnapshot().ShipDebris?.Count ?? 0;

        world.DebugDestroyRoomWallBlocks(builtRoomId);
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var roomGone = world.Ship.Rooms.Count == roomsBefore - 1 && world.Ship.Rooms.All(r => r.Id != builtRoomId);
        var gotOneFragment = (snapshot.ShipDebris?.Count ?? 0) == debrisBefore + 1;
        var fragmentHasARoom = snapshot.ShipDebris is { Count: > 0 } && snapshot.ShipDebris[^1].Rooms.Count == 1;

        return roomGone && gotOneFragment && fragmentHasARoom;
    }

    // Pure inertia (World.ShipDebris.cs's own doc comment - no gravity since M59) - a fragment
    // launched with a known velocity has to have moved by exactly velocity*elapsed after a few ticks.
    private static bool World_ShipDebris_DriftsByInertiaAfterDetaching()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var builtRoomId = BuildAndCompleteOneDeadEndRoom(world);

        var velocity = new Vec2(37, -19);
        world.DebugSetShipVelocity(velocity);
        world.DebugDestroyRoomWallBlocks(builtRoomId);
        world.Step(RealtimeStep);

        var fragment = world.CreateSnapshot().ShipDebris?.FirstOrDefault();
        if (fragment is null)
            return false; // setup problem - detachment itself didn't happen
        var positionRightAfterSplit = new Vec2(fragment.X, fragment.Y);

        const int steps = 10;
        for (var i = 0; i < steps; i++)
            world.Step(RealtimeStep);

        var moved = world.CreateSnapshot().ShipDebris!.First(f => f.Id == fragment.Id);
        var actual = new Vec2(moved.X, moved.Y);
        var expected = positionRightAfterSplit + velocity * (RealtimeStep * steps);

        return (actual - expected).Length() < 0.5; // ship's own auto-stabilize doesn't touch debris, only float rounding
    }

    // M64 - a character actually standing in a detaching room must come out the other side as a
    // free EVA body at their own real position, not silently teleported to the remaining ship's
    // spawn point the way ApplyShipDefinition's generic orphaned-room fallback would otherwise do
    // (right for M61's voluntary docked demolition, wrong for a combat kill). An item they dropped
    // there has to disappear along with the wreck rather than linger as a pickup nobody can ever
    // reach again (the room's own interior isn't simulated once it's debris - World.ShipDebris.cs's
    // own doc comment).
    private static bool World_ShipDebris_EjectsCrewAndDropsItemsWhenRoomDetaches()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var builtRoomId = BuildAndCompleteOneDeadEndRoom(world);
        var room = world.Ship.Rooms.First(r => r.Id == builtRoomId);

        var slot = StandAtRackHolding(world, ItemType.Wrench);
        if (slot < 0)
            return false; // setup problem - couldn't get anything into hand to drop
        // Two-stage route rather than one diagonal beeline: every hand-authored door on this hull
        // sits on the row's own y=3 crossing band, so a straight line toward the built room's centre
        // (which sits in a whole separate row below/above/beside it) can walk the character into a
        // solid bulkhead well off that band and get stuck there instead of lining up with a door.
        // Travel along y=3 first (crossing every door along the way, however many there are) to get
        // the right column, then descend straight into the new room through its own doorway.
        MoveCharacterTo(world, 1, (float)room.Center.X, 3f);
        MoveCharacterTo(world, 1, (float)room.Center.X, (float)room.Center.Y);
        world.ApplyCommand(1, new ClientCommand(1, DropItemFrom: new SlotRef(ItemSlotKind.Main, slot)));

        var beforeDestroy = world.CreateSnapshot();
        var itemLandedInTheRoom = beforeDestroy.DroppedItems.Any(d => d.RoomId == builtRoomId);
        var wasStillIndoors = !beforeDestroy.Characters.Single(c => c.PlayerId == 1).IsOutside;
        if (!itemLandedInTheRoom || !wasStillIndoors)
            return false; // setup problem - drop or walk-in didn't actually land where expected

        world.DebugDestroyRoomWallBlocks(builtRoomId);
        world.Step(RealtimeStep);

        var after = world.CreateSnapshot();
        var characterEjected = after.Characters.Single(c => c.PlayerId == 1).IsOutside;
        var itemGone = after.DroppedItems.All(d => d.RoomId != builtRoomId);

        return characterEjected && itemGone;
    }

    // The reactor's own room can't sensibly detach on its own (World.ShipDebris.cs's own guard) -
    // destroying its wall blocks must leave the room exactly where it was, not spin off debris or
    // corrupt the ship.
    private static bool World_ShipDebris_DestroyingReactorRoom_StaysAttachedNoDebris()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var roomsBefore = world.Ship.Rooms.Count;

        world.DebugDestroyRoomWallBlocks("reactor");
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        return world.Ship.Rooms.Count == roomsBefore && world.Ship.Rooms.Any(r => r.Id == "reactor")
            && (snapshot.ShipDebris?.Count ?? 0) == 0;
    }

    // Same "refuse rather than corrupt" guard, hit via the hand-authored hull's own topology instead
    // of the reactor special-case above: the default Frigate's "quarters" room holds the ship's only
    // AmmoStorage (Ship.cs's own comment on it) and sits in the middle of a single-file corridor, so
    // destroying it would both lose the sole ammo rack AND strand "engine"/"airlock-chamber" behind
    // it - CustomShipValidator has to reject the shrink, and the room must simply stay put, fully
    // breached, same as an ordinary un-detachable hull breach today.
    private static bool World_ShipDebris_DestroyingRoomThatWouldInvalidateTheHull_StaysAttached()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var roomsBefore = world.Ship.Rooms.Count;

        world.DebugDestroyRoomWallBlocks("quarters");
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        return world.Ship.Rooms.Count == roomsBefore && world.Ship.Rooms.Any(r => r.Id == "quarters")
            && (snapshot.ShipDebris?.Count ?? 0) == 0;
    }

    // M77 (humble-soaring-cat.md) - proves the tile-region BFS actually walks INDIRECT connectivity,
    // not just "was the destroyed room itself involved": builds a two-room chain (a plain "empty-
    // small" room flush against the hull, then a "camera" room - a device-carrying catalog entry,
    // RoomCatalog.cs - flush against THAT room's own far side, so it connects to the rest of the ship
    // ONLY through the first one), then destroys the first room's wall blocks. The second room - and
    // its camera device - were never touched directly, but must detach too, because the only path
    // from them to the reactor now runs through a room that no longer exists.
    private static bool World_ShipDebris_DestroyingRoomWithChainedNeighbor_DetachesBothIndirectly()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        var innerEntry = world.GetBuildableRoomCatalog().First(e => e.Id == "empty-small");
        var hullAnchor = world.Ship.Rooms[0];
        var innerX = hullAnchor.X;
        var innerY = hullAnchor.Y + hullAnchor.Height;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(innerEntry.Id, innerX, innerY)));
        CompletePendingRoomBuilds(world);
        var innerRoom = world.Ship.Rooms.FirstOrDefault(r => r.X == innerX && r.Y == innerY);
        if (innerRoom is null)
            return false; // setup problem - the first room didn't actually land where expected

        var outerEntry = world.GetBuildableRoomCatalog().First(e => e.Id == "camera");
        var outerX = innerX;
        var outerY = innerY + innerRoom.Height;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(outerEntry.Id, outerX, outerY)));
        CompletePendingRoomBuilds(world);
        var outerRoom = world.Ship.Rooms.FirstOrDefault(r => r.X == outerX && r.Y == outerY);
        if (outerRoom is null)
            return false; // setup problem - the chained room didn't actually land where expected
        var camerasBefore = world.Ship.Cameras.Count;
        if (camerasBefore == 0)
            return false; // setup problem - the camera room's own device didn't actually get built
                           // (every hand-authored hull already carries its own camera(s) - World.
                           // Cameras.cs's own tests - so this is a delta check, not an absolute one)

        var roomsBefore = world.Ship.Rooms.Count;
        var debrisBefore = world.CreateSnapshot().ShipDebris?.Count ?? 0;

        world.DebugDestroyRoomWallBlocks(innerRoom.Id);
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var bothRoomsGone = world.Ship.Rooms.Count == roomsBefore - 2
            && world.Ship.Rooms.All(r => r.Id != innerRoom.Id && r.Id != outerRoom.Id);
        var cameraDeviceGone = world.Ship.Cameras.Count == camerasBefore - 1;
        var gotOneFragmentWithBothRooms = (snapshot.ShipDebris?.Count ?? 0) == debrisBefore + 1
            && snapshot.ShipDebris is { Count: > 0 } && snapshot.ShipDebris[^1].Rooms.Count == 2;

        return bothRoomsGone && cameraDeviceGone && gotOneFragmentWithBothRooms;
    }
}
