using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The Signal half of the component graph (World.Wiring.cs carries the Power half) - gates, a
// timer, a memory latch, a manual relay, and from M22 sensors and actuators. Every kind here has
// at most one output pin (ComponentDefinitions.PinsFor), so one float-free bool per component id
// is the entire state a settled circuit needs.
public sealed partial class World
{
    private readonly Dictionary<string, bool> _signalOutput = new();
    private readonly Dictionary<string, float> _timerHeldSeconds = new();

    private const int MaxPropagationPasses = 8;

    private void StepComponentLogic(double deltaSeconds)
    {
        // Sensors: side-effect-free world-state reads with no dependency on anything else in this
        // graph, so they go first. Each reads the ROOM IT'S MOUNTED IN (Component.RoomId) - no
        // separate "which room to watch" configuration needed, since a sensor only ever cares about
        // wherever it physically is. PowerLossSensor is the one exception: it watches the reactor
        // as a whole rather than any single PowerSystemId, a deliberate simplification for the
        // first version (World.Wiring.cs's GetEffectivePower is already per-system if this needs to
        // narrow later). Relay needs no per-tick computation at all: its value only ever changes
        // via ToggleRelay, a direct player action.
        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.OxygenSensor))
            _signalOutput[sensor.Id] = _roomOxygen.GetValueOrDefault(sensor.RoomId, FullOxygen) < OxygenSafeThreshold;

        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.BreachSensor))
            _signalOutput[sensor.Id] = Ship.WallBlocks.Any(b => b.RoomId == sensor.RoomId && IsWallBlockBreached(b.Id));

        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.PowerLossSensor))
            _signalOutput[sensor.Id] = PowerGrid.Reactor.CurrentOutput <= 0f;

        foreach (var sensor in _components.Where(c => c.Kind == ComponentKind.MotionSensor))
            _signalOutput[sensor.Id] = _characters.Values.Any(c => c.RoomId == sensor.RoomId && !c.IsBot && c.Health > 0f);

        // Bounded-pass relaxation for gates only, NOT a topological sort: player wiring can and
        // will contain feedback loops (e.g. a Memory's own output fed back through a NOT gate into
        // its own Reset is a normal, intentional blinker circuit, not a bug) - a DAG-only algorithm
        // would either reject those outright or need a full cycle-detection pass on every wiring
        // change. Each tick starts from LAST tick's settled values (never reset to false first), so
        // a stable circuit stays stable and only genuine changes ripple; a circuit that doesn't
        // converge within the pass budget just alternates tick to tick - the blinking-light effect
        // a player who wired that loop was actually going for, not an error state to guard against.
        for (var pass = 0; pass < MaxPropagationPasses; pass++)
            foreach (var gate in _components.Where(c => IsGateKind(c.Kind)))
                _signalOutput[gate.Id] = EvaluateGate(gate);

        // Timer: an ON-delay (not an edge pulse) - the simplest primitive matching "Timer/Delay" as
        // asked, needing only one piece of persistent state (how long "trigger" has been
        // continuously true).
        foreach (var timer in _components.Where(c => c.Kind == ComponentKind.Timer))
        {
            var held = ReadSignalInput(timer.Id, "trigger")
                ? _timerHeldSeconds.GetValueOrDefault(timer.Id) + (float)deltaSeconds
                : 0f;
            _timerHeldSeconds[timer.Id] = held;
            _signalOutput[timer.Id] = held >= timer.TimerSeconds;
        }

        // Memory: an SR latch. Reset wins on a tie - the safe default for an alarm-latch use case
        // ("clear the intrusion memory" should never lose to a simultaneous new trigger).
        foreach (var memory in _components.Where(c => c.Kind == ComponentKind.Memory))
        {
            if (ReadSignalInput(memory.Id, "reset"))
                _signalOutput[memory.Id] = false;
            else if (ReadSignalInput(memory.Id, "set"))
                _signalOutput[memory.Id] = true;
            // else: holds whatever it already was - the entire point of a latch.
        }

        // Actuators are pure consumers, applied last so they see this tick's fully settled signals.
        // AutoDoorController forces its target door OPEN when its input is true; when false it just
        // releases control back to manual toggling rather than forcing closed - slamming a door on
        // a player standing in the frame is a bad, unrequested effect nothing here asked for.
        // AlarmKlaxon/LightToggle have no world effect of their own to apply - their whole value is
        // the visual state the client reads off ComponentState, computed here as a pass-through of
        // their single input.
        foreach (var door in _components.Where(c => c.Kind == ComponentKind.AutoDoorController))
        {
            if (door.TargetId is { } doorId && ReadSignalInput(door.Id, "open"))
                _doorOpen[doorId] = true;
        }

        foreach (var alarm in _components.Where(c => c.Kind == ComponentKind.AlarmKlaxon))
            _signalOutput[alarm.Id] = ReadSignalInput(alarm.Id, "on");

        foreach (var light in _components.Where(c => c.Kind == ComponentKind.LightToggle))
            _signalOutput[light.Id] = ReadSignalInput(light.Id, "on");
    }

    private static bool IsGateKind(ComponentKind kind) =>
        kind is ComponentKind.GateAnd or ComponentKind.GateOr or ComponentKind.GateNot or ComponentKind.GateXor;

    private bool EvaluateGate(Component gate) => gate.Kind switch
    {
        ComponentKind.GateAnd => ReadSignalInput(gate.Id, "in-a") && ReadSignalInput(gate.Id, "in-b"),
        ComponentKind.GateOr => ReadSignalInput(gate.Id, "in-a") || ReadSignalInput(gate.Id, "in-b"),
        ComponentKind.GateNot => !ReadSignalInput(gate.Id, "in"),
        ComponentKind.GateXor => ReadSignalInput(gate.Id, "in-a") != ReadSignalInput(gate.Id, "in-b"),
        _ => false,
    };

    // A Signal input pin has at most one wire (Wire.cs's cardinality rule), so this is unambiguous
    // by construction - no combine rule needed. Unwired or damaged reads as false (inert), never an
    // error - matching how an unwired Power input already behaves (IsPinPowered).
    private bool ReadSignalInput(string componentId, string pinId)
    {
        var target = new PinRef(componentId, pinId);
        var wire = _wires.FirstOrDefault(w => w.ToPin == target);
        if (wire is null || _wireDamaged[wire.Id])
            return false;
        return _signalOutput.GetValueOrDefault(wire.FromPin.ComponentId);
    }

    // A Relay's whole purpose: a manual signal source the player flips directly (World.cs's
    // ComponentOperateId), same trusted-client convention as DoorToggleId - no proximity check
    // here, the client only offers the click once it's already standing near the thing.
    public void ToggleRelay(string componentId)
    {
        if (_components.FirstOrDefault(c => c.Id == componentId) is { Kind: ComponentKind.Relay })
            _signalOutput[componentId] = !_signalOutput.GetValueOrDefault(componentId);
    }
}
