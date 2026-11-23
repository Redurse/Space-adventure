namespace Anabiosis.Shared.Model;

// One power wire drawn in the editor, between two devices' pins. The ends are the devices' centres in the editor's own coordinates -
// the same numbers Ship.FromCustomDefinition places the devices at, so the server finds the component by where it stands and by
// the pin existing on it (no ids to keep in step). A "out-*" pin on a distribution block means "any of its system outputs that has room".
public sealed record CustomWireDef(float FromX, float FromY, string FromPin, float ToX, float ToY, string ToPin);

// W10 of wiring-plan.md - the editor's wiring mode. The player joins two devices with one click each; which pin of each takes the wire is
// worked out here from what the two are (a reactor's output into a distribution block's input, a junction box's output into a machine's
// input ...), so nothing needs a pin chooser. Power only: logic parts are installed in the game, in their mounts.
public static class EditorWiring
{
    public const string AnyDistributionOutput = "out-*";

    // The devices that have power pins at all.
    public static bool IsWireable(CustomDeviceKind kind) =>
        kind is CustomDeviceKind.Reactor or CustomDeviceKind.Battery or CustomDeviceKind.Distribution or CustomDeviceKind.Junction
            or CustomDeviceKind.Engine or CustomDeviceKind.Shields or CustomDeviceKind.WeaponCharger or CustomDeviceKind.Oxygen
            or CustomDeviceKind.Secondary or CustomDeviceKind.Camera;

    private static bool IsConsumer(CustomDeviceKind kind) =>
        IsWireable(kind) && kind is not (CustomDeviceKind.Reactor or CustomDeviceKind.Battery or CustomDeviceKind.Distribution or CustomDeviceKind.Junction);

    // The distribution output a consumer of this kind hangs off (the pin the standard wiring uses: WireGraphFactory's "out-<system>").
    private static string DistributionOutputFor(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Engine => "out-engine",
        CustomDeviceKind.Shields => "out-shields",
        CustomDeviceKind.WeaponCharger => "out-weaponcharger",
        CustomDeviceKind.Oxygen => "out-oxygen",
        _ => "out-secondary",
    };

    // The wires one click-pair makes: (from kind's pin, to kind's pin) in the order of the arguments' "who feeds whom", or none when the two cannot be joined.
    // Returned pairs are (pin on a, pin on b) when the flow goes a -> b, with `reversed` telling the caller to swap the ends.
    public static IReadOnlyList<(string FromPin, string ToPin, bool Reversed)> Link(CustomDeviceKind a, CustomDeviceKind b)
    {
        if (!IsWireable(a) || !IsWireable(b))
            return Array.Empty<(string, string, bool)>();

        // Order by who feeds whom: reactor, battery, distribution, junction, then machines.
        int Rank(CustomDeviceKind k) => k switch
        {
            CustomDeviceKind.Reactor => 0,
            CustomDeviceKind.Battery => 1,
            CustomDeviceKind.Distribution => 2,
            CustomDeviceKind.Junction => 3,
            _ => 4,
        };

        var reversed = Rank(b) < Rank(a);
        var (source, sink) = reversed ? (b, a) : (a, b);
        if (source == sink && source != CustomDeviceKind.Junction)
            return Array.Empty<(string, string, bool)>(); // two reactors, two machines: nothing to join
        if (IsConsumer(source))
            return Array.Empty<(string, string, bool)>();

        (string, string, bool) Wire(string from, string to) => (from, to, reversed);

        return source switch
        {
            CustomDeviceKind.Reactor => new[] { Wire("out", "in") },
            // The battery feeds the grid and is charged from it: both wires, like the standard wiring's feed-battery and charge-battery.
            CustomDeviceKind.Battery when sink == CustomDeviceKind.Distribution => new[] { Wire("out", "in"), (("out-battery", "in", !reversed)) },
            CustomDeviceKind.Battery => new[] { Wire("out", "in") },
            CustomDeviceKind.Distribution when sink == CustomDeviceKind.Junction => new[] { Wire(AnyDistributionOutput, "in") },
            CustomDeviceKind.Distribution when IsConsumer(sink) => new[] { Wire(DistributionOutputFor(sink), "in") },
            CustomDeviceKind.Junction => new[] { Wire("out-0", "in") },
            _ => Array.Empty<(string, string, bool)>(),
        };
    }

    // The ends of the wires for a click-pair, positioned at the two devices' centres. `a` and `b` are the devices as clicked.
    public static IReadOnlyList<CustomWireDef> Wires(CustomDeviceKind a, float ax, float ay, CustomDeviceKind b, float bx, float by)
    {
        var wires = new List<CustomWireDef>();
        foreach (var (fromPin, toPin, reversed) in Link(a, b))
        {
            // `reversed` means the flow goes from b to a.
            wires.Add(reversed
                ? new CustomWireDef(bx, by, fromPin, ax, ay, toPin)
                : new CustomWireDef(ax, ay, fromPin, bx, by, toPin));
        }
        return wires;
    }
}
