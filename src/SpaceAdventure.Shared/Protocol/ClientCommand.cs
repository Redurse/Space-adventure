namespace SpaceAdventure.Shared.Protocol;

// MoveX/MoveY is the desired movement direction (not necessarily normalized), in [-1, 1] per axis.
// PowerSystemIndex selects a PowerSystemId (by enum order, -1 = none) for the distribution
// block; PowerDirection is -1/0/1 (decrease/hold/increase), mirroring the movement input model.
// InteractPressed/FirePressed are edge-triggered (true only on the frame the key goes down);
// TurretAimDirection is -1/0/1, applied continuously like PowerDirection while manning a turret.
// ToggleHoldSlotIndex is edge-triggered like InteractPressed (-1 = no click that frame): the
// main inventory slot index whose hold strip was clicked (game_design.md section 13 —
// Barotrauma-style hand equip). ToggleReactorSlotIndex is the same pattern for the reactor's
// 4 fuel-rod slots (-1 = no click), only meaningful while standing at the reactor.
// TravelToPointId is set the one frame a galaxy map point was clicked (null otherwise) — only
// takes effect while docked or idling in open space, not mid-battle.
public sealed record ClientCommand(
    int PlayerId,
    float MoveX = 0,
    float MoveY = 0,
    int PowerSystemIndex = -1,
    float PowerDirection = 0,
    bool InteractPressed = false,
    float TurretAimDirection = 0,
    bool FirePressed = false,
    int ToggleHoldSlotIndex = -1,
    int ToggleReactorSlotIndex = -1,
    string? TravelToPointId = null);
