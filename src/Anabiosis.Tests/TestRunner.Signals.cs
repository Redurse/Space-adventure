using System.Globalization;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W6 of wiring-plan.md - the signal layer: string values on signal pins, the ship's devices
// publishing readings and taking commands.
internal static partial class TestRunner
{
    private static World WorldWithRelay(out string relayId, CustomShipDefinition? def = null)
    {
        var world = def is null ? new World() : new World(ShipKind.Custom, def);
        relayId = "relay-test";
        world.AddComponent(new Component(relayId, ComponentKind.Relay, world.Ship.Rooms[0].Id, 0, 0));
        return world;
    }

    private static void StepTicks(World world, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            world.Step(RealtimeStep);
    }

    private static float SignalNumber(World world, string componentId, string pinId) =>
        float.Parse(world.GetPinSignal(new PinRef(componentId, pinId)) ?? "-1", CultureInfo.InvariantCulture);

    private static bool Signal_DevicesPublishTheirReadings()
    {
        var world = new World();
        StepTicks(world, 5);
        return SignalNumber(world, WireGraphFactory.ReactorId, "power_value_out") > 0f
            && SignalNumber(world, WireGraphFactory.ReactorId, "load_value_out") > 0f
            && SignalNumber(world, WireGraphFactory.ReactorId, "fuel_out") > 0f
            && world.GetPinSignal(new PinRef(WireGraphFactory.ReactorId, "state_out")) == SignalValues.True
            && SignalNumber(world, WireGraphFactory.BatteryId, "charge_%") >= 0f
            && SignalNumber(world, "distribution", "power_value_out") > 0f
            && SignalNumber(world, "junction-oxygen", "load_value_out") > 0f
            && world.Components.Where(c => c.Kind == ComponentKind.Door)
                .All(d => world.GetPinSignal(new PinRef(d.Id, "state_out")) == SignalValues.Of(world.IsDoorOpen(d.Id)));
    }

    private static bool Signal_RelayIntoDoorSetState_OpensAndClosesIt()
    {
        var world = WorldWithRelay(out var relay);
        var door = world.Components.First(c => c.Kind == ComponentKind.Door).Id;
        world.AddWire("wire-s", new PinRef(relay, "out"), new PinRef(door, "set_state"));
        StepTicks(world, 2);
        var closedAtFirst = !world.IsDoorOpen(door);
        world.ToggleRelay(relay);
        StepTicks(world, 2);
        var opened = world.IsDoorOpen(door);
        world.ToggleRelay(relay);
        StepTicks(world, 2);
        return closedAtFirst && opened && !world.IsDoorOpen(door);
    }

    // A held "1" on toggle flips the door once, not every tick.
    private static bool Signal_DoorToggle_ActsOnTheRisingEdgeOnly()
    {
        var world = WorldWithRelay(out var relay);
        var door = world.Components.First(c => c.Kind == ComponentKind.Door).Id;
        world.AddWire("wire-t", new PinRef(relay, "out"), new PinRef(door, "toggle"));
        world.ToggleRelay(relay);
        StepTicks(world, 6);
        var openAfterHeldSignal = world.IsDoorOpen(door);
        world.ToggleRelay(relay);
        StepTicks(world, 2);
        var stillOpen = world.IsDoorOpen(door);
        world.ToggleRelay(relay);
        StepTicks(world, 2);
        return openAfterHeldSignal && stillOpen && !world.IsDoorOpen(door);
    }

    private static bool Signal_RelayIntoReactorSetState_StopsAndStartsIt()
    {
        var world = WorldWithRelay(out var relay);
        world.AddWire("wire-r", new PinRef(relay, "out"), new PinRef(WireGraphFactory.ReactorId, "set_state"));
        StepTicks(world, 2);
        var stopped = !world.PowerGrid.Reactor.Running;
        world.ToggleRelay(relay);
        StepTicks(world, 2);
        return stopped && world.PowerGrid.Reactor.Running;
    }

    private static bool Signal_RelayIntoLampSetState_SwitchesTheLamp()
    {
        var def = BuildSimpleCustomShipDefinition();
        def = def with { Devices = def.Devices.Append(new CustomDeviceDef(CustomDeviceKind.WallLamp, 1, 0)).ToList() };
        var world = WorldWithRelay(out var relay, def);
        var lamp = world.Ship.WallLamps.Single().Id;
        world.AddWire("wire-l", new PinRef(relay, "out"), new PinRef(lamp, "set_state"));
        StepTicks(world, 2);
        var offWithZero = world.CreateSnapshot().Wiring.LampsOff?.Contains(lamp) == true;
        world.ToggleRelay(relay);
        StepTicks(world, 2);
        return offWithZero && world.CreateSnapshot().Wiring.LampsOff?.Contains(lamp) != true;
    }

    // A device's reading feeds the logic: a door's state_out through a NOT gate, and a number
    // (the reactor's fuel) read as "on" by a gate - any value but "0" is.
    private static bool Signal_DeviceReadingsFeedTheLogic()
    {
        var world = new World();
        var door = world.Components.First(c => c.Kind == ComponentKind.Door).Id;
        world.AddComponent(new Component("not-door", ComponentKind.GateNot, world.Ship.Rooms[0].Id, 0, 0));
        world.AddComponent(new Component("not-fuel", ComponentKind.GateNot, world.Ship.Rooms[0].Id, 0, 0));
        world.AddWire("wire-d", new PinRef(door, "state_out"), new PinRef("not-door", "in"));
        world.AddWire("wire-f", new PinRef(WireGraphFactory.ReactorId, "fuel_out"), new PinRef("not-fuel", "in"));
        StepTicks(world, 2);
        var closedReadsOne = world.GetPinSignal(new PinRef("not-door", "out")) == SignalValues.True;
        world.ToggleDoor(door);
        StepTicks(world, 2);
        return closedReadsOne
            && world.GetPinSignal(new PinRef("not-door", "out")) == SignalValues.False
            && world.GetPinSignal(new PinRef("not-fuel", "out")) == SignalValues.False;
    }

    // A wire between two junction boxes' signal pins carries readings, not power.
    private static bool Signal_WireOnSignalPins_CarriesNoPower()
    {
        var world = new World();
        var oxygen = OxygenDeviceId(world);
        world.CutWire("trunk-oxygen");
        world.AddWire("wire-sig", new PinRef("distribution", "power_value_out"), new PinRef("junction-oxygen", "power_value_out"));
        StepTicks(world, 2);
        return !world.IsDeviceConnected(oxygen) && world.GetEffectivePower(PowerSystemId.Oxygen) == 0f;
    }
}
