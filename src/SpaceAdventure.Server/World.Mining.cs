using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// What's left of mining once the cutting itself moved to a held flame (World.Cutting.cs): picking
// up what fell out of a block. [F] outside is "pick that up" whenever something's in reach - the
// ore is knocked loose by the torch, not by pressing a key at a marker - and otherwise toggles the
// suit's magnetic boots (World.Eva.cs's TryAutoAttach), so the same key both collects a drop right
// underfoot and, everywhere else, switches whether touching the hull/a rock grabs on.
public sealed partial class World
{
    private const float PickupRadius = 1.5f;

    private readonly List<DroppedItem> _droppedItems = new();
    private int _nextDroppedItemId = 1;

    private void HandleEvaInteract(Character character)
    {
        var worldPos = GetEvaWorldPosition(character);

        var nearbyDropped = _droppedItems
            .Where(d => (d.Position - worldPos).Length() < PickupRadius)
            .OrderBy(d => (d.Position - worldPos).Length())
            .FirstOrDefault();
        if (nearbyDropped is not null)
        {
            if (character.Inventory.TryAdd(nearbyDropped.Item))
                _droppedItems.Remove(nearbyDropped);
            return;
        }

        character.MagneticBootsOn = !character.MagneticBootsOn;
    }

    // Click-to-pick-up, additive alongside HandleEvaInteract's E-key path above: works anywhere a
    // DroppedItem can exist (EVA, ship interior, station interior - World.Storage.cs's TryDropItem
    // is what puts one on a ship/station floor in the first place). RoomId has to match first (null
    // for both sides means EVA) - ship-local and station-local coordinates can legitimately land on
    // the same numbers, so proximity alone isn't enough to prove it's the same physical item.
    private void TryPickupDroppedItem(Character character, string droppedItemId)
    {
        var dropped = _droppedItems.FirstOrDefault(d => d.Id == droppedItemId);
        if (dropped is null)
            return;

        var (position, roomId) = character.IsOutside
            ? (GetEvaWorldPosition(character), (string?)null)
            : (character.Position, character.RoomId);

        if (dropped.RoomId != roomId)
            return;
        if ((dropped.Position - position).Length() >= PickupRadius)
            return;

        if (character.Inventory.TryAdd(dropped.Item))
            _droppedItems.Remove(dropped);
    }
}
