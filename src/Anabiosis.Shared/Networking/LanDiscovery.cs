using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Anabiosis.Shared.Networking;

// Finding servers on the local network - the "server list" of the join screen. A host announces itself over UDP broadcast a couple of times a
// second (name, the TCP port to join, how many seats are taken, whether it is still in the lobby); a client that is looking just listens and keeps
// whoever has been heard from lately. Nothing here needs a central server, which is also why it reaches only the local network: a server across
// the internet is joined by typing its address.
//
// The announcement is a short UTF-8 text, "ANABIOSIS1\n<name>\n<port>\n<players>\n<max>\n<lobby 0/1>" - not the game's JSON wire format, so a stray
// packet from some other program is told apart by its first line and costs nothing.
// Id: one per running server, so the same server heard through several network cards (each announces from its own address) is one entry.
public sealed record LanServerInfo(string Id, string Name, string Address, int Port, int Players, int MaxPlayers, bool InLobby, DateTime LastSeenUtc);

public sealed record LanAnnouncement(string Name, int Port, int Players, int MaxPlayers, bool InLobby);

public static class LanDiscovery
{
    // Not an IANA-registered port; one above the game's own TCP port so the two are easy to remember together.
    public const int DiscoveryPort = 47625;
    public const string Magic = "ANABIOSIS1";
    // How long a server stays on the list after its last announcement.
    public static readonly TimeSpan ServerTimeout = TimeSpan.FromSeconds(6);
    public static readonly TimeSpan AnnounceInterval = TimeSpan.FromSeconds(1.5);

    public static byte[] Encode(LanAnnouncement announcement, string id)
    {
        // The name is one line: anything that would break the framing is flattened.
        var name = announcement.Name.Replace('\n', ' ').Replace('\r', ' ').Trim();
        if (name.Length > 40)
            name = name[..40];
        return Encoding.UTF8.GetBytes(string.Join('\n', Magic, id, name, announcement.Port, announcement.Players, announcement.MaxPlayers, announcement.InLobby ? 1 : 0));
    }

    public static bool TryDecode(ReadOnlySpan<byte> data, string senderAddress, DateTime nowUtc, out LanServerInfo info)
    {
        info = null!;
        if (data.Length < Magic.Length || data.Length > 512)
            return false;
        var lines = Encoding.UTF8.GetString(data).Split('\n');
        if (lines.Length != 7 || lines[0] != Magic || lines[1].Length is 0 or > 32)
            return false;
        if (!int.TryParse(lines[3], out var port) || port is < 1 or > 65535
            || !int.TryParse(lines[4], out var players) || players < 0
            || !int.TryParse(lines[5], out var max) || max < 1)
            return false;
        info = new LanServerInfo(lines[1], lines[2], senderAddress, port, players, max, lines[6] == "1", nowUtc);
        return true;
    }

    // Where an announcement is sent: the limited broadcast, the directed broadcast of every network card (the limited one only leaves through the
    // default card), and this machine itself - so a game opened on the same computer sees the server too.
    public static IReadOnlyList<IPAddress> BroadcastTargets()
    {
        var targets = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;
                foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || unicast.IPv4Mask is null)
                        continue;
                    var address = unicast.Address.GetAddressBytes();
                    var mask = unicast.IPv4Mask.GetAddressBytes();
                    var directed = new byte[4];
                    for (var i = 0; i < 4; i++)
                        directed[i] = (byte)(address[i] | ~mask[i]);
                    var target = new IPAddress(directed);
                    if (!targets.Contains(target))
                        targets.Add(target);
                }
            }
        }
        catch (Exception)
        {
            // A machine whose network cards cannot be listed still announces on the limited broadcast.
        }
        return targets;
    }
}

