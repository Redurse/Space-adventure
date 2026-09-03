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
        SyncWallBlockTiles(Ship.Tiles, Ship.Rooms, Ship.WallBlocks);

        // M73 - Station.MoveAlongAxis now also reads Tiles, so its doors need the same open-state
        // mirror (no wall HP to sync: a station is never actually breachable - Station.WallBlocks.cs's
        // own comment). Without this, TileCell.DoorOpen's bool default (false) would make every
        // station door look permanently closed to the new movement path, even though World's own
        // _doorOpen defaults every station door to true (World.cs's GetOrCreateStation).
        foreach (var door in Station.Doors)
            SyncDoorTile(Station.Tiles, TileGridRasterizer.DoorTileCoords(Station.Rooms, door.X, door.Y, door.Width, door.Height), door.Id);

        // Bug fix (humble-soaring-cat.md, "стены не имеют коллизии" - the docked-movement follow-up)
        // - Station.ShipConnector rasterizes its own, entirely separate Door tile into Station.Tiles
        // (Station.cs's own constructor), carrying its own unrelated id ($"{pointId}-connector" -
        // Station.Procedural.cs) that nothing ever toggles directly; GetDockedTileGrid's merged
        // Ship+Station grid otherwise leaves it stuck at its rasterized default (permanently closed),
        // even once the ship's own airlock door tile right next to it opens - the two ends of what is
        // physically one connector would then disagree, blocking a docked crossing one tile after it
        // looked open. Mirrors GetDockedLayout's own "same id" convention (its synthetic connector
        // Door reuses the ship's own outer-door id rather than ShipConnector's) - kept in sync with
        // that SAME live door state, not a second, independent one.
        if (Ship.AirlockOuterDoors.Count > 0)
        {
            var connectorOpen = IsDoorOpen(Ship.AirlockOuterDoors[0].Id);
            var connector = Station.ShipConnector;
            SyncDoorOpenOnly(Station.Tiles,
                TileGridRasterizer.DoorTileCoords(new[] { Station.GetRoom(connector.RoomId) }, connector.X, connector.Y, connector.Width, connector.Height),
                connectorOpen);
        }
    }

    private void SyncDoorOpenOnly(TileGrid tiles, IEnumerable<TileCoord> coords, bool open)
    {
        foreach (var coord in coords)
            if (tiles.CellAt(coord) is { Wall: TileWallKind.Door })
                tiles.SetDoorOpen(coord, open);
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

    // Bug fix follow-up (humble-soaring-cat.md, "стены не имеют коллизии") - a hull corner where an
    // interior room-to-room boundary begins exactly at that room's own outer-wall corner has TWO
    // independent WallBlock entities (the room's own outer-edge block, and the interior boundary's
    // own topmost/leftmost unit) legitimately land on the SAME tile coordinate once run through the
    // now-corrected WallBlockTileCoord (both correctly resolve to whichever room's LEADING edge
    // actually owns that tile - the old, buggy version scattered them onto two different, both-wrong
    // coordinates, which accidentally avoided this collision instead of resolving it). The old model
    // never needed to merge them - two independent 1-unit rectangles simply overlapped, each with its
    // own HP, both rendered/hittable in their own right. The tile model has only one real tile there,
    // so it needs one real answer: synced here to whichever of the colliding blocks is MOST damaged
    // (lowest Hp) - a tile a player has cut through via either of its two legacy identities really
    // does have a hole in it, so "most damaged wins" is the physically honest reading, at the cost of
    // needing every colliding identity repaired before the tile itself reads as solid again. Grouped
    // into one dictionary pass first (rather than syncing block-by-block like SyncDoorTile does)
    // specifically so this resolution doesn't depend on Ship.WallBlocks' own iteration order.
    private void SyncWallBlockTiles(TileGrid tiles, IReadOnlyList<Room> rooms, IReadOnlyList<WallBlock> blocks)
    {
        var hpByCoord = new Dictionary<TileCoord, float>();
        var materialByCoord = new Dictionary<TileCoord, WallMaterial>();
        foreach (var block in blocks)
        {
            var coord = TileGridRasterizer.WallBlockTileCoord(block, rooms, Ship.GetRoom(block.RoomId));
            var hp = WallBlockHp(block.Id);
            if (!hpByCoord.TryGetValue(coord, out var existing) || hp < existing)
            {
                hpByCoord[coord] = hp;
                materialByCoord[coord] = block.Material; // the more-damaged identity's own skin wins too - same "one real tile" reasoning
            }
        }
        foreach (var (coord, hp) in hpByCoord)
        {
            if (tiles.CellAt(coord) is not { Wall: TileWallKind.Solid })
                continue;
            tiles.SetWallHp(coord, hp);
            tiles.SetWallMaterial(coord, materialByCoord[coord]);
        }
    }
}
