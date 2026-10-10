namespace Anabiosis.Shared.Model;

// "Remove this compartment, and cut loose whatever that leaves unreachable from the reactor" - the pure structural half of a
// compartment's destruction, shared by the player's ship (World.RoomHp.cs) and every hostile ship (World.EnemyDamage.cs).
// It only computes the result; applying it (ejecting people, spawning the debris, rebuilding the ship) stays with the caller.
public static class ShipDetachment
{
    // Needs "remove
    // roomId, and detach anything ELSE that becomes unreachable from the reactor as a result",
    // differing only in what happens to roomId's own footprint once it's gone (flies off with the
    // rest of the detached group here vs sits in place as a permanent wreck decoration for
    // ExplodeRoom - see that method's own doc comment). Stops short of actually applying anything -
    // callers still need to eject crew/drop items/spawn debris (or a wreck patch) using the OLD Ship
    // before calling ApplyShipDefinition themselves, since the exact "what happens to the detached
    // group" step differs between the two callers.
    //
    // M77 (humble-soaring-cat.md) - reachability and device membership are answered from the
    // ALREADY-SYNCED live ship.Tiles (real tile/region data), not from ship.ToDefinition()'s DTO
    // round-trip (RoomGraphConnectivity) plus bounding-box math. ship.Tiles still has the doomed
    // room's own tiles in it at this point (only ApplyShipDefinition, later, actually rebuilds the
    // grid) - simulate its removal on a throwaway TileGrid.Clone() (never mutate the live grid other
    // systems read this same tick) by clearing its floor tiles the exact same way
    // TileRegionConnectivity's own unit tests do, then run the region BFS on that.
    public static bool TryCompute(Ship ship, string roomId, out CustomShipDefinition shrunk, out IReadOnlyList<CustomRoomDef> detachedRooms)
    {
        shrunk = CustomShipDefinition.Empty;
        detachedRooms = Array.Empty<CustomRoomDef>();

        var def = ship.ToDefinition();
        if (def.Rooms.Count <= 1)
            return false; // the ship's own last room dying is a bigger event than this milestone handles
        if (def.Rooms.All(r => r.Id != roomId))
            return false;

        // M74 - generic Devices query instead of the ReactorBlock field directly; still just the
        // first/primary reactor (multiple reactors' anchor-choice is an open question for a later
        // milestone, not this one - humble-soaring-cat.md's own "Риски" section).
        var anchorRoomId = ship.Devices.First(d => d.Kind == DeviceKind.Reactor).RoomId;
        if (anchorRoomId == roomId)
            return false; // the reactor's own compartment was the one destroyed - not something a
                           // room-by-room detachment can sensibly resolve; leave it breached-but-
                           // attached (the existing wall-breach behavior) rather than guessing at a
                           // bigger outcome

        var remainingRooms = def.Rooms.Where(r => r.Id != roomId).ToList();
        var remainingRoomIds = remainingRooms.Select(r => r.Id).ToHashSet();
        // A door no longer authors its own room pair (humble-soaring-cat.md "Дверь как свободный
        // объект") - resolved against the ORIGINAL room layout (def.Rooms, still including the
        // destroyed room) since a door's own position doesn't move when a room disappears.
        var remainingDoors = def.Doors.Where(d =>
        {
            var overlap = ShipLayoutGeometry.FindOverlapAt(def.Rooms, d.X, d.Y, d.Vertical);
            return overlap is { } o && remainingRoomIds.Contains(o.RoomAId) && remainingRoomIds.Contains(o.RoomBId);
        }).ToList();

        var scratchTiles = ship.Tiles.Clone();
        var destroyedRoom = ship.Rooms.First(r => r.Id == roomId);
        foreach (var coord in RoomTileCoords(destroyedRoom))
            scratchTiles.SetFloor(coord, false);

        var anchorRoom = ship.Rooms.First(r => r.Id == anchorRoomId);
        var anchorRegionId = RoomRegionId(anchorRoom, scratchTiles);
        var reachableRegionIds = anchorRegionId is { } anchorId
            ? TileRegionConnectivity.ReachableRegionsFrom(scratchTiles, anchorId)
            : new HashSet<int>();

        var keptRoomIds = remainingRooms
            .Where(r =>
            {
                var liveRoom = ship.Rooms.First(lr => lr.Id == r.Id);
                return RoomRegionId(liveRoom, scratchTiles) is { } regionId && reachableRegionIds.Contains(regionId);
            })
            .Select(r => r.Id)
            .ToHashSet();
        var keptRooms = remainingRooms.Where(r => keptRoomIds.Contains(r.Id)).ToList();
        var keptDoors = remainingDoors.Where(d =>
        {
            var overlap = ShipLayoutGeometry.FindOverlapAt(def.Rooms, d.X, d.Y, d.Vertical);
            return overlap is { } o && keptRoomIds.Contains(o.RoomAId) && keptRoomIds.Contains(o.RoomBId);
        }).ToList();
        var keptAirlocks = def.Airlocks.Where(a => keptRoomIds.Contains(a.RoomId)).ToList();

        var detachedRoomsList = def.Rooms.Where(r => !keptRoomIds.Contains(r.Id)).ToList(); // the destroyed room + anything cut off from the reactor with it
        var keptDevices = def.Devices.Where(d => IsDeviceReachable(d, scratchTiles, reachableRegionIds, detachedRoomsList)).ToList();
        // Same reachability filter as Devices just above (IsPointReachable's own doc comment) -
        // Ship.Engines' own fixtures are a separate list CustomShipDefinition never folded into
        // Devices, so without this an engine sitting in a detached room would silently survive into
        // shrunkDef and crash Ship.FromCustomDefinition's own RoomIdAt lookup for it.
        var keptEngines = def.Engines.Where(e => IsPointReachable(e.X, e.Y, scratchTiles, reachableRegionIds, detachedRoomsList)).ToList();

        // A door edge sits between two tiles of two rooms; it only survives when both of those rooms do.
        string? RoomIdOf(Vec2 point) => ship.Rooms.FirstOrDefault(r => r.Contains(point))?.Id;
        var keptDoorEdges = def.DoorEdges.Where(e =>
        {
            var a = new Vec2(e.X + 0.5, e.Y + 0.5);
            var b = e.Side switch
            {
                TileSide.North => new Vec2(a.X, a.Y - 1),
                TileSide.South => new Vec2(a.X, a.Y + 1),
                TileSide.East => new Vec2(a.X + 1, a.Y),
                _ => new Vec2(a.X - 1, a.Y),
            };
            return RoomIdOf(a) is { } ra && keptRoomIds.Contains(ra) && RoomIdOf(b) is { } rb && keptRoomIds.Contains(rb);
        }).ToList();

        // The tile-editor corrections the ship was built with (extra wall tiles, tiles forced open, half-thickness walls) are not part
        // of ToDefinition but the rebuilt hull needs them - without them its door edges would sit in walls. Any of them that
        // lies only inside a room that is going away goes with it.
        var goneTiles = detachedRoomsList.SelectMany(d => RoomTileCoords(ship.Rooms.First(r => r.Id == d.Id))).ToHashSet();
        goneTiles.ExceptWith(keptRooms.SelectMany(k => RoomTileCoords(ship.Rooms.First(r => r.Id == k.Id))));

        var shrunkDef = def with { Rooms = keptRooms, Doors = keptDoors, Airlocks = keptAirlocks, Devices = keptDevices, Engines = keptEngines, DoorEdges = keptDoorEdges,
            SupplementalWallTiles = ship.SupplementalWallTiles.Where(t => !goneTiles.Contains(t)).ToList(),
            ForcedFloorTiles = ship.ForcedFloorTiles.Where(t => !goneTiles.Contains(t)).ToList(),
            WallOpenSides = ship.WallOpenSideOverrides.Where(o => !goneTiles.Contains(new TileCoord(o.X, o.Y))).ToList(),
            JunctionWalls = ship.JunctionWalls.Where(w => !goneTiles.Contains(new TileCoord(w.X, w.Y)) && keptDoorEdges.Any(e => e.Id == w.DoorId)).ToList() };

        // Same "refuse rather than corrupt" instinct TryBuildRoom/TryDemolishRoom both already have -
        // if what's left no longer validates (lost the sole helm/nav/last airlock/etc.), detachment
        // is skipped entirely for THIS destruction and the room simply stays fully breached in place,
        // rather than forcing a shrink that would leave the remaining ship unplayable.
        if (CustomShipValidator.Validate(shrunkDef).Count > 0)
            return false;

        shrunk = shrunkDef;
        detachedRooms = detachedRoomsList;
        return true;
    }

