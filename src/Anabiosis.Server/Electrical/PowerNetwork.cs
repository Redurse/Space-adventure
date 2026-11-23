using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server.Electrical;

// W1 of wiring-plan.md (direct user request: "перенести проводку баротравмы максимально полно и
// почти 1 в 1"). Barotrauma's power model, as far as it shows in play:
//  - wires join power pins into GRIDS; a junction box conducts between everything wired to it, a
//    relay only while it's switched on;
//  - every consumer on a grid states how much it draws; sources (the reactor, discharging
//    batteries) cover that load;
//  - "voltage" is delivered / load, capped at 1 - a grid short of power runs every consumer on it
//    proportionally weaker (dim lights, slow pumps), nothing switches off by itself;
//  - batteries charge from whatever the reactor has spare and discharge only to cover a shortfall;
//  - the reactor in auto mode follows its grid's load (Reactor.cs ramps toward ReactorRequested in
//    W3 - here it's just told what's wanted and how much it can actually give this tick).
// Barotrauma's own internal numbers aren't known, so this reproduces the behaviour, not the formula.
//
// Pure and self-contained: devices + wires in, per-device results out, nothing read from World -
// the player ship and (W11) enemy ships can both run on it, and it's unit-tested on its own.
public enum ElectricalRole
{
    Consumer,   // "power_in"
    Reactor,    // "power_out"
    Battery,    // "power_in" (charging) and "power_out" (discharging) - separate pins, may sit on different grids
    Junction,   // "power" - one pin, everything wired to it is one grid
    Relay,      // "power_in" / "power_out" - joined only while On
}

public sealed class ElectricalDevice
{
    public const string PowerIn = "power_in";
    public const string PowerOut = "power_out";
    public const string Power = "power";

    public required string Id { get; init; }
    public required ElectricalRole Role { get; init; }

    // Consumer: power drawn at full voltage, units per second.
    public float Demand { get; set; }
    // Reactor: the most it can deliver right now (its current, ramped output ceiling).
    public float ReactorAvailable { get; set; }
    // Relay: whether it conducts.
    public bool RelayOn { get; set; } = true;
    // Battery: stored energy, capacity, and the fastest it charges or discharges (units per second).
    public float BatteryCharge { get; set; }
    public float BatteryCapacity { get; set; }
    public float BatteryMaxRate { get; set; }

    // ---- results of the last PowerNetwork.Step
    // 0..1: the share of its demand this device actually got (consumers), or its grid's voltage.
    public float Voltage { get; internal set; }
    // Reactor: the load its grid asks of it (consumers + batteries wanting a charge), its share of it.
    public float ReactorRequested { get; internal set; }
    // Reactor: what it actually delivered this tick.
    public float ReactorDelivered { get; internal set; }
    // The grid this device's main pin sits on: total power flowing in it and total load on it -
    // what a junction box reports on its power/load signal outputs (W6).
    public float GridPower { get; internal set; }
    public float GridLoad { get; internal set; }

    public IEnumerable<string> PowerPins => Role switch
    {
        ElectricalRole.Consumer => new[] { PowerIn },
        ElectricalRole.Reactor => new[] { PowerOut },
        ElectricalRole.Battery => new[] { PowerIn, PowerOut },
        ElectricalRole.Junction => new[] { Power },
        _ => new[] { PowerIn, PowerOut },
    };

    // The pin whose grid GridPower/GridLoad/Voltage describe.
    public string MainPin => Role switch
    {
        ElectricalRole.Consumer => PowerIn,
        ElectricalRole.Junction => Power,
        _ => PowerOut,
    };
}

public static class PowerNetwork
{
    public static bool IsPowerPin(string pinId) =>
        pinId is ElectricalDevice.PowerIn or ElectricalDevice.PowerOut or ElectricalDevice.Power;

