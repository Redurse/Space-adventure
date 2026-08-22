using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Magnetic boots hold you against the plating, not somewhere in a shell around it: wherever you
    // walk, you're on the hull's outline - never floating a metre or two off it, and never adrift
    // across the middle of the footprint with nothing underfoot.
    private static bool World_Eva_MagnetizedWalk_StaysFlushAgainstTheHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // out onto the hull
        // Boots off by default leaves a fresh crossing floating right at the door
        // (World.Eva.cs's TryCrossIntoVacuum); this test is about magnetized walking, so switch
        // them on and let the still-touching boots grab on before checking anything.
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        var rooms = world.Ship.Rooms;
        var hullCenter = new Vec2(
            (rooms.Min(r => r.Left) + rooms.Max(r => r.Right)) / 2,
            (rooms.Min(r => r.Top) + rooms.Max(r => r.Bottom)) / 2);

        // The invariant is one number: standing exactly the boot clearance off the plating,
        // measured against the compartments themselves (HullSilhouette). Checking it per axis
        // against a bounding box - which is what this used to do - can't describe an outside
        // corner, where the right answer is to pivot around it at a constant distance.
        // The ship is stationary and unrotated here, so world position minus the field position is
        // the hull-local offset directly.
        bool OnTheOutline()
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            if (!me.IsOutside || !me.IsEvaAttached)
                return false;
            var field = world.CreateSnapshot().ShipField;
            var layoutPoint = hullCenter + new Vec2(me.X - field.X, me.Y - field.Y);
            return Math.Abs(HullSilhouette.DistanceOutside(rooms, layoutPoint) - 0.35f) < 0.02f;
        }

        if (!OnTheOutline())
            return false;

        // Walk each way in turn, including straight into the hull and around a corner.
        foreach (var (dx, dy) in new[] { (0f, -1f), (-1f, 0f), (0f, 1f), (1f, 0f), (-1f, -1f) })
        {
            for (var i = 0; i < 40; i++)
            {
                world.ApplyCommand(1, new ClientCommand(1, MoveX: dx, MoveY: dy));
                world.Step(RealtimeStep);
                if (!OnTheOutline())
                    return false;
            }
        }

        return true;
    }

    // The magnets are contact, not proximity. They used to reach a couple of units, which snatched
    // a jump out of the air and snapped the character onto a surface it visibly hadn't reached yet -
    // the flight ended before it arrived. Measured at the moment the boots grab, on the position
    // from the tick *before* the snap: the gap that was actually crossed, not where the snap put it.
    private static bool World_Eva_BootsGrabOnContact_NotAcrossTheGap()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        var deposit = world.AsteroidField.OreDeposits.First(d => d.Id == "ore-4b");
        var rock = world.AsteroidField.Asteroids.First(a => a.Id == deposit.AsteroidId);
        ExitShipIntoVacuum(world);

        var start = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var push = new Vec2(deposit.X - start.X, deposit.Y - start.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: push.X, PushOffDirectionY: push.Y));

        var gapWhenCaught = float.NaN;
        for (var i = 0; i < 40 * 30; i++)
        {
            var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var gapBefore = AsteroidShape.DistanceOutside(rock, new Vec2(before.X, before.Y));
            var toward = new Vec2(deposit.X - before.X, deposit.Y - before.Y).Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: toward.X, MoveY: toward.Y));
            world.Step(RealtimeStep);

            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsEvaAttached)
            {
                gapWhenCaught = gapBefore;
                break;
            }
        }

        // The boots reach half a unit; the last tick of flight covers a few tenths more at the speed
        // a jetpack burn builds up, and the grab is found by sampling along that step. So contact
        // is a gap of about one unit measured a tick early - and nowhere near the couple of units
        // the old proximity magnets grabbed from, which is what this pins down. A gap this small
        // also proves it caught this rock rather than something it passed on the way.
        return !float.IsNaN(gapWhenCaught) && gapWhenCaught < 1f;
    }

    // Stepping out leaves you standing on the door's own rectangle now that there's no nudge into
    // open space - walking along the hull away from it must not read as "walked back into the
    // airlock" and drag you inside again.
    private static bool World_Eva_WalkingAwayFromTheDoor_StaysOutside()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);
        // Boots off by default leaves a fresh crossing floating right at the door, not attached
        // (World.Eva.cs's TryCrossIntoVacuum); this test is specifically about walking along the
        // hull, so switch them on and let the still-touching boots grab on first.
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        // Along the hull, away from the door - and also pushing outward, straight at the door's own
        // rectangle from the outside. Neither counts as going back in.
        foreach (var (dx, dy) in new[] { (0f, -1f), (1f, 0f), (0f, 1f) })
        {
            for (var i = 0; i < 40; i++)
            {
                world.ApplyCommand(1, new ClientCommand(1, MoveX: dx, MoveY: dy));
                world.Step(RealtimeStep);
                if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside)
                    return false;
            }
        }

        return true;
    }

    // Player 1 exits and stays magnetized to the hull; player 2 stays inside and pilots the ship
    // from the helm - player 1's world position must shift in lockstep, since a magnetized EVA
    // character moves rigidly with whatever it's attached to (game_design.md Phase 3, M17).
    private static bool World_Eva_AttachedToShip_MovesWithShipWhenShipMoves()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);
        // Boots off by default leaves a fresh crossing floating right at the door, not attached
        // (World.Eva.cs's TryCrossIntoVacuum) - this test is specifically about rigid attached
        // movement, so switch them on and let the still-touching boots grab on first.
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        var beforeSnapshot = world.CreateSnapshot();
        var player1Before = beforeSnapshot.Characters.Single(c => c.PlayerId == 1);
        if (!player1Before.IsOutside)
            return false;
        var shipBefore = beforeSnapshot.ShipField;

        world.ApplyCommand(2, new ClientCommand(2, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        MoveCharacterTo(world, 2, 3f, 3f);
        var helmConsole = world.Ship.HelmConsole.Position;
        MoveCharacterTo(world, 2, helmConsole.X, helmConsole.Y); // helm console
        world.ApplyCommand(2, new ClientCommand(2, InteractPressed: true)); // man it
        world.ApplyCommand(2, new ClientCommand(2, HelmThrottle: 1f)); // straight +X - no rotation involved
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var afterSnapshot = world.CreateSnapshot();
        var shipAfter = afterSnapshot.ShipField;
        var player1After = afterSnapshot.Characters.Single(c => c.PlayerId == 1);

        var shipDeltaX = shipAfter.X - shipBefore.X;
        var characterDeltaX = player1After.X - player1Before.X;

        return shipDeltaX > 0.5f && Math.Abs(characterDeltaX - shipDeltaX) < 0.1f;
    }

    private static bool World_Eva_PushOff_BecomesFreeFloatingWithVelocity()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);
        // Boots on, and one step to let the still-touching boots grab on: PushOffPressed is a
        // no-op while not attached (World.Eva.cs's HandlePushOff), and this test needs a real push
        // to check the free-floating velocity it leaves behind.
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: 1f, PushOffDirectionY: 0f));
        world.Step(RealtimeStep);

        var afterPush = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (afterPush.IsEvaAttached)
            return false; // must be free-floating now

        var posBefore = new Vec2(afterPush.X, afterPush.Y);
        for (var i = 0; i < 30; i++) // 1s of pure drift, no further input
            world.Step(RealtimeStep);
        var afterDrift = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var posAfter = new Vec2(afterDrift.X, afterDrift.Y);

        return posAfter.X > posBefore.X + 0.5f;
    }

    private static bool World_Eva_Jetpack_ExhaustsFuelThenKeepsDriftingAtLastVelocity()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);
        // Boots on and one settling step, so the push-off below actually has something to push
        // off from (World.Eva.cs's HandlePushOff is a no-op while not attached).
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);
        // Push off along the same axis as the jetpack burn below (+Y) - clears the ship's attach
        // zone faster than pushing sideways would (the zone hugs the whole hull along X).
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: 0f, PushOffDirectionY: 1f));
        world.Step(RealtimeStep);

        // Burn through all jetpack fuel holding a thrust direction - JetpackMaxFuel(500) /
        // JetpackFuelPerSecond(10) = 50s - stopping as soon as it's actually empty rather than
        // continuing to drift needlessly further on a loop sized for the worst case.
        for (var i = 0; i < 60 * 30; i++)
        {
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).JetpackFuel <= 0f)
                break;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 1));
            world.Step(RealtimeStep);
        }

        var afterBurn = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (afterBurn.JetpackFuel != 0f)
            return false; // should be fully exhausted well before 15s

        // Two more equal windows, still holding the same input the whole time: if the jetpack still
        // had any effect, the second window's displacement would exceed the first's (still
        // accelerating). Out of fuel, velocity is constant, so the two should match closely.
        var posAtEmpty = new Vec2(afterBurn.X, afterBurn.Y);
        for (var i = 0; i < 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 1));
            world.Step(RealtimeStep);
        }
        var afterWindow1 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var delta1 = new Vec2(afterWindow1.X - posAtEmpty.X, afterWindow1.Y - posAtEmpty.Y).Length();

        for (var i = 0; i < 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 1));
            world.Step(RealtimeStep);
        }
        var afterWindow2 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var delta2 = new Vec2(afterWindow2.X - afterWindow1.X, afterWindow2.Y - afterWindow1.Y).Length();

        return Math.Abs(delta1 - delta2) < 0.05f;
    }

    private static bool World_Eva_AutoReattachToShip_WhenDriftingBack()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);
        // Boots on: off by default now, so crossing the airlock left this character floating
        // right at the door rather than attached (World.Eva.cs's TryCrossIntoVacuum). One more
        // step after switching them on lets the still-touching boots grab on immediately - this
        // test is specifically about the reattach-on-contact mechanic once safely attached again,
        // which is also what PushOffPressed itself requires (World.Eva.cs's HandlePushOff is a
        // no-op while already unattached).
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: 1f, PushOffDirectionY: 0f));
        for (var i = 0; i < 30; i++) // drift away for 1s
            world.Step(RealtimeStep);

        var midway = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (midway.IsEvaAttached)
            return false; // sanity: shouldn't have reattached yet, still moving away

        // Jetpack thrust back toward the ship, stopping the instant it reattaches (game_design.md
        // Phase 3, M17) - held any longer than that and, now walking rather than drifting, it'd
        // just keep going and walk itself straight back inside through the airlock door within the
        // same loop, which isn't what this test is checking.
        var reattached = false;
        for (var i = 0; i < 10 * 30 && !reattached; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
            reattached = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsEvaAttached;
        }

        return reattached;
    }

    private static bool World_Eva_ReenterShip_ReturnsInsideAtAirlockChamber()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // exit, boots off by default so not attached yet
        // This test is specifically about walking back in while attached, so switch boots on and
        // let the still-touching boots grab on first (World.Eva.cs's TryCrossIntoVacuum).
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        var afterExit = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (!afterExit.IsOutside)
            return false;

        // Walk back in the -X direction (toward the door) while still attached.
        for (var i = 0; i < 5 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }

        var afterReturn = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !afterReturn.IsOutside;
    }

    // Shared M18 mining setup: pick up and hold the cutter, suit up, exit through the (opened)
    // outer door, then fly - aiming continuously at the target the whole way, coasting once the
    // jetpack's fuel runs out - to within mining range of a specific ore deposit.
    // Inventory.WornSuitSlot - the socket on the suit being worn, addressed like a row slot.
    private const int WornSuitSlotIndex = -1;

    // A held tool burns its tank the whole time it's lit, whether or not anything is actually in
    // reach to weld (same rule the cutter already lives by) - holding it outside while drifting
    // past nothing in particular spends the tank same as holding it uselessly indoors would. This
    // pins down that an EVA trip and back leaves the *rest* of welding intact: the tank keeps
    // exactly the charge it drained to (nothing extra vanishes crossing the airlock either way),
    // and a real breach back inside still welds normally afterward.
    private static bool World_Weld_SurvivesAnEvaRoundTripWithoutLosingUnrelatedCharge()
    {
        var world = new World();
        world.SpawnCharacter(1);

        var weldingToolSlot = TakeFromRack(world, ItemType.WeldingTool);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: weldingToolSlot));
        TakeTankFromRack(world, ItemType.WeldingTank);
        AttachTankTo(world, Array.IndexOf(
            world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.WeldingTool),
            ItemType.WeldingTank);

        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);
        // Boots on and one settling step: this test walks back in through the airlock later on,
        // which only works while attached (World.Eva.cs's StepShipAttachedWalk) - boots off by
        // default would otherwise leave it drifting free instead.
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside)
            return false;

        // Hold the weld button pointed at nothing in particular for a while, same as a player
        // holding it while looking around outside with no breach actually in reach yet.
        for (var i = 0; i < 15 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: 0f, LookY: -1f));
            world.Step(RealtimeStep);
        }

        var chargeBeforeReturn = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).WelderTank;
        if (chargeBeforeReturn is not { } charge || charge <= 0f || charge >= WeldingTankDefinitions.FullCharge)
            return false; // sanity check: it should have drained some, but not all, of a full tank

        world.ApplyCommand(1, new ClientCommand(1, WeldHeld: false));
        for (var i = 0; i < 5 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }

        var afterReturn = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (afterReturn.IsOutside || afterReturn.WelderTank != chargeBeforeReturn)
            return false; // crossing the airlock either way must not touch the tank on its own

        BreachRoom(world, "corridor");
        var breachCountBefore = CountBreaches(world.CreateSnapshot(), "corridor");
        if (breachCountBefore == 0)
            return false;

        // Walk to whichever wall the breach actually landed on (top or bottom row) rather than
        // assuming one - the welder only reaches ~1.7 units, and BreachRoom's target is random.
        var breachedBlock = world.CreateSnapshot().WallBlocks.First(bl => bl.RoomId == "corridor" &&
            world.CreateSnapshot().WallBlockStates.First(s => s.Id == bl.Id).Breached);
        WalkAcrossShipTo(world, breachedBlock.X, breachedBlock.Y > 3f ? breachedBlock.Y - 0.5f : breachedBlock.Y + 0.5f);

        for (var i = 0; i < 5 * 30 && CountBreaches(world.CreateSnapshot(), "corridor") == breachCountBefore; i++)
        {
            var snapshot = world.CreateSnapshot();
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            var target = snapshot.WallBlocks
                .Where(b => b.RoomId == "corridor" && snapshot.WallBlockStates.First(s => s.Id == b.Id).Breached)
                .OrderBy(b => (new Vec2(b.X, b.Y) - new Vec2(me.X, me.Y)).Length())
                .First();
            var aim = new Vec2(target.X - me.X, target.Y - me.Y);
            aim = aim.Length() > 0.01f ? aim.Normalized() : new Vec2(0f, -1f);
            world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: aim.X, LookY: aim.Y));
            world.Step(RealtimeStep);
        }

        return CountBreaches(world.CreateSnapshot(), "corridor") == breachCountBefore - 1;
    }
}
