namespace Anabiosis.Shared.Model;

// Central catalog of per-ComponentKind metadata, mirroring ItemDefinitions.cs's role for ItemType:
// one place both the server (pin-count/compatibility validation) and the client (rendering/hit-
// testing, from M20 on) read instead of each hardcoding its own copy.
//
// Distribution and Junction are deliberately NOT covered by PinsFor: their pin count depends on
// which PowerSystemIds this particular hull actually has a device for (a hull missing an Engine
// block simply has no "out-engine" pin at all), so their pins are synthesized per-hull by
// DistributionPins/JunctionPins instead of looked up from a fixed table.
public static class ComponentDefinitions
{
    public static IReadOnlyList<Pin> PinsFor(ComponentKind kind) => kind switch
    {
        ComponentKind.Device => new[] { new Pin("in", PinKind.PowerIn) },

        ComponentKind.GateAnd or ComponentKind.GateOr or ComponentKind.GateXor => new[]
        {
            new Pin("in-a", PinKind.SignalIn), new Pin("in-b", PinKind.SignalIn), new Pin("out", PinKind.SignalOut),
        },
        ComponentKind.GateNot => new[] { new Pin("in", PinKind.SignalIn), new Pin("out", PinKind.SignalOut) },
        ComponentKind.Timer => new[] { new Pin("trigger", PinKind.SignalIn), new Pin("out", PinKind.SignalOut) },
        ComponentKind.Memory => new[]
        {
            new Pin("set", PinKind.SignalIn), new Pin("reset", PinKind.SignalIn), new Pin("out", PinKind.SignalOut),
        },
        ComponentKind.Relay => new[] { new Pin("out", PinKind.SignalOut) },

        // W7/W8: the value parts. Pins "in-a"/"in-b" take numbers (or on/off), "out" gives back "1"/"0" or a number.
        ComponentKind.Delay or ComponentKind.SignalCheck or ComponentKind.Function or ComponentKind.Wireless
            or ComponentKind.Regex or ComponentKind.Keyboard =>
            new[] { new Pin("in", PinKind.SignalIn), new Pin("out", PinKind.SignalOut) },
        ComponentKind.Comparator or ComponentKind.Math => new[]
        {
            new Pin("in-a", PinKind.SignalIn), new Pin("in-b", PinKind.SignalIn), new Pin("out", PinKind.SignalOut),
        },
        // Runs while "enable" is true - or always, when nothing is wired to it.
        ComponentKind.Oscillator => new[] { new Pin("enable", PinKind.SignalIn), new Pin("out", PinKind.SignalOut) },
        ComponentKind.Button => new[] { new Pin("out", PinKind.SignalOut) },

        ComponentKind.OxygenSensor or ComponentKind.BreachSensor
            or ComponentKind.PowerLossSensor or ComponentKind.MotionSensor =>
            new[] { new Pin("out", PinKind.SignalOut) },

        ComponentKind.AutoDoorController => new[] { new Pin("open", PinKind.SignalIn) },
        ComponentKind.AlarmKlaxon or ComponentKind.LightToggle => new[] { new Pin("on", PinKind.SignalIn) },

        ComponentKind.Door or ComponentKind.Lamp => DeviceSignalPins(kind),

        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind,
            "Distribution/Junction pins are hull-dependent - use DistributionPins/JunctionPins instead."),
    };

    // One PowerOut pin per system this hull actually has at least one device for (WireGraphFactory) -
    // "out-oxygen", "out-engine", etc.
    public static Pin DistributionOutPin(PowerSystemId system) => new($"out-{system}".ToLowerInvariant(), PinKind.PowerOut);

    // A junction has exactly one PowerIn (from Distribution) and exactly one PowerOut, always - a
    // system with several identical devices (Engine/Shields on some hulls) gets one junction PER
    // device instead of one shared box fanning out to all of them (WireGraphFactory), so each
    // device's own junction only ever needs its single "out-0" pin.
    public static Pin JunctionInPin() => new("in", PinKind.PowerIn);
    public static Pin JunctionOutPin(int deviceIndex) => new($"out-{deviceIndex}", PinKind.PowerOut);

    // W4 of wiring-plan.md - the power sources as real wire ends. Distribution gets an input (the
    // reactor's feed and the battery's discharge both land on it) and a dedicated output that
    // charges the battery; the battery has one of each, the reactor only an output.
    public static Pin DistributionInPin() => new("in", PinKind.PowerIn);
    public static Pin DistributionBatteryPin() => new("out-battery", PinKind.PowerOut);
    public static Pin ReactorOutPin() => new("out", PinKind.PowerOut);
    public static Pin BatteryInPin() => new("in", PinKind.PowerIn);
    public static Pin BatteryOutPin() => new("out", PinKind.PowerOut);

    // The ship's own power backbone - generated per hull (WireGraphFactory), never bought or
    // installed; everything else is a signal-logic part sitting in a ComponentMount.
    public static bool IsPowerBackbone(ComponentKind kind) =>
        kind is ComponentKind.Distribution or ComponentKind.Junction or ComponentKind.Device
            or ComponentKind.Reactor or ComponentKind.Battery;

    // Everything generated per hull rather than installed by the player: the power backbone plus
    // the doors and lamps (W6) - kept by InitializeWiring's reconcile as long as it still stands.
    public static bool IsShipFixture(ComponentKind kind) =>
        IsPowerBackbone(kind) || kind is ComponentKind.Door or ComponentKind.Lamp;

    // W6 of wiring-plan.md - Barotrauma's signal pins on the ship's own devices (alongside their
    // power pins, for those that have any): readings out, commands in.
    //  - reactor: what it gives and is asked for, plasma temperature, fuel, whether it's running;
    //    toggle/set_state work its start button;
    //  - battery: charge in percent and in units;
    //  - distribution block, junction box: the power and load of the grid they're on;
    //  - door: toggle, set_state (1 open / 0 closed), state_out;
    //  - lamp: toggle, set_state (1 on / 0 off).
    public static IReadOnlyList<Pin> DeviceSignalPins(ComponentKind kind) => kind switch
    {
        ComponentKind.Reactor => new[]
        {
            new Pin("toggle", PinKind.SignalIn), new Pin("set_state", PinKind.SignalIn),
            new Pin("power_value_out", PinKind.SignalOut), new Pin("load_value_out", PinKind.SignalOut),
            new Pin("temperature_out", PinKind.SignalOut), new Pin("fuel_out", PinKind.SignalOut),
            new Pin("state_out", PinKind.SignalOut),
        },
        ComponentKind.Battery => new[] { new Pin("charge_%", PinKind.SignalOut), new Pin("charge", PinKind.SignalOut) },
        ComponentKind.Distribution or ComponentKind.Junction => new[]
        {
            new Pin("power_value_out", PinKind.SignalOut), new Pin("load_value_out", PinKind.SignalOut),
        },
        ComponentKind.Door => new[]
        {
            new Pin("toggle", PinKind.SignalIn), new Pin("set_state", PinKind.SignalIn), new Pin("state_out", PinKind.SignalOut),
        },
        ComponentKind.Lamp => new[] { new Pin("toggle", PinKind.SignalIn), new Pin("set_state", PinKind.SignalIn) },
        _ => Array.Empty<Pin>(),
    };

    // 1:1 with the 14 purchasable ItemType values (World.ComponentMounts.cs, M23) - Distribution/
    // Junction/Device are never purchasable, so they have no ItemType counterpart.
    public static ItemType? ItemTypeFor(ComponentKind kind) => kind switch
    {
        ComponentKind.GateAnd => ItemType.GateAnd,
        ComponentKind.GateOr => ItemType.GateOr,
        ComponentKind.GateNot => ItemType.GateNot,
        ComponentKind.GateXor => ItemType.GateXor,
        ComponentKind.Timer => ItemType.Timer,
        ComponentKind.Memory => ItemType.Memory,
        ComponentKind.Relay => ItemType.Relay,
        ComponentKind.OxygenSensor => ItemType.OxygenSensor,
        ComponentKind.BreachSensor => ItemType.BreachSensor,
        ComponentKind.PowerLossSensor => ItemType.PowerLossSensor,
        ComponentKind.MotionSensor => ItemType.MotionSensor,
        ComponentKind.AutoDoorController => ItemType.AutoDoorController,
        ComponentKind.AlarmKlaxon => ItemType.AlarmKlaxon,
        ComponentKind.LightToggle => ItemType.LightToggle,
        ComponentKind.Delay => ItemType.Delay,
        ComponentKind.Oscillator => ItemType.Oscillator,
        ComponentKind.Comparator => ItemType.Comparator,
        ComponentKind.SignalCheck => ItemType.SignalCheck,
        ComponentKind.Button => ItemType.Button,
        ComponentKind.Math => ItemType.MathPart,
        ComponentKind.Function => ItemType.FunctionPart,
        ComponentKind.Wireless => ItemType.WirelessPart,
        ComponentKind.Regex => ItemType.RegexPart,
        ComponentKind.Keyboard => ItemType.TerminalPart,
        _ => null,
    };

    public static ComponentKind? ComponentKindFor(ItemType item) => item switch
    {
        ItemType.GateAnd => ComponentKind.GateAnd,
        ItemType.GateOr => ComponentKind.GateOr,
        ItemType.GateNot => ComponentKind.GateNot,
        ItemType.GateXor => ComponentKind.GateXor,
        ItemType.Timer => ComponentKind.Timer,
        ItemType.Memory => ComponentKind.Memory,
        ItemType.Relay => ComponentKind.Relay,
        ItemType.OxygenSensor => ComponentKind.OxygenSensor,
        ItemType.BreachSensor => ComponentKind.BreachSensor,
        ItemType.PowerLossSensor => ComponentKind.PowerLossSensor,
        ItemType.MotionSensor => ComponentKind.MotionSensor,
        ItemType.AutoDoorController => ComponentKind.AutoDoorController,
        ItemType.AlarmKlaxon => ComponentKind.AlarmKlaxon,
        ItemType.LightToggle => ComponentKind.LightToggle,
        ItemType.Delay => ComponentKind.Delay,
        ItemType.Oscillator => ComponentKind.Oscillator,
        ItemType.Comparator => ComponentKind.Comparator,
        ItemType.SignalCheck => ComponentKind.SignalCheck,
        ItemType.Button => ComponentKind.Button,
        ItemType.MathPart => ComponentKind.Math,
        ItemType.FunctionPart => ComponentKind.Function,
        ItemType.WirelessPart => ComponentKind.Wireless,
        ItemType.RegexPart => ComponentKind.Regex,
        ItemType.TerminalPart => ComponentKind.Keyboard,
        _ => null,
    };

    public static string DisplayName(ComponentKind kind) => kind switch
    {
        ComponentKind.Distribution => "распределительный блок",
        ComponentKind.Junction => "щиток",
        ComponentKind.Device => "потребитель",
        ComponentKind.Reactor => "реактор",
        ComponentKind.Battery => "батарея",
        ComponentKind.Door => "дверь",
        ComponentKind.Lamp => "лампа",
        ComponentKind.GateAnd => "логический элемент И",
        ComponentKind.GateOr => "логический элемент ИЛИ",
        ComponentKind.GateNot => "логический элемент НЕ",
        ComponentKind.GateXor => "логический элемент Искл.ИЛИ",
        ComponentKind.Timer => "таймер",
        ComponentKind.Memory => "элемент памяти",
        ComponentKind.Relay => "реле",
        ComponentKind.OxygenSensor => "датчик кислорода",
        ComponentKind.BreachSensor => "датчик пробоины",
        ComponentKind.PowerLossSensor => "датчик потери питания",
        ComponentKind.MotionSensor => "датчик движения",
        ComponentKind.AutoDoorController => "контроллер двери",
        ComponentKind.AlarmKlaxon => "сирена",
        ComponentKind.LightToggle => "переключатель света",
        ComponentKind.Delay => "линия задержки",
        ComponentKind.Oscillator => "генератор",
        ComponentKind.Comparator => "компаратор",
        ComponentKind.SignalCheck => "проверка сигнала",
        ComponentKind.Button => "кнопка",
        ComponentKind.Math => "математический блок",
        ComponentKind.Function => "функциональный блок",
        ComponentKind.Wireless => "беспроводной модуль",
        ComponentKind.Regex => "регулярное выражение",
        ComponentKind.Keyboard => "терминал",
        _ => kind.ToString(),
    };

    public static string ShortLabel(ComponentKind kind) => kind switch
    {
        ComponentKind.GateAnd => "AND",
        ComponentKind.GateOr => "OR",
        ComponentKind.GateNot => "NOT",
        ComponentKind.GateXor => "XOR",
        ComponentKind.Timer => "TIM",
        ComponentKind.Memory => "MEM",
        ComponentKind.Relay => "REL",
        ComponentKind.OxygenSensor => "O2S",
        ComponentKind.BreachSensor => "BR",
        ComponentKind.PowerLossSensor => "PWR",
        ComponentKind.MotionSensor => "MOT",
        ComponentKind.AutoDoorController => "DOOR",
        ComponentKind.AlarmKlaxon => "ALM",
        ComponentKind.LightToggle => "LT",
        ComponentKind.Delay => "DEL",
        ComponentKind.Oscillator => "OSC",
        ComponentKind.Comparator => "CMP",
        ComponentKind.SignalCheck => "TST",
        ComponentKind.Button => "BTN",
        ComponentKind.Math => "MAT",
        ComponentKind.Function => "FUN",
        ComponentKind.Wireless => "WRL",
        ComponentKind.Regex => "REX",
        ComponentKind.Keyboard => "TRM",
        _ => "?",
    };
}
