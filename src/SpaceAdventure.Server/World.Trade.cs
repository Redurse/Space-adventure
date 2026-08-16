using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Trading with the station's Trader NPC (game_design.md sections 6, 10 — M10 economy). One
// shared crew wallet, fixed catalog/prices everywhere (TradeCatalog) — no reputation discounts,
// no per-station variation yet, both deliberately deferred. Only usable while actually docked
// (VoyagePhase.Station), same gate as the airlock console that gets you here in the first place.
public sealed partial class World
{
    private const int StartingCredits = 300;

    public int Credits { get; private set; } = StartingCredits;

    // Charges the crew wallet and adds the item to the character's inventory. No-ops (no credits
    // spent) if the crew can't afford it, the inventory row is full, or the ship isn't docked.
    private void TryBuyItem(Character character, ItemType item)
    {
        if (Phase != VoyagePhase.Station)
            return;

        // Reputation with whoever owns this station scales the price (game_design.md section 12,
        // World.Factions.cs) - allies get a discount, disliked crews pay a markup.
        if (TradeCatalog.Find(item) is not { } good)
            return;
        var price = (int)MathF.Round(good.BuyPrice * LocalPriceMultiplier);
        if (Credits < price)
            return;

        if (!character.Inventory.TryAdd(item))
            return; // row full — no charge

        Credits -= price;
    }

    // Sells whatever's in the given slot (if anything, and if the trader will take it) back to
    // the crew wallet. No-ops if the ship isn't docked, the slot is empty, or holds an item the
    // trader doesn't buy (e.g. AmmoCrate is never actually reachable this way, but personal
    // weapons like Rifle/Knife are — they're just not in the catalog).
    private void TrySellItem(Character character, int slotIndex)
    {
        if (Phase != VoyagePhase.Station)
            return;

        if (character.Inventory.ItemAt(slotIndex) is not { } item)
            return;

        if (TradeCatalog.Find(item) is not { } good)
            return;

        if (!character.Inventory.TryRemoveAt(slotIndex))
            return;

        // Sell prices move the same way as buy prices - an ally's station pays out better, a
        // hostile one lowballs you (the multiplier is inverted here, since a low multiplier means
        // "cheap for the player" on the buy side but "stingy" on the sell side).
        Credits += (int)MathF.Round(good.SellPrice / LocalPriceMultiplier);
    }
}
