using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W4 of wiring-plan.md - the reactor and battery on real wires, junction boxes as the real
// distribution points, and the wiring surviving a save/load and a changed hull.
internal static partial class TestRunner
{
    // The simple two-room test hull plus a junction box in each room.
    private static CustomShipDefinition DefWithJunctionBoxes()
    {
        var def = BuildSimpleCustomShipDefinition();
        return def with
        {
            Devices = def.Devices
                .Append(new CustomDeviceDef(CustomDeviceKind.Junction, 3, 3))
                .Append(new CustomDeviceDef(CustomDeviceKind.Junction, 6, 3))
                .ToList(),
        };
    }

    private static SaveGame RoundTripThroughDisk(SaveGame save)
    {
        var path = Path.Combine(Path.GetTempPath(), $"anabiosis-w4-{Guid.NewGuid():N}.json");
        try
        {
            SaveStore.Save(save, path);
            return SaveStore.Load(path)!;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string OxygenDeviceId(World world) => world.Ship.SystemDevices.First(d => d.System == PowerSystemId.Oxygen).Id;

    private static bool Wiring_NewHull_ReactorAndBatteryAreOnRealWires()
    {
        var world = new World();
        return world.Components.Any(c => c.Kind == ComponentKind.Reactor)
            && world.Components.Any(c => c.Kind == ComponentKind.Battery)
            && new[] { "feed-reactor", "feed-battery", "charge-battery" }.All(id => world.Wires.Any(w => w.Id == id));
    }

    private static bool Wiring_ReactorFeedCut_BatteryCarriesTheShip()
    {
        var world = new World();
        world.PowerGrid.Battery.AddCharge(world.PowerGrid.Battery.Capacity); // a fresh ship's battery starts empty
        world.Step(RealtimeStep);
        var chargeBefore = world.PowerGrid.Battery.Charge;
        world.CutWire("feed-reactor");
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        // The battery gives at most 60 against this hull's full 68 load, so everything runs a bit
        // under full - still running, which is the point.
        return world.Ship.SystemDevices.All(d => world.IsDeviceConnected(d.Id))
            && world.GetEffectivePower(PowerSystemId.Oxygen) > 9f
            && world.PowerGrid.Battery.Charge < chargeBefore;
    }

    private static bool Wiring_BothFeedsCut_ShipGoesDark()
    {
        var world = new World();
        world.CutWire("feed-reactor");
        world.CutWire("feed-battery");
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);
        return world.Ship.SystemDevices.All(d => !world.IsDeviceConnected(d.Id))
            && world.GetEffectivePower(PowerSystemId.Oxygen) == 0f;
    }

    private static bool Wiring_JunctionBoxes_DevicesHangOffTheNearestBox()
    {
        var world = new World(ShipKind.Custom, DefWithJunctionBoxes());
        var boxes = world.Ship.JunctionBoxes;
        var near = boxes.MinBy(b => Math.Abs(b.X - 3.5f) + Math.Abs(b.Y - 3.5f))!;
        world.Step(RealtimeStep);
        return boxes.Count == 2
            && world.Ship.SystemDevices.All(d => world.Wires.Single(w => w.ToPin.ComponentId == d.Id).FromPin.ComponentId == near.Id)
            && boxes.All(b => world.Wires.Any(w => w.ToPin == new PinRef(b.Id, "in") && w.FromPin.ComponentId == "distribution"))
            && !world.Components.Any(c => c.Id.StartsWith("junction-oxygen"))
            && world.Ship.SystemDevices.All(d => world.IsDeviceConnected(d.Id))
            && world.GetEffectivePower(PowerSystemId.Oxygen) >= 12f - 0.01f;
    }

    private static bool Wiring_SaveLoad_KeepsPlayerWiresAndCuts()
    {
        var world = new World();
        var oxygen = OxygenDeviceId(world);
        world.AddWire("wire-7", new PinRef("distribution", "out-secondary"), new PinRef("junction-oxygen", "in"));
        world.CutWire($"drop-{oxygen}");

        var loaded = new World();
        loaded.ApplySave(RoundTripThroughDisk(world.CreateSave()));
        var playerWire = loaded.Wires.FirstOrDefault(w => w.Id == "wire-7");
        return playerWire is not null
            && playerWire.ToPin == new PinRef("junction-oxygen", "in")
            && !loaded.IsDeviceConnected(oxygen)
            && loaded.Wires.Count == world.Wires.Count;
    }

    private static bool Wiring_SaveWithoutWiring_GetsTheStandardWiring()
    {
        var world = new World();
        world.AddWire("wire-0", new PinRef("distribution", "out-secondary"), new PinRef("junction-oxygen", "in"));
        var loaded = new World();
        loaded.ApplySave(world.CreateSave() with { Wiring = null });
        return loaded.Wires.All(w => w.Id != "wire-0")
            && loaded.Wires.Any(w => w.Id == "feed-reactor")
            && loaded.Ship.SystemDevices.All(d => loaded.IsDeviceConnected(d.Id));
    }

    // A hull that changed between save and load keeps everything that still fits: a junction box
    // that's gone takes its own wires (the player's included) with it, a new one gets wired in,
    // everything else stays as laid.
    private static bool Wiring_LoadOntoChangedHull_KeepsWhatStillFits()
    {
        var world = new World(ShipKind.Custom, DefWithJunctionBoxes());
        var far = world.Ship.JunctionBoxes.MaxBy(b => b.X)!;
        var near = world.Ship.JunctionBoxes.MinBy(b => b.X)!;
        world.AddWire("wire-0", new PinRef(near.Id, "out-5"), new PinRef(far.Id, "in"));
        world.AddWire("wire-1", new PinRef("distribution", "out-shields"), new PinRef(near.Id, "in"));
        var save = world.CreateSave();
        var def = save.CustomShip!;
        var farX = (int)MathF.Floor(far.X);
        var changed = def with
        {
            Devices = def.Devices
                .Where(d => !(d.Kind == CustomDeviceKind.Junction && (int)MathF.Floor(d.X) == farX))
                .Append(new CustomDeviceDef(CustomDeviceKind.Junction, 1, 1))
                .ToList(),
        };

        var loaded = new World();
        loaded.ApplySave(RoundTripThroughDisk(save with { CustomShip = changed }));
        var boxes = loaded.Ship.JunctionBoxes;
        var newBox = boxes.First(b => b.Id != near.Id);
        return boxes.Count == 2
            && loaded.Wires.All(w => w.Id != "wire-0")
            && loaded.Wires.Any(w => w.Id == "wire-1")
            && loaded.Wires.Any(w => w.ToPin == new PinRef(newBox.Id, "in"))
            && loaded.Ship.SystemDevices.All(d => loaded.IsDeviceConnected(d.Id));
    }
}
