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

    // Removes roomId AND every other room that becomes unreachable from the reactor once it's gone
    // (RoomGraphConnectivity - the same BFS utility M61's TryDemolishRoom already uses, just kept
    // instead of refused when it finds a split) - the whole detached group spins off together as one
    // fragment, not one fragment per orphaned room, since they're still structurally one piece.
    private void DestroyRoomAndDetach(string roomId)
    {
        var def = Ship.ToDefinition();
        if (def.Rooms.Count <= 1)
            return; // the ship's own last room dying is a bigger event than this milestone handles
        if (def.Rooms.All(r => r.Id != roomId))
            return;

        // M74 - generic Devices query instead of the ReactorBlock field directly; still just the
        // first/primary reactor (multiple reactors' anchor-choice is an open question for M77, not
        // this milestone - humble-soaring-cat.md's own "Риски" section).
        var anchorRoomId = Ship.Devices.First(d => d.Kind == DeviceKind.Reactor).RoomId;
        if (anchorRoomId == roomId)
            return; // the reactor's own compartment was the one destroyed - not something a room-
                     // by-room detachment can sensibly resolve; leave it breached-but-attached
                     // (the existing wall-breach behavior) rather than guessing at a bigger outcome

        var remainingRooms = def.Rooms.Where(r => r.Id != roomId).ToList();
        var remainingRoomIds = remainingRooms.Select(r => r.Id).ToHashSet();
        var remainingDoors = def.Doors.Where(d => remainingRoomIds.Contains(d.RoomAId) && remainingRoomIds.Contains(d.RoomBId)).ToList();

        var keptRoomIds = RoomGraphConnectivity.ReachableFrom(remainingRooms, remainingDoors, anchorRoomId);
        var keptRooms = remainingRooms.Where(r => keptRoomIds.Contains(r.Id)).ToList();
        var keptDoors = remainingDoors.Where(d => keptRoomIds.Contains(d.RoomAId) && keptRoomIds.Contains(d.RoomBId)).ToList();
        var keptAirlocks = def.Airlocks.Where(a => keptRoomIds.Contains(a.RoomId)).ToList();

        var detachedRooms = def.Rooms.Where(r => !keptRoomIds.Contains(r.Id)).ToList(); // the destroyed room + anything cut off from the reactor with it
        var keptDevices = def.Devices.Where(d =>
            !detachedRooms.Any(r => d.X >= r.X && d.X <= r.X + r.Width && d.Y >= r.Y && d.Y <= r.Y + r.Height)).ToList();

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
            DamageWallBlock(block.Id, WallBlockMaxHp);
    }
}
