using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// W7/W8 of wiring-plan.md - the parts whose output is a VALUE rather than an on/off: a delay line, a generator, comparison, a value
// check, a push button, arithmetic, a function of one number and the wireless module.
//
// Stateless ones (comparator, check, maths, function) are worked out inside the same bounded relaxation as the gates, so they cost no
// delay and can sit in a loop. The ones that remember something (delay, oscillator, wireless, button) publish what they held at the start
// of the tick and take their new state at the end, like the timer and the memory do.
public sealed partial class World
{
    // The last output of every value part, as a string: what it publishes at the top of the next tick.
    private readonly Dictionary<string, string> _partOutput = new();
    // Delay lines: what came in and when.
    private readonly Dictionary<string, Queue<(double Time, string Value)>> _delayLine = new();
    private readonly Dictionary<string, float> _oscillatorPhase = new();
    private readonly Dictionary<string, float> _buttonHeldSeconds = new();
    // What each wireless module is sending this tick (read by the others on the same channel next tick).
    private readonly Dictionary<string, string?> _wirelessSent = new();
    private double _valuePartClock;

    public const float ButtonPulseSeconds = 0.5f;
    private const int MaxDelayLineSamples = 4000;

    private static bool IsValuePart(ComponentKind kind) =>
        kind is ComponentKind.Delay or ComponentKind.Oscillator or ComponentKind.Comparator or ComponentKind.SignalCheck
            or ComponentKind.Button or ComponentKind.Math or ComponentKind.Function or ComponentKind.Wireless
            or ComponentKind.Regex or ComponentKind.Keyboard;

    private static bool IsStatelessValuePart(ComponentKind kind) =>
        kind is ComponentKind.Comparator or ComponentKind.SignalCheck or ComponentKind.Math or ComponentKind.Function or ComponentKind.Regex;

    private void PublishValueParts()
    {
        foreach (var part in _components.Where(c => IsValuePart(c.Kind)))
        {
            var pin = new PinRef(part.Id, "out");
            _pinSignal[pin] = part.Kind switch
            {
                ComponentKind.Button => SignalValues.Of(_buttonHeldSeconds.GetValueOrDefault(part.Id) > 0f),
                // A terminal gives out the message it was sent for a moment, then nothing.
                ComponentKind.Keyboard => _buttonHeldSeconds.GetValueOrDefault(part.Id) > 0f ? part.Text ?? "" : "",
                ComponentKind.Wireless => ReadWirelessChannel(part),
                _ => _partOutput.GetValueOrDefault(part.Id, SignalValues.False),
            };
        }
    }

    // One relaxation pass over the stateless parts: true if any of them gave a different value than it had.
    private bool RelaxValueParts()
    {
        var changed = false;
        foreach (var part in _components.Where(c => IsStatelessValuePart(c.Kind)))
        {
            var value = EvaluateValuePart(part);
            var pin = new PinRef(part.Id, "out");
            changed |= _pinSignal.GetValueOrDefault(pin) != value;
            _pinSignal[pin] = value;
            _partOutput[part.Id] = value;
        }
        return changed;
    }

    private string EvaluateValuePart(Component part)
    {
        switch (part.Kind)
        {
            case ComponentKind.Comparator:
            {
                var a = SignalValues.NumberOr(ReadSignal(part.Id, "in-a"));
                var b = ReadSecondOperand(part);
                var result = part.Mode switch
                {
                    1 => a < b,
                    2 => Math.Abs(a - b) < 0.005f,
                    3 => Math.Abs(a - b) >= 0.005f,
                    4 => a >= b,
                    5 => a <= b,
                    _ => a > b,
                };
                return SignalValues.Of(result);
            }
            case ComponentKind.SignalCheck:
                return SignalValues.Of(SignalValues.TryNumber(ReadSignal(part.Id, "in"), out var seen) && Math.Abs(seen - part.Value) < 0.005f);
            case ComponentKind.Regex:
                return SignalValues.Of(RegexMatches(part.Text, ReadSignal(part.Id, "in")));
            case ComponentKind.Math:
            {
                var a = SignalValues.NumberOr(ReadSignal(part.Id, "in-a"));
                var b = ReadSecondOperand(part);
                var result = part.Mode switch
                {
                    1 => a - b,
                    2 => a * b,
                    3 => Math.Abs(b) < 0.0001f ? 0f : a / b, // dividing by nothing gives nothing, like Barotrauma's
                    _ => a + b,
                };
                return SignalValues.Of(float.IsFinite(result) ? result : 0f);
            }
            case ComponentKind.Function:
            {
                var x = SignalValues.NumberOr(ReadSignal(part.Id, "in"));
                var result = part.Mode switch
                {
                    1 => x >= 0f ? MathF.Sqrt(x) : 0f,
                    2 => MathF.Round(x),
                    3 => -x,
                    4 => MathF.Sin(x * MathF.PI / 180f), // degrees in
                    _ => MathF.Abs(x),
                };
                return SignalValues.Of(float.IsFinite(result) ? result : 0f);
            }
            default:
                return SignalValues.False;
        }
    }

