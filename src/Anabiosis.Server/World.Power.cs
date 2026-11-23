using Anabiosis.Server.Electrical;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// W2 of wiring-plan.md (direct user request: Barotrauma's wiring, near 1:1) - the player ship's
// power now runs on PowerNetwork instead of the distribution block's five sliders. Each tick the
// existing wire graph (World.Wiring.cs - reactor -> distribution -> junction boxes -> devices, same
// ids as before) is handed to PowerNetwork as it stands:
//  - the distribution block and every junction box conduct between everything wired to them (a
//    broken distribution block stops conducting, like a burnt-out Barotrauma junction box);
//  - every device draws its own load - a system's devices share SystemDemand between them, which is
//    exactly what each system got from the old even split of a 60-unit reactor, so shields/oxygen/
//    weapons keep their tuning at full power (the engines draw EngineDemand, see there);
//  - the reactor and battery are wired into the distribution block by real wires (W4 -
//    "feed-reactor", "feed-battery", "charge-battery"; cut one and that source is off the grid);
//  - the reactor covers its grid's load (auto mode), the battery charges only from what's spare and
//    discharges only into a shortfall, and a grid short of power runs every device on it
//    proportionally weaker - nothing switches itself off.
public sealed partial class World
{
    private const float SystemDemand = 12f;
    // The engines are the hungriest machine aboard, as in Barotrauma - and 20 at full power is what
    // gives the legacy engine devices the same 2x thrust (World.ShipField.cs's enginePowerScale,
    // capped at 2x its 10-unit reference) that pushing the old Engine slider up used to.
    private const float EngineDemand = 20f;
    private const float CameraDemand = 1f;
    private const float BatteryMaxRate = 60f;

    // What each device drew last tick and the share of it that actually arrived (0..1).
    private readonly Dictionary<string, float> _deviceDemand = new();
    private readonly Dictionary<string, float> _deviceVoltage = new();
    // The distribution block's own grid voltage - 0 while it's broken (it stops conducting).
    private float _mainGridVoltage;
    // What the reactor's grid asked of it last tick, and what it actually gave - the reactor
    // panel's graph (W3).
    private float _reactorLoad;
    private float _reactorDelivered;
    // W6: the power and load of the grid each junction box / the distribution block sits on, last
    // tick - their power_value_out / load_value_out signal pins.
    private readonly Dictionary<string, (float Power, float Load)> _boxGrid = new();

    private void StepShipPower(double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        var devices = new List<ElectricalDevice>();
        var conducting = new HashSet<string>();
        var consumers = new HashSet<string>();
        _deviceDemand.Clear();

        foreach (var component in _components)
        {
            switch (component.Kind)
            {
                case ComponentKind.Distribution when !PowerGrid.DistributionBroken:
                case ComponentKind.Junction:
                    devices.Add(new ElectricalDevice { Id = component.Id, Role = ElectricalRole.Junction });
                    conducting.Add(component.Id);
                    break;
                case ComponentKind.Device:
                    var demand = DemandOf(component.Id);
                    _deviceDemand[component.Id] = demand;
                    devices.Add(new ElectricalDevice { Id = component.Id, Role = ElectricalRole.Consumer, Demand = demand });
                    consumers.Add(component.Id);
                    break;
            }
        }

        // W4: the reactor and battery are components on real wires (WireGraphFactory) - one each,
        // standing for PowerGrid's one Reactor and one Battery.
        var reactor = new ElectricalDevice { Id = WireGraphFactory.ReactorId, Role = ElectricalRole.Reactor, ReactorAvailable = PowerGrid.Reactor.CurrentOutput };
        devices.Add(reactor);
        // Shore power (World.StationPower.cs): the docked station's reactor as one more source on the same grid, wired into the distribution block.
        ElectricalDevice? shore = null;
        if (ShoreActive && !PowerGrid.DistributionBroken)
        {
            shore = new ElectricalDevice { Id = ShoreDeviceId, Role = ElectricalRole.Reactor, ReactorAvailable = ShoreCapacity };
            devices.Add(shore);
        }
        ElectricalDevice? battery = null;
        if (!PowerGrid.Battery.Broken)
        {
            battery = new ElectricalDevice
            {
                Id = WireGraphFactory.BatteryId,
                Role = ElectricalRole.Battery,
                BatteryCharge = PowerGrid.Battery.Charge,
                BatteryCapacity = PowerGrid.Battery.Capacity,
                BatteryMaxRate = BatteryMaxRate,
            };
            devices.Add(battery);
        }

        var wires = new List<Wire>();
        foreach (var wire in _wires)
        {
            if (_wireDamaged.GetValueOrDefault(wire.Id))
                continue;
            // W6: a signal wire on a junction's or the reactor's signal pins carries no power.
            if (GetPinKind(wire.FromPin) is not { } fromKind || !IsPower(fromKind)
                || GetPinKind(wire.ToPin) is not { } toKind || !IsPower(toKind))
                continue;
            if (ElectricalPin(wire.FromPin) is { } from && ElectricalPin(wire.ToPin) is { } to)
                wires.Add(wire with { FromPin = from, ToPin = to });
        }

        if (shore is not null)
            wires.Add(new Wire("shore-feed", new PinRef(ShoreDeviceId, ElectricalDevice.PowerOut), new PinRef(DistributionComponentId, ElectricalDevice.Power)));

        PowerNetwork.Step(devices, wires, dt);
        _shoreDelivered = shore?.ReactorDelivered ?? 0f;

        _deviceVoltage.Clear();
        _boxGrid.Clear();
        foreach (var device in devices)
        {
            if (device.Role == ElectricalRole.Consumer)
                _deviceVoltage[device.Id] = device.Voltage;
            else if (device.Role == ElectricalRole.Junction)
                _boxGrid[device.Id] = (device.GridPower, device.GridLoad);
        }

        _mainGridVoltage = devices.FirstOrDefault(d => d.Id == "distribution")?.Voltage ?? 0f;
        // W3: auto mode - the reactor steers toward what its grid asked for this tick (with its own
        // spin-up/down lag), and burns fuel for what it actually gave.
        _reactorLoad = reactor.ReactorRequested;
        _reactorDelivered = reactor.ReactorDelivered;
        PowerGrid.Reactor.Regulate(deltaSeconds, reactor.ReactorRequested);
        PowerGrid.Reactor.Step(deltaSeconds, reactor.ReactorDelivered);
        if (battery is not null)
        {
            var change = battery.BatteryCharge - PowerGrid.Battery.Charge;
            if (change > 0f)
                PowerGrid.Battery.AddCharge(change);
            else if (change < 0f)
                PowerGrid.Battery.Discharge(-change);
        }

        // The wire graph's own pins ("in", "out-oxygen", ...) mapped onto PowerNetwork's - anything
        // on a conducting box is its one shared power pin, a device's is its power input; signal
        // pins (logic components) aren't part of the power network at all.
        PinRef? ElectricalPin(PinRef pin) =>
            conducting.Contains(pin.ComponentId) ? new PinRef(pin.ComponentId, ElectricalDevice.Power)
            : consumers.Contains(pin.ComponentId) ? new PinRef(pin.ComponentId, ElectricalDevice.PowerIn)
            : pin.ComponentId == WireGraphFactory.ReactorId ? new PinRef(pin.ComponentId, ElectricalDevice.PowerOut)
            : pin.ComponentId == WireGraphFactory.BatteryId && battery is not null
                ? new PinRef(pin.ComponentId, pin.PinId == ComponentDefinitions.BatteryInPin().Id ? ElectricalDevice.PowerIn : ElectricalDevice.PowerOut)
            : null;
    }

