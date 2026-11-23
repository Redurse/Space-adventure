using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The Signal half of the component graph (World.Wiring.cs carries the Power half).
//
// W6 of wiring-plan.md - Barotrauma's signal layer: every signal output pin carries a string
// (_pinSignal) - "1"/"0" for on/off, numbers as text, or nothing at all. Each tick:
//  1. sources publish: the ship's devices (reactor/battery/junction box readings, door states -
//     ComponentDefinitions.DeviceSignalPins), sensors, relays, and the timer/memory state;
//  2. the gates settle by bounded-pass relaxation within the same tick (zero delay, as the user
//     asked), loops allowed;
//  3. stateful parts (timer, memory) update;
//  4. consumers act on what arrived: doors, lamps, the reactor's start button, the auto-door
//     controller. "toggle" acts on a rising edge (a held button doesn't flicker a door every tick);
//     "set_state" acts whenever the value it receives changes.
// An input wired to several outputs reads the first one that's truthy, else the first value at all.
// _signalOutput keeps one on/off per logic part - what the client's ComponentState shows.
public sealed partial class World
{
    private readonly Dictionary<string, bool> _signalOutput = new();
    private readonly Dictionary<string, float> _timerHeldSeconds = new();
    private readonly Dictionary<PinRef, string> _pinSignal = new();
    // What each consumer input read last tick - for toggle's rising edge and set_state's "changed".
    private readonly Dictionary<PinRef, string?> _lastInputSignal = new();
    // Wall lamps a signal has switched off (W6). Lamps start on.
    private readonly HashSet<string> _lampsOff = new();

    private const int MaxPropagationPasses = 32;

