using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Flies the ship near the station's berth and leaves it parked there, without docking - the
    // shared setup for the button tests below. Undocks first if needed; there's no separate
    // "approach" state any more (M39) - "parked at the berth" just means close enough and slow
    // enough for CanDockNow to go true, without a DockPressed ever landing.
    private static void ApproachBerth(World world, string targetPointId = "trade-station")
    {
        var wasDocked = world.IsDocked;
        var homeBerth = world.DockBerthPosition; // meaningless once cast off - read it first

        if (wasDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }

        SitAtHelm(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        var target = world.GalaxyMap.GetPoint(targetPointId).Position;

        // Same reasoning as FlyToward's own peel: departing straight toward a target that
        // requires net +X travel points straight back through the home station's own row
        // (Station.Default.cs extends it toward +X from the berth), which the ship is still
        // sitting right next to right after undocking. Backing off perpendicular first, same as a
        // real pilot would, clears it - and on whichever side of the row the target's own Y
        // already sits on, so the straight-line leg below never has to cross back through the
        // row's Y-band to get there (PeelAwayFromBerth's own doc comment).
        if (wasDocked)
            PeelAwayFromBerth(world, homeBerth, target, 1);

        // Deep hostility with whoever owns some *other* nearby sector can still snag the ship into
        // an incidental battle en route to the berth (World.Voyage.cs's TryEngageHostileSector
        // fires from anywhere near a hostile sector's own marker, not just when that sector was
        // the actual target). Resolved a bounded number of times rather than every time it
        // happens: the ship sits still exactly where the fight ended, so the very first few ticks
        // of the resumed trip can still be inside the same capture radius and roll straight into a
        // second one, and a third, indefinitely - each full fight costs real time, so an unbounded
        // retry here turned one slow test into one that never finished. Past a handful of fights
        // this just gives up on dodging them and lets the loop's own timeout run out sitting in
        // whatever battle it's in, same as it always could before this existed.
        //
        // CanDockNow is only trusted once DockBerthPosition actually corresponds to THIS target,
        // not just whenever it happens to be true - right after undocking (or after fleeing an
        // ambush back near its own marker) the ship can still be sitting at some other, nearer
        // station's own berth, where CanDockNow would already read true for that wrong station.
        // DockBerthPosition tracks whichever station is currently nearest and is offset from that
        // station's own marker by its (Center-vs-hull-centre) layout gap - tens of units across
        // every ship/station-kind combination in this game, not the single digits a plain "close
        // to the marker" radius check would assume, so that cheaper check just oscillates forever
        // instead of ever converging. Comparing DockBerthPosition to the marker directly asks the
        // right question - "is the nearest station actually this one" - independent of how far
        // away it still physically is.
        const float BerthTracksIntendedTargetSlack = 40f;
        var ambushesResolved = 0;
        for (var i = 0; i < 120 * 30; i++)
        {
            if (world.IsInBattle)
            {
                if (ambushesResolved++ >= 5)
                    break;
                FireBowTurretUntilEnemyDefeated(world, 1);
                for (var j = 0; j < 30 && world.IsInBattle; j++)
                    world.Step(RealtimeStep); // let StepVoyage resolve the kill
                SitAtHelm(world, 1); // FireBowTurretUntilEnemyDefeated leaves the character standing free
                continue;
            }

            var berthTracksTarget = (world.DockBerthPosition - target).Length() < BerthTracksIntendedTargetSlack;
            if (berthTracksTarget && world.CanDockNow)
                break; // parked at the actual target's berth, slow enough to dock - job done

            var shipField = world.CreateSnapshot().ShipField;
            var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();

            if (speed > 1.5f)
                world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
            else
            {
                var steerTarget = berthTracksTarget ? world.DockBerthPosition : target;
                // Some berths (outpost-gamma among them) sit on a bearing from home that clips
                // straight through a cluster of hostile sectors close enough together that
                // disengaging one can immediately re-enter another - World.Voyage.cs's
                // _recentlyDisengagedSectorId only remembers the single most recent one, so a
                // dumb straight line here can ping-pong between two neighbors and burn through
                // every ambush retry above without ever actually getting clear of the cluster.
                // Routing around them the same way FlyToward already does removes the ping-pong
                // rather than just budgeting more retries for it.
                var shipPos = new Vec2(shipField.X, shipField.Y);
                var avoided = AvoidIncidentalHazards(world, shipPos, steerTarget, null);
                world.ApplyCommand(1, SteerToward(world, 1, avoided));
            }
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
        if (world.IsDocked)
            return false;

        // ...and the button, once pressed, does dock it.
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return world.IsDocked && world.CreateSnapshot().Voyage.DockedPointId == "trade-station";
    }

    // Far from any berth, the button must do nothing at all - there's no separate "approach" state
    // left to sit in (M39): CanDockNow just stays false until the ship is actually close and slow.
    private static bool World_Docking_ButtonFarFromPort_DoesNothing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        SitAtHelm(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f));
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        // Fly clear of the home berth toward open water - nowhere near any station's dock range.
        // +Y, not +X: the home station's own room row extends toward +X from the berth
        // (Station.Default.cs), so a plain +X offset just aims the whole approach straight down
        // that row instead of into open space.
        var awayFromHome = world.GalaxyMap.GetPoint(world.GalaxyMap.HomePointId).Position + new Vec2(0f, 200f);
        for (var i = 0; i < 30 * 30; i++)
        {
            world.ApplyCommand(1, SteerToward(world, 1, awayFromHome));
            world.Step(RealtimeStep);
        }

        if (world.CanDockNow)
            return false; // still near a berth - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return !world.IsDocked;
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
        if (!world.IsDocked)
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

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // cast off
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
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);

        SitAtHelm(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f));
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        // Barrel straight at the berth, mashing the button the whole way: while moving faster than
        // DockMaxSpeed it must never take. SteerToward already floors the throttle once roughly
        // lined up with the target, so aiming it straight at the station the whole way is the same
        // "full ahead" approach the old fixed-heading version used.
        var target = world.GalaxyMap.GetPoint("trade-station").Position;
        var sawPortAtSpeed = false;
        for (var i = 0; i < 60 * 30 && !world.IsDocked; i++)
        {
            world.ApplyCommand(1, SteerToward(world, 1, target));
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
                if (world.IsDocked)
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

        if (!world.IsDocked)
            return false; // setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return !world.IsDocked && world.CreateSnapshot().Voyage.DockedPointId is null;
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
        for (var i = 0; i < 20 * 30 && !world.IsDocked; i++)
            world.Step(RealtimeStep);

        var final = world.CreateSnapshot().ShipField;
        var distanceToCentre = (world.Station.Position - new Vec2(final.X, final.Y)).Length();
        return !world.IsDocked && distanceToCentre >= 4.5f; // never got inside the hull
    }
}
