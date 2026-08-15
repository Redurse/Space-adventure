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

        var rodSlots = PowerGrid.Reactor.RodSlots;
        if (rodSlots[slotIndex])
        {
            if (character.Inventory.TryAdd(ItemType.FuelRod))
                rodSlots[slotIndex] = false;
        }
        else if (character.Inventory.TryTakeHeldItem(ItemType.FuelRod))
        {
            rodSlots[slotIndex] = true;
        }
    }
}
