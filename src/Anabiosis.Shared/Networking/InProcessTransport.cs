using System.Collections.Concurrent;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Networking;

// In-memory transport connecting one embedded Server to one local Client (solo mode).
// A real network transport (TCP/LiteNetLib) can implement the same two interfaces later.
public sealed class InProcessTransport : IServerConnection, IClientConnection
{
    private readonly ConcurrentQueue<ClientCommand> _commandsToServer = new();
    private readonly ConcurrentQueue<WorldSnapshot> _snapshotsToClient = new();

    void IServerConnection.Send(WorldSnapshot snapshot) => _snapshotsToClient.Enqueue(snapshot);

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
        while (_snapshotsToClient.TryDequeue(out var snapshot))
            latest = snapshot;
        return latest;
    }
}
