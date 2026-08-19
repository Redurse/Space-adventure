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

        // Every breaker panel hangs in the reactor room, spaced apart rather than lined up one
        // behind another (Ship.cs's Frigate carries the same layout and reasoning) - system-oxygen
        // is the one exception, since its RoomId is where the generator actually feeds air
        // (World.Atmosphere.cs), not just a panel location.
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

        var reactorBlock = new ReactorBlock("reactor-block", "reactor", X: 9.5f, Y: 1f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "reactor", X: 9.5f, Y: 3f);
        var batteryBlock = new BatteryBlock("battery-block", "reactor", X: 9.5f, Y: 5f);
        var navigationConsole = new NavigationConsole("navigation-console", "cockpit", X: 1.5f, Y: 1.5f);
        var helmConsole = new HelmConsole("helm-console", "cockpit", X: 3f, Y: 4f);
        var cardTable = new CardTable("card-table", "cockpit", X: 4f, Y: 1f);

        var wallBlocks = new List<WallBlock>();
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: true, left: true, right: false));
        for (var i = 1; i < rooms.Length; i++)
            wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[i], top: true, bottom: true, left: false, right: false));

        var corridor = rooms.First(r => r.Id == "corridor");
        // The cruiser has an actual hold - one shelf belongs there, the other in the crew quarters.
        var hold = rooms.First(r => r.Id == "hold");
        var storageRacks = new[]
        {
            new StorageRack("rack-hold", hold.Id, X: hold.Center.X, Y: hold.Top + 1.5f),
            new StorageRack("rack-quarters", "quarters", X: 16.5f, Y: 5f),
        };

        var componentMounts = new[]
        {
            new ComponentMount("mount-cockpit-1", "cockpit", X: 1.5f, Y: 5f),
            new ComponentMount("mount-cockpit-2", "cockpit", X: 4f, Y: 4.5f),
            new ComponentMount("mount-reactor-1", "reactor", X: 6f, Y: 1.5f),
            new ComponentMount("mount-corridor-1", "corridor", X: 12.5f, Y: 5f),
            new ComponentMount("mount-quarters-1", "quarters", X: 13.5f, Y: 5f),
            new ComponentMount("mount-quarters-2", "quarters", X: 17.5f, Y: 4.5f),
            new ComponentMount("mount-hold-1", "hold", X: 19f, Y: 5f),
            new ComponentMount("mount-engine-1", "engine", X: 24f, Y: 5f),
            new ComponentMount("mount-engine-2", "engine", X: 26f, Y: 4.5f),
            new ComponentMount("mount-engine-door", "engine", X: 27f, Y: 4f, TargetDoorId: "door-engine-airlock"),
        };

        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, batteryBlock, navigationConsole, helmConsole, storageRacks, corridor.Center, corridor.Id,
            cardTable, componentMounts: componentMounts);
    }
}
