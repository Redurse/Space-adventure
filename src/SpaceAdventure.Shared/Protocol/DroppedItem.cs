using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// A physical item lying at a fixed world position, not inside anyone's inventory (game_design.md
// Phase 3, M18) - spawned by mining, picked up by proximity, or (World.Storage.cs) dropped out of
// an inventory slot. The first "free-floating" item this project has; everything before this was
// either a fixed ToolStation or already in an inventory.
//
// RoomId is null for the original EVA-space case (X/Y are ABSOLUTE field-space coordinates, same
// frame World.Eva.cs's GetEvaWorldPosition returns, drawn by FieldRenderer); non-null means "lying
// on the floor of this ship or station room" (X/Y are that hull's ordinary room-space coordinates,
// small numbers, drawn by ShipRenderer/StationRenderer instead). Which of Ship.Rooms/Station.Rooms
// the id resolves against is only ever the one the viewing character is currently in - the two
// never need disambiguating beyond that.
// X/Y are double, not float (M58 follow-up - same fix as Asteroid/OreDeposit/CharacterState's own
// doc comments): the EVA-space case is field-absolute at KSP-real scale (hundreds of billions of
// units), where a float32 can't resolve two points closer than tens of thousands of units apart -
// a dropped item's own PickupRadius (1.5 units, World.Mining.cs) is nowhere near forgiving enough
// to survive that kind of rounding, so an ore chunk mined out in the field could never actually be
// picked back up. The room-local case stays comfortably precise at double too, no downside there.
public sealed record DroppedItem(string Id, ItemType Item, double X, double Y, string? RoomId = null)
{
    public Vec2 Position => new(X, Y);
}
