namespace SpaceAdventure.Shared.Model;

// The powers that own stations and send out raiders (game_design.md section 12, Phase 3 -
// "фракции и репутация: несколько фракций, динамические отношения, влияют на цены/задания").
public enum FactionId
{
    Independent, // unaligned outposts - never take sides, always neutral
    Consortium,  // trade-focused; owns the wealthier stations
    FreeFleet,   // ex-military privateers; the ones you usually end up fighting
    MinersGuild, // ore economy, not a side in the Consortium/FreeFleet war - has its own standing,
                 // but (like Independent) no Rival, so nothing ripples to it from that fight
}
