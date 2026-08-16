namespace SpaceAdventure.Shared.Model;

// game_design.md section 10 — Кадровик (hires bots) is deliberately still not modeled, per the
// agreed Phase 1 scope cut.
public enum NpcKind
{
    Administrator,
    Trader,
    Mechanic,
    Shipwright, // sells hulls (game_design.md section 9) — only at stations that have a shipyard
    Security,   // patrols for thieves (game_design.md section 10) — nothing to talk about, only to avoid
}
