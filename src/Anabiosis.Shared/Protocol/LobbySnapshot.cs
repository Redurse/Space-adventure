using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// Direct user request (screenshot 3 of the Barotrauma reference set) - a real pre-game waiting
// room. Sent instead of WorldSnapshot while GameServer's own SessionPhase is Lobby (there is no
// World/Character yet to describe), replaced entirely by ordinary WorldSnapshots the instant the
// host's LobbyStartRoundPressed command lands and the round actually begins.
public sealed record LobbyPlayerState(int PlayerId, string? Nickname, CrewRole? Role, bool IsHost, bool IsReady);

public sealed record LobbySnapshot(
    string ServerName,
    int MaxPlayers,
    IReadOnlyList<LobbyPlayerState> Players,
    ShipKind ShipKind,
    string? CustomShipName);
