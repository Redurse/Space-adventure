using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// What's left of mining once the cutting itself moved to a held flame (World.Cutting.cs): picking
// up what fell out of a block. [F] outside is now only ever "pick that up" - the ore is knocked
// loose by the torch, not by pressing a key at a marker.
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
        if (nearbyDropped is null)
            return;

        if (character.Inventory.TryAdd(nearbyDropped.Item))
            _droppedItems.Remove(nearbyDropped);
    }
}
