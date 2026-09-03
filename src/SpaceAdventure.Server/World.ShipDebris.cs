using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// M63 - "структурное отделение" (see C:\Users\Andrey\.claude\plans\humble-soaring-cat.md's own M63
// design): a room whose own exterior wall is fully breached is destroyed outright, and whatever
// that leaves unreachable from the reactor - not just the destroyed room itself - splits off as one
// free-flying debris fragment. No new physics: a fragment is pure inertia (position += velocity*dt),
// the exact same integrator the ship/asteroids/EVA already use (this project has had no gravity
// since M59). Nothing inside a fragment is simulated (no walking around on it) - the same
// simplified treatment already given to every other non-interactive field object.
//
// M64 - consequences of a detachment, on top of the structure itself: anyone actually standing in a
// detaching room is ejected into free EVA at the split point (DestroyRoomAndDetach's own doc comment
// below) rather than left orphaned for ApplyShipDefinition's generic "no such room any more, fall
// back to spawn" cleanup to silently teleport them to safety inside the remaining ship - that
// fallback is right for M61's voluntary, docked demolition but wrong for a combat kill. Any item
// dropped in a detaching room is removed outright ("flies off with the debris" - the plan's own
// wording for why this is simpler than scattering it into vacuum as a separate free-floating pickup,
// and consistent with a fragment's interior never being walked on in the first place). The plan's
// other two M64 items need no new code here: a detached reactor/distribution block already can't
// happen (DestroyRoomAndDetach's own reactor-room guard), and power loss in the remaining ship from
// losing OTHER devices already falls out of World.Wiring.cs's existing IsPinPowered reachability
// check for free, same as it already does for a cut wire.
public sealed partial class World
{
    private sealed class ShipDebrisFragment
    {
        public required string Id;
        public Vec2 Position;
        public required Vec2 Velocity;
        public required float RotationDegrees;
        // Stored relative to the FRAGMENT's own pivot (its footprint's centre at the moment of
        // detachment), not the old ship's hull centre - so Position alone is enough to place every
        // room correctly from here on, the same "one transform, not two" shape Ship.Rooms itself
        // already has relative to _shipFieldPosition.
        public required IReadOnlyList<Room> Rooms;
    }

    private readonly List<ShipDebrisFragment> _shipDebris = new();
    private int _nextDebrisId;

    // Called from World.WallBlocks.cs's own DamageWallBlock, the single choke point every source of
    // player-ship wall damage (enemy fire, cutting, asteroid impact) already funnels through - a
    // room with no wall blocks of its own (fully interior, no exterior wall) can never be destroyed
    // this way; it can still be swept into a debris group if whatever WAS between it and the reactor
    // gets destroyed instead (DestroyRoomAndDetach's own connectivity check below handles that).
    private void CheckRoomStructuralFailure(string blockId)
    {
        var block = Ship.WallBlocks.FirstOrDefault(b => b.Id == blockId);
        if (block is null)
            return;
        var roomBlocks = Ship.WallBlocks.Where(b => b.RoomId == block.RoomId).ToList();
        if (roomBlocks.Count == 0 || roomBlocks.Any(b => !IsWallBlockBreached(b.Id)))
            return; // no exterior wall to lose, or not every block of it is gone yet

        DestroyRoomAndDetach(block.RoomId);
    }