    // Input B when something is wired to it, else the part's own constant.
    private float ReadSecondOperand(Component part) =>
        ReadSignal(part.Id, "in-b") is { } wired ? SignalValues.NumberOr(wired) : part.Value;

    // The stateful parts take their new state from what arrived this tick.
    private void AdvanceValueParts(double deltaSeconds)
    {
        _valuePartClock += deltaSeconds;

        foreach (var part in _components.Where(c => c.Kind == ComponentKind.Delay))
        {
            if (!_delayLine.TryGetValue(part.Id, out var line))
                _delayLine[part.Id] = line = new Queue<(double, string)>();
            line.Enqueue((_valuePartClock, ReadSignal(part.Id, "in") ?? SignalValues.False));
            // The oldest sample that is old enough is what comes out now; anything before it is stale.
            var due = _valuePartClock - part.TimerSeconds;
            var output = _partOutput.GetValueOrDefault(part.Id, SignalValues.False);
            while (line.Count > 0 && line.Peek().Time <= due)
                output = line.Dequeue().Value;
            while (line.Count > MaxDelayLineSamples)
                line.Dequeue();
            _partOutput[part.Id] = output;
        }

        foreach (var part in _components.Where(c => c.Kind == ComponentKind.Oscillator))
        {
            var enable = ReadSignal(part.Id, "enable");
            var running = enable is null || SignalValues.IsTruthy(enable); // nothing wired: it just runs
            var phase = running ? (_oscillatorPhase.GetValueOrDefault(part.Id) + (float)deltaSeconds) % part.TimerSeconds : 0f;
            _oscillatorPhase[part.Id] = phase;
            _partOutput[part.Id] = SignalValues.Of(running && phase < part.TimerSeconds / 2f);
        }

        foreach (var part in _components.Where(c => c.Kind == ComponentKind.Wireless))
            _wirelessSent[part.Id] = ReadSignal(part.Id, "in");

        foreach (var id in _buttonHeldSeconds.Keys.ToList())
            _buttonHeldSeconds[id] = Math.Max(0f, _buttonHeldSeconds[id] - (float)deltaSeconds);
    }

    // What the other modules on this one's channel sent last tick: the first truthy value, else the first at all, else nothing.
    private string ReadWirelessChannel(Component receiver)
    {
        string? first = null;
        foreach (var sender in _components.Where(c => c.Kind == ComponentKind.Wireless && c.Id != receiver.Id && c.Value == receiver.Value))
        {
            var sent = _wirelessSent.GetValueOrDefault(sender.Id);
            if (sent is null)
                continue;
            if (SignalValues.IsTruthy(sent))
                return sent;
            first ??= sent;
        }
        return first ?? SignalValues.False;
    }

    // Patterns the players typed, compiled once. A pattern that is not valid, or that takes too long, simply does not match.
    private readonly Dictionary<string, System.Text.RegularExpressions.Regex?> _regexCache = new();
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(20);

    private bool RegexMatches(string? pattern, string? input)
    {
        if (string.IsNullOrEmpty(pattern) || input is null)
            return false;
        if (!_regexCache.TryGetValue(pattern, out var regex))
        {
            try { regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.None, RegexTimeout); }
            catch (ArgumentException) { regex = null; }
            if (_regexCache.Count > 64)
                _regexCache.Clear();
            _regexCache[pattern] = regex;
        }
        try { return regex?.IsMatch(input) ?? false; }
        catch (System.Text.RegularExpressions.RegexMatchTimeoutException) { return false; }
    }

    // The text typed into a regular expression (its pattern) or a terminal (its message, sent at once) - ClientCommand.ComponentText.
    public bool SetComponentText(string componentId, string? text)
    {
        var index = _components.FindIndex(c => c.Id == componentId);
        if (index < 0 || ComponentSettings.CleanText(_components[index].Kind, text) is not { } clean)
            return false;
        _components[index] = _components[index] with { Text = clean };
        if (_components[index].Kind == ComponentKind.Keyboard)
            _buttonHeldSeconds[componentId] = ButtonPulseSeconds;
        return true;
    }

    // A push button: its output is true for ButtonPulseSeconds after it is pressed.
    public void PressButton(string componentId)
    {
        if (_components.FirstOrDefault(c => c.Id == componentId) is { Kind: ComponentKind.Button })
            _buttonHeldSeconds[componentId] = ButtonPulseSeconds;
    }

    // The settings panel's click: change one setting of one installed or built-in part (ComponentSettings).
    public bool ChangeComponentSetting(string componentId, ComponentSettingAction action)
    {
        var index = _components.FindIndex(c => c.Id == componentId);
        if (index < 0)
            return false;
        var changed = ComponentSettings.Apply(_components[index], action);
        if (changed == _components[index])
            return false;
        _components[index] = changed;
        // A new delay or period starts clean, so the old setting's samples do not leak through.
        _delayLine.Remove(componentId);
        _oscillatorPhase.Remove(componentId);
        return true;
    }

    private void ForgetValuePart(string componentId)
    {
        _partOutput.Remove(componentId);
        _delayLine.Remove(componentId);
        _oscillatorPhase.Remove(componentId);
        _buttonHeldSeconds.Remove(componentId);
        _wirelessSent.Remove(componentId);
    }
}
