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
    // same trust level as PinInteractId. Additive alongside EVA's existing E-key pickup, not a
    // replacement for it.
    string? PickupDroppedItemId = null,
    // Edge-triggered like the other two quest actions above - drops the active quest without
    // turning it in, costing standing with whoever issued it (World.Quests.cs, World.Factions.cs).
    // Unlike accept/turn-in, needs no docked gate: giving up is something you can decide mid-flight.
    // Appended at the end rather than next to AcceptCargoQuestPressed/TurnInCargoQuestPressed so it
    // doesn't shift every positional argument after it at GameClient.cs's construction call site.
    bool AbandonQuestPressed = false,
    // Which system to warp to, the one frame the button was clicked (World.StarSystems.cs) - null
    // means no click that frame, same convention as TravelToPointId. Only honored while CanWarpNow.
    string? WarpToSystemId = null,
    // Sent every tick once known (not edge-triggered) - simplest way to keep the server's copy
    // current without a separate one-shot "set nickname" handshake; null/empty is just ignored
    // rather than overwriting an already-known name with nothing.
    string? Nickname = null,
    // Which role to self-assign, the one frame the player clicks a role icon in the crew panel
    // (CrewPanel.cs) - purely a self-identification label with no gameplay restriction, unlike a
    // hired bot's own Role (a live player can still do any job regardless of what's shown here).
    // Null means no click that frame; there's no way to encode "clear" through a nullable enum
    // alone, hence the separate bool below - the same split AttachTankFromSlot/DetachTankSlot
    // already uses for "set" vs "unset" on one thing.
    CrewRole? SetOwnRoleTo = null,
    // Edge-triggered like SetOwnRoleTo's own click, just for the "no role" option in the same picker.
    bool ClearOwnRolePressed = false,
    // Дурак переводной (World.CardGame.cs, CardGamePanel): playing a card out of hand, whether
    // that's an attack, a beat, or a перевод is resolved server-side from context - the client
    // just names the exact card by rank/suit (a 36-card deck never has a duplicate, so that's
    // unambiguous) rather than a fragile hand-slot index. Null suit means no card played this
    // frame, same "null/negative = nothing happened" convention as every other edge-triggered
    // field above.
    int? PlayCardRank = null,
    CardSuit? PlayCardSuit = null,
    // The defender giving up on the current pending attack(s), taking every card on the table into
    // their hand - edge-triggered like DoorToggleId.
    bool CardGameTakePressed = false,
    // The attacker's "Бито" - only legal once every pending attack has actually been beaten,
    // discards the round and refills both hands from the deck. Edge-triggered like the above.
    bool CardGameEndRoundPressed = false,
    // Echoed back verbatim from the most recent WorldSnapshot.ServerTimestampMs this client has
    // seen (0 before the first snapshot arrives) - lets the server measure this player's round
    // trip off its own clock (CharacterState.PingMs).
    long LastServerTimestampMs = 0,
    // A LMB click while laying a wire that didn't land on a pin fixes a bend at that world spot
    // instead (World.Wiring.cs's HandleWireBend) - null means no such click this frame, same
    // convention as DoorToggleId. WireLayCancelPressed now backs out one step at a time: the last
    // fixed bend if there is one, the whole anchor otherwise (World.Wiring.cs's HandleWireLayCancel) -
    // no new field needed for that half, just a generalized meaning for the existing one.
    float? WireBendAtX = null,
    float? WireBendAtY = null,
    // The reactor's 3 physical levers (World.cs) - edge-triggered like InteractPressed, and
    // proximity-checked server-side against Ship.ReactorBlock.Position since these are a physical
    // in-world interaction rather than a trusted HUD panel click.
    bool ToggleLightsPressed = false,
    bool ToggleReactorEmergencyPressed = false,
    bool ToggleDoorsLockedPressed = false,
    // Held, not edge-triggered, same shape as CutHeld/WeldHeld - the axe swings at whatever
    // choppable door is in reach every tick this is true, but World.Doors.cs's own swing cooldown
    // (not this flag) is what actually paces it into two discrete hits rather than one instant kill.
    bool AxeSwingHeld = false,
    // Which SystemDevice was left-clicked while holding ItemType.GoshaScrewdriver (Game1.Input.cs's
    // own click branch for it, ahead of the regular screwdriver's open-the-panel behavior) -
    // edge-triggered like DoorToggleId/ComponentMountInteractId, null meaning no such click this
    // frame. Same trusted-client, no-proximity-check convention as those two: the client already
    // gated the click on NearEnough before ever setting this.
    string? SabotageDeviceId = null,
    // Edge-triggered like HelmStabilizePressed - flips the helm between ShipControlMode.Arc and
    // .Rcs (World.ShipField.cs, M41), the Z key at the client. Only meaningful while actually
    // manning the helm, same as HelmThrottle/HelmTurn/HelmStabilizePressed.
    bool ToggleControlModePressed = false,
    // The navigation console's scanner (World.Scanner.cs, M44): a world-space bearing in degrees,
    // sent continuously like HelmThrottle - only actually applied server-side while this player is
    // standing at NavigationConsole (same InteractionRadius gate the reactor's own levers use), so
    // it's simply ignored the rest of the time rather than needing its own separate "am I at the
    // console" flag.
    float ScannerSweepDegrees = 0f,
    // Promotes one of this player's own private scanner contacts onto the shared map, the one
    // frame it's clicked - null means no such click this frame, same convention as DoorToggleId.
    // Same console-proximity gate as ScannerSweepDegrees above.
    float? PlaceScannerMarkerAtX = null,
    float? PlaceScannerMarkerAtY = null,
    // Fires a single sonar-style pulse along the current ScannerSweepDegrees bearing (M47 follow-up -
    // "не постоянным сканером а с перезарядкой"): edge-triggered like DoorToggleId, only actually
    // does anything server-side while at the console AND ScannerCooldownRemaining is 0
    // (World.Scanner.cs). Aiming the dial itself stays free and continuous - only the detecting
    // pulse costs the cooldown.
    bool ScannerPingPressed = false,
    // Which of the console's own toggle-switch positions is currently selected (M48 follow-up -
    // "переключением рычажка... либо лучевой либо круговой"), sent continuously like
    // ScannerSweepDegrees rather than edge-triggered - the switch just sits wherever it was last
    // left, the same "holds its last value" treatment every other continuous scanner field gets.
    ScannerMode RequestedScannerMode = ScannerMode.Directional,
    // The jukebox's on/off checkbox and its two steppers (JukeboxPanel) - edge-triggered like the
    // reactor's levers, and proximity-checked server-side against Ship.Jukebox.Position the same
    // way, since these are a physical device rather than a trusted HUD panel click.
    bool JukeboxTogglePressed = false,
    bool JukeboxNextTrackPressed = false,
    bool JukeboxPrevTrackPressed = false,
    bool JukeboxVolumeUpPressed = false,
    bool JukeboxVolumeDownPressed = false,
    // Held, not edge-triggered like FirePressed above - added for the 3-weapon turret rework
    // (World.Combat.cs's TryFire) so a manned turret fires every tick the trigger is down, its own
    // CooldownRemaining pacing the actual shots. FirePressed itself is untouched and still drives
    // the personal-weapon shot while on foot (World.cs), which stays one shot per press.
    bool FireHeld = false,
    // The dev cheat panel's own button (Ё key, Game1.cs's CheatPanel) - edge-triggered like
    // DoorToggleId, spawns one more raider at normal firing distance for testing hit resolution
    // live instead of waiting through a whole encounter's approach (World.EnemyFleet.cs's
    // DebugSpawnEnemyNearby). No proximity/role gate - it's a testing tool, not a game action.
    bool DebugSpawnEnemyPressed = false,
    // M55 - the helm's "Посадка"/"Взлёт" toggle (World.PlanetLanding.cs's HandleLandingButtonPressed),
    // same edge-triggered shape as DockPressed. Appended at the very end - GameClient.cs's
    // SendInput/Game1.cs's call site pass every field positionally, so a new parameter has to go
    // last, never inserted in the middle.
    bool ToggleLandingPressed = false,
    // M57 - "режим ускорения времени": null means "no change requested this tick" (the level is
    // sticky server-side, not something re-sent every frame the way HelmThrottle is), a value means
    // "set the level to exactly this" - only 1/10/100/1000 are meaningful, World.cs's handler
    // ignores anything else. Only takes effect while manning the helm - this is a captain-tab
    // button, not a free-standing menu. Appended at the very end for the same
    // never-insert-in-the-middle reason every other field's own comment here already explains.
    int? RequestedTimeAccelerationLevel = null,
    // M57 - the Engineer tab's own device list: which device (if any) this character is currently
    // remotely focused on repairing. Unlike every edge-triggered "...Pressed" field above, this is
    // resent every tick with the client's own current selection (Game1.cs's _engineerFocusDeviceId,
    // never reset to null after sending) - null is a real "not focused on anything" state, the same
    // "zero/null overwrites, it's not 'no input'" convention HelmThrottle already established.
    string? EngineerFocusDeviceId = null,
    // M57 - the captain tab's "Флип" button: a one-press 180° turn for a flip-and-burn maneuver
    // (World.ShipField.cs's FlipHeading), edge-triggered same shape as ToggleControlModePressed
    // rather than a held/continuous input.
    bool FlipHeadingPressed = false,
    // M60 - "строить отсеки по ходу игры": which RoomCatalog entry to append, and where (world
    // units, same frame as every other placed device). Edge-triggered like PurchaseShipKind, same
    // docked-at-a-Shipwright gate (World.ShipBuilding.cs's TryBuildRoom). Appended at the very end
    // for the same never-insert-in-the-middle reason every other field's own comment here explains.
    BuildRoomRequest? BuildRoom = null,
    // M61 - the symmetric "снести отсек" action (World.ShipBuilding.cs's TryDemolishRoom):
    // edge-triggered like BuildRoom, null means no click that frame.
    string? DemolishRoomId = null,
    // Dev cheat panel's second button (Ё key, Game1.cs's CheatPanel) - same edge-triggered, no-gate
    // shape as DebugSpawnEnemyPressed above, just handing out credits (World.Trade.cs's own
    // DebugAddCredits, already used by the test suite) instead of spawning a raider. Appended at the
    // very end for the same never-insert-in-the-middle reason every other field's own comment here
    // explains.
    bool DebugAddCreditsPressed = false);
