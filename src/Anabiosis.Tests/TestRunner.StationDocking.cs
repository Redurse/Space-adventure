using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Flies the ship near the station's berth and leaves it parked there, without docking - the
    // shared setup for the button tests below. Undocks first if needed; there's no separate
    // "approach" state any more (M39) - "parked at the berth" just means close enough and slow
    // enough for CanDockNow to go true, without a DockPressed ever landing.
    private static void ApproachBerth(World world, string targetPointId = "trade-station", bool leaveAtHelm = false)
    {
        // Suited up before anything else, the same defensive move every combat-grinding test in
        // TestRunner.FactionsAndShipyard.cs already makes: a genuinely hostile sector sitting near
        // the route (World.Voyage.cs's TryEngageHostileSector, not just this target) can still
        // snag the ship into a real, unresolved-for-a while fight, and a corridor breach that
        // opens during it is otherwise fatal to an unsuited character within the grace period
        // (World.Atmosphere.cs) - a dead pilot can never get seated at the helm again, freezing
        // the ship exactly where the fight left it for the rest of this loop's budget. Skipped if
        // a caller (TestRunner.EvaStation.cs's own ExitShipIntoVacuum) already suited up first -
        // EquipSuit's own Interact press toggles, so calling it again on an already-worn suit
        // would start taking it back off instead of being a harmless no-op.
        if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.Equipped[EquipSlot.Suit] is null)
            EquipSuit(world, 1);

        var wasDocked = world.IsDocked;

        if (wasDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }

        SitAtHelm(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        // M58 follow-up - "перевести стыковку на относительный кадр", part 2: reaching a matched,
        // parked-at-the-berth state via SIMULATED THRUST turned into its own real rendezvous-
        // guidance problem once World.cs's own Tick fix (same milestone) made a hosted station's
        // real Kepler orbital speed - tens of thousands of units/s at this game's KSP-real scale -
        // genuinely apply during a test. Closing a gap against a target that fast, without
        // overshooting it or clipping the station's own solid hull along the way, is a real
        // guidance problem with nothing to do with whatever the test actually wants to check
        // (trade/quest/faction/EVA scaffolding, or the docking BUTTON's own gate, never the flight
        // itself - TestRunner.HelmAndHull.cs/TestRunner.Voyage.cs's own manual-flight tests are the
        // ones that actually fly). World.DebugSetShipVelocity (added alongside this) applies
        // DebugPlaceShip's own established reasoning to velocity instead of position: this is
        // scaffolding, not a piloting test, so the matched state is set directly.
        var target = world.ResolveGalaxyPointPosition(world.GalaxyMap.GetPoint(targetPointId));
        world.DebugPlaceShip(target);
        world.Step(RealtimeStep); // lets UpdateNearestStation/Station.RepositionTo pick up targetPointId as nearest before DockBerthPosition is read below

        // Deep hostility with whoever owns some *other* nearby sector, or the target station
        // itself defending against low standing, can still snag the ship the instant it's placed
        // (World.Voyage.cs's TryEngageHostileSector/UpdateNearestStation). Resolved a bounded
        // number of times rather than every time it happens - each full fight costs real time, and
        // the ship can land right back in capture range of the same hazard after fleeing it.
        var ambushesResolved = 0;
        while (world.IsInBattle && ambushesResolved++ < 5)
        {
            FireBowTurretUntilEnemyDefeated(world, 1);
            for (var j = 0; j < 30 && world.IsInBattle; j++)
                world.Step(RealtimeStep); // let StepVoyage resolve the kill
            SitAtHelm(world, 1); // FireBowTurretUntilEnemyDefeated leaves the character standing free
            world.DebugPlaceShip(target);
            world.Step(RealtimeStep);
        }

        // Two consecutive live samples of the berth's own resolved position - the same plain
        // central-difference idea World.GalaxyPoints.cs's own ResolveGalaxyPointVelocity uses
        // (private to World, not reachable from here) - give the station's real instantaneous
        // velocity without needing that accessor exposed. DockBerthPosition tracks whichever
        // station UpdateNearestStation currently considers nearest, which the placement/battle
        // handling above already made targetPointId.
        var berthSample1 = world.DockBerthPosition;
        world.Step(RealtimeStep);
        var berthSample2 = world.DockBerthPosition;
        var stationVelocity = (berthSample2 - berthSample1) * (1.0 / RealtimeStep);

        // A couple of units inside DockCaptureRadius(4, World.StationDocking.cs) - safely short of
        // DockBerthPosition itself, which is the flush-MATED position (deliberately overlapping the
        // station's own solid hull; TryDockAtStation teleports the ship the rest of the way for
        // exactly that reason, rather than flying it there and triggering a hull collision).
        world.DebugPlaceShip(berthSample2 + new Vec2(2f, 0f));
        world.DebugSetShipVelocity(stationVelocity);

        // Deliberately NOT re-engaging HelmStabilizePressed here (unlike the old flight-based
        // approach this replaced): DebugSetShipVelocity already turns auto-stabilize off on its own
        // (World.ShipField.cs's own doc comment on it) specifically so this matched state survives
        // a caller waiting around doing something else (World_Docking_ProximityAloneDoesNotDock
        // sitting idle, ExitShipIntoVacuum going EVA) - stabilizing here would immediately start
        // decelerating the ship back toward universe-absolute rest, drifting it away from a station
        // that's still really moving at its own full orbital speed.

        // Most callers expect to walk character 1 off to do something else right after this (open
        // the airlock, take a tool, go EVA) - manning a console locks movement the same way
        // manning a turret does (World.Movement.cs), so leaving the pilot seated here would make
        // every one of those walks a silent no-op instead of an error, same trap
        // EnterAsteroidFieldStationary's own doc comment already calls out for the same reason.
        // The rare caller that wants to keep flying immediately after this returns (still issuing
        // HelmThrottle/HelmTurn, which World.cs only applies while IsAtHelm) opts out with
        // leaveAtHelm instead.
        if (!leaveAtHelm && world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm)
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
    }

    // Same turn-to-bearing logic as SteerToward, but throttle is scaled to the fraction of
    // `maxSpeedAddedPerTick` actually needed to close `desiredDeltaV` this tick, instead of a flat
    // 1/0 - see ApproachBerth's own comment on why cruise's 20x thrust multiplier makes plain
    // bang-bang throttle unable to settle inside DockMaxSpeed's narrow window.
    private static ClientCommand SteerTowardProportional(World world, int playerId, Vec2 desiredDeltaV, float maxSpeedAddedPerTick)
    {
        var shipField = world.CreateSnapshot().ShipField;
        var bearingDegrees = MathF.Atan2((float)desiredDeltaV.Y, (float)desiredDeltaV.X) * (180f / MathF.PI) - world.Ship.ForwardDegrees;
        var error = ((bearingDegrees - shipField.RotationDegrees) % 360f + 540f) % 360f - 180f;
        var throttle = MathF.Abs(error) < 25f && maxSpeedAddedPerTick > 0f
            ? MathF.Min(1f, (float)(desiredDeltaV.Length() / maxSpeedAddedPerTick))
            : 0f;
        return new ClientCommand(playerId,
            HelmThrottle: throttle,
            HelmTurn: MathF.Abs(error) < 2f ? 0f : MathF.Sign(error));
    }

    // Flies one fixed leg to a clearance waypoint if the current straight line to `target` clips
    // some other station's row, then stops - a no-op if the line is already clear. Computed and
    // flown ONCE per call, not recomputed every tick like AvoidIncidentalHazards: recomputing a
    // detour every tick interacts badly with ApproachBerth's own speed-capped cruise (right at the
    // clearance boundary the chosen point can flip between the raw line and the detour tick to
    // tick, and SteerToward only floors the throttle once roughly aimed, so the ship ends up
    // mostly turning in place instead of ever actually accelerating). Callers re-run this after
    // anything that relocates the ship (a battle's own disengage nudge, most likely) - the
    // straight line from the new position is a different one that the last check never saw.
    private static void FlyClearOfOtherStations(World world, Vec2 target, string? targetPointId)
    {
        var shipPos = new Vec2(world.CreateSnapshot().ShipField.X, world.CreateSnapshot().ShipField.Y);
        if (OneTimeStationClearWaypoint(world, shipPos, target, targetPointId) is not { } clearWaypoint)
            return;

        for (var k = 0; k < 200 * 30 && !world.IsInBattle &&
             (clearWaypoint - new Vec2(world.CreateSnapshot().ShipField.X, world.CreateSnapshot().ShipField.Y)).Length() > 15f; k++)
        {
            var sf = world.CreateSnapshot().ShipField;
            var spd = new Vec2(sf.VelocityX, sf.VelocityY).Length();
            if (spd > 1.5f)
                world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
            else
                world.ApplyCommand(1, SteerToward(world, 1, clearWaypoint));
            world.Step(RealtimeStep);
        }
    }

    // If the straight line from `from` to `target` would pass within `clearance` of some OTHER
    // station's own marker (their row structure - Station.Default.cs - extends from there, just
    // like home's own), returns a single waypoint that clears it with the smallest possible
    // sideways detour; otherwise null. Deliberately a one-shot calculation, not recomputed every
    // tick - see the caller's own comment on why that matters here.
    private static Vec2? OneTimeStationClearWaypoint(World world, Vec2 from, Vec2 target, string? targetPointId, float clearance = 20f)
    {
        var toTarget = target - from;
        var length = toTarget.Length();
        if (length < 1f)
            return null;
        var dir = toTarget * (1f / length);

        foreach (var station in world.GalaxyMap.GetSystem(world.CreateSnapshot().CurrentSystemId).Points
                     .Where(p => p.Kind == GalaxyPointKind.Station && p.Id != targetPointId))
        {
            var stationPosition = world.ResolveGalaxyPointPosition(station);
            var toStation = stationPosition - from;
            var projected = toStation.X * dir.X + toStation.Y * dir.Y;
            if (projected < 0f || projected > length)
                continue; // not actually between here and the target

            var closestPoint = from + dir * projected;
            var offset = stationPosition - closestPoint;
            if (offset.Length() >= clearance)
                continue;

            var perpendicular = new Vec2(-dir.Y, dir.X);
            var side = offset.X * perpendicular.X + offset.Y * perpendicular.Y >= 0f ? -1f : 1f;
            return closestPoint + perpendicular * (side * clearance);
        }
        return null;
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
        // "Sit at the berth doing nothing at all" for 10 whole seconds is longer than
        // TryFlyShipOnRails's own analytic orbit (seeded once, from a necessarily-approximate
        // finite-difference velocity match) reliably tracks the station's own live, independently-
        // computed position - close enough in to trip a real hull collision (World.ShipField.cs's
        // own StepShipFieldPhysics) well before 10s of drift accumulates, at the DockCaptureRadius(4)
        // range this scenario sits in. Re-snapped to the live offset every tick instead of trusting
        // that longer, unattended coast - this is what "just sitting there, not touching anything"
        // actually needs to mean for this test, not a claim about on-rails fidelity over 10s at
        // point-blank range (a separate, real concern this doesn't try to fix).
        var berthOffset = new Vec2(world.CreateSnapshot().ShipField.X, world.CreateSnapshot().ShipField.Y) - world.DockBerthPosition;
        Vec2? previousBerthForIdle = null;
        for (var i = 0; i < 10 * 30; i++)
        {
            var currentBerth = world.DockBerthPosition;
            world.DebugPlaceShip(currentBerth + berthOffset); // DebugPlaceShip itself zeroes velocity, so re-set it AFTER this, not before
            if (previousBerthForIdle is { } prevBerth)
                world.DebugSetShipVelocity((currentBerth - prevBerth) * (1.0 / RealtimeStep));
            previousBerthForIdle = currentBerth;
            world.Step(RealtimeStep);
        }
        if (world.IsDocked)
            return false;

        // The loop's own last Step (needed so IsDocked/decompression/etc. actually run across the
        // full 10s, not skipped) leaves the ship exactly where that tick's real velocity carried it
        // from the last placement - up to ~1 unit's worth of DockCaptureRadius at ordinary speeds,
        // but a fast-orbiting station's own ~50,000 units/s covers thousands of units in a single
        // 1/30s tick. One final re-snap is what makes the CanDockNow check right below (and the
        // press after it) test "still sitting at the berth", not "wherever one uncorrected tick of
        // drift happened to leave it".
        var liveBerth = world.DockBerthPosition;
        world.DebugPlaceShip(liveBerth + berthOffset);
        world.DebugSetShipVelocity(previousBerthForIdle is { } lastBerth ? (liveBerth - lastBerth) * (1.0 / RealtimeStep) : Vec2.Zero);

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
        var awayFromHome = world.ResolveGalaxyPointPosition(world.GalaxyMap.GetPoint(world.GalaxyMap.HomePointId)) + new Vec2(0f, 200f);
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

        // M58 follow-up - "перевести стыковку на относительный кадр": physically flying this,
        // "barrel straight at the berth, mashing the button the whole way", stopped being
        // reproducible once World.cs's own Tick fix (same milestone) made a hosted station's real
        // Kepler orbital speed (tens of thousands of units/s) genuinely apply - home-station to
        // trade-station is a real interplanetary hop at this game's KSP-scale, and matching a
        // target moving that fast with bang-bang RCS steering either overshoots and diverges
        // (uncapped) or, capped for stability, never actually catches up (a pure "close the current
        // gap" term alone can't out-run a target already receding at the same speed the cap allows).
        // ApproachBerth hit the identical wall and settled on setting the matched state directly
        // (DebugSetShipVelocity, World.ShipField.cs) rather than simulating the chase - this test
        // only needs "close to the berth AND moving too fast relative to it", so it constructs that
        // directly the same way: place at the berth's own live position, and give it that same live
        // velocity plus an ordinary deliberate extra kick, well over DockMaxSpeed(2) but nowhere
        // near what would look like a flight-model bug.
        var target = world.ResolveGalaxyPointPosition(world.GalaxyMap.GetPoint("trade-station"));
        world.DebugPlaceShip(target);
        world.Step(RealtimeStep); // lets UpdateNearestStation/Station.RepositionTo pick up trade-station as nearest before DockBerthPosition is read below

        var berthSample1 = world.DockBerthPosition;
        world.Step(RealtimeStep);
        var berthSample2 = world.DockBerthPosition;
        var berthVelocity = (berthSample2 - berthSample1) * (1.0 / RealtimeStep);

        world.DebugPlaceShip(berthSample2 + new Vec2(2f, 0f)); // inside DockCaptureRadius(4)
        world.DebugSetShipVelocity(berthVelocity + new Vec2(50f, 0f)); // matched to the berth, plus a deliberate 50 units/s over DockMaxSpeed(2)

        var shipField = world.CreateSnapshot().ShipField;
        var toBerth = world.DockBerthPosition - new Vec2(shipField.X, shipField.Y);
        var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();
        if (toBerth.Length() >= 4f || speed < 2f)
            return false; // setup problem - didn't actually land in the "close and too fast" state this is testing

        if (world.CanDockNow)
            return false; // armed while still barrelling in
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return !world.IsDocked; // must NOT have docked despite the speed
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

    // Regression guard (humble-soaring-cat.md, "стены не имеют коллизии") - a fresh campaign starts
    // docked (World.cs's own constructor comment), so this is the movement path an ordinary new game
    // actually exercises first. World.Movement.cs used to send docked movement through the old,
    // pre-M73 RoomLayout system, whose walls are zero-thickness (clamped to the room's own rectangle
    // edge) - a full tile short of where M75's renderer actually draws the wall's own plating
    // (TileGridRasterizer's room-edge tile). Walking into the corridor's own Top wall (row 0, always
    // solid) while docked must now stop at the SAME place Ship.MoveAlongAxis/TileMovement would once
    // undocked - one tile in from the hull, not right at its outer face.
    private static bool World_MoveAlongAxis_WhileDocked_BlocksAtTileWallNotOldZeroWidthEdge()
    {
        var world = new World();
        world.SpawnCharacter(1);
        if (!world.IsDocked)
            return false; // setup problem - a fresh campaign should always start docked

        for (var i = 0; i < 300; i++) // 10 simulated seconds - far more than enough to converge
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: -1));
            world.Step(RealtimeStep);
        }

        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        // Row 0 (the corridor's own leading Top edge) is a full tile of solid wall - clearance stops
        // at y=1+CharacterRadius, not at the old y=CharacterRadius the zero-thickness model gave.
        return Math.Abs(after.X - 11.5) < 0.01 && Math.Abs(after.Y - (1.0 + RoomLayout.CharacterRadius)) < 0.01;
    }
}
