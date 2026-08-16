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
    public IReadOnlyList<AmmoStorage> AmmoStorages { get; }
    public IReadOnlyList<SuitLocker> SuitLockers { get; }
    public IReadOnlyList<ToolStation> ToolStations { get; }
    public IReadOnlyList<ShipSystemDevice> SystemDevices { get; }
    public IReadOnlyList<WallBlock> WallBlocks { get; }
    public ReactorBlock ReactorBlock { get; }
    public PowerDistributionBlock DistributionBlock { get; }
    public NavigationConsole NavigationConsole { get; }
    public AirlockConsole AirlockConsole { get; }
    public WiringTerminal WiringTerminal { get; }
    public HelmConsole HelmConsole { get; }
    public StorageRack StorageRack { get; }
    public Vec2 SpawnPoint { get; }
    public string SpawnRoomId { get; }
    // Which way this hull points when it flies, in its own layout coordinates. The classes laid out
    // as a row of compartments travel along +X, so 0; a hull built down the screen has to lead with
    // its nose instead, or it drifts through space broadside-on. Used only to pick the rotation
    // that matches the current velocity (World.ShipField.cs) - everything else still works in the
    // ship's own unrotated frame.
    public float ForwardDegrees { get; }

    private readonly Dictionary<string, Room> _roomsById;

    public Ship(
        IReadOnlyList<Room> rooms,
        IReadOnlyList<Door> doors,
        IReadOnlyList<AirlockOuterDoor> airlockOuterDoors,
        IReadOnlyList<Turret> turrets,
        IReadOnlyList<AmmoStorage> ammoStorages,
        IReadOnlyList<SuitLocker> suitLockers,
        IReadOnlyList<ToolStation> toolStations,
        IReadOnlyList<ShipSystemDevice> systemDevices,
        IReadOnlyList<WallBlock> wallBlocks,
        ReactorBlock reactorBlock,
        PowerDistributionBlock distributionBlock,
        NavigationConsole navigationConsole,
        AirlockConsole airlockConsole,
        WiringTerminal wiringTerminal,
        HelmConsole helmConsole,
        StorageRack storageRack,
        Vec2 spawnPoint,
        string spawnRoomId,
        float forwardDegrees = 0f)
    {
        ForwardDegrees = forwardDegrees;
        Rooms = rooms;
        Doors = doors;
        AirlockOuterDoors = airlockOuterDoors;
        Turrets = turrets;
        AmmoStorages = ammoStorages;
        SuitLockers = suitLockers;
        ToolStations = toolStations;
        SystemDevices = systemDevices;
        WallBlocks = wallBlocks;
        ReactorBlock = reactorBlock;
        DistributionBlock = distributionBlock;
        NavigationConsole = navigationConsole;
        AirlockConsole = airlockConsole;
        WiringTerminal = wiringTerminal;
        HelmConsole = helmConsole;
        StorageRack = storageRack;
        SpawnPoint = spawnPoint;
        SpawnRoomId = spawnRoomId;
        _roomsById = rooms.ToDictionary(r => r.Id);
    }

    public Room GetRoom(string roomId) => _roomsById[roomId];

    public static Ship Create(ShipKind kind) => kind switch
    {
        ShipKind.Scout => CreateScout(),
        ShipKind.Cruiser => CreateCruiser(),
        ShipKind.Corvette => CreateCorvette(),
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

    // Moves along a single axis at a time (call once for X, once for Y — see World.Step):
    // stay inside the current room's AABB by default; cross into a connected room only through
    // an aligned, currently-open Door; otherwise stop at the wall. A closed door blocks crossing
    // exactly like solid hull (game_design.md Phase 3, M16 - airtight compartments). No walls yet
    // block crossing outside a room's own bounds if it isn't adjacent to any room at all (open
    // space / outside the hull).
    public (Vec2 Position, string RoomId) MoveAlongAxis(Vec2 position, string roomId, Vec2 delta, Func<string, bool> isDoorOpen) =>
        RoomLayout.MoveAlongAxis(Rooms, Doors, position, roomId, delta, isDoorOpen);

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
            // one normal door in from engine, one AirlockOuterDoor out to vacuum. Distinct name
            // from the existing "Шлюз" AirlockConsole button (docking/NPCs, unrelated feature).
            new Room("airlock-chamber", "Шлюзовая камера", 23, 0, 3, 6),
        };

        // Doors sit on the shared vertical wall between adjacent rooms, open around the row's
        // mid-height (y=3) — walking near the top/bottom of a room still hits a solid wall.
        var doors = new[]
        {
            new Door("door-cockpit-reactor", "cockpit", "reactor", 5, 3, 1.0f, 1.8f),
            new Door("door-reactor-corridor", "reactor", "corridor", 10, 3, 1.0f, 1.8f),
            new Door("door-corridor-quarters", "corridor", "quarters", 13, 3, 1.0f, 1.8f),
            new Door("door-quarters-engine", "quarters", "engine", 18, 3, 1.0f, 1.8f),
            new Door("door-engine-airlock", "engine", "airlock-chamber", 23, 3, 1.0f, 1.8f),
        };

        // The chamber's far wall - opens onto vacuum, not another room (game_design.md Phase 3,
        // M16). No interlock with door-engine-airlock: opening both at once really does vent the
        // whole ship, same as leaving both real airlock doors open.
        var airlockOuterDoors = new[]
        {
            new AirlockOuterDoor("door-airlock-vacuum", "airlock-chamber", 26, 3, 1.0f, 1.8f),
        };

        // Two turrets (Phase1 MVP: "1-2 орудия"): bow ballistic in the cockpit, and the laser —
        // "единственное исключение" per game_design.md section 2 — in the reactor room, where
        // it's thematically wired to the power grid it draws its capacitor charge from.
        var turrets = new[]
        {
            new Turret("turret-bow", "cockpit", PeriscopeX: 1.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 10f, CooldownSeconds: 0.5f,
                WeaponType: TurretWeaponType.Ballistic, MagazineCapacity: 6),
            new Turret("turret-laser", "reactor", PeriscopeX: 6.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 8f, CooldownSeconds: 0.4f,
                WeaponType: TurretWeaponType.Laser, MaxCharge: 30f, ChargePerShot: 10f, RechargePerPowerUnitPerSecond: 0.5f),
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

        // Hand tools and personal weapons (game_design.md sections 2, 3, 7), one station each,
        // spread across every room so no single stop hands out everything.
        var toolStations = new[]
        {
            new ToolStation("toolbox-reactor-wrench", "reactor", X: 7f, Y: 5f, ItemType.Wrench),
            new ToolStation("toolbox-reactor-screwdriver", "reactor", X: 9f, Y: 5f, ItemType.Screwdriver),
            new ToolStation("toolbox-corridor-welding", "corridor", X: 11.5f, Y: 5f, ItemType.WeldingTool),
            new ToolStation("toolbox-engine-cutter", "engine", X: 21.5f, Y: 5f, ItemType.Cutter),
            new ToolStation("armory-quarters-knife", "quarters", X: 14f, Y: 5f, ItemType.Knife),
            new ToolStation("armory-quarters-rifle", "quarters", X: 17f, Y: 5f, ItemType.Rifle),
            new ToolStation("armory-cockpit-laser-rifle", "cockpit", X: 3.5f, Y: 5f, ItemType.LaserRifle),
            new ToolStation("rod-rack-reactor", "reactor", X: 7.5f, Y: 1f, ItemType.FuelRod),
            new ToolStation("medkit-quarters", "quarters", X: 16f, Y: 5f, ItemType.MedKit),
            new ToolStation("wirespool-engine", "engine", X: 22.5f, Y: 1.5f, ItemType.WireSpool),
            // Oxygen tanks beside the suit locker: a suit without one keeps nobody alive and a
            // cutter without one won't light (OxygenTankDefinitions), so they belong where the crew
            // suits up rather than somewhere else on the deck.
            new ToolStation("tank-rack-engine", "engine", X: 19f, Y: 1.5f, ItemType.OxygenTank),
        };

        // One physical, damageable block per power-grid system (game_design.md section 1), one
        // per room so every room has something the enemy AI can knock out locally. Shields is the
        // one system with two physical generators (design doc §1 — "несколько генераторов щита в
        // разных частях корпуса"), matching its two drop links in WireNetwork.
        var systemDevices = new[]
        {
            new ShipSystemDevice("system-shields", "cockpit", X: 3.5f, Y: 1.5f, PowerSystemId.Shields),
            new ShipSystemDevice("system-shields-2", "quarters", X: 13.5f, Y: 1.5f, PowerSystemId.Shields),
            new ShipSystemDevice("system-weapon-charger", "reactor", X: 5.5f, Y: 1.5f, PowerSystemId.WeaponCharger),
            new ShipSystemDevice("system-oxygen", "corridor", X: 12.5f, Y: 1.5f, PowerSystemId.Oxygen),
            new ShipSystemDevice("system-secondary", "quarters", X: 15.5f, Y: 1.5f, PowerSystemId.Secondary),
            new ShipSystemDevice("system-engine", "engine", X: 19f, Y: 1.5f, PowerSystemId.Engine),
            // Paired engine block, as every class now carries (WireNetwork.CreateDefault).
            new ShipSystemDevice("system-engine-2", "engine", X: 19f, Y: 4.5f, PowerSystemId.Engine),
        };

        // Reactor is a big, clickable block; the distribution block sits right next to it
        // (game_design.md section 1 — "Distribution-блок рядом с реактором").
        var reactorBlock = new ReactorBlock("reactor-block", "reactor", X: 9.5f, Y: 1f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "reactor", X: 9.5f, Y: 3f);

        // Navigation console on the bridge (game_design.md section 5) — click it to bring up
        // the galaxy map.
        var navigationConsole = new NavigationConsole("navigation-console", "cockpit", X: 1.5f, Y: 1.5f);

        // Airlock in the corridor (game_design.md section 10) — click it while docked to visit
        // the station's NPCs.
        var airlockConsole = new AirlockConsole("airlock-console", "corridor", X: 10.5f, Y: 1.5f);

        // Wiring terminal next to the distribution block (game_design.md section 1, M14) — click
        // it to bring up the wiring schematic instead of the ship view.
        var wiringTerminal = new WiringTerminal("wiring-terminal", "reactor", X: 8f, Y: 3f);

        // Helm console on the bridge (game_design.md Phase 3, M15) — stand here to take manual
        // control of the ship in open space. Kept away from the bow turret's periscope (1.5, 3)
        // and the laser rifle armory (3.5, 5) so their interaction radii don't overlap with this.
        var helmConsole = new HelmConsole("helm-console", "cockpit", X: 3f, Y: 4f);

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

        // Cargo shelving in the crew quarters - the one room that isn't already crowded with
        // machinery, and the natural place to keep what you're hauling.
        var storageRack = new StorageRack("rack-quarters", "quarters", X: 16f, Y: 1.5f);

        var corridor = rooms.First(r => r.Id == "corridor");
        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, toolStations, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, navigationConsole, airlockConsole, wiringTerminal, helmConsole, storageRack, corridor.Center, corridor.Id);
    }
}
