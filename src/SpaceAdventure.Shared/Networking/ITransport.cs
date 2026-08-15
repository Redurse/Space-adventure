using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Shared.Networking;

// Server-side end of a client-server connection: push snapshots out, drain incoming commands.
public interface IServerConnection
{
    void Send(WorldSnapshot snapshot);
    IReadOnlyList<ClientCommand> ReceiveCommands();
}

// Client-side end of a client-server connection: push commands out, read the latest snapshot.
public interface IClientConnection
{
    void Send(ClientCommand command);
    WorldSnapshot? ReceiveLatestSnapshot();
}