    // M77 - every tile a Room's own rectangle covers, using the exact same rounding convention
    // TileGridRasterizer.FromRooms's own floor-population pass uses (RoundToInt, away-from-zero) so
    // this walks precisely the tiles that rasterizer originally floored for this room - kept as its
    // own small copy here rather than exposing TileGridRasterizer's private RoundToInt, the same
    // "kept as its own small copy" call World.ShipBuilding.cs's NextRoomId already makes for a
    // similarly tiny helper.
    private static IEnumerable<TileCoord> RoomTileCoords(Room room)
    {
        var left = (int)MathF.Round(room.Left, MidpointRounding.AwayFromZero);
        var right = (int)MathF.Round(room.Right, MidpointRounding.AwayFromZero);
        var top = (int)MathF.Round(room.Top, MidpointRounding.AwayFromZero);
        var bottom = (int)MathF.Round(room.Bottom, MidpointRounding.AwayFromZero);
        for (var x = left; x < right; x++)
            for (var y = top; y < bottom; y++)
                yield return new TileCoord(x, y);
    }

    // A Room's own tiles all belong to one SealedRegion by construction (TileGridRasterizer walls
    // every room's own boundary) - find it via any ONE of the room's tiles that's actually a region
    // member (an edge/corner tile is a wall, not a member; RegionIdAt returns null for those and for
    // any tile the room no longer has at all in `tiles` - e.g. the room just got cleared by the
    // SetFloor(false) loop above). Null only if literally no tile of this room is a region member
    // right now (the room itself was just cleared, or is too small to have any interior at all).
    private static int? RoomRegionId(Room room, TileGrid tiles) =>
        RoomTileCoords(room).Select(tiles.RegionIdAt).FirstOrDefault(id => id is not null);

