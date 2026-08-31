using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// M72/M73 (humble-soaring-cat.md) - keeps Ship.Tiles's (and, since M73, Station.Tiles's) mutable
// per-tile state (TileCell.DoorOpen/WallHp) in sync with the door/wall state this World already
// tracks authoritatively in its own _doorOpen/_doorHp/_wallBlockHp dictionaries. TileGridRasterizer
// only ever runs once, inside each structure's own constructor - without this, a tile grid would
// silently stay frozen at "every door closed, every wall at full HP" forever, no matter what the
// player actually does, which both World.Atmosphere.cs (M72) and Ship/Station's own MoveAlongAxis
// (M73) now read from. EnemyShipLayout is deliberately NOT synced here yet - its interior doors
// share World's dictionaries the same way Ship's do, but its two AirlockOuterDoors are tracked in
// EnemyShipRuntime's own separate _airlockHp instead (never toggle-able, only cut open) - a
// different-enough shape that EnemyShipLayout.MoveAlongAxis stays on the old RoomLayout system for
// now rather than rush a half-tested hybrid sync for boarding.
//
// A full reconciliation pass every tick, rather than hooking each of the 7+ individual call sites
// that mutate _doorOpen/_doorHp/_wallBlockHp (ToggleDoor, DamageDoor, ChopDoor, FinishSystemRepair,
// AutoDoorController, DamageWallBlock, RepairWallBlock, plus the bulk resets in
// InitializeShipState/ApplyShipDefinition) - simpler to get right than threading a sync call through
// every one of them, and cheap: a hull has tens of doors/wall blocks, not thousands.
public sealed partial class World
{
    private void SyncShipTiles()
    {
        foreach (var door in Ship.Doors)
            SyncDoorTile(Ship.Tiles, TileGridRasterizer.DoorTileCoords(Ship.Rooms, door.X, door.Y, door.Width, door.Height), door.Id);
        foreach (var airlock in Ship.AirlockOuterDoors)
            SyncDoorTile(Ship.Tiles, TileGridRasterizer.DoorTileCoords(new[] { Ship.GetRoom(airlock.RoomId) }, airlock.X, airlock.Y, airlock.Width, airlock.Height), airlock.Id);
        foreach (var block in Ship.WallBlocks)
            SyncWallBlockTile(Ship.Tiles, block, Ship.GetRoom(block.RoomId));

        // M73 - Station.MoveAlongAxis now also reads Tiles, so its doors need the same open-state
        // mirror (no wall HP to sync: a station is never actually breachable - Station.WallBlocks.cs's
        // own comment). Without this, TileCell.DoorOpen's bool default (false) would make every
        // station door look permanently closed to the new movement path, even though World's own
        // _doorOpen defaults every station door to true (World.cs's GetOrCreateStation).
        foreach (var door in Station.Doors)
            SyncDoorTile(Station.Tiles, TileGridRasterizer.DoorTileCoords(Station.Rooms, door.X, door.Y, door.Width, door.Height), door.Id);
    }

    private void SyncDoorTile(TileGrid tiles, IEnumerable<TileCoord> coords, string doorId)
    {
        var hp = DoorHp(doorId);
        var open = IsDoorOpen(doorId);
        foreach (var coord in coords)
        {
            if (tiles.CellAt(coord) is not { Wall: TileWallKind.Door })
                continue; // half-unit-hull rounding drift (see TileGridRasterizer) - skip rather than crash
            tiles.SetWallHp(coord, hp);
            tiles.SetDoorOpen(coord, open);
        }
    }

    private void SyncWallBlockTile(TileGrid tiles, WallBlock block, Room room)
    {
        var coord = TileGridRasterizer.WallBlockTileCoord(block, room);
        if (tiles.CellAt(coord) is not { Wall: TileWallKind.Solid })
            return;
        tiles.SetWallHp(coord, WallBlockHp(block.Id));
    }
}
