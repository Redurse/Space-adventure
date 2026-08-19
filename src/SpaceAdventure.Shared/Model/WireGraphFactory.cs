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

        // The room the distribution block itself sits in (the reactor room, on every hull) - each
        // junction now takes a spot along its left/right walls instead of stacking in a row right
        // next to the block, which read as one clump rather than separate physical boxes.
        var room = ship.Rooms.First(r => r.Id == ship.DistributionBlock.RoomId);
        var junctionIndex = 0;

        foreach (var system in systems)
        {
            var devices = ship.SystemDevices.Where(d => d.System == system).ToList();
            if (devices.Count == 0)
                continue; // this hull has no device on this system at all - no junction, no wires

            // Alternate left/right wall, stepping down each side as more junctions land on it - a
            // hull with up to 5 power systems puts at most 3 boxes down either wall.
            var onLeftWall = junctionIndex % 2 == 0;
            var slot = junctionIndex / 2;
            var junctionX = onLeftWall ? room.Left + 1f : room.Right - 1f;
            var junctionY = room.Top + 1.5f + slot * 1.6f;
            junctionIndex++;

            var junctionId = $"junction-{system}".ToLowerInvariant();
            components.Add(new Component(junctionId, ComponentKind.Junction, ship.DistributionBlock.RoomId, junctionX, junctionY));

            // Routed, not diagonal: across to the wall at the distribution block's own height,
            // then one turn down/up the wall to the junction - the same "along the bulkhead, one
            // corner" look a real conduit run would have, instead of a wire cutting straight
            // across the compartment (Wire.Bends is purely cosmetic - never read for connectivity).
            wires.Add(new Wire($"trunk-{system}".ToLowerInvariant(),
                new PinRef("distribution", ComponentDefinitions.DistributionOutPin(system).Id),
                new PinRef(junctionId, ComponentDefinitions.JunctionInPin().Id),
                new[] { new Vec2(junctionX, ship.DistributionBlock.Y) }));

            for (var i = 0; i < devices.Count; i++)
            {
                var device = devices[i];
                components.Add(new Component(device.Id, ComponentKind.Device, device.RoomId, device.X, device.Y));

                // Same one-corner routing for the drop wire: straight along the junction's own
                // wall to the device's height, then straight across to the device itself.
                wires.Add(new Wire($"drop-{device.Id}",
                    new PinRef(junctionId, ComponentDefinitions.JunctionOutPin(i).Id),
                    new PinRef(device.Id, "in"),
                    new[] { new Vec2(junctionX, device.Y) }));
            }
        }

        return (components, wires);
    }
}
