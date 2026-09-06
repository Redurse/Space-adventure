using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Networking;

// The joining player's end of the socket - the mirror image of TcpServerConnection, and from
// GameClient's point of view indistinguishable from the in-process transport it replaces.
//
// Commands are queued, not dropped: unlike snapshots (where only the newest matters) a command can
// carry an edge-triggered event - a door click, a shot, a purchase - that exists in exactly one
// frame and would simply never happen if it were overwritten.
public sealed class TcpClientConnection : IClientConnection, IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ConcurrentQueue<ClientCommand> _outgoing = new();
    private readonly AutoResetEvent _outgoingReady = new(false);
    private WorldSnapshot? _latest;
    private volatile bool _closed;

    public int PlayerId { get; }
    public bool IsOpen => !_closed;

    private TcpClientConnection(TcpClient client, NetworkStream stream, int playerId)
    {
        _client = client;
        _stream = stream;
        PlayerId = playerId;

        new Thread(ReadLoop) { IsBackground = true, Name = "net-rx" }.Start();
        new Thread(WriteLoop) { IsBackground = true, Name = "net-tx" }.Start();
    }

    // Blocks until the handshake completes, so the caller comes back either connected and knowing
    // its player id, or with an exception to show the player - no half-joined state to render.
    public static TcpClientConnection Join(string host, int port, TimeSpan timeout)
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            if (!client.ConnectAsync(host, port).Wait(timeout))
                throw new TimeoutException($"нет ответа от {host}:{port}");

            var stream = client.GetStream();
            stream.ReadTimeout = (int)timeout.TotalMilliseconds;
            var welcome = Wire.ReadFrame<ServerMessage>(stream)
                ?? throw new IOException("сервер закрыл соединение до приветствия");
            if (welcome.Kind != ServerMessageKind.Welcome)
                throw new IOException($"вместо приветствия пришло {welcome.Kind}");
            stream.ReadTimeout = Timeout.Infinite; // a quiet server is a paused one, not a dead one

            return new TcpClientConnection(client, stream, welcome.PlayerId);
        }
        catch (Exception)
        {
            client.Dispose();
            throw;
        }
    }

    void IClientConnection.Send(ClientCommand command)
    {
        if (_closed)
            return;
        _outgoing.Enqueue(command);
        _outgoingReady.Set();
    }

    WorldSnapshot? IClientConnection.ReceiveLatestSnapshot() => Interlocked.Exchange(ref _latest, null);

    private void ReadLoop()
    {
        try
        {
            while (!_closed)
            {
                var message = Wire.ReadFrame<ServerMessage>(_stream);
                if (message is null)
                    break;
                if (message.Kind == ServerMessageKind.Snapshot && message.Snapshot is { } snapshot)
                    Volatile.Write(ref _latest, snapshot);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            Close();
        }
    }

    private void WriteLoop()
    {
        try
        {
            while (!_closed)
            {
                _outgoingReady.WaitOne();
                while (_outgoing.TryDequeue(out var command))
                    Wire.WriteFrame(_stream, command);
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            Close();
        }
    }

    private void Close()
    {
        if (_closed)
            return;
        _closed = true;
        _outgoingReady.Set();
        try
        {
            _client.Close();
        }
        catch (Exception)
        {
        }
    }

    public void Dispose() => Close();
}
