namespace Anabiosis.Shared.Model;

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
        // Replacement tanks for the two tools right above - bought fully charged
        // (Inventory.TryAdd already fills any TankSockets.IsTank item to FullChargeOf on pickup).
        new TradeGood(ItemType.WeldingTank, BuyPrice: 35, SellPrice: 14),
        new TradeGood(ItemType.OxygenTank, BuyPrice: 35, SellPrice: 14),
        new TradeGood(ItemType.Spacesuit, BuyPrice: 150, SellPrice: 60),
        new TradeGood(ItemType.AmmoCrate, BuyPrice: 40, SellPrice: 15),
        new TradeGood(ItemType.FuelRod, BuyPrice: 60, SellPrice: 25),
        new TradeGood(ItemType.MedKit, BuyPrice: 50, SellPrice: 20),
        new TradeGood(ItemType.WireSpool, BuyPrice: 40, SellPrice: 15),
        // Mined, not bought - BuyPrice is nominal/uneconomical, this good exists so the existing
        // generic sell flow (World.Trade.cs) is what turns mined ore into credits at the station
        // (game_design.md Phase 3, M18), rather than a separate quest-turn-in mechanism.
        new TradeGood(ItemType.Mineral, BuyPrice: 999, SellPrice: 35),
        // Purchasable wiring components (ComponentKind, World.ComponentMounts.cs, M23) - priced by
        // complexity, all comfortably under Spacesuit's 150 (small modular parts, not major gear).
        new TradeGood(ItemType.GateNot, BuyPrice: 25, SellPrice: 10),
        new TradeGood(ItemType.Relay, BuyPrice: 30, SellPrice: 12),
        new TradeGood(ItemType.LightToggle, BuyPrice: 25, SellPrice: 10),
        new TradeGood(ItemType.GateAnd, BuyPrice: 35, SellPrice: 15),
        new TradeGood(ItemType.GateOr, BuyPrice: 35, SellPrice: 15),
        new TradeGood(ItemType.GateXor, BuyPrice: 35, SellPrice: 15),
        new TradeGood(ItemType.AlarmKlaxon, BuyPrice: 40, SellPrice: 16),
        new TradeGood(ItemType.OxygenSensor, BuyPrice: 45, SellPrice: 18),
        new TradeGood(ItemType.BreachSensor, BuyPrice: 45, SellPrice: 18),
        new TradeGood(ItemType.PowerLossSensor, BuyPrice: 45, SellPrice: 18),
        new TradeGood(ItemType.MotionSensor, BuyPrice: 55, SellPrice: 22),
        new TradeGood(ItemType.AutoDoorController, BuyPrice: 60, SellPrice: 24),
        new TradeGood(ItemType.Timer, BuyPrice: 70, SellPrice: 28),
        new TradeGood(ItemType.Memory, BuyPrice: 90, SellPrice: 35),
    };

    public static TradeGood? Find(ItemType item) => Goods.FirstOrDefault(g => g.Item == item);
}
