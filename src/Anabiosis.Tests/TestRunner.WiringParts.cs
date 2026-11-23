using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W7/W8 of wiring-plan.md: the parts whose output is a value - delay, oscillator, comparator, signal check, button, maths, function and the
// wireless module - plus their settings and the shop. Seeded directly with AddComponent/AddWire like the gate tests, so evaluation is
// isolated from the economy.
internal static partial class TestRunner
{
    private static string? Out(World world, string id) => world.GetPinSignal(new PinRef(id, "out"));

    private static World WorldWithRelayInto(string partId, ComponentKind kind, string pin, Component? part = null)
    {
        var world = new World();
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(part ?? new Component(partId, kind, "corridor", 0, 0));
        world.AddWire("w", new PinRef("relay", "out"), new PinRef(partId, pin));
        return world;
    }

    private static bool World_Component_Comparator_ComparesAgainstTheConstant()
    {
        var world = WorldWithRelayInto("cmp", ComponentKind.Comparator, "in-a", new Component("cmp", ComponentKind.Comparator, "corridor", 0, 0, Value: 0.5f));
        world.Step(RealtimeStep);
        var off = Out(world, "cmp"); // 0 > 0.5 is false
        world.ToggleRelay("relay");
        world.Step(RealtimeStep);
        var on = Out(world, "cmp"); // 1 > 0.5
        // The other operations on the same numbers.
        world.ChangeComponentSetting("cmp", ComponentSettingAction.ModeNext); // "<"
        world.Step(RealtimeStep);
        var less = Out(world, "cmp"); // 1 < 0.5 is false
        return off == "0" && on == "1" && less == "0";
    }

    private static bool World_Component_SignalCheck_FiresOnTheWantedValueOnly()
    {
        var world = WorldWithRelayInto("chk", ComponentKind.SignalCheck, "in", new Component("chk", ComponentKind.SignalCheck, "corridor", 0, 0, Value: 1f));
        world.Step(RealtimeStep);
        var off = Out(world, "chk");
        world.ToggleRelay("relay");
        world.Step(RealtimeStep);
        var on = Out(world, "chk");
        return off == "0" && on == "1";
    }

    private static bool World_Component_Math_AddsMultipliesAndSurvivesDivisionByZero()
    {
        var world = WorldWithRelayInto("math", ComponentKind.Math, "in-a", new Component("math", ComponentKind.Math, "corridor", 0, 0, Value: 2.5f));
        world.ToggleRelay("relay"); // a = 1
        world.Step(RealtimeStep);
        var sum = Out(world, "math"); // 1 + 2.5
        world.ChangeComponentSetting("math", ComponentSettingAction.ModeNext); // -
        world.ChangeComponentSetting("math", ComponentSettingAction.ModeNext); // *
        world.Step(RealtimeStep);
        var product = Out(world, "math"); // 1 * 2.5
        // Dividing by a constant of 0 gives 0, not a crash or infinity.
        var zero = WorldWithRelayInto("math", ComponentKind.Math, "in-a", new Component("math", ComponentKind.Math, "corridor", 0, 0, Mode: 3, Value: 0f));
        zero.ToggleRelay("relay");
        zero.Step(RealtimeStep);
        var divided = Out(zero, "math"); // 1 / 0
        return sum == "3.5" && product == "2.5" && divided == "0";
    }

    // Maths feeding a function, in one tick: 0 + 90 = 90, then sin(90 degrees) = 1.
    private static bool World_Component_Function_ChainsWithMathInOneTick()
    {
        var world = new World();
        world.AddComponent(new Component("math", ComponentKind.Math, "corridor", 0, 0, Value: 90f));
        world.AddComponent(new Component("fn", ComponentKind.Function, "corridor", 0, 0, Mode: 4));
        world.AddWire("w", new PinRef("math", "out"), new PinRef("fn", "in"));
        for (var i = 0; i < 3; i++) // a few ticks: parts start from the last tick's value
            world.Step(RealtimeStep);
        var sine = Out(world, "fn");

        world.ChangeComponentSetting("fn", ComponentSettingAction.ModeNext); // wraps round to "модуль"
        world.Step(RealtimeStep);
        return sine == "1" && Out(world, "fn") == "90";
    }

