using System.Net;
using System.Net.Sockets;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

// The waiting room's extras (GameServer.Lobby.cs): chat, removing a player, the round settings, the host passing on; the dedicated server's options; the
// local-network server list (LanDiscovery.cs).
internal static partial class TestRunner
{
    // An in-process connection that can be "closed" from the test, the way a socket goes away under the server.
    private sealed class ClosableConnection : IServerConnection, IClientConnection
    {
        private readonly InProcessTransport _inner = new();
        public bool Open = true;
        public bool IsOpen => Open;
        public void Send(WorldSnapshot snapshot) => ((IServerConnection)_inner).Send(snapshot);
        public void SendLobby(LobbySnapshot lobby) => ((IServerConnection)_inner).SendLobby(lobby);
        public IReadOnlyList<ClientCommand> ReceiveCommands() => ((IServerConnection)_inner).ReceiveCommands();
        public void Send(ClientCommand command) => ((IClientConnection)_inner).Send(command);
        public WorldSnapshot? ReceiveLatestSnapshot() => ((IClientConnection)_inner).ReceiveLatestSnapshot();
        public LobbySnapshot? ReceiveLatestLobby() => ((IClientConnection)_inner).ReceiveLatestLobby();
    }

    // A lobby with a host (the first to connect) and one guest, both named, one tick in.
    private static (GameServer Server, ClosableConnection Host, int HostId, ClosableConnection Guest, int GuestId) LobbyWithGuest(bool dedicated = false)
    {
        var server = new GameServer("Тестовый сервер", maxPlayers: 4, savePath: null) { Dedicated = dedicated };
        var host = new ClosableConnection();
        var hostId = server.Connect(host);
        var guest = new ClosableConnection();
        var guestId = server.Connect(guest);
        server.Tick();
        host.Send(new ClientCommand(hostId, Nickname: "Капитан"));
        guest.Send(new ClientCommand(guestId, Nickname: "Инженер"));
        server.Tick();
        return (server, host, hostId, guest, guestId);
    }

    // What anyone says reaches everyone, under their own name.
    private static bool Lobby_ChatReachesEveryone()
    {
        var (server, host, _, guest, guestId) = LobbyWithGuest();
        guest.Send(new ClientCommand(guestId, LobbyChat: "Привет, капитан"));
        server.Tick();
        var line = host.ReceiveLatestLobby()?.Chat?.LastOrDefault();
        var seenByGuest = guest.ReceiveLatestLobby()?.Chat?.LastOrDefault();
        return line is { Author: "Инженер", Text: "Привет, капитан" } && seenByGuest == line;
    }

    // One line is not a wall of text, and the room does not remember everything for ever.
    private static bool Lobby_ChatIsTrimmedAndCapped()
    {
        var (server, host, hostId, _, _) = LobbyWithGuest();
        host.Send(new ClientCommand(hostId, LobbyChat: new string('я', 500)));
        server.Tick();
        var longLine = host.ReceiveLatestLobby()!.Chat!.Last();
        for (var i = 0; i < 60; i++)
            host.Send(new ClientCommand(hostId, LobbyChat: $"сообщение {i}"));
        server.Tick();
        var chat = host.ReceiveLatestLobby()!.Chat!;
        host.Send(new ClientCommand(hostId, LobbyChat: "   \n  "));
        server.Tick();
        var afterBlank = host.ReceiveLatestLobby()!.Chat!;
        return longLine.Text.Length <= 200 && chat.Count <= 40 && chat[^1].Text == "сообщение 59" && afterBlank.Count == chat.Count;
    }

