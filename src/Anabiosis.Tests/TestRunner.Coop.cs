using System.Net.Sockets;
using Anabiosis.Server;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;
using CrewRole = Anabiosis.Shared.Model.CrewRole;

// Co-op over a real socket: the wire format, and what happens to the world when a crew member
// joins or drops out. Everything else in the game is already per-player and needs no test of its
// own for this - the point of these is the seam between the simulation and the network.
internal static partial class TestRunner
{
    // The strongest cheap check on the format: serialize, read back, serialize again, compare the
    // bytes. Anything the round trip loses - a field a record constructor can't take, a dictionary
    // key that doesn't survive, a collection that comes back empty - shows up as a difference,
    // whereas comparing a handful of fields by hand would only cover the ones remembered here.
    private static bool Wire_SnapshotSurvivesTheRoundTrip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1));
        for (var i = 0; i < 10; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var first = Wire.Serialize(snapshot);
        var restored = Wire.Deserialize<WorldSnapshot>(first);
        var second = Wire.Serialize(restored);

        return first.AsSpan().SequenceEqual(second) &&
            restored.Characters.Count == snapshot.Characters.Count &&
            restored.Rooms.Count == snapshot.Rooms.Count &&
            restored.Field.Asteroids.Count == snapshot.Field.Asteroids.Count &&
            restored.Characters[0].Inventory is not null &&
            restored.ShipUpgradeLevels.Count == snapshot.ShipUpgradeLevels.Count;
    }

    // A socket is a byte stream, not a message queue: two frames written back to back have to come
    // back as exactly those two, and the second one is big enough to take the compressed path.
    private static bool Wire_FramesAreSelfDelimitingOnOneStream()
    {
        var command = new ClientCommand(7, MoveX: 0.5f, DoorToggleId: "door-1");
        var snapshot = new World().CreateSnapshot();

        using var stream = new MemoryStream();
        Wire.WriteFrame(stream, command);
        Wire.WriteFrame(stream, new ServerMessage(ServerMessageKind.Snapshot, 0, snapshot));
        stream.Position = 0;

        var readCommand = Wire.ReadFrame<ClientCommand>(stream);
        var readMessage = Wire.ReadFrame<ServerMessage>(stream);

        return readCommand == command &&
            readMessage?.Kind == ServerMessageKind.Snapshot &&
            readMessage.Snapshot?.Rooms.Count == snapshot.Rooms.Count &&
            Wire.ReadFrame<ClientCommand>(stream) is null; // clean end of stream, not an exception
    }

    // WorldSnapshot resends the entire ship, station and galaxy layout every tick, which costs
    // nothing in-process and everything on a wire. Deflate collapses that repetition; this pins the
    // result down, because the thing that quietly makes co-op unplayable over the internet is a new
    // field that multiplies the frame. 30 ticks/sec against this budget is well under 1 Mbit/s.
    private static bool Wire_SnapshotFrameStaysWithinBudget()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);

        using var stream = new MemoryStream();
        Wire.WriteFrame(stream, new ServerMessage(ServerMessageKind.Snapshot, 0, world.CreateSnapshot()));
        Console.WriteLine($"     кадр снапшота: {stream.Length} байт (без сжатия {Wire.Serialize(world.CreateSnapshot()).Length})");
        return stream.Length < 24 * 1024;
    }

    private static bool Coop_JoinerGetsOwnCharacterAndDrivesItOverTheSocket()
    {
        var server = new GameServer();
        var hostTransport = new InProcessTransport();
        var hostId = server.Connect(hostTransport);

        // Port 0: the OS picks a free one, so the suite never collides with a running game.
        using var host = new NetworkHost(server, port: 0);
        using var joined = TcpClientConnection.Join("127.0.0.1", host.Port, TimeSpan.FromSeconds(5));
        IClientConnection guest = joined;

        var spawn = new World().Ship.SpawnPoint;
        guest.Send(new ClientCommand(joined.PlayerId, MoveX: 1)); // движение помнится сервером, слать каждый тик не нужно

        WorldSnapshot? latest = null;
        for (var i = 0; i < 400; i++)
        {
            server.Tick();
            Thread.Sleep(1); // let the socket threads carry the frame across
            latest = guest.ReceiveLatestSnapshot() ?? latest;
            if (latest?.Characters.FirstOrDefault(c => c.PlayerId == joined.PlayerId)?.X > spawn.X + 1f)
                break;
        }

        var mine = latest?.Characters.FirstOrDefault(c => c.PlayerId == joined.PlayerId);
        return joined.PlayerId != hostId &&
            latest?.Characters.Count == 2 &&
            mine is not null &&
            mine.X > spawn.X + 1f;
    }

    private static bool Coop_DroppedConnection_TakesItsCharacterOffTheShip()
    {
        var server = new GameServer();
        var hostTransport = new InProcessTransport();
        var hostId = server.Connect(hostTransport);
        var guest = new DroppableConnection();
        server.Connect(guest);

        server.Tick();
        var whileBothAboard = ((IClientConnection)hostTransport).ReceiveLatestSnapshot();

        guest.Open = false; // кабель выдернули
        server.Tick();
        var afterTheDrop = ((IClientConnection)hostTransport).ReceiveLatestSnapshot();

        return whileBothAboard?.Characters.Count == 2 &&
            afterTheDrop?.Characters.Count == 1 &&
            afterTheDrop.Characters[0].PlayerId == hostId;
    }

    // The seat has to be freed too, or the ship keeps a turret manned by a player who is no longer
    // in the session and nobody else can take it.
    private static bool Coop_GunnerLeavingUnmansTheTurret()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        MoveCharacterTo(world, 2, targetX: 1.5f, targetY: 3f);
        world.ApplyCommand(2, new ClientCommand(2, InteractPressed: true));
        if (!world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 2))
            return false;

        world.RemoveCharacter(2);

        var snapshot = world.CreateSnapshot();
        return snapshot.Characters.Count == 1 &&
            snapshot.TurretStates.All(t => t.MannedByPlayerId is null);
    }

    // Barotrauma-style pre-game lobby (GameServer's own SessionPhase.Lobby, screenshot 3 of the
    // reference set): both players join before the round exists at all, pick a nickname/role/ready
    // state, and only the host's own LobbyStartRoundPressed actually spawns anyone.
    private static bool Coop_Lobby_HostStartsRoundAfterPlayersJoinAndSetRoles()
    {
        var server = new GameServer("Тестовый сервер", maxPlayers: 4, savePath: null);
        var hostTransport = new InProcessTransport();
        var hostId = server.Connect(hostTransport);
        var guestTransport = new InProcessTransport();
        var guestId = server.Connect(guestTransport);

        server.Tick(); // dequeues both joiners into the lobby roster, no World yet

        ((IClientConnection)hostTransport).Send(new ClientCommand(hostId, Nickname: "Капитан", SetOwnRoleTo: CrewRole.Captain));
        ((IClientConnection)guestTransport).Send(new ClientCommand(guestId, Nickname: "Инженер", LobbyReady: true));
        server.Tick();

        var hostLobby = ((IClientConnection)hostTransport).ReceiveLatestLobby();
        var hostRow = hostLobby?.Players.FirstOrDefault(p => p.PlayerId == hostId);
        var guestRow = hostLobby?.Players.FirstOrDefault(p => p.PlayerId == guestId);
        if (hostLobby is not { Players.Count: 2 } || hostRow is not { IsHost: true, Nickname: "Капитан", Role: CrewRole.Captain } ||
            guestRow is not { IsHost: false, Nickname: "Инженер", IsReady: true })
            return false;

        // Not started yet - no World, no WorldSnapshot for anyone.
        if (((IClientConnection)hostTransport).ReceiveLatestSnapshot() is not null)
            return false;

        ((IClientConnection)hostTransport).Send(new ClientCommand(hostId, LobbyStartRoundPressed: true));
        server.Tick();

        var snapshot = ((IClientConnection)hostTransport).ReceiveLatestSnapshot();
        return snapshot?.Characters.Count == 2 &&
            snapshot.Characters.Any(c => c.PlayerId == hostId) &&
            snapshot.Characters.Any(c => c.PlayerId == guestId);
    }

    // GameServer.ApplyLobbyCommand only honors LobbyStartRoundPressed from _lobbyHostPlayerId -
    // anyone else's copy of that field has to be a silent no-op, not a way to force-start someone
    // else's lobby out from under them.
    private static bool Coop_Lobby_NonHostCannotStartRound()
    {
        var server = new GameServer("Тестовый сервер", maxPlayers: 4, savePath: null);
        var hostTransport = new InProcessTransport();
        server.Connect(hostTransport);
        var guestTransport = new InProcessTransport();
        var guestId = server.Connect(guestTransport);
        server.Tick();

        ((IClientConnection)guestTransport).Send(new ClientCommand(guestId, LobbyStartRoundPressed: true));
        server.Tick();

        return ((IClientConnection)hostTransport).ReceiveLatestSnapshot() is null &&
            ((IClientConnection)hostTransport).ReceiveLatestLobby() is { Players.Count: 2 };
    }

    // NetworkHost's own accept-loop cap (Game1.Menu.cs's "Макс. игроков") - a joiner past the limit
    // never gets a player id at all, just a Rejected welcome frame the client surfaces as an error.
    private static bool Coop_NetworkHost_RejectsJoinerPastMaxPlayers()
    {
        var server = new GameServer();
        var hostTransport = new InProcessTransport();
        server.Connect(hostTransport); // fills the one and only seat

        using var host = new NetworkHost(server, port: 0, maxPlayers: 1);
        try
        {
            using var joined = TcpClientConnection.Join("127.0.0.1", host.Port, TimeSpan.FromSeconds(5));
            return false; // should have been refused before a Welcome ever went out
        }
        catch (IOException ex)
        {
            return ex.Message.Contains("заполнен");
        }
    }

    // Stands in for a socket that dies: GameServer only ever asks a connection whether it is still
    // open, so a flag is the whole of what a dropped player looks like from the tick loop.
    private sealed class DroppableConnection : IServerConnection
    {
        public bool Open = true;

        public bool IsOpen => Open;

        public void Send(WorldSnapshot snapshot)
        {
        }

        public void SendLobby(LobbySnapshot lobby)
        {
        }

        public IReadOnlyList<ClientCommand> ReceiveCommands() => Array.Empty<ClientCommand>();
    }
}
