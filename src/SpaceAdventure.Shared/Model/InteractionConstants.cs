namespace SpaceAdventure.Shared.Model;

// Every proximity radius the server actually enforces for a physical interaction (World.Interact.cs/
// World.ClickInteract.cs/World.Welding.cs/World.Mining.cs), shared here so the client's own copies
// (used for hint text, hover highlighting, and click hit-testing - Game1.Input.cs/
// Game1.Interactables.cs) can reference the same constant instead of a second hand-typed literal
// with a "must match" comment as the only thing keeping the two in sync. One of these WAS already
// silently out of sync before this file existed (the EVA dropped-item pickup hint/hover/click used
// DeviceInteractionRadius=1.0 while TryPickupDroppedItem actually allows PickupRadius=1.5).
public static class InteractionConstants
{
    public const float DeviceInteractionRadius = 1.0f; // periscope/storage/locker/system device/etc.
    public const float WelderReachUnits = 1.7f;
    public const float PickupRadius = 1.5f; // World.Mining.cs's TryPickupDroppedItem - EVA and interior alike
}
