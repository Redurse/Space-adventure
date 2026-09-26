namespace Anabiosis.Shared.Model;

// Direct user request ("удали все текущие корабли в разделе начать новую игру... полностью удалить
// из кода") - every fixed/hand-authored hull class (Frigate/Scout/Cruiser/Corvette/Destroyer/
// Freighter) is gone; ShipKind is just Custom now, built exclusively through FromCustomDefinition
// (Ship.Custom.cs). This file holds only the shared Ship type/generic helpers every hull (player-
// drawn or the frozen default, ShipDefaultHull.cs) goes through alike.
public sealed partial class Ship
{
    public IReadOnlyList<Room> Rooms { get; }
    // A vacuum-facing door (Door.LeadsToVacuum - humble-soaring-cat.md, "убрать AirlockOuterDoor
    // как отдельный тип") lives in this SAME list, not a separate one - VacuumDoors below is a
    // computed filter over it, not an independent source of truth. Order matters: Ship.Custom.cs
    // appends hull doors AFTER interior ones, so VacuumDoors[0] stays the same door the old
    // AirlockOuterDoors[0] used to be (World.StationDocking.cs's ResolveShipAirlock/World.Eva.cs's
    // TryCrossIntoVacuum both key off that first entry as the ship's own primary connector).
    public IReadOnlyList<Door> Doors { get; }
    public IReadOnlyList<Door> VacuumDoors { get; }
    // M-doors-as-edges (humble-soaring-cat.md) - narrow doors that sit on the EDGE between 2
    // already-free floor tiles instead of occupying a tile themselves. Empty for every hand-
    // authored hull (CreateStarter/.Scout/.Cruiser/.Corvette/.CatalogHulls never place one) - only
    // ever populated by a Ship Editor-built hull (Ship.Custom.cs's FromCustomDefinition).
    public IReadOnlyList<ShipDoorEdge> DoorEdges { get; }
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
    // M71 (humble-soaring-cat.md) - additive projection of Rooms/Doors/WallBlocks
    // onto the new tile-grid model (TileGrid.cs). Nobody reads this yet outside tests; it exists
    // purely to prove the projection is lossless before any dependent system (atmosphere, movement,
    // rendering...) migrates to it one milestone at a time.
    public TileGrid Tiles { get; }
    // Direct user bug report ("стены отображаются не на своих местах, а коллизии там же") -
    // TileShipBuilder.BuildDefinition's own steps 3.5/3.6 (a T-junction's residual wall, or a
    // half-block notch sitting at a region's own edge) can produce extra wall/floor tiles that
    // don't fit into any Room's own Rects at all - Ship.Custom.cs's FromCustomDefinition paints
    // them directly onto THIS Ship's own Tiles right after construction, which is correct for
    // SERVER-side collision (TileMovement.cs reads Tiles directly) but was NEVER reaching the
    // CLIENT: WorldSnapshot only ever networked Rooms/Doors/WallBlocks, and the
    // client rebuilds its OWN copy of Tiles purely by re-running TileGridRasterizer.FromRooms on
    // those - the exact same "naive" rasterization that steps 3.5/3.6 exist to CORRECT, so the
    // client's rendering silently reverted to the wrong, uncorrected geometry while the server's
    // real collision (built from THIS list, once, here) stayed right. These 3 lists are Ship's own
    // record of exactly which post-rasterization corrections were applied, so WorldSnapshot can
    // hand them to the client to replay the identical fix (ClientTileGrid.ApplySupplementalTiles).
    // Empty for every hand-authored hull (CreateStarter/.Scout/.Cruiser/.Corvette never need this
    // correction - their Room rects are hand-tuned to already include their own wall ring).
    public IReadOnlyList<TileCoord> SupplementalWallTiles { get; }
    public IReadOnlyList<TileCoord> ForcedFloorTiles { get; }
    public IReadOnlyList<CustomWallOpenSideDef> WallOpenSideOverrides { get; }
    // Same reasoning as WallOpenSideOverrides just above, for WallMaterial instead - a non-Standard
    // material (Reinforced/Window) on a tile that fell into SupplementalWallTiles (never gets a real
    // WallBlock) would otherwise be invisible to the client the exact same way a half-block notch's
    // WallOpenSide used to be.
    public IReadOnlyList<CustomWallMaterialDef> WallMaterialOverrides { get; }
    // Direct user request ("на месте взорванного отсека будут обломки") - hull-local footprints of
    // every compartment that has exploded (World.RoomHp.cs's ExplodeRoom), for ShipRenderer to draw a
    // static wreck decoration over - see CustomShipDefinition.WreckPatches' own doc comment for why
    // this (unlike SupplementalWallTiles/ForcedFloorTiles/WallOpenSideOverrides above) is explicitly
    // round-tripped through Ship.ToDefinition().
    public IReadOnlyList<RectF> WreckPatches { get; }
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
    // The wall terminals' physical positions - direct user request ("это в будущем будет одно из
    // главных устройств, их будет много"): many independent instances per hull, each with its own
    // on/off state (World.Terminals.cs), same "list of fixtures, each with its own Id" shape
    // SuitLockers/AmmoStorages/Turrets already use, not a single optional flavor device any more.
    public IReadOnlyList<Terminal> Terminals { get; }
    // Direct user request ("настенную лампу... когда она установлена, она излучает свет") - same
    // list-of-fixtures shape as Terminals, no server-side state of its own at all (purely passive,
    // Game1.Lighting.cs lights it whenever the ship's own lamps are on).
    public IReadOnlyList<WallLamp> WallLamps { get; }
    // Direct user request ("у тебя есть проблема что всех этих 4 устройств на корабле может быть
    // только по одному") - many independent instances, same list-of-fixtures shape as WallLamps
    // just above (not a fixture every hull gets - Ship Editor only, no hand-authored hull places
    // either).
    public IReadOnlyList<ShipStatusMonitor> ShipStatusMonitors { get; }
    public IReadOnlyList<CommsConsole> CommsConsoles { get; }
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
    // Direct user request ("сделай чтобы на корабле могло быть несколько батарей") - same "bonus,
    // not list" shape as ReactorDeviceCount/ExtraReactorPositions just above: BatteryBlock still
    // only ever comes from the FIRST placed Battery device (or the auto-placed fallback, if the
    // player never placed one at all - see BuildSimpleDevices's own doc comment), so this always
    // reads at least 1 even when the raw device count is 0. World.ShipBuilding.cs's
    // RecomputeDeviceBonuses turns extras into real stored-energy capacity (Battery.CapacityBonus),
    // mirroring exactly how an extra reactor already turns into extra output.
    public int BatteryDeviceCount { get; }
    public IReadOnlyList<Vec2> ExtraBatteryPositions { get; }
    // Direct user bug report ("щитки отображались в игре а не была просто пустота") - the "Щиток"
    // fixture (JunctionBox.cs's own doc comment) is purely decorative, same shape as WallLamps.
    public IReadOnlyList<JunctionBox> JunctionBoxes { get; }
    // Direct user bug report ("некоторые устройства в игре не отображаются а в редакторе они
    // видны") - every remaining CustomDeviceKind with no dedicated mechanic (DecorativeDevice.Kinds),
    // generalized instead of giving each its own JunctionBox-style record.
    public IReadOnlyList<DecorativeDevice> DecorativeDevices { get; }

