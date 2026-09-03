using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Building (M60/M62) and demolishing (M61) a compartment on the CURRENT hull - see
// C:\Users\Andrey\.claude\plans\humble-soaring-cat.md's own M60/M61/M62 design. Both go through the
// same "derive a new CustomShipDefinition, rebuild via Ship.FromCustomDefinition" shape World.
// ShipPurchase.cs already uses for a whole-hull swap - adding or removing one room is just a
// different way to arrive at a new definition, not a different way to apply it.
//
// M62 - building is no longer instant or docked-only ("даже в полёте и в бою", the user's own
// original ask): TryBuildRoom now only validates the placement, spends the resources, and enqueues
// a timed PendingRoomBuild; StepRoomBuilds (World.cs's Step, before everything else) ticks it down
// and only actually folds it into the hull via ApplyShipDefinition once the timer completes -
// FinishRoomBuild re-validates against whatever the hull looks like BY THEN, since a fight can
// change it out from under a build in progress. TryDemolishRoom stays docked+Shipwright-gated
// exactly as M61 left it - a deliberate, assisted teardown, not the M63 combat-driven kind.
public sealed partial class World
{
    // Placeholder balance numbers (same spirit as M60's own catalog prices) - a build now needs to
    // be slow enough to matter as a tactical decision mid-fight, not so slow it's never worth
    // starting one. Both are easy to retune later; nothing downstream depends on their exact values.
    private const double RoomBuildDurationSeconds = 30.0;
    public const int HullPlatingCapacity = 40;

    private sealed class PendingRoomBuild
    {
        public required CustomRoomDef Room;
        // Content-каталог отсеков - the catalog entry's own device(s), already positioned at the
        // room's centre (TryBuildRoom) - carried through the timer to FinishRoomBuild unchanged.
        public required IReadOnlyList<CustomDeviceDef> Devices;
        // Direct user request (Cosmoteer-style marching engines) - the catalog entry's own
        // ShipEngine, if it has one (RoomCatalog.EnginesFor), carried through the same way.
        public required IReadOnlyList<CustomEngineDef> Engines;
        public double ElapsedSeconds;
    }

    private readonly List<PendingRoomBuild> _pendingRoomBuilds = new();
    private int _hullPlatingStock;

    public IReadOnlyList<RoomCatalogEntry> GetBuildableRoomCatalog() => RoomCatalog.Entries;
    public int HullPlatingStock => _hullPlatingStock;

    // Called from InitializeShipState (constructor + every hull swap) and EnterStation (World.
    // Voyage.cs) - a fresh/bought hull and every station visit both top the hold back up to full,
    // the same restock timing AmmoStorage/oxygen/fuel/hull already use.
    private void RestockHullPlating() => _hullPlatingStock = HullPlatingCapacity;

    // Test-only precondition setter, same convention as World.SystemRepair.cs's own
    // DebugFastForwardAllRepairs - RoomBuildDurationSeconds is deliberate real content (a build has
    // to actually take a while to matter as a mid-fight decision), not something a unit test should
    // sit through tick-by-tick. Only advances entries that already exist (started via a real
    // TryBuildRoom call) - the next World.Step() still has to run for StepRoomBuilds to actually
    // notice the timer crossed the finish line and apply it.
    public void DebugFastForwardRoomBuilds(double seconds)
    {
        foreach (var pending in _pendingRoomBuilds)
            pending.ElapsedSeconds += seconds;
    }

    // Test-only precondition setter, same convention as DebugBreachWallBlock/DebugPlaceShip - lets a
    // test drive the plating hold to a specific level directly instead of grinding real builds down
    // to it (credits run out first at today's placeholder balance numbers, so there's no other way
    // for a test to isolate "plating specifically is the blocker").
    public void DebugSetHullPlatingStock(int amount) => _hullPlatingStock = amount;

