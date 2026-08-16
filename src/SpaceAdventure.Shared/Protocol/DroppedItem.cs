using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// A physical item lying at a fixed world position, not inside anyone's inventory (game_design.md
// Phase 3, M18) - spawned by mining, picked up by proximity. The first "free-floating" item this
// project has; everything before this was either a fixed ToolStation or already in an inventory.
public sealed record DroppedItem(string Id, ItemType Item, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
