using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// M75 (humble-soaring-cat.md) - the client has no direct WorldSnapshot field carrying Ship.Tiles
// (never added - see Ship.cs's own doc comment on Tiles, "nobody reads this yet outside tests"), but
// it doesn't need one: TileGridRasterizer.FromRooms is a pure, deterministic function of Rooms/Doors/
// AirlockOuterDoors, which the snapshot already carries every tick. Rebuilding it here reconstructs
// the EXACT same tile shape the server's own Ship.Tiles has, with zero protocol change.
//
// Deliberately does NOT overlay live wall-HP/door-open state the way World.TileSync.cs does
// server-side - the client doesn't need it for rendering: a breached WallBlock already gets its own
// separate "hole" visual (ShipRenderer.DrawBreachedWallBlock, driven by WallBlockStates directly),
// and a Door/AirlockOuterDoor is drawn by its own existing DrawDoor calls - this grid exists purely to
// answer "does a wall-kind tile sit at this coordinate, and which of its 4 neighbors also do" for the
// new per-tile wall renderer (ShipRenderer.DrawShipWalls), not to track live damage/open state.
internal static class ClientTileGrid
{
    public static TileGrid Build(WorldSnapshot snapshot) =>
        TileGridRasterizer.FromRooms(snapshot.Rooms, snapshot.Doors, snapshot.AirlockOuterDoors);
}
