namespace Anabiosis.Shared.Model;

// W4 of wiring-plan.md - the player ship's wiring as a save carries it. Loading hands it back to
// World, which reconciles it against the loaded hull (World.Wiring.cs's InitializeWiring): what
// still fits is kept as laid, anything the hull gained since gets the standard wiring.
public sealed record ShipWiringSave(
    IReadOnlyList<Component> Components,
    IReadOnlyList<Wire> Wires,
    IReadOnlyList<string> DamagedWireIds);
