namespace Anabiosis.Shared.Model;

// W7 of wiring-plan.md - the settings panel of a logic part: what can be set on each kind, and the one place a setting is changed (the server
// applies a click, the client draws the row from the same description, tests call it directly).
//
//  * Time - TimerSeconds (timer, delay, oscillator period);
//  * Value - Component.Value: the number a comparison / sum is made against when the second input is not wired, the number a signal
//    check waits for, the channel of a wireless module;
//  * Mode - which operation (comparison, arithmetic, function).
public enum ComponentSettingAction
{
    None = 0,
    TimeUp = 1,
    TimeDown = 2,
    ValueUp = 3,
    ValueDown = 4,
    ModeNext = 5,
    // Not a change by itself: the client starts typing into the part's text (the new text goes over ClientCommand.ComponentText).
    EditText = 6,
}

public static class ComponentSettings
{
    public const float MinSeconds = 0.5f;
    public const float MaxSeconds = 60f;
    public const float SecondsStep = 0.5f;
    public const float MaxValue = 999f;
    public const float MaxChannel = 99f;

    public static readonly IReadOnlyList<string> ComparisonModes = new[] { ">", "<", "=", "!=", ">=", "<=" };
    public static readonly IReadOnlyList<string> MathModes = new[] { "+", "-", "*", "/" };
    public static readonly IReadOnlyList<string> FunctionModes = new[] { "модуль", "корень", "округление", "минус", "синус" };

    public static bool HasTime(ComponentKind kind) => kind is ComponentKind.Timer or ComponentKind.Delay or ComponentKind.Oscillator;

    public static bool HasValue(ComponentKind kind) =>
        kind is ComponentKind.Comparator or ComponentKind.SignalCheck or ComponentKind.Math or ComponentKind.Wireless;

    public static IReadOnlyList<string>? Modes(ComponentKind kind) => kind switch
    {
        ComponentKind.Comparator => ComparisonModes,
        ComponentKind.Math => MathModes,
        ComponentKind.Function => FunctionModes,
        _ => null,
    };

    public const int MaxTextLength = 64;

    public static bool HasText(ComponentKind kind) => kind is ComponentKind.Regex or ComponentKind.Keyboard;

    public static string TextLabel(ComponentKind kind) => kind == ComponentKind.Regex ? "Шаблон" : "Сообщение";

    public static bool HasSettings(ComponentKind kind) => HasTime(kind) || HasValue(kind) || HasText(kind) || Modes(kind) is not null;

    // The text a part accepts: no control characters, at most MaxTextLength long. Null when the part has no text setting.
    public static string? CleanText(ComponentKind kind, string? text)
    {
        if (!HasText(kind))
            return null;
        var clean = new string((text ?? "").Where(c => !char.IsControl(c)).ToArray());
        return clean.Length > MaxTextLength ? clean[..MaxTextLength] : clean;
    }

    public static string TimeLabel(ComponentKind kind) => kind switch
    {
        ComponentKind.Oscillator => "Период",
        ComponentKind.Delay => "Задержка",
        _ => "Время",
    };

    public static string ValueLabel(ComponentKind kind) => kind switch
    {
        ComponentKind.Wireless => "Канал",
        ComponentKind.SignalCheck => "Ждать значение",
        _ => "Число (если вход B пуст)",
    };

    public static string ModeLabel(Component component) =>
        Modes(component.Kind) is { Count: > 0 } modes ? modes[Math.Clamp(component.Mode, 0, modes.Count - 1)] : "";

    // The part with the setting changed, or the same part when the action does not apply to it or is already at its limit.
    public static Component Apply(Component component, ComponentSettingAction action)
    {
        switch (action)
        {
            case ComponentSettingAction.TimeUp or ComponentSettingAction.TimeDown when HasTime(component.Kind):
                var seconds = component.TimerSeconds + (action == ComponentSettingAction.TimeUp ? SecondsStep : -SecondsStep);
                return component with { TimerSeconds = Math.Clamp(seconds, MinSeconds, MaxSeconds) };

            case ComponentSettingAction.ValueUp or ComponentSettingAction.ValueDown when HasValue(component.Kind):
                var step = action == ComponentSettingAction.ValueUp ? 1f : -1f;
                var (low, high) = component.Kind == ComponentKind.Wireless ? (0f, MaxChannel) : (-MaxValue, MaxValue);
                return component with { Value = Math.Clamp(component.Value + step, low, high) };

            case ComponentSettingAction.ModeNext when Modes(component.Kind) is { Count: > 0 } modes:
                return component with { Mode = (Math.Clamp(component.Mode, 0, modes.Count - 1) + 1) % modes.Count };

            default:
                return component;
        }
    }
}
