using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W10 of wiring-plan.md: the editor's wiring mode - which pins a click-pair joins, how the wires travel in the ship definition, and that the
// server uses them in place of the standard wiring at the pins they reach (and only there).
internal static partial class TestRunner
{
    private static bool EditorWiring_Link_PicksPinsFromWhatTheDevicesAre()
    {
        var reactorToBox = EditorWiring.Link(CustomDeviceKind.Reactor, CustomDeviceKind.Distribution);
        // Same pair the other way round: the same wire, with the ends swapped.
        var boxToReactor = EditorWiring.Link(CustomDeviceKind.Distribution, CustomDeviceKind.Reactor);
        var engineFromBox = EditorWiring.Link(CustomDeviceKind.Distribution, CustomDeviceKind.Engine);
        var batteryAndBox = EditorWiring.Link(CustomDeviceKind.Battery, CustomDeviceKind.Distribution);
        return reactorToBox is [("out", "in", false)]
            && boxToReactor is [("out", "in", true)]
            && engineFromBox is [("out-engine", "in", false)]
            && batteryAndBox.Count == 2
            && EditorWiring.Link(CustomDeviceKind.Engine, CustomDeviceKind.Oxygen).Count == 0   // two machines
            && EditorWiring.Link(CustomDeviceKind.Reactor, CustomDeviceKind.Reactor).Count == 0
            && EditorWiring.Link(CustomDeviceKind.Helm, CustomDeviceKind.Reactor).Count == 0;   // takes no power wire
    }

    private static bool EditorWiring_Wires_PutTheFlowTheRightWayRound()
    {
        // Clicked machine first, then the box: the wire still starts at the box.
        var wires = EditorWiring.Wires(CustomDeviceKind.Oxygen, 2f, 3f, CustomDeviceKind.Junction, 3f, 1f);
        return wires.Count == 1 && wires[0] is { FromX: 3f, FromY: 1f, FromPin: "out-0", ToX: 2f, ToY: 3f, ToPin: "in" };
    }

    // The simple test hull with a junction box added, and the author's wires: distribution -> box -> oxygen machine.
    private static CustomShipDefinition EditorWiredShip()
    {
        var def = BuildSimpleCustomShipDefinition();
        return def with
        {
            Devices = def.Devices.Append(new CustomDeviceDef(CustomDeviceKind.Junction, 3, 1)).ToList(),
            Wires = new[]
            {
                new CustomWireDef(2, 1, EditorWiring.AnyDistributionOutput, 3, 1, "in"),
                new CustomWireDef(3, 1, "out-0", 2, 3, "in"),
            },
        };
    }

    private static bool World_EditorWires_ReplaceTheStandardWiringAtTheirPins()
    {
        var world = new World(ShipKind.Custom, EditorWiredShip());
        var oxygen = world.Ship.SystemDevices.First(d => d.System == PowerSystemId.Oxygen).Id;
        var box = world.Ship.JunctionBoxes.Single().Id;

        var feedingOxygen = world.Wires.Where(w => w.ToPin.ComponentId == oxygen && w.ToPin.PinId == "in").ToList();
        var authored = feedingOxygen.Count == 1 && feedingOxygen[0].Id.StartsWith("wire-") && feedingOxygen[0].FromPin.ComponentId == box;
        var boxFed = world.Wires.Any(w => w.Id.StartsWith("wire-") && w.ToPin.ComponentId == box && w.FromPin.ComponentId == "distribution");

        StepFor(world, 30);
        // The authored chain carries the power, and the machines the author never wired still have the standard feed.
        var engine = world.Ship.SystemDevices.First(d => d.System == PowerSystemId.Engine).Id;
        return authored && boxFed && world.IsDeviceConnected(oxygen) && world.IsDeviceConnected(engine);
    }

    private static bool World_EditorWires_ToNowhereAreIgnored()
    {
        var def = BuildSimpleCustomShipDefinition() with
        {
            Wires = new[] { new CustomWireDef(30, 30, "out", 31, 31, "in") }, // nothing stands there
        };
        var world = new World(ShipKind.Custom, def);
        var standard = new World(ShipKind.Custom, BuildSimpleCustomShipDefinition());
        return world.Wires.Count == standard.Wires.Count && world.Wires.All(w => !w.Id.StartsWith("wire-"));
    }

    private static bool World_EditorWires_CutOffAMachineTheAuthorLeftOutOfTheirChain()
    {
        // The author wires the oxygen machine to a junction box that nothing feeds: it stays dark - the wire really replaced the standard feed.
        var def = BuildSimpleCustomShipDefinition();
        def = def with
        {
            Devices = def.Devices.Append(new CustomDeviceDef(CustomDeviceKind.Junction, 3, 1)).ToList(),
            Wires = new[] { new CustomWireDef(3, 1, "out-0", 2, 3, "in") },
        };
        var world = new World(ShipKind.Custom, def);
        var oxygen = world.Ship.SystemDevices.First(d => d.System == PowerSystemId.Oxygen).Id;
        var box = world.Ship.JunctionBoxes.Single().Id;
        // Take the box's own standard feed away too, so nothing reaches it.
        foreach (var feed in world.Wires.Where(w => w.ToPin.ComponentId == box && w.Id != "none").Select(w => w.Id).ToList())
            world.DebugRemoveWire(feed);
        StepFor(world, 30);
        return !world.IsDeviceConnected(oxygen);
    }

    private static bool CustomShip_Wires_SurviveJsonAndTheRoundTripThroughAShip()
    {
        var def = EditorWiredShip();
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var loaded = JsonSerializer.Deserialize<CustomShipDefinition>(JsonSerializer.Serialize(def, options), options);
        var viaShip = Ship.FromCustomDefinition(def).ToDefinition();
        // A definition from before wires existed has none.
        var withoutWires = JsonSerializer.Deserialize<CustomShipDefinition>(
            JsonSerializer.Serialize(BuildSimpleCustomShipDefinition(), options).Replace("\"Wires\":[]", "\"Gone\":[]"), options);
        return loaded is { Wires.Count: 2 } && loaded.Wires[0].ToPin == "in"
            && viaShip.Wires.Count == 2 && withoutWires is { Wires.Count: 0 };
    }
}
