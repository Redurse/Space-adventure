using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Flies the ship to the station's berth and leaves it parked there, without docking - the
    // shared setup for the button tests below.
    private static void ApproachBerth(World world, string targetPointId = "trade-station")
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: targetPointId));
        // Deep hostility with whoever owns some *other* nearby sector can still snag the ship into
        // an incidental battle en route to the berth (World.Voyage.cs's Arrive - its own comment
        // calls this the "universal capture-radius scan", and it fires from anywhere near a
        // hostile sector's marker, not just when that sector was the actual target). Resolved a
        // bounded number of times rather than every time it happens: the ship sits still exactly
        // where the fight ended (Arrive's own doc comment - "starts wherever the ship already
        // was"), so the very first few ticks of the resumed trip can still be inside the same
        // capture radius and roll straight into a second one, and a third, indefinitely - each
        // full fight costs real time, so an unbounded retry here turned one slow test into one
        // that never finished. Past a handful of fights this just gives up on dodging them and
        // lets the loop's own timeout run out sitting in whatever battle it's in, same as it
        // always could before this existed.
        var ambushesResolved = 0;
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
        {
            if (world.Phase == VoyagePhase.Battle)
            {
                if (ambushesResolved++ >= 5)
                    break;
                FireBowTurretUntilEnemyDefeated(world, 1);
                for (var j = 0; j < 30 && world.Phase == VoyagePhase.Battle; j++)
                    world.Step(RealtimeStep); // let StepVoyage resolve the kill
                world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: targetPointId)); // resume
                continue;
            }
            world.Step(RealtimeStep);
        }

        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 60 * 30 && !world.CanDockNow; i++)
        {
            var shipField = world.CreateSnapshot().ShipField;
            var toPort = world.DockBerthPosition - new Vec2(shipField.X, shipField.Y); // the berth, not the airlock rectangle
            var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();

            if (speed > 1.5f)
                world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
            else
                world.ApplyCommand(1, SteerToward(world, 1, world.DockBerthPosition));
            world.Step(RealtimeStep);
        }
    }

    // Drifting into the berth must not dock the ship by itself - that's the whole point of the
    // button (World.StationDocking.cs).
    private static bool World_Docking_ProximityAloneDoesNotDock()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);

        if (!world.CanDockNow)
            return false; // never reached the berth - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 0f));
        for (var i = 0; i < 10 * 30; i++) // sit at the berth doing nothing at all
            world.Step(RealtimeStep);
        if (world.Phase != VoyagePhase.StationApproach)
            return false;

        // ...and the button, once pressed, does dock it.
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return world.Phase == VoyagePhase.Station && world.CreateSnapshot().Voyage.DockedPointId == "trade-station";
    }

    private static bool World_Docking_ButtonFarFromPort_DoesNothing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        // Arrival parks the ship a long way off the berth (StationApproachStartDistance).
        if (world.CanDockNow)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return world.Phase == VoyagePhase.StationApproach;
    }

    // Docking squares the ship up and pulls it the last few metres onto the berth, so its own outer
    // airlock ends up exactly on the station's connector rather than merely near it.
    private static bool World_Docking_MatesAirlockOntoStationConnector()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);
        if (!world.CanDockNow)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        if (world.Phase != VoyagePhase.Station)
            return false;

        var shipField = world.CreateSnapshot().ShipField;
        if (Math.Abs(shipField.RotationDegrees) > 0.001f)
            return false;

        // Both frames now differ by exactly Station.WorldOffset, so the two door rectangles land on
        // the same spot - which is what makes the crossing an ordinary doorway.
        var outerDoor = world.Ship.AirlockOuterDoors.First();
        return (outerDoor.Position - world.Station.ShipConnector.Position).Length() < 0.001f;
    }

    // No teleport at the boundary: the character keeps walking in the same coordinate system, one
    // ordinary step at a time, and simply ends up in a station room.
    private static bool World_Station_CrossingConnector_MovesContinuously()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 25f, 3f);

        static Vec2 PositionOf(World w) =>
            w.CreateSnapshot().Characters.Single(c => c.PlayerId == 1) is var c ? new Vec2(c.X, c.Y) : Vec2.Zero;

        var previous = PositionOf(world);
        var crossed = false;
        for (var i = 0; i < 90; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 1f, MoveY: 0f));
            world.Step(RealtimeStep);

            var now = PositionOf(world);
            if ((now - previous).Length() > 0.5f)
                return false; // a jump - exactly what this change removed
            previous = now;
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            {
                crossed = true;
                break;
            }
        }

        // Past the shared door rectangle and inside the station's own dock chamber, in the very
        // same coordinates the ship's interior uses.
        return crossed && world.Station.GetRoom(world.Station.DockRoomId).Contains(previous);
    }

    // Casting off with someone still ashore can't leave them standing in geometry that's no longer
    // attached to the ship.
    private static bool World_Station_Departing_PullsCrewBackAboard()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.OnStation && world.Ship.Rooms.Any(r => r.Contains(new Vec2(me.X, me.Y)));
    }

    // Going ashore means opening the outer airlock, and a docked ship's outer airlock leads into
    // the station's own pressurized chamber - doing the normal thing must not suffocate the crew.
    private static bool World_Station_OpenAirlockWhileDocked_DoesNotVentTheShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        return snapshot.RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen > 99f &&
               snapshot.RoomOxygen.First(o => o.RoomId == "engine").Oxygen > 99f;
    }

    private static bool World_Docking_TooFastAtPort_ButtonStaysDisarmed()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3f, 4f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f));
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        // Barrel straight at the berth at full throttle, mashing the button the whole way: while
        // moving faster than DockMaxSpeed it must never take.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        var sawPortAtSpeed = false;
        for (var i = 0; i < 30 * 30 && world.Phase == VoyagePhase.StationApproach; i++)
        {
            world.Step(RealtimeStep);
            var shipField = world.CreateSnapshot().ShipField;
            // Measured against the berth (where the hull has to sit), not the airlock rectangle -
            // the hull centre is a good half-ship short of the latter when the two are mated.
            var toBerth = world.DockBerthPosition - new Vec2(shipField.X, shipField.Y);
            var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();
            if (toBerth.Length() < 4f && speed >= 2f)
            {
                sawPortAtSpeed = true;
                if (world.CanDockNow)
                    return false; // armed while still barrelling in
                world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
                if (world.Phase == VoyagePhase.Station)
                    return false; // docked despite the speed
            }
        }

        return sawPortAtSpeed;
    }

    // Same button either way (World.StationDocking.cs's HandleDockButtonPressed) - pressing it
    // while already docked casts off instead of trying (and failing) to dock all over again.
    private static bool World_Docking_ButtonUndocksWhenPressedWhileAlreadyDocked()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts docked at the home station

        if (world.Phase != VoyagePhase.Station)
            return false; // setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return world.Phase == VoyagePhase.Traveling && world.CreateSnapshot().Voyage.DockedPointId is null;
    }

    // Casting off through the dock button pulls anyone still ashore back aboard, exactly like
    // casting off by picking a destination does (World_Station_Departing_PullsCrewBackAboard) -
    // both routes share PullCrewOffStation now.
    private static bool World_Docking_UndockButtonPullsCrewBackAboard()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.OnStation && world.Ship.Rooms.Any(r => r.Contains(new Vec2(me.X, me.Y)));
    }

    // The station's bulk is solid - the ship stops against it instead of flying through, and the
    // berth deliberately sits outside that radius so lining up never means shouldering the hull.
    private static bool World_Docking_StationHullBlocksTheShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);
        if (!world.CanDockNow)
            return false;

        // Keep pushing past the berth, straight at the station's centre.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 20 * 30 && world.Phase == VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        var final = world.CreateSnapshot().ShipField;
        var distanceToCentre = (world.Station.Position - new Vec2(final.X, final.Y)).Length();
        return world.Phase == VoyagePhase.StationApproach && distanceToCentre >= 4.5f; // never got inside the hull
    }
}
