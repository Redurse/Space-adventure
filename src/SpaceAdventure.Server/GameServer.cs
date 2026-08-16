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
    private readonly List<IServerConnection> _connections = new();
    private readonly Dictionary<IServerConnection, int> _playerIds = new();
    private int _nextPlayerId = 1;

    private readonly string? _savePath;

    // savePath null disables persistence entirely - which is what the whole test suite wants, and
    // keeps a headless server from scribbling over a player's save file.
    public GameServer(ShipKind shipKind = ShipKind.Frigate, SaveGame? loadFrom = null, string? savePath = null)
    {
        _world = new World(shipKind);
        _savePath = savePath;
        if (loadFrom is not null)
            _world.ApplySave(loadFrom);
    }

    public int Connect(IServerConnection connection)
    {
        var playerId = _nextPlayerId++;
        _connections.Add(connection);
        _playerIds[connection] = playerId;
        _world.SpawnCharacter(playerId);
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
        foreach (var connection in _connections)
        {
            var playerId = _playerIds[connection];
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
        foreach (var connection in _connections)
            connection.Send(snapshot);
    }
}
