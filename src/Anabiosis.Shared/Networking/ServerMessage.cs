using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Networking;

public enum ServerMessageKind
{
    // Sent once, as the very first frame after a socket is accepted: which player the joiner is.
    // In-process the client learns that from GameServer.Connect's return value; over a socket
    // there's no return value to read, so it has to be said out loud.
    Welcome = 0,
    Snapshot = 1,
}

// Everything the server says to a client, in one envelope - a socket carries a byte stream, not a
// typed method call, so the reader needs to know what the next frame is before parsing it.
public sealed record ServerMessage(
    ServerMessageKind Kind,
    int PlayerId = 0,
    WorldSnapshot? Snapshot = null);
