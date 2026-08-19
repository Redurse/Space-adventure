using System.Collections.Concurrent;
using System.Diagnostics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

public sealed class GameServer
{
    private const int TicksPerSecond = 30;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1.0 / TicksPerSecond);

    private readonly World _world;
    private readonly List<(IServerConnection Connection, int PlayerId)> _connections = new();

    // Players join from whichever thread accepted their socket, never from the tick loop - so the
    // list above is only ever touched by the tick, and a join waits here until the next one.
    private readonly ConcurrentQueue<(IServerConnection Connection, int PlayerId)> _joining = new();
    private int _nextPlayerId;

    private readonly string? _savePath;

    // savePath null disables persistence entirely - which is what the whole test suite wants, and
    // keeps a headless server from scribbling over a player's save file. customShip carries a
    // Ship Editor layout when shipKind is Custom; loadFrom's own CustomShip covers the "continue a
    // custom-hull run" case when the caller didn't already pass one explicitly.
    public GameServer(ShipKind shipKind = ShipKind.Frigate, SaveGame? loadFrom = null, string? savePath = null,
        CustomShipDefinition? customShip = null)
    {
        _world = new World(shipKind, customShip ?? loadFrom?.CustomShip);
        _savePath = savePath;
        if (loadFrom is not null)
            _world.ApplySave(loadFrom);
        else
            _world.StartCampaign();
    }

    // Thread-safe: NetworkHost calls this from its accept thread while the tick loop is running.
    // The id is handed back at once (the joiner's welcome frame needs it), but the character itself
    // is spawned at the top of the next tick, where the world is not mid-step.
    public int Connect(IServerConnection connection)
    {
        var playerId = Interlocked.Increment(ref _nextPlayerId);
        _joining.Enqueue((connection, playerId));
        return playerId;
    }

    public void Run(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var nextTickAt = stopwatch.Elapsed;

        while (!cancellationToken.IsCancellationRequested)
        {
            var now = stopwatch.Elapsed;
            if (now < nextTickAt)
            {
                Thread.Sleep(nextTickAt - now);
                continue;
            }

            nextTickAt += TickInterval;
            Tick();
        }
    }

    // Single tick step, exposed separately from Run() so tests can drive it without real-time waits.
    public void Tick()
    {
        while (_joining.TryDequeue(out var joiner))
        {
            _connections.Add(joiner);
            _world.SpawnCharacter(joiner.PlayerId);
        }

        // A crew member who drops out leaves with their body: the alternative is a motionless
        // character standing in a corridor, still breathing the room's air and still counted as a
        // boarder or an arrest target.
        for (var i = _connections.Count - 1; i >= 0; i--)
        {
            if (_connections[i].Connection.IsOpen)
                continue;
            _world.RemoveCharacter(_connections[i].PlayerId);
            (_connections[i].Connection as IDisposable)?.Dispose();
            _connections.RemoveAt(i);
        }

        foreach (var (connection, playerId) in _connections)
        {
            foreach (var command in connection.ReceiveCommands())
                _world.ApplyCommand(playerId, command);
        }

        _world.Tick++;
        _world.Step(TickInterval.TotalSeconds);

        // Autosave on docking (game_design.md section 5). The World only raises a flag; the
        // decision to touch the filesystem at all is the server's.
        if (_world.AutosavePending)
        {
            _world.ClearAutosavePending();
            if (_savePath is not null)
                SaveStore.Save(_world.CreateSave(), _savePath);
        }

        var snapshot = _world.CreateSnapshot();
        foreach (var (connection, _) in _connections)
            connection.Send(snapshot);
    }
}
