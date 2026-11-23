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
        new TradeGood(ItemType.Bandage, BuyPrice: 12, SellPrice: 5),
        new TradeGood(ItemType.Tourniquet, BuyPrice: 18, SellPrice: 7),
        new TradeGood(ItemType.Antiseptic, BuyPrice: 15, SellPrice: 6),
        new TradeGood(ItemType.Splint, BuyPrice: 20, SellPrice: 8),
        new TradeGood(ItemType.PlasterCast, BuyPrice: 30, SellPrice: 12),
        new TradeGood(ItemType.Painkiller, BuyPrice: 25, SellPrice: 10),
        new TradeGood(ItemType.Antibiotic, BuyPrice: 35, SellPrice: 14),
        new TradeGood(ItemType.BurnGel, BuyPrice: 22, SellPrice: 9),
        new TradeGood(ItemType.Adrenaline, BuyPrice: 45, SellPrice: 18),
        new TradeGood(ItemType.Defibrillator, BuyPrice: 220, SellPrice: 90),
        new TradeGood(ItemType.SurgeryKit, BuyPrice: 180, SellPrice: 70),
        new TradeGood(ItemType.WireSpool, BuyPrice: 40, SellPrice: 15),
        // Mined, not bought - BuyPrice is nominal/uneconomical, this good exists so the existing
        // generic sell flow (World.Trade.cs) is what turns mined ore into credits at the station
        // (game_design.md Phase 3, M18), rather than a separate quest-turn-in mechanism.
        new TradeGood(ItemType.Mineral, BuyPrice: 999, SellPrice: 35),
        // Direct user request ("добавим множество предметов материалов") - mined ores (OreDeposit.
        // OreType, World.Cutting.cs), same "not really for sale" BuyPrice convention as Mineral above -
        // priced by rarity, roughest first.
        new TradeGood(ItemType.IronOre, BuyPrice: 999, SellPrice: 15),
        new TradeGood(ItemType.CopperOre, BuyPrice: 999, SellPrice: 18),
        new TradeGood(ItemType.AluminumOre, BuyPrice: 999, SellPrice: 18),
        new TradeGood(ItemType.Carbon, BuyPrice: 999, SellPrice: 16),
        new TradeGood(ItemType.ZincOre, BuyPrice: 999, SellPrice: 20),
        new TradeGood(ItemType.Silicon, BuyPrice: 999, SellPrice: 20),
        new TradeGood(ItemType.NickelOre, BuyPrice: 999, SellPrice: 22),
        new TradeGood(ItemType.TitaniumOre, BuyPrice: 999, SellPrice: 30),
        new TradeGood(ItemType.PlastalloyOre, BuyPrice: 999, SellPrice: 35),
        new TradeGood(ItemType.UraniumOre, BuyPrice: 999, SellPrice: 45),
        // Raw ore, as it comes off an asteroid: worth less than the material the fabricator makes of it.
        new TradeGood(ItemType.RawIron, BuyPrice: 999, SellPrice: 10),
        new TradeGood(ItemType.RawCopper, BuyPrice: 999, SellPrice: 12),
        new TradeGood(ItemType.RawAluminum, BuyPrice: 999, SellPrice: 12),
        new TradeGood(ItemType.RawCarbon, BuyPrice: 999, SellPrice: 11),
        new TradeGood(ItemType.RawZinc, BuyPrice: 999, SellPrice: 14),
        new TradeGood(ItemType.RawSilicon, BuyPrice: 999, SellPrice: 14),
        new TradeGood(ItemType.RawNickel, BuyPrice: 999, SellPrice: 15),
        new TradeGood(ItemType.RawTitanium, BuyPrice: 999, SellPrice: 20),
        new TradeGood(ItemType.RawPlastalloy, BuyPrice: 999, SellPrice: 24),
        new TradeGood(ItemType.RawUranium, BuyPrice: 999, SellPrice: 30),
        new TradeGood(ItemType.RawHyperium, BuyPrice: 999, SellPrice: 28),
        // Jump fuel (World.StarSystems.cs) - the one raw material a trader actually stocks, so a crew
        // that ran dry can still buy its way out of a system.
        new TradeGood(ItemType.Hyperium, BuyPrice: 80, SellPrice: 40),
        new TradeGood(ItemType.Magazine, BuyPrice: 30, SellPrice: 12),
        new TradeGood(ItemType.Revolver, BuyPrice: 140, SellPrice: 55),
        new TradeGood(ItemType.RevolverRounds, BuyPrice: 24, SellPrice: 9),
        new TradeGood(ItemType.BodyArmor, BuyPrice: 380, SellPrice: 150),
        new TradeGood(ItemType.BallisticHelmet, BuyPrice: 220, SellPrice: 90),
        // The rest of the guns (GunDefinitions.cs) and what they fire. The assault rifle is sold now too - its magazines always were.
        new TradeGood(ItemType.Rifle, BuyPrice: 420, SellPrice: 120),
        new TradeGood(ItemType.MachinePistol, BuyPrice: 180, SellPrice: 70),
        new TradeGood(ItemType.Smg, BuyPrice: 260, SellPrice: 100),
        new TradeGood(ItemType.SmgMagazine, BuyPrice: 26, SellPrice: 10),
        new TradeGood(ItemType.Handcannon, BuyPrice: 320, SellPrice: 125),
        new TradeGood(ItemType.HandcannonRounds, BuyPrice: 40, SellPrice: 15),
        new TradeGood(ItemType.Shotgun, BuyPrice: 300, SellPrice: 120),
        new TradeGood(ItemType.AutoShotgun, BuyPrice: 480, SellPrice: 190),
        new TradeGood(ItemType.ShotgunShells, BuyPrice: 30, SellPrice: 12),
        new TradeGood(ItemType.BoltRifle, BuyPrice: 360, SellPrice: 140),
        new TradeGood(ItemType.RifleRounds, BuyPrice: 36, SellPrice: 14),
        new TradeGood(ItemType.Hmg, BuyPrice: 620, SellPrice: 250),
        new TradeGood(ItemType.HmgMagazine, BuyPrice: 70, SellPrice: 28),
        new TradeGood(ItemType.HarpoonGun, BuyPrice: 200, SellPrice: 80),
        new TradeGood(ItemType.Harpoons, BuyPrice: 20, SellPrice: 8),
        new TradeGood(ItemType.RevolverRoundsDepleted, BuyPrice: 48, SellPrice: 18),
        new TradeGood(ItemType.SmgMagazineDepleted, BuyPrice: 52, SellPrice: 20),
        new TradeGood(ItemType.ShotgunShellsRubber, BuyPrice: 24, SellPrice: 9),
        new TradeGood(ItemType.ShotgunSlugs, BuyPrice: 70, SellPrice: 28),
        new TradeGood(ItemType.HarpoonsHeavy, BuyPrice: 46, SellPrice: 18),
        new TradeGood(ItemType.HarpoonsExplosive, BuyPrice: 80, SellPrice: 32),
        // FabricatorCatalog.cs's own recipe outputs - crafted, not bought (same convention as the
        // ores above), priced above the sum of their own ingredients so crafting-to-sell is a real,
        // modestly profitable activity rather than a wash.
        new TradeGood(ItemType.CopperCable, BuyPrice: 999, SellPrice: 45),
        new TradeGood(ItemType.SteelPlate, BuyPrice: 999, SellPrice: 60),
        new TradeGood(ItemType.Plastic, BuyPrice: 999, SellPrice: 65),
        new TradeGood(ItemType.Plastalloy, BuyPrice: 999, SellPrice: 110),
        new TradeGood(ItemType.TitaniumPlate, BuyPrice: 999, SellPrice: 150),
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
        // W7/W8 of wiring-plan.md: the signal-value parts.
        new TradeGood(ItemType.Button, BuyPrice: 20, SellPrice: 8),
        new TradeGood(ItemType.SignalCheck, BuyPrice: 45, SellPrice: 18),
        new TradeGood(ItemType.Delay, BuyPrice: 50, SellPrice: 20),
        new TradeGood(ItemType.Comparator, BuyPrice: 55, SellPrice: 22),
        new TradeGood(ItemType.FunctionPart, BuyPrice: 55, SellPrice: 22),
        new TradeGood(ItemType.Oscillator, BuyPrice: 60, SellPrice: 24),
        new TradeGood(ItemType.MathPart, BuyPrice: 65, SellPrice: 26),
        new TradeGood(ItemType.WirelessPart, BuyPrice: 80, SellPrice: 32),
        new TradeGood(ItemType.TerminalPart, BuyPrice: 60, SellPrice: 24),
        new TradeGood(ItemType.RegexPart, BuyPrice: 70, SellPrice: 28),
    };

    public static TradeGood? Find(ItemType item) => Goods.FirstOrDefault(g => g.Item == item);

    // The Miners' Guild pays a premium for ore at its own base - the one concrete thing that makes docking
    // there (rather than any other trader) worth the detour (game_design.md section 10, StationKind.Mining).
    public const float MiningStationOreSellBonus = 1.2f;

    // What the crew actually pays/gets at the docked station: the list price scaled by the faction price
    // multiplier (FactionDefinitions.PriceMultiplier - allies pay less, the disliked pay more; the sell side
    // inverts it). One place for the server (World.Trade.cs) and the trade window's cart totals, so what the
    // window promises is what the wallet changes by.
    public static int BuyPrice(TradeGood good, float priceMultiplier) =>
        (int)MathF.Round(good.BuyPrice * priceMultiplier);

    public static int SellPrice(TradeGood good, float priceMultiplier, bool miningStation) =>
        (int)MathF.Round(good.SellPrice / priceMultiplier *
            (ItemDefinitions.IsRawOre(good.Item) && miningStation ? MiningStationOreSellBonus : 1f));

    // Only things a trader really stocks are offered for purchase: the mined ores and crafted parts carry a
    // nominal 999 price just so the sell flow can price them, they are not meant to be bought back.
    public const int NotForSalePrice = 999;
    public static bool IsForSale(TradeGood good) => good.BuyPrice < NotForSalePrice;
}