    // The host removes a player: they vanish from the list at once, are told, and their connection is closed.
    private static bool Lobby_HostRemovesAPlayer()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        host.Send(new ClientCommand(hostId, LobbyKickPlayerId: guestId));
        server.Tick();
        var hostView = host.ReceiveLatestLobby();
        var guestView = guest.ReceiveLatestLobby();
        var removedAtOnce = hostView is { Players.Count: 1 } && guestView?.KickedPlayerIds?.Contains(guestId) == true;
        server.Tick(); // the socket is closed at the start of the next tick
        return removedAtOnce && server.PlayerCount == 1 && host.ReceiveLatestLobby() is { Players.Count: 1 };
    }

    // Nobody but the host removes anyone, and the host cannot remove themselves.
    private static bool Lobby_OnlyTheHostRemovesAndNotThemselves()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        guest.Send(new ClientCommand(guestId, LobbyKickPlayerId: hostId));
        host.Send(new ClientCommand(hostId, LobbyKickPlayerId: hostId));
        server.Tick();
        server.Tick();
        return server.PlayerCount == 2 && host.ReceiveLatestLobby() is { Players.Count: 2 };
    }

    // The host's settings reach everyone (clamped), and are what the round is built with; a guest cannot change them.
    private static bool Lobby_HostSettingsAreAppliedToTheRound()
    {
        var (server, host, hostId, guest, guestId) = LobbyWithGuest();
        guest.Send(new ClientCommand(guestId, LobbySettingsUpdate: new LobbySettings(StartCredits: 4000)));
        server.Tick();
        if (host.ReceiveLatestLobby()!.Settings!.StartCredits != 300)
            return false; // a guest changed it
        host.Send(new ClientCommand(hostId, LobbySettingsUpdate: new LobbySettings(StartCredits: 99999, GalacticWar: false, StartDocked: true)));
        server.Tick();
        var settings = guest.ReceiveLatestLobby()!.Settings!;
        if (settings.StartCredits != LobbySettings.MaxStartCredits || settings.GalacticWar || !settings.StartDocked)
            return false;
        host.Send(new ClientCommand(hostId, LobbySettingsUpdate: new LobbySettings(StartCredits: 1200)));
        server.Tick();
        host.Send(new ClientCommand(hostId, LobbyStartRoundPressed: true));
        server.Tick();
        return host.ReceiveLatestSnapshot() is { Credits: 1200 };
    }

    // The host leaves the waiting room: the one who has been there longest takes over, and can start the round.
    private static bool Lobby_HostPassesToTheOldestWhenTheyLeave()
    {
        var (server, host, _, guest, guestId) = LobbyWithGuest();
        host.Open = false;
        server.Tick();
        var view = guest.ReceiveLatestLobby();
        if (view?.Players.FirstOrDefault(p => p.PlayerId == guestId) is not { IsHost: true })
            return false;
        guest.Send(new ClientCommand(guestId, LobbyStartRoundPressed: true));
        server.Tick();
        return guest.ReceiveLatestSnapshot() is not null;
    }

    // A dedicated server has no player of its own: the first to join is the host. Empty, it does not step its world.
    private static bool Dedicated_FirstToJoinHostsAndAnEmptyRoundWaits()
    {
        var server = new GameServer("Выделенный", maxPlayers: 4, savePath: null) { Dedicated = true };
        var first = new ClosableConnection();
        var firstId = server.Connect(first);
        server.Tick();
        first.Send(new ClientCommand(firstId, Nickname: "Первый"));
        server.Tick();
        if (first.ReceiveLatestLobby()?.Players.FirstOrDefault(p => p.PlayerId == firstId) is not { IsHost: true })
            return false;
        first.Send(new ClientCommand(firstId, LobbyStartRoundPressed: true));
        server.Tick();
        var tickWhileOn = first.ReceiveLatestSnapshot()?.Tick;
        if (tickWhileOn is null)
            return false;
        first.Open = false;
        server.Tick(); // the last player is dropped
        var observer = new ClosableConnection();
        server.Connect(observer);
        // Someone joins again: the world resumes from where it stood (it did not run on, empty).
        server.Tick();
        var tickAfter = observer.ReceiveLatestSnapshot()?.Tick;
        return tickAfter is { } t && t <= tickWhileOn + 3;
    }

    // The console of the dedicated server asks the same things the host's buttons do.
    private static bool Dedicated_ConsoleCanStartAndRemove()
    {
        var (server, host, _, _, guestId) = LobbyWithGuest(dedicated: true);
        server.EnqueueAdmin(() => server.AdminKick(guestId));
        server.Tick();
        var removed = host.ReceiveLatestLobby()?.Players.Count == 1;
        server.EnqueueAdmin(server.AdminStartRound);
        server.Tick();
        return removed && !server.InLobby && host.ReceiveLatestSnapshot() is not null && server.StatusText.Contains("раунд");
    }

    private static bool Dedicated_OptionsAreParsedAndChecked()
    {
        var defaults = DedicatedServerOptions.Parse(Array.Empty<string>());
        var full = DedicatedServerOptions.Parse(new[] { "--name", "Мой", "--port", "5000", "--max", "6", "--save", "s.json", "--continue", "--no-lan" });
        return defaults is { Error: null, Port: Anabiosis.Shared.Networking.Wire.DefaultPort, MaxPlayers: 4, Lan: true, Continue: false }
            && full is { Error: null, Name: "Мой", Port: 5000, MaxPlayers: 6, SavePath: "s.json", Continue: true, Lan: false }
            && DedicatedServerOptions.Parse(new[] { "--port", "70000" }).Error is not null
            && DedicatedServerOptions.Parse(new[] { "--max", "0" }).Error is not null
            && DedicatedServerOptions.Parse(new[] { "--max", "9" }).Error is not null
            && DedicatedServerOptions.Parse(new[] { "--continue" }).Error is not null
            && DedicatedServerOptions.Parse(new[] { "--nonsense" }).Error is not null
            && DedicatedServerOptions.Parse(new[] { "--name" }).Error is not null
            && DedicatedServerOptions.Parse(new[] { "--help" }).Help;
    }

    // ---- the local-network server list ----

    private static bool LanDiscovery_AnnouncementRoundTripsAndJunkIsIgnored()
    {
        var bytes = LanDiscovery.Encode(new LanAnnouncement("Мой" + (char)10 + "сервер", 47624, 2, 4, true), "abc123");
        if (!LanDiscovery.TryDecode(bytes, "192.168.0.5", DateTime.UtcNow, out var info))
            return false;
        return info is { Id: "abc123", Name: "Мой сервер", Address: "192.168.0.5", Port: 47624, Players: 2, MaxPlayers: 4, InLobby: true }
            && !LanDiscovery.TryDecode(new byte[] { 1, 2, 3 }, "x", DateTime.UtcNow, out _)
            && !LanDiscovery.TryDecode(System.Text.Encoding.UTF8.GetBytes("HELLO\nа\n1\n2\n3\n0"), "x", DateTime.UtcNow, out _)
            && !LanDiscovery.TryDecode(System.Text.Encoding.UTF8.GetBytes(LanDiscovery.Magic + "\nа\n99999\n2\n3\n0"), "x", DateTime.UtcNow, out _);
    }

    // A browser hears an announcer (straight over the loopback on a private port, so the test does not depend on the network), with the live player count.
    private static bool LanDiscovery_BrowserFindsAnAnnouncerWithItsLivePlayerCount()
    {
        int port;
        using (var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            port = ((IPEndPoint)probe.Client.LocalEndPoint!).Port;

        var players = 1;
        using var browser = new LanBrowser(port);
        using var announcer = new LanAnnouncer(() => new LanAnnouncement("Лодка", 47624, players, 4, false), port, new[] { IPAddress.Loopback });
        bool Seen(int expectedPlayers) =>
            browser.Servers.Any(s => s.Name == "Лодка" && s.Players == expectedPlayers && s.Port == 47624 && s.Address == "127.0.0.1");

        var found = false;
        for (var i = 0; i < 100 && !found; i++)
        {
            found = Seen(1);
            if (!found)
                Thread.Sleep(50);
        }
        if (!found)
            return false;
        players = 3;
        for (var i = 0; i < 100; i++)
        {
            if (Seen(3))
                return true;
            Thread.Sleep(50);
        }
        return false;
    }
}
