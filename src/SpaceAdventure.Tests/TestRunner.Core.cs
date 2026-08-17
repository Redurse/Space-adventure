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
        return Math.Abs(character.X - maxX) < 0.01f;
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
        return roomId == "cockpit" && Math.Abs(pos.Y - 0f) < 0.01f; // clamped at the top hull wall
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
        // Same wall, but y=0.5 is outside the door's 2.1..3.9 opening — should hit the wall.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.9f, 0.5f), "cockpit", new Vec2(0.3f, 0), _ => true);
        return roomId == "cockpit" && Math.Abs(pos.X - 5f) < 0.01f;
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
        grid.ApplyInput(systemIndex: 0, direction: 1f);
        for (var i = 0; i < 5; i++)
            grid.Step(1.0); // enough seconds at the adjust rate to try to overshoot the cap

        var state = grid.CreateState();
        var total = state.Allocated.Values.Sum();
        return total <= state.ReactorOutput + 0.01f && total > 0f;
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

    // Shells travel now (World.Projectiles.cs), so there has to be something out there to hit and
    // the shot needs time to reach it - "fire and read the HP next tick" isn't a thing any more.
    private static void EnterBattle(World world, int playerId = 1)
    {
        world.ApplyCommand(playerId, new ClientCommand(playerId, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.Battle; i++)
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
        var bearingDegrees = MathF.Atan2(toTarget.Y, toTarget.X) * (180f / MathF.PI) - world.Ship.ForwardDegrees;
        var error = ((bearingDegrees - shipField.RotationDegrees) % 360f + 540f) % 360f - 180f;
        return new ClientCommand(playerId,
            HelmThrottle: MathF.Abs(error) < 25f ? 1f : 0f,
            HelmTurn: MathF.Abs(error) < 2f ? 0f : MathF.Sign(error));
    }
}
