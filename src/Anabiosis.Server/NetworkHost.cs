using System.Net;
using System.Net.Sockets;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Opens a running GameServer to players on other machines: a listen socket whose accepted clients
// become ordinary IServerConnections. The host keeps playing through its own in-process transport,
// so this is a listen server (the host is player 1), not a separate dedicated process - which is
// what a co-op crew of friends actually wants, and it leaves the solo path untouched.
public sealed class NetworkHost : IDisposable
{
    private readonly GameServer _server;
    private readonly TcpListener _listener;
    private readonly Thread _acceptThread;
    private readonly int _maxPlayers;
    private volatile bool _running = true;

    public int Port { get; }

    // maxPlayers counts the host's own seat too (GameServer.PlayerCount does) - the "Создать
    // сервер" screen's own default of 4 means the host plus 3 joiners, matching what the stepper
    // there actually reads as ("Макс. игроков"), not "3 more on top of the host".
    public NetworkHost(GameServer server, int port = Wire.DefaultPort, int maxPlayers = int.MaxValue)
    {
        _server = server;
        _maxPlayers = maxPlayers;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "net-accept" };
        _acceptThread.Start();
    }

    private void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client;
            try
            {
                client = _listener.AcceptTcpClient();
            }
            catch (Exception)
            {
                break; // listener stopped, or the socket died - either way there's nothing left to accept
            }

            try
            {
                if (_server.PlayerCount >= _maxPlayers)
                {
                    // Rejected before Connect ever runs - no player id is handed out and no seat is
                    // reserved, so a refused joiner costs the server nothing. Shutdown(Send) before
                    // disposing (rather than an abrupt close right after Write) makes the already-
                    // flushed Rejected frame's delivery a guarantee instead of a race against the
                    // socket tearing down - a real flake under the test suite's own parallel load
                    // (Coop_NetworkHost_RejectsJoinerPastMaxPlayers) traced back to exactly this.
                    var rejectStream = client.GetStream();
                    Wire.WriteFrame(rejectStream, new ServerMessage(ServerMessageKind.Rejected, Reason: "сервер заполнен"));
                    client.Client.Shutdown(SocketShutdown.Send);
                    rejectStream.Dispose();
                    client.Dispose();
                    continue;
                }

                var connection = new TcpServerConnection(client);
                // Connect first, Start second: the welcome frame carries the id Connect hands out.
                connection.Start(_server.Connect(connection));
            }
            catch (Exception)
            {
                // A join that fails mid-handshake costs the joiner a retry and the session nothing.
                client.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        try
        {
            _listener.Stop();
        }
        catch (Exception)
        {
        }
        _acceptThread.Join(TimeSpan.FromSeconds(1));
    }
}
