using System;
using System.Threading;
using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;

namespace SpaceAdventure.Client.Networking;

// Solo mode: the client spins up its own embedded server in-process and connects to it
// like a normal client, over the same InProcessTransport a real network transport will
// later be swapped in for.
public sealed class SoloSession : IDisposable
{
    private readonly GameServer _server;
    private readonly InProcessTransport _transport = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _serverThread;

    public IClientConnection Connection => _transport;
    public int PlayerId { get; }

    // loadFrom carries a previously saved run (game_design.md section 5); when null this is a new
    // game with the chosen hull. Either way the embedded server keeps autosaving to the standard
    // slot on every docking.
    public SoloSession(ShipKind shipKind = ShipKind.Frigate, SaveGame? loadFrom = null)
    {
        _server = new GameServer(shipKind, loadFrom, SaveStore.DefaultPath);
        PlayerId = _server.Connect(_transport);
        _serverThread = new Thread(() => _server.Run(_cts.Token))
        {
            IsBackground = true,
            Name = "EmbeddedServer",
        };
        _serverThread.Start();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _serverThread.Join();
        _cts.Dispose();
    }
}
