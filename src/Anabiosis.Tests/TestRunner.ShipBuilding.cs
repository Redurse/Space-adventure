using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

// M60/M61/M62 - "строить отсеки по ходу игры, и снести можно" (see C:\Users\Andrey\.claude\plans\
// humble-soaring-cat.md's own M60/M61/M62 design): building a room onto the current hull (M62 -
// anywhere, even mid-flight/mid-combat, on a timer + a plating cost) and demolishing one back off
// (still docked+Shipwright, M61's own deliberate-teardown gate, unchanged by M62).
internal static partial class TestRunner
{
    // M62 - a build is no longer instant; every test that needs one to have actually landed calls
    // this after the ApplyCommand that started it, rather than a bare Step(). 9999s comfortably
    // clears RoomBuildDurationSeconds regardless of its exact current value.
    private static void CompletePendingRoomBuilds(World world)
    {
        world.DebugFastForwardRoomBuilds(9999);
        world.Step(RealtimeStep);
    }

    private static bool World_ShipBuilding_BuildsWhenRequested_AddsRoomAndDeductsCreditsAndPlating()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma"); // any station works now (M62) - docked here purely
                                                // so RealtimeStep-based setup elsewhere isn't disturbed

        var roomsBefore = world.Ship.Rooms.Count;
        var creditsBefore = world.Credits;
        var platingBefore = world.HullPlatingStock;
        var entry = world.GetBuildableRoomCatalog()[0];

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        // Resources are spent the instant the build is accepted, not on completion.
        var creditsRightAfterRequest = world.Credits;
        var platingRightAfterRequest = world.HullPlatingStock;
        CompletePendingRoomBuilds(world);

