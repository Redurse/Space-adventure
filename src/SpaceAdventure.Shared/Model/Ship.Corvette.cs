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
            new Door("door-cockpit-armory", "cockpit", "armory", 6.75f, 4, Door.StandardSpanUnits, 1.0f),
            new Door("door-armory-reactor", "armory", "reactor", 6.75f, 8, Door.StandardSpanUnits, 1.0f),
            new Door("door-reactor-shields", "reactor", "shields-bay", 4, 11, 1.0f, Door.StandardSpanUnits),
            new Door("door-reactor-lifesupport", "reactor", "life-support", 9.5f, 11, 1.0f, Door.StandardSpanUnits),
        };

        // One port on each beam, on the outer wall of each side compartment (they're the only rooms
        // that reach the hull's flanks). Either one mates with a station, and either one is the way
        // out into vacuum - which is why the suit lockers stand right next to them rather than in
        // some other compartment you'd have to cross the ship from.
        var airlockOuterDoors = new[]
        {
            new AirlockOuterDoor("door-airlock-vacuum", "life-support", 13.5f, 9.5f, 1.0f, Door.StandardSpanUnits),
            new AirlockOuterDoor("door-airlock-port", "shields-bay", 0, 9.5f, 1.0f, Door.StandardSpanUnits),
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
            new AmmoStorage("ammo-storage-armory", "armory", X: 6.75f, Y: 4.9f),
        };

        var suitLockers = new[]
        {
            new SuitLocker("suit-locker-port", "shields-bay", X: 1.2f, Y: 8.5f),
            new SuitLocker("suit-locker-starboard", "life-support", X: 12.3f, Y: 8.5f),
        };

        // Every breaker panel hangs in the reactor hall, as asked - one place to run to when the
        // enemy severs something, and spaced apart rather than lined up so wiring one isn't a
        // squeeze. system-oxygen is the one exception: it stays in life-support, because its
        // RoomId is where the generator actually feeds air (World.Atmosphere.cs), not just a
        // panel location - moving it would relocate life support to a different compartment.
        var systemDevices = new[]
        {
            // Both shield generators moved to the shields bay they're named for, stacked one above
            // the other on the same centreline (x=1.8) rather than crammed into the reactor hall.
            new ShipSystemDevice("system-shields", "shields-bay", X: 1.8f, Y: 13.1f, PowerSystemId.Shields),
            new ShipSystemDevice("system-shields-2", "shields-bay", X: 1.8f, Y: 14.3f, PowerSystemId.Shields),
            new ShipSystemDevice("system-weapon-charger", "armory", X: 6.75f, Y: 6.1f, PowerSystemId.WeaponCharger),
            new ShipSystemDevice("system-secondary", "life-support", X: 11.1f, Y: 14f, PowerSystemId.Secondary),
            new ShipSystemDevice("system-oxygen", "life-support", X: 11.2f, Y: 12.9f, PowerSystemId.Oxygen),
            // Mirrored across the spine (x=2 port, x=11.5 starboard, same y) rather than paired
            // side by side in the reactor hall.
            new ShipSystemDevice("system-engine", "shields-bay", X: 2f, Y: 17.8f, PowerSystemId.Engine, SizeScale: 1.7f),
            new ShipSystemDevice("system-engine-2", "life-support", X: 11.5f, Y: 17.8f, PowerSystemId.Engine, SizeScale: 1.7f),
        };

        // Low in the hall and much larger than other classes' - this compartment is built around it.
        var reactorBlock = new ReactorBlock("reactor-block", "reactor", X: 6.75f, Y: 14f, SizeScale: 1.8f);
        var distributionBlock = new PowerDistributionBlock("distribution-block", "reactor", X: 6.75f, Y: 12.3f);
        var batteryBlock = new BatteryBlock("battery-block", "reactor", X: 6.75f, Y: 10.6f);
        // Nav and the card table flank the helm on the cockpit's own centreline (x=6.75), mirrored
        // around it - x=5.1 and x=8.4 sit the same distance either side.
        var navigationConsole = new NavigationConsole("navigation-console", "cockpit", X: 5.1f, Y: 2.1f);
        var helmConsole = new HelmConsole("helm-console", "cockpit", X: 6.75f, Y: 0.9f);
        // Two crew standing here together starts a hand of Дурак переводной (World.CardGame.cs).
        var cardTable = new CardTable("card-table", "cockpit", X: 8.4f, Y: 2.1f);
        var storageRacks = new[]
        {
            // Mirrored across the reactor hall's own centreline (x=6.75): 8.7 and 4.8 sit the same
            // distance either side, at the same depth.
            new StorageRack("rack-reactor", "reactor", X: 8.7f, Y: 8.7f),
            new StorageRack("rack-armory", "reactor", X: 4.8f, Y: 8.7f),
        };

        var wallBlocks = new List<WallBlock>();
        // Only the edges that actually face vacuum: the spine's flanks are open to the side bays
        // from y=8 down, and the bays are closed on every side but the one facing the spine.
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[0], top: true, bottom: false, left: true, right: true));   // cockpit
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[1], top: false, bottom: false, left: true, right: true));  // armory
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[2], top: false, bottom: true, left: false, right: false)); // reactor hall (flanked, open aft)
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[3], top: true, bottom: true, left: true, right: false));   // shields bay
        wallBlocks.AddRange(GenerateOuterWallBlocks(rooms[4], top: true, bottom: true, left: false, right: true));   // life support

        var componentMounts = new[]
        {
            new ComponentMount("mount-cockpit-1", "cockpit", X: 6.75f, Y: 3f),
            new ComponentMount("mount-armory-1", "armory", X: 6f, Y: 6f),
            new ComponentMount("mount-reactor-1", "reactor", X: 10f, Y: 10f),
            new ComponentMount("mount-shields-bay-1", "shields-bay", X: 2f, Y: 10f),
            new ComponentMount("mount-life-support-1", "life-support", X: 11.5f, Y: 10f),
            new ComponentMount("mount-shields-bay-door", "shields-bay", X: 2f, Y: 17f, TargetDoorId: "door-airlock-port"),
        };

        var cockpit = rooms[0];
        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, batteryBlock, navigationConsole, helmConsole, storageRacks, cockpit.Center, cockpit.Id,
            cardTable,
            forwardDegrees: ShipCatalog.ForwardDegrees(ShipKind.Corvette), // bow up the plan: this hull flies nose-first, not broadside
            componentMounts: componentMounts);
    }
}
