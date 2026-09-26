using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Networking;

// The server's end of one joined player's socket. Same two methods as InProcessTransport exposes to
// the server, so GameServer can't tell the difference between a local player and a remote one.
//
// Both directions run on their own thread so that neither can stall the simulation: the tick loop
// only ever touches a queue and a slot.
public sealed class TcpServerConnection : IServerConnection, IDisposable
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ConcurrentQueue<ClientCommand> _incoming = new();
    private readonly AutoResetEvent _outgoingReady = new(false);

    // Latest snapshot only, never a backlog. A client that can't keep up should fall behind in
    // *time* and then see the present, not replay a queue of stale worlds at the wrong speed -
    // exactly what IClientConnection.ReceiveLatestSnapshot already does at the other end.
    private WorldSnapshot? _outgoing;
    private LobbySnapshot? _outgoingLobby;

    private volatile bool _closed;

    public bool IsOpen => !_closed;

    public TcpServerConnection(TcpClient client)
    {
        _client = client;
        _client.NoDelay = true; // 30 small frames a second is exactly what Nagle would batch into lag
        _stream = client.GetStream();
    }

    // Separate from the constructor because the player id only exists once GameServer has accepted
    // the connection, and the welcome frame has to be the first thing on the wire.
    public void Start(int playerId)
    {
        Wire.WriteFrame(_stream, new ServerMessage(ServerMessageKind.Welcome, playerId));

        new Thread(ReadLoop) { IsBackground = true, Name = $"net-rx-{playerId}" }.Start();
        new Thread(WriteLoop) { IsBackground = true, Name = $"net-tx-{playerId}" }.Start();
    }

    void IServerConnection.Send(WorldSnapshot snapshot)
    {
        Volatile.Write(ref _outgoing, snapshot);
        if (!_closed)
            _outgoingReady.Set();
    }

    void IServerConnection.SendLobby(LobbySnapshot lobby)
    {
        Volatile.Write(ref _outgoingLobby, lobby);
        if (!_closed)
            _outgoingReady.Set();
    }

    IReadOnlyList<ClientCommand> IServerConnection.ReceiveCommands()
    {
        var commands = new List<ClientCommand>();
        while (_incoming.TryDequeue(out var command))
            commands.Add(command);
        return commands;
    }

    private void ReadLoop()
    {
        try
        {
            while (!_closed)
            {
                var command = Wire.ReadFrame<ClientCommand>(_stream);
                if (command is null)
                    break; // клиент вышел
                _incoming.Enqueue(command);
            }
        }
        catch (Exception)
        {
            // A dropped connection is ordinary in a game session, not an error to propagate: the
            // player pulled the cable, alt-F4'd, or the wifi blinked. Closing is the whole response.
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
                var snapshot = Interlocked.Exchange(ref _outgoing, null);
                var lobby = Interlocked.Exchange(ref _outgoingLobby, null);
                if (snapshot is null && lobby is null)
                    continue; // woken by Close, or both slots were already taken

                if (snapshot is not null)
                    Wire.WriteFrame(_stream, new ServerMessage(ServerMessageKind.Snapshot, 0, snapshot));
                if (lobby is not null)
                    Wire.WriteFrame(_stream, new ServerMessage(ServerMessageKind.Lobby, 0, Lobby: lobby));
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
        _outgoingReady.Set(); // let the writer notice and leave
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
