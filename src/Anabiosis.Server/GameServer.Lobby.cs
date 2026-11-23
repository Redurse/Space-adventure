using System.Collections.Concurrent;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The waiting room (SessionPhase.Lobby): what everyone can do there (their own name, role, look, kit, ready/away/watching flags, the chat), what only the host
// can do (pick the ship or a saved campaign, change the round's settings, remove or ban a player, start), how the host passes on if they leave, and the
// dedicated server's console hooks. A player who is "away" (AFK) is not spawned when the round starts and stays here, in the waiting room, until they
// come back; one who is "watching" gets the round's snapshots but no character.
public sealed partial class GameServer
{
    private const int LobbyChatMaxLines = 40;
    private const int LobbyChatMaxLength = 200;

    private readonly List<LobbyChatLine> _lobbyChat = new();
    private LobbySettings _lobbySettings = new();
    // Removed by the host this tick: they are in the lobby frame that goes out (so the one removed can say why), then their sockets are closed.
    private readonly List<int> _lobbyKicks = new();
    private readonly List<int> _lobbyKickedLastTick = new();
    private readonly ConcurrentQueue<Action> _adminActions = new();
    private volatile string _statusText = "";

    // A server with no player of its own (Anabiosis.Server.exe): the first one to join becomes the host, and an empty running world is not stepped.
    public bool Dedicated { get; init; }

    public bool InLobby => _phase == SessionPhase.Lobby;

    // What the host has set for the round, as the lobby has it right now.
    public LobbySettings Settings => _lobbySettings;

    // The name the server goes by now (the host can rename it in the settings window) - read by whoever announces it on the network.
    public string LobbyServerName => string.IsNullOrWhiteSpace(_lobbyServerName) ? "Сервер" : _lobbyServerName;

    // A line the console shows for the "status" command: phase, who is on, refreshed every tick (read from another thread, so a whole string).
    public string StatusText => _statusText;

    // Run on the tick thread, between ticks - how the dedicated server's console and the tests touch the server without racing the tick.
    public void EnqueueAdmin(Action action) => _adminActions.Enqueue(action);

    private void RunAdminActions()
    {
        while (_adminActions.TryDequeue(out var action))
            action();
    }

    // The console's "start" and "kick": the same things the host's buttons do.
    public void AdminStartRound()
    {
        if (_phase == SessionPhase.Lobby)
            StartRoundFromLobby();
    }

    public bool AdminKick(int playerId) => KickFromLobby(playerId);

    private void UpdateStatusText()
    {
        var phase = _phase == SessionPhase.Lobby ? "лобби" : "раунд идёт";
        var names = _lobbyRoster.Count > 0
            ? string.Join(", ", _connections.Select(c => $"{c.PlayerId}:{(_lobbyRoster.TryGetValue(c.PlayerId, out var e) ? e.Nickname ?? "?" : "?")}{(c.PlayerId == _lobbyHostPlayerId ? "*" : "")}"))
            : string.Join(", ", _connections.Select(c => c.PlayerId.ToString()));
        _statusText = $"{phase}; игроков {_connections.Count}{(names.Length > 0 ? " [" + names + "]" : "")}";
    }

    // ---- chat ----

