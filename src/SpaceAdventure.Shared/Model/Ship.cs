namespace SpaceAdventure.Shared.Model;

// Fixed per-class layouts (game_design.md section 9 — "несколько классов кораблей... своя
// фиксированная планировка отсеков"). Split across partials by class: this file holds the shared
// Ship type plus CreateStarter() (the original M2 layout, now ShipKind.Frigate); Ship.Scout.cs and
// Ship.Cruiser.cs hold the two additional classes added alongside it.
public sealed partial class Ship
{
    public IReadOnlyList<Room> Rooms { get; }
    public IReadOnlyList<Door> Doors { get; }
    public IReadOnlyList<AirlockOuterDoor> AirlockOuterDoors { get; }
    public IReadOnlyList<Turret> Turrets { get; }
    public IReadOnlyList<HullCamera> Cameras { get; }
    public IReadOnlyList<AmmoStorage> AmmoStorages { get; }
    public IReadOnlyList<SuitLocker> SuitLockers { get; }
    public IReadOnlyList<ShipSystemDevice> SystemDevices { get; }
    public IReadOnlyList<WallBlock> WallBlocks { get; }
    // Direct user request (Cosmoteer-style marching engines, ShipEngine.cs's own doc comment) -
    // empty for every hand-authored hull (CreateStarter/.Scout/.Cruiser/.Corvette never place one),
    // only ever populated by a Ship Editor-built hull (Ship.Custom.cs's FromCustomDefinition).
    public IReadOnlyList<ShipEngine> Engines { get; }
    // M71 (humble-soaring-cat.md) - additive projection of Rooms/Doors/AirlockOuterDoors/WallBlocks
    // onto the new tile-grid model (TileGrid.cs). Nobody reads this yet outside tests; it exists
    // purely to prove the projection is lossless before any dependent system (atmosphere, movement,
    // rendering...) migrates to it one milestone at a time.
    public TileGrid Tiles { get; }
    // M74 (humble-soaring-cat.md) - flattened ECS-style view over every physical device fixture
    // below (ReactorBlock/DistributionBlock/BatteryBlock/HelmConsole/NavigationConsole/CardTable/
    // Jukebox/SystemDevices/Turrets/AmmoStorages/SuitLockers/StorageRacks/Cameras/ComponentMounts,
    // plus every Extra*Position/Console beyond the first) - purely additive, built once below from
    // those same fields (ShipDevice.cs's own doc comment), so nothing about them changes.
    public IReadOnlyList<ShipDevice> Devices { get; }
    public ReactorBlock ReactorBlock { get; }
    public PowerDistributionBlock DistributionBlock { get; }
    public BatteryBlock BatteryBlock { get; }
    public NavigationConsole NavigationConsole { get; }
    public HelmConsole HelmConsole { get; }
    public CardTable CardTable { get; }
    // The jukebox's physical position, or null when this hull has none - unlike CardTable this is
    // genuinely optional flavor furniture (Ship Editor only for now), not a fixture every hull gets.
    public Jukebox? Jukebox { get; }
    // Two per hull (game_design.md section 13) - a starter kit of 3 units of every hand
    // tool/tank/weapon/consumable used to live scattered across the ship as individual ToolStation
    // pickups; it now lives here instead, split across these two shelves (World.ShipPurchase.cs's
    // InitializeRackSlots), so the player has one kind of place to look for gear, not two.
    public IReadOnlyList<StorageRack> StorageRacks { get; }
    public IReadOnlyList<ComponentMount> ComponentMounts { get; }
    public Vec2 SpawnPoint { get; }
    public string SpawnRoomId { get; }
    // Which way this hull points when it flies, in its own layout coordinates. The classes laid out
    // as a row of compartments travel along +X, so 0; a hull built down the screen has to lead with
    // its nose instead, or it drifts through space broadside-on. Used only to pick the rotation
    // that matches the current velocity (World.ShipField.cs) - everything else still works in the
    // ship's own unrotated frame.
    public float ForwardDegrees { get; }
    public bool IsCustomBuilt { get; }