    // One simulation tick. Wires to unknown devices/pins, or carrying signals, are ignored.
    public static void Step(IReadOnlyList<ElectricalDevice> devices, IEnumerable<Wire> wires, float deltaSeconds)
    {
        var dt = MathF.Max(deltaSeconds, 1e-4f);

        // Grids: union-find over power pins.
        var parent = new Dictionary<PinRef, PinRef>();
        PinRef Find(PinRef p)
        {
            while (parent[p] != p)
                p = parent[p] = parent[parent[p]];
            return p;
        }
        void Union(PinRef a, PinRef b)
        {
            var ra = Find(a);
            var rb = Find(b);
            if (ra != rb)
                parent[ra] = rb;
        }

        foreach (var device in devices)
            foreach (var pin in device.PowerPins)
            {
                var key = new PinRef(device.Id, pin);
                parent[key] = key;
            }
        foreach (var wire in wires)
            if (parent.ContainsKey(wire.FromPin) && parent.ContainsKey(wire.ToPin))
                Union(wire.FromPin, wire.ToPin);
        foreach (var relay in devices)
            if (relay.Role == ElectricalRole.Relay && relay.RelayOn)
                Union(new PinRef(relay.Id, ElectricalDevice.PowerIn), new PinRef(relay.Id, ElectricalDevice.PowerOut));

        var grids = new Dictionary<PinRef, Grid>();
        Grid GridOf(string deviceId, string pin)
        {
            var root = Find(new PinRef(deviceId, pin));
            if (!grids.TryGetValue(root, out var grid))
                grids[root] = grid = new Grid();
            return grid;
        }

        foreach (var device in devices)
        {
            switch (device.Role)
            {
                case ElectricalRole.Consumer:
                    GridOf(device.Id, ElectricalDevice.PowerIn).Consumers.Add(device);
                    break;
                case ElectricalRole.Reactor:
                    GridOf(device.Id, ElectricalDevice.PowerOut).Reactors.Add(device);
                    break;
                case ElectricalRole.Battery:
                    GridOf(device.Id, ElectricalDevice.PowerIn).ChargingBatteries.Add(device);
                    GridOf(device.Id, ElectricalDevice.PowerOut).DischargingBatteries.Add(device);
                    break;
            }
        }

        var charged = new Dictionary<ElectricalDevice, float>();
        var discharged = new Dictionary<ElectricalDevice, float>();
        foreach (var grid in grids.Values)
            grid.Solve(dt, charged, discharged);

        foreach (var (battery, rate) in charged)
            battery.BatteryCharge = MathF.Min(battery.BatteryCapacity, battery.BatteryCharge + rate * dt);
        foreach (var (battery, rate) in discharged)
            battery.BatteryCharge = MathF.Max(0f, battery.BatteryCharge - rate * dt);

        foreach (var device in devices)
        {
            var grid = GridOf(device.Id, device.MainPin);
            device.GridPower = grid.Delivered;
            device.GridLoad = grid.Load;
            if (device.Role != ElectricalRole.Consumer)
                device.Voltage = grid.Voltage;
        }
    }

    private sealed class Grid
    {
        public readonly List<ElectricalDevice> Consumers = new();
        public readonly List<ElectricalDevice> Reactors = new();
        public readonly List<ElectricalDevice> ChargingBatteries = new();
        public readonly List<ElectricalDevice> DischargingBatteries = new();

        public float Load;
        public float Delivered;
        public float Voltage;

        public void Solve(float dt, Dictionary<ElectricalDevice, float> charged, Dictionary<ElectricalDevice, float> discharged)
        {
            Load = Consumers.Sum(c => MathF.Max(0f, c.Demand));

            // What the batteries on this grid would take, at most, to fill up this tick.
            var chargeWanted = ChargingBatteries.Sum(ChargeRoom);
            var reactorCapacity = Reactors.Sum(r => MathF.Max(0f, r.ReactorAvailable));

            // Auto mode: the reactors are asked for the whole load, shared by what each can give.
            var requested = Load + chargeWanted;
            foreach (var reactor in Reactors)
                reactor.ReactorRequested = reactorCapacity > 0f ? requested * reactor.ReactorAvailable / reactorCapacity : requested / Reactors.Count;

            // Consumers first; batteries cover only what the reactors can't.
            var fromReactors = MathF.Min(reactorCapacity, Load);
            var shortfall = Load - fromReactors;
            var dischargeable = DischargingBatteries.Sum(b => DischargeRoom(b, dt));
            var fromBatteries = MathF.Min(shortfall, dischargeable);
            if (fromBatteries > 0f)
                foreach (var battery in DischargingBatteries)
                {
                    var share = DischargeRoom(battery, dt) / dischargeable * fromBatteries;
                    discharged[battery] = discharged.GetValueOrDefault(battery) + share;
                }

            // Whatever the reactors have left over goes into charging batteries.
            var spare = MathF.Max(0f, reactorCapacity - fromReactors);
            var toBatteries = MathF.Min(spare, chargeWanted);
            if (toBatteries > 0f)
                foreach (var battery in ChargingBatteries)
                {
                    var share = ChargeRoom(battery) / chargeWanted * toBatteries;
                    charged[battery] = charged.GetValueOrDefault(battery) + share;
                }

            var reactorOutput = fromReactors + toBatteries;
            foreach (var reactor in Reactors)
                reactor.ReactorDelivered = reactorCapacity > 0f ? reactorOutput * reactor.ReactorAvailable / reactorCapacity : 0f;

            Delivered = fromReactors + fromBatteries;
            var anySupply = reactorCapacity > 0f || dischargeable > 0f;
            Voltage = Load > 0f ? MathF.Min(1f, Delivered / Load) : anySupply ? 1f : 0f;
            foreach (var consumer in Consumers)
                consumer.Voltage = Voltage;

            float ChargeRoom(ElectricalDevice b) =>
                MathF.Max(0f, MathF.Min(b.BatteryMaxRate, (b.BatteryCapacity - b.BatteryCharge) / dt));
        }

        private static float DischargeRoom(ElectricalDevice b, float dt) =>
            MathF.Max(0f, MathF.Min(b.BatteryMaxRate, b.BatteryCharge / dt));
    }
}
