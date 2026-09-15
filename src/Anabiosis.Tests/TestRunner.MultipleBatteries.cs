using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;

// Direct user request ("сделай чтобы на корабле могло быть несколько батарей") - BatteryBlock
// itself is still always just the FIRST placed Battery device (or the auto-placed fallback right
// next to the reactor), but extras beyond it now count toward Ship.BatteryDeviceCount and turn
// into real stored-energy capacity (Battery.CapacityBonus) - the same "bonus, not list" shape
// Reactor/Distribution already had (Ship.cs's own doc comment).
internal static partial class TestRunner
{
    private static bool Ship_FromCustomDefinition_MultipleBatteryDevices_CountsAndTracksExtras()
    {
        // BuildSimpleCustomShipDefinition places no Battery device at all (unlike Reactor/
        // Distribution, which always have exactly one) - 3 appended here means a count of 3, with
        // the first becoming the real BatteryBlock and the other two the extras.
        var def = BuildSimpleCustomShipDefinition();
        var withExtraBatteries = def with
        {
            Devices = def.Devices
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 1))
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 2))
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 3))
                .ToList(),
        };
        var ship = Ship.FromCustomDefinition(withExtraBatteries);

        return ship.BatteryDeviceCount == 3
            && ship.ExtraBatteryPositions.Count == 2
            && ship.ExtraBatteryPositions.Any(p => p.X == 3 && p.Y == 2)
            && ship.ExtraBatteryPositions.Any(p => p.X == 3 && p.Y == 3);
    }

    // A hull that never places a Battery device at all still gets the auto-placed fallback -
    // BatteryDeviceCount must read 1 (the fallback), not 0, so RecomputeDeviceBonuses' own
    // Math.Max(0, count-1) stays at 0 instead of going negative.
    private static bool Ship_FromCustomDefinition_NoBatteryDevicePlaced_CountsAsOne()
    {
        var ship = Ship.FromCustomDefinition(BuildSimpleCustomShipDefinition());
        return ship.BatteryDeviceCount == 1 && ship.ExtraBatteryPositions.Count == 0;
    }

    private static bool World_MultipleBatteryDevices_IncreasesPowerGridCapacity()
    {
        var def = BuildSimpleCustomShipDefinition();
        var withExtraBatteries = def with
        {
            Devices = def.Devices
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 1))
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 2))
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 3))
                .ToList(),
        };
        var world = new World(ShipKind.Custom, withExtraBatteries);

        return world.Ship.BatteryDeviceCount == 3
            && world.PowerGrid.Battery.CapacityBonus == RoomCatalog.BatteryRoomBonusCapacity * 2;
    }

    // Round-trip: a hull that already has extra battery rooms must keep them summed the next time
    // it goes through ToDefinition()->FromCustomDefinition() (World.ShipBuilding.cs's build/demolish
    // path always starts from ToDefinition()) - same concern ExtraReactorPositions/
    // ExtraDistributionPositions already guard against. Needs 2 placed devices, not 1 - with only
    // one, that single device becomes the primary BatteryBlock and there's no "extra" at all to
    // lose in the first place.
    private static bool Ship_ToDefinition_RoundTripsExtraBatteryPositions()
    {
        var def = BuildSimpleCustomShipDefinition();
        var withExtraBattery = def with
        {
            Devices = def.Devices
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 1))
                .Append(new CustomDeviceDef(CustomDeviceKind.Battery, 3, 2))
                .ToList(),
        };
        var ship = Ship.FromCustomDefinition(withExtraBattery);

        var roundTripped = Ship.FromCustomDefinition(ship.ToDefinition());
        return roundTripped.BatteryDeviceCount == 2
            && roundTripped.ExtraBatteryPositions.Count == 1
            && roundTripped.ExtraBatteryPositions[0].X == 3 && roundTripped.ExtraBatteryPositions[0].Y == 2;
    }
}
