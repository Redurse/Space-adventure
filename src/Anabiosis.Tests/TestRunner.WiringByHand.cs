using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W5 of wiring-plan.md - wiring by hand: taking wires off with a screwdriver, and power wiring
// being undirected the way Barotrauma's is.
internal static partial class TestRunner
{
    private static bool World_WireRemove_ScrewdriverTakesItOffAndGivesTheSpoolBack()
    {
        var world = new World();
        world.SpawnCharacter(1);
        GiveAndHold(world, ItemType.Screwdriver);
        MoveToOxygenJunction(world);
        var oxygen = OxygenDeviceId(world);

        world.ApplyCommand(1, new ClientCommand(1, RemoveWireId: "trunk-oxygen"));
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);

        return world.Wires.All(w => w.Id != "trunk-oxygen")
            && HasWireSpool(world)
            && !world.IsDeviceConnected(oxygen)
            && world.GetEffectivePower(PowerSystemId.Oxygen) == 0f;
    }

    private static bool World_WireRemove_NeedsTheScrewdriverAndToBeAtAnEnd()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveToOxygenJunction(world);
        world.ApplyCommand(1, new ClientCommand(1, RemoveWireId: "trunk-oxygen")); // empty hands
        var keptWithoutTool = world.Wires.Any(w => w.Id == "trunk-oxygen");

        GiveAndHold(world, ItemType.Screwdriver);
        var engineDrop = world.Wires.First(w => w.Id.StartsWith("drop-system-engine"));
        world.ApplyCommand(1, new ClientCommand(1, RemoveWireId: engineDrop.Id)); // neither end is here
        return keptWithoutTool && world.Wires.Any(w => w.Id == engineDrop.Id);
    }

    // The generator never puts more than WireRules.MaxWiresPerPin trunks on one distribution
    // output - eight empty junction boxes spread over several outputs, every one still fed.
    private static bool Wiring_Generator_SpreadsTrunksOverTheFivePerPinRule()
    {
        var def = BuildSimpleCustomShipDefinition();
        var withBoxes = def with
        {
            Devices = def.Devices.Concat(Enumerable.Range(0, 8)
                .Select(i => new CustomDeviceDef(CustomDeviceKind.Junction, i < 4 ? 3 : 6, i % 4))).ToList(),
        };
        var world = new World(ShipKind.Custom, withBoxes);
        var distributionPins = world.Wires.SelectMany(w => new[] { w.FromPin, w.ToPin })
            .Where(p => p.ComponentId == "distribution").Distinct();
        return world.Ship.JunctionBoxes.Count == 8
            && distributionPins.All(p => WireRules.WiresOn(world.Wires, p) <= WireRules.MaxWiresPerPin)
            && world.Ship.JunctionBoxes.All(b => world.Wires.Any(w => w.ToPin == new PinRef(b.Id, "in")));
    }

    // A device whose own drop is cut, wired by its power pin to another device's power pin, shares
    // that device's grid - Barotrauma's daisy-chaining.
    private static bool World_Wiring_DeviceChainedOffAnotherDevicesPin_IsPowered()
    {
        var world = new World();
        var oxygen = OxygenDeviceId(world);
        var shields = world.Ship.SystemDevices.First(d => d.System == PowerSystemId.Shields).Id;
        world.CutWire($"drop-{oxygen}");
        world.AddWire("wire-chain", new PinRef(shields, "in"), new PinRef(oxygen, "in"));
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);
        return world.IsDeviceConnected(oxygen) && world.GetEffectivePower(PowerSystemId.Oxygen) >= 12f - 0.01f;
    }
}
