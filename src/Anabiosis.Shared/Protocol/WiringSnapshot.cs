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
    IReadOnlyList<ComponentMountState> ComponentMountStates);
