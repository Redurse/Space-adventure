using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// A physical item lying at a fixed world position, not inside anyone's inventory (game_design.md
// Phase 3, M18) - spawned by mining, picked up by proximity, or (World.Storage.cs) dropped out of
// an inventory slot. The first "free-floating" item this project has; everything before this was
// either a fixed ToolStation or already in an inventory.
//
// RoomId is null for the original EVA-space case (X/Y are ship-local exterior coordinates, drawn by
// FieldRenderer); non-null means "lying on the floor of this ship or station room" (X/Y are that
// hull's ordinary room-space coordinates, drawn by ShipRenderer/StationRenderer instead). Which of
// Ship.Rooms/Station.Rooms the id resolves against is only ever the one the viewing character is
// currently in - the two never need disambiguating beyond that.
public sealed record DroppedItem(string Id, ItemType Item, float X, float Y, string? RoomId = null)
{
    public Vec2 Position => new(X, Y);
}
