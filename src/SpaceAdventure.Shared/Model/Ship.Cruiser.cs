namespace SpaceAdventure.Shared.Model;

// ShipKind.Cruiser — the top tier (game_design.md section 9): everything the Frigate has, plus an
// extra "hold" room carrying a third turret and a second ammo cache, so it reads as strictly more
// ship, not just a reskin. Same 6 power-system device ids/rooms as the Frigate otherwise — only
// the hold room and its stern turret are actually new.
public sealed partial class Ship
{
    public static Ship CreateCruiser()
    {
        var rooms = new[]
        {
            new Room("cockpit", "Кокпит", 0, 0, 5, 6),
            new Room("reactor", "Реакторная", 5, 0, 5, 6),
            new Room("corridor", "Коридор", 10, 0, 3, 6),
            new Room("quarters", "Каюты", 13, 0, 5, 6),
            new Room("hold", "Трюм", 18, 0, 5, 6),
            new Room("engine", "Машинное отделение", 23, 0, 5, 6),
            new Room("airlock-chamber", "Шлюзовая камера", 28, 0, 3, 6),
        };

        var doors = new[]
        {
            new Door("door-cockpit-reactor", "cockpit", "reactor", 5, 3, 1.0f, 1.8f),
            new Door("door-reactor-corridor", "reactor", "corridor", 10, 3, 1.0f, 1.8f),
            new Door("door-corridor-quarters", "corridor", "quarters", 13, 3, 1.0f, 1.8f),
            new Door("door-quarters-hold", "quarters", "hold", 18, 3, 1.0f, 1.8f),
            new Door("door-hold-engine", "hold", "engine", 23, 3, 1.0f, 1.8f),
            new Door("door-engine-airlock", "engine", "airlock-chamber", 28, 3, 1.0f, 1.8f),
        };

        var airlockOuterDoors = new[]
        {
            new AirlockOuterDoor("door-airlock-vacuum", "airlock-chamber", 31, 3, 1.0f, 1.8f),
        };

        // Three guns total (bow, laser, stern) vs. the Frigate's two — the "больше... орудий" top
        // tier from game_design.md section 9. Stern is a second ballistic turret (no new
        // PowerSystemId needed — it draws ammo like the bow, not reactor charge like the laser).
        var turrets = new[]
        {
            new Turret("turret-bow", "cockpit", PeriscopeX: 1.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 10f, CooldownSeconds: 0.5f,
                WeaponType: TurretWeaponType.Ballistic, MagazineCapacity: 6),
            new Turret("turret-laser", "reactor", PeriscopeX: 6.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 8f, CooldownSeconds: 0.4f,
                WeaponType: TurretWeaponType.Laser, MaxCharge: 30f, ChargePerShot: 10f, RechargePerPowerUnitPerSecond: 0.5f),
            new Turret("turret-stern", "hold", PeriscopeX: 19.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 10f, CooldownSeconds: 0.5f,
                WeaponType: TurretWeaponType.Ballistic, MagazineCapacity: 6),
        };

        var ammoStorages = new[]
        {
            new AmmoStorage("ammo-storage-quarters", "quarters", X: 15f, Y: 3f),
            new AmmoStorage("ammo-storage-hold", "hold", X: 20.5f, Y: 5f),
        };

        var suitLockers = new[]
        {
            new SuitLocker("suit-locker-engine", "engine", X: 25f, Y: 3f),
        };

        var toolStations = new[]
        {
            new ToolStation("toolbox-reactor-wrench", "reactor", X: 7f, Y: 5f, ItemType.Wrench),
            new ToolStation("toolbox-reactor-screwdriver", "reactor", X: 9f, Y: 5f, ItemType.Screwdriver),
            new ToolStation("toolbox-corridor-welding", "corridor", X: 11.5f, Y: 5f, ItemType.WeldingTool),
            new ToolStation("toolbox-engine-cutter", "engine", X: 26.5f, Y: 5f, ItemType.Cutter),
            new ToolStation("armory-quarters-knife", "quarters", X: 14f, Y: 5f, ItemType.Knife),
            new ToolStation("armory-quarters-rifle", "quarters", X: 17f, Y: 5f, ItemType.Rifle),
            new ToolStation("armory-cockpit-laser-rifle", "cockpit", X: 3.5f, Y: 5f, ItemType.LaserRifle),
            new ToolStation("rod-rack-reactor", "reactor", X: 7.5f, Y: 1f, ItemType.FuelRod),
            new ToolStation("medkit-quarters", "quarters", X: 16f, Y: 5f, ItemType.MedKit),
            new ToolStation("wirespool-engine", "engine", X: 27.5f, Y: 1.5f, ItemType.WireSpool),
            new ToolStation("tank-rack-engine", "engine", X: 25.5f, Y: 1.5f, ItemType.OxygenTank), // beside the suit locker
        };

        var systemDevices = new[]
        {
            new ShipSystemDevice("system-shields", "cockpit", X: 3.5f, Y: 1.5f, PowerSystemId.Shields),
            new ShipSystemDevice("system-shields-2", "quarters", X: 13.5f, Y: 1.5f, PowerSystemId.Shields),
            new ShipSystemDevice("system-weapon-charger", "reactor", X: 5.5f, Y: 1.5f, PowerSystemId.WeaponCharger),
            new ShipSystemDevice("system-oxygen", "corridor", X: 12.5f, Y: 1.5f, PowerSystemId.Oxygen),
            new ShipSystemDevice("system-secondary", "quarters", X: 15.5f, Y: 1.5f, PowerSystemId.Secondary),
            new ShipSystemDevice("system-engine", "engine", X: 24f, Y: 1.5f, PowerSystemId.Engine),
            // Paired engine block, as every class now carries (WireNetwork.CreateDefault).
            new ShipSystemDevice("system-engine-2", "engine", X: 24f, Y: 4.5f, PowerSystemId.Engine),
        };

        var reactorBlock = new ReactorBlock("reactor-block", "reactor", X: 9.5f, Y: 1f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "reactor", X: 9.5f, Y: 3f);
        var navigationConsole = new NavigationConsole("navigation-console", "cockpit", X: 1.5f, Y: 1.5f);
        var airlockConsole = new AirlockConsole("airlock-console", "corridor", X: 10.5f, Y: 1.5f);
        var wiringTerminal = new WiringTerminal("wiring-terminal", "reactor", X: 8f, Y: 3f);
        var helmConsole = new HelmConsole("helm-console", "cockpit", X: 3f, Y: 4f);

        var wallBlocks = new List<WallBlock>();
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: true, left: true, right: false));
        for (var i = 1; i < rooms.Length; i++)
            wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[i], top: true, bottom: true, left: false, right: false));

        var corridor = rooms.First(r => r.Id == "corridor");
        // The cruiser has an actual hold - the rack belongs there rather than in the crew quarters.
        var hold = rooms.First(r => r.Id == "hold");
        var storageRack = new StorageRack("rack-hold", hold.Id, X: hold.Center.X, Y: hold.Top + 1.5f);
        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, toolStations, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, navigationConsole, airlockConsole, wiringTerminal, helmConsole, storageRack, corridor.Center, corridor.Id);
    }
}