    // Content-каталог отсеков ("бонус, не список" - see the plan's own design note): the ONE literal
    // ReactorBlock/DistributionBlock/HelmConsole/NavigationConsole object stays exactly that even
    // once a player builds a second reactor/bridge room - Ship.Custom.cs's FromCustomDefinition still
    // only ever constructs a physical object from the FIRST device of each kind. These counts are
    // what let the extra ones still contribute a numeric bonus (World.ShipBuilding.cs's own
    // RecomputeDeviceBonuses) - and, just as importantly, what let Ship.ToDefinition() round-trip the
    // count losslessly (emitting one CustomDeviceDef per counted instance, not just one) so a LATER
    // unrelated build/demolish doesn't silently collapse the bonus back down to 1 the next time the
    // hull passes through ToDefinition()->FromCustomDefinition(). Every hand-authored hull (Ship.cs/
    // Ship.Scout.cs/Ship.Cruiser.cs/Ship.Corvette.cs) never sets these explicitly, so they default to
    // the correct "exactly one" - zero behavior change for any existing fixed-class hull.
    public int ReactorDeviceCount { get; }
    public int DistributionDeviceCount { get; }
    public int HelmDeviceCount { get; }
    public int NavigationDeviceCount { get; }
    // Extra bridge/cockpit rooms beyond the first (content-каталог отсеков): reuses HelmConsole/
    // NavigationConsole's own record shape as a plain seat position, not a second physical,
    // damageable fixture - World.Interact.cs/World.Scanner.cs treat proximity to ANY of these the
    // same as proximity to the primary HelmConsole/NavigationConsole above.
    public IReadOnlyList<HelmConsole> ExtraHelmConsoles { get; }
    public IReadOnlyList<NavigationConsole> ExtraNavigationConsoles { get; }
    // Extra reactor/distribution rooms beyond the first - a bonus-only device with no seat, so a
    // plain Vec2 is enough (unlike Helm/Navigation, nothing ever needs to stand at one). Still has
    // to carry its OWN real position, not just a count: Ship.ToDefinition() (Ship.Convert.cs) needs
    // it to re-emit the device where it was actually BUILT, not collapsed onto ReactorBlock/
    // DistributionBlock's own position - otherwise M63's structural detachment (World.ShipDebris.cs's
    // keptDevices bounds filter) can never tell a bonus reactor device apart from the ship's original
    // one, and destroying the bonus room's own wall blocks would never actually remove its device.
    public IReadOnlyList<Vec2> ExtraReactorPositions { get; }
    public IReadOnlyList<Vec2> ExtraDistributionPositions { get; }

    private readonly Dictionary<string, Room> _roomsById;