    private static bool World_Component_Delay_PassesTheSignalLate()
    {
        var world = WorldWithRelayInto("delay", ComponentKind.Delay, "in", new Component("delay", ComponentKind.Delay, "corridor", 0, 0, TimerSeconds: 1f));
        world.ToggleRelay("relay");
        StepFor(world, 20);
        var tooEarly = Out(world, "delay"); // a third of a second
        StepFor(world, 80);
        var arrived = Out(world, "delay"); // well past a second
        return tooEarly == "0" && arrived == "1";
    }

    private static bool World_Component_Oscillator_PulsesAndCanBeSwitchedOff()
    {
        var world = new World();
        world.AddComponent(new Component("osc", ComponentKind.Oscillator, "corridor", 0, 0, TimerSeconds: 1f));
        var seen = new HashSet<string?>();
        var switches = 0;
        string? last = null;
        for (var i = 0; i < 200; i++) // a bit over three seconds at 60 ticks
        {
            world.Step(RealtimeStep);
            var now = Out(world, "osc");
            seen.Add(now);
            if (last is not null && now != last)
                switches++;
            last = now;
        }

        // Wired to an "enable" that is off, it stays quiet.
        var gated = WorldWithRelayInto("osc", ComponentKind.Oscillator, "enable", new Component("osc", ComponentKind.Oscillator, "corridor", 0, 0, TimerSeconds: 1f));
        var everOn = false;
        for (var i = 0; i < 200; i++)
        {
            gated.Step(RealtimeStep);
            everOn |= Out(gated, "osc") == "1";
        }
        return seen.Contains("1") && seen.Contains("0") && switches >= 4 && !everOn;
    }

