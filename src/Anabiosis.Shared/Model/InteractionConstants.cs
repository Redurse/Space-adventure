namespace Anabiosis.Shared.Model;

// Every proximity radius the server actually enforces for a physical interaction (World.Interact.cs/
// World.ClickInteract.cs/World.Welding.cs/World.Mining.cs), shared here so the client's own copies
// (used for hint text, hover highlighting, and click hit-testing - Game1.Input.cs/
// Game1.Interactables.cs) can reference the same constant instead of a second hand-typed literal
// with a "must match" comment as the only thing keeping the two in sync. One of these WAS already
// silently out of sync before this file existed (the EVA dropped-item pickup hint/hover/click used
// DeviceInteractionRadius=1.0 while TryPickupDroppedItem actually allows PickupRadius=1.5).
public static class InteractionConstants
{
    // periscope/storage/locker/system device/etc. Was 1.0; devices are solid now (ShipDeviceOccupancy), and standing
    // flush against a 2x2 console or a turret leaves its centre 1.35 away - 1.5 reaches anything from its edge.
    public const float DeviceInteractionRadius = 1.5f;
    public const float WelderReachUnits = 1.7f;
    public const float PickupRadius = 1.5f; // World.Mining.cs's TryPickupDroppedItem - EVA and interior alike

    // The reactor is a solid 4x4-unit machine now (ShipDeviceOccupancy) - nobody gets closer than its
    // edge, so it is worked from there: the ordinary radius plus half its footprint, from the centre.
    // One rule for the server (rods, levers, start button, wiring) and the client's click/hover alike.
    public static float ReactorReach(float sizeScale) => DeviceInteractionRadius + 2f * sizeScale;
}
