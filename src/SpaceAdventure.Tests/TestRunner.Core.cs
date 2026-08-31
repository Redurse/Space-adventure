using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Заглушка на время каркаса: реальные тесты появятся вместе с логикой в Shared/Server.
    private static bool Smoke_ProjectsWireUpCorrectly() => true;

    private static bool InProcessTransport_DeliversCommandToServer()
    {
        var transport = new InProcessTransport();
        IClientConnection clientSide = transport;
        IServerConnection serverSide = transport;

        var command = new ClientCommand(PlayerId: 1);
        clientSide.Send(command);

        var received = serverSide.ReceiveCommands();
        return received.Count == 1 && received[0] == command;
    }

    private static bool GameServer_TickIncrementsAndBroadcastsSnapshot()
    {
        var server = new GameServer();
        var transport = new InProcessTransport();
        server.Connect(transport);

        server.Tick();
        server.Tick();

        IClientConnection clientSide = transport;
        var latest = clientSide.ReceiveLatestSnapshot();
        return latest is not null && latest.Tick == 2;
    }

    // Real usage (GameServer.Tick) steps in small ~1/30s increments — door crossings only work
    // when the per-step distance stays within a door's depth, so tests replicate that cadence
    // rather than one huge Step() jump.
    private const double RealtimeStep = 1.0 / 30;

    private static bool World_Step_MovesCharacterTowardInput()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var start = world.Ship.SpawnPoint;

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep); // ~1 second at full speed, crosses into the next room via its door

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > start.X + 1f && Math.Abs(character.Y - start.Y) < 0.01f;
    }

    private static bool World_Step_ClampsToShipBounds()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 300; i++)
            world.Step(RealtimeStep); // far more than enough to walk through every door into the hull wall

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var maxX = world.Ship.Rooms.Max(r => r.Right);
        // Stops RoomLayout.CharacterRadius short of the bare wall, not exactly on it - a wall has
        // real thickness now, so the character's own edge is what touches it, not its center point.
        return Math.Abs(character.X - (maxX - RoomLayout.CharacterRadius)) < 0.01f;
    }

    private static bool GameServer_Tick_AppliesMoveCommandFromClient()
    {
        var spawn = Ship.CreateStarter().SpawnPoint;

        var server = new GameServer();
        var transport = new InProcessTransport();
        var playerId = server.Connect(transport);

        IClientConnection clientSide = transport;
        clientSide.Send(new ClientCommand(playerId, MoveX: 1, MoveY: 0));

        server.Tick();

        var snapshot = clientSide.ReceiveLatestSnapshot();
        var character = snapshot?.Characters.SingleOrDefault(c => c.PlayerId == playerId);
        return character is not null && character.X > spawn.X;
    }

    private static bool Ship_MoveAlongAxis_BlocksAtWallWithoutDoor()
    {
        var ship = Ship.CreateStarter();
        // Start deep enough in the room that the start position itself isn't inside the top wall's
        // own tile (cockpit's Top row is now a real, solid tile - see TileGridRasterizer.FromRooms).
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(2.5f, 2f), "cockpit", new Vec2(0, -1f), _ => true);
        // Clamped CharacterRadius short of the top hull wall's face, one tile deeper into the room
        // than the old zero-width-wall model (the wall now consumes the room's own leading/Top row).
        return roomId == "cockpit" && Math.Abs(pos.Y - (1f + RoomLayout.CharacterRadius)) < 0.01f;
    }

    private static bool Ship_MoveAlongAxis_PassesThroughAlignedDoor()
    {
        var ship = Ship.CreateStarter();
        // Near the cockpit/reactor wall (x=5) at the door's y=3 — should cross through.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.9f, 3f), "cockpit", new Vec2(0.3f, 0), _ => true);
        return roomId == "reactor" && Math.Abs(pos.X - 5.2f) < 0.01f;
    }

    private static bool Ship_MoveAlongAxis_BlockedWhenMisalignedWithDoor()
    {
        var ship = Ship.CreateStarter();
        // Same wall, but y=1.5 is outside the door's opening (tile rows 2-3) — should hit the wall,
        // stopping CharacterRadius short of it rather than exactly on it (see RoomLayout.cs). y=1.5
        // instead of the old y=0.5 so the start position itself isn't sitting inside the cockpit's
        // own top wall tile (row 0, always walled - see TileGridRasterizer.FromRooms).
        // Also start further back on X (4.5, not the old 4.9): at y=1.5 (a non-door row) column 5
        // is solid, and a start of x=4.9 already has its own clearance box (±0.35) touching that
        // solid column - TileMovement.MoveAlongAxis treats an already-non-clear start as a special
        // case and returns the raw, uncollided position instead of running the blocking logic (see
        // that file's own doc comment), which would make this test pass or fail for the wrong
        // reason. x=4.5 is comfortably clear of column 5, and the 0.3 delta still carries it far
        // enough to hit the wall. This boundary itself is unchanged from the old model since the
        // wall tile at x=5 belongs to the NEIGHBORING reactor room's leading edge, not cockpit's
        // own trailing edge.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.5f, 1.5f), "cockpit", new Vec2(0.3f, 0), _ => true);
        return roomId == "cockpit" && Math.Abs(pos.X - (5f - RoomLayout.CharacterRadius)) < 0.01f;
    }

    // Regression guard: the obstacle used to apply unconditionally to EVERY ship's reactor room,
    // hand-authored hulls included - on the Frigate's own 5x6 "reactor" room that swallowed the
    // room's only door, stranding any crew pathing through it (several unrelated tests hung on
    // exactly this before RoomCatalog.NamesWithReferenceArt gated the obstacle to rooms that
    // actually have the reference art it's meant to match).
    private static bool Ship_MoveAlongAxis_HandAuthoredReactorRoomHasNoObstacle()
    {
        var ship = Ship.CreateStarter();
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(9.5f, 1f), "reactor", new Vec2(0f, 1f), _ => true);
        return roomId == "reactor" && Math.Abs(pos.Y - 2f) < 0.01f; // moved freely, no obstacle
    }

    private static Ship BuildCatalogReactorTestShip()
    {
        var rooms = new[]
        {
            new CustomRoomDef("reactor-room", "Реакторный отсек", 0f, 0f, 9f, 9f),
            new CustomRoomDef("utility-room", "Служебный отсек", 9f, 0f, 12f, 9f),
        };
        var doors = new[] { new CustomDoorDef("reactor-room", "utility-room") };
        var airlocks = new[] { new CustomAirlockDef("utility-room", EdgeSide.Right) };
        var devices = new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 4.5f, 4.5f),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 12f, 2f),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 16f, 2f),
            new CustomDeviceDef(CustomDeviceKind.Helm, 12f, 5f),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 14f, 5f),
            new CustomDeviceDef(CustomDeviceKind.Engine, 16f, 5f),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 12f, 8f),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 16f, 8f),
        };
        var def = new CustomShipDefinition("Тест", rooms, doors, airlocks, devices, ForwardDegrees: 0f);
        return Ship.FromCustomDefinition(def);
    }

    // The other side of the same guard: a room whose NAME does match real reference art
    // (RoomCatalog.NamesWithReferenceArt) still gets the obstacle, sized to 80% of that room.
    private static bool Ship_MoveAlongAxis_CatalogReactorRoomBlocksObstacle()
    {
        var ship = BuildCatalogReactorTestShip();
        // reactor-room is 9x9 at (0,0), Reactor device centred at (4.5,4.5) - obstacle half-extent
        // (room.Width*0.3, room.Height*0.3) = (2.7,2.7) plus clearance covers roughly [1.45,7.55]
        // on both axes. Start y=1.4 sits in the narrow band that's both clear of the room's own top
        // wall tile (row 0, walled unconditionally - needs y >= 1.35) and clear of the obstacle
        // (needs y < 1.45) - the old y=1 start would now land inside that top wall tile instead.
        // TileMovement.MoveAlongAxis slides right up to whichever boundary is nearer (wall or
        // obstacle, by design - see that file's own doc comment) rather than refusing outright the
        // way the old RoomLayout model did, so the expected stop is the obstacle's own clearance
        // edge (1.45), not the unchanged start position.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.5f, 1.4f), "reactor-room", new Vec2(0f, 1f), _ => true);
        return roomId == "reactor-room" && Math.Abs(pos.Y - 1.45f) < 0.01f;
    }

    private static bool Reactor_Step_DepletesFuelProportionalToUsage()
    {
        var reactor = new Reactor(maxOutput: 10f, maxFuel: 10f, fuelPerPowerUnitPerSecond: 1f);
        reactor.Step(1.0, totalAllocatedPower: 5f); // 5 power * 1 fuel/power/sec * 1s
        return Math.Abs(reactor.Fuel - 5f) < 0.01f;
    }

    private static bool Reactor_CurrentOutput_DropsToZeroWhenFuelDepleted()
    {
        var reactor = new Reactor(maxOutput: 10f, maxFuel: 2f, fuelPerPowerUnitPerSecond: 1f);
        reactor.Step(1.0, totalAllocatedPower: 10f); // would need 10 fuel, only 2 available
        return reactor.Fuel == 0f && reactor.CurrentOutput == 0f;
    }

    // A campaign begins with the reactor split evenly across the systems rather than with every
    // slider at zero. Worth a test rather than a glance, for two reasons: the share is derived from
    // two numbers that live apart from each other - reactor output and the number of PowerSystemId
    // values - so adding a sixth system quietly changes what every ship starts on; and the split has
    // to happen on starting a run, not on constructing the grid, which is a distinction no reader
    // would guess from the value alone.
    private static bool World_NewCampaign_StartsWithAnEqualShareForEverySystem()
    {
        var world = new World(ShipKind.Frigate);
        world.StartCampaign();

        var systems = Enum.GetValues<PowerSystemId>();
        var state = world.PowerGrid.CreateState();
        var expected = state.ReactorOutput / systems.Length;
        return systems.All(s => Math.Abs(world.PowerGrid.GetAllocation(s) - expected) < 0.01f)
            && Math.Abs(state.Allocated.Values.Sum() - state.ReactorOutput) < 0.05f;
    }

    // The tutorial teaches allocating power, so it has to start from nothing - otherwise its own
    // completion check sees a boot-time split and ticks the step off before the player moves. This
    // is the guard against anyone moving the split back into the PowerGrid constructor, where it
    // would reach the tutorial too.
    private static bool World_TutorialWorld_StartsWithNothingAllocated()
    {
        var world = new World(ShipKind.Frigate);
        world.StartTutorial();
        return Enum.GetValues<PowerSystemId>().All(s => world.PowerGrid.GetAllocation(s) < 0.01f);
    }

    private static bool PowerGrid_Allocation_CannotExceedReactorOutput()
    {
        var grid = new PowerGrid();
        grid.ApplyInput(playerId: 1, systemIndex: 0, direction: 1f);
        for (var i = 0; i < 5; i++)
            grid.Step(1.0); // enough seconds at the adjust rate to try to overshoot the cap

        var state = grid.CreateState();
        var total = state.Allocated.Values.Sum();
        return total <= state.ReactorOutput + 0.01f && total > 0f;
    }

    // Regression: two players adjusting different sliders in the same tick used to share one
    // "last call wins" adjust slot (World.cs's ApplyCommand called PowerGrid.ApplyInput once per
    // player, each overwriting the other) - in practice only whichever player happened to be
    // processed last that tick ever actually moved a slider. Each player's own input now applies
    // independently, on its own player-keyed slot.
    private static bool PowerGrid_TwoPlayers_CanAdjustDifferentSlidersSimultaneously()
    {
        var grid = new PowerGrid();
        grid.ApplyInput(playerId: 1, systemIndex: (int)PowerSystemId.Oxygen, direction: 1f);
        grid.ApplyInput(playerId: 2, systemIndex: (int)PowerSystemId.Shields, direction: 1f);
        for (var i = 0; i < 5; i++)
            grid.Step(1.0);

        var state = grid.CreateState();
        return state.Allocated[PowerSystemId.Oxygen] > 0f && state.Allocated[PowerSystemId.Shields] > 0f;
    }

    private static bool PowerGrid_Battery_ChargesFromSurplus()
    {
        var grid = new PowerGrid();
        // No allocation adjustment at all -> the whole reactor output is surplus.
        for (var i = 0; i < 10; i++)
            grid.Step(1.0);

        var state = grid.CreateState();
        return state.BatteryCharge > 0f;
    }

    // Regression coverage for the battery block feature: once the battery is charged, a reactor
    // output shortfall should draw from it before allocations get rescaled down, so a system that
    // was already running at some level doesn't instantly dip the moment fuel runs out.
    private static bool PowerGrid_Battery_DischargesToCoverReactorShortfall()
    {
        var grid = new PowerGrid();
        grid.ApplyInput(playerId: 1, systemIndex: (int)PowerSystemId.Shields, direction: 1f);
        for (var i = 0; i < 5; i++)
            grid.Step(1.0); // build up an allocation, charging the battery from the surplus

        grid.ApplyInput(playerId: 1, systemIndex: (int)PowerSystemId.Shields, direction: 0f); // let go of the slider

        var beforeCharge = grid.CreateState().BatteryCharge;
        var allocatedBefore = grid.CreateState().Allocated.Values.Sum();
        if (beforeCharge <= 0f || allocatedBefore <= 0f)
            return false;

        // Starve the reactor's fuel directly (bypassing PowerGrid.Step) so its output collapses
        // to zero without touching the still-held allocation.
        for (var i = 0; i < 2000 && grid.Reactor.Fuel > 0; i++)
            grid.Reactor.Step(1.0, totalAllocatedPower: grid.Reactor.MaxOutput);

        grid.Step(1.0); // one grid tick with the reactor already dark

        var state = grid.CreateState();
        return state.BatteryCharge < beforeCharge && state.Allocated.Values.Sum() > 0.01f;
    }

    // Bang-bang controller: drives the character toward a target via small realtime steps
    // (same cadence GameServer.Tick uses), so it can also cross doors along the way.
    private static void MoveCharacterTo(World world, int playerId, float targetX, float targetY)
    {
        for (var i = 0; i < 400; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId);
            var dx = targetX - me.X;
            var dy = targetY - me.Y;
            if (Math.Abs(dx) < 0.05f && Math.Abs(dy) < 0.05f)
                return;

            world.ApplyCommand(playerId, new ClientCommand(playerId, MoveX: Math.Sign(dx), MoveY: Math.Sign(dy)));
            world.Step(RealtimeStep);
        }
    }

    private static bool World_ToggleManning_RequiresProximityToPeriscope()
    {
        var world = new World();
        world.SpawnCharacter(1); // spawns in the corridor, far from the cockpit periscope

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return !world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);
    }

    private static bool World_ToggleManning_SucceedsNearPeriscope()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, targetX: 1.5f, targetY: 3f);

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);
    }

    private static bool World_TurretAim_ClampsToDefinitionLimits()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        world.ApplyCommand(1, new ClientCommand(1, TurretAimDirection: 1f));
        for (var i = 0; i < 60; i++) // 2s — far more than enough to hit the 45-degree limit
            world.Step(RealtimeStep);

        var state = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-bow");
        return Math.Abs(state.AimDegrees - 45f) < 0.5f;
    }

    // Walks the character to the helm console (the same two-leg route every test in this project
    // already used by hand: onto the shared y=3 spine first, then up into the console itself, so a
    // diagonal move can never clip a corner) and mans it, unless already there. Reads the console's
    // own live position rather than a hardcoded (3,4) - M47's helm redesign moved it to the
    // cockpit's forward bulkhead, and a stale coordinate here just walks past it and never sits
    // down (the same drift bug TestRunner.Scanner.cs's own MoveToNavigationConsole hit first).
    private static void SitAtHelm(World world, int playerId = 1)
    {
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId).IsAtHelm)
        {
            var console = world.Ship.HelmConsole.Position;
            MoveCharacterTo(world, playerId, 3f, 3f); // corridor -> reactor -> cockpit, at the doors' shared height
            MoveCharacterTo(world, playerId, (float)console.X, (float)console.Y);
            world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true));
        }

        // Every test helper that flies the ship by hand (SteerToward and everything built on it)
        // was written against RCS's free rotation - it can turn in place at any speed. Arc (M41's
        // default) can't turn at all from a dead stop, which breaks the whole "aim, then thrust"
        // pattern those helpers depend on. Switching to RCS here, once, covers every caller
        // uniformly - the same choice a real pilot would make for precision work (docking, lining
        // up on a target) rather than something special-cased just for tests.
        if (world.CreateSnapshot().ShipField.ControlMode == ShipControlMode.Arc)
            world.ApplyCommand(playerId, new ClientCommand(playerId, ToggleControlModePressed: true));
    }

    // True once the ship's current field position is within `radius` of `point`.
    private static bool NearPosition(World world, Vec2 point, float radius)
    {
        var shipField = world.CreateSnapshot().ShipField;
        return (point - new Vec2(shipField.X, shipField.Y)).Length() < radius;
    }

    // Flies to within a stone's throw of a world-space point and brakes to a dead stop there - the
    // manual-flight replacement for the old autopilot's own guaranteed-stationary arrival, which
    // several tests below rely on (starting at rest, to measure a single control input in
    // isolation) and which real asteroid/EVA-target positions are calibrated relative to (they sit
    // near the field's own asteroid-dense marker, not near wherever the ship happens to undock).
    private static void FlyNearAndStop(World world, Vec2 target, int playerId = 1)
    {
        FlyToward(world, target, () => NearPosition(world, target, 10f), playerId);
        world.ApplyCommand(playerId, new ClientCommand(playerId, HelmStabilizePressed: true));
        // How long this actually takes now depends on how fast the ship got going on the way here
        // (World.Gravity.cs's dynamic speed cap, M50, can sit far above the old flat ArcMaxSpeed) -
        // and EXACT zero is no longer reachable even in principle anywhere in the field: real
        // gravity (M50) acts unconditionally, so auto-stabilize's own decel and that tick's tiny
        // gravity nudge settle into a small nonzero steady state rather than ever cancelling to
        // the bit-for-bit 0f the old, gravity-free physics could actually reach. A tight tolerance
        // instead - negligible next to any real thrust, easily reached at a spot placed far from
        // every body (AsteroidField.ClusterCenter) - and callers compare their own "before" reading
        // against this same tolerance rather than exact equality now.
        for (var i = 0; i < 200 * 30; i++)
        {
            var field = world.CreateSnapshot().ShipField;
            if (MathF.Abs(field.VelocityX) < 0.01f && MathF.Abs(field.VelocityY) < 0.01f)
                break;
            world.Step(RealtimeStep);
        }

        // The old autopilot's "guaranteed-stationary arrival" was rotation-locked at 0 too, not
        // just velocity-zeroed - several EVA/hull tests calibrated against this helper assume the
        // ship's local frame lines up with world axes afterwards. Manual flight leaves the ship
        // pointed wherever it was last steered, so square it back up explicitly.
        for (var i = 0; i < 10 * 30 && MathF.Abs(NormalizeDegrees(world.CreateSnapshot().ShipField.RotationDegrees)) > 0.5f; i++)
        {
            var error = -NormalizeDegrees(world.CreateSnapshot().ShipField.RotationDegrees);
            world.ApplyCommand(playerId, new ClientCommand(playerId, HelmTurn: MathF.Sign(error)));
            world.Step(RealtimeStep);
        }
        world.ApplyCommand(playerId, new ClientCommand(playerId, HelmTurn: 0f));
        world.Step(RealtimeStep);
    }

    private static float NormalizeDegrees(float degrees) => ((degrees % 360f) + 540f) % 360f - 180f;

    // Undocks (if needed), ramps the Engine and mans the helm, then steers straight at a
    // world-space point until `until` is satisfied or the tick budget runs out - the manual-flight
    // replacement for every "TravelToPointId/TravelToX,Y then wait for arrival" pattern the old
    // server-side autopilot used to cover (M39 removed it entirely - see World.Voyage.cs).
    // targetPointId, when the target IS a hostile sector's own marker (EnterBattle's case),
    // excludes it from AvoidIncidentalHazards below - the whole point there is to actually reach it.
    private static void FlyToward(World world, Vec2 target, Func<bool> until, int playerId = 1, int maxTicks = 120 * 30, string? targetPointId = null)
    {
        var wasDocked = world.IsDocked;
        var berth = world.DockBerthPosition; // read before undocking - it's meaningless once cast off
        if (wasDocked)
        {
            world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(playerId, new ClientCommand(playerId, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        SitAtHelm(world, playerId);

        // A real pilot backs off the berth before setting a course, rather than pointing straight
        // at wherever they're ultimately headed - the station's own structure is solid now
        // (World.ShipField.cs), and it sits right where the ship was just mated to it, so a
        // beeline toward an arbitrary target can point straight back through it. SteerToward has
        // no obstacle-avoidance of its own (it's a straight-line dumb pilot), so this peels the
        // ship a short, safe distance clear of the berth first - same shape as backing a real ship
        // out before turning onto a heading.
        if (wasDocked)
            PeelAwayFromBerth(world, berth, target, playerId);

        // AvoidIncidentalHazards below steers clear of hostile sectors and asteroids - a station's
        // own row (Station.Default.cs) sitting on the straight line to `target` is a different,
        // solid obstacle it never accounts for, and the collision it causes has no ambush/retry
        // mechanism to ever dislodge the ship from (TestRunner.StationDocking.cs's own doc comment
        // on this). One fixed leg to a clearance waypoint first, the same fix ApproachBerth already
        // needed.
        FlyClearOfOtherStations(world, target, targetPointId);

        for (var i = 0; i < maxTicks && !until(); i++)
        {
            var shipField = world.CreateSnapshot().ShipField;
            var shipPos = new Vec2(shipField.X, shipField.Y);
            var steerTarget = AvoidIncidentalHazards(world, shipPos, target, targetPointId);
            var command = SteerToward(world, playerId, steerTarget);
            world.ApplyCommand(playerId, command);
            world.Step(RealtimeStep);
        }
    }

    // How far clear of a hostile sector's own CaptureRadius(8) a course cutting across the system
    // has to stay - SteerToward has no obstacle-avoidance of its own, so a straight line toward
    // some other target can otherwise clip a sector it was never actually headed for, starting a
    // fight that has nothing to do with whatever the test is checking.
    private const float HazardClearance = 20f;
    // Ships aren't points - clear an asteroid's own radius plus this margin, not just its bare
    // centre (found the hard way: a docking-position bugfix elsewhere shifted every flight's own
    // starting point by a couple hundred units, which was enough for a straight-line course that
    // used to miss a rock to graze it instead - this dumb test autopilot had no recovery from that
    // at all, unlike a real player who'd just steer around it).
    private const float AsteroidClearanceMargin = 15f;

    // If the straight line from `from` to `target` would pass within clearance of some hazard,
    // returns a waypoint that clears it with the smallest possible sideways detour instead; null if
    // the line is already clear of it. Shared by both hazard kinds below - only what counts as a
    // hazard and its own clearance radius differ between them.
    private static Vec2? DetourAround(Vec2 hazardPosition, float clearance, Vec2 from, Vec2 dir, double length)
    {
        var toHazard = hazardPosition - from;
        var projected = toHazard.X * dir.X + toHazard.Y * dir.Y;
        if (projected < 0f || projected > length)
            return null; // not actually between here and the target

        var closestPoint = from + dir * projected;
        var offset = hazardPosition - closestPoint;
        if (offset.Length() >= clearance)
            return null;

        var perpendicular = new Vec2(-dir.Y, dir.X);
        var side = offset.X * perpendicular.X + offset.Y * perpendicular.Y >= 0f ? -1f : 1f;
        return closestPoint + perpendicular * (side * clearance);
    }

    // Hostile sectors (marked points) and asteroids (solid rocks with a real radius) both get
    // steered around here - recomputed fresh every tick (FlyToward's own loop) off the ship's
    // actual current position, so the course keeps curving smoothly around whichever hazard is
    // closest rather than committing to one fixed detour point regardless of how the approach
    // angle changes.
    private static Vec2 AvoidIncidentalHazards(World world, Vec2 from, Vec2 target, string? targetPointId)
    {
        var toTarget = target - from;
        var length = toTarget.Length();
        if (length < 1f)
            return target;
        var dir = toTarget * (1f / length);

        foreach (var hazard in world.GalaxyMap.GetSystem(world.CreateSnapshot().CurrentSystemId).Points
                     .Where(p => p.Kind == GalaxyPointKind.HostileSector && p.Id != targetPointId))
        {
            if (DetourAround(hazard.Position, HazardClearance, from, dir, length) is { } detour)
                return detour;
        }

        // Same straight-line blind spot as the hostile-sector check above, just for solid rocks -
        // World.EnemyFleet.cs's own HasLineOfSight already treats these as real obstacles for
        // gunfire, so a dumb test pilot flying straight through one is exactly as wrong.
        foreach (var asteroid in world.AsteroidField.Asteroids)
        {
            if (DetourAround(asteroid.Position, asteroid.Radius + AsteroidClearanceMargin, from, dir, length) is { } detour)
                return detour;
        }

        return target;
    }

    // Perpendicular to the berth row (±Y), not along it (±X): every station's own room row
    // (Station.Default.cs) is a thin strip - only RoomHeight(6) tall, but running however many
    // modules wide in +X from the connector - so stepping sideways off the row clears the whole
    // structure in a short, fixed distance regardless of how far it happens to extend lengthwise.
    // Backing straight out along -X (the direction a real approach starts from) looks tempting,
    // but only actually helps when wherever the ship is headed next also happens to lie in -X -
    // for most destinations in this game (they sit roughly east of home) that just walks the ship
    // straight back into the same row it was trying to leave, since -X is a dead end for anywhere
    // else. The Y side is picked to match: peeling toward the SAME side of the row the target
    // already sits on means the subsequent straight-line course, wherever it goes from here,
    // never has to cross back through the row's own Y-band to get there - peeling to the opposite
    // side just relocates that same crossing to later in the trip instead of avoiding it. Whatever
    // OTHER hazard this sideways step happens to put in the way of the subsequent course is
    // FlyToward's own problem, not this one's - AvoidIncidentalHazards handles that generally, for
    // any leg of the trip, not just this first one. Assumes the ship is already undocked and
    // sitting at `berth`.
    private static void PeelAwayFromBerth(World world, Vec2 berth, Vec2 target, int playerId = 1)
    {
        var side = target.Y >= berth.Y ? 1f : -1f;
        var awayTarget = berth + new Vec2(0f, side * 40f);
        for (var i = 0; i < 15 * 30 && !NearPosition(world, awayTarget, 15f); i++)
        {
            world.ApplyCommand(playerId, SteerToward(world, playerId, awayTarget));
            world.Step(RealtimeStep);
        }
    }

    // Shells travel now (World.Projectiles.cs), so there has to be something out there to hit and
    // the shot needs time to reach it - "fire and read the HP next tick" isn't a thing any more.
    // Places the ship right on the named hostile sector's own marker so the proximity scan starts
    // the fight immediately (World.Voyage.cs's TryEngageHostileSector) - almost every caller is
    // using "a fight has started" purely as scaffolding for a combat/boarding/faction mechanic, not
    // testing the approach itself, so this doesn't fly there for real (World.DebugPlaceShip -
    // test-only, see its own doc comment; a system now scattered with several such sectors and
    // multiple stations' own solid hulls needs actual obstacle-avoidance to reach reliably by a
    // straight-line pilot, which is a real feature in its own right, not scaffolding). Stands the
    // pilot back up (old autopilot arrival never needed a human at the helm) - every caller expects
    // to find the character standing free right after this, free to walk off to a turret, the ammo
    // rack, or wherever the actual test needs it next.
    private static void EnterBattle(World world, int playerId = 1, string sectorId = "sector-alpha")
    {
        if (world.IsDocked)
        {
            world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
            world.Step(RealtimeStep);
        }

        world.DebugPlaceShip(world.GalaxyMap.GetPoint(sectorId).Position);
        world.Step(RealtimeStep);

        if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId).IsAtHelm)
            world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true));

        // A modest baseline of Engine power (M50) - without it, auto-stabilize's own decel scales
        // to zero (World.ShipField.cs's enginePowerScale), leaving real gravity (World.Gravity.cs)
        // as the only surviving term for however long the fight actually runs; several callers here
        // step for minutes of simulated time, long enough for even a small unopposed pull to carry
        // the ship a real distance. A real crew would have some power allocated before picking a
        // fight, not none at all, so this is realistic setup, not a workaround.
        world.ApplyCommand(playerId, new ClientCommand(playerId, PowerSystemIndex: 1, PowerDirection: 1f));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
    }

    private static void StepFor(World world, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            world.Step(RealtimeStep);
    }

    // Turns the helm toward a world position the way a player would: full throttle once roughly
    // lined up, turning input scaled off, both dropping to zero once the heading error is tiny.
    private static ClientCommand SteerToward(World world, int playerId, Vec2 target)
    {
        var shipField = world.CreateSnapshot().ShipField;
        var toTarget = target - new Vec2(shipField.X, shipField.Y);
        var bearingDegrees = MathF.Atan2((float)toTarget.Y, (float)toTarget.X) * (180f / MathF.PI) - world.Ship.ForwardDegrees;
        var error = ((bearingDegrees - shipField.RotationDegrees) % 360f + 540f) % 360f - 180f;
        return new ClientCommand(playerId,
            HelmThrottle: MathF.Abs(error) < 25f ? 1f : 0f,
            HelmTurn: MathF.Abs(error) < 2f ? 0f : MathF.Sign(error));
    }
}