    public Ship(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<Door> doors,
        IReadOnlyList<AirlockOuterDoor> airlockOuterDoors,
        IReadOnlyList<Turret> turrets,
        IReadOnlyList<HullCamera> cameras,
        IReadOnlyList<AmmoStorage> ammoStorages,
        IReadOnlyList<SuitLocker> suitLockers,
        IReadOnlyList<ShipSystemDevice> systemDevices,
        IReadOnlyList<WallBlock> wallBlocks,
        ReactorBlock reactorBlock,
        PowerDistributionBlock distributionBlock,
        BatteryBlock batteryBlock,
        NavigationConsole navigationConsole,
        HelmConsole helmConsole,
        IReadOnlyList<StorageRack> storageRacks,
        Vec2 spawnPoint,
        string spawnRoomId,
        CardTable cardTable,
        float forwardDegrees = 0f,
        IReadOnlyList<ComponentMount>? componentMounts = null,
        Jukebox? jukebox = null,
        int reactorDeviceCount = 1,
        int distributionDeviceCount = 1,
        int helmDeviceCount = 1,
        int navigationDeviceCount = 1,
        IReadOnlyList<HelmConsole>? extraHelmConsoles = null,
        IReadOnlyList<NavigationConsole>? extraNavigationConsoles = null,
        IReadOnlyList<Vec2>? extraReactorPositions = null,
        IReadOnlyList<Vec2>? extraDistributionPositions = null,
        IReadOnlyList<ShipEngine>? engines = null,
        // True only for a Ship Editor-built hull (Ship.Custom.cs's FromCustomDefinition sets this) -
        // every hand-authored hull (CreateStarter/.Scout/.Cruiser/.Corvette) leaves it false. Gates
        // the Reactor's own zone-name penalty (World.Upgrades.cs's RecomputeReactorZonePenalty):
        // hand-authored hulls already use their own flavor room names ("Реакторная", "Реакторный
        // отсек" - never derived from this feature's zone picker), so checking those names against
        // the canonical zone label would misfire and penalize ships that were never built with zones
        // at all. Only a custom hull's room name is actually driven by the zone-type picker.
        bool isCustomBuilt = false)
    {
        IsCustomBuilt = isCustomBuilt;
        ForwardDegrees = forwardDegrees;
        ReactorDeviceCount = reactorDeviceCount;
        DistributionDeviceCount = distributionDeviceCount;
        HelmDeviceCount = helmDeviceCount;
        NavigationDeviceCount = navigationDeviceCount;
        ExtraHelmConsoles = extraHelmConsoles ?? Array.Empty<HelmConsole>();
        ExtraNavigationConsoles = extraNavigationConsoles ?? Array.Empty<NavigationConsole>();
        ExtraReactorPositions = extraReactorPositions ?? Array.Empty<Vec2>();
        ExtraDistributionPositions = extraDistributionPositions ?? Array.Empty<Vec2>();
        ComponentMounts = componentMounts ?? Array.Empty<ComponentMount>();
        Jukebox = jukebox;
        Rooms = rooms;
        Doors = doors;
        AirlockOuterDoors = airlockOuterDoors;
        Turrets = turrets;
        Cameras = cameras;
        AmmoStorages = ammoStorages;
        SuitLockers = suitLockers;
        SystemDevices = systemDevices;
        // A door is its own airtight seal now, open or closed (World.Atmosphere.cs already gates
        // room-to-room/vacuum leakage on IsDoorOpen directly) - it doesn't need a hull WallBlock
        // sitting underneath it too. GenerateOuterWallBlocks generates blindly along an edge's
        // whole length with no idea where a door was cut into it (e.g. the Corvette's shield-bay/
        // life-support flanks, each with an AirlockOuterDoor on an otherwise-solid side), so any
        // block that lands exactly on a door's own footprint is dropped here, once, for every hull.
        Engines = engines ?? Array.Empty<ShipEngine>();
        // A marching engine's own Bulkhead tile IS the hull plating at that spot (ShipEngine.cs's
        // own doc comment) - drops the ordinary WallBlock the room's own outer-wall generation would
        // otherwise ALSO place there, the same way a door's footprint already excludes one, so the
        // two don't silently coexist at (almost) the same position.
        WallBlocks = wallBlocks
            .Where(b => !doors.Any(d => d.Contains(b.Position)) && !airlockOuterDoors.Any(d => d.Contains(b.Position))
                && !Engines.Any(e => (e.BulkheadPosition - b.Position).Length() < 0.1))
            .ToList();
        ReactorBlock = reactorBlock;
        DistributionBlock = distributionBlock;
        BatteryBlock = batteryBlock;
        NavigationConsole = navigationConsole;
        HelmConsole = helmConsole;
        CardTable = cardTable;
        StorageRacks = storageRacks;
        SpawnPoint = spawnPoint;
        SpawnRoomId = spawnRoomId;
        _roomsById = rooms.ToDictionary(r => r.Id);
        Tiles = TileGridRasterizer.FromRooms(Rooms, Doors, AirlockOuterDoors);
        Devices = BuildDevices();
    }

    public Room GetRoom(string roomId) => _roomsById[roomId];