// The host's side: every AnnounceInterval sends what `source` says right now (so the player count is always live) to every target.
public sealed class LanAnnouncer : IDisposable
{
    private readonly Func<LanAnnouncement?> _source;
    private readonly int _discoveryPort;
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];
    private readonly IReadOnlyList<IPAddress> _targets;
    private readonly CancellationTokenSource _cts = new();
    private readonly Thread _thread;

    // `targets` is only given by tests, which send straight to the loopback instead of onto the network.
    public LanAnnouncer(Func<LanAnnouncement?> source, int discoveryPort = LanDiscovery.DiscoveryPort, IReadOnlyList<IPAddress>? targets = null)
    {
        _source = source;
        _discoveryPort = discoveryPort;
        _targets = targets ?? LanDiscovery.BroadcastTargets();
        _thread = new Thread(Run) { IsBackground = true, Name = "lan-announce" };
        _thread.Start();
    }

    private void Run()
    {
        using var socket = new UdpClient { EnableBroadcast = true };
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // A source that has nothing to announce (the server is closed to the network for now) stays silent.
                if (_source() is not { } announcement)
                {
                    _cts.Token.WaitHandle.WaitOne(LanDiscovery.AnnounceInterval);
                    continue;
                }
                var payload = LanDiscovery.Encode(announcement, _id);
                foreach (var target in _targets)
                {
                    try
                    {
                        socket.Send(payload, payload.Length, new IPEndPoint(target, _discoveryPort));
                    }
                    catch (SocketException)
                    {
                        // One card that cannot send (cable pulled, no route) does not stop the others.
                    }
                }
            }
            catch (Exception)
            {
                // The announcement is a convenience - whatever goes wrong here must never take the game down.
            }
            _cts.Token.WaitHandle.WaitOne(LanDiscovery.AnnounceInterval);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _thread.Join(TimeSpan.FromSeconds(1));
        _cts.Dispose();
    }
}

// The client's side: listens for announcements and keeps the ones heard from lately. Several game windows on one computer can listen together
// (the port is shared), which is how a host and a joiner are tried out on a single machine.
public sealed class LanBrowser : IDisposable
{
    private readonly object _lock = new();
    private readonly Dictionary<string, LanServerInfo> _servers = new();
    private readonly UdpClient? _socket;
    private readonly Thread? _thread;
    private volatile bool _running = true;

    public LanBrowser(int discoveryPort = LanDiscovery.DiscoveryPort)
    {
        try
        {
            _socket = new UdpClient();
            _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _socket.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            _thread = new Thread(Listen) { IsBackground = true, Name = "lan-browse" };
            _thread.Start();
        }
        catch (SocketException)
        {
            // The port is taken by something that will not share it: the list stays empty, typing an address still works.
            _socket?.Dispose();
            _socket = null;
        }
    }

    private void Listen()
    {
        var remote = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                var data = _socket!.Receive(ref remote);
                var address = IPAddress.IsLoopback(remote.Address) ? "127.0.0.1" : remote.Address.ToString();
                if (!LanDiscovery.TryDecode(data, address, DateTime.UtcNow, out var info))
                    continue;
                lock (_lock)
                {
                    // The same server heard through another network card keeps the address it was first seen at (any of them reaches it) - one line, not three.
                    if (_servers.TryGetValue(info.Id, out var known) && DateTime.UtcNow - known.LastSeenUtc < LanDiscovery.ServerTimeout)
                        info = info with { Address = known.Address };
                    _servers[info.Id] = info;
                }
            }
            catch (Exception)
            {
                if (!_running)
                    break; // closed while waiting
            }
        }
    }

    // The servers heard from within ServerTimeout, in a steady order (name, then address) so the list does not shuffle under the cursor.
    public IReadOnlyList<LanServerInfo> Servers
    {
        get
        {
            var now = DateTime.UtcNow;
            lock (_lock)
            {
                foreach (var stale in _servers.Where(s => now - s.Value.LastSeenUtc > LanDiscovery.ServerTimeout).Select(s => s.Key).ToList())
                    _servers.Remove(stale);
                return _servers.Values.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(s => s.Address).ThenBy(s => s.Port).ToList();
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        try
        {
            _socket?.Close();
        }
        catch (Exception)
        {
        }
        _thread?.Join(TimeSpan.FromSeconds(1));
    }
}
