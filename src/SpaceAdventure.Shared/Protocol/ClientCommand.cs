using SpaceAdventure.Shared.Model;

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
// BuyItemType/SellSlotIndex are edge-triggered like the rest (null/-1 = no click that frame) —
// trading with the station's Trader NPC (game_design.md sections 6, 10 — M10 economy). Both are
// only honored server-side while actually docked (VoyagePhase.Station).
// AcceptCargoQuestPressed/TurnInCargoQuestPressed are edge-triggered bools for the Administrator
// NPC's delivery quest (game_design.md section 7, M11 scope) — same docked-only gate as trading.
// PurchaseUpgradeTrack is edge-triggered like BuyItemType (null = no click that frame) — buying
// the next level of a ship upgrade from the station's Mechanic (game_design.md section 9, M13
// scope), same docked-only gate.
// WireLinkInteractId is edge-triggered like BuyItemType (null = no click that frame) — clicking a
// wire on the wiring panel schematic (game_design.md section 1, M14). The server picks the action
// from whatever's currently held: a wrench/screwdriver repairs whichever half (primary or backup)
// is damaged, a WireSpool lays a backup if the link doesn't already have one. No proximity check —
// same reasoning as the MedKit (it's a panel interaction, not a physical station).
// HelmThrottle/HelmTurn are the helm's flight controls ([-1,1] each, held rather than edge-triggered —
// game_design.md Phase 3, M15), sent continuously like PowerDirection; only applied server-side
// while the sender is actually manning the helm (World.ShipField.cs), and otherwise ignored rather
// than zeroing anything, so the last commanded thrust keeps being applied after standing up.
// HelmStabilizePressed is edge-triggered like InteractPressed — engages auto-stabilize, which
// kills the ship's drift and holds position until a new thrust vector is given.
// DoorToggleId is edge-triggered like WireLinkInteractId (null = no click that frame) — clicking
// a door (interior Door or an AirlockOuterDoor to vacuum) flips it open/closed (game_design.md
// Phase 3, M16). No proximity check server-side, same trusted-client reasoning as the other
// click-driven fields above.
// PushOffPressed is edge-triggered (Space, like FirePressed) — while EVA and attached, pushes off
// toward PushOffDirectionX/Y (a client-computed, already-normalized aim vector toward the mouse
// cursor - game_design.md Phase 3, M17), becoming free-floating with that as the initial velocity.
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
    string? TravelToPointId = null,
    ItemType? BuyItemType = null,
    int SellSlotIndex = -1,
    bool AcceptCargoQuestPressed = false,
    bool TurnInCargoQuestPressed = false,
    ShipUpgradeTrack? PurchaseUpgradeTrack = null,
    string? WireLinkInteractId = null,
    // The helm flies the ship the way you'd expect to fly one: HelmThrottle is the engines along
    // the nose (positive ahead, negative astern), HelmTurn swings the bow (-1 left, +1 right).
    // Heading is something the pilot holds, not something derived from where the ship happens to be
    // drifting - which is what a joystick that set a world-space vector made it.
    float HelmThrottle = 0,
    float HelmTurn = 0,
    bool HelmStabilizePressed = false,
    string? DoorToggleId = null,
    bool PushOffPressed = false,
    float PushOffDirectionX = 0,
    float PushOffDirectionY = 0,
    ShipKind? PurchaseShipKind = null,
    // Which job to take off the Administrator's board (game_design.md section 7). Only read when
    // AcceptCargoQuestPressed is set; null there means "any available kind".
    QuestKind? AcceptQuestKind = null,
    // Edge-triggered, from the helm's "Стыковка" button - only honored while the ship is actually
    // parked alongside the station's port (World.StationDocking.cs's CanDockNow).
    bool DockPressed = false,
    // Where the head is turned, from the cursor - the suit lamp points along it. Separate from
    // MoveX/MoveY because looking one way while walking another is the whole point; zero means the
    // player isn't aiming and the body's own heading stands in.
    float LookX = 0,
    float LookY = 0,
    // Edge-triggered, set together on the one frame a drag between two item slots was released
    // (World.Storage.cs). Either end may be a carried slot or one on the ship's storage rack;
    // the rack end additionally requires standing at the rack.
    SlotRef? MoveItemFrom = null,
    SlotRef? MoveItemTo = null,
    // Oxygen tanks (World.OxygenTanks.cs). Attach drags a tank out of the row into the socket of
    // whatever is in the target slot - a cutter, or Inventory.WornSuitSlot for the suit being worn.
    // Detach pops it back into the row.
    int? AttachTankFromSlot = null,
    int? AttachTankToSlot = null,
    int? DetachTankSlot = null,
    // Held, not edge-triggered: the cutter burns for as long as the button is down, aimed along
    // LookX/LookY. Cutting is a continuous action against a block of ore, not a click on a marker.
    bool CutHeld = false);
