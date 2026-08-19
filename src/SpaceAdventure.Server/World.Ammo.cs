using System.Linq;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Ammo crates used to be an unlimited pickup (AmmoStorage.cs's own doc comment flagged this as a
// placeholder "for M6"). Each storage now holds a finite number of crates, drawn down one at a
// time by HandleInteract's pickup branch and topped back up wherever the rest of the ship's
// consumables already get restocked - a fresh hull/swap (World.ShipPurchase.cs's
// InitializeShipState) and every station visit (World.Voyage.cs's EnterStation), the same two
// places oxygen/hull/fuel already reset from.
public sealed partial class World
{
    public const int AmmoStorageCapacity = 5;

    private readonly Dictionary<string, int> _ammoStorageStock = new();

    private void RestockAmmoStorages()
    {
        foreach (var storage in Ship.AmmoStorages)
            _ammoStorageStock[storage.Id] = AmmoStorageCapacity;
    }

    private int AmmoStorageStockOf(string storageId) =>
        _ammoStorageStock.GetValueOrDefault(storageId, AmmoStorageCapacity);

    // Only actually spent once the crate lands in the character's inventory - a full inventory
    // (TryAdd returning false) still "reaches" the storage but takes nothing from it.
    private bool TryTakeAmmoCrate(Character character, AmmoStorage storage)
    {
        if (AmmoStorageStockOf(storage.Id) <= 0)
            return true; // handled (nothing to pick up), same as reaching an empty crate

        if (!character.Inventory.TryAdd(ItemType.AmmoCrate))
            return true;

        _ammoStorageStock[storage.Id] = AmmoStorageStockOf(storage.Id) - 1;
        return true;
    }

    private IReadOnlyList<AmmoStorageState> CreateAmmoStorageStates() =>
        Ship.AmmoStorages.Select(s => new AmmoStorageState(s.Id, AmmoStorageStockOf(s.Id), AmmoStorageCapacity)).ToArray();
}
