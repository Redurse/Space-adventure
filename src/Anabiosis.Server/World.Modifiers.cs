using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// What the lobby's host set for the campaign (LobbySettings): the price and reward multipliers, whether the crew can talk. Applied once, when the round is
// built (GameServer.Lobby.cs); a world that never saw a lobby keeps the defaults - everything as designed.
public sealed partial class World
{
    public CampaignModifiers Modifiers { get; private set; } = CampaignModifiers.Default;

    // False when the host turned the voice chat off: voice chunks are dropped at the relay (World.Voice.cs).
    public bool VoiceChatEnabled { get; set; } = true;

    public void SetModifiers(CampaignModifiers modifiers) => Modifiers = modifiers.Clamped();
}