    private void AddLobbyChat(string author, string text)
    {
        var line = string.Join(' ', text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (line.Length == 0)
            return;
        if (line.Length > LobbyChatMaxLength)
            line = line[..LobbyChatMaxLength];
        _lobbyChat.Add(new LobbyChatLine(author, line));
        if (_lobbyChat.Count > LobbyChatMaxLines)
            _lobbyChat.RemoveAt(0);
    }

    // ---- commands ----

    // Everything a lobby-phase client can do: update their own roster row (Nickname/SetOwnRoleTo are the same fields an in-round Character already
    // reads every tick - see ClientCommand's own doc comments - LobbyReady is the lobby-only addition), their look, kit and away/watching flags, say
    // something in the chat, and, host only, change the selected ship or the round's settings, remove or ban a player, or actually start the round.
    // The same handler serves a player held in the waiting room during a round (away): they can chat and come back, and nothing else.
    private void ApplyLobbyCommand(int playerId, ClientCommand command)
    {
        if (!_lobbyRoster.TryGetValue(playerId, out var entry))
            return;
        if (!string.IsNullOrEmpty(command.Nickname))
            entry.Nickname = command.Nickname;
        if (command.SetOwnRoleTo is { } role)
            entry.Role = role;
        if (_phase == SessionPhase.Lobby)
            entry.IsReady = command.LobbyReady;
        if (command.LobbyLook is { } look)
            entry.Look = look.Clamped();
        if (command.LobbyKitIndex is { } kit)
            entry.KitIndex = Math.Clamp(kit, 0, StartKits.PerRole - 1);
        // "Away" and "watching" exclude each other; each is only honoured while the host allows it.
        if (command.LobbyAfk is { } afk)
        {
            entry.IsAfk = afk && _lobbySettings.AllowAfk;
            if (entry.IsAfk)
                entry.IsSpectator = false;
        }
        if (command.LobbySpectate is { } spectate && _phase == SessionPhase.Lobby)
        {
            entry.IsSpectator = spectate && _lobbySettings.AllowSpectate;
            if (entry.IsSpectator)
                entry.IsAfk = false;
        }
        if (!string.IsNullOrWhiteSpace(command.LobbyChat))
            AddLobbyChat(entry.Nickname ?? "игрок", command.LobbyChat);

        if (playerId != _lobbyHostPlayerId || _phase != SessionPhase.Lobby)
            return; // everything below is host-only, and only while the round has not begun

        if (command.LobbySelectCustomShip is not null)
        {
            _lobbyCustomShip = command.LobbySelectCustomShip;
            _lobbyCustomShipName = command.LobbySelectCustomShipName;
            _lobbyShipKind = ShipKind.Custom;
            _lobbyLoadSave = null; // choosing a ship goes back to a new campaign
            _lobbyLoadedTitle = null;
        }

        if (command.LobbyLoadSaveId is { } saveId)
            ChooseSavedCampaign(saveId);

        if (command.LobbySettingsUpdate is { } settings)
            ApplyLobbySettings(settings.Clamped());

        if (command.LobbyBanPlayerId is { } banId && banId != playerId)
            BanFromLobby(banId);

        if (command.LobbyUnbanId is { } unbanId)
            Unban(unbanId);

        if (command.LobbyKickPlayerId is { } kickId && kickId != playerId)
            KickFromLobby(kickId);

        if (command.LobbyStartRoundPressed)
            StartRoundFromLobby();
    }

    private void ApplyLobbySettings(LobbySettings settings)
    {
        _lobbySettings = settings;
        if (settings.ServerName.Length > 0)
            _lobbyServerName = settings.ServerName;
        // Turning a flag off sends everyone who had it back to being a normal player.
        foreach (var entry in _lobbyRoster.Values)
        {
            if (!settings.AllowAfk)
                entry.IsAfk = false;
            if (!settings.AllowSpectate)
                entry.IsSpectator = false;
        }
    }

    // ---- saved campaigns ----

    private SaveGame? _lobbyLoadSave;
    private string? _lobbyLoadedTitle;
    private IReadOnlyList<LobbySaveInfo> _savesCache = Array.Empty<LobbySaveInfo>();
    private int _savesRefreshCountdown;

    // An empty id goes back to a new campaign; a known one makes the round continue that save.
    private void ChooseSavedCampaign(string id)
    {
        if (id.Length == 0)
        {
            _lobbyLoadSave = null;
            _lobbyLoadedTitle = null;
            return;
        }
        if (_savePath is null || SaveStore.LoadSlot(SaveStore.SlotsDirectoryFor(_savePath), id) is not { } save)
            return;
        _lobbyLoadSave = save;
        _lobbyLoadedTitle = _savesCache.FirstOrDefault(s => s.Id == id)?.Title ?? id;
    }

    private IReadOnlyList<LobbySaveInfo> CurrentSaves()
    {
        // A directory read and a parse of every file: once in a while, not every tick.
        if (_savesRefreshCountdown-- <= 0)
        {
            _savesCache = _savePath is null ? Array.Empty<LobbySaveInfo>() : SaveStore.ListSlots(SaveStore.SlotsDirectoryFor(_savePath));
            _savesRefreshCountdown = 150;
        }
        return _savesCache;
    }

    // ---- removing a player ----

    private bool KickFromLobby(int playerId)
    {
        if (_phase != SessionPhase.Lobby || playerId == _lobbyHostPlayerId || !_lobbyRoster.ContainsKey(playerId) || _lobbyKicks.Contains(playerId))
            return false;
        _lobbyKicks.Add(playerId);
        _lobbyRoster.Remove(playerId); // gone from everyone's list at once; the socket closes after the frame that tells them
        return true;
    }

    private void MarkKickedForDropping()
    {
        _lobbyKickedLastTick.AddRange(_lobbyKicks);
        _lobbyKicks.Clear();
    }

    private void DropKickedConnections()
    {
        foreach (var playerId in _lobbyKickedLastTick)
        {
            var index = _connections.FindIndex(c => c.PlayerId == playerId);
            if (index < 0)
                continue;
            (_connections[index].Connection as IDisposable)?.Dispose();
            _connections.RemoveAt(index);
        }
        _lobbyKickedLastTick.Clear();
    }

    // The host left the waiting room: the one who has been here longest takes over (a dedicated server has no host of its own at all).
    private void PassLobbyHostIfNeeded(int leavingId)
    {
        if (leavingId != _lobbyHostPlayerId)
            return;
        _lobbyHostPlayerId = _connections.Count > 0 ? _connections.Min(c => c.PlayerId) : null;
    }

    // ---- starting ----

    // The lobby's own "НАЧАТЬ" - builds the World for real (everything StartHostedSession's own non-lobby path already does synchronously in the
    // constructor, just deferred to this moment instead), applies the host's settings, and spawns every player already sitting in the roster at once
    // - with the look, role and kit each chose; an away player stays in the roster, a watcher is let through without a character.
    // _lobbyCustomShip staying null is not an error - it falls back to the frozen default hull exactly like ShipKind.Custom with customShip: null already
    // does everywhere else in this project. A chosen save is the world instead: restored as "Продолжить" restores it.
    private void StartRoundFromLobby()
    {
        if (_phase != SessionPhase.Lobby)
            return;
        var settings = _lobbySettings;
        if (_lobbyLoadSave is { } save)
        {
            _world = new World(save.ShipKind, save.CustomShip, save.WorldSeed ?? 0, startDocked: true);
            _world.ApplySave(save);
        }
        else
        {
            // A test server (no save path) always starts docked, as every test is written against; a real game starts as the host chose.
            _world = new World(_lobbyShipKind, _lobbyCustomShip, _savePath is null ? 0 : NewWorldSeed(), startDocked: _savePath is null || settings.StartDocked);
            _world.StartCampaign();
            _world.SetStartingCredits(settings.StartCredits);
        }
        _phase = SessionPhase.Running;
        _world.SetModifiers(settings.Modifiers);
        _world.VoiceChatEnabled = settings.VoiceChat;
        if (_savePath is not null && (settings.GalacticWar || _lobbyLoadSave is not null))
            _world.EnableWar();
        foreach (var (playerId, entry) in _lobbyRoster.ToList())
        {
            if (entry.IsAfk)
                continue; // stays in the waiting room
            if (!entry.IsSpectator)
                _world.SpawnCharacter(playerId, entry.Look, entry.Role, entry.KitIndex);
            _lobbyRoster.Remove(playerId);
        }
        // Nickname/Role aren't copied from the roster onto the freshly spawned Characters here - every client (host included) already resends both every
        // tick via ordinary ClientCommand.Nickname/SetOwnRoleTo, so they re-arrive within a tick or two on their own, the exact same path a non-lobby join
        // already relies on.
    }

    // "Start when the players are ready": once enough of those who are taking part (not away) have pressed ready, the round begins by itself.
    private void CheckAutoStart()
    {
        if (!_lobbySettings.StartWhenReady)
            return;
        var taking = _lobbyRoster.Values.Where(e => !e.IsAfk).ToList();
        if (taking.Count == 0)
            return;
        if (taking.Count(e => e.IsReady) * 100 >= taking.Count * _lobbySettings.StartWhenReadyPercent)
            StartRoundFromLobby();
    }

    // Away players who came back while the round runs: they join it now, as a late arrival does.
    private void ReleaseHeldPlayers()
    {
        foreach (var (playerId, entry) in _lobbyRoster.ToList())
        {
            if (entry.IsAfk)
                continue;
            _world!.SpawnCharacter(playerId, entry.Look, entry.Role, entry.KitIndex);
            _lobbyRoster.Remove(playerId);
        }
    }

    private LobbySnapshot BuildLobbySnapshot()
    {
        var players = new List<LobbyPlayerState>(_connections.Count);
        foreach (var (_, playerId) in _connections)
        {
            if (!_lobbyRoster.TryGetValue(playerId, out var entry))
                continue; // just despawned/dropped this same tick - CreateSnapshot's own idea of "no stale rows" mirrored here
            players.Add(new LobbyPlayerState(playerId, entry.Nickname, entry.Role, playerId == _lobbyHostPlayerId, entry.IsReady,
                entry.IsAfk, entry.IsSpectator, entry.Look, entry.KitIndex));
        }
        return new LobbySnapshot(LobbyServerName, _lobbyMaxPlayers, players, _lobbyShipKind, _lobbyCustomShipName,
            Chat: _lobbyChat.ToArray(), Settings: _lobbySettings, KickedPlayerIds: _lobbyKicks.Count > 0 ? _lobbyKicks.ToArray() : null,
            Bans: BanList(), Saves: _phase == SessionPhase.Lobby ? CurrentSaves() : null, LoadedSaveTitle: _lobbyLoadedTitle,
            RoundInProgress: _phase == SessionPhase.Running);
    }
}
