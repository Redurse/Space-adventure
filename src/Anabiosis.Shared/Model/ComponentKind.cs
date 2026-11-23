namespace Anabiosis.Shared.Model;

// Replaces WireNodeKind. Distribution/Junction/Device are the ship's built-in, non-removable power
// backbone (WireGraphFactory) - the same three roles WireNode.Kind used to distinguish. Everything
// else is a purchasable signal-logic part installed at a ComponentMount (M23) - one value per
// distinct part, the same "one enum value per thing" convention ItemType already uses, so each maps
// 1:1 onto its own ItemType.
public enum ComponentKind
{
    Distribution,
    Junction,
    Device,

    GateAnd,
    GateOr,
    GateNot,
    GateXor,
    Timer,
    Memory,
    Relay,

    OxygenSensor,
    BreachSensor,
    PowerLossSensor,
    MotionSensor,

    AutoDoorController,
    AlarmKlaxon,
    LightToggle,

    // W4 of wiring-plan.md - the reactor and the battery become real ends of real wires (reactor ->
    // distribution, distribution <-> battery) instead of being hooked up implicitly. Appended, not
    // grouped with the backbone above, so every existing value keeps its number.
    Reactor,
    Battery,

    // W6 - the ship's doors and wall lamps as wiring components: they carry signal pins only
    // (toggle / set_state, a door's state_out), generated per hull like the backbone above.
    Door,
    Lamp,

    // W7 of wiring-plan.md - more logic: a delay line, a generator, a comparison, a value check, a push button.
    // W8 - signal maths, a function of one value, and the wireless module with channels. Appended so every
    // existing value keeps its number.
    Delay,
    Oscillator,
    Comparator,
    SignalCheck,
    Button,
    Math,
    Function,
    Wireless,

    // Parts with a text setting (Component.Text): "1" when the signal matches the pattern, and a keyboard that sends what was typed.
    Regex,
    Keyboard,
}