    // M74 - see Devices's own doc comment above. Runs once at construction time (device fixtures
    // are never added/removed after a Ship is built - Ship.Custom.cs.FromCustomDefinition always
    // constructs a brand new Ship rather than mutating one), same lifecycle as Tiles just above.
    private List<ShipDevice> BuildDevices()
    {
        var devices = new List<ShipDevice>
        {
            new(ReactorBlock.Id, DeviceKind.Reactor, ReactorBlock.RoomId, ReactorBlock.X, ReactorBlock.Y),
            new(DistributionBlock.Id, DeviceKind.Distribution, DistributionBlock.RoomId, DistributionBlock.X, DistributionBlock.Y),
            new(BatteryBlock.Id, DeviceKind.Battery, BatteryBlock.RoomId, BatteryBlock.X, BatteryBlock.Y),
            new(HelmConsole.Id, DeviceKind.Helm, HelmConsole.RoomId, HelmConsole.X, HelmConsole.Y),
            new(NavigationConsole.Id, DeviceKind.Navigation, NavigationConsole.RoomId, NavigationConsole.X, NavigationConsole.Y),
            new(CardTable.Id, DeviceKind.CardTable, CardTable.RoomId, CardTable.X, CardTable.Y),
        };

        // Extra reactor/distribution rooms beyond the first only ever carry a bare Vec2 (no Id, no
        // RoomId - Ship.cs's own doc comment on ExtraReactorPositions) - synthesize both here so
        // each still becomes a fully independent Devices entry rather than being dropped.
        devices.AddRange(ExtraReactorPositions.Select((p, i) =>
            new ShipDevice($"{ReactorBlock.Id}-extra-{i + 1}", DeviceKind.Reactor, RoomIdAt(p), (float)p.X, (float)p.Y)));
        devices.AddRange(ExtraDistributionPositions.Select((p, i) =>
            new ShipDevice($"{DistributionBlock.Id}-extra-{i + 1}", DeviceKind.Distribution, RoomIdAt(p), (float)p.X, (float)p.Y)));
        devices.AddRange(ExtraHelmConsoles.Select(c => new ShipDevice(c.Id, DeviceKind.Helm, c.RoomId, c.X, c.Y)));
        devices.AddRange(ExtraNavigationConsoles.Select(c => new ShipDevice(c.Id, DeviceKind.Navigation, c.RoomId, c.X, c.Y)));

        if (Jukebox is { } jukebox)
            devices.Add(new ShipDevice(jukebox.Id, DeviceKind.Jukebox, jukebox.RoomId, jukebox.X, jukebox.Y));

        // PowerSystemId.Secondary has no DeviceKind counterpart (ShipDevice.cs's own doc comment) -
        // a hull's "system-secondary" fixture stays on SystemDevices untouched, just absent here.
        foreach (var device in SystemDevices)
        {
            DeviceKind? kind = device.System switch
            {
                PowerSystemId.Oxygen => DeviceKind.Oxygen,
                PowerSystemId.Engine => DeviceKind.Engine,
                PowerSystemId.Shields => DeviceKind.Shields,
                PowerSystemId.WeaponCharger => DeviceKind.WeaponCharger,
                _ => null,
            };
            if (kind is { } k)
                devices.Add(new ShipDevice(device.Id, k, device.RoomId, device.X, device.Y,
                    ThrustBonus: device.ThrustBonus, TurnBonus: device.TurnBonus, CapacityBonus: device.CapacityBonus));
        }

        foreach (var turret in Turrets)
        {
            var kind = turret.WeaponType switch
            {
                TurretWeaponType.Magnetic => DeviceKind.TurretBallistic,
                TurretWeaponType.MachineGun => DeviceKind.TurretMachineGun,
                _ => DeviceKind.TurretLaser,
            };
            devices.Add(new ShipDevice(turret.Id, kind, turret.RoomId, turret.PeriscopeX, turret.PeriscopeY, MountSide: turret.MountSide));
        }

        devices.AddRange(AmmoStorages.Select(a => new ShipDevice(a.Id, DeviceKind.AmmoStorage, a.RoomId, a.X, a.Y)));
        devices.AddRange(SuitLockers.Select(s => new ShipDevice(s.Id, DeviceKind.SuitLocker, s.RoomId, s.X, s.Y)));
        devices.AddRange(StorageRacks.Select(s => new ShipDevice(s.Id, DeviceKind.StorageRack, s.RoomId, s.X, s.Y)));
        devices.AddRange(Cameras.Select(c => new ShipDevice(c.Id, DeviceKind.Camera, c.RoomId, c.X, c.Y, CameraSide: c.MountSide)));
        devices.AddRange(ComponentMounts.Select(m => new ShipDevice(m.Id, DeviceKind.ComponentMount, m.RoomId, m.X, m.Y, TargetDoorId: m.TargetDoorId)));

        return devices;
    }

    private string RoomIdAt(Vec2 position) => Rooms.FirstOrDefault(r => r.Contains(position))?.Id ?? SpawnRoomId;

    public static Ship Create(ShipKind kind) => kind switch
    {
        ShipKind.Scout => CreateScout(),
        ShipKind.Cruiser => CreateCruiser(),
        ShipKind.Corvette => CreateCorvette(),
        // Custom has no fixed layout to build here - callers must go through FromCustomDefinition
        // with the player's own CustomShipDefinition instead (World.cs, World.Save.cs).
        ShipKind.Custom => throw new InvalidOperationException("ShipKind.Custom has no fixed layout - use Ship.FromCustomDefinition."),
        _ => CreateStarter(),
    };