    // A system's load shared between however many devices this hull gives it; anything else wired
    // as a device (a hull camera's junction) draws a token amount.
    private float DemandOf(string deviceId)
    {
        if (Ship.SystemDevices.FirstOrDefault(d => d.Id == deviceId) is { } device)
            return (device.System == PowerSystemId.Engine ? EngineDemand : SystemDemand)
                / Math.Max(1, Ship.SystemDevices.Count(d => d.System == device.System));
        return CameraDemand;
    }

    // The share of its demand a device got last tick, 0..1 - Barotrauma's "voltage".
    public float GetDeviceVoltage(string deviceId) => _deviceVoltage.GetValueOrDefault(deviceId);

    // The power actually reaching a system - what every consumer of power reads (oxygen, engines,
    // shields, weapon charging, lighting mood). Full power is SystemDemand, the same figure the old
    // even split handed each system; a cut wire, a broken box or a grid short of power all lower it.
    //
    // A hull with no Secondary device of its own (most editor-built ships) still has its lamps,
    // airlocks and scanner - they hang straight off the main grid, so Secondary gets its full draw
    // scaled by that grid's voltage (dark only when the grid itself has nothing to give).
    public float GetEffectivePower(PowerSystemId system)
    {
        if (system == PowerSystemId.Secondary && Ship.SystemDevices.All(d => d.System != PowerSystemId.Secondary))
            return SystemDemand * _mainGridVoltage;
        return Ship.SystemDevices.Where(d => d.System == system)
            .Sum(d => _deviceDemand.GetValueOrDefault(d.Id) * _deviceVoltage.GetValueOrDefault(d.Id));
    }

    // The HUD's PowerState: "Allocated" now carries what each system is actually getting (there are
    // no sliders left to report), so every panel and the lighting mood that read it keep working.
    private PowerState CreatePowerState() => new(
        PowerGrid.Reactor.MaxOutput,
        PowerGrid.Reactor.CurrentOutput,
        PowerGrid.Reactor.Fuel,
        PowerGrid.Reactor.MaxFuel,
        PowerGrid.Battery.Charge,
        PowerGrid.Battery.Capacity,
        Enum.GetValues<PowerSystemId>().ToDictionary(s => s, GetEffectivePower),
        _reactorLoad,
        _reactorDelivered,
        PowerGrid.Reactor.Running,
        PowerGrid.Reactor.PlasmaTemperature);

    // The start/stop button on the reactor panel (W3) - same proximity rule as the reactor's levers.
    private void HandleToggleReactorRunning(Character character)
    {
        if ((Ship.ReactorBlock.Position - character.Position).Length() < ReactorReach)
            PowerGrid.Reactor.Running = !PowerGrid.Reactor.Running;
    }
}
