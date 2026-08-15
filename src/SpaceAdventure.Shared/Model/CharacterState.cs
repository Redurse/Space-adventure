using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Shared.Model;

public sealed record CharacterState(
    int PlayerId,
    float X,
    float Y,
    bool CarryingAmmoCrate = false,
    float Health = 100f,
    bool WearingSuit = false,
    float SuitActionRemaining = 0f,
    float FacingX = -1f,
    float FacingY = 0f,
    InventoryState? Inventory = null);
