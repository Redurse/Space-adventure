using System;
using SpaceAdventure.Shared.Networking;

namespace SpaceAdventure.Client.Networking;

// Joining someone else's ship: no embedded server, no world of its own - just a socket to the host
// and the player id it handed back. The counterpart of SoloSession, and interchangeable with it as
// far as GameClient is concerned (both are "a connection plus my player id").
public sealed class NetworkSession : IDisposable
{
    private readonly TcpClientConnection _connection;

    public IClientConnection Connection => _connection;
    public int PlayerId => _connection.PlayerId;
    public bool IsConnected => _connection.IsOpen;

    private NetworkSession(TcpClientConnection connection) => _connection = connection;

    // Throws on a refused/timed-out join - the menu shows the message and stays where it is.
    public static NetworkSession Join(string host, int port) =>
        new(TcpClientConnection.Join(host, port, TimeSpan.FromSeconds(5)));

    public void Dispose() => _connection.Dispose();
}
