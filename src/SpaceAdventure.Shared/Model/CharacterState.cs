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
    InventoryState? Inventory = null,
    bool IsBleeding = false,
    bool IsAtHelm = false,
    bool IsOutside = false,
    float JetpackFuel = 100f,
    bool IsEvaAttached = false,
    bool OnStation = false,
    bool OnEnemyShip = false,
    // Oxygen left in the tank socketed into the worn suit and into the held cutter, null for "no
    // tank in it". Both are read straight off the inventory, but they're the two the HUD has to
    // show constantly - a suit running dry in vacuum is the thing you must not have to go looking
    // for (OxygenTankDefinitions).
    float? SuitTank = null,
    float? CutterTank = null,
    // The cutting flame is lit this tick: what the client draws, and what other players see.
    bool Cutting = false);
