using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// The Component/Pin/Wire graph (M19-M24) - split out of WorldSnapshot's own flat field list into
// its own group, the same way Station/EnemyShip/AsteroidField already are. ComponentRenderer,
// ConnectionsPanel and the ship editor are the only readers, and always want the whole graph
// together (a Wire's endpoints reference Components, a Component's pins reference ComponentMounts
// for a purchased part), never one list of it in isolation.
public sealed record WiringSnapshot(
    IReadOnlyList<Component> Components,
    IReadOnlyList<ComponentState> ComponentStates,
    IReadOnlyList<Wire> Wires,
    IReadOnlyList<WireState> WireStates,
    IReadOnlyList<ComponentMount> ComponentMounts,
    IReadOnlyList<ComponentMountState> ComponentMountStates,
    // W6 of wiring-plan.md - the value currently on every signal output pin that carries one, and
    // which wall lamps a signal has switched off. Appended, optional, same as every additive field.
    IReadOnlyList<PinSignalState>? PinSignals = null,
    IReadOnlyList<string>? LampsOff = null,
    // W9 - junction boxes carrying more than they can (World.WiringHazards.cs), about to burn out: the client sparks them.
    IReadOnlyList<string>? OverheatingJunctions = null);

public sealed record PinSignalState(PinRef Pin, string Value);
