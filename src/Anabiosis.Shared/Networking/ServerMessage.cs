using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Networking;

public enum ServerMessageKind
{
    // Sent once, as the very first frame after a socket is accepted: which player the joiner is.
    // In-process the client learns that from GameServer.Connect's return value; over a socket
    // there's no return value to read, so it has to be said out loud.
    Welcome = 0,
    Snapshot = 1,

    // Sent instead of Welcome, then the socket is closed - the server's max-player cap
    // (Game1.Menu.cs's "Создать сервер" screen, "Макс. игроков") was already reached. Only the
    // TCP accept path (NetworkHost) can ever send this - the in-process transport a solo/hosting
    // player uses for their own character never goes through a cap at all, only joiners do.
    Rejected = 2,

    // GameServer's own SessionPhase is still Lobby - no World/Character exists yet, just a roster
    // (LobbySnapshot). Replaced by ordinary Snapshot frames forever once the host's own
    // LobbyStartRoundPressed command actually starts the round.
    Lobby = 3,
}

// Everything the server says to a client, in one envelope - a socket carries a byte stream, not a
// typed method call, so the reader needs to know what the next frame is before parsing it.
public sealed record ServerMessage(
    ServerMessageKind Kind,
    int PlayerId = 0,
    WorldSnapshot? Snapshot = null,
    string? Reason = null,
    LobbySnapshot? Lobby = null);
