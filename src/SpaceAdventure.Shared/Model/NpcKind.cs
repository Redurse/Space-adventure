namespace SpaceAdventure.Shared.Model;

public enum NpcKind
{
    Administrator,
    Trader,
    Mechanic,
    Shipwright,  // sells hulls (game_design.md section 9) — only at stations that have a shipyard
    Security,    // patrols for thieves (game_design.md section 10) — nothing to talk about, only to avoid
    Recruiter,   // offers bot crew for hire (game_design.md section 10, World.Recruiting.cs)
}