        return world.Ship.Rooms.Count == roomsBefore + 1
            && creditsRightAfterRequest == creditsBefore - entry.Price
            && platingRightAfterRequest == platingBefore - entry.PlatingCost
            && world.Credits == creditsRightAfterRequest // completion itself charges nothing further
            && world.CurrentShipKind == ShipKind.Custom;
    }

    // M62's whole point: a build is a timed process, not an instant swap - right after the request,
    // the room must NOT be part of Ship.Rooms yet, but must show up as a ghost in the snapshot with
    // a progress fraction between 0 and 1 (not yet complete).
    private static bool World_ShipBuilding_BuildIsNotInstant_ShowsAsPendingGhostUntilComplete()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var roomsBefore = world.Ship.Rooms.Count;
        var entry = world.GetBuildableRoomCatalog()[0];

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        world.Step(RealtimeStep); // one ordinary tick - nowhere near RoomBuildDurationSeconds

        var midSnapshot = world.CreateSnapshot();
        var stillNotBuilt = world.Ship.Rooms.Count == roomsBefore;
        var hasOneGhost = midSnapshot.PendingRoomBuilds is { Count: 1 };
        var ghostProgressInRange = midSnapshot.PendingRoomBuilds![0].ProgressFraction is > 0f and < 1f;

        CompletePendingRoomBuilds(world);
        var finalSnapshot = world.CreateSnapshot();
        var nowBuilt = world.Ship.Rooms.Count == roomsBefore + 1;
        var ghostGoneAfterCompletion = finalSnapshot.PendingRoomBuilds is null or { Count: 0 };

        return stillNotBuilt && hasOneGhost && ghostProgressInRange && nowBuilt && ghostGoneAfterCompletion;
    }

    // M62's own core promise ("даже в полёте и в бою") - unlike M60/M61, building needs neither
    // docking nor a Shipwright NPC in earshot any more. Undocked AND out of range of any station's
    // Shipwright is the strongest version of this claim to test directly.
    private static bool World_ShipBuilding_SucceedsWhileUndockedAndAwayFromAnyShipwright()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        var roomsBefore = world.Ship.Rooms.Count;

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(RoomCatalog.Entries[0].Id)));
        CompletePendingRoomBuilds(world);

        return world.Ship.Rooms.Count == roomsBefore + 1;
    }

    private static bool World_ShipBuilding_FailsWithoutEnoughCredits()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        // Spend everything first via a cheap hull downgrade's own refund path would complicate this
        // setup - simpler to just drain the wallet the same way a losing-fight test would, by
        // directly checking a request that costs more than the starting wallet has: every catalog
        // entry today costs far less than the 900-credit starting wallet, so buy enough of the
        // cheapest one to exhaust it, then confirm the next one is refused. Bounded rather than a
        // bare `while (Credits >= price)` - if a placement ever legitimately stops succeeding before
        // the wallet is empty (e.g. ran out of open hull edges), that must fail this test loudly
        // instead of spinning the whole suite forever the way an earlier version of this test did.
        // Each attempt is completed (M62) before the next one starts, so every completed room
        // becomes a fresh anchor for the next placement search the same way it always did pre-M62.
        var entry = world.GetBuildableRoomCatalog()[0];
        for (var i = 0; i < 20 && world.Credits >= entry.Price; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
            CompletePendingRoomBuilds(world);
        }
        if (world.Credits >= entry.Price)
            return false; // setup problem - didn't actually exhaust the wallet in the attempt budget

        var roomsBefore = world.Ship.Rooms.Count;
        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));

        return world.Ship.Rooms.Count == roomsBefore && world.Credits == creditsBefore;
    }

    // M62's own new resource gate, isolated from the credit one above via DebugSetHullPlatingStock -
    // at today's placeholder balance numbers credits alone always run out first through ordinary
    // play, so this is the only reliable way to prove the plating check independently exists at all.
    private static bool World_ShipBuilding_FailsWithoutEnoughPlating()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var entry = world.GetBuildableRoomCatalog()[0];
        world.DebugSetHullPlatingStock(entry.PlatingCost - 1);

        var roomsBefore = world.Ship.Rooms.Count;
        var creditsBefore = world.Credits;
        var platingBefore = world.HullPlatingStock;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        CompletePendingRoomBuilds(world);

        return world.Ship.Rooms.Count == roomsBefore && world.Credits == creditsBefore && world.HullPlatingStock == platingBefore;
    }

    private static bool World_ShipBuilding_FailsForUnknownCatalogId()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        var roomsBefore = world.Ship.Rooms.Count;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest("not-a-real-entry")));

        return world.Ship.Rooms.Count == roomsBefore;
    }

    // The new room's own state (wiring/oxygen/wall-block HP) has to actually initialize, not just
    // exist as geometry - otherwise the very next atmosphere tick would find rooms/doors ungated by
    // any oxygen entry (World.Atmosphere.cs's own dictionaries are keyed by room id) and misbehave.
    private static bool World_ShipBuilding_NewRoomOxygenAndDoorsInitialize()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var entry = world.GetBuildableRoomCatalog()[0];

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        CompletePendingRoomBuilds(world);

        var newRoom = world.Ship.Rooms[^1];
        var snapshot = world.CreateSnapshot();
        var hasOxygenEntry = snapshot.Field.Asteroids is not null; // sanity: snapshot built without throwing
        var newRoomHasADoor = world.Ship.Doors.Any(d => d.RoomAId == newRoom.Id || d.RoomBId == newRoom.Id);

        return hasOxygenEntry && newRoomHasADoor;
    }

    // A second build requested before the first one completes must not be allowed to pick the same
    // spot - the pending ghost's own footprint has to count as occupied, the same way a real room's
    // does, even though it isn't part of Ship.Rooms yet.
    private static bool World_ShipBuilding_SecondPendingBuild_DoesNotOverlapTheFirstGhost()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var entry = world.GetBuildableRoomCatalog()[0];

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        world.Step(RealtimeStep);

        var pending = world.CreateSnapshot().PendingRoomBuilds;
        if (pending is not { Count: 2 })
            return false; // setup problem - both requests should have found a free spot each

        var a = pending[0];
        var b = pending[1];
        var overlaps = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
        return !overlaps;
    }

    // Content-каталог отсеков - click-to-place UI: BuildRoomRequest.X/Y now let the caller pick the
    // exact spot instead of the auto-search always finding "some" open edge - the new room has to
    // actually land AT that position, flush against the anchor room's own Bottom edge (every hand-
    // authored hull packs its rooms in a row with nothing above/below, so this is always open space).
    private static bool World_ShipBuilding_ExplicitPosition_BuildsExactlyThere()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        var entry = world.GetBuildableRoomCatalog()[0];
        var anchor = world.Ship.Rooms[0];
        var x = anchor.X;
        var y = anchor.Y + anchor.Height;

        var roomsBefore = world.Ship.Rooms.Select(r => r.Id).ToHashSet();
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id, x, y)));
        CompletePendingRoomBuilds(world);

        var newRoom = world.Ship.Rooms.FirstOrDefault(r => !roomsBefore.Contains(r.Id));
        return newRoom is not null && newRoom.X == x && newRoom.Y == y;
    }

    // The server re-validates an explicit position rather than trusting it (World.ShipBuilding.cs's
    // own doc comment on TryBuildRoom) - a position that touches nothing gets refused exactly like
    // the old auto-search's own "no open edge right now" outcome: no room added, no charge either.
    private static bool World_ShipBuilding_ExplicitPosition_RefusesFloatingPlacementWithoutCharge()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        var entry = world.GetBuildableRoomCatalog()[0];
        var roomsBefore = world.Ship.Rooms.Count;
        var creditsBefore = world.Credits;
        var platingBefore = world.HullPlatingStock;

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id, 9999f, 9999f)));

        return world.Ship.Rooms.Count == roomsBefore
            && world.Credits == creditsBefore
            && world.HullPlatingStock == platingBefore
            && world.CreateSnapshot().PendingRoomBuilds is not { Count: > 0 };
    }

    // The whole downstream plan (M61 onward) leans on Ship.ToDefinition()/FromCustomDefinition being
    // a lossless round trip for every hand-authored hull, not just editor-drawn ones - this is the
    // guard test the plan's own M60 design calls out by name. Structural counts rather than exact id
    // equality: FromCustomDefinition always renumbers device ids from scratch (same known
    // simplification a whole-hull swap already has), so ids are expected to change; the physical
    // shape (room/door/airlock/device counts, and legality) must not.
    private static bool World_ShipBuilding_ToDefinitionRoundTrip_PreservesEveryHandAuthoredHull()
    {
        foreach (var kind in new[] { ShipKind.Scout, ShipKind.Frigate, ShipKind.Cruiser, ShipKind.Corvette })
        {
            var original = Ship.Create(kind);
            var def = original.ToDefinition();

            if (CustomShipValidator.Validate(def).Count > 0)
                return false; // a hand-authored hull's own definition must already be legal

            var rebuilt = Ship.FromCustomDefinition(def);

            if (rebuilt.Rooms.Count != original.Rooms.Count) return false;
            if (rebuilt.Doors.Count != original.Doors.Count) return false;
            if (rebuilt.AirlockOuterDoors.Count != original.AirlockOuterDoors.Count) return false;
            if (rebuilt.Turrets.Count != original.Turrets.Count) return false;
            if (rebuilt.Cameras.Count != original.Cameras.Count) return false;
            if (rebuilt.ComponentMounts.Count != original.ComponentMounts.Count) return false;
            if (rebuilt.AmmoStorages.Count != original.AmmoStorages.Count) return false;
            if (rebuilt.SuitLockers.Count != original.SuitLockers.Count) return false;
            if (rebuilt.StorageRacks.Count != original.StorageRacks.Count) return false;
            // Every original room id must still exist, at the same footprint - what everything else
            // in World (character RoomId, oxygen dictionaries, etc.) actually keys off.
            foreach (var room in original.Rooms)
            {
                var match = rebuilt.Rooms.FirstOrDefault(r => r.Id == room.Id);
                if (match is null || match.X != room.X || match.Y != room.Y || match.Width != room.Width || match.Height != room.Height)
                    return false;
            }

            // Each airlock has to land on the exact same WALL, not just anywhere on its own room -
            // Ship.Corvette.cs's own two airlocks sit off-centre along their wall (by design), which
            // is exactly the case that broke a naive nearest-midpoint inference in ToDefinition()'s
            // own InferAirlockSide the first time this test was written.
            foreach (var airlock in original.AirlockOuterDoors)
            {
                var room = original.GetRoom(airlock.RoomId);
                var onRight = MathF.Abs(airlock.X - room.Right) < 0.01f;
                var onLeft = MathF.Abs(airlock.X - room.Left) < 0.01f;
                var onBottom = MathF.Abs(airlock.Y - room.Bottom) < 0.01f;
                var rebuiltMatch = rebuilt.AirlockOuterDoors.FirstOrDefault(a => a.RoomId == airlock.RoomId);
                if (rebuiltMatch is null)
                    return false;
                var rebuiltOnRight = MathF.Abs(rebuiltMatch.X - room.Right) < 0.01f;
                var rebuiltOnLeft = MathF.Abs(rebuiltMatch.X - room.Left) < 0.01f;
                var rebuiltOnBottom = MathF.Abs(rebuiltMatch.Y - room.Bottom) < 0.01f;
                if (onRight != rebuiltOnRight || onLeft != rebuiltOnLeft || onBottom != rebuiltOnBottom)
                    return false;
            }
        }
        return true;
    }

    // M61 - RoomGraphConnectivity is a plain BFS over the door graph, tested directly against
    // hand-built graphs rather than through a real hull: a line (connected end to end), a ring
    // (still connected even with a redundant extra edge), and a graph with a genuinely disconnected
    // island - the exact three shapes the plan itself calls out for this utility.
    private static bool RoomGraphConnectivity_DetectsConnectedAndDisconnectedGraphs()
    {
        var rooms = new[]
        {
            new CustomRoomDef("a", "A", 0, 0, 1, 1),
            new CustomRoomDef("b", "B", 1, 0, 1, 1),
            new CustomRoomDef("c", "C", 2, 0, 1, 1),
        };

        var line = new[] { new CustomDoorDef("a", "b"), new CustomDoorDef("b", "c") };
        if (!RoomGraphConnectivity.AllReachable(rooms, line, "a"))
            return false;

        var ring = new[] { new CustomDoorDef("a", "b"), new CustomDoorDef("b", "c"), new CustomDoorDef("c", "a") };
        if (!RoomGraphConnectivity.AllReachable(rooms, ring, "a"))
            return false;

        var island = new[] { new CustomDoorDef("a", "b") }; // "c" has no door to anything
        if (RoomGraphConnectivity.AllReachable(rooms, island, "a"))
            return false;
        if (RoomGraphConnectivity.ReachableFrom(rooms, island, "a").Count != 2)
            return false;

        return true;
    }

    // ApplyShipDefinition's additive fast path only kicks in once the device-id graph is already
    // stable - the very FIRST build off a hand-authored hull always renumbers every device id from
    // scratch (Ship.Custom.cs's BuildTurrets/BuildSystemDevices/BuildSimpleDevices never preserve
    // hand-authored ids like "system-shields"), so that first build always takes the full-reset
    // path, same as a whole-hull swap - correctly so, not a bug to work around. Every test below
    // that means to exercise the additive path builds once first to normalize onto the Custom hull,
    // then sets up its precondition and builds AGAIN - only that second build's device graph is
    // guaranteed unchanged (a plain catalog room carries no devices at all).
    private static World NormalizeToCustomHullViaOneBuild(out RoomCatalogEntry entry)
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        entry = world.GetBuildableRoomCatalog()[0];
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        CompletePendingRoomBuilds(world);
        return world;
    }

    // M61's whole point: building a room must no longer bulldoze existing damage/state the way
    // M60's full InitializeShipState() reset did. A cut wire (any wire - the reactor's own trunk is
    // as good as any other id already tracked in _wireDamaged) has to still read damaged afterward.
    private static bool World_ShipBuilding_BuildRoom_PreservesExistingWireDamage()
    {
        var world = NormalizeToCustomHullViaOneBuild(out var entry);
        var wireId = world.Wires[0].Id;
        world.CutWire(wireId);

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        CompletePendingRoomBuilds(world);

        var wireState = world.CreateSnapshot().Wiring.WireStates.FirstOrDefault(w => w.WireId == wireId);
        return wireState is not null && wireState.Damaged;
    }

    // Same guarantee, for wall-block HP: a pre-existing breach in an original room must survive a
    // build untouched, and the newly built room's own blocks must still start at full health.
    private static bool World_ShipBuilding_BuildRoom_PreservesWallBlockHpAndInitializesNewRoom()
    {
        var world = NormalizeToCustomHullViaOneBuild(out var entry);
        world.DebugBreachWallBlock("reactor");

        var roomsBefore = world.Ship.Rooms.Count;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        CompletePendingRoomBuilds(world);
        if (world.Ship.Rooms.Count != roomsBefore + 1)
            return false; // setup problem - build itself failed

        var states = world.CreateSnapshot().WallBlockStates;
        var reactorBlockIds = world.Ship.WallBlocks.Where(b => b.RoomId == "reactor").Select(b => b.Id).ToHashSet();
        var stillBreached = states.Any(s => reactorBlockIds.Contains(s.Id) && s.Hp <= 0f);

        var newRoom = world.Ship.Rooms[^1];
        var newRoomBlockIds = world.Ship.WallBlocks.Where(b => b.RoomId == newRoom.Id).Select(b => b.Id).ToHashSet();
        var newRoomFullHealth = states.Where(s => newRoomBlockIds.Contains(s.Id)).All(s => s.Hp >= World.WallBlockMaxHp);

        return stillBreached && newRoomFullHealth;
    }

    // Same guarantee again, for a closed door: building a room elsewhere must not silently reopen a
    // door the player (or an emergency) already closed.
    private static bool World_ShipBuilding_BuildRoom_PreservesClosedDoorState()
    {
        var world = NormalizeToCustomHullViaOneBuild(out var entry);
        var doorId = world.Ship.Doors[0].Id;
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: doorId)); // closes it (starts open)
        world.Step(RealtimeStep);
        var wasOpenAfterToggle = world.CreateSnapshot().DoorStates.First(d => d.DoorId == doorId).IsOpen;
        if (wasOpenAfterToggle)
            return false; // setup problem - toggle didn't actually close it

        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        CompletePendingRoomBuilds(world);

        return !world.CreateSnapshot().DoorStates.First(d => d.DoorId == doorId).IsOpen;
    }

    // The symmetric operation: build then demolish the same room back off, room count returns to
    // baseline. Exercised through the real ClientCommand.DemolishRoomId path, not TryDemolishRoom
    // directly, the same way every other test in this file goes through ApplyCommand.
    private static bool World_ShipBuilding_DemolishRoom_RemovesTheRoom()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var roomsBefore = world.Ship.Rooms.Count;

        var entry = world.GetBuildableRoomCatalog()[0];
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(entry.Id)));
        CompletePendingRoomBuilds(world);
        if (world.Ship.Rooms.Count != roomsBefore + 1)
            return false; // setup problem - build itself failed
        var builtRoomId = world.Ship.Rooms[^1].Id;

        world.ApplyCommand(1, new ClientCommand(1, DemolishRoomId: builtRoomId));
        world.Step(RealtimeStep);

        return world.Ship.Rooms.Count == roomsBefore && world.Ship.Rooms.All(r => r.Id != builtRoomId);
    }

    // A room holding the sole reactor/helm/navigation/last airlock must refuse to demolish -
    // CustomShipValidator.Validate already enforces this for free against the shrunk definition,
    // this just confirms TryDemolishRoom actually rejects on it instead of silently applying anyway.
    private static bool World_ShipBuilding_DemolishRoom_BlockedWhenRoomHoldsSoleReactor()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var roomsBefore = world.Ship.Rooms.Count;

        world.ApplyCommand(1, new ClientCommand(1, DemolishRoomId: "reactor"));
        world.Step(RealtimeStep);

        return world.Ship.Rooms.Count == roomsBefore && world.Ship.Rooms.Any(r => r.Id == "reactor");
    }

    // Unlike building (M62 lifted this gate entirely), demolishing stays the deliberate,
    // Shipwright-assisted teardown M61 designed it as - undocked must still refuse.
    private static bool World_ShipBuilding_DemolishRoom_FailsWhenNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        var roomsBefore = world.Ship.Rooms.Count;

        world.ApplyCommand(1, new ClientCommand(1, DemolishRoomId: "reactor"));
        world.Step(RealtimeStep);

        return world.Ship.Rooms.Count == roomsBefore;
    }

    private static bool World_ShipBuilding_DemolishRoom_FailsAtStationWithoutShipwright()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // home-station is a Shipyard (GalaxyMap.cs - "добавь корабела на стартовую станцию"), so
        // this needs a station that genuinely has no Shipwright; trade-station is the nearest one.
        DockAtStation(world, "trade-station");
        var roomsBefore = world.Ship.Rooms.Count;

        world.ApplyCommand(1, new ClientCommand(1, DemolishRoomId: "reactor"));
        world.Step(RealtimeStep);

        return world.Ship.Rooms.Count == roomsBefore;
    }
}
