namespace SpaceAdventure.Shared.Model;

// ShipKind.Corvette — the first hull laid out along its own axis instead of as a row of boxes:
// bow at the top, stern at the bottom, with two long side compartments hung off the reactor deck.
// Five compartments: cockpit, gun deck and reactor hall down the spine, with the shield bay to port
// and life support to starboard, each carrying a docking port, its suit locker and an engine at the
// very bottom. The side bays run past the end of the spine, so the hull's tail is a pair of engine
// pylons with the reactor hall between them.
//
// The layout drives the fighting: the two guns sit on opposite walls of the gun deck and fire out
// through the plating either side, which is a broadside, so raiders holding station off the beam
// (World.EnemyFleet.cs) are exactly what these guns are for. Both side compartments carry their own
// docking port, so the ship can mate with a station from either beam.
//
// Everything here is drawn at ~70% of the layout's first draft - the whole hull, its fittings and
// the reactor/engine blocks scaled together, so the proportions are untouched and only the size
// changed. Room edges land on halves of a unit, which keeps the per-unit hull blocks tidy.
public sealed partial class Ship
{
    public static Ship CreateCorvette()
    {
        // x: 0..4 port compartment | 4..9.5 spine | 9.5..13.5 starboard compartment
        // y: 0..4 cockpit, 4..8 gun deck, 8..15 reactor hall, 15..18.5 airlock
        //
        // The cockpit is set in half a unit from the spine's flanks on each side. Rooms have to stay
        // rectangles - the walking and the sight lines are separated per axis against them - but the
        // hull they add up to does not have to be one, and that step is what gives the bow a taper
        // instead of a flat face the width of the whole ship.
        var rooms = new[]
        {
            new Room("cockpit", "Кокпит", 4.5f, 0, 4.5f, 4),
            new Room("armory", "Оружейная", 4, 4, 5.5f, 4),
            new Room("reactor", "Реакторный отсек", 4, 8, 5.5f, 7),
            new Room("shields-bay", "Щитовая", 0, 8, 4, 10.5f),
            new Room("life-support", "Отсек жизнеобеспечения", 9.5f, 8, 4, 10.5f),
        };

        // Down the spine, then out to both sides from the reactor hall - the reactor deck is the
        // junction of the ship, which is why the breaker panels live there too.
        var doors = new[]
        {
            new Door("door-cockpit-armory", "cockpit", "armory", 6.75f, 4, 1.8f, 1.0f),
            new Door("door-armory-reactor", "armory", "reactor", 6.75f, 8, 1.8f, 1.0f),
            new Door("door-reactor-shields", "reactor", "shields-bay", 4, 11, 1.0f, 1.8f),
            new Door("door-reactor-lifesupport", "reactor", "life-support", 9.5f, 11, 1.0f, 1.8f),
        };

        // One port on each beam, on the outer wall of each side compartment (they're the only rooms
        // that reach the hull's flanks). Either one mates with a station, and either one is the way
        // out into vacuum - which is why the suit lockers stand right next to them rather than in
        // some other compartment you'd have to cross the ship from.
        var airlockOuterDoors = new[]
        {
            new AirlockOuterDoor("door-airlock-vacuum", "life-support", 13.5f, 9.5f, 1.0f, 1.8f),
            new AirlockOuterDoor("door-airlock-port", "shields-bay", 0, 9.5f, 1.0f, 1.8f),
        };

        // Two guns on opposite walls of the gun deck, firing out through their own plating
        // (TurretMount reads MountSide). The starboard gun is the one that bears on a raider
        // holding the standard standoff; the port gun covers the other beam once you turn.
        var turrets = new[]
        {
            new Turret("turret-starboard", "armory", PeriscopeX: 8.5f, PeriscopeY: 6f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 10f, CooldownSeconds: 0.5f,
                WeaponType: TurretWeaponType.Ballistic, MagazineCapacity: 6, MountSide: TurretMountSide.Starboard),
            new Turret("turret-port", "armory", PeriscopeX: 5f, PeriscopeY: 6f,
                MinAimDegrees: -45f, MaxAimDegrees: 45f, DamagePerShot: 8f, CooldownSeconds: 0.4f,
                WeaponType: TurretWeaponType.Laser, MaxCharge: 30f, ChargePerShot: 10f,
                RechargePerPowerUnitPerSecond: 0.5f, MountSide: TurretMountSide.Port),
        };

        var ammoStorages = new[]
        {
            new AmmoStorage("ammo-storage-armory", "armory", X: 6.75f, Y: 5f),
        };

        var suitLockers = new[]
        {
            new SuitLocker("suit-locker-port", "shields-bay", X: 1.2f, Y: 10.6f),
            new SuitLocker("suit-locker-starboard", "life-support", X: 12.3f, Y: 10.6f),
        };

        var toolStations = new[]
        {
            new ToolStation("armory-rifle", "armory", X: 5f, Y: 4.8f, ItemType.Rifle),
            new ToolStation("armory-laser-rifle", "armory", X: 8.5f, Y: 4.8f, ItemType.LaserRifle),
            new ToolStation("armory-knife", "armory", X: 6.75f, Y: 7.2f, ItemType.Knife),
            new ToolStation("medkit-cockpit", "cockpit", X: 8.7f, Y: 3.2f, ItemType.MedKit),
            new ToolStation("toolbox-reactor-wrench", "reactor", X: 4.6f, Y: 8.8f, ItemType.Wrench),
            new ToolStation("toolbox-reactor-screwdriver", "reactor", X: 5.4f, Y: 8.8f, ItemType.Screwdriver),
            new ToolStation("toolbox-reactor-welding", "reactor", X: 6.2f, Y: 8.8f, ItemType.WeldingTool),
            new ToolStation("toolbox-reactor-cutter", "reactor", X: 7f, Y: 8.8f, ItemType.Cutter),
            new ToolStation("wirespool-reactor", "reactor", X: 7.8f, Y: 8.8f, ItemType.WireSpool),
            new ToolStation("rod-rack-reactor", "reactor", X: 8.8f, Y: 8.8f, ItemType.FuelRod),
            // A tank rack at each suit locker, since this hull has an airlock on either beam.
            new ToolStation("tank-rack-port", "shields-bay", X: 1.2f, Y: 12.4f, ItemType.OxygenTank),
            new ToolStation("tank-rack-starboard", "life-support", X: 12.3f, Y: 12.4f, ItemType.OxygenTank),
        };

        // Every breaker panel hangs in the reactor hall, as asked - one place to run to when the
        // enemy severs something - except the two systems that physically live in the side bays
        // and the engines at the very bottom of them.
        var systemDevices = new[]
        {
            new ShipSystemDevice("system-shields", "shields-bay", X: 2f, Y: 9.2f, PowerSystemId.Shields),
            new ShipSystemDevice("system-shields-2", "reactor", X: 4.8f, Y: 14f, PowerSystemId.Shields),
            new ShipSystemDevice("system-weapon-charger", "reactor", X: 6.75f, Y: 14f, PowerSystemId.WeaponCharger),
            new ShipSystemDevice("system-secondary", "reactor", X: 8.7f, Y: 14f, PowerSystemId.Secondary),
            new ShipSystemDevice("system-oxygen", "life-support", X: 11.5f, Y: 9.2f, PowerSystemId.Oxygen),
            // The two engines, big and right at the tail of each side compartment.
            new ShipSystemDevice("system-engine", "shields-bay", X: 2f, Y: 17.2f, PowerSystemId.Engine, SizeScale: 1.7f),
            new ShipSystemDevice("system-engine-2", "life-support", X: 11.5f, Y: 17.2f, PowerSystemId.Engine, SizeScale: 1.7f),
        };

        // Low in the hall and much larger than other classes' - this compartment is built around it.
        var reactorBlock = new ReactorBlock("reactor-block", "reactor", X: 6.75f, Y: 12.6f, SizeScale: 1.8f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "reactor", X: 8.7f, Y: 10f);
        var navigationConsole = new NavigationConsole("navigation-console", "cockpit", X: 5f, Y: 1.2f);
        var airlockConsole = new AirlockConsole("airlock-console", "cockpit", X: 8.5f, Y: 1.2f);
        var wiringTerminal = new WiringTerminal("wiring-terminal", "reactor", X: 4.8f, Y: 10f);
        var helmConsole = new HelmConsole("helm-console", "cockpit", X: 6.75f, Y: 2.4f);
        var storageRack = new StorageRack("rack-reactor", "reactor", X: 4.8f, Y: 12.6f);

        var wallBlocks = new List<WallBlock>();
        // Only the edges that actually face vacuum: the spine's flanks are open to the side bays
        // from y=8 down, and the bays are closed on every side but the one facing the spine.
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: false, left: true, right: true));   // cockpit
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[1], top: false, bottom: false, left: true, right: true));  // armory
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[2], top: false, bottom: true, left: false, right: false)); // reactor hall (flanked, open aft)
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[3], top: true, bottom: true, left: true, right: false));   // shields bay
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[4], top: true, bottom: true, left: false, right: true));   // life support

        var cockpit = rooms[0];
        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, toolStations, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, navigationConsole, airlockConsole, wiringTerminal, helmConsole, storageRack, cockpit.Center, cockpit.Id,
            forwardDegrees: ShipCatalog.ForwardDegrees(ShipKind.Corvette)); // bow up the plan: this hull flies nose-first, not broadside
    }
}
