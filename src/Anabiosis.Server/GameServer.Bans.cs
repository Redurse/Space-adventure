using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The black list: players the host removed for good, kept by network address (a new nickname does not get them back in) and, on a server that keeps files, in
// banlist.txt next to its save so the list survives a restart. The accept thread asks IsBanned before a socket gets a seat.
public sealed partial class GameServer
{
    private readonly List<(string Address, string Nickname)> _bans = new();
    private readonly object _banLock = new();
    // Network joiners only: the host's own in-process connection has no address and cannot be banned.
    private readonly Dictionary<int, string> _addresses = new();

    private string? BanFilePath => _savePath is null ? null : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_savePath)) ?? ".", "banlist.txt");

    // Thread-safe: NetworkHost calls this from its accept thread.
    public bool IsBanned(string address)
    {
        lock (_banLock)
            return _bans.Any(b => b.Address == address);
    }

    // Like Connect(connection), remembering where the player came from so the host can ban them later.
    public int Connect(IServerConnection connection, string? address)
    {
        var id = Connect(connection);
        if (address is not null)
            lock (_banLock)
                _addresses[id] = address;
        return id;
    }

    private bool BanFromLobby(int playerId)
    {
        string? address;
        lock (_banLock)
            _addresses.TryGetValue(playerId, out address);
        if (address is null || !_lobbyRoster.TryGetValue(playerId, out var entry))
            return false;
        lock (_banLock)
        {
            if (_bans.All(b => b.Address != address))
                _bans.Add((address, entry.Nickname ?? "игрок"));
        }
        SaveBans();
        return KickFromLobby(playerId);
    }

    private void Unban(string address)
    {
        lock (_banLock)
        {
            if (_bans.RemoveAll(b => b.Address == address) == 0)
                return;
        }
        SaveBans();
    }

    // Test and console access.
    public IReadOnlyList<string> BannedAddresses
    {
        get
        {
            lock (_banLock)
                return _bans.Select(b => b.Address).ToList();
        }
    }

    private IReadOnlyList<LobbyBanEntry>? BanList()
    {
        lock (_banLock)
            return _bans.Count == 0 ? null : _bans.Select(b => new LobbyBanEntry(b.Address, b.Nickname)).ToList();
    }

    private void LoadBans()
    {
        try
        {
            if (BanFilePath is not { } path || !File.Exists(path))
                return;
            foreach (var line in File.ReadAllLines(path))
            {
                var parts = line.Split('|', 2);
                if (parts[0].Length > 0)
                    _bans.Add((parts[0], parts.Length > 1 ? parts[1] : "игрок"));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // No list is no bans; a server must still start.
        }
    }

    private void SaveBans()
    {
        try
        {
            if (BanFilePath is not { } path)
                return;
            string[] lines;
            lock (_banLock)
                lines = _bans.Select(b => b.Address + "|" + b.Nickname.Replace('|', ' ').Replace('\n', ' ')).ToArray();
            File.WriteAllLines(path, lines);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A list that cannot be written is kept in memory for this run.
        }
    }
}