    private void StepComponentLogic(double deltaSeconds)
    {
        _pinSignal.Clear();
        PublishDeviceSignals();
        PublishValueParts();

        // Sensors: side-effect-free world-state reads. Each reads the ROOM IT'S MOUNTED IN
        // (Component.RoomId). PowerLossSensor watches the reactor as a whole. A relay's value only
        // ever changes via ToggleRelay, a direct player action.
        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.OxygenSensor))
            _signalOutput[sensor.Id] = _roomOxygen.GetValueOrDefault(sensor.RoomId, FullOxygen) < OxygenSafeThreshold;
        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.BreachSensor))
            _signalOutput[sensor.Id] = Ship.WallBlocks.Any(b => b.RoomId == sensor.RoomId && IsWallBlockBreached(b.Id));
        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.PowerLossSensor))
            _signalOutput[sensor.Id] = PowerGrid.Reactor.CurrentOutput <= 0f;
        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.MotionSensor))
            _signalOutput[sensor.Id] = _characters.Values.Any(c => c.RoomId == sensor.RoomId && !c.IsBot && c.Health > 0f);

        // Every part with an "out" pin publishes its current on/off - sensors as just computed,
        // relays/timers/memories as they stood (their state updates below, seen next tick).
        foreach (var part in _components.Where(c => HasOutPin(c.Kind) && !IsGateKind(c.Kind)))
            _pinSignal[new PinRef(part.Id, "out")] = SignalValues.Of(_signalOutput.GetValueOrDefault(part.Id));

        // Bounded-pass relaxation for gates, NOT a topological sort: player wiring can and will
        // contain feedback loops (a Memory's output fed back through a NOT gate into its own Reset
        // is a normal blinker). Gates start from last tick's values, so a stable circuit stays
        // stable; one that doesn't converge within the budget alternates tick to tick - the
        // blinking a player who wired that loop was going for.
        foreach (var gate in _components.Where(c => IsGateKind(c.Kind)))
            _pinSignal[new PinRef(gate.Id, "out")] = SignalValues.Of(_signalOutput.GetValueOrDefault(gate.Id));
        for (var pass = 0; pass < MaxPropagationPasses; pass++)
        {
            var changed = false;
            foreach (var gate in _components.Where(c => IsGateKind(c.Kind)))
            {
                var value = EvaluateGate(gate);
                changed |= _signalOutput.GetValueOrDefault(gate.Id) != value;
                _signalOutput[gate.Id] = value;
                _pinSignal[new PinRef(gate.Id, "out")] = SignalValues.Of(value);
            }
            changed |= RelaxValueParts(); // comparison, maths and the like settle in the same passes
            if (!changed)
                break;
        }

        // Timer: an ON-delay - how long "trigger" has been continuously true.
        foreach (var timer in _components.Where(c => c.Kind == ComponentKind.Timer))
        {
            var held = ReadSignalInput(timer.Id, "trigger")
                ? _timerHeldSeconds.GetValueOrDefault(timer.Id) + (float)deltaSeconds
                : 0f;
            _timerHeldSeconds[timer.Id] = held;
            _signalOutput[timer.Id] = held >= timer.TimerSeconds;
        }

        // Memory: an SR latch. Reset wins on a tie (an alarm latch's "clear" must never lose).
        foreach (var memory in _components.Where(c => c.Kind == ComponentKind.Memory))
        {
            if (ReadSignalInput(memory.Id, "reset"))
                _signalOutput[memory.Id] = false;
            else if (ReadSignalInput(memory.Id, "set"))
                _signalOutput[memory.Id] = true;
        }

        AdvanceValueParts(deltaSeconds);

        // Consumers, applied last so they see this tick's fully settled signals. AutoDoorController
        // forces its door OPEN while its input is true and otherwise leaves the door alone.
        foreach (var door in _components.Where(c => c.Kind == ComponentKind.AutoDoorController))
        {
            if (door.TargetId is { } doorId && ReadSignalInput(door.Id, "open"))
                SetDoorOpenBySignal(doorId, true);
        }
        foreach (var alarm in _components.Where(c => c.Kind == ComponentKind.AlarmKlaxon))
            _signalOutput[alarm.Id] = ReadSignalInput(alarm.Id, "on");
        foreach (var light in _components.Where(c => c.Kind == ComponentKind.LightToggle))
            _signalOutput[light.Id] = ReadSignalInput(light.Id, "on");

        ApplyDeviceSignalInputs();
    }

    // The ship's devices' readings (W6) - from the last power step and the world as it stands.
    private void PublishDeviceSignals()
    {
        foreach (var component in _components)
        {
            switch (component.Kind)
            {
                case ComponentKind.Reactor:
                    Publish(component.Id, "power_value_out", SignalValues.Of(_reactorDelivered));
                    Publish(component.Id, "load_value_out", SignalValues.Of(_reactorLoad));
                    Publish(component.Id, "temperature_out", SignalValues.Of(PowerGrid.Reactor.PlasmaTemperature));
                    Publish(component.Id, "fuel_out", SignalValues.Of(PowerGrid.Reactor.Fuel));
                    Publish(component.Id, "state_out", SignalValues.Of(PowerGrid.Reactor.Running));
                    break;
                case ComponentKind.Battery:
                    var capacity = PowerGrid.Battery.Capacity;
                    Publish(component.Id, "charge_%", SignalValues.Of(capacity > 0f ? PowerGrid.Battery.Charge / capacity * 100f : 0f));
                    Publish(component.Id, "charge", SignalValues.Of(PowerGrid.Battery.Charge));
                    break;
                case ComponentKind.Distribution or ComponentKind.Junction:
                    var grid = _boxGrid.GetValueOrDefault(component.Id);
                    Publish(component.Id, "power_value_out", SignalValues.Of(grid.Power));
                    Publish(component.Id, "load_value_out", SignalValues.Of(grid.Load));
                    break;
                case ComponentKind.Door when component.TargetId is { } doorId:
                    Publish(component.Id, "state_out", SignalValues.Of(IsDoorOpen(doorId)));
                    break;
            }
        }

        void Publish(string componentId, string pinId, string value) => _pinSignal[new PinRef(componentId, pinId)] = value;
    }

    // What arrives at the ship's devices' own inputs (W6): doors, lamps, the reactor's start button.
    private void ApplyDeviceSignalInputs()
    {
        foreach (var component in _components)
        {
            if (component.Kind is not (ComponentKind.Door or ComponentKind.Lamp or ComponentKind.Reactor))
                continue;

            var (toggled, setTo) = ReadCommandInputs(component.Id);
            switch (component.Kind)
            {
                case ComponentKind.Door when component.TargetId is { } doorId:
                    if (setTo is { } open)
                        SetDoorOpenBySignal(doorId, open);
                    else if (toggled)
                        SetDoorOpenBySignal(doorId, !IsDoorOpen(doorId));
                    break;
                case ComponentKind.Lamp when component.TargetId is { } lampId:
                    var on = setTo ?? (toggled ? _lampsOff.Contains(lampId) : !_lampsOff.Contains(lampId));
                    if (on)
                        _lampsOff.Remove(lampId);
                    else
                        _lampsOff.Add(lampId);
                    break;
                case ComponentKind.Reactor:
                    if (setTo is { } running)
                        PowerGrid.Reactor.Running = running;
                    else if (toggled)
                        PowerGrid.Reactor.Running = !PowerGrid.Reactor.Running;
                    break;
            }
        }
    }

    // A device's two command inputs this tick: toggle on a rising edge; set_state whenever the value
    // it receives changes (including first arriving) - null when nothing new came in.
    private (bool Toggled, bool? SetTo) ReadCommandInputs(string componentId)
    {
        var togglePin = new PinRef(componentId, "toggle");
        var toggle = ReadSignal(componentId, "toggle");
        var toggled = SignalValues.IsTruthy(toggle) && !SignalValues.IsTruthy(_lastInputSignal.GetValueOrDefault(togglePin));
        _lastInputSignal[togglePin] = toggle;

        var setPin = new PinRef(componentId, "set_state");
        var set = ReadSignal(componentId, "set_state");
        bool? setTo = set is not null && set != _lastInputSignal.GetValueOrDefault(setPin) ? SignalValues.IsTruthy(set) : null;
        _lastInputSignal[setPin] = set;
        return (toggled, setTo);
    }

    // A door moved by a signal obeys the same rules as one moved by hand: not while the reactor's
    // door-lock lever is down, never a destroyed one.
    private void SetDoorOpenBySignal(string doorId, bool open)
    {
        if (IsDoorOpen(doorId) != open && !DoorsLocked && !IsDoorDestroyed(doorId))
            ToggleDoor(doorId);
    }

    private static bool IsGateKind(ComponentKind kind) =>
        kind is ComponentKind.GateAnd or ComponentKind.GateOr or ComponentKind.GateNot or ComponentKind.GateXor;

    private static bool HasOutPin(ComponentKind kind) =>
        !ComponentDefinitions.IsShipFixture(kind) && !IsValuePart(kind) // value parts publish their own strings (World.ComponentLogic.Values.cs)
        && ComponentDefinitions.PinsFor(kind).Any(p => p.Id == "out" && p.Kind == PinKind.SignalOut);

    private bool EvaluateGate(Component gate) => gate.Kind switch
    {
        ComponentKind.GateAnd => ReadSignalInput(gate.Id, "in-a") && ReadSignalInput(gate.Id, "in-b"),
        ComponentKind.GateOr => ReadSignalInput(gate.Id, "in-a") || ReadSignalInput(gate.Id, "in-b"),
        ComponentKind.GateNot => !ReadSignalInput(gate.Id, "in"),
        ComponentKind.GateXor => ReadSignalInput(gate.Id, "in-a") != ReadSignalInput(gate.Id, "in-b"),
        _ => false,
    };

    // The value arriving at an input pin over its intact wires: the first truthy one, else the
    // first one at all, else null (nothing wired, or nothing on the other end). Wires are matched
    // at either end - only a signal output's value counts.
    private string? ReadSignal(string componentId, string pinId)
    {
        var target = new PinRef(componentId, pinId);
        string? first = null;
        foreach (var wire in _wires)
        {
            if (_wireDamaged.GetValueOrDefault(wire.Id))
                continue;
            var other = wire.ToPin == target ? wire.FromPin : wire.FromPin == target ? wire.ToPin : (PinRef?)null;
            if (other is not { } source || !_pinSignal.TryGetValue(source, out var value))
                continue;
            if (SignalValues.IsTruthy(value))
                return value;
            first ??= value;
        }
        return first;
    }

    private bool ReadSignalInput(string componentId, string pinId) => SignalValues.IsTruthy(ReadSignal(componentId, pinId));

    // A Relay's whole purpose: a manual signal source the player flips directly (World.cs's
    // ComponentOperateId), same trusted-client convention as DoorToggleId.
    public void ToggleRelay(string componentId)
    {
        if (_components.FirstOrDefault(c => c.Id == componentId) is { Kind: ComponentKind.Relay })
            _signalOutput[componentId] = !_signalOutput.GetValueOrDefault(componentId);
    }

    // For tests and the snapshot: the value on a pin right now (null = no signal).
    public string? GetPinSignal(PinRef pin) => _pinSignal.GetValueOrDefault(pin);

    private IReadOnlyList<PinSignalState> CreatePinSignals() =>
        _pinSignal.Select(kv => new PinSignalState(kv.Key, kv.Value)).ToArray();

    private IReadOnlyList<string> CreateLampsOff() => _lampsOff.ToArray();
}
