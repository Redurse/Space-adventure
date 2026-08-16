namespace SpaceAdventure.Shared.Model;

// One entry in the trader's catalog (game_design.md sections 6, 10 — M10 economy). BuyPrice is
// what the crew pays to acquire it, SellPrice what the trader pays for one from the crew's
// inventory; SellPrice is always lower (no risk-free flip). Prices are flat for now — no
// per-station variation, no reputation discounts (later refinement, see continue.md M10 scope).
public sealed record TradeGood(ItemType Item, int BuyPrice, int SellPrice);
