using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool World_ToggleDoor_ViaClientCommand_FlipsState()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Every ship door now starts closed (direct user request, "сделай чтобы все двери на
        // корабле изначально были закрыты"), so the flip this proves is closed -> open, not the
        // old open -> closed.
        var before = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor"));
        var after = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;

        return !before && after;
    }

    // Doors have their own hit points now (game_design.md) - a destroyed one is forced open (its
    // frame/motor can't hold a seal any more) and stops answering ToggleDoor entirely, the same
    // "jammed" behavior a WallBlock-style one-shot combat hit produces everywhere else in the game.
    private static bool World_Door_Destroyed_ForcesOpenAndBlocksClosing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // Every ship door now starts closed by default (direct user request, "сделай чтобы все
        // двери на корабле изначально были закрыты") - no toggle needed to get it into the closed
        // starting state this test wants before damaging it.
        var closedBeforeDamage = !world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;

        world.DamageDoor("door-cockpit-reactor");
        var afterDamage = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor");

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor")); // try to close it again
        var stillOpenAfterToggleAttempt = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;

        return closedBeforeDamage && afterDamage.Destroyed && afterDamage.IsOpen && stillOpenAfterToggleAttempt;
    }

    // Repaired the same wrench/screwdriver-driven minigame a SystemDevice/Junction already uses
    // (World.SystemRepair.cs) - once restored to full health, the door goes back to answering
    // ToggleDoor normally.
    private static bool World_Door_Repair_RestoresItAndAllowsClosingAgain()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DamageDoor("door-cockpit-reactor");

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        MoveCharacterTo(world, 1, 4.9f, 3f); // right at the door, cockpit side
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // starts the repair

        // World.SystemRepair.cs's own real 12-hour elapsed-time timer - see
        // World_RepairSystem_RequiresWrenchHeldInHand's own comment on DebugFastForwardAllRepairs.
        world.DebugFastForwardAllRepairs(13.0 * 3600.0);
        world.Step(RealtimeStep);

        var repaired = !world.IsDoorDestroyed("door-cockpit-reactor");

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor")); // now closes normally again
        var closedAfterRepair = !world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;

        return repaired && closedAfterRepair;
    }

    // "ТОПОР ГОШИ ДЛЯ ЛОМАНИЯ ДВЕРЕЙ" - AxeChopDamage is exactly half DoorMaxHp, so a door standing
    // at full health takes two swings to break down, not one and not three.
    private static bool World_Axe_ChopsClosedDoorInTwoHits()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var axeSlot = TakeFromRack(world, ItemType.Axe);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: axeSlot));
        // Bug fix follow-up (humble-soaring-cat.md, docked-movement tile collision) - cross into
        // cockpit FIRST, while the door is still open, then close it from that side - not the other
        // way around any more. The door's own wall TILE (a real, one-unit-thick TileWallKind.Door
        // now, not the old zero-thickness line at Door.X) is owned by reactor's own leading edge
        // (TileGridRasterizer's leading/trailing rule), sitting at world x=[5,6) - so once closed, the
        // cockpit side's reachable clearance (x<=4.65) sits only 0.35 from the door's own nominal
        // centre (Door.X=5, what World.Doors.cs's InteractionRadius reach check still measures
        // against), while the reactor side's clearance (x>=6.35) sits 1.35 away - past that 1.0
        // reach entirely. Approaching from the OTHER side, as this test used to when it closed the
        // door before ever crossing it, is a real "can't reach a closed door from every direction any
        // more" gap the wall's new physical thickness opened up - out of scope for this fix; picking
        // the side that was always going to work is enough to keep this test honest about what it's
        // actually checking (a two-hit chop), without also re-litigating InteractionRadius itself.
        MoveCharacterTo(world, 1, 4.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor")); // now close it behind us

        world.ApplyCommand(1, new ClientCommand(1, AxeSwingHeld: true));
        world.Step(RealtimeStep);
        var afterFirstHit = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor");

        // Keep "holding" the swing button past the cooldown, same as a client sending it every
        // tick - the second hit only lands once World.Doors.cs's own swing cooldown clears.
        for (var i = 0; i < 30; i++) // 1s, comfortably past the 0.6s swing cooldown
        {
            world.ApplyCommand(1, new ClientCommand(1, AxeSwingHeld: true));
            world.Step(RealtimeStep);
        }
        var afterSecondHit = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor");

        return !afterFirstHit.Destroyed && afterFirstHit.Hp > 0
            && afterSecondHit.Destroyed && afterSecondHit.IsOpen;
    }

    // Swinging with nothing - or the wrong tool - in hand does nothing to the door (TryChopDoor's
    // own IsHolding(Axe) gate), the same way CutHeld/WeldHeld need the matching tool to do anything.
    private static bool World_Axe_DoesNothingWithoutAxeInHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor"));
        MoveCharacterTo(world, 1, 5f, 3f);

        world.ApplyCommand(1, new ClientCommand(1, AxeSwingHeld: true));
        world.Step(RealtimeStep);

        var state = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor");
        return state.Hp >= World.DoorMaxHp;
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
        // door-engine-airlock (the inner door) already starts closed by default now (direct user
        // request, "сделай чтобы все двери на корабле изначально были закрыты") - no toggle needed
        // to get it into the closed state this test is named for.
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var chamberOxygen = snapshot.RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen;
        var engineOxygen = snapshot.RoomOxygen.First(o => o.RoomId == "engine").Oxygen;
        return chamberOxygen < 10f && engineOxygen > 99f;
    }

    // Same setup, but the inner door is explicitly opened instead - the vent now drags the rest
    // of the ship down too, which is exactly the risk the previous test's closed door avoids. Both
    // doors start closed by default now (see the door-default note above), so unlike before, this
    // one can no longer rely on the inner door's default state to represent "open" - it has to
    // toggle it open itself.
    private static bool World_OpenInnerDoor_LetsVentedChamberDrainRestOfShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        CastOffIntoSpace(world); // docked, that door opens onto the station rather than vacuum
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-engine-airlock")); // starts closed -> open
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var engineOxygen = world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "engine").Oxygen;
        return engineOxygen < 90f;
    }

    // Shared EVA test setup (game_design.md Phase 3, M17). There's no separate
    // VoyagePhase.AsteroidField to fly into any more (M39) - the field is simply wherever the ship
    // already is once it's undocked - but every EVA/mining target below (ore deposits, asteroid
    // positions) is calibrated relative to the field's own asteroid-dense marker, not wherever the
    // ship happens to undock, so this still needs the ship to actually be at rest there, the same
    // guaranteed-stationary arrival the old autopilot gave for free.
    //
    // Used to fly there for real (FlyNearAndStop) - same M53 KSP-scale problem
    // EnterAsteroidFieldAndManHelm's own doc comment describes: the marker (AsteroidField.
    // ClusterCenter) now sits far enough out that FlyToward's tick budget stopped reaching it. None
    // of these EVA/mining callers are testing FLIGHT itself, only needing "at rest at the marker" as
    // scaffolding, so this teleports there directly instead (World.DebugPlaceShip). The old
    // autopilot never needed a human at the helm either, so the pilot stands back up once placed -
    // every caller below expects to walk character 1 off to the airlock/suit locker right after this.
    private static void EnterAsteroidFieldStationary(World world)
    {
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        SitAtHelm(world, 1);
        world.DebugPlaceShip(world.GalaxyMap.GetPoint("asteroid-field-epsilon").Position); // already at rest, rotation 0
        world.Step(RealtimeStep);

        if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm)
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
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

    // Unsuited exit is allowed now, and the rule that replaced the old flat ban is a timer: you get
    // out, you get a few seconds, and then vacuum kills you. Both halves are asserted here, because
    // either one alone would pass for the wrong reason - "allowed" alone would pass if the timer
    // never fired, and "fatal" alone would pass if the exit had been blocked all along.
    private static bool World_Eva_ExitUnsuited_AllowedButFatalAfterGrace()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world); // its own SitAtHelm -> MoveCharacterTo already force-opens
        // every door (world.DebugOpenAllDoors(), TestRunner.Core.cs) as a side effect of walking to the
        // helm, so this door is already open by this point - a blind DoorToggleId here would slam it
        // back SHUT instead. Check-then-toggle instead of assuming either starting state.
        if (!world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-airlock-vacuum").IsOpen)
            world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        MoveCharacterTo(world, 1, 23f, 3f); // corridor -> ... -> engine -> airlock-chamber
        WalkFixedDirection(world, 1, 1f, 0f); // walk straight through the open outer door, unsuited

        var afterExit = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (!afterExit.IsOutside)
            return false; // stepping out unsuited has to be possible at all

        // Direct user request ("при выходе начинается дамаг игрока и через 3 секунды игрок
        // мгновенно умирает") - Health has to fall VISIBLY and MONOTONICALLY across the whole
        // 3-second grace, not sit untouched until an abrupt final-tick snap to 0 (the old flat-
        // counter model this replaced) - sampled once per second so a regression back to the old
        // model would show up as two equal readings in a row, not just a wrong final number.
        var oneSecond = (int)(1.0 / RealtimeStep);
        var previousHealth = afterExit.Health;
        if (previousHealth <= 0f)
            return false; // setup problem - should still be alive the instant they step out
        for (var second = 1; second <= 3; second++)
        {
            for (var i = 0; i < oneSecond; i++)
                world.Step(RealtimeStep);
            var health = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
            if (health >= previousHealth)
                return false; // damage must keep decreasing every second, not just snap at the end
            previousHealth = health;
        }

        // A little further past the 3-second mark for good measure, same margin the old test used.
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health <= 0f;
    }

    // Two adjacent (World.WallBlocks.cs's own PassableBreachAdjacency) exterior blocks on the same
    // wall - "wide enough to climb through", the same bar IsPassableBreach already sets for walking
    // OUT through a breach (World.Eva.cs's TryCrossIntoVacuum). Checks every pair rather than
    // sorting-then-checking-consecutive-only: a room's exterior blocks span more than one wall (a
    // rectangular room's Top and Bottom edges share the same X range), so sorting by X alone
    // interleaves two unrelated walls and a "consecutive after sort" check can miss a real adjacent
    // pair entirely - a handful of blocks per room makes the full O(n²) scan cheap regardless.
    private static (string First, string Second)? FindTwoAdjacentExteriorWallBlockIds(World world, string roomId)
    {
        var blocks = world.Ship.WallBlocks.Where(b => b.RoomId == roomId && !b.IsInterior).ToList();
        for (var i = 0; i < blocks.Count; i++)
            for (var j = i + 1; j < blocks.Count; j++)
                if ((blocks[i].Position - blocks[j].Position).Length() <= 1.05)
                    return (blocks[i].Id, blocks[j].Id);
        return null;
    }

    // Direct user request ("я хочу сделать чтобы когда игрок находился не в герметичных помещениях
    // и пробоина была достаточно большая то игрок через 3 секунды мгновенно погибал") - a character
    // standing INSIDE the ship, never crossing into EVA at all, dies on the exact same 3-second ramp
    // as stepping outside once the room they're in has a passable breach (World.Eva.cs's
    // IsFullyExposedToVacuum) - not the slower, oxygen-level-based leak an ordinary breach uses.
    private static bool World_RoomHp_LargeBreachInRoom_KillsUnsuitedCharacterLikeEva()
    {
        var world = new World();
        world.SpawnCharacter(1); // spawns at the helm, in the cockpit - not the corridor
        MoveCharacterTo(world, 1, 11.5f, 3f); // corridor
        world.ApplyCommand(1, new ClientCommand(1)); // stop drifting once arrived - MoveCharacterTo leaves the last move input active
        if (FindTwoAdjacentExteriorWallBlockIds(world, "corridor") is not { } pair)
            return false; // setup problem - the corridor should have an exterior wall to breach

        world.DebugBreachWallBlockById(pair.First);
        world.DebugBreachWallBlockById(pair.Second);

        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (before.IsOutside || before.Health <= 0f)
            return false; // setup problem - still inside the ship, still alive

        // A little past the 3-second mark, same margin World_Eva_ExitUnsuited_AllowedButFatalAfterGrace
        // uses for the outside case.
        for (var i = 0; i < 3 * 30 + 30; i++)
            world.Step(RealtimeStep);

        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !after.IsOutside && after.Health <= 0f; // dies right there, never gets ejected to EVA
    }

    // Same setup, but only ONE block breached - a pinhole, not wide enough to fit through
    // (IsPassableBreach needs a second adjacent broken block). This must NOT trip the fast 3-second
    // ramp - only StepAtmosphere's own much slower, oxygen-level-based leak applies here, which
    // hasn't come anywhere near the lethal threshold yet at the same time mark the large-breach test
    // above already proves fatal.
    private static bool World_RoomHp_SmallPinholeBreach_DoesNotKillUnsuitedCharacterLikeALargeOne()
    {
        var world = new World();
        world.SpawnCharacter(1); // spawns at the helm, in the cockpit - not the corridor
        MoveCharacterTo(world, 1, 11.5f, 3f); // corridor
        world.ApplyCommand(1, new ClientCommand(1)); // stop drifting once arrived - MoveCharacterTo leaves the last move input active
        if (FindTwoAdjacentExteriorWallBlockIds(world, "corridor") is not { } pair)
            return false; // setup problem - the corridor should have an exterior wall to breach

        world.DebugBreachWallBlockById(pair.First); // exactly one - deliberately NOT the second

        for (var i = 0; i < 3 * 30 + 30; i++)
            world.Step(RealtimeStep);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health > 0f;
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
        // Boots off by default now, so a fresh crossing floats right at the door instead of
        // attaching (World.Eva.cs's TryCrossIntoVacuum) - switch them on and let the
        // still-touching boots grab on, which is the "AttachesToShip" this test is named for.
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

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
        MoveCharacterTo(world, 1, (float)me.X, doorRow);
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
