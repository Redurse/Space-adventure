namespace SpaceAdventure.Shared.Model;

// One named connection point on a ComponentKind (ComponentDefinitions.PinsFor) - Id is unique only
// within its owning component, addressed together with the component's own id via PinRef.
public sealed record Pin(string Id, PinKind Kind);
