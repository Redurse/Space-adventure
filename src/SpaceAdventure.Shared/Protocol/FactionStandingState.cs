using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// One faction's current standing with the crew (game_design.md section 12). Name travels with it
// so the HUD doesn't need its own copy of the faction name table.
public sealed record FactionStandingState(FactionId Faction, string Name, int Standing);