    // Removes roomId AND every other room that becomes unreachable from the reactor once it's gone -
    // the whole detached group spins off together as one fragment, not one fragment per orphaned
    // room, since they're still structurally one piece.
    //
    // M77 (humble-soaring-cat.md) - reachability and device membership are now both answered from
    // the ALREADY-SYNCED live Ship.Tiles (real tile/region data), not from Ship.ToDefinition()'s DTO
    // round-trip (RoomGraphConnectivity) plus bounding-box math. Ship.Tiles still has the doomed
    // room's own tiles in it at this point (only ApplyShipDefinition, below, actually rebuilds the
    // grid) - simulate its removal on a throwaway TileGrid.Clone() (never mutate the live grid other
    // systems read this same tick) by clearing its floor tiles the exact same way
    // TileRegionConnectivity's own unit tests do, then run the region BFS on that.
    private void DestroyRoomAndDetach(string roomId)
    {
        var def = Ship.ToDefinition();
        if (def.Rooms.Count <= 1)
            return; // the ship's own last room dying is a bigger event than this milestone handles
        if (def.Rooms.All(r => r.Id != roomId))
            return;

        // M74 - generic Devices query instead of the ReactorBlock field directly; still just the
        // first/primary reactor (multiple reactors' anchor-choice is an open question for a later
        // milestone, not this one - humble-soaring-cat.md's own "Риски" section).
        var anchorRoomId = Ship.Devices.First(d => d.Kind == DeviceKind.Reactor).RoomId;
        if (anchorRoomId == roomId)
            return; // the reactor's own compartment was the one destroyed - not something a room-
                     // by-room detachment can sensibly resolve; leave it breached-but-attached
                     // (the existing wall-breach behavior) rather than guessing at a bigger outcome

        var remainingRooms = def.Rooms.Where(r => r.Id != roomId).ToList();
        var remainingRoomIds = remainingRooms.Select(r => r.Id).ToHashSet();
        var remainingDoors = def.Doors.Where(d => remainingRoomIds.Contains(d.RoomAId) && remainingRoomIds.Contains(d.RoomBId)).ToList();

        var scratchTiles = Ship.Tiles.Clone();
        var destroyedRoom = Ship.Rooms.First(r => r.Id == roomId);
        foreach (var coord in RoomTileCoords(destroyedRoom))
            scratchTiles.SetFloor(coord, false);

        var anchorRoom = Ship.Rooms.First(r => r.Id == anchorRoomId);
        var anchorRegionId = RoomRegionId(anchorRoom, scratchTiles);
        var reachableRegionIds = anchorRegionId is { } anchorId
            ? TileRegionConnectivity.ReachableRegionsFrom(scratchTiles, anchorId)
            : new HashSet<int>();

        var keptRoomIds = remainingRooms
            .Where(r =>
            {
                var liveRoom = Ship.Rooms.First(lr => lr.Id == r.Id);
                return RoomRegionId(liveRoom, scratchTiles) is { } regionId && reachableRegionIds.Contains(regionId);
            })
            .Select(r => r.Id)
            .ToHashSet();
        var keptRooms = remainingRooms.Where(r => keptRoomIds.Contains(r.Id)).ToList();
        var keptDoors = remainingDoors.Where(d => keptRoomIds.Contains(d.RoomAId) && keptRoomIds.Contains(d.RoomBId)).ToList();
        var keptAirlocks = def.Airlocks.Where(a => keptRoomIds.Contains(a.RoomId)).ToList();

        var detachedRooms = def.Rooms.Where(r => !keptRoomIds.Contains(r.Id)).ToList(); // the destroyed room + anything cut off from the reactor with it
        var keptDevices = def.Devices.Where(d => IsDeviceReachable(d, scratchTiles, reachableRegionIds, detachedRooms)).ToList();

        var shrunk = def with { Rooms = keptRooms, Doors = keptDoors, Airlocks = keptAirlocks, Devices = keptDevices };

        // Same "refuse rather than corrupt" instinct TryBuildRoom/TryDemolishRoom both already have -
        // if what's left no longer validates (lost the sole helm/nav/last airlock/etc.), detachment
        // is skipped entirely for THIS destruction and the room simply stays fully breached in place,
        // rather than forcing a shrink that would leave the remaining ship unplayable.
        if (CustomShipValidator.Validate(shrunk).Count > 0)
            return;

        // M64 - both use the OLD Ship/OLD room set, so this has to happen before ApplyShipDefinition
        // below replaces Ship with the shrunk hull.
        var detachedRoomIds = detachedRooms.Select(r => r.Id).ToHashSet();
        EjectCrewFromDetachingRooms(detachedRoomIds);
        _droppedItems.RemoveAll(item => item.RoomId is not null && detachedRoomIds.Contains(item.RoomId));

        SpawnDebrisFragment(detachedRooms);
        ApplyShipDefinition(shrunk);
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
    // its own tile is in, and is kept iff that region is still reachable. Ship.Tiles never actually
    // tags a live device's own tile with TileCell.DeviceId (only the offline Ship Editor's own
    // separate scratch grid ever calls PlaceDevice - Ship's own TileGridRasterizer/TileSync never
    // do), so this looks the device's tile up by its own position instead, which is exactly the same
    // point-containment idea DeviceId would have encoded. Falls back to the OLD bounding-box check
    // (against the now-detached rooms) whenever the device's own tile isn't a region member right
    // now - a wall-mounted device (camera/turret periscope/terminal-adjacent console) sitting exactly
    // on a wall tile, or a device whose room was just cleared above - so a device is never silently
    // dropped just because its exact point landed off the walkable interior.
    private static bool IsDeviceReachable(CustomDeviceDef device, TileGrid tiles, HashSet<int> reachableRegionIds, IReadOnlyList<CustomRoomDef> detachedRooms)
    {
        var coord = DeviceTileCoord(device.X, device.Y);
        if (tiles.RegionIdAt(coord) is { } regionId)
            return reachableRegionIds.Contains(regionId);
        return !detachedRooms.Any(r => device.X >= r.X && device.X <= r.X + r.Width && device.Y >= r.Y && device.Y <= r.Y + r.Height);
    }

    // M64 - everyone actually aboard a room that's about to detach becomes a free EVA body at their
    // own exact position, the moment before the room stops existing - same state-reset shape
    // World.Boarding.cs's own EjectFromEnemyShip uses for "the structure you were standing in is
    // gone", just computing the ejection point from THIS ship's own hull transform instead of
    // BoardableEnemy's. Doing this FIRST (before ApplyShipDefinition runs) matters: it flips
    // IsOutside to true, which is exactly the flag ApplyShipDefinition's own generic orphaned-
    // character fallback checks to decide whether to step in - once it's already true, that
    // fallback correctly leaves the ejection alone instead of overwriting it.
    private void EjectCrewFromDetachingRooms(HashSet<string> detachedRoomIds)
    {
        var (hullCenter, _) = GetHullLocalBounds();
        foreach (var character in _characters.Values.Where(c => !c.OnStation && !c.IsOutside && detachedRoomIds.Contains(c.RoomId)).ToList())
        {
            character.IsOutside = true;
            character.EvaAttachedTo = EvaAttachment.None;
            character.EvaAttachedAsteroidId = null;
            // EvaLocalOffset's meaning flips from a hull-local offset to an absolute world position
            // the instant nothing is actually holding the character to a structure any more
            // (Character.cs's own doc comment on the field) - exactly this instant.
            character.EvaLocalOffset = _shipFieldPosition + RotateLocalToWorld(character.Position - hullCenter, _shipRotationDegrees);
            // Keeps moving with the ship's own last velocity rather than snapping to absolute rest -
            // the same "pure pursuit isn't enough, hand over the origin's own live velocity too"
            // fix World.Eva.cs's HandlePushOff already needed this session for the ordinary push-off
            // case; a structural kill deserves the same courtesy; the debris fragment itself is
            // launched with the identical velocity (SpawnDebrisFragment), so ejected crew and the
            // wreck they were just standing in drift apart smoothly rather than one snapping still.
            character.EvaVelocity = _shipVelocity;
            character.PushedOffFrom = PushOffOrigin.None;
            character.BouncedOffFrom = PushOffOrigin.None;
            character.ManningTurretId = null;
            character.IsAtHelm = false;
            character.RoomId = Ship.SpawnRoomId; // meaningless while outside, valid for the trip home -
                                                  // same convention EjectFromEnemyShip already uses
        }
    }

    private void SpawnDebrisFragment(IReadOnlyList<CustomRoomDef> detachedRooms)
    {
        // The detached group's own footprint centre becomes its new pivot - simple bounding-box
        // centre rather than an area-weighted one, plenty accurate for a first cut (nothing about
        // gameplay depends on the pivot being the exact centre of mass).
        var minX = detachedRooms.Min(r => r.X);
        var maxX = detachedRooms.Max(r => r.X + r.Width);
        var minY = detachedRooms.Min(r => r.Y);
        var maxY = detachedRooms.Max(r => r.Y + r.Height);
        var pivot = new Vec2((minX + maxX) / 2.0, (minY + maxY) / 2.0);

        var (hullCenter, _) = GetHullLocalBounds();
        var worldPosition = _shipFieldPosition + RotateLocalToWorld(pivot - hullCenter, _shipRotationDegrees);

        _shipDebris.Add(new ShipDebrisFragment
        {
            Id = $"debris-{_nextDebrisId++}",
            Position = worldPosition,
            Velocity = _shipVelocity, // inherits the ship's own velocity at the exact moment of separation - no extra impulse
            RotationDegrees = _shipRotationDegrees,
            Rooms = detachedRooms.Select(r => new Room(r.Id, r.Name, (float)(r.X - pivot.X), (float)(r.Y - pivot.Y), r.Width, r.Height)).ToArray(),
        });
    }

    // Pure inertia, ticked from World.Step() - the exact same integrator every other field object
    // already uses (no gravity since M59, so this is genuinely the whole physics model).
    private void StepShipDebris(double deltaSeconds)
    {
        foreach (var fragment in _shipDebris)
            fragment.Position += fragment.Velocity * deltaSeconds;
    }

    private IReadOnlyList<ShipDebrisState> CreateShipDebrisStates() =>
        _shipDebris.Select(f => new ShipDebrisState(f.Id, (float)f.Position.X, (float)f.Position.Y, f.RotationDegrees, f.Rooms)).ToArray();

    // Test-only precondition setter, same convention as DebugBreachWallBlock - fully breaches EVERY
    // wall block a room has (DebugBreachWallBlock itself only ever touches the first one it finds),
    // going through the real private DamageWallBlock so CheckRoomStructuralFailure's own hook fires
    // exactly the way a real cutter/enemy-fire/asteroid-impact sequence gradually working through
    // every block on a room's exterior would - a test that calls this is exercising the actual
    // trigger path, not a shortcut around it.
    public void DebugDestroyRoomWallBlocks(string roomId)
    {
        foreach (var block in Ship.WallBlocks.Where(b => b.RoomId == roomId).ToList())
            DamageWallBlock(block.Id, MaxHpFor(block));
    }
}
