using Anabiosis.Shared.Model;
using System.Collections.Concurrent;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Networking;

// In-memory transport connecting one embedded Server to one local Client (solo mode).
// A real network transport (TCP/LiteNetLib) can implement the same two interfaces later.
public sealed class InProcessTransport : IServerConnection, IClientConnection
{
    private readonly ConcurrentQueue<ClientCommand> _commandsToServer = new();
    private readonly ConcurrentQueue<WorldSnapshot> _snapshotsToClient = new();
    private readonly ConcurrentQueue<LobbySnapshot> _lobbyToClient = new();

    void IServerConnection.Send(WorldSnapshot snapshot) => _snapshotsToClient.Enqueue(snapshot);
    void IServerConnection.SendLobby(LobbySnapshot lobby) => _lobbyToClient.Enqueue(lobby);

    IReadOnlyList<ClientCommand> IServerConnection.ReceiveCommands()
    {
        var commands = new List<ClientCommand>();
        while (_commandsToServer.TryDequeue(out var command))
            commands.Add(command);
        return commands;
    }

    void IClientConnection.Send(ClientCommand command) => _commandsToServer.Enqueue(command);

    WorldSnapshot? IClientConnection.ReceiveLatestSnapshot()
    {
        WorldSnapshot? latest = null;
        List<VoiceChunkMessage>? droppedVoice = null;
        while (_snapshotsToClient.TryDequeue(out var snapshot))
        {
            // Voice chunks ride in exactly one tick's snapshot (World.Voice.cs read-and-clear), so a
            // skipped snapshot must hand its chunks on to the one that replaces it - otherwise
            // every client frame hitch swallows a slice of speech.
            if (latest?.VoiceChunks is { Count: > 0 } skipped)
                (droppedVoice ??= new List<VoiceChunkMessage>()).AddRange(skipped);
            latest = snapshot;
        }
        if (droppedVoice is not null && latest is not null)
        {
            if (latest.VoiceChunks is { Count: > 0 } own)
                droppedVoice.AddRange(own);
            latest = latest with { VoiceChunks = droppedVoice };
        }
        return latest;
    }

    LobbySnapshot? IClientConnection.ReceiveLatestLobby()
    {
        LobbySnapshot? latest = null;
        while (_lobbyToClient.TryDequeue(out var lobby))
            latest = lobby;
        return latest;
    }
}
