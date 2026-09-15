using System.Linq;
using Anabiosis.Shared.Model;

// Direct user bug report ("сделай чтобы щитки отображались в игре а не была просто пустота") -
// CustomDeviceKind.Junction ("Щиток") used to have no case at all in Ship.FromCustomDefinition, so
// a real played ship had nothing where the Ship Editor's own preview showed the device.
internal static partial class TestRunner
{
    private static bool Ship_FromCustomDefinition_JunctionDevices_BecomeJunctionBoxes()
    {
        var def = BuildSimpleCustomShipDefinition();
        var withJunctions = def with
        {
            Devices = def.Devices
                .Append(new CustomDeviceDef(CustomDeviceKind.Junction, 3, 1))
                .Append(new CustomDeviceDef(CustomDeviceKind.Junction, 3, 2))
                .ToList(),
        };
        var ship = Ship.FromCustomDefinition(withJunctions);

        return ship.JunctionBoxes.Count == 2
            && ship.JunctionBoxes.Any(j => j.X == 3f && j.Y == 1f && j.RoomId == "a")
            && ship.JunctionBoxes.Any(j => j.X == 3f && j.Y == 2f && j.RoomId == "a");
    }

    // Round-trip: a hull that already has junction boxes must keep them the next time it goes
    // through ToDefinition()->FromCustomDefinition() (World.ShipBuilding.cs's own build/demolish
    // path always starts from ToDefinition()) - same "don't silently drop it" concern every other
    // ToDefinition() field already guards against (Engines, WallMaterials, ...).
    private static bool Ship_ToDefinition_RoundTripsJunctionBoxes()
    {
        var def = BuildSimpleCustomShipDefinition();
        var withJunction = def with { Devices = def.Devices.Append(new CustomDeviceDef(CustomDeviceKind.Junction, 3, 1)).ToList() };
        var ship = Ship.FromCustomDefinition(withJunction);

        var roundTripped = Ship.FromCustomDefinition(ship.ToDefinition());
        return roundTripped.JunctionBoxes.Count == 1
            && roundTripped.JunctionBoxes[0].X == 3f && roundTripped.JunctionBoxes[0].Y == 1f;
    }
}
