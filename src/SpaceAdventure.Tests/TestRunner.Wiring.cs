using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // WireLink topology. Laying a wire by physically walking to two pins in turn
    // (ClientCommand.PinInteractId) is M20's job - these tests exercise the graph model itself via
    // World.AddWire, the same primitive that command will call through once it exists.
    private static bool World_Wiring_LayBackup_KeepsSystemPoweredAfterTrunkCut()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 3, PowerDirection: 1f)); // WeaponCharger
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var allocatedBefore = world.CreateSnapshot().Power.Allocated[PowerSystemId.WeaponCharger];

        // Reinforce the trunk with a second wire into the same junction input - the generalized
        // backup mechanic (a Power input pin accepts up to 2 wires, Wire.cs).
        world.AddWire("backup-trunk-weaponcharger",
            new PinRef("distribution", "out-weaponcharger"), new PinRef("junction-weaponcharger", "in"));

        world.CutWire("trunk-weaponcharger"); // primary severed - the backup should now carry it
        var effectiveAfterCut = world.GetEffectivePower(PowerSystemId.WeaponCharger);

        return allocatedBefore > 0f && effectiveAfterCut > 0f;
    }

    private static bool World_Wiring_ShieldsOneDropCut_HalvesEffectivePower()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // PowerSystemId order: Oxygen(0), Engine(1), Shields(2), WeaponCharger(3), Secondary(4).
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 2, PowerDirection: 1f));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var fullPower = world.GetEffectivePower(PowerSystemId.Shields);

        world.CutWire("drop-system-shields-2"); // only the second generator's drop is cut
        var halfPower = world.GetEffectivePower(PowerSystemId.Shields);

        return fullPower > 0f && Math.Abs(halfPower - fullPower / 2f) < 0.01f;
    }

    // The actual bug this independence exists to fix: two identical devices on the same system used
    // to share one junction/trunk, so damaging either one damaged both, and repairing either one
    // repaired both. Each device now gets its own dedicated junction+trunk (WireGraphFactory), so
    // cutting/repairing one must never touch its sibling.
    private static bool World_Wiring_RepairingOneMultiDeviceUnit_DoesNotRepairItsSibling()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.CutWire("trunk-system-engine");
        world.CutWire("trunk-system-engine-2");
        if (world.IsDeviceConnected("system-engine") || world.IsDeviceConnected("system-engine-2"))
            return false; // both should start out damaged

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        WalkAcrossShipTo(world, 7.2f, 4.3f); // reactor room's first engine device (Ship.cs)
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // starts the repair

        for (var i = 0; i < 30 * 30; i++) // 30s, comfortably past the ~25s a passive-only repair takes
            world.Step(RealtimeStep);

        return world.IsDeviceConnected("system-engine") && !world.IsDeviceConnected("system-engine-2");
    }

    // Physical wire-laying (World.Wiring.cs's HandlePinInteract, M20) - the real player-facing
    // counterpart to the World.AddWire test hook the tests above use directly: walk to one pin
    // holding a WireSpool, then to a second.
    private static void PickUpAndHoldSpool(World world)
    {
        var spoolSlot = TakeFromRack(world, ItemType.WireSpool);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: spoolSlot)); // hold it
    }

    // Distribution sits at a fixed spot in the reactor room (Ship.cs); each Junction now takes a
    // spot along the room's own left/right walls instead (WireGraphFactory) - far enough apart
    // that nothing reaches two of them from one single spot any more, so these are 3 separate
    // walks rather than the one midpoint the old row layout allowed.
    private static void MoveToDistribution(World world)
    {
        MoveCharacterTo(world, 1, 19f, 3f);
        MoveCharacterTo(world, 1, 7f, 3f); // reactor room
        MoveCharacterTo(world, 1, 9.5f, 3f); // right on the distribution block
    }

    // junction-oxygen: PowerSystemId.Oxygen is index 0, so it's the first junction placed - left
    // wall (room.Left + 1 = 6), first slot down it (room.Top + 1.5 = 1.5).
    private static void MoveToOxygenJunction(World world)
    {
        MoveCharacterTo(world, 1, 19f, 3f);
        MoveCharacterTo(world, 1, 7f, 3f); // reactor room
        MoveCharacterTo(world, 1, 6f, 1.5f); // junction-oxygen's own spot on the left wall
    }

    // junction-system-engine: Engine has two devices on this hull, so it gets one junction per
    // device (WireGraphFactory) instead of one shared box - this is the first of the two, placed
    // right after junction-oxygen, so it lands on the right wall (room.Right - 1 = 9), same first
    // slot as junction-oxygen (room.Top + 1.5 = 1.5).
    private static void MoveToEngineJunction(World world)
    {
        MoveCharacterTo(world, 1, 19f, 3f);
        MoveCharacterTo(world, 1, 7f, 3f); // reactor room
        MoveCharacterTo(world, 1, 9f, 1.5f); // junction-system-engine's own spot on the right wall
    }

    private static bool HasWireSpool(World world) =>
        world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Any(s => s == ItemType.WireSpool);

    private static bool World_WireLay_ConnectsTwoCompatiblePins_CreatesWireAndConsumesSpool()
    {
        var world = new World();
        world.SpawnCharacter(1);
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);

        var wiresBefore = world.Wires.Count;
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: new PinRef("distribution", "out-oxygen")));
        MoveToOxygenJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: new PinRef("junction-oxygen", "in")));

        return world.Wires.Count == wiresBefore + 1 && !HasWireSpool(world);
    }

    private static bool World_WireLay_IncompatiblePinTypes_RejectsConnection()
    {
        var world = new World();
        world.SpawnCharacter(1);
        PickUpAndHoldSpool(world);
        MoveToOxygenJunction(world);

        var wiresBefore = world.Wires.Count;
        // Both are inputs - a junction's "in" can never legally pair with another input.
        var oxygenIn = new PinRef("junction-oxygen", "in");
        var engineIn = new PinRef("junction-system-engine", "in");
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: oxygenIn));
        MoveToEngineJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: engineIn));

        var stillLaying = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).LayingWireFromPin == engineIn;
        return world.Wires.Count == wiresBefore && HasWireSpool(world) && stillLaying;
    }

    private static bool World_WireLay_InputPinAtCapacity_RejectsThirdWire()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var oxygenOut = new PinRef("distribution", "out-oxygen");
        var junctionIn = new PinRef("junction-oxygen", "in");

        // First reinforcement succeeds - the trunk wire already there plus this one reaches the cap of 2.
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: oxygenOut));
        MoveToOxygenJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: junctionIn));
        var wiresAfterFirst = world.Wires.Count;

        // Second attempt at the same input must be refused - the spool is not spent on a rejection.
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: oxygenOut));
        MoveToOxygenJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: junctionIn));

        return world.Wires.Count == wiresAfterFirst && HasWireSpool(world);
    }

    private static bool World_WireLay_CancelPressed_ClearsPendingLayWithoutConsumingSpool()
    {
        var world = new World();
        world.SpawnCharacter(1);
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);

        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: new PinRef("distribution", "out-oxygen")));
        world.ApplyCommand(1, new ClientCommand(1, WireLayCancelPressed: true));

        var stillLaying = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).LayingWireFromPin;
        return stillLaying is null && HasWireSpool(world);
    }

    private static bool World_WireLay_ClickingSamePinTwice_CancelsPendingLay()
    {
        var world = new World();
        world.SpawnCharacter(1);
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);

        var pin = new PinRef("distribution", "out-oxygen");
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: pin));
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: pin));

        var stillLaying = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).LayingWireFromPin;
        return stillLaying is null && HasWireSpool(world);
    }

    // Bend points (World.cs's WireBendAtX/Y, World.Wiring.cs's HandleWireBend) - a click mid-lay
    // that isn't itself finishing the wire fixes a cosmetic waypoint instead, carried onto the
    // finished Wire's own Bends once the second pin completes it.
    private static bool World_WireLay_BendPoint_CarriesOntoFinishedWire()
    {
        var world = new World();
        world.SpawnCharacter(1);
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);

        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: new PinRef("distribution", "out-oxygen")));
        world.ApplyCommand(1, new ClientCommand(1, WireBendAtX: 11f, WireBendAtY: 4f));
        MoveToOxygenJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: new PinRef("junction-oxygen", "in")));

        var wire = world.Wires.Last();
        return wire.Bends is { Count: 1 } bends && (bends[0] - new Vec2(11f, 4f)).Length() < 0.01f;
    }

    // Right-click now backs out one step at a time (World.Wiring.cs's HandleWireLayCancel): every
    // fixed bend first, last one first, and only once none are left does it fall back to its old
    // behavior of clearing the anchor itself.
    private static bool World_WireLay_CancelPressed_RemovesBendsBeforeTheAnchorItself()
    {
        var world = new World();
        world.SpawnCharacter(1);
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);

        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: new PinRef("distribution", "out-oxygen")));
        world.ApplyCommand(1, new ClientCommand(1, WireBendAtX: 11f, WireBendAtY: 4f));
        world.ApplyCommand(1, new ClientCommand(1, WireBendAtX: 11.5f, WireBendAtY: 4.5f));

        world.ApplyCommand(1, new ClientCommand(1, WireLayCancelPressed: true)); // pops the second bend
        var afterFirstCancel = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var oneBendLeftAndStillLaying =
            (afterFirstCancel.LayingWireBends?.Count ?? 0) == 1 && afterFirstCancel.LayingWireFromPin is not null;

        world.ApplyCommand(1, new ClientCommand(1, WireLayCancelPressed: true)); // pops the first bend
        var afterSecondCancel = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var noBendsLeftAndStillLaying =
            (afterSecondCancel.LayingWireBends?.Count ?? 0) == 0 && afterSecondCancel.LayingWireFromPin is not null;

        world.ApplyCommand(1, new ClientCommand(1, WireLayCancelPressed: true)); // no bends left - cancels the anchor itself
        var anchorClearedAfterThirdCancel = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).LayingWireFromPin is null;

        return oneBendLeftAndStillLaying && noBendsLeftAndStillLaying && anchorClearedAfterThirdCancel && HasWireSpool(world);
    }

    // Nothing about wire-laying is private to the player doing it - a second crew member has to see
    // the trailing wire too (CharacterState.LayingWireFromPin), same reasoning as Cutting/Welding.
    private static bool World_WireLay_OtherPlayersSeeIsLayingWireFlagWhilePending()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        PickUpAndHoldSpool(world);
        MoveToDistribution(world);

        world.ApplyCommand(1, new ClientCommand(1, PinInteractId: new PinRef("distribution", "out-oxygen")));

        var snapshot = world.CreateSnapshot();
        return snapshot.Characters.Single(c => c.PlayerId == 1).LayingWireFromPin is not null
            && snapshot.Characters.Single(c => c.PlayerId == 2).LayingWireFromPin is null;
    }

    // A Junction box is its own breakable device now (game_design.md - "щитки") - "damage" is its
    // own trunk wire (Distribution->Junction) being cut, repaired with the same wrench/screwdriver-
    // driven minigame a SystemDevice already uses (World.SystemRepair.cs), just standing at the box
    // itself instead of at one of its downstream devices.
    private static bool World_Junction_BecomesDamagedOnTrunkCut_AndRepairsWithWrenchHeldInHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.CutWire("trunk-oxygen");
        var damagedRightAfterCut = world.CreateSnapshot().JunctionStates.First(s => s.DeviceId == "junction-oxygen").Damaged;

        MoveToOxygenJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // no tool - should fail
        var stillDamagedWithoutTool = world.IsJunctionDamaged("junction-oxygen");

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        MoveToOxygenJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // starts the repair

        for (var i = 0; i < 30 * 30; i++) // 30s, comfortably past the ~25s a passive-only repair takes
            world.Step(RealtimeStep);

        return damagedRightAfterCut && stillDamagedWithoutTool && !world.IsJunctionDamaged("junction-oxygen");
    }

    // Logic components (World.ComponentLogic.cs, M21) - test-seeded directly via World.AddComponent/
    // AddWire (the same primitives the real purchase-and-install flow, M23, will call through) so
    // evaluation correctness is isolated from the economy. Position/room are arbitrary - these
    // components are never walked to in these tests, only wired and stepped.
    private static bool GateSignal(World world, string id) =>
        world.CreateSnapshot().ComponentStates.First(s => s.ComponentId == id).SignalValue;

    private static bool World_Component_GateAnd_OutputsTrueOnlyWhenBothInputsTrue()
    {
        var world = new World();
        world.AddComponent(new Component("relay-a", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("relay-b", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("gate", ComponentKind.GateAnd, "corridor", 0, 0));
        world.AddWire("w1", new PinRef("relay-a", "out"), new PinRef("gate", "in-a"));
        world.AddWire("w2", new PinRef("relay-b", "out"), new PinRef("gate", "in-b"));

        world.Step(RealtimeStep);
        var bothFalse = GateSignal(world, "gate");

        world.ToggleRelay("relay-a");
        world.Step(RealtimeStep);
        var oneTrue = GateSignal(world, "gate");

        world.ToggleRelay("relay-b");
        world.Step(RealtimeStep);
        var bothTrue = GateSignal(world, "gate");

        return !bothFalse && !oneTrue && bothTrue;
    }

    private static bool World_Component_GateOr_OutputsTrueWhenEitherInputTrue()
    {
        var world = new World();
        world.AddComponent(new Component("relay-a", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("relay-b", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("gate", ComponentKind.GateOr, "corridor", 0, 0));
        world.AddWire("w1", new PinRef("relay-a", "out"), new PinRef("gate", "in-a"));
        world.AddWire("w2", new PinRef("relay-b", "out"), new PinRef("gate", "in-b"));

        world.Step(RealtimeStep);
        var bothFalse = GateSignal(world, "gate");

        world.ToggleRelay("relay-a");
        world.Step(RealtimeStep);
        var oneTrue = GateSignal(world, "gate");

        return !bothFalse && oneTrue;
    }

    private static bool World_Component_GateNot_InvertsInput()
    {
        var world = new World();
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("gate", ComponentKind.GateNot, "corridor", 0, 0));
        world.AddWire("w", new PinRef("relay", "out"), new PinRef("gate", "in"));

        world.Step(RealtimeStep);
        var whenInputFalse = GateSignal(world, "gate");

        world.ToggleRelay("relay");
        world.Step(RealtimeStep);
        var whenInputTrue = GateSignal(world, "gate");

        return whenInputFalse && !whenInputTrue;
    }

    private static bool World_Component_GateXor_OutputsTrueWhenInputsDiffer()
    {
        var world = new World();
        world.AddComponent(new Component("relay-a", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("relay-b", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("gate", ComponentKind.GateXor, "corridor", 0, 0));
        world.AddWire("w1", new PinRef("relay-a", "out"), new PinRef("gate", "in-a"));
        world.AddWire("w2", new PinRef("relay-b", "out"), new PinRef("gate", "in-b"));

        world.Step(RealtimeStep);
        var bothFalse = GateSignal(world, "gate");

        world.ToggleRelay("relay-a");
        world.Step(RealtimeStep);
        var differing = GateSignal(world, "gate");

        world.ToggleRelay("relay-b");
        world.Step(RealtimeStep);
        var bothTrue = GateSignal(world, "gate");

        return !bothFalse && differing && !bothTrue;
    }

    private static bool World_Component_Timer_DelaysOutputUntilTriggerHeldLongEnough()
    {
        var world = new World();
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("timer", ComponentKind.Timer, "corridor", 0, 0, TimerSeconds: 1f));
        world.AddWire("w", new PinRef("relay", "out"), new PinRef("timer", "trigger"));

        world.ToggleRelay("relay");
        for (var i = 0; i < 25; i++) // ~0.83s - clearly short of the 1s delay
            world.Step(RealtimeStep);
        var beforeDelay = GateSignal(world, "timer");

        for (var i = 0; i < 20; i++) // ~1.5s total - clearly past it
            world.Step(RealtimeStep);
        var afterDelay = GateSignal(world, "timer");

        return !beforeDelay && afterDelay;
    }

    private static bool World_Component_Timer_ResetsElapsedWhenTriggerGoesFalse()
    {
        var world = new World();
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("timer", ComponentKind.Timer, "corridor", 0, 0, TimerSeconds: 1f));
        world.AddWire("w", new PinRef("relay", "out"), new PinRef("timer", "trigger"));

        world.ToggleRelay("relay"); // trigger true
        for (var i = 0; i < 25; i++) // ~0.83s - not yet fired
            world.Step(RealtimeStep);
        world.ToggleRelay("relay"); // trigger false again before it could fire
        for (var i = 0; i < 10; i++)
            world.Step(RealtimeStep);
        var afterDrop = GateSignal(world, "timer");

        world.ToggleRelay("relay"); // trigger true again - a fresh count, not a resumed one
        for (var i = 0; i < 25; i++) // only ~0.83s since the restart
            world.Step(RealtimeStep);
        var afterRestart = GateSignal(world, "timer");

        return !afterDrop && !afterRestart;
    }

    private static bool World_Component_Memory_LatchesOnSetAndHoldsUntilReset()
    {
        var world = new World();
        world.AddComponent(new Component("setRelay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("resetRelay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("mem", ComponentKind.Memory, "corridor", 0, 0));
        world.AddWire("w1", new PinRef("setRelay", "out"), new PinRef("mem", "set"));
        world.AddWire("w2", new PinRef("resetRelay", "out"), new PinRef("mem", "reset"));

        world.Step(RealtimeStep);
        var beforeSet = GateSignal(world, "mem");

        world.ToggleRelay("setRelay"); // a pulse: on, then off again
        world.Step(RealtimeStep);
        world.ToggleRelay("setRelay");
        world.Step(RealtimeStep);
        var afterPulse = GateSignal(world, "mem");

        for (var i = 0; i < 10; i++) // holds with no input at all
            world.Step(RealtimeStep);
        var stillHeld = GateSignal(world, "mem");

        return !beforeSet && afterPulse && stillHeld;
    }

    private static bool World_Component_Memory_ResetWinsWhenSetAndResetBothTrue()
    {
        var world = new World();
        world.AddComponent(new Component("setRelay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("resetRelay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("mem", ComponentKind.Memory, "corridor", 0, 0));
        world.AddWire("w1", new PinRef("setRelay", "out"), new PinRef("mem", "set"));
        world.AddWire("w2", new PinRef("resetRelay", "out"), new PinRef("mem", "reset"));

        world.ToggleRelay("setRelay");
        world.ToggleRelay("resetRelay"); // both true at once
        world.Step(RealtimeStep);

        return !GateSignal(world, "mem");
    }

    private static bool World_Component_Relay_TogglesOnInteractAndDrivesWiredGate()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("gate", ComponentKind.GateNot, "corridor", 0, 0));
        world.AddWire("w", new PinRef("relay", "out"), new PinRef("gate", "in"));

        world.Step(RealtimeStep);
        var beforeToggle = GateSignal(world, "gate"); // NOT(false) = true

        world.ApplyCommand(1, new ClientCommand(1, ComponentOperateId: "relay"));
        world.Step(RealtimeStep);
        var afterToggle = GateSignal(world, "gate"); // NOT(true) = false

        return beforeToggle && !afterToggle;
    }

    // A deliberately looped circuit (each NOT gate feeds the other) must never hang or throw - the
    // bounded-pass relaxation in StepComponentLogic guarantees termination every tick regardless of
    // whether the loop ever settles.
    private static bool World_ComponentLogic_CycleOfTwoNotGates_SettlesOrOscillatesWithinBoundedPasses()
    {
        var world = new World();
        world.AddComponent(new Component("notA", ComponentKind.GateNot, "corridor", 0, 0));
        world.AddComponent(new Component("notB", ComponentKind.GateNot, "corridor", 0, 0));
        world.AddWire("w1", new PinRef("notA", "out"), new PinRef("notB", "in"));
        world.AddWire("w2", new PinRef("notB", "out"), new PinRef("notA", "in"));

        for (var i = 0; i < 200; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        return snapshot.ComponentStates.Any(s => s.ComponentId == "notA") && snapshot.ComponentStates.Any(s => s.ComponentId == "notB");
    }

    // Sensors and actuators (World.ComponentLogic.cs, M22) - a sensor watches the room it's
    // physically mounted in (Component.RoomId), no separate "which room" configuration.
    private static bool World_Component_OxygenSensor_FiresWhenRoomOxygenLow()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        world.AddComponent(new Component("o2", ComponentKind.OxygenSensor, "corridor", 0, 0));
        world.Step(RealtimeStep);
        var beforeBreach = GateSignal(world, "o2");

        BreachRoom(world, "corridor");
        for (var i = 0; i < 600 * 30 && world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "corridor").Oxygen >= 50f; i++)
            world.Step(RealtimeStep);
        var afterOxygenDrop = GateSignal(world, "o2");

        return !beforeBreach && afterOxygenDrop;
    }

    private static bool World_Component_BreachSensor_FiresWhileRoomHasBreach()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        world.AddComponent(new Component("br", ComponentKind.BreachSensor, "corridor", 0, 0));
        world.Step(RealtimeStep);
        var beforeBreach = GateSignal(world, "br");

        BreachRoom(world, "corridor");
        world.Step(RealtimeStep);
        var afterBreach = GateSignal(world, "br");

        return !beforeBreach && afterBreach;
    }

    private static bool World_Component_PowerLossSensor_FiresWhenReactorHasNoFuel()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.AddComponent(new Component("pwr", ComponentKind.PowerLossSensor, "corridor", 0, 0));
        world.Step(RealtimeStep);
        var beforeEmpty = GateSignal(world, "pwr");

        for (var i = 0; i < 4; i++)
            world.PowerGrid.Reactor.RemoveRod(i);
        for (var i = 0; i < 3; i++) // PowerGrid.Step (which recomputes CurrentOutput) runs after
            world.Step(RealtimeStep); // StepComponentLogic in the same tick - give it a couple ticks

        return !beforeEmpty && GateSignal(world, "pwr");
    }

    private static bool World_Component_MotionSensor_FiresWhileCharacterInRoom()
    {
        var world = new World();
        world.SpawnCharacter(1); // spawns in corridor
        world.AddComponent(new Component("mot", ComponentKind.MotionSensor, "corridor", 0, 0));
        world.Step(RealtimeStep);
        var whilePresent = GateSignal(world, "mot");

        MoveCharacterTo(world, 1, 7f, 3f); // corridor -> reactor, leaves the watched room
        world.Step(RealtimeStep);
        var afterLeaving = GateSignal(world, "mot");

        return whilePresent && !afterLeaving;
    }

    private static bool World_Component_AutoDoorController_OpensDoorWhenSignalTrue()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("ctrl", ComponentKind.AutoDoorController, "corridor", 0, 0, TargetId: "door-reactor-corridor"));
        world.AddWire("w", new PinRef("relay", "out"), new PinRef("ctrl", "open"));

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-reactor-corridor")); // manually close it (starts open)
        world.Step(RealtimeStep);
        var closedManually = !world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-reactor-corridor").IsOpen;

        world.ToggleRelay("relay");
        world.Step(RealtimeStep);
        var forcedOpen = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-reactor-corridor").IsOpen;

        return closedManually && forcedOpen;
    }

    // AlarmKlaxon/LightToggle apply no world effect of their own - their whole value is the visual
    // state a client reads off ComponentState, a pass-through of their single input.
    private static bool World_Component_AlarmAndLightActuators_ReflectWiredSignalInState()
    {
        var world = new World();
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("alarm", ComponentKind.AlarmKlaxon, "corridor", 0, 0));
        world.AddComponent(new Component("light", ComponentKind.LightToggle, "corridor", 0, 0));
        world.AddWire("w1", new PinRef("relay", "out"), new PinRef("alarm", "on"));
        world.AddWire("w2", new PinRef("relay", "out"), new PinRef("light", "on"));

        world.Step(RealtimeStep);
        var beforeOn = GateSignal(world, "alarm") || GateSignal(world, "light");

        world.ToggleRelay("relay");
        world.Step(RealtimeStep);
        var afterOn = GateSignal(world, "alarm") && GateSignal(world, "light");

        return !beforeOn && afterOn;
    }

    // Purchasable component economy (World.ComponentMounts.cs, M23) - bought from the Trader like
    // any other TradeGood (World() starts docked, so BuyItemType needs no extra setup), installed
    // at one of the Frigate's fixed ComponentMount sockets.
    private static bool World_ComponentMount_InstallFromHeldItem_AddsComponentAndClearsHeldItem()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Relay));
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0));

        world.ApplyCommand(1, new ClientCommand(1, ComponentMountInteractId: "mount-cockpit-1"));

        var snapshot = world.CreateSnapshot();
        var installed = snapshot.ComponentMountStates.First(s => s.MountId == "mount-cockpit-1").InstalledComponentId;
        var itemGone = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.All(s => s != ItemType.Relay);
        return installed is not null && itemGone;
    }

    private static bool World_ComponentMount_Uninstall_ReturnsItemAndRemovesAttachedWires()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.GateNot));
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0));
        world.ApplyCommand(1, new ClientCommand(1, ComponentMountInteractId: "mount-cockpit-1"));

        var installedId = world.CreateSnapshot().ComponentMountStates.First(s => s.MountId == "mount-cockpit-1").InstalledComponentId!;
        world.AddComponent(new Component("relay-x", ComponentKind.Relay, "cockpit", 0, 0));
        world.AddWire("wire-test", new PinRef("relay-x", "out"), new PinRef(installedId, "in"));

        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench)); // lands in the now-free slot 0
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0));
        world.ApplyCommand(1, new ClientCommand(1, ComponentMountInteractId: "mount-cockpit-1"));

        var after = world.CreateSnapshot();
        var mountEmpty = after.ComponentMountStates.First(s => s.MountId == "mount-cockpit-1").InstalledComponentId is null;
        var itemReturned = after.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Any(s => s == ItemType.GateNot);
        var wireGone = world.Wires.All(w => w.Id != "wire-test");
        return mountEmpty && itemReturned && wireGone;
    }

    private static bool World_ComponentMount_RelayTogglesWithoutStartingWireLay()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Relay));
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0));
        world.ApplyCommand(1, new ClientCommand(1, ComponentMountInteractId: "mount-cockpit-1"));
        var installedId = world.CreateSnapshot().ComponentMountStates.First(s => s.MountId == "mount-cockpit-1").InstalledComponentId!;

        world.Step(RealtimeStep);
        var before = GateSignal(world, installedId);

        world.ApplyCommand(1, new ClientCommand(1, ComponentMountInteractId: "mount-cockpit-1")); // empty hands -> operate
        world.Step(RealtimeStep);
        var after = GateSignal(world, installedId);

        return !before && after;
    }

    private static bool World_ShipPurchase_ResetsComponentMountsAndWiring()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Relay));
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0));
        world.ApplyCommand(1, new ClientCommand(1, ComponentMountInteractId: "mount-cockpit-1"));
        var installedBefore = world.CreateSnapshot().ComponentMountStates.First(s => s.MountId == "mount-cockpit-1").InstalledComponentId;

        // Only a Shipyard-kind station sells hulls (game_design.md section 10) - the home outpost
        // has no Shipwright at all.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));

        var afterMounts = world.CreateSnapshot().ComponentMountStates;
        return installedBefore is not null && world.CurrentShipKind == ShipKind.Scout && afterMounts.All(s => s.InstalledComponentId is null);
    }

    // ToolStation is gone entirely - every hand tool/tank/weapon/consumable that used to be a
    // scattered pickup now starts as 3 units in the ship's own storage racks (game_design.md
    // section 13), split evenly across the two shelves every hull carries.
    private static bool World_Storage_RackStartsWithThreeOfEachStarterItemAcrossTwoShelves()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var snapshot = world.CreateSnapshot();

        ItemType[] expectedTypes =
        {
            ItemType.Wrench, ItemType.Screwdriver, ItemType.Cutter, ItemType.WeldingTool,
            ItemType.OxygenTank, ItemType.WeldingTank, ItemType.FuelRod, ItemType.MedKit,
            ItemType.WireSpool, ItemType.Knife, ItemType.Rifle, ItemType.LaserRifle,
        };

        return snapshot.StorageRacks.Count == 2 &&
            expectedTypes.All(t => snapshot.RackSlots.Count(s => s == t) == 3);
    }

    // Swapping hulls at the Shipyard resets every other piece of ship-keyed state (wiring, component
    // mounts) back to factory-fresh - the new hull's shelves shouldn't inherit whatever the old
    // one's held (emptied out here by handing an item from the rack straight overboard).
    private static bool World_Storage_RackResetsOnShipPurchase()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);

        var wrenchSlot = Array.IndexOf(world.CreateSnapshot().RackSlots.ToArray(), ItemType.Wrench);
        var rack = world.Ship.StorageRacks[wrenchSlot / StorageRack.Capacity];
        WalkAcrossShipTo(world, rack.X, rack.Y);
        world.ApplyCommand(1, new ClientCommand(1, DropItemFrom: new SlotRef(ItemSlotKind.Rack, wrenchSlot)));
        var depletedCount = world.CreateSnapshot().RackSlots.Count(s => s == ItemType.Wrench);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));

        var restockedCount = world.CreateSnapshot().RackSlots.Count(s => s == ItemType.Wrench);
        return depletedCount == 2 && restockedCount == 3;
    }

}
