using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Shared.Model;

// Replaces WireNetwork.CreateDefault(). The old fixed topology forced every hull to declare the
// exact same 7-device set or take a permanent "half power" penalty on whichever device it didn't
// have (see the old WireNetwork.cs's own doc comment). Building the graph from this hull's actual
// Ship.SystemDevices instead means a hull missing a second engine block simply has no second drop
// wire at all - the wart is gone because the graph's shape now follows the real device list rather
// than a hand-authored fixed one.
//
// Device components reuse ShipSystemDevice.Id directly (as WireNode did before them), so a Wire's
// endpoint can be matched straight back to the physical block it powers with no extra lookup table.
public static class WireGraphFactory
{
    public static (IReadOnlyList<Component> Components, IReadOnlyList<Wire> Wires) CreateDefaultForHull(Ship ship)
    {
        var components = new List<Component>
        {
            new("distribution", ComponentKind.Distribution, ship.DistributionBlock.RoomId,
                ship.DistributionBlock.X, ship.DistributionBlock.Y),
        };
        var wires = new List<Wire>();
        var systems = Enum.GetValues<PowerSystemId>();

        foreach (var system in systems)
        {
            var devices = ship.SystemDevices.Where(d => d.System == system).ToList();
            if (devices.Count == 0)
                continue; // this hull has no device on this system at all - no junction, no wires

            // Spread the junctions out along X rather than stacking every one on the same point -
            // there's nothing physically at Distribution's side beyond the block itself, so this is
            // the only place a position for them exists at all.
            var junctionId = $"junction-{system}".ToLowerInvariant();
            components.Add(new Component(junctionId, ComponentKind.Junction, ship.DistributionBlock.RoomId,
                ship.DistributionBlock.X + 1f + Array.IndexOf(systems, system) * 0.6f, ship.DistributionBlock.Y));

            wires.Add(new Wire($"trunk-{system}".ToLowerInvariant(),
                new PinRef("distribution", ComponentDefinitions.DistributionOutPin(system).Id),
                new PinRef(junctionId, ComponentDefinitions.JunctionInPin().Id)));

            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                components.Add(new Component(device.Id, ComponentKind.Device, device.RoomId, device.X, device.Y));
                wires.Add(new Wire($"drop-{device.Id}",
                    new PinRef(junctionId, ComponentDefinitions.JunctionOutPin(i).Id),
                    new PinRef(device.Id, "in")));
            }
        }

        return (components, wires);
    }
}
