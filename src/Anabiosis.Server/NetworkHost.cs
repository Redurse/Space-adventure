using System.Net;
using System.Net.Sockets;
using Anabiosis.Shared.Networking;

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
    private volatile bool _running = true;

    public int Port { get; }

    public NetworkHost(GameServer server, int port = Wire.DefaultPort)
    {
        _server = server;
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
