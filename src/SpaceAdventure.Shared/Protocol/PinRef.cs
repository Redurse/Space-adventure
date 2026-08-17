namespace SpaceAdventure.Shared.Protocol;

// Names one pin on one component: a string PinId (not an index) since Distribution/Junction have a
// hull-dependent number of pins with meaningful names ("out-oxygen", "out-1", ...). Same "one
// generic ref type spans everything addressable" shape as SlotRef.
public readonly record struct PinRef(string ComponentId, string PinId);
