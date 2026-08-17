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
        var helmConsole = new HelmConsole("helm-console", "bridge", X: 4f, Y: 3f);

        var wallBlocks = new List<WallBlock>();
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: true, left: true, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[1], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[2], top: true, bottom: true, left: false, right: false));

        var bridge = rooms.First(r => r.Id == "bridge");
        // No dedicated hold on this hull - both shelves share the ship's only two rooms.
        var storageRacks = new[]
        {
            new StorageRack("rack-engine", "engine", X: 8f, Y: 1.5f),
            new StorageRack("rack-bridge", "bridge", X: 4.5f, Y: 5f),
        };

        var componentMounts = new[]
        {
            new ComponentMount("mount-bridge-1", "bridge", X: 2f, Y: 4.5f),
            new ComponentMount("mount-engine-1", "engine", X: 6.5f, Y: 4.5f),
            new ComponentMount("mount-engine-door", "engine", X: 9.5f, Y: 4f, TargetDoorId: "door-engine-airlock"),
        };

        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, navigationConsole, airlockConsole, helmConsole, storageRacks, bridge.Center, bridge.Id,
            componentMounts: componentMounts);
    }
}
