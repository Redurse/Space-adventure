namespace SpaceAdventure.Shared.Model;

// Fixed catalog offered by every station's Trader (game_design.md section 10) — same list and
// prices everywhere for now, M10 scope. Only covers Phase 1 gear that actually does something;
// personal weapons (Knife/Rifle/LaserRifle) have no gameplay effect yet (see ItemDefinitions) so
// they're deliberately left out of the shop until Phase 3 boarding gives them a purpose.
public static class TradeCatalog
{
    public static readonly IReadOnlyList<TradeGood> Goods = new[]
    {
        new TradeGood(ItemType.Wrench, BuyPrice: 20, SellPrice: 8),
        new TradeGood(ItemType.Screwdriver, BuyPrice: 20, SellPrice: 8),
        new TradeGood(ItemType.WeldingTool, BuyPrice: 50, SellPrice: 20),
        new TradeGood(ItemType.Cutter, BuyPrice: 30, SellPrice: 12),
        new TradeGood(ItemType.Spacesuit, BuyPrice: 150, SellPrice: 60),
        new TradeGood(ItemType.AmmoCrate, BuyPrice: 40, SellPrice: 15),
        new TradeGood(ItemType.FuelRod, BuyPrice: 60, SellPrice: 25),
        new TradeGood(ItemType.MedKit, BuyPrice: 50, SellPrice: 20),
        new TradeGood(ItemType.WireSpool, BuyPrice: 40, SellPrice: 15),
        // Mined, not bought - BuyPrice is nominal/uneconomical, this good exists so the existing
        // generic sell flow (World.Trade.cs) is what turns mined ore into credits at the station
        // (game_design.md Phase 3, M18), rather than a separate quest-turn-in mechanism.
        new TradeGood(ItemType.Mineral, BuyPrice: 999, SellPrice: 35),
    };

    public static TradeGood? Find(ItemType item) => Goods.FirstOrDefault(g => g.Item == item);
}