    // One 1x1 block per unit segment of whichever edges are actually outer hull (no neighboring
    // room on that side) — interior bulkheads between two pressurized rooms don't get blocks,
    // since there's nothing to decompress into on the other side.
    private static IEnumerable<WallBlock> GenerateOuterWallBlocks(
        Room room, bool top, bool bottom, bool left, bool right)
    {
        var index = 0;
        if (top)
            for (var x = room.Left; x < room.Right; x += 1f)
                yield return new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, room.Top);
        if (bottom)
            for (var x = room.Left; x < room.Right; x += 1f)
                yield return new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, room.Bottom);
        if (left)
            for (var y = room.Top; y < room.Bottom; y += 1f)
                yield return new WallBlock($"{room.Id}-wall-{index++}", room.Id, room.Left, y + 0.5f);
        if (right)
            for (var y = room.Top; y < room.Bottom; y += 1f)
                yield return new WallBlock($"{room.Id}-wall-{index++}", room.Id, room.Right, y + 0.5f);
    }

    // One wall block per unit segment of every INTERIOR boundary - detected purely from room
    // geometry (any two rooms whose rectangles share an edge segment), so every hull - hand-
    // authored or player-built (Ship.Custom.cs) - gets these automatically without listing room
    // pairs by hand. Unlike GenerateOuterWallBlocks these are tagged IsInterior: true (nothing to
    // decompress into on the other side, World.Atmosphere.cs/AtmosphereParticles.cs both skip
    // them for exactly that reason) but are otherwise ordinary WallBlocks - just as solid to a shot
    // (World.EnemyAi.cs treats every WallBlock alike) and repaired the same way. A door footprint
    // cut into the boundary is filtered out afterward by the same pass the constructor already
    // runs for outer wall blocks.
    private static IEnumerable<WallBlock> GenerateInteriorWallBlocks(IReadOnlyList<Room> rooms)
    {
        const float Epsilon = 0.01f;
        var index = 0;
        for (var i = 0; i < rooms.Count; i++)
        {
            for (var j = i + 1; j < rooms.Count; j++)
            {
                var a = rooms[i];
                var b = rooms[j];

                if (Math.Abs(a.Right - b.Left) < Epsilon || Math.Abs(b.Right - a.Left) < Epsilon)
                {
                    var sharedX = Math.Abs(a.Right - b.Left) < Epsilon ? a.Right : a.Left;
                    var overlapTop = Math.Max(a.Top, b.Top);
                    var overlapBottom = Math.Min(a.Bottom, b.Bottom);
                    for (var y = overlapTop; y < overlapBottom - Epsilon; y += 1f)
                        yield return new WallBlock($"{a.Id}-{b.Id}-wall-{index++}", a.Id, sharedX, y + 0.5f, IsInterior: true, OtherRoomId: b.Id);
                }
                else if (Math.Abs(a.Bottom - b.Top) < Epsilon || Math.Abs(b.Bottom - a.Top) < Epsilon)
                {
                    var sharedY = Math.Abs(a.Bottom - b.Top) < Epsilon ? a.Bottom : a.Top;
                    var overlapLeft = Math.Max(a.Left, b.Left);
                    var overlapRight = Math.Min(a.Right, b.Right);
                    for (var x = overlapLeft; x < overlapRight - Epsilon; x += 1f)
                        yield return new WallBlock($"{a.Id}-{b.Id}-wall-{index++}", a.Id, x + 0.5f, sharedY, IsInterior: true, OtherRoomId: b.Id);
                }
            }
        }
    }

    // Moves along a single axis at a time (call once for X, once for Y — see World.Step):
    // stay inside the current room's AABB by default; cross into a connected room only through
    // an aligned, currently-open Door, or a wide-enough breach in an interior wall between two
    // rooms (isPassableBreach - World.WallBlocks.cs's IsPassableBreach, wired in from
    // World.Movement.cs); otherwise stop at the wall. A closed door blocks crossing exactly like
    // solid hull (game_design.md Phase 3, M16 - airtight compartments). No walls yet block crossing
    // outside a room's own bounds if it isn't adjacent to any room at all (open space / outside the
    // hull) - that transition is World.Eva.cs's own, separate exterior-hull-breach path.
    // M73 - now backed by Tiles/TileMovement instead of the Rooms/Doors rectangle-clamp.
    // isDoorOpen/isPassableBreach are unused here (door-open state and breach passability are
    // already baked into TileCell via World.TileSync.cs/TileGrid.IsWalkable) - kept as parameters
    // purely so World.Movement.cs's existing call site doesn't need to change.
    public (Vec2 Position, string RoomId) MoveAlongAxis(Vec2 position, string roomId, Vec2 delta, Func<string, bool> isDoorOpen,
        Func<WallBlock, bool>? isPassableBreach = null)
    {
        var next = TileMovement.MoveAlongAxis(Tiles, position, delta, DeviceObstacles);
        return (next, TileMovement.RoomIdAt(Rooms, next) ?? roomId);
    }

    // The reactor's own machine (a catalog/editor room's reference art bakes the whole thing right
    // into the room's own floor texture - RoomDecor's "texture doubles as the device" rule, so
    // ShipRenderer.DrawReactorBlock draws nothing extra there any more) - a character's own body has
    // to actually be blocked by it rather than walking straight through the artwork. Sized to 60% of
    // the room's own width/height (a tighter fit than the room itself, matching just the machine's
    // own outline in the art, not the surrounding floor/wall-frame around it), which still leaves a
    // walkway around all four sides for the crew to reach it from any direction. Only the reactor
    // for now (RoomLayout.RoomObstacle is meant to grow one entry per "big fixture" module as each is
    // worked through the same way, not just this one).
    private IReadOnlyList<RoomLayout.RoomObstacle> DeviceObstacles
    {
        get
        {
            var room = GetRoom(ReactorBlock.RoomId);
            // Only where there's actually a big machine drawn to match (RoomCatalog.
            // NamesWithReferenceArt) - every hand-authored hull's own reactor room (a plain
            // procedural floor, no reference art) gets no obstacle at all, same as before this
            // feature existed. Without this gate the obstacle used to swallow the room's only door
            // on a normal hull (e.g. the Frigate's 5x6 "reactor" room), stranding anyone trying to
            // walk through it - found via several tests hanging on exactly that stuck pathing.
            if (!RoomCatalog.NamesWithReferenceArt.Contains(room.Name))
                return Array.Empty<RoomLayout.RoomObstacle>();
            return new[] { new RoomLayout.RoomObstacle(ReactorBlock.RoomId, ReactorBlock.Position, new Vec2(room.Width * 0.3, room.Height * 0.3)) };
        }
    }

    public static Ship CreateStarter()
    {
        var rooms = new[]
        {
            new Room("cockpit", "Кокпит", 0, 0, 5, 6),
            new Room("reactor", "Реакторная", 5, 0, 5, 6),
            new Room("corridor", "Коридор", 10, 0, 3, 6),
            new Room("quarters", "Каюты", 13, 0, 5, 6),
            new Room("engine", "Машинное отделение", 18, 0, 5, 6),
            // Small airtight chamber appended at the row's far end (game_design.md Phase 3, M16):
            // one normal door in from engine, one AirlockOuterDoor out to vacuum.
            new Room("airlock-chamber", "Шлюзовая камера", 23, 0, 3, 6),
        };

        // Doors sit on the shared vertical wall between adjacent rooms, open around the row's
        // mid-height (y=3) — walking near the top/bottom of a room still hits a solid wall.
        var doors = new[]
        {
            new Door("door-cockpit-reactor", "cockpit", "reactor", 5, 3, 1.0f, Door.StandardSpanUnits),
            new Door("door-reactor-corridor", "reactor", "corridor", 10, 3, 1.0f, Door.StandardSpanUnits),
            new Door("door-corridor-quarters", "corridor", "quarters", 13, 3, 1.0f, Door.StandardSpanUnits),
            new Door("door-quarters-engine", "quarters", "engine", 18, 3, 1.0f, Door.StandardSpanUnits),
            new Door("door-engine-airlock", "engine", "airlock-chamber", 23, 3, 1.0f, Door.StandardSpanUnits),
        };

        // The chamber's far wall - opens onto vacuum, not another room (game_design.md Phase 3,
        // M16). No interlock with door-engine-airlock: opening both at once really does vent the
        // whole ship, same as leaving both real airlock doors open.
        var airlockOuterDoors = new[]
        {
            new AirlockOuterDoor("door-airlock-vacuum", "airlock-chamber", 26, 3, 1.0f, Door.StandardSpanUnits),
        };

        // Two turrets (Phase1 MVP: "1-2 орудия"): bow ballistic in the cockpit, and the laser —
        // "единственное исключение" per game_design.md section 2 — in the reactor room, where
        // it's thematically wired to the power grid it draws its capacitor charge from.
        var turrets = new[]
        {
            new Turret("turret-bow", "cockpit", PeriscopeX: 1.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: TurretBalance.MagneticDamage,
                CooldownSeconds: TurretBalance.MagneticCooldownSeconds, WeaponType: TurretWeaponType.Magnetic,
                MagazineCapacity: TurretBalance.MagneticMagazineCapacity),
            new Turret("turret-laser", "reactor", PeriscopeX: 6.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: TurretBalance.LaserDamagePerTick,
                CooldownSeconds: TurretBalance.LaserTickIntervalSeconds, WeaponType: TurretWeaponType.Laser,
                MaxCharge: TurretBalance.LaserMaxCharge, ChargePerShot: TurretBalance.LaserChargePerTick,
                RechargePerPowerUnitPerSecond: TurretBalance.LaserRechargePerPowerUnitPerSecond),
        };

        // Two hull cameras, bow and stern (M48 - "камеры как устройства корабля"): junction boxes
        // a crew member walks up to and wires/repairs like any other system, HullCameraMount
        // derives their actual outward-facing position on the plating from MountSide. Kept clear
        // of the bow turret's own periscope (1.5, 3) by more than InteractionRadius.
        var cameras = new[]
        {
            new HullCamera("camera-bow", "cockpit", X: 3.5f, Y: 5f, CameraMountSide.Fore),
            new HullCamera("camera-stern", "airlock-chamber", X: 24f, Y: 1f, CameraMountSide.Aft),
        };

        // Ammo storage lives in quarters — deliberately far from the bow turret so hauling a
        // crate across the ship (game_design.md section 2) is a real trip, not a formality.
        var ammoStorages = new[]
        {
            new AmmoStorage("ammo-storage-quarters", "quarters", X: 15f, Y: 3f),
        };

        // Suit locker lives in the engine room — a third destination spread across the ship
        // alongside the turret (cockpit) and ammo storage (quarters).
        var suitLockers = new[]
        {
            new SuitLocker("suit-locker-engine", "engine", X: 20f, Y: 3f),
        };

        // Every breaker panel hangs in the reactor room, spaced apart rather than lined up one
        // behind another, so running a wire from the distribution block to any of them is a short,
        // uncluttered trip instead of a walk across the whole ship - same consolidation as the
        // Corvette's reactor hall (Ship.Corvette.cs). Shields is the one system with two physical
        // generators (design doc §1 — "несколько генераторов щита в разных частях корпуса"),
        // matching its two drop links in WireNetwork - both still live here, not one per hull side.
        // system-oxygen is the one exception: it stays in the corridor, because its RoomId is where
        // the generator actually pumps air into (World.Atmosphere.cs), not just a panel location -
        // moving it would relocate life support to a different compartment, not just tidy up wiring.
        var systemDevices = new[]
        {
            new ShipSystemDevice("system-shields", "reactor", X: 7.2f, Y: 0.7f, PowerSystemId.Shields),
            new ShipSystemDevice("system-shields-2", "reactor", X: 8.6f, Y: 1.6f, PowerSystemId.Shields),
            new ShipSystemDevice("system-weapon-charger", "reactor", X: 7.6f, Y: 2.2f, PowerSystemId.WeaponCharger),
            new ShipSystemDevice("system-oxygen", "corridor", X: 12.5f, Y: 1.5f, PowerSystemId.Oxygen),
            new ShipSystemDevice("system-secondary", "reactor", X: 8.5f, Y: 3.8f, PowerSystemId.Secondary),
            new ShipSystemDevice("system-engine", "reactor", X: 7.2f, Y: 4.3f, PowerSystemId.Engine),
            // Paired engine block, as every class now carries (WireNetwork.CreateDefault).
            new ShipSystemDevice("system-engine-2", "reactor", X: 8.5f, Y: 5.2f, PowerSystemId.Engine),
        };

        // Reactor is a big, clickable block; the distribution block sits right next to it
        // (game_design.md section 1 — "Distribution-блок рядом с реактором").
        var reactorBlock = new ReactorBlock("reactor-block", "reactor", X: 9.5f, Y: 1f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "reactor", X: 9.5f, Y: 3f);
        var batteryBlock = new BatteryBlock("battery-block", "reactor", X: 9.5f, Y: 5f);

        // Helm console on the bridge (game_design.md Phase 3, M15) — stand here to take manual
        // control of the ship in open space. Moved to the cockpit's own forward bulkhead (low X,
        // the nose side - GenerateOuterWallBlocks(rooms[0], left: true) marks X=0 as outer hull)
        // rather than the mid-room spot it used to occupy, so the captain stands right up against
        // the nose (M47 follow-up - "впередней части кокпита"). Kept clear of the bow turret's
        // periscope (1.5, 3) and the card table (4, 1) by more than InteractionRadius (1.0).
        var helmConsole = new HelmConsole("helm-console", "cockpit", X: 1.4f, Y: 1.3f);

        // The scanner console (game_design.md section 5/M44) - right next to the helm (M47), both
        // now at the cockpit's forward bulkhead so they read as one bridge station pair the
        // captain and the scientist share, rather than one of them standing off on its own.
        var navigationConsole = new NavigationConsole("navigation-console", "cockpit", X: 2.8f, Y: 1.3f);

        // A quiet corner of the cockpit, clear of the nav console/helm/turret/mount above - two
        // crew standing here together starts a hand of Дурак переводной (World.CardGame.cs).
        var cardTable = new CardTable("card-table", "cockpit", X: 4f, Y: 1f);

        // Aft of the card table, clear of the turret periscope (1.5, 3) and every console above.
        var jukebox = new Jukebox("jukebox", "cockpit", X: 4f, Y: 4.5f);

        // Outer-hull wall blocks: every room's top/bottom is exterior (the ship is one row
        // wide); only cockpit's left and the airlock chamber's right are exterior side walls not
        // covered by a dedicated door — engine's former right-side hull is now the door to the
        // chamber, and the chamber's own right side is the dedicated AirlockOuterDoor above rather
        // than random breachable hull (it's a small deliberate compartment, not open combat armor).
        var wallBlocks = new List<WallBlock>();
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: true, left: true, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[1], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[2], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[3], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[4], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[5], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateInteriorWallBlocks(rooms));

        // Two shelves: quarters (the one room that isn't already crowded with machinery) and engine
        // (World.ShipPurchase.cs's InitializeRackSlots seeds the crew's starter gear between them).
        var storageRacks = new[]
        {
            new StorageRack("rack-quarters", "quarters", X: 16f, Y: 1.5f),
            new StorageRack("rack-engine", "engine", X: 20f, Y: 5f),
        };

        // Empty sockets for purchasable logic/sensor/actuator parts (World.ComponentMounts.cs,
        // game_design.md section 1's wiring) - spread one or two per room, not one per possible
        // kind, since the player chooses what to install where. One sits by the airlock door
        // specifically for an AutoDoorController.
        var componentMounts = new[]
        {
            new ComponentMount("mount-cockpit-1", "cockpit", X: 1.5f, Y: 5f),
            new ComponentMount("mount-reactor-1", "reactor", X: 6f, Y: 1.5f),
            new ComponentMount("mount-corridor-1", "corridor", X: 12.5f, Y: 5f),
            new ComponentMount("mount-quarters-1", "quarters", X: 13.5f, Y: 5f),
            new ComponentMount("mount-quarters-2", "quarters", X: 17.5f, Y: 4.5f),
            new ComponentMount("mount-engine-door", "engine", X: 22f, Y: 4f, TargetDoorId: "door-engine-airlock"),
        };

        var corridor = rooms.First(r => r.Id == "corridor");
        return new Ship(rooms, doors, airlockOuterDoors, turrets, cameras, ammoStorages, suitLockers, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, batteryBlock, navigationConsole, helmConsole, storageRacks, corridor.Center, corridor.Id,
            cardTable, componentMounts: componentMounts, jukebox: jukebox);
    }
}
