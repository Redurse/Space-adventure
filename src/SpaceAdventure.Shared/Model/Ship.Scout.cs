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
            new Door("door-bridge-engine", "bridge", "engine", 5, 3, 1.0f, Door.StandardSpanUnits),
            new Door("door-engine-airlock", "engine", "airlock-chamber", 11, 3, 1.0f, Door.StandardSpanUnits),
        };

        var airlockOuterDoors = new[]
        {
            new AirlockOuterDoor("door-airlock-vacuum", "airlock-chamber", 14, 3, 1.0f, Door.StandardSpanUnits),
        };

        // Single bow gun, and a weaker one than the Frigate's (game_design.md section 9 — cheap
        // class, fewer/weaker systems).
        var turrets = new[]
        {
            new Turret("turret-bow", "bridge", PeriscopeX: 1.5f, PeriscopeY: 3f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: TurretBalance.MagneticDamage,
                CooldownSeconds: TurretBalance.MagneticCooldownSeconds, WeaponType: TurretWeaponType.Magnetic,
                MagazineCapacity: TurretBalance.MagneticMagazineCapacity),
        };

        // Two hull cameras, bow and stern (M48) - same bow/stern split as every other class, kept
        // clear of the bow turret's periscope (1.5, 3) and the helm (4, 3).
        var cameras = new[]
        {
            new HullCamera("camera-bow", "bridge", X: 1.5f, Y: 5f, CameraMountSide.Fore),
            new HullCamera("camera-stern", "airlock-chamber", X: 12f, Y: 1f, CameraMountSide.Aft),
        };

        var ammoStorages = new[]
        {
            new AmmoStorage("ammo-storage-bridge", "bridge", X: 2.5f, Y: 5f),
        };

        var suitLockers = new[]
        {
            new SuitLocker("suit-locker-engine", "engine", X: 10f, Y: 5f),
        };

        // Same 6 device ids/PowerSystemIds as every other class. No separate reactor room on a
        // hull this small - the engine room carries the reactor/distribution blocks directly, so
        // it's the one that plays that role, and every breaker panel hangs there too, spaced apart
        // rather than lined up in a row - only system-oxygen stays out of it, since its RoomId is
        // where the generator actually feeds air (World.Atmosphere.cs), not just a panel location.
        var systemDevices = new[]
        {
            new ShipSystemDevice("system-shields", "engine", X: 5.8f, Y: 1f, PowerSystemId.Shields),
            new ShipSystemDevice("system-shields-2", "engine", X: 5.8f, Y: 3f, PowerSystemId.Shields),
            new ShipSystemDevice("system-weapon-charger", "engine", X: 5.8f, Y: 5f, PowerSystemId.WeaponCharger),
            new ShipSystemDevice("system-oxygen", "engine", X: 9.5f, Y: 1f, PowerSystemId.Oxygen),
            new ShipSystemDevice("system-secondary", "engine", X: 10.3f, Y: 3f, PowerSystemId.Secondary),
            new ShipSystemDevice("system-engine", "engine", X: 7.3f, Y: 5.3f, PowerSystemId.Engine),
            // Second engine block: every class carries the pair the wiring topology expects
            // (WireNetwork.CreateDefault) - spaced away from its twin now, not sitting side by side.
            new ShipSystemDevice("system-engine-2", "engine", X: 9f, Y: 5.3f, PowerSystemId.Engine),
        };

        var reactorBlock = new ReactorBlock("reactor-block", "engine", X: 7f, Y: 1f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "engine", X: 7f, Y: 3f);
        var batteryBlock = new BatteryBlock("battery-block", "engine", X: 8.7f, Y: 2.3f);
        var navigationConsole = new NavigationConsole("navigation-console", "bridge", X: 1f, Y: 1f);
        var helmConsole = new HelmConsole("helm-console", "bridge", X: 4f, Y: 3f);
        // Two crew standing here together starts a hand of Дурак переводной (World.CardGame.cs).
        var cardTable = new CardTable("card-table", "bridge", X: 3f, Y: 1f);

        // Bottom-right corner, clear of the ammo storage (2.5, 5) and the helm (4, 3).
        var jukebox = new Jukebox("jukebox", "bridge", X: 4.5f, Y: 5f);

        var wallBlocks = new List<WallBlock>();
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: true, left: true, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[1], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[2], top: true, bottom: true, left: false, right: false));
        wallBlocks.AddRange(GenerateInteriorWallBlocks(rooms));

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

        return new Ship(rooms, doors, airlockOuterDoors, turrets, cameras, ammoStorages, suitLockers, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, batteryBlock, navigationConsole, helmConsole, storageRacks, bridge.Center, bridge.Id,
            cardTable, componentMounts: componentMounts, jukebox: jukebox);
    }
}
