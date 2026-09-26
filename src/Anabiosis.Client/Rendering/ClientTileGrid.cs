using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// M75 (humble-soaring-cat.md) - the client has no direct WorldSnapshot field carrying Ship.Tiles
// (never added - see Ship.cs's own doc comment on Tiles, "nobody reads this yet outside tests"), but
// it doesn't need one: TileGridRasterizer.FromRooms is a pure, deterministic function of Rooms/Doors,
// which the snapshot already carries every tick. Rebuilding it here reconstructs
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
        var tiles = TileGridRasterizer.FromRooms(snapshot.Rooms, snapshot.Doors);
        ApplySupplementalTiles(tiles, snapshot.SupplementalWallTiles ?? Array.Empty<TileCoord>(),
            snapshot.ForcedFloorTiles ?? Array.Empty<TileCoord>(), snapshot.WallOpenSideOverrides ?? Array.Empty<CustomWallOpenSideDef>(),
            snapshot.WallMaterialOverrides ?? Array.Empty<CustomWallMaterialDef>());
        ApplyWallOpenSides(tiles, snapshot.Rooms, snapshot.WallBlocks);
        ApplyWallMaterials(tiles, snapshot.Rooms, snapshot.WallBlocks);
        ApplyLiveDoorState(tiles, snapshot.Rooms, snapshot.Doors, snapshot.DoorStates);
        ApplyDoorEdges(tiles, snapshot.DoorEdges ?? Array.Empty<ShipDoorEdge>());
        ApplyLiveDoorEdgeState(tiles, snapshot.DoorEdges ?? Array.Empty<ShipDoorEdge>(), snapshot.DoorEdgeStates ?? Array.Empty<DoorEdgeState>());
        return tiles;
    }

    // M-doors-as-edges (humble-soaring-cat.md) - mirrors Ship.Custom.cs's own "AddDoorEdge after
    // every other correction has settled the tile's final Wall/HasFloor state" ordering exactly, so
    // CanPlaceDoorEdge's guard sees the same final geometry the server saw when it placed this same
    // edge without error. Guards rather than throws on a stale/mismatched snapshot (a mid-flight
    // resnapshot the client hasn't fully caught up to yet), same defensiveness ApplyLiveDoorState's
    // own airlock branch already uses.
    public static void ApplyDoorEdges(TileGrid tiles, IReadOnlyList<ShipDoorEdge> edges)
    {
        foreach (var edge in edges)
            if (tiles.DoorEdgeAt(edge.Coord, edge.Side) is null && tiles.CanPlaceDoorEdge(edge.Coord, edge.Side))
                tiles.AddDoorEdge(edge.Coord, edge.Side, edge.Id);
    }

    // Mirrors ApplyLiveDoorState just above, for the new edge primitive - same "resent every tick,
    // overlaid onto a freshly rasterized grid" shape, just keyed by Coord/Side instead of a
    // TileGridRasterizer.DoorTileCoords lookup (an edge has no Cells entry of its own to find).
    public static void ApplyLiveDoorEdgeState(TileGrid tiles, IReadOnlyList<ShipDoorEdge> edges, IReadOnlyList<DoorEdgeState> doorEdgeStates)
    {
        foreach (var edge in edges)
        {
            var state = doorEdgeStates.FirstOrDefault(s => s.Id == edge.Id);
            tiles.SetDoorEdgeOpen(edge.Coord, edge.Side, state?.IsOpen ?? true);
            tiles.SetDoorEdgeHp(edge.Coord, edge.Side, state?.Hp ?? 100f);
        }
    }

    // Mirrors World.TileSync.cs's SyncDoorTile server-side - same DoorTileCoords lookup, same
    // DoorStates source, same open-by-default conventions Game1.Lighting.cs already uses when
    // building SightGaps (a regular door defaults to open/`true` if no explicit state exists yet, a
    // vacuum-facing one - Door.LeadsToVacuum - defaults to closed/`false`) so this can never disagree
    // with the gaps.
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
        IReadOnlyList<DoorState> doorStates)
    {
        foreach (var door in doors)
        {
            // Stale/mismatched snapshot guard - skip rather than throw, same defensiveness
            // TileGridRasterizer itself doesn't need (server-authoritative data, room always exists
            // by construction) but this client-side rendering path does (a mid-flight resnapshot the
            // client hasn't fully caught up to yet). Only the vacuum-facing branch can ever hit this -
            // RoomsForDoor's own single-room lookup is the one that would throw, an interior door's
            // DoorTileCoords call against the full room list never does.
            if (door.LeadsToVacuum && rooms.All(r => r.Id != door.RoomAId))
                continue;
            var open = doorStates.FirstOrDefault(s => s.DoorId == door.Id)?.IsOpen ?? !door.LeadsToVacuum;
            var doorRooms = TileGridRasterizer.RoomsForDoor(rooms, door);
            SetDoorOpenState(tiles, TileGridRasterizer.DoorTileCoords(doorRooms, door.X, door.Y, door.Width, door.Height), open);
        }
    }

    private static void SetDoorOpenState(TileGrid tiles, IEnumerable<TileCoord> coords, bool open)
    {
        foreach (var coord in coords)
            if (tiles.CellAt(coord) is { Wall: TileWallKind.Door })
                tiles.SetDoorOpen(coord, open);
    }

    // Direct user request ("хочу сделать чтобы игрок сам выбирал" полублочную стену) - the client's
    // own re-rasterization (TileGridRasterizer.FromRooms) can no longer derive WallOpenSide from
    // Room geometry at all (the automatic inference this method's own doc comment used to lean on
    // was removed), so a manually-painted half-block wall is invisible to the client unless it's
    // read from somewhere else. WallBlock.WallOpenSide (synced every tick, same as Material already
    // is) is that somewhere else - same WallBlockTileCoord lookup ShipRenderer.Rooms.cs's own
    // materialByTile dictionary already uses, just written straight onto the grid instead of kept
    // as a side dictionary, since DrawWallTile/TileOccluders both read WallOpenSide off the CELL.
    public static void ApplyWallOpenSides(TileGrid tiles, IReadOnlyList<Room> rooms, IReadOnlyList<WallBlock> wallBlocks)
    {
        var roomsById = rooms.ToDictionary(r => r.Id);
        foreach (var block in wallBlocks)
        {
            if (block.WallOpenSide is not { } side || !roomsById.TryGetValue(block.RoomId, out var room))
                continue;
            var coord = TileGridRasterizer.WallBlockTileCoord(block, rooms, room);
            tiles.SetWallOpenSide(coord, side);
        }
    }

    // Direct user bug report ("не вижу ничего через стену являющейся иллюминатором") - the client's
    // own re-rasterized tile grid (TileGridRasterizer.FromRooms) never carries WallMaterial at all
    // (every cell defaults to Standard) - the DRAW path alone learns the real material through a
    // separate side dictionary (ShipRenderer.Rooms.cs's own materialByTile, WallBlock-keyed), which
    // TileOccluders never sees since it only reads TileCell.WallMaterial off the grid directly. A
    // Window tile's material has to actually land ON the cell for TileOccluders' new Window
    // exception (IsOccluding) to ever take effect - same WallBlockTileCoord lookup/shape as
    // ApplyWallOpenSides just above, just writing Material instead.
    public static void ApplyWallMaterials(TileGrid tiles, IReadOnlyList<Room> rooms, IReadOnlyList<WallBlock> wallBlocks)
    {
        var roomsById = rooms.ToDictionary(r => r.Id);
        foreach (var block in wallBlocks)
        {
            if (block.Material == WallMaterial.Standard || !roomsById.TryGetValue(block.RoomId, out var room))
                continue;
            var coord = TileGridRasterizer.WallBlockTileCoord(block, rooms, room);
            tiles.SetWallMaterial(coord, block.Material);
        }
    }

    // Direct user bug report ("стены отображаются не на своих местах, а коллизии там же") - the
    // client's own re-rasterization (TileGridRasterizer.FromRooms on Rooms/Doors
    // alone) is the exact same "naive" projection Ship.Custom.cs's own post-processing corrects on
    // the SERVER's Tiles - a T-junction's residual wall (step 3.6) or a half-block notch sitting at
    // a region's own edge (step 3.5's own sibling-subrect guard) can never be represented by Room.
    // Rects at all, so without replaying these same three corrections here, the client draws walls
    // in the wrong place (or misses/mislocates a half-block notch) while the server's real
    // collision (built from the SAME three lists, once, in Ship.Custom.cs) stays correct - exactly
    // "walls don't match where you actually bump into something." Mirrors that method's own order
    // (supplemental wall tiles, then forced-open floor tiles, then wall-open-side overrides)
    // exactly, just applied to the client's own copy of the grid instead of the server's.
    public static void ApplySupplementalTiles(TileGrid tiles, IReadOnlyList<TileCoord> supplementalWallTiles,
        IReadOnlyList<TileCoord> forcedFloorTiles, IReadOnlyList<CustomWallOpenSideDef> wallOpenSideOverrides,
        IReadOnlyList<CustomWallMaterialDef> wallMaterialOverrides)
    {
        foreach (var coord in supplementalWallTiles)
        {
            tiles.SetFloor(coord, true);
            tiles.SetWall(coord, TileWallKind.Solid);
        }
        foreach (var coord in forcedFloorTiles)
            if (tiles.CellAt(coord) is { Wall: not TileWallKind.None })
                tiles.SetWall(coord, TileWallKind.None);
        foreach (var openSide in wallOpenSideOverrides)
            tiles.SetWallOpenSide(new TileCoord(openSide.X, openSide.Y), openSide.Side);
        foreach (var material in wallMaterialOverrides)
            tiles.SetWallMaterial(new TileCoord(material.X, material.Y), material.Material);
    }

    // Direct user report ("проблема из-за низкого фпс", diagnostic overlay showing ~89ms in the
    // visibility-mask phase) - FromRooms/rasterization (region flood-fill included) is a real cost,
    // and it was being paid two or three times over EVERY drawn frame (once each for the sight/room-
    // lighting mask, the wall-drawing pass, and the voice-muffling check - Game1.Lighting.cs,
    // ShipRenderer.Rooms.cs, Game1.cs respectively), even though the ship's actual layout (Rooms/
    // Doors) changes only on the rare tick a compartment is actually built/removed -
    // door OPEN/CLOSED state changes far more often, but that's already a separate, much cheaper
    // overlay (ApplyLiveDoorState) applied on top. This fingerprint is what a caller-side cache (see
    // ShipRenderer's own GetLiveShipTiles, and Game1.Lighting.cs's station-tiles field) compares
    // frame to frame to know whether the expensive rasterization actually needs to run again, or
    // whether last frame's already-built TileGrid (with door state freshly re-overlaid) is still
    // correct. Deliberately excludes door open/closed state - only the STRUCTURAL shape.
    public static int ComputeStructuralFingerprint(IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors)
    {
        var hash = new HashCode();
        hash.Add(rooms.Count);
        foreach (var room in rooms)
        {
            hash.Add(room.Id);
            hash.Add(room.Rects.Count);
            foreach (var rect in room.Rects)
                hash.Add(rect);
        }
        hash.Add(doors.Count);
        foreach (var door in doors)
            hash.Add(HashCode.Combine(door.Id, door.RoomAId, door.RoomBId, door.X, door.Y, door.Width, door.Height, door.Vertical));
        return hash.ToHashCode();
    }
}
