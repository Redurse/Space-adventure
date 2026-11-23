using Anabiosis.Server;
using Anabiosis.Server.Electrical;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W1 of wiring-plan.md - the Barotrauma-style power network on its own, before the game runs on it.
internal static partial class TestRunner
{
    private static ElectricalDevice NetReactor(string id, float available) =>
        new() { Id = id, Role = ElectricalRole.Reactor, ReactorAvailable = available };

    private static ElectricalDevice NetConsumer(string id, float demand) =>
        new() { Id = id, Role = ElectricalRole.Consumer, Demand = demand };

    private static Wire NetWire(string from, string fromPin, string to, string toPin) =>
        new($"{from}->{to}", new PinRef(from, fromPin), new PinRef(to, toPin));

    private static bool Near(float a, float b) => MathF.Abs(a - b) < 0.01f;

    // W2: a fresh World's grid powers every wired device from the first tick, where the old sliders
    // started a test world at zero. A test that's about something else happening WITHOUT a system's
    // power (a breach suffocating a room, enemy fire landing unshielded) cuts that system's wires,
    // the in-world way a system loses power now.
    private static void CutPowerTo(World world, params PowerSystemId[] systems)
    {
        foreach (var device in world.Ship.SystemDevices.Where(d => systems.Contains(d.System)))
            foreach (var wire in world.Wires.Where(w => w.ToPin.ComponentId == device.Id).ToList())
                world.CutWire(wire.Id);
    }

    private static bool PowerNetwork_ReactorWiredToConsumer_RunsItAtFullVoltage()
    {
        var reactor = NetReactor("reactor", 100);
        var pump = NetConsumer("pump", 30);
        PowerNetwork.Step(new[] { reactor, pump }, new[] { NetWire("reactor", "power_out", "pump", "power_in") }, 0.1f);
        return Near(pump.Voltage, 1f) && Near(reactor.ReactorRequested, 30) && Near(reactor.ReactorDelivered, 30);
    }

    private static bool PowerNetwork_UnwiredConsumer_GetsNothing()
    {
        var reactor = NetReactor("reactor", 100);
        var pump = NetConsumer("pump", 30);
        PowerNetwork.Step(new[] { reactor, pump }, Array.Empty<Wire>(), 0.1f);
        return Near(pump.Voltage, 0f) && Near(reactor.ReactorRequested, 0);
    }

    private static bool PowerNetwork_JunctionBox_JoinsEverythingWiredToIt()
    {
        var reactor = NetReactor("reactor", 100);
        var junction = new ElectricalDevice { Id = "jb", Role = ElectricalRole.Junction };
        var a = NetConsumer("a", 20);
        var b = NetConsumer("b", 20);
        PowerNetwork.Step(new[] { reactor, junction, a, b }, new[]
        {
            NetWire("reactor", "power_out", "jb", "power"),
            NetWire("jb", "power", "a", "power_in"),
            NetWire("jb", "power", "b", "power_in"),
        }, 0.1f);
        return Near(a.Voltage, 1f) && Near(b.Voltage, 1f) && Near(junction.GridLoad, 40) && Near(junction.GridPower, 40);
    }

    // Barotrauma's brownout: a grid short of power runs everything on it proportionally weaker.
    private static bool PowerNetwork_Overload_RunsEveryConsumerProportionallyWeaker()
    {
        var reactor = NetReactor("reactor", 50);
        var a = NetConsumer("a", 60);
        var b = NetConsumer("b", 40);
        PowerNetwork.Step(new[] { reactor, a, b }, new[]
        {
            NetWire("reactor", "power_out", "a", "power_in"),
            NetWire("reactor", "power_out", "b", "power_in"),
        }, 0.1f);
        return Near(a.Voltage, 0.5f) && Near(b.Voltage, 0.5f) && Near(reactor.ReactorRequested, 100) && Near(reactor.ReactorDelivered, 50);
    }

    private static bool PowerNetwork_Relay_ConductsOnlyWhileOn()
    {
        var reactor = NetReactor("reactor", 100);
        var relay = new ElectricalDevice { Id = "relay", Role = ElectricalRole.Relay, RelayOn = false };
        var lamp = NetConsumer("lamp", 5);
        var devices = new[] { reactor, relay, lamp };
        var wires = new[]
        {
            NetWire("reactor", "power_out", "relay", "power_in"),
            NetWire("relay", "power_out", "lamp", "power_in"),
        };
        PowerNetwork.Step(devices, wires, 0.1f);
        var offVoltage = lamp.Voltage;
        relay.RelayOn = true;
        PowerNetwork.Step(devices, wires, 0.1f);
        return Near(offVoltage, 0f) && Near(lamp.Voltage, 1f);
    }

    private static bool PowerNetwork_Battery_CoversAShortfallAndDrains()
    {
        var reactor = NetReactor("reactor", 0);
        var battery = new ElectricalDevice
        {
            Id = "battery", Role = ElectricalRole.Battery, BatteryCharge = 100, BatteryCapacity = 100, BatteryMaxRate = 50,
        };
        var pump = NetConsumer("pump", 20);
        PowerNetwork.Step(new[] { reactor, battery, pump }, new[]
        {
            NetWire("reactor", "power_out", "battery", "power_in"),
            NetWire("battery", "power_out", "pump", "power_in"),
        }, 1f);
        return Near(pump.Voltage, 1f) && Near(battery.BatteryCharge, 80);
    }

    private static bool PowerNetwork_Battery_ChargesOnlyFromWhatTheReactorHasSpare()
    {
        var reactor = NetReactor("reactor", 100);
        var battery = new ElectricalDevice
        {
            Id = "battery", Role = ElectricalRole.Battery, BatteryCharge = 0, BatteryCapacity = 1000, BatteryMaxRate = 50,
        };
        var pump = NetConsumer("pump", 30);
        PowerNetwork.Step(new[] { reactor, battery, pump }, new[]
        {
            NetWire("reactor", "power_out", "pump", "power_in"),
            NetWire("reactor", "power_out", "battery", "power_in"),
        }, 1f);
        return Near(pump.Voltage, 1f) && Near(battery.BatteryCharge, 50) && Near(reactor.ReactorRequested, 80) && Near(reactor.ReactorDelivered, 80);
    }

    private static bool PowerNetwork_SeparateGrids_DontShareAShortage()
    {
        var bigReactor = NetReactor("big", 100);
        var smallReactor = NetReactor("small", 10);
        var a = NetConsumer("a", 50);
        var b = NetConsumer("b", 50);
        PowerNetwork.Step(new[] { bigReactor, smallReactor, a, b }, new[]
        {
            NetWire("big", "power_out", "a", "power_in"),
            NetWire("small", "power_out", "b", "power_in"),
        }, 0.1f);
        return Near(a.Voltage, 1f) && Near(b.Voltage, 0.2f);
    }

    private static bool WireRules_APinTakesAtMostFiveWires()
    {
        var pin = new PinRef("jb", "power");
        var wires = Enumerable.Range(0, 4).Select(i => new Wire($"w{i}", pin, new PinRef($"d{i}", "power_in"))).ToList();
        var fourth = WireRules.CanAddWireTo(wires, pin);
        wires.Add(new Wire("w4", pin, new PinRef("d4", "power_in")));
        var fifth = WireRules.CanAddWireTo(wires, pin);
        return fourth && !fifth && !WireRules.CanConnect(Array.Empty<Wire>(), pin, pin);
    }
}
