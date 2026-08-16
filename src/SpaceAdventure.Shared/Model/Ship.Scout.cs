namespace SpaceAdventure.Shared.Model;

// ShipKind.Scout — the cheapest, weakest class (game_design.md section 9): 2 rooms instead of the
// Frigate's 5, one turret instead of two, and no personal ranged weapons at all (Knife only) —
// deliberately weaker than the Frigate, not just smaller, so the price tiers in section 9 actually
// mean something once purchasing exists.
public sealed partial class Ship
{
    public static Ship CreateScout()
    {
        var rooms = new[]
        {
            new Room("bridge", "Мостик", 0, 0, 5, 6),
            new Room("engine", "Машинное отделение", 5, 0, 6, 6),
            new Room("airlock-chamber", "Шлюзовая камера", 11, 0, 3, 6),
        };

        var doors = new[]
        {
            new Door("door-bridge-engine", "bridge", "engine", 5, 3, 1.0f, 1.8f),
            new Door("door-engine-airlock", "engine", "airlock-chamber", 11, 3, 1.0f, 1.8f),
        };

        var airlockOuterDoors = new[]
        {
            new AirlockOuterDoor("door-airlock-vacuum", "airlock-chamber", 14, 3, 1.0f, 1.8f),
        };

        // Single bow gun, and a weaker one than the Frigate's (game_design.md section 9 — cheap
        // class, fewer/weaker systems).
        var turrets = new[]
        {
            new Turret("turret-bow", "bridge", PeriscopeX: 1.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 8f, CooldownSeconds: 0.6f,
                WeaponType: TurretWeaponType.Ballistic, MagazineCapacity: 5),
        };

        var ammoStorages = new[]
        {
            new AmmoStorage("ammo-storage-bridge", "bridge", X: 2.5f, Y: 5f),
        };

        var suitLockers = new[]
        {
            new SuitLocker("suit-locker-engine", "engine", X: 10f, Y: 5f),
        };

        // No Rifle/LaserRifle station — a Scout crew has nothing but a Knife to fall back on if
        // boarded, part of what makes this the weakest class rather than just the smallest.
        var toolStations = new[]
        {
            new ToolStation("armory-bridge-knife", "bridge", X: 1f, Y: 5f, ItemType.Knife),
            new ToolStation("medkit-bridge", "bridge", X: 4f, Y: 5f, ItemType.MedKit),
            new ToolStation("toolbox-engine-wrench", "engine", X: 6f, Y: 5f, ItemType.Wrench),
            new ToolStation("toolbox-engine-screwdriver", "engine", X: 7f, Y: 5f, ItemType.Screwdriver),
            new ToolStation("toolbox-engine-welding", "engine", X: 8f, Y: 5f, ItemType.WeldingTool),
            new ToolStation("toolbox-engine-cutter", "engine", X: 9f, Y: 5f, ItemType.Cutter),
            new ToolStation("wirespool-engine", "engine", X: 5.5f, Y: 4f, ItemType.WireSpool),
            new ToolStation("rod-rack-engine", "engine", X: 10.5f, Y: 3.5f, ItemType.FuelRod),
            new ToolStation("tank-rack-engine", "engine", X: 9f, Y: 3.5f, ItemType.OxygenTank), // beside the suit locker
        };

        // Same 6 device ids/PowerSystemIds as every other class, just fewer rooms to spread them
        // across — keeps WireNetwork.CreateDefault() (which reuses these ids as its node ids)
        // working unmodified for every ship class.
        var systemDevices = new[]
        {
            new ShipSystemDevice("system-shields", "bridge", X: 4.5f, Y: 1f, PowerSystemId.Shields),
            new ShipSystemDevice("system-shields-2", "engine", X: 10.5f, Y: 1f, PowerSystemId.Shields),
            new ShipSystemDevice("system-weapon-charger", "bridge", X: 2f, Y: 1f, PowerSystemId.WeaponCharger),
            new ShipSystemDevice("system-oxygen", "engine", X: 9.5f, Y: 1f, PowerSystemId.Oxygen),
            new ShipSystemDevice("system-secondary", "engine", X: 10.5f, Y: 3f, PowerSystemId.Secondary),
            new ShipSystemDevice("system-engine", "engine", X: 5.5f, Y: 1f, PowerSystemId.Engine),
            // Second engine block: every class carries the pair the wiring topology expects
            // (WireNetwork.CreateDefault), and on a hull this small they sit side by side.
            new ShipSystemDevice("system-engine-2", "engine", X: 6.5f, Y: 1f, PowerSystemId.Engine),
        };

        var reactorBlock = new ReactorBlock("reactor-block", "engine", X: 7f, Y: 1f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "engine", X: 7f, Y: 3f);
        var navigationConsole = new NavigationConsole("navigation-console", "bridge", X: 1f, Y: 1f);
        var airlockConsole = new AirlockConsole("airlock-console", "bridge", X: 3f, Y: 1f);
        var wiringTerminal = new WiringTerminal("wiring-terminal", "engine", X: 5.5f, Y: 3f);
        var helmConsole = new HelmConsole("helm-console", "bridge", X: 4f, Y: 3f);

        var wallBlocks = new List<WallBlock>();
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: true, left: true, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[1], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[2], top: true, bottom: true, left: false, right: false));

        var bridge = rooms.First(r => r.Id == "bridge");
        // The scout has no dedicated hold, so the rack shares the engine room.
        var storageRack = new StorageRack("rack-engine", "engine", X: 8f, Y: 1.5f);
        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, toolStations, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, navigationConsole, airlockConsole, wiringTerminal, helmConsole, storageRack, bridge.Center, bridge.Id);
    }
}
