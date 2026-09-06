namespace Anabiosis.Shared.Protocol;

// One entry per Signal-bearing component (gates/timer/memory/relay/sensors/actuators, from M21 on) -
// what lets the client tint a wire or a relay button by its live boolean. Power-only components
// (Distribution/Junction/Device) don't get one; their state is already covered by ShipSystemState.
public sealed record ComponentState(string ComponentId, bool SignalValue);
