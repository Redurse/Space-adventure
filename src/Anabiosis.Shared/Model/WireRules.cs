using Anabiosis.Shared.Protocol;

namespace Anabiosis.Shared.Model;

// W1 of wiring-plan.md (Barotrauma-style wiring, direct user request): Barotrauma's own rule - any
// connection pin, power or signal, input or output, takes up to five wires, and a wire can be any
// length. Replaces the old per-kind caps (2 into a power input, 1 into a signal input, outputs
// unlimited - see Wire.cs's own comment) once the game moves onto PowerNetwork (W2).
public static class WireRules
{
    public const int MaxWiresPerPin = 5;

    public static int WiresOn(IEnumerable<Wire> wires, PinRef pin) =>
        wires.Count(w => w.FromPin == pin || w.ToPin == pin);

    public static bool CanAddWireTo(IEnumerable<Wire> wires, PinRef pin) => WiresOn(wires, pin) < MaxWiresPerPin;

    // Both ends must have room, and a wire never loops back onto the very pin it starts from.
    public static bool CanConnect(IReadOnlyCollection<Wire> wires, PinRef from, PinRef to) =>
        from != to && CanAddWireTo(wires, from) && CanAddWireTo(wires, to);
}
