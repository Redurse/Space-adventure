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

    // TEMP-DIAG (M51 - "лагает с самого начала игры", FPS/Sim overlay reads Sim well under 30):
    // per-tick cost breakdown so the client's own diagnostic overlay (Game1.cs) can show WHICH part
    // of a tick is actually slow, instead of guessing further. static + a single Current instance,
    // the same "there's only ever one real one alive" reasoning GalaxyMap.Current already uses,
    // since SoloSession's embedded server runs on its own thread with no other channel back to the
    // render thread for this. Not thread-synchronized - a stale/torn read on a debug-only overlay
    // is harmless, and doubles are practically atomic on x64. Remove once the actual cause is found.
    public static GameServer? Current;
    public double LastStepMs;
    public double LastSnapshotMs;
    public double LastTickTotalMs;

    // savePath null disables persistence entirely - which is what the whole test suite wants, and
    // keeps a headless server from scribbling over a player's save file (also how the tutorial run
    // avoids ever touching the real campaign's autosave). customShip carries a Ship Editor layout
    // when shipKind is Custom; loadFrom's own CustomShip covers the "continue a custom-hull run"
    // case when the caller didn't already pass one explicitly.
    public GameServer(ShipKind shipKind = ShipKind.Frigate, SaveGame? loadFrom = null, string? savePath = null,
        CustomShipDefinition? customShip = null, bool isTutorial = false)
    {
        _world = new World(shipKind, customShip ?? loadFrom?.CustomShip);
        _savePath = savePath;
        Current = this;
        if (isTutorial)
            _world.StartTutorial();
        else if (loadFrom is not null)
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
        var tickStopwatch = Stopwatch.StartNew(); // TEMP-DIAG

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

        // M57 - "режим ускорения времени": run N ordinary, unscaled 1/30s physics steps instead of
        // one step with a scaled-up deltaSeconds (World.TimeAcceleration.cs's own doc comment
        // explains why - project history already hit the "scaled deltaSeconds overshoots a fixed
        // turn-rate threshold" trap once). Commands are still only drained ONCE above and the
        // snapshot is still only sent ONCE below - only the simulation itself runs extra times.
        var stepStopwatch = Stopwatch.StartNew(); // TEMP-DIAG
        for (var i = 0; i < _world.TimeAccelerationLevel; i++)
        {
            // Tick itself is now incremented inside World.Step (World.cs's own M58 follow-up
            // comment) - not duplicated here any more, which used to double-count against it.
            _world.Step(TickInterval.TotalSeconds);
        }
        LastStepMs = stepStopwatch.Elapsed.TotalMilliseconds; // TEMP-DIAG

        // Autosave on docking (game_design.md section 5). The World only raises a flag; the
        // decision to touch the filesystem at all is the server's.
        if (_world.AutosavePending)
        {
            _world.ClearAutosavePending();
            if (_savePath is not null)
                SaveStore.Save(_world.CreateSave(), _savePath);
        }

        var snapshotStopwatch = Stopwatch.StartNew(); // TEMP-DIAG
        var snapshot = _world.CreateSnapshot();
        LastSnapshotMs = snapshotStopwatch.Elapsed.TotalMilliseconds; // TEMP-DIAG
        foreach (var (connection, _) in _connections)
            connection.Send(snapshot);

        LastTickTotalMs = tickStopwatch.Elapsed.TotalMilliseconds; // TEMP-DIAG
    }
}
