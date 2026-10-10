using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// The trade window's shopping cart (direct user request: the Trader's menu "как в Баротравме"): what you
// are about to buy and sell, with totals and a check that the whole deal can go through, before a single
// credit moves. Pure logic with no drawing, so it is tested on its own. Confirming turns the cart into
// the same one-item BuyItemType / SellSlotIndex commands the server already understands (World.Trade.cs),
// sells first so the money and the room they free are there for the purchases.
public sealed class TradeCart
{
    // How many of each item. Insertion-ordered so the cart lists things in the order they were added.
    public Dictionary<ItemType, int> Buy { get; } = new();
    public Dictionary<ItemType, int> Sell { get; } = new();

    // The deal's prices at the docked station (faction multiplier, mining-station ore bonus) - set by the
    // window every frame from the snapshot.
    public float PriceMultiplier { get; set; } = 1f;
    public bool MiningStation { get; set; }

    public int BuyCount => Buy.Values.Sum();
    public int SellCount => Sell.Values.Sum();
    public bool IsEmpty => Buy.Count == 0 && Sell.Count == 0;

    public int BuyPriceOf(ItemType item) =>
        TradeCatalog.Find(item) is { } good ? TradeCatalog.BuyPrice(good, PriceMultiplier) : 0;

    public int SellPriceOf(ItemType item) =>
        TradeCatalog.Find(item) is { } good ? TradeCatalog.SellPrice(good, PriceMultiplier, MiningStation) : 0;

    public int BuyTotal => Buy.Sum(kv => kv.Value * BuyPriceOf(kv.Key));
    public int SellTotal => Sell.Sum(kv => kv.Value * SellPriceOf(kv.Key));

    // Change in credits if the deal goes through (negative = you pay).
    public int Net => SellTotal - BuyTotal;

    public void AddBuy(ItemType item, int amount = 1) => Change(Buy, item, amount, int.MaxValue);

    // owned: how many of it the character carries - you cannot put more in the sell column than that.
    public void AddSell(ItemType item, int owned, int amount = 1) => Change(Sell, item, amount, owned);

    public void RemoveBuy(ItemType item, int amount = 1) => Change(Buy, item, -amount, int.MaxValue);
    public void RemoveSell(ItemType item, int amount = 1) => Change(Sell, item, -amount, int.MaxValue);

    public void Clear()
    {
        Buy.Clear();
        Sell.Clear();
    }

    // Drops sell lines that no longer match what is really in the bag (something was moved or sold since
    // the line was added) so the cart can never promise more than the character holds.
    public void ClampSellTo(IReadOnlyDictionary<ItemType, int> owned)
    {
        foreach (var item in Sell.Keys.ToList())
        {
            var have = owned.GetValueOrDefault(item);
            if (have <= 0)
                Sell.Remove(item);
            else if (Sell[item] > have)
                Sell[item] = have;
        }
    }

    // Why the deal cannot be done, or null if it can: not enough credits (after what you sell), or not
    // enough room in the bag for everything you buy (what you sell frees room, since the server clears
    // each sold slot before the purchases arrive).
    public string? Problem(int credits, int freeSlots)
    {
        if (IsEmpty)
            return "Корзина пуста";
        if (credits + Net < 0)
            return "Не хватает кредитов";
        if (BuyCount > freeSlots + SellCount)
            return "Не хватит места в инвентаре";
        return null;
    }

    // The commands that carry the deal out, in the order to send them: each sale names a concrete slot
    // holding that item (the first ones found), then each purchase. A sale line that no longer has enough
    // matching slots simply sells what is there.
    public List<(ItemType? Buy, int SellSlot)> BuildCommands(IReadOnlyList<ItemType?> mainSlots)
    {
        var commands = new List<(ItemType?, int)>();
        foreach (var (item, count) in Sell)
        {
            var left = count;
            for (var slot = 0; slot < mainSlots.Count && left > 0; slot++)
                if (mainSlots[slot] == item)
                {
                    commands.Add((null, slot));
                    left--;
                }
        }
        foreach (var (item, count) in Buy)
            for (var i = 0; i < count; i++)
                commands.Add((item, -1));
        return commands;
    }

    private static void Change(Dictionary<ItemType, int> lines, ItemType item, int amount, int max)
    {
        var next = Math.Clamp(lines.GetValueOrDefault(item) + amount, 0, max);
        if (next == 0)
            lines.Remove(item);
        else
            lines[item] = next;
    }
}
