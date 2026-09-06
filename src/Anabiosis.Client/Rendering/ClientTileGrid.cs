using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// M75 (humble-soaring-cat.md) - the client has no direct WorldSnapshot field carrying Ship.Tiles
// (never added - see Ship.cs's own doc comment on Tiles, "nobody reads this yet outside tests"), but
// it doesn't need one: TileGridRasterizer.FromRooms is a pure, deterministic function of Rooms/Doors/
// AirlockOuterDoors, which the snapshot already carries every tick. Rebuilding it here reconstructs
// the EXACT same tile shape the server's own Ship.Tiles has, with zero protocol change.
//
// Bug fix (M78 follow-up, humble-soaring-cat.md) - this USED to deliberately skip overlaying live
// door-open state, on the reasoning that the renderer doesn't need it: a Door tile is always skipped
// entirely by ShipRenderer.DrawShipWalls regardless of TileCell.DoorOpen (DrawDoor already draws it
// separately), and a breached WallBlock gets its own hole visual driven by WallBlockStates directly.
// That reasoning was correct for the ORIGINAL (M75) rendering-only consumer, but M78 added a second
// consumer - TileOccluders.Build (Game1.Lighting.cs) - whose own IsOccluding treats a Door tile as
// occluding whenever DoorOpen is false. Since this method never set it, every door tile came back
// perpetually "closed" here regardless of the live game state, so TileOccluders always emitted a full
// wall segment at every door position. Game1.Lighting.cs separately cuts a SightGap through that
// segment for a door it knows is open, but the gap rectangle is built from the door's own centered,
// zero-thickness (X, Y, Width, Height) footprint (Occluders.ToGap) - the pre-tile convention - while
// the tile-derived wall run sits on the ASYMMETRIC one-tile-thick footprint TileGridRasterizer's
// leading/trailing rule actually places it on (see that class's own comment), which the two edges of
// a door tile only sometimes coincide with. The result: opening a door often left one face of its
// tile uncut, still blocking sight exactly at the doorway - "door open but still can't see through it"
// (bug report, humble-soaring-cat.md). Overlaying the real DoorOpen state here fixes it at the root:
// an open door tile is skipped by IsOccluding entirely, so no wall segment (and therefore no
// gap-alignment question) exists at that position in the first place - matching what TileOccluders'
// own doc comment already assumed callers would do.
// Public (not internal) so TestRunner.ClientTileGrid.cs (Anabiosis.Tests, which already
// references this project for CustomShipStore's own test) can exercise the exact regression fixed
// above directly, rather than reimplementing the overlay logic a second time just to test it - the
// same reasoning TileGridRasterizer/TileOccluders are already public for.
public static class ClientTileGrid
{
    public static TileGrid Build(WorldSnapshot snapshot)
    {
        var tiles = TileGridRasterizer.FromRooms(snapshot.Rooms, snapshot.Doors, snapshot.AirlockOuterDoors);
        ApplyLiveDoorState(tiles, snapshot.Rooms, snapshot.Doors, snapshot.AirlockOuterDoors, snapshot.DoorStates);
        return tiles;
    }

    // Mirrors World.TileSync.cs's SyncDoorTile server-side - same DoorTileCoords lookup, same
    // DoorStates source, same open-by-default conventions Game1.Lighting.cs already uses when
    // building SightGaps (a regular Door defaults to open/`true` if no explicit state exists yet, an
    // AirlockOuterDoor defaults to closed/`false`) so this can never disagree with the gaps.
    //
    // Public (not private) - Build above only ever rasterizes the PLAYER'S OWN ship (snapshot.Rooms/
    // Doors), but Game1.Lighting.cs's docked case also rasterizes the station's own layout via a
    // separate direct TileGridRasterizer.FromRooms(snapshot.Station.Rooms, ...) call (two structures,
    // two independent TileGrids - see that call site's own doc comment on why they aren't merged).
    // That station-side grid needs the exact same live-door-state overlay this method already does
    // for the ship - without it, a station-side door (or the ship<->station connector) is
    // permanently "closed" to TileOccluders regardless of its real open/closed state, the identical
    // bug this file was written to fix, just on the other structure. Exposed here so Game1.Lighting.cs
    // can call it a second time rather than duplicating the overlay logic.
    public static void ApplyLiveDoorState(TileGrid tiles, IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors,
        IReadOnlyList<AirlockOuterDoor> airlocks, IReadOnlyList<DoorState> doorStates)
    {
        foreach (var door in doors)
        {
            var open = doorStates.FirstOrDefault(s => s.DoorId == door.Id)?.IsOpen ?? true;
            SetDoorOpenState(tiles, TileGridRasterizer.DoorTileCoords(rooms, door.X, door.Y, door.Width, door.Height), open);
        }
        foreach (var airlock in airlocks)
        {
            var room = rooms.FirstOrDefault(r => r.Id == airlock.RoomId);
            if (room is null)
                continue; // stale/mismatched snapshot - skip rather than throw, same defensiveness TileGridRasterizer itself uses
            var open = doorStates.FirstOrDefault(s => s.DoorId == airlock.Id)?.IsOpen ?? false;
            SetDoorOpenState(tiles, TileGridRasterizer.DoorTileCoords(new[] { room }, airlock.X, airlock.Y, airlock.Width, airlock.Height), open);
        }
    }

    private static void SetDoorOpenState(TileGrid tiles, IEnumerable<TileCoord> coords, bool open)
    {
        foreach (var coord in coords)
            if (tiles.CellAt(coord) is { Wall: TileWallKind.Door })
                tiles.SetDoorOpen(coord, open);
    }
}