    private static bool World_Component_Button_PulsesWhenPressed()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.AddComponent(new Component("btn", ComponentKind.Button, "corridor", 0, 0));
        world.Step(RealtimeStep);
        var idle = Out(world, "btn");
        world.ApplyCommand(1, new ClientCommand(1, ComponentOperateId: "btn"));
        world.Step(RealtimeStep);
        var pressed = Out(world, "btn");
        StepFor(world, 60); // a second later it has let go
        var released = Out(world, "btn");
        return idle == "0" && pressed == "1" && released == "0";
    }

    private static bool World_Component_Wireless_CarriesASignalOnItsChannelOnly()
    {
        var world = new World();
        world.AddComponent(new Component("relay", ComponentKind.Relay, "corridor", 0, 0));
        world.AddComponent(new Component("tx", ComponentKind.Wireless, "corridor", 0, 0, Value: 5f));
        world.AddComponent(new Component("rx", ComponentKind.Wireless, "corridor", 0, 0, Value: 5f));
        world.AddComponent(new Component("other", ComponentKind.Wireless, "corridor", 0, 0, Value: 6f));
        world.AddWire("w", new PinRef("relay", "out"), new PinRef("tx", "in"));
        world.ToggleRelay("relay");
        for (var i = 0; i < 4; i++) // sent one tick, heard the next
            world.Step(RealtimeStep);
        return Out(world, "rx") == "1" && Out(world, "other") == "0";
    }

    private static bool World_Component_Terminal_SendsItsMessageForAMoment()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.AddComponent(new Component("kb", ComponentKind.Keyboard, "corridor", 0, 0));
        world.Step(RealtimeStep);
        var quiet = Out(world, "kb");
        // Through the command, as a client sends it: control characters out, length capped.
        world.ApplyCommand(1, new ClientCommand(1, ComponentTextId: "kb", ComponentText: "he\tllo" + new string('x', 200)));
        world.Step(RealtimeStep);
        var sent = Out(world, "kb");
        StepFor(world, 60);
        var gone = Out(world, "kb");
        var stored = world.CreateSnapshot().Wiring.Components.First(c => c.Id == "kb").Text;
        return quiet == "" && sent is { } s && s.StartsWith("hellox") && stored!.Length == ComponentSettings.MaxTextLength && gone == "";
    }

    private static bool World_Component_Regex_MatchesTheMessageAndSurvivesBadPatterns()
    {
        var world = new World();
        world.AddComponent(new Component("kb", ComponentKind.Keyboard, "corridor", 0, 0));
        world.AddComponent(new Component("re", ComponentKind.Regex, "corridor", 0, 0, Text: "^he"));
        world.AddWire("w", new PinRef("kb", "out"), new PinRef("re", "in"));
        world.SetComponentText("kb", "hello");
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);
        var matches = Out(world, "re");

        world.SetComponentText("re", "^x");
        world.SetComponentText("kb", "hello"); // send again so there is something to read
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);
        var noMatch = Out(world, "re");

        world.SetComponentText("re", "("); // not a valid pattern
        world.SetComponentText("kb", "hello");
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);
        var bad = Out(world, "re");
        return matches == "1" && noMatch == "0" && bad == "0";
    }

    private static bool World_Junction_InAnEmptyRoomShortsOut()
    {
        var world = new World();
        var box = world.CreateSnapshot().Wiring.Components.First(c => c.Kind == ComponentKind.Junction).Id;
        var room = world.DebugJunctionRoom(box);
        for (var i = 0; i < 300; i++)
        {
            world.DebugSetRoomOxygen(room, 0f);
            world.Step(RealtimeStep);
        }
        return world.IsJunctionDamaged(box);
    }

    private static bool Component_Settings_StayInTheirLimitsAndCycle()
    {
        var timer = new Component("t", ComponentKind.Delay, "r", 0, 0, TimerSeconds: ComponentSettings.MinSeconds);
        var tooLow = ComponentSettings.Apply(timer, ComponentSettingAction.TimeDown);
        var up = ComponentSettings.Apply(timer, ComponentSettingAction.TimeUp);

        var channel = new Component("w", ComponentKind.Wireless, "r", 0, 0, Value: 0f);
        var below = ComponentSettings.Apply(channel, ComponentSettingAction.ValueDown);

        var cmp = new Component("c", ComponentKind.Comparator, "r", 0, 0, Mode: ComponentSettings.ComparisonModes.Count - 1);
        var wrapped = ComponentSettings.Apply(cmp, ComponentSettingAction.ModeNext);

        // A setting a part does not have changes nothing.
        var gate = new Component("g", ComponentKind.GateAnd, "r", 0, 0);
        var untouched = ComponentSettings.Apply(gate, ComponentSettingAction.ValueUp);

        return tooLow.TimerSeconds == ComponentSettings.MinSeconds
            && up.TimerSeconds == ComponentSettings.MinSeconds + ComponentSettings.SecondsStep
            && below.Value == 0f && wrapped.Mode == 0 && untouched == gate;
    }

    private static bool World_ComponentSettingCommand_ChangesThePartSetting()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.AddComponent(new Component("cmp", ComponentKind.Comparator, "corridor", 0, 0));
        world.ApplyCommand(1, new ClientCommand(1, ComponentSettingId: "cmp", ComponentSettingAction: (int)ComponentSettingAction.ModeNext));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().Wiring.Components.First(c => c.Id == "cmp").Mode == 1;
    }

    private static bool Component_WiringParts_AllHaveAnItemAndAShopEntry()
    {
        var parts = new[]
        {
            ComponentKind.Delay, ComponentKind.Oscillator, ComponentKind.Comparator, ComponentKind.SignalCheck,
            ComponentKind.Button, ComponentKind.Math, ComponentKind.Function, ComponentKind.Wireless,
            ComponentKind.Regex, ComponentKind.Keyboard,
        };
        return parts.All(kind =>
            ComponentDefinitions.ItemTypeFor(kind) is { } item
            && ComponentDefinitions.ComponentKindFor(item) == kind
            && TradeCatalog.Find(item) is not null
            && ComponentDefinitions.PinsFor(kind).Any(p => p.Id == "out"));
    }
}
