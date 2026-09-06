using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Model;

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

        Component PlaceJunction(string junctionId)
        {
            var onLeftWall = junctionIndex % 2 == 0;
            var slot = junctionIndex / 2;
            var junctionX = onLeftWall ? room.Left + 1f : room.Right - 1f;
            var junctionY = room.Top + 1.5f + slot * 1.6f;
            junctionIndex++;

            var junction = new Component(junctionId, ComponentKind.Junction, ship.DistributionBlock.RoomId, junctionX, junctionY);
            components.Add(junction);
            return junction;
        }

        // Routed, not diagonal: across to the wall at the distribution block's own height, then one
        // turn down/up the wall to the junction - the same "along the bulkhead, one corner" look a
        // real conduit run would have, instead of a wire cutting straight across the compartment
        // (Wire.Bends is purely cosmetic - never read for connectivity).
        void ConnectTrunk(string wireId, PowerSystemId system, Component junction) =>
            wires.Add(new Wire(wireId,
                new PinRef("distribution", ComponentDefinitions.DistributionOutPin(system).Id),
                new PinRef(junction.Id, ComponentDefinitions.JunctionInPin().Id),
                new[] { new Vec2(junction.X, ship.DistributionBlock.Y) }));

        // Same one-corner routing for the drop wire: straight along the junction's own wall to the
        // device's height, then straight across to the device itself. Takes the raw id/room/position
        // rather than a ShipSystemDevice so a HullCamera (below) can share this exact wiring shape
        // without actually being a ShipSystemDevice itself - TestRunner.Mining.cs's
        // ExpectedSystemDeviceIds asserts an exact set of 7 ids per hull, so a camera's drop/trunk
        // has to reuse the Component/Wire graph directly instead of registering through
        // ship.SystemDevices like every device the loop below handles.
        void ConnectDeviceNode(string id, string roomId, float x, float y, Component junction, int outputIndex)
        {
            components.Add(new Component(id, ComponentKind.Device, roomId, x, y));
            wires.Add(new Wire($"drop-{id}",
                new PinRef(junction.Id, ComponentDefinitions.JunctionOutPin(outputIndex).Id),
                new PinRef(id, "in"),
                new[] { new Vec2(junction.X, y) }));
        }

        void ConnectDevice(ShipSystemDevice device, Component junction, int outputIndex) =>
            ConnectDeviceNode(device.Id, device.RoomId, device.X, device.Y, junction, outputIndex);

        foreach (var system in systems)
        {
            var devices = ship.SystemDevices.Where(d => d.System == system).ToList();
            if (devices.Count == 0)
                continue; // this hull has no device on this system at all - no junction, no wires

            if (devices.Count == 1)
            {
                // A single generator has nothing to gain from a dedicated box of its own - same
                // shared-junction shape as always, "junction-{system}" id and all (existing wiring
                // tests depend on that exact id for the single-device systems).
                var junctionId = $"junction-{system}".ToLowerInvariant();
                var junction = PlaceJunction(junctionId);
                ConnectTrunk($"trunk-{system}".ToLowerInvariant(), system, junction);
                ConnectDevice(devices[0], junction, outputIndex: 0);
                continue;
            }

            // Several identical devices on the same system (Engine/Shields on some hulls) each get
            // their own junction box and their own trunk straight back to Distribution's one output
            // pin for this system - so a hit that takes one out (its trunk or its drop) never
            // touches its sibling, and repairing one never silently fixes the other the way sharing
            // a single junction/trunk used to (game_design.md - each physical block is its own
            // point of failure, not a shared one just because two blocks happen to do the same job).
            foreach (var device in devices)
            {
                var junctionId = $"junction-{device.Id}".ToLowerInvariant();
                var junction = PlaceJunction(junctionId);
                ConnectTrunk($"trunk-{device.Id}".ToLowerInvariant(), system, junction);
                ConnectDevice(device, junction, outputIndex: 0);
            }
        }

        // Every hull camera (M48 - "камеры как устройства корабля") gets its own dedicated
        // junction+trunk off Distribution's Secondary output, same auxiliary channel the ship's
        // lighting already draws from - a camera going dark from a cut wire reads the same way a
        // lighting panel going dark already does. Deliberately not folded into the systems loop
        // above: cameras aren't in ship.SystemDevices at all (see ConnectDeviceNode's own comment),
        // so this only ever runs if the hull actually has any.
        foreach (var camera in ship.Cameras)
        {
            var junctionId = $"junction-{camera.Id}".ToLowerInvariant();
            var junction = PlaceJunction(junctionId);
            ConnectTrunk($"trunk-{camera.Id}".ToLowerInvariant(), PowerSystemId.Secondary, junction);
            ConnectDeviceNode(camera.Id, camera.RoomId, camera.X, camera.Y, junction, outputIndex: 0);
        }

        return (components, wires);
    }
}
