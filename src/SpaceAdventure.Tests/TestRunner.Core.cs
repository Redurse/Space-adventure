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
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(2.5f, 0.5f), "cockpit", new Vec2(0, -1f), _ => true);
        // Clamped CharacterRadius short of the top hull wall, not exactly on it (see RoomLayout.cs).
        return roomId == "cockpit" && Math.Abs(pos.Y - RoomLayout.CharacterRadius) < 0.01f;
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
        // Same wall, but y=0.5 is outside the door's 2.1..3.9 opening — should hit the wall, stopping
        // CharacterRadius short of it rather than exactly on it (see RoomLayout.cs).
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.9f, 0.5f), "cockpit", new Vec2(0.3f, 0), _ => true);
        return roomId == "cockpit" && Math.Abs(pos.X - (5f - RoomLayout.CharacterRadius)) < 0.01f;
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
    // diagonal move can never clip a corner) and mans it, unless already there.
    private static void SitAtHelm(World world, int playerId = 1)
    {
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId).IsAtHelm)
        {
            MoveCharacterTo(world, playerId, 3f, 3f); // corridor -> reactor -> cockpit, at the doors' shared height
            MoveCharacterTo(world, playerId, 3f, 4f); // helm console
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
        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);

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

        for (var i = 0; i < maxTicks && !until(); i++)
        {
            var shipPos = new Vec2(world.CreateSnapshot().ShipField.X, world.CreateSnapshot().ShipField.Y);
            var steerTarget = AvoidIncidentalHazards(world, shipPos, target, targetPointId);
            world.ApplyCommand(playerId, SteerToward(world, playerId, steerTarget));
            world.Step(RealtimeStep);
        }
    }

    // How far clear of a hostile sector's own CaptureRadius(8) a course cutting across the system
    // has to stay - SteerToward has no obstacle-avoidance of its own, so a straight line toward
    // some other target can otherwise clip a sector it was never actually headed for, starting a
    // fight that has nothing to do with whatever the test is checking.
    private const float HazardClearance = 20f;

    // If the straight line from `from` to `target` would pass within HazardClearance of some
    // hostile sector OTHER than targetPointId in the ship's current system, returns a waypoint
    // that clears it with the smallest possible sideways detour instead; otherwise returns
    // `target` unchanged. Recomputed fresh every tick (FlyToward's own loop) off the ship's actual
    // current position, so the course keeps curving smoothly around the hazard rather than
    // committing to one fixed detour point regardless of how the approach angle changes.
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
            var toHazard = hazard.Position - from;
            var projected = toHazard.X * dir.X + toHazard.Y * dir.Y;
            if (projected < 0f || projected > length)
                continue; // not actually between here and the target

            var closestPoint = from + dir * projected;
            var offset = hazard.Position - closestPoint;
            if (offset.Length() >= HazardClearance)
                continue;

            var perpendicular = new Vec2(-dir.Y, dir.X);
            var side = offset.X * perpendicular.X + offset.Y * perpendicular.Y >= 0f ? -1f : 1f;
            return closestPoint + perpendicular * (side * HazardClearance);
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
        var bearingDegrees = MathF.Atan2(toTarget.Y, toTarget.X) * (180f / MathF.PI) - world.Ship.ForwardDegrees;
        var error = ((bearingDegrees - shipField.RotationDegrees) % 360f + 540f) % 360f - 180f;
        return new ClientCommand(playerId,
            HelmThrottle: MathF.Abs(error) < 25f ? 1f : 0f,
            HelmTurn: MathF.Abs(error) < 2f ? 0f : MathF.Sign(error));
    }
}
