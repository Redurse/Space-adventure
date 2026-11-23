using System;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// Holds the latest state received from the server and forwards outgoing commands.
// Owns no simulation logic of its own.
public sealed class GameClient
{
    private readonly IClientConnection _connection;

    public int PlayerId { get; }
    private readonly SnapshotSmoother _smoother = new();
    public WorldSnapshot? LatestSnapshot { get; private set; }
    // Barotrauma-style pre-game lobby (screenshot 3 of the reference set) - non-null only while
    // GameServer's own SessionPhase is Lobby, never overwritten back to null once the round starts
    // (ordinary WorldSnapshots just take over from there; Game1.Menu.cs's own lobby screen reads
    // _sessionStarted/_client.LatestSnapshot to notice the round began, the same way it already
    // does for every other session).
    public LobbySnapshot? LatestLobby { get; private set; }

    public GameClient(IClientConnection connection, int playerId)
    {
        _connection = connection;
        PlayerId = playerId;
    }

    public void Send(ClientCommand command) => _connection.Send(command);

    // LatestSnapshot is the newest server snapshot pushed forward to this moment (SnapshotSmoother.cs) - a new object every call, same Tick until the
    // server sends the next one.
    public void PollSnapshots()
    {
        var now = SnapshotSmoother.Now();
        var snapshot = _connection.ReceiveLatestSnapshot();
        if (snapshot is not null)
            _smoother.Accept(snapshot, now);
        if (_smoother.Advance(now) is { } shown)
            LatestSnapshot = shown;
        var lobby = _connection.ReceiveLatestLobby();
        if (lobby is not null)
            LatestLobby = lobby;
    }
}
