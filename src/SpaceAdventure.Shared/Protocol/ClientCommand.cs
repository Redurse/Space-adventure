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
// HelmThrottle/HelmTurn are the helm's flight controls ([-1,1] each, held rather than edge-triggered —
// game_design.md Phase 3, M15), sent continuously like PowerDirection; only applied server-side
// while the sender is actually manning the helm (World.ShipField.cs), and otherwise ignored rather
// than zeroing anything, so the last commanded thrust keeps being applied after standing up.
// HelmStabilizePressed is edge-triggered like InteractPressed — engages auto-stabilize, which
// kills the ship's drift and holds position until a new thrust vector is given.
// DoorToggleId is edge-triggered like BuyItemType (null = no click that frame) — clicking
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
    bool CutHeld = false,
    // Which candidate on the Recruiter's current roster to hire (World.Recruiting.cs), edge-
    // triggered like DoorToggleId - null means no click that frame. Docked-only, same gate
    // as the rest of the station NPCs.
    string? HireCandidateId = null,
    // Held, not edge-triggered, same as CutHeld: the welding tool burns for as long as the button
    // is down, aimed along LookX/LookY, and repairs whatever breached hull block the flame passes
    // through (World.Welding.cs).
    bool WeldHeld = false,
    // Physically laying a wire (World.Wiring.cs's HandlePinInteract, M20): edge-triggered like
    // WireLinkInteractId used to be, but - unlike that trusted panel click - server-checked for
    // proximity, since this is a physical in-world interaction, not a HUD panel. Not laying yet:
    // this pin becomes the anchor. Laying, same pin clicked again: cancels. Laying, a different
    // compatible pin clicked: completes the wire and consumes a WireSpool. Laying, a different
    // incompatible/occupied pin clicked: restarts the lay from that pin instead of dead-ending.
    PinRef? PinInteractId = null,
    // Edge-triggered explicit cancel for a pending lay - lets the client offer an out (e.g. a
    // right-click) without having to walk back to the anchor pin and click it again.
    bool WireLayCancelPressed = false,
    // Which installed Relay component to toggle (World.ComponentLogic.cs, M21) - edge-triggered
    // like DoorToggleId, same no-proximity-check trusted-client convention.
    string? ComponentOperateId = null,
    // Which ComponentMount was clicked (World.ComponentMounts.cs, M23) - edge-triggered like
    // DoorToggleId. What actually happens (install/uninstall/operate the installed Relay) depends
    // on what's already there and what the player is holding, resolved server-side.
    string? ComponentMountInteractId = null,
    // A drag that ended over empty space instead of a slot (World.Storage.cs's TryDropItem) - the
    // item falls to the floor at the character's own feet as a DroppedItem rather than silently
    // snapping back. Server re-validates reachability itself, same trust level as MoveItemFrom/To.
    SlotRef? DropItemFrom = null,
    // Click-to-pick-up a DroppedItem (World.Mining.cs's TryPickupDroppedItem) - works in EVA, ship
    // interior, and station interior alike; server-checked for proximity and matching room/context,
    // same trust level as PinInteractId. Additive alongside EVA's existing F-key pickup, not a
    // replacement for it.
    string? PickupDroppedItemId = null,
    // Edge-triggered like the other two quest actions above - drops the active quest without
    // turning it in, costing standing with whoever issued it (World.Quests.cs, World.Factions.cs).
    // Unlike accept/turn-in, needs no docked gate: giving up is something you can decide mid-flight.
    // Appended at the end rather than next to AcceptCargoQuestPressed/TurnInCargoQuestPressed so it
    // doesn't shift every positional argument after it at GameClient.cs's construction call site.
    bool AbandonQuestPressed = false);
