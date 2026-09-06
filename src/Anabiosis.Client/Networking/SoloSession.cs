using System;
using System.Threading;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;

namespace Anabiosis.Client.Networking;

// Solo mode: the client spins up its own embedded server in-process and connects to it
// like a normal client, over the same InProcessTransport a real network transport will
// later be swapped in for.
public sealed class SoloSession : IDisposable
{
    private readonly GameServer _server;
    private readonly InProcessTransport _transport = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _serverThread;
    private readonly NetworkHost? _host;

    public IClientConnection Connection => _transport;
    public int PlayerId { get; }

    // The port other players type in to join this crew, null when the session is closed to the
    // network. Hosting changes nothing about how the host itself plays - they stay a local client
    // on the in-process transport, exactly as in solo.
    public int? ListenPort => _host?.Port;

    // loadFrom carries a previously saved run (game_design.md section 5); when null this is a new
    // game with the chosen hull. Either way the embedded server keeps autosaving to the standard
    // slot on every docking.
    public SoloSession(ShipKind shipKind = ShipKind.Frigate, SaveGame? loadFrom = null, int? listenPort = null,
        CustomShipDefinition? customShip = null, bool isTutorial = false)
    {
        // A tutorial run never touches the real autosave slot - null disables persistence entirely,
        // same as the test suite's own embedded servers.
        _server = new GameServer(shipKind, loadFrom, isTutorial ? null : SaveStore.DefaultPath, customShip, isTutorial);
        PlayerId = _server.Connect(_transport);
        _serverThread = new Thread(() => _server.Run(_cts.Token))
        {
            IsBackground = true,
            Name = "EmbeddedServer",
        };
        _serverThread.Start();

        // Opened only after the tick loop is running, so a player who joins in the same instant is
        // accepted by a server that is already stepping the world.
        if (listenPort is { } port)
            _host = new NetworkHost(_server, port);
    }

    public void Dispose()
    {
        _host?.Dispose();
        _cts.Cancel();
        _serverThread.Join();
        _cts.Dispose();
    }
}
