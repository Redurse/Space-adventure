using System.Diagnostics;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

public sealed class GameServer
{
    private const int TicksPerSecond = 30;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1.0 / TicksPerSecond);

    private readonly World _world = new();
    private readonly List<IServerConnection> _connections = new();
    private readonly Dictionary<IServerConnection, int> _playerIds = new();
    private int _nextPlayerId = 1;

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

        var snapshot = _world.CreateSnapshot();
        foreach (var connection in _connections)
            connection.Send(snapshot);
    }
}
