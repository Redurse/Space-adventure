using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    // Mouse-click counterpart to the inventory hold strip: click a reactor slot while standing
    // at the reactor to insert a rod held in hand, or click a loaded slot to take its rod back.
    private void ToggleReactorSlot(Character character, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= Reactor.RodSlotCount)
            return;

        if ((Ship.ReactorBlock.Position - character.Position).Length() >= InteractionRadius)
            return;

        var reactor = PowerGrid.Reactor;
        if (reactor.IsRodLoaded(slotIndex))
        {
            if (character.Inventory.TryAdd(ItemType.FuelRod))
                reactor.RemoveRod(slotIndex);
        }
        else if (character.Inventory.TryTakeHeldItem(ItemType.FuelRod))
        {
            // Rods come off the rack fresh, so this is what actually refuels the reactor.
            reactor.InsertRod(slotIndex);
        }
    }
}