    // Mirrors Game1.ShipEditor.cs's own NextRoomCounter (same "derive from the highest surviving
    // room-N suffix, not the room count" reasoning - a deleted middle room must not have its id
    // reused) - kept as its own small copy here rather than shared, since the editor's version
    // reasons about a UI-local room list, this one about a live Ship's.
    private static string NextRoomId(IReadOnlyList<CustomRoomDef> rooms)
    {
        var max = 0;
        foreach (var room in rooms)
            if (room.Id.StartsWith("room-") && int.TryParse(room.Id.AsSpan(5), out var n) && n > max)
                max = n;
        return $"room-{max + 1}";
    }

    // Same AABB-overlap test CustomShipValidator's own private Overlaps uses - duplicated here
    // (that one isn't exposed) since the placement search below needs to try several candidate
    // positions and reject the ones that would overlap BEFORE handing anything to the validator.
    private static bool RoomsOverlap(CustomRoomDef a, CustomRoomDef b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    // Shared by TryBuildRoom's own placement search (below) and FinishRoomBuild's completion-time
    // re-check: does appending newRoom to def produce a hull that (a) actually touches an existing
    // room somewhere - a floating, disconnected compartment isn't a valid placement, same "every
    // room reachable" intent Station.Procedural.cs's own ring construction guarantees structurally,
    // here just "touches at least one edge" - and (b) still passes every other structural rule
    // CustomShipValidator already checks (exactly one reactor/helm/nav, etc. - trivially true since
    // appended is a strict superset of an already-valid hull, but re-validated rather than assumed
    // so a bug in the placement math fails loudly instead of building a broken hull). Returns the
    // new definition on success, null on any failure - never partially applies anything itself.
    private static CustomShipDefinition? AppendRoomIfValid(CustomShipDefinition def, CustomRoomDef newRoom,
        IReadOnlyList<CustomDeviceDef>? devices = null, IReadOnlyList<CustomEngineDef>? engines = null)
    {
        var roomsWithNew = def.Rooms.Append(newRoom).ToList();
        var overlaps = ShipLayoutGeometry.FindRoomPairOverlaps(roomsWithNew);
        var newRoomOverlaps = overlaps.Where(o => o.RoomAId == newRoom.Id || o.RoomBId == newRoom.Id).ToList();
        if (newRoomOverlaps.Count == 0)
            return null;

        var newDoors = def.Doors.Concat(newRoomOverlaps.Select(o => new CustomDoorDef(o.RoomAId, o.RoomBId))).ToList();
        // Content-каталог отсеков - a device-carrying catalog entry's own device(s), already
        // positioned inside newRoom's own bounds (TryBuildRoom) - appended alongside the room itself.
        var newDevices = devices is { Count: > 0 } ? def.Devices.Concat(devices).ToList() : def.Devices;
        // Direct user request (Cosmoteer-style marching engines) - same "append if this catalog
        // entry actually carries one" shape as devices above.
        var newEngines = engines is { Count: > 0 } ? def.Engines.Concat(engines).ToList() : def.Engines;
        var appended = def with { Rooms = roomsWithNew, Doors = newDoors, Devices = newDevices, Engines = newEngines };
        return CustomShipValidator.Validate(appended).Count > 0 ? null : appended;
    }

    // M62 - "даже в полёте и в бою": no IsDocked/Shipwright gate any more (M60/M61 both had one;
    // building doesn't need a station's help, just materials). Only VALIDATES the placement and
    // spends the resources up front - the room itself doesn't join Ship.Rooms until StepRoomBuilds'
    // timer completes (FinishRoomBuild below), so a build started mid-fight can genuinely be lost if
    // the situation changes before it finishes.
    private void TryBuildRoom(BuildRoomRequest request)
    {
        if (RoomCatalog.Find(request.CatalogId) is not { } entry)
            return;
        if (Credits < entry.Price || _hullPlatingStock < entry.PlatingCost)
            return;

        var def = Ship.ToDefinition();
        // A room already under construction occupies its footprint too (the ghost blocks a second,
        // overlapping build from starting on top of it) but is deliberately NOT a valid attachment
        // ANCHOR yet - candidates below only ever iterate the real, already-built def.Rooms, never
        // _pendingRoomBuilds, so a chain of builds can't be started against a piece that might still
        // fail to complete.
        var occupiedRooms = def.Rooms.Concat(_pendingRoomBuilds.Select(p => p.Room)).ToList();

        CustomRoomDef? newRoom;
        if (request.X is { } x && request.Y is { } y)
        {
            // Content-каталог отсеков - click-to-place UI (StationBuildPanel): the player already
            // picked this exact spot via the client's own mirrored preview (it runs the identical
            // ShipLayoutGeometry.FindRoomPairOverlaps/overlap check against the Rooms/PendingRoomBuilds
            // the snapshot already exposes, purely for instant visual feedback) - re-validate it
            // authoritatively here rather than trusting it, the same "never trust the client's own
            // math" reasoning every other command handler in this file already follows. A stale
            // position (the hull changed under a slow click, a laggy/modified client) is silently
            // refused, same "no charge, nothing happens" outcome the old auto-search always had for
            // "no open edge right now".
            var attempt = new CustomRoomDef(NextRoomId(occupiedRooms), entry.Name, x, y, entry.Width, entry.Height);
            newRoom = occupiedRooms.Any(r => RoomsOverlap(r, attempt))
                ? null
                : AppendRoomIfValid(def, attempt, RoomCatalog.DevicesFor(entry, attempt), RoomCatalog.EnginesFor(entry, attempt)) is null ? null : attempt;
        }
        else
        {
            newRoom = FindAutoPlacement(def, occupiedRooms, entry);
        }
        if (newRoom is null)
            return; // no valid placement right now

        Credits -= entry.Price;
        _hullPlatingStock -= entry.PlatingCost;
        _pendingRoomBuilds.Add(new PendingRoomBuild { Room = newRoom, Devices = RoomCatalog.DevicesFor(entry, newRoom), Engines = RoomCatalog.EnginesFor(entry, newRoom) });
    }

    // M60's original placement search, kept as the fallback for a BuildRoomRequest with no explicit
    // X/Y (nothing forces every future caller to have a placement UI) - flush against SOME existing
    // room's open side. Every hand-authored "row of boxes" hull (Ship.cs/Ship.Scout.cs/Ship.
    // Cruiser.cs) packs its rooms wall-to-wall along X with nothing above/below them at all, so
    // simply extending the row past the last room (the first thing tried here) almost always
    // runs straight into that room's own airlock instead - the row's far wall IS the airlock
    // chamber's own outer door, and an airlock can't sit on a wall that now borders another room
    // (CustomShipValidator's own SideHasNeighbor check). Trying every room's own 4 sides in turn,
    // not just the hull's single furthest edge, is what actually finds a free spot reliably (a
    // row hull's own Top/Bottom is always open exterior hull with nothing there to conflict
    // with) - Bottom/Right tried before Top/Left just biases the result toward "grows the hull
    // outward/downward" rather than "pokes out ahead of the bow", not a hard requirement.
    private static CustomRoomDef? FindAutoPlacement(CustomShipDefinition def, IReadOnlyList<CustomRoomDef> occupiedRooms, RoomCatalogEntry entry)
    {
        foreach (var candidate in def.Rooms)
        {
            var attempts = new (float X, float Y, EdgeSide Side)[]
            {
                (candidate.X, candidate.Y + candidate.Height, EdgeSide.Bottom),
                (candidate.X + candidate.Width, candidate.Y, EdgeSide.Right),
                (candidate.X, candidate.Y - entry.Height, EdgeSide.Top),
                (candidate.X - entry.Width, candidate.Y, EdgeSide.Left),
            };
            foreach (var (x, y, side) in attempts)
            {
                if (def.Airlocks.Any(a => a.RoomId == candidate.Id && a.Side == side))
                    continue;
                var attempt = new CustomRoomDef(NextRoomId(occupiedRooms), entry.Name, x, y, entry.Width, entry.Height);
                if (occupiedRooms.Any(r => RoomsOverlap(r, attempt)))
                    continue;
                var attemptDevices = RoomCatalog.DevicesFor(entry, attempt);
                if (AppendRoomIfValid(def, attempt, attemptDevices, RoomCatalog.EnginesFor(entry, attempt)) is null)
                    continue;
                return attempt;
            }
        }
        return null;
    }

    // Ticked from World.Step(), before every other system (the plan's own "в начале, до кислородной
    // диффузии") so a build that completes this tick is already part of Ship.Rooms by the time
    // anything else this same tick reads it.
    private void StepRoomBuilds(double deltaSeconds)
    {
        for (var i = _pendingRoomBuilds.Count - 1; i >= 0; i--)
        {
            var pending = _pendingRoomBuilds[i];
            pending.ElapsedSeconds += deltaSeconds;
            if (pending.ElapsedSeconds < RoomBuildDurationSeconds)
                continue;

            _pendingRoomBuilds.RemoveAt(i);
            FinishRoomBuild(pending.Room, pending.Devices, pending.Engines);
        }
    }

    // Re-validates against whatever the hull looks like RIGHT NOW, not what it looked like when the
    // build started - a fight (or another build, or a demolition) can change the hull out from under
    // a build in progress. Silently dropped rather than forced on if it no longer fits; the plating
    // and credits already spent when the build started are NOT refunded - a lost supply run is a
    // real, deliberate consequence of building mid-combat, not a bug to patch around.
    private void FinishRoomBuild(CustomRoomDef room, IReadOnlyList<CustomDeviceDef> devices, IReadOnlyList<CustomEngineDef> engines)
    {
        var def = Ship.ToDefinition();
        if (AppendRoomIfValid(def, room, devices, engines) is not { } appended)
            return;
        ApplyShipDefinition(appended);
    }

    // Content-каталог отсеков - re-derives every summed device bonus (reactor output, shield
    // capacity, thrust, turn rate) from the CURRENT Ship, the single source of truth for "how many
    // of each bonus-carrying device exist right now" (Ship.cs's own ReactorDeviceCount/etc. doc
    // comment, ShipSystemDevice's own ThrustBonus/TurnBonus/CapacityBonus fields). Called from every
    // point Ship itself can change (InitializeShipState covers the constructor/purchase/save paths;
    // ApplyShipDefinition covers build/demolish/M63 detachment) so all of them stay correct.
    private void RecomputeDeviceBonuses()
    {
        _reactorRoomBonusOutput = RoomCatalog.ReactorRoomBonusOutput * Math.Max(0, Ship.ReactorDeviceCount - 1);
        ApplyUpgradeEffects(); // re-derives PowerGrid.Reactor.OutputBonus from both sources together
        Shield.CapacityBonus = Ship.SystemDevices.Sum(d => d.CapacityBonus);
    }

    private IReadOnlyList<PendingRoomBuildState> CreatePendingRoomBuildStates() =>
        _pendingRoomBuilds.Select(p => new PendingRoomBuildState(p.Room.Id, p.Room.Name, p.Room.X, p.Room.Y,
            p.Room.Width, p.Room.Height, (float)Math.Min(1.0, p.ElapsedSeconds / RoomBuildDurationSeconds))).ToArray();

    // M61 - the symmetric operation: removes a room (and only the devices actually sitting inside
    // its own footprint - an empty M60-catalog room never has any, but this stays correct once a
    // later milestone's catalog rooms do). Free (no refund) for this milestone - the plan doesn't
    // specify demolition economics yet, and inventing one nobody asked for is worse than leaving it
    // for whichever milestone actually needs it.
    private void TryDemolishRoom(string roomId)
    {
        if (!IsDocked)
            return;
        if (Station.Npcs.All(n => n.Kind != NpcKind.Shipwright))
            return;

        var def = Ship.ToDefinition();
        if (def.Rooms.Count <= 1)
            return; // nothing left to demolish down to
        if (def.Rooms.FirstOrDefault(r => r.Id == roomId) is not { } demolished)
            return;

        var remainingRooms = def.Rooms.Where(r => r.Id != roomId).ToList();
        var remainingDoors = def.Doors.Where(d => d.RoomAId != roomId && d.RoomBId != roomId).ToList();
        var remainingAirlocks = def.Airlocks.Where(a => a.RoomId != roomId).ToList();
        var remainingDevices = def.Devices.Where(d =>
            !(d.X >= demolished.X && d.X <= demolished.X + demolished.Width &&
              d.Y >= demolished.Y && d.Y <= demolished.Y + demolished.Height)).ToList();
        // Direct user request (Cosmoteer-style marching engines) - same bounds-containment filter as
        // devices above, checked against the engine's own Control tile.
        var remainingEngines = def.Engines.Where(e =>
            !(e.X >= demolished.X && e.X <= demolished.X + demolished.Width &&
              e.Y >= demolished.Y && e.Y <= demolished.Y + demolished.Height)).ToList();
        var shrunk = def with { Rooms = remainingRooms, Doors = remainingDoors, Airlocks = remainingAirlocks, Devices = remainingDevices, Engines = remainingEngines };

        // CustomShipValidator already catches "that was the sole reactor/distribution/helm/
        // navigation room" and "that was the last airlock/oxygen generator/suit locker/storage
        // rack" - exactly the "held the sole X" rule the plan calls for, for free. It does NOT check
        // connectivity (nothing about a room-overlap/device-in-bounds validator needs to), which is
        // exactly what RoomGraphConnectivity is for below.
        if (CustomShipValidator.Validate(shrunk).Count > 0)
            return;
        if (!RoomGraphConnectivity.AllReachable(remainingRooms, remainingDoors, remainingRooms[0].Id))
            return; // would split the hull into disconnected pieces - refused, not something a
                     // voluntary demolition should ever produce (M63's combat-driven detachment is
                     // the deliberate, different case where this outcome is actually the point)

        ApplyShipDefinition(shrunk);
    }

    // M61 - shared apply path for both TryBuildRoom and TryDemolishRoom, replacing M60's own
    // "InitializeShipState() wholesale, like a hull swap" simplification (that file's own doc
    // comment called out the exact exploit: it silently healed hull/wire damage and reset doors
    // across the WHOLE ship, not just wherever the room/doors actually changed - fine for a rare,
    // deliberate act, a real problem once building/demolishing becomes routine).
    private void ApplyShipDefinition(CustomShipDefinition newDef)
    {
        var oldShip = Ship;
        var newShip = Ship.FromCustomDefinition(newDef);

        // Whether anything besides plain room/door/wall-block geometry changed. If the device graph
        // itself (turrets/mounts/racks/lockers/ammo/cameras/system devices) is identical, every
        // other device-keyed dictionary (wiring, component-mount installs, rack contents, turret
        // runtimes, ...) is still guaranteed valid against the new Ship untouched - M60's own
        // placeholder room catalog never adds/removes a device, so a build/demolish through it
        // always takes this branch. A room catalog that starts offering real devices (a later
        // milestone) falls back to the proven-correct full reset below until an incremental
        // per-kind reconciliation is worth building for it.
        static HashSet<string> DeviceIds(Ship s) => s.Turrets.Select(t => t.Id)
            .Concat(s.ComponentMounts.Select(m => m.Id)).Concat(s.StorageRacks.Select(r => r.Id))
            .Concat(s.SuitLockers.Select(l => l.Id)).Concat(s.AmmoStorages.Select(a => a.Id))
            .Concat(s.Cameras.Select(c => c.Id)).Concat(s.SystemDevices.Select(d => d.Id))
            // Direct user request (Cosmoteer-style marching engines) - a build/demolish that
            // adds/removes one must take the "device graph changed" full-reset branch below
            // (InitializeShipState, which calls InitializeEngines) rather than the incremental one.
            .Concat(s.Engines.Select(e => e.Id)).ToHashSet();
        var deviceGraphUnchanged = DeviceIds(oldShip).SetEquals(DeviceIds(newShip));

        CurrentShipKind = ShipKind.Custom;
        _customShipDefinition = newDef;
        Ship = newShip;
        RecomputeDeviceBonuses();

        if (!deviceGraphUnchanged)
        {
            InitializeShipState();
            _turretRuntimes.Clear();
            foreach (var turret in newShip.Turrets)
                _turretRuntimes[turret.Id] = new TurretRuntime(turret);
            _turretAimInput.Clear();
            _cardGame = null;
            foreach (var character in _characters.Values)
            {
                character.ManningTurretId = null;
                character.IsAtHelm = false;
                character.IsOutside = false;
                character.OnEnemyShip = false;
                character.OnStation = false;
                character.EvaAttachedTo = EvaAttachment.None;
                character.EvaAttachedAsteroidId = null;
                character.EvaVelocity = Vec2.Zero;
                character.Position = newShip.SpawnPoint;
                character.RoomId = newShip.SpawnRoomId;
            }
            return;
        }

        // Device graph identical - only the room/door/wall-block state (the part that DOES change
        // on every build/demolish by definition) needs reconciling: keep existing ids' state, drop
        // removed ids, default new ones the same way InitializeShipState always has. This is what
        // actually closes the "building/demolishing a room heals every wire/wall/door on the ship"
        // exploit for M60's own placeholder catalog.
        var newBlockIds = newShip.WallBlocks.Select(b => b.Id).ToHashSet();
        foreach (var key in _wallBlockHp.Keys.Where(k => !newBlockIds.Contains(k)).ToList())
            _wallBlockHp.Remove(key);
        foreach (var block in newShip.WallBlocks)
            if (!_wallBlockHp.ContainsKey(block.Id))
                _wallBlockHp[block.Id] = MaxHpFor(block);

        var newDoorLikeIds = newShip.Doors.Select(d => d.Id).Concat(newShip.AirlockOuterDoors.Select(a => a.Id)).ToHashSet();
        foreach (var key in _doorOpen.Keys.Where(k => !newDoorLikeIds.Contains(k)).ToList())
        {
            _doorOpen.Remove(key);
            _doorHp.Remove(key);
        }
        foreach (var door in newShip.Doors)
            if (!_doorOpen.ContainsKey(door.Id))
            {
                _doorOpen[door.Id] = true; // preserves the pre-M16 always-passable behavior
                _doorHp[door.Id] = DoorMaxHp;
            }
        foreach (var outerDoor in newShip.AirlockOuterDoors)
            if (!_doorOpen.ContainsKey(outerDoor.Id))
            {
                _doorOpen[outerDoor.Id] = false; // opening to vacuum is always a deliberate choice
                _doorHp[outerDoor.Id] = DoorMaxHp;
            }

        var newRoomIds = newShip.Rooms.Select(r => r.Id).ToHashSet();
        foreach (var key in _roomOxygen.Keys.Where(k => !newRoomIds.Contains(k)).ToList())
            _roomOxygen.Remove(key);
        foreach (var room in newShip.Rooms)
            if (!_roomOxygen.ContainsKey(room.Id))
                _roomOxygen[room.Id] = 0f; // vacuum - diffuses in naturally once a door opens

        RebuildStationLayouts(); // the airlock's own local position can shift even without a device changing

        // A character standing in a room that no longer exists (demolition) falls back to spawn -
        // the only per-character safety net still needed once the heavier reset above is skipped.
        foreach (var character in _characters.Values.Where(c => !c.OnStation && !c.IsOutside && !newRoomIds.Contains(c.RoomId)))
        {
            character.Position = newShip.SpawnPoint;
            character.RoomId = newShip.SpawnRoomId;
        }
    }
}