    private readonly Dictionary<string, Room> _roomsById;

    public Ship(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<Door> doors,
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
        IReadOnlyList<Terminal>? terminals = null,
        IReadOnlyList<WallLamp>? wallLamps = null,
        IReadOnlyList<ShipStatusMonitor>? shipStatusMonitors = null,
        IReadOnlyList<CommsConsole>? commsConsoles = null,
        int reactorDeviceCount = 1,
        int distributionDeviceCount = 1,
        int helmDeviceCount = 1,
        int navigationDeviceCount = 1,
        IReadOnlyList<HelmConsole>? extraHelmConsoles = null,
        IReadOnlyList<NavigationConsole>? extraNavigationConsoles = null,
        IReadOnlyList<Vec2>? extraReactorPositions = null,
        IReadOnlyList<Vec2>? extraDistributionPositions = null,
        int batteryDeviceCount = 1,
        IReadOnlyList<Vec2>? extraBatteryPositions = null,
        IReadOnlyList<JunctionBox>? junctionBoxes = null,
        IReadOnlyList<DecorativeDevice>? decorativeDevices = null,
        IReadOnlyList<ShipEngine>? engines = null,
        IReadOnlyList<TileCoord>? supplementalWallTiles = null,
        IReadOnlyList<TileCoord>? forcedFloorTiles = null,
        IReadOnlyList<CustomWallOpenSideDef>? wallOpenSideOverrides = null,
        IReadOnlyList<CustomWallMaterialDef>? wallMaterialOverrides = null,
        IReadOnlyList<ShipDoorEdge>? doorEdges = null,
        // True only for a Ship Editor-built hull (Ship.Custom.cs's FromCustomDefinition sets this) -
        // every hand-authored hull (CreateStarter/.Scout/.Cruiser/.Corvette) leaves it false. Gates
        // the Reactor's own zone-name penalty (World.Upgrades.cs's RecomputeReactorZonePenalty):
        // hand-authored hulls already use their own flavor room names ("Реакторная", "Реакторный
        // отсек" - never derived from this feature's zone picker), so checking those names against
        // the canonical zone label would misfire and penalize ships that were never built with zones
        // at all. Only a custom hull's room name is actually driven by the zone-type picker.
        bool isCustomBuilt = false,
        IReadOnlyList<RectF>? wreckPatches = null)
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
        BatteryDeviceCount = batteryDeviceCount;
        ExtraBatteryPositions = extraBatteryPositions ?? Array.Empty<Vec2>();
        JunctionBoxes = junctionBoxes ?? Array.Empty<JunctionBox>();
        DecorativeDevices = decorativeDevices ?? Array.Empty<DecorativeDevice>();
        ComponentMounts = componentMounts ?? Array.Empty<ComponentMount>();
        Jukebox = jukebox;
        Terminals = terminals ?? Array.Empty<Terminal>();
        WallLamps = wallLamps ?? Array.Empty<WallLamp>();
        ShipStatusMonitors = shipStatusMonitors ?? Array.Empty<ShipStatusMonitor>();
        CommsConsoles = commsConsoles ?? Array.Empty<CommsConsole>();
        Rooms = rooms;
        Doors = doors;
        VacuumDoors = doors.Where(d => d.LeadsToVacuum).ToList();
        DoorEdges = doorEdges ?? Array.Empty<ShipDoorEdge>();
        Turrets = turrets;
        Cameras = cameras;
        AmmoStorages = ammoStorages;
        SuitLockers = suitLockers;
        SystemDevices = systemDevices;
        // A door is its own airtight seal now, open or closed (World.Atmosphere.cs already gates
        // room-to-room/vacuum leakage on IsDoorOpen directly) - it doesn't need a hull WallBlock
        // sitting underneath it too. GenerateOuterWallBlocks generates blindly along an edge's
        // whole length with no idea where a door was cut into it (e.g. the Corvette's shield-bay/
        // life-support flanks, each with a vacuum-facing door on an otherwise-solid side), so any
        // block that lands exactly on a door's own footprint is dropped here, once, for every hull.
        Engines = engines ?? Array.Empty<ShipEngine>();
        SupplementalWallTiles = supplementalWallTiles ?? Array.Empty<TileCoord>();
        ForcedFloorTiles = forcedFloorTiles ?? Array.Empty<TileCoord>();
        WallOpenSideOverrides = wallOpenSideOverrides ?? Array.Empty<CustomWallOpenSideDef>();
        WallMaterialOverrides = wallMaterialOverrides ?? Array.Empty<CustomWallMaterialDef>();
        WreckPatches = wreckPatches ?? Array.Empty<RectF>();
        // A marching engine's own Bulkhead tile IS the hull plating at that spot (ShipEngine.cs's
        // own doc comment) - drops the ordinary WallBlock the room's own outer-wall generation would
        // otherwise ALSO place there, the same way a door's footprint already excludes one, so the
        // two don't silently coexist at (almost) the same position.
        WallBlocks = wallBlocks
            .Where(b => !doors.Any(d => d.Contains(b.Position))
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
        Tiles = TileGridRasterizer.FromRooms(Rooms, Doors);
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

        devices.AddRange(Terminals.Select(t => new ShipDevice(t.Id, DeviceKind.Terminal, t.RoomId, t.X, t.Y)));
        devices.AddRange(WallLamps.Select(l => new ShipDevice(l.Id, DeviceKind.WallLamp, l.RoomId, l.X, l.Y)));

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

    // Direct user request ("удали все текущие корабли... полностью удалить из кода") - ShipKind is
    // just Custom now, which has no fixed layout of its own - every real caller goes through
    // FromCustomDefinition (World.cs, World.Save.cs), falling back to ShipDefaultHull.Definition
    // when no CustomShipDefinition was actually supplied. Kept as a single throwing method (not
    // deleted outright) only so any straggling `Ship.Create(...)` call site fails loudly at the
    // call, rather than compiling into something silently wrong.
    public static Ship Create(ShipKind kind) =>
        throw new InvalidOperationException("ShipKind has no fixed layout any more - use Ship.FromCustomDefinition.");

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
    // Generalized (humble-soaring-cat.md M90) to run against every room's flattened subrects
    // instead of assuming one rectangle per room - a same-room subrect pair is skipped outright
    // (internal seam, never a wall - two pieces of the same multi-rect room are one continuous
    // space). Byte-identical to the old per-room math whenever every room has exactly one rect
    // (every hand-authored hull, forever), since RoomGeometry.Flatten produces exactly one RoomRect
    // per such room.
    private static IEnumerable<WallBlock> GenerateInteriorWallBlocks(IReadOnlyList<Room> rooms)
    {
        const float Epsilon = 0.01f;
        var index = 0;
        var flat = RoomGeometry.Flatten(rooms);
        for (var i = 0; i < flat.Count; i++)
        {
            for (var j = i + 1; j < flat.Count; j++)
            {
                var a = flat[i];
                var b = flat[j];
                if (a.RoomId == b.RoomId)
                    continue;

                if (Math.Abs(a.Rect.Right - b.Rect.X) < Epsilon || Math.Abs(b.Rect.Right - a.Rect.X) < Epsilon)
                {
                    var sharedX = Math.Abs(a.Rect.Right - b.Rect.X) < Epsilon ? a.Rect.Right : a.Rect.X;
                    var overlapTop = Math.Max(a.Rect.Y, b.Rect.Y);
                    var overlapBottom = Math.Min(a.Rect.Bottom, b.Rect.Bottom);
                    for (var y = overlapTop; y < overlapBottom - Epsilon; y += 1f)
                        yield return new WallBlock($"{a.RoomId}-{b.RoomId}-wall-{index++}", a.RoomId, sharedX, y + 0.5f, IsInterior: true, OtherRoomId: b.RoomId);
                }
                else if (Math.Abs(a.Rect.Bottom - b.Rect.Y) < Epsilon || Math.Abs(b.Rect.Bottom - a.Rect.Y) < Epsilon)
                {
                    var sharedY = Math.Abs(a.Rect.Bottom - b.Rect.Y) < Epsilon ? a.Rect.Bottom : a.Rect.Y;
                    var overlapLeft = Math.Max(a.Rect.X, b.Rect.X);
                    var overlapRight = Math.Min(a.Rect.Right, b.Rect.Right);
                    for (var x = overlapLeft; x < overlapRight - Epsilon; x += 1f)
                        yield return new WallBlock($"{a.RoomId}-{b.RoomId}-wall-{index++}", a.RoomId, x + 0.5f, sharedY, IsInterior: true, OtherRoomId: b.RoomId);
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
        var next = TileMovement.MoveAlongAxis(Tiles, position, delta, DeviceObstacles.Concat(WallDeviceObstacles).ToList());
        return (next, TileMovement.RoomIdAt(Rooms, next) ?? roomId);
    }

    // Direct user request ("в таком случае будет полностью заполнен тайл") - a Terminal/WallLamp
    // always fully blocks its own (X,Y) tile once placed, regardless of whether it's recessed in a
    // half-thick wall's own free half or protruding onto an ordinary floor tile from a ordinary
    // wall - both modes end up needing the exact same shape here, a plain full-unit box centered on
    // the device's own already-exported tile-center position (TileShipBuilder's own convention).
    // Unlike DeviceObstacles' own Reactor gating, this applies to every ship unconditionally - these
    // two device kinds always have a real, well-defined 1x1 footprint, hand-authored or custom alike.
    private IReadOnlyList<RoomLayout.RoomObstacle> WallDeviceObstacles
    {
        get
        {
            var halfExtents = new Vec2(0.5, 0.5);
            var obstacles = new List<RoomLayout.RoomObstacle>(Terminals.Count + WallLamps.Count);
            obstacles.AddRange(Terminals.Select(t => new RoomLayout.RoomObstacle(t.RoomId, t.Position, halfExtents)));
            obstacles.AddRange(WallLamps.Select(l => new RoomLayout.RoomObstacle(l.RoomId, l.Position, halfExtents)));
            return obstacles;
        }
    }

    // The reactor's own machine (a catalog/editor room's reference art bakes the whole thing right
    // into the room's own floor texture - RoomDecor's "texture doubles as the device" rule, so
    // ShipRenderer.DrawReactorBlock draws nothing extra there any more) - a character's own body has
    // to actually be blocked by it rather than walking straight through the artwork. Sized to 60% of
    // the room's own width/height (a tighter fit than the room itself, matching just the machine's
    // own outline in the art, not the surrounding floor/wall-frame around it), which still leaves a
    // walkway around all four sides for the crew to reach it from any direction.
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
            if (RoomCatalog.NamesWithReferenceArt.Contains(room.Name))
                return new[] { new RoomLayout.RoomObstacle(ReactorBlock.RoomId, ReactorBlock.Position, new Vec2(room.Width * 0.3, room.Height * 0.3)) };

            // Direct user request ("сделай чтобы у реактора была коллизия и через него нельзя было
            // пройти") - a custom-built ship (Ship Editor, CompartmentCatalog reactor-a/b/c/d or a
            // free-placed Reactor device) never gets the reference-art heuristic above (its room is
            // never named exactly "Реакторный отсек"), so it used to have NO collision at all. Unlike
            // the heuristic above, a custom ship's Reactor has a real, exact tile footprint
            // (CustomDeviceFootprint.Size) that CompartmentPlacer/TileShipBuilder already guarantee
            // sits strictly interior with a walkway on every side (see
            // CompartmentCatalog_EveryEntry_HasDevicesStrictlyInteriorAndInBounds) - so the obstacle
            // can just use that exact size directly, no heuristic shrink needed, and it can never
            // swallow a door the way the old blanket rule did on a hand-authored hull.
            if (!IsCustomBuilt)
                return Array.Empty<RoomLayout.RoomObstacle>();

            var (footprintWidth, footprintHeight) = CustomDeviceFootprint.Size(CustomDeviceKind.Reactor);
            var halfExtents = new Vec2(footprintWidth / 2f, footprintHeight / 2f);
            // Bug fix (found live: a minimal hand-authored CustomShipDefinition test fixture placed
            // its own Reactor in a plain 4x4 room with none of the compartment catalog's guaranteed
            // clearance - the full-size obstacle swallowed nearly the whole room, trapping a
            // character trying to cross it and starving them of oxygen through a breached bulkhead
            // elsewhere on the same tiny ship before they could ever get free). Only trust the exact
            // footprint size when the room it landed in is actually big enough to leave a walkway on
            // every side (the same >=1-unit-clearance margin the old reference-art heuristic's own
            // doc comment describes) - skip the obstacle entirely otherwise, same as a hand-authored
            // hull with no reference art gets today.
            bool RoomHasClearance(string roomId)
            {
                var r = GetRoom(roomId);
                return r.Width >= footprintWidth + 2 && r.Height >= footprintHeight + 2;
            }

            var obstacles = new List<RoomLayout.RoomObstacle>();
            if (RoomHasClearance(ReactorBlock.RoomId))
                obstacles.Add(new RoomLayout.RoomObstacle(ReactorBlock.RoomId, ReactorBlock.Position, halfExtents));
            obstacles.AddRange(ExtraReactorPositions
                .Select(p => (RoomId: RoomIdAt(p), Position: p))
                .Where(x => RoomHasClearance(x.RoomId))
                .Select(x => new RoomLayout.RoomObstacle(x.RoomId, x.Position, halfExtents)));
            return obstacles;
        }
    }


    // M-doors-as-edges follow-up (direct user request, "удали все текущие корабли... полностью
    // удалить из кода") - CreateStarter() used to live here, the original M2 layout backing
    // ShipKind.Frigate. Its exact shape survives as data, not code - captured once via a DIAG=1
    // diagnostic (Ship.CreateStarter().ToDefinition()) into ShipDefaultHull.cs, which is what a
    // fresh World/GameServer/SoloSession now falls back to when no CustomShipDefinition is
    // supplied, so those hundreds of pre-existing callers keep behaving identically.
}
