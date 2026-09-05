using System.Linq;
using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M74 (humble-soaring-cat.md) - Ship.Devices is a purely additive projection over Ship's existing
    // typed fields (ShipDevice.cs's own doc comment); these confirm the projection actually lines up
    // with those fields on a real hand-authored hull, not just that it compiles.

    private static bool Ship_Devices_StarterHull_HasExpectedCountsPerKind()
    {
        var ship = Ship.CreateStarter();
        var byKind = ship.Devices.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Count());

        int Count(DeviceKind kind) => byKind.TryGetValue(kind, out var n) ? n : 0;

        // One physical fixture each - ReactorBlock/DistributionBlock/BatteryBlock/HelmConsole/
        // NavigationConsole/CardTable/Jukebox/Terminal all appear exactly once on the starter hull.
        return Count(DeviceKind.Reactor) == 1
            && Count(DeviceKind.Distribution) == 1
            && Count(DeviceKind.Battery) == 1
            && Count(DeviceKind.Helm) == 1
            && Count(DeviceKind.Navigation) == 1
            && Count(DeviceKind.CardTable) == 1
            && Count(DeviceKind.Jukebox) == 1
            && Count(DeviceKind.Terminal) == 1
            // system-shields/system-shields-2 (Shields x2), system-weapon-charger (x1),
            // system-oxygen (x1), system-engine/system-engine-2 (Engine x2) - system-secondary is
            // deliberately excluded (ShipDevice.cs's own doc comment on PowerSystemId.Secondary).
            && Count(DeviceKind.Shields) == 2
            && Count(DeviceKind.WeaponCharger) == 1
            && Count(DeviceKind.Oxygen) == 1
            && Count(DeviceKind.Engine) == 2
            // turret-bow (Magnetic) + turret-laser (Laser)
            && Count(DeviceKind.TurretBallistic) == 1
            && Count(DeviceKind.TurretLaser) == 1
            && Count(DeviceKind.TurretMachineGun) == 0
            && Count(DeviceKind.AmmoStorage) == 1
            && Count(DeviceKind.SuitLocker) == 1
            && Count(DeviceKind.StorageRack) == 2
            && Count(DeviceKind.Camera) == 2
            && Count(DeviceKind.ComponentMount) == 6
            // Nothing places a physical Junction device yet (ShipDevice.cs's own doc comment).
            && Count(DeviceKind.Junction) == 0
            && ship.Devices.Count == 28;
    }

    // Every entry must carry a distinct Id and the same position as its source fixture - two turrets
    // of different kinds is the simplest real N>1-of-a-family case the starter hull already has.
    private static bool Ship_Devices_Turrets_MapToDistinctEntriesWithMatchingKindAndPosition()
    {
        var ship = Ship.CreateStarter();
        var bow = ship.Devices.Single(d => d.Id == "turret-bow");
        var laser = ship.Devices.Single(d => d.Id == "turret-laser");
        return bow.Kind == DeviceKind.TurretBallistic
            && laser.Kind == DeviceKind.TurretLaser
            && bow.Id != laser.Id
            && bow.Position == ship.Turrets.First(t => t.Id == "turret-bow").PeriscopePosition
            && laser.Position == ship.Turrets.First(t => t.Id == "turret-laser").PeriscopePosition;
    }

    // A second reactor built via the content-каталог отсеков path (ExtraReactorPositions - Ship.cs's
    // own doc comment) has no stored Id/RoomId of its own; BuildDevices must still synthesize a
    // distinct Id and resolve the right RoomId for it rather than silently dropping it from Devices.
    private static bool Ship_Devices_ExtraReactor_GetsOwnIdAndCorrectRoom()
    {
        var def = BuildSimpleCustomShipDefinition();
        var withExtraReactor = def with { Devices = def.Devices.Append(new CustomDeviceDef(CustomDeviceKind.Reactor, 5, 3)).ToList() };
        var ship = Ship.FromCustomDefinition(withExtraReactor);

        var reactors = ship.Devices.Where(d => d.Kind == DeviceKind.Reactor).ToList();
        if (reactors.Count != 2)
            return false;

        var primary = reactors.First(r => r.Id == ship.ReactorBlock.Id);
        var extra = reactors.First(r => r.Id != ship.ReactorBlock.Id);
        return primary.RoomId == "a" // BuildSimpleCustomShipDefinition's first Reactor sits at (1,1), inside room "a"
            && extra.RoomId == "b" // the extra one sits at (5,3), inside room "b"
            && extra.X == 5f && extra.Y == 3f;
    }
}
