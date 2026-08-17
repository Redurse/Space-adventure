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
    bool Cutting = false,
    // Hired crew (World.Recruiting.cs) - null Role means an ordinary player. The client draws a bot
    // with its given name and role instead of another anonymous crew member.
    bool IsBot = false,
    string? BotName = null,
    CrewRole? Role = null,
    // Same story as SuitTank/CutterTank above, for the tank socketed into a held welding tool
    // (WeldingTankDefinitions). Welding is lit this tick: what the client draws (a yellow-orange
    // flame, distinct from the cutter's blue one) and what other players see.
    float? WelderTank = null,
    bool Welding = false,
    // Which pin a wire-lay is anchored at, null when not laying (World.Wiring.cs's
    // HandlePinInteract) - lets every client, not just this one, draw the trailing wire from that
    // pin to wherever this character currently stands.
    PinRef? LayingWireFromPin = null);
