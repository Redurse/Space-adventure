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

        // W4 of wiring-plan.md: the reactor and battery are real components on real wires now - the
        // reactor feeds the distribution block, the battery both discharges into it and charges off
        // its dedicated output (Barotrauma's battery: power_in and power_out on the same grid).
        components.Add(new Component(ReactorId, ComponentKind.Reactor, ship.ReactorBlock.RoomId, ship.ReactorBlock.X, ship.ReactorBlock.Y));
        components.Add(new Component(BatteryId, ComponentKind.Battery, ship.BatteryBlock.RoomId, ship.BatteryBlock.X, ship.BatteryBlock.Y));
        var distributionIn = new PinRef("distribution", ComponentDefinitions.DistributionInPin().Id);
        wires.Add(new Wire("feed-reactor", new PinRef(ReactorId, ComponentDefinitions.ReactorOutPin().Id), distributionIn));
        wires.Add(new Wire("feed-battery", new PinRef(BatteryId, ComponentDefinitions.BatteryOutPin().Id), distributionIn));
        wires.Add(new Wire("charge-battery",
            new PinRef("distribution", ComponentDefinitions.DistributionBatteryPin().Id),
            new PinRef(BatteryId, ComponentDefinitions.BatteryInPin().Id)));

        // W6: every door and wall lamp is a component too - signal pins only, nothing wired to them
        // until the player (or, from W10, a factory hull's standard automation) does it. Their ids
        // are the door's/lamp's own.
        foreach (var door in ship.Doors)
            components.Add(new Component(door.Id, ComponentKind.Door, door.RoomAId, door.X, door.Y, TargetId: door.Id));
        // A wide door is several edge segments sharing one id - one component for the whole door,
        // at the middle of its segments.
        foreach (var door in ship.DoorEdges.GroupBy(e => e.Id))
        {
            var centres = door.Select(e => e.Side == TileSide.East ? (X: e.Coord.X + 1f, Y: e.Coord.Y + 0.5f) : (X: e.Coord.X + 0.5f, Y: e.Coord.Y + 1f)).ToList();
            var first = door.First();
            components.Add(new Component(door.Key, ComponentKind.Door, first.RoomAId ?? first.RoomBId ?? "",
                centres.Average(c => c.X), centres.Average(c => c.Y), TargetId: door.Key));
        }
        foreach (var lamp in ship.WallLamps)
            components.Add(new Component(lamp.Id, ComponentKind.Lamp, lamp.RoomId, lamp.X, lamp.Y, TargetId: lamp.Id));

        // A hull with junction boxes placed in it (the ship editor's "щиток", JunctionBox.cs) is
        // wired through them, Barotrauma-style; one without any keeps the generated virtual boxes
        // in the reactor room below.
        if (ship.JunctionBoxes.Count > 0)
        {
            WireThroughJunctionBoxes(ship, components, wires);
            return (components.DistinctBy(c => c.Id).ToList(), wires); // ids must be unique - first one wins
        }

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

        return (components.DistinctBy(c => c.Id).ToList(), wires);
    }

    public const string ReactorId = "reactor";
    public const string BatteryId = "battery";

    // Every consumer (system device or hull camera) hangs off its nearest junction box, each on its
    // own output pin; every box - including one with nothing on it yet, ready for the player's own
    // wiring - gets a trunk back to the distribution block. Same one-corner routing as the virtual
    // layout (Wire.Bends is cosmetic only).
    private static void WireThroughJunctionBoxes(Ship ship, List<Component> components, List<Wire> wires)
    {
        var consumers = ship.SystemDevices.Select(d => (d.Id, d.RoomId, d.X, d.Y, d.System))
            .Concat(ship.Cameras.Select(c => (c.Id, c.RoomId, c.X, c.Y, System: PowerSystemId.Secondary)));
        var usedOutputs = ship.JunctionBoxes.ToDictionary(b => b.Id, _ => 0);
        var firstSystem = new Dictionary<string, PowerSystemId>();

        foreach (var consumer in consumers)
        {
            var box = ship.JunctionBoxes.MinBy(b => (b.X - consumer.X) * (b.X - consumer.X) + (b.Y - consumer.Y) * (b.Y - consumer.Y))!;
            var outputIndex = usedOutputs[box.Id]++;
            firstSystem.TryAdd(box.Id, consumer.System);
            components.Add(new Component(consumer.Id, ComponentKind.Device, consumer.RoomId, consumer.X, consumer.Y));
            wires.Add(new Wire($"drop-{consumer.Id}",
                new PinRef(box.Id, ComponentDefinitions.JunctionOutPin(outputIndex).Id),
                new PinRef(consumer.Id, "in"),
                new[] { new Vec2(box.X, consumer.Y) }));
        }

        // Each trunk leaves on its first device's system output - or, once that pin already carries
        // WireRules.MaxWiresPerPin wires, on the next distribution output that still has room.
        var distributionOutputs = Enum.GetValues<PowerSystemId>().Select(ComponentDefinitions.DistributionOutPin).ToList();
        foreach (var box in ship.JunctionBoxes)
        {
            components.Add(new Component(box.Id, ComponentKind.Junction, box.RoomId, box.X, box.Y));
            var system = firstSystem.TryGetValue(box.Id, out var s) ? s : PowerSystemId.Secondary;
            var preferred = ComponentDefinitions.DistributionOutPin(system);
            var outPin = new[] { preferred }.Concat(distributionOutputs)
                .FirstOrDefault(p => WireRules.CanAddWireTo(wires, new PinRef("distribution", p.Id))) ?? preferred;
            wires.Add(new Wire($"trunk-{box.Id}",
                new PinRef("distribution", outPin.Id),
                new PinRef(box.Id, ComponentDefinitions.JunctionInPin().Id),
                new[] { new Vec2(box.X, ship.DistributionBlock.Y) }));
        }
    }
}
