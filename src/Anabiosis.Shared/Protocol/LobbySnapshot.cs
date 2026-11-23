using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// Direct user request (screenshot 3 of the Barotrauma reference set) - a real pre-game waiting
// room. Sent instead of WorldSnapshot while GameServer's own SessionPhase is Lobby (there is no
// World/Character yet to describe), replaced entirely by ordinary WorldSnapshots the instant the
// host's LobbyStartRoundPressed command lands and the round actually begins.
//  - IsAfk: "away from keyboard" - takes no part in the round, and stays in the waiting room while it is on;
//  - IsSpectator: watches the round without a character of their own;
//  - Look / KitIndex: the chosen appearance and starting kit (the "Внешность" tab and the 1/2/3 sets).
public sealed record LobbyPlayerState(int PlayerId, string? Nickname, CrewRole? Role, bool IsHost, bool IsReady,
    bool IsAfk = false, bool IsSpectator = false, CharacterLook? Look = null, int KitIndex = 0);

// One line of the lobby's chat (the waiting room's own, before any World exists - the in-round chat is World.Chat.cs).
public sealed record LobbyChatLine(string Author, string Text);

// A player the host has put on the black list: kept by address, so a new nickname does not get them back in.
public sealed record LobbyBanEntry(string Id, string Nickname);

// A saved campaign the server has on disk (the "Загр. кампанию" tab). Id is the file's name inside the saves folder, never a path.
public sealed record LobbySaveInfo(string Id, string Title, string ShipName, int Credits, DateTime LastPlayedUtc);

// What the host sets before the round starts (the lobby's settings panel and the server settings window). Applied when the round is built.
//  - StartCredits: the crew's money at the start;
//  - GalacticWar: whether the Union/virus war runs on the galaxy map (a saved real game only);
//  - StartDocked: begin already mated to the home station instead of free beside it.
// Server tab: ServerName (empty keeps the name it was created with), Motd (the "сообщение дня" every player is shown on joining), OpenServer (announced
// on the local network).
// General tab: VoiceChat, AllowSpectate, AllowAfk, StartWhenReady (the round begins by itself once StartWhenReadyPercent of the players are ready).
// Campaign: the three multipliers (CampaignModifiers).
public sealed record LobbySettings(int StartCredits = 300, bool GalacticWar = true, bool StartDocked = false,
    string ServerName = "", string Motd = "", bool OpenServer = true,
    bool VoiceChat = true, bool AllowSpectate = true, bool AllowAfk = true, bool StartWhenReady = false, int StartWhenReadyPercent = 80,
    float RewardMultiplier = 1f, float ShopPriceMultiplier = 1f, float ShipyardPriceMultiplier = 1f)
{
    public const int MinStartCredits = 0;
    public const int MaxStartCredits = 5000;
    public const int StartCreditsStep = 100;
    public const int MaxNameLength = 40;
    public const int MaxMotdLength = 500;

    public CampaignModifiers Modifiers => new CampaignModifiers(RewardMultiplier, ShopPriceMultiplier, ShipyardPriceMultiplier).Clamped();

    // Whatever arrives from the wire is clamped here - the host's client is trusted for what it asks, not for what it can ask for.
    public LobbySettings Clamped()
    {
        var modifiers = Modifiers;
        return this with
        {
            StartCredits = Math.Clamp(StartCredits, MinStartCredits, MaxStartCredits),
            ServerName = Flatten(ServerName, MaxNameLength),
            Motd = (Motd ?? "").Trim().Length > MaxMotdLength ? Motd!.Trim()[..MaxMotdLength] : (Motd ?? "").Trim(),
            StartWhenReadyPercent = Math.Clamp(StartWhenReadyPercent, 10, 100),
            RewardMultiplier = modifiers.Reward,
            ShopPriceMultiplier = modifiers.ShopPrice,
            ShipyardPriceMultiplier = modifiers.ShipyardPrice,
        };
    }

    // One line, trimmed, no longer than `max`.
    private static string Flatten(string? text, int max)
    {
        var line = string.Join(' ', (text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.Length > max ? line[..max] : line;
    }
}

public sealed record LobbySnapshot(
    string ServerName,
    int MaxPlayers,
    IReadOnlyList<LobbyPlayerState> Players,
    ShipKind ShipKind,
    string? CustomShipName,
    // The waiting room's chat, oldest first (the last few dozen lines).
    IReadOnlyList<LobbyChatLine>? Chat = null,
    // The round settings as they stand now - everyone sees them, only the host changes them.
    LobbySettings? Settings = null,
    // Players the host has just removed: the server sends this once more before closing their connection, so the one removed can say why it ended.
    IReadOnlyList<int>? KickedPlayerIds = null,
    // The black list (the host's window shows it) and the campaigns that can be loaded (the host's "Загр. кампанию" tab).
    IReadOnlyList<LobbyBanEntry>? Bans = null,
    IReadOnlyList<LobbySaveInfo>? Saves = null,
    // Set when the host has chosen a saved campaign to continue instead of a new one: its title. The round is then built from that save.
    string? LoadedSaveTitle = null,
    // True once the round is on and this player is held in the waiting room (AFK): the lobby they see is the same one, the world goes on without them.
    bool RoundInProgress = false);
