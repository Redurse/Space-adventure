using System.Collections.Concurrent;
using System.Diagnostics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

public sealed partial class GameServer
{
    private const int TicksPerSecond = 30;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1.0 / TicksPerSecond);

    // Null only while SessionPhase is Lobby - StartRoundFromLobby constructs it the instant the
    // host's own LobbyStartRoundPressed command arrives. Every entry point OTHER than the "Создать
    // сервер" screen's own lobby constructor below builds this immediately, exactly as before, and
    // SessionPhase for those starts at Running - so solo play, the old [H]-toggle quick-host,
    // Continue, tutorial, editor "Играть", and every existing test are completely unaffected by
    // this field's own nullability.
    private World? _world;
    private readonly List<(IServerConnection Connection, int PlayerId)> _connections = new();

    // Players join from whichever thread accepted their socket, never from the tick loop - so the
    // list above is only ever touched by the tick, and a join waits here until the next one.
    private readonly ConcurrentQueue<(IServerConnection Connection, int PlayerId)> _joining = new();
    private int _nextPlayerId;

    private readonly string? _savePath;

    private enum SessionPhase { Lobby, Running }
    private SessionPhase _phase;

    // ---- Lobby-phase-only state (screenshot 3 of the Barotrauma reference set) - unused and
    // untouched for every non-lobby GameServer, since _phase starts at Running for those. ----
    private sealed class LobbyRosterEntry
    {
        public string? Nickname;
        public CrewRole? Role;
        public bool IsReady;
        public bool IsAfk;
        public bool IsSpectator;
        public CharacterLook? Look;
        public int KitIndex;
    }
    private string? _lobbyServerName;
    private readonly int _lobbyMaxPlayers;
    private int? _lobbyHostPlayerId;
    private readonly Dictionary<int, LobbyRosterEntry> _lobbyRoster = new();
    private ShipKind _lobbyShipKind = ShipKind.Custom;
    private CustomShipDefinition? _lobbyCustomShip;
    private string? _lobbyCustomShipName;

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
    public GameServer(ShipKind shipKind = ShipKind.Custom, SaveGame? loadFrom = null, string? savePath = null,
        CustomShipDefinition? customShip = null, bool isTutorial = false)
    {
        // A real, persisted game rolls its own world (hidden places differ every playthrough) and keeps it in
        // the save; tests and the tutorial (no save path) stay on the fixed seed-0 world.
        _world = new World(shipKind, customShip ?? loadFrom?.CustomShip,
            loadFrom?.WorldSeed ?? (savePath is null || isTutorial ? 0 : NewWorldSeed()),
            // A real new game starts with the ship free beside the station, in its base position; a loaded save restores its own state, tests and the tutorial dock.
            startDocked: !(loadFrom is null && !isTutorial && savePath is not null));
        _savePath = savePath;
        _phase = SessionPhase.Running;
        Current = this;
        if (isTutorial)
            _world.StartTutorial();
        else if (loadFrom is not null)
            _world.ApplySave(loadFrom);
        else
            _world.StartCampaign();
        // The galactic war runs in a real game only: a test world or the tutorial would pay for a whole galaxy for nothing.
        if (savePath is not null && !isTutorial)
            _world.EnableWar();
    }

    // Direct user request (screenshot 3 of the Barotrauma reference set - a real pre-game waiting
    // room: player list, live role/ship pick, a start button the round only actually begins on) -
    // the World/campaign is deliberately NOT constructed here. StartRoundFromLobby below builds it
    // the instant the host's own LobbyStartRoundPressed command lands, which is the only thing that
    // ever transitions _phase away from Lobby. Continuing a saved run has no lobby - only a brand
    // new game does, since "which save" isn't a decision a waiting room needs to broadcast.
    public GameServer(string lobbyServerName, int maxPlayers, string? savePath)
    {
        _phase = SessionPhase.Lobby;
        _lobbyServerName = lobbyServerName;
        _lobbyMaxPlayers = maxPlayers;
        _savePath = savePath;
        LoadBans();
        Current = this;
    }

    // How many seats are already taken (joined + still joining) - read by NetworkHost's accept
    // thread to decide whether a new socket fits under the host's own "Макс. игроков" cap before
    // it ever calls Connect. Approximate under concurrent joins (no lock spans both collections),
    // which is fine for a soft cap on a friends-and-family server: worst case one extra player
    // slips in the same instant the last seat fills, never a real overcommit.
    public int PlayerCount => _connections.Count + _joining.Count;

    // Thread-safe: NetworkHost calls this from its accept thread while the tick loop is running.
    // The id is handed back at once (the joiner's welcome frame needs it), but the character itself
    // is spawned at the top of the next tick, where the world is not mid-step.
    public int Connect(IServerConnection connection)
    {
        var playerId = Interlocked.Increment(ref _nextPlayerId);
        // The very first Connect a lobby-mode server ever receives is always the host's own local
        // (InProcessTransport) connection - SoloSession's lobby constructor calls Connect for
        // itself before NetworkHost (and therefore any joiner's own Connect) even exists, so this
        // never races against a joiner for "who got here first".
        if (_phase == SessionPhase.Lobby)
            _lobbyHostPlayerId ??= playerId;
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

        RunAdminActions(); // the dedicated server's console (GameServer.Lobby.cs) - on this thread, between ticks
        while (_joining.TryDequeue(out var joiner))
        {
            _connections.Add(joiner);
            if (_phase == SessionPhase.Running)
                _world!.SpawnCharacter(joiner.PlayerId);
            else
                _lobbyRoster[joiner.PlayerId] = new LobbyRosterEntry();
        }

        // A crew member who drops out leaves with their body: the alternative is a motionless
        // character standing in a corridor, still breathing the room's air and still counted as a
        // boarder or an arrest target. A lobby-phase drop-out has no body to leave - just their
        // own roster row disappearing off everyone else's player list.
        for (var i = _connections.Count - 1; i >= 0; i--)
        {
            if (_connections[i].Connection.IsOpen)
                continue;
            var leavingId = _connections[i].PlayerId;
            if (_phase == SessionPhase.Running)
                _world!.RemoveCharacter(leavingId);
            _lobbyRoster.Remove(leavingId); // a player held in the waiting room during a round has a row too
            lock (_banLock)
                _addresses.Remove(leavingId);
            (_connections[i].Connection as IDisposable)?.Dispose();
            _connections.RemoveAt(i);
            if (_phase == SessionPhase.Lobby)
                PassLobbyHostIfNeeded(leavingId);
        }
        DropKickedConnections();

        foreach (var (connection, playerId) in _connections)
        {
            foreach (var command in connection.ReceiveCommands())
            {
                // In a round, a player who is away still sits in the waiting room: what they send is the lobby's business, not the world's.
                if (_phase == SessionPhase.Running && !_lobbyRoster.ContainsKey(playerId))
                    _world!.ApplyCommand(playerId, command);
                else
                    ApplyLobbyCommand(playerId, command);
            }
        }

        if (_phase == SessionPhase.Lobby)
            CheckAutoStart();
        else
            ReleaseHeldPlayers();

        if (_phase == SessionPhase.Lobby)
        {
            var lobbySnapshot = BuildLobbySnapshot();
            foreach (var (connection, _) in _connections)
                connection.SendLobby(lobbySnapshot);
            MarkKickedForDropping(); // sent once more with their ids in it; their sockets close at the start of the next tick
            UpdateStatusText();
            LastTickTotalMs = tickStopwatch.Elapsed.TotalMilliseconds; // TEMP-DIAG
            return;
        }

        // A dedicated server with nobody on it does not run the world: it waits, as a closed shop does not heat the rooms.
        if (Dedicated && _connections.Count == 0)
        {
            UpdateStatusText();
            return;
        }

        // M57 - "режим ускорения времени": run N ordinary, unscaled 1/30s physics steps instead of
        // one step with a scaled-up deltaSeconds (World.TimeAcceleration.cs's own doc comment
        // explains why - project history already hit the "scaled deltaSeconds overshoots a fixed
        // turn-rate threshold" trap once). Commands are still only drained ONCE above and the
        // snapshot is still only sent ONCE below - only the simulation itself runs extra times.
        var stepStopwatch = Stopwatch.StartNew(); // TEMP-DIAG
        for (var i = 0; i < _world!.TimeAccelerationLevel; i++)
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
            {
                var save = _world.CreateSave();
                SaveStore.Save(save, _savePath);
                SaveStore.SaveSlot(save, SaveStore.SlotsDirectoryFor(_savePath)); // also kept as this campaign's own slot (the lobby's "Загр. кампанию")
            }
        }

        var snapshotStopwatch = Stopwatch.StartNew(); // TEMP-DIAG
        var snapshot = _world.CreateSnapshot();
        LastSnapshotMs = snapshotStopwatch.Elapsed.TotalMilliseconds; // TEMP-DIAG
        // REVERTED (WorldSnapshot.cs's own doc comment has the full story) - used to send Doors/
        // Turrets only on a connection's first tick, relying on every tick's snapshot eventually
        // reaching the client, which the "latest tick wins" connection types don't guarantee.
        // Those held in the waiting room (away) keep seeing the lobby; everyone else gets the world.
        var heldLobby = _lobbyRoster.Count > 0 ? BuildLobbySnapshot() : null;
        foreach (var (connection, playerId) in _connections)
        {
            if (heldLobby is not null && _lobbyRoster.ContainsKey(playerId))
                connection.SendLobby(heldLobby);
            else
                connection.Send(snapshot);
        }

        UpdateStatusText();
        LastTickTotalMs = tickStopwatch.Elapsed.TotalMilliseconds; // TEMP-DIAG
    }

    private static int NewWorldSeed() => Random.Shared.Next(1, int.MaxValue);
}
