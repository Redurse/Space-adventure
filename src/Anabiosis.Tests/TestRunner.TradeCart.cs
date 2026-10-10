using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The Trader's store window (Rendering/TradeWindow.cs): its shopping cart and the shared price formulas.
internal static partial class TestRunner
{
    private static bool TradeCart_TotalsUseTheListPrices()
    {
        var cart = new TradeCart();
        cart.AddBuy(ItemType.Wrench, 2);      // 20 each
        cart.AddBuy(ItemType.MedKit);         // 50
        cart.AddSell(ItemType.Screwdriver, owned: 3, 2); // 8 each
        return cart.BuyTotal == 90 && cart.SellTotal == 16 && cart.Net == -74 && cart.BuyCount == 3 && cart.SellCount == 2;
    }

    private static bool TradeCart_FactionMultiplierMovesBothSides()
    {
        var cart = new TradeCart { PriceMultiplier = 0.8f };
        // An ally's station: buying is cheaper (20 -> 16) and selling pays better (8 / 0.8 = 10).
        return cart.BuyPriceOf(ItemType.Wrench) == 16 && cart.SellPriceOf(ItemType.Wrench) == 10;
    }

    private static bool TradeCart_MiningStationPaysMoreForOreOnly()
    {
        var plain = new TradeCart();
        var mining = new TradeCart { MiningStation = true };
        return mining.SellPriceOf(ItemType.IronOre) > plain.SellPriceOf(ItemType.IronOre)
            && mining.SellPriceOf(ItemType.Wrench) == plain.SellPriceOf(ItemType.Wrench);
    }

    private static bool TradeCart_CannotSellMoreThanYouCarry_AndNeverGoesNegative()
    {
        var cart = new TradeCart();
        for (var i = 0; i < 5; i++)
            cart.AddSell(ItemType.Wrench, owned: 2);
        if (cart.Sell[ItemType.Wrench] != 2)
            return false;
        cart.RemoveSell(ItemType.Wrench, 10);
        if (cart.Sell.ContainsKey(ItemType.Wrench))
            return false;
        cart.AddSell(ItemType.Wrench, owned: 3, 3);
        cart.ClampSellTo(new Dictionary<ItemType, int> { [ItemType.Wrench] = 1 }); // two were moved away since
        return cart.Sell[ItemType.Wrench] == 1;
    }

    private static bool TradeCart_ProblemExplainsWhyTheDealCannotGoThrough()
    {
        var cart = new TradeCart();
        if (cart.Problem(credits: 100, freeSlots: 5) is null)
            return false; // empty cart
        cart.AddBuy(ItemType.Spacesuit); // 150
        if (cart.Problem(credits: 100, freeSlots: 5) is null)
            return false; // can't afford it
        if (cart.Problem(credits: 150, freeSlots: 5) is not null)
            return false; // exactly enough
        cart.AddSell(ItemType.Wrench, owned: 1); // +8 helps the credits ...
        if (cart.Problem(credits: 142, freeSlots: 5) is not null)
            return false;
        // ... and frees a slot: 1 purchase + 0 free slots only works because one item is sold.
        return cart.Problem(credits: 150, freeSlots: 0) is null
            && new TradeCart { }.Problem(100, 5) is not null
            && TradeCartWithTwoBuys().Problem(credits: 500, freeSlots: 1) is not null;
    }

    private static TradeCart TradeCartWithTwoBuys()
    {
        var cart = new TradeCart();
        cart.AddBuy(ItemType.Wrench, 2);
        return cart;
    }

    private static bool TradeCart_CommandsSellFirstFromRealSlots_ThenBuy()
    {
        var cart = new TradeCart();
        cart.AddBuy(ItemType.MedKit, 2);
        cart.AddSell(ItemType.Wrench, owned: 2, 2);
        var slots = new ItemType?[] { ItemType.Wrench, null, ItemType.Screwdriver, ItemType.Wrench };
        var commands = cart.BuildCommands(slots);
        return commands.Count == 4
            && commands[0] == (null, 0) && commands[1] == (null, 3)
            && commands[2] == (ItemType.MedKit, -1) && commands[3] == (ItemType.MedKit, -1);
    }

    private static bool TradeCatalog_OnlyRealStockIsForSale_AndEverythingFindsACategory()
    {
        if (TradeCatalog.IsForSale(TradeCatalog.Find(ItemType.IronOre)!) || !TradeCatalog.IsForSale(TradeCatalog.Find(ItemType.Wrench)!))
            return false;
        if (TradeWindow.CategoryOf(ItemType.Wrench) != TradeWindow.Category.Tools ||
            TradeWindow.CategoryOf(ItemType.Hyperium) != TradeWindow.Category.Supplies ||
            TradeWindow.CategoryOf(ItemType.GateAnd) != TradeWindow.Category.Electronics ||
            TradeWindow.CategoryOf(ItemType.IronOre) != TradeWindow.Category.Materials ||
            TradeWindow.CategoryOf(ItemType.Magazine) != TradeWindow.Category.Weapons)
            return false;
        return TradeCatalog.Goods.Where(TradeCatalog.IsForSale).Count() >= 20;
    }

    // The cart's own totals must be what the wallet really changes by - the window and the server share
    // TradeCatalog's formulas, this proves it end to end for a purchase and a sale.
    private static bool World_Trade_WalletChangesByExactlyWhatTheCartPromised()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var good = TradeCatalog.Find(ItemType.MedKit)!;
        var before = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.MedKit));
        world.Step(RealtimeStep);
        var bought = before - world.Credits;
        if (bought != TradeCatalog.BuyPrice(good, 1f))
            return false;

        var slot = Array.IndexOf(world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), (ItemType?)ItemType.MedKit);
        var beforeSell = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, SellSlotIndex: slot));
        world.Step(RealtimeStep);
        return world.Credits - beforeSell == TradeCatalog.SellPrice(good, 1f, miningStation: false);
    }
}