    // Which tile a device's own center position falls in - the same point-in-tile containment
    // TileCoord's own doc comment defines ([X, X+1) x [Y, Y+1)), matching the existing point-
    // containment convention Ship.RoomIdAt/CustomShipValidator's own Contains already use for "which
    // room is this device in" (floor, not round - a device is never itself tile-aligned the way a
    // wall/floor tile is).
    private static TileCoord DeviceTileCoord(float x, float y) => new((int)MathF.Floor(x), (int)MathF.Floor(y));

    // M77 - real tile ownership instead of bounding-box math: a device belongs to whichever region
    // its own tile is in, and is kept iff that region is still reachable. ship.Tiles never actually
    // tags a live device's own tile with TileCell.DeviceId (only the offline Ship Editor's own
    // separate scratch grid ever calls PlaceDevice - Ship's own TileGridRasterizer/TileSync never
    // do), so this looks the device's tile up by its own position instead, which is exactly the same
    // point-containment idea DeviceId would have encoded. Falls back to the OLD bounding-box check
    // (against the now-detached rooms) whenever the device's own tile isn't a region member right
    // now - a wall-mounted device (camera/turret periscope/terminal-adjacent console) sitting exactly
    // on a wall tile, or a device whose room was just cleared above - so a device is never silently
    // dropped just because its exact point landed off the walkable interior.
    private static bool IsDeviceReachable(CustomDeviceDef device, TileGrid tiles, HashSet<int> reachableRegionIds, IReadOnlyList<CustomRoomDef> detachedRooms) =>
        IsPointReachable(device.X, device.Y, tiles, reachableRegionIds, detachedRooms);

    // Direct user request ("тяга зависимая от расположения движков") follow-up bug fix: a
    // CustomEngineDef (Ship.Engines' own fixture - X/Y is its Control tile, ShipEngine.cs) is a
    // SEPARATE list from CustomShipDefinition.Devices, so TryComputeRoomDetachment used to leave it
    // entirely unfiltered - an engine sitting in a room that just got detached/exploded stayed in the
    // shrunk definition anyway, and Ship.FromCustomDefinition's own RoomIdAt(engine position) then
    // throws (no room left to assign it to). Same point-reachability test IsDeviceReachable already
    // uses, just renamed off "device" since it now serves both.
    private static bool IsPointReachable(float x, float y, TileGrid tiles, HashSet<int> reachableRegionIds, IReadOnlyList<CustomRoomDef> detachedRooms)
    {
        var coord = DeviceTileCoord(x, y);
        if (tiles.RegionIdAt(coord) is { } regionId)
            return reachableRegionIds.Contains(regionId);
        return !detachedRooms.Any(r => x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height);
    }
}
