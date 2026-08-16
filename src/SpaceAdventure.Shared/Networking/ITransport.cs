using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Shared.Networking;

// Server-side end of a client-server connection: push snapshots out, drain incoming commands.
public interface IServerConnection
{
    void Send(WorldSnapshot snapshot);
    IReadOnlyList<ClientCommand> ReceiveCommands();

    // A socket can go away under the server; an in-process transport never does, which is why this
    // has a default rather than making the solo path implement a question it can't answer wrong.
    // The server drops a connection that answers false and despawns its character.
    bool IsOpen => true;
}

// Client-side end of a client-server connection: push commands out, read the latest snapshot.
public interface IClientConnection
{
    void Send(ClientCommand command);
    WorldSnapshot? ReceiveLatestSnapshot();
}
