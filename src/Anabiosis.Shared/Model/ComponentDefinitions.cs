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

        ComponentKind.OxygenSensor or ComponentKind.BreachSensor
            or ComponentKind.PowerLossSensor or ComponentKind.MotionSensor =>
            new[] { new Pin("out", PinKind.SignalOut) },

        ComponentKind.AutoDoorController => new[] { new Pin("open", PinKind.SignalIn) },
        ComponentKind.AlarmKlaxon or ComponentKind.LightToggle => new[] { new Pin("on", PinKind.SignalIn) },

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
        _ => null,
    };

    public static string DisplayName(ComponentKind kind) => kind switch
    {
        ComponentKind.Distribution => "распределительный блок",
        ComponentKind.Junction => "щиток",
        ComponentKind.Device => "потребитель",
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
        _ => "?",
    };
}
