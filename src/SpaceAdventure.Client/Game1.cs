using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Audio;
using SpaceAdventure.Client.Networking;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Server; // SaveStore only - the client owns the save slot's lifetime (new game vs. continue)
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// The menu that runs before a session exists - ship choice, hosting, joining - lives in
// Game1.Menu.cs; everything here assumes a live _client.
public partial class Game1 : Game
{
    private const float TurretInteractionRadius = InteractionConstants.DeviceInteractionRadius;
    private const float WelderHintReachUnits = InteractionConstants.WelderReachUnits;
    private const float PickupHintRadius = InteractionConstants.PickupRadius; // World.Mining.cs's real TryPickupDroppedItem radius - was drifted to TurretInteractionRadius before

    // Shared by the ship interior and the space outside it - both are now drawn through the same
    // camera (game_design.md: "one continuous space, no hidden transition"), so there's only one
    // viewport left to define instead of a separate fixed ShipOrigin plus a separate following
    // FieldViewportOrigin/Size.
    private static readonly Vector2 WorldViewportOrigin = new(40, 40);
    private static readonly Vector2 WorldViewportSize = new(1120, 300);
    // Panels open in the middle of the screen. They used to be pinned to the bottom left, which
    // put them under the player's own hands on a wide display and meant every terminal appeared
    // somewhere the eye was not. Both the drawing and the click handling read these, so the
    // housing and its hit boxes can never disagree.
    private static Point DesignScreen => new(DesignWidth, DesignHeight);

    // Keyed by which block is open so each terminal keeps its own dragged position (Game1.PanelDrag.cs).
    private string CurrentPanelKey => _openBlock.Kind.ToString();
    private Point CurrentPanelSize => _openBlock.Kind switch
    {
        BlockKind.Rack => RackPanel.PanelSize,
        // The wiring panel is wider than the standard box and sizes its own height from the pin
        // count, which is not known out here - the standard height is used as its grab/hit box, so
        // an unusually tall one has a dead strip along its bottom.
        BlockKind.Connections => new Point(ConnectionsPanel.Width, DevicePanelChrome.Standard.Y),
        _ => DevicePanelChrome.Standard,
    };
    private Vector2 PowerPanelOrigin => PanelOrigin(CurrentPanelKey, DevicePanelChrome.Standard);
    private Vector2 RackPanelOrigin => PanelOrigin(CurrentPanelKey, RackPanel.PanelSize);
    private static readonly Vector2 VoyagePanelOrigin = new(250, 12);
    // The carried row is centred on the bottom edge and the equipment slots are pinned to the
    // bottom-right corner, so both are derived from the design resolution rather than fixed at
    // coordinates that would have to be re-tuned every time a slot changes size.
    private const int HudEdgeMargin = 12;
    private const int HudBottomMargin = 14;
    // How far past the design area's bottom edge this window's letterbox band reaches, in design
    // pixels. The inventory HUD is deliberately allowed down into it: on a 16:9 screen the design
    // area ends well above the physical bottom, and a row that stops there reads as floating in
    // mid-screen rather than sitting on the edge. Zero when the window letterboxes sideways
    // instead, where the design bottom already is the screen bottom.
    private float LetterboxBelowDesign => _renderZoom > 0f ? _renderOffset.Y / _renderZoom : 0f;
    private float HudBottom => DesignHeight + LetterboxBelowDesign - HudBottomMargin;
    // The slot itself sits flush on the bottom edge now - the hold strip moved above it
    // (InventoryPanel.GetHoldStripRect), so what used to anchor the *whole* block's top has to
    // anchor just the slot's own top instead, or the strip would float off past the top with
    // nothing reserving its space and the slot would land RowHeight too high.
    private Vector2 InventoryRowOrigin(int slotCount) => new(
        (DesignWidth - InventoryPanel.RowWidth(slotCount)) / 2f,
        HudBottom - InventoryPanel.SlotSize);
    // The equip row and the role/portrait box past its right edge are one unit, right-aligned to
    // the screen edge together - Barotrauma's own bottom-right corner has the role portrait as
    // just the last icon in the same row, not a separately-positioned element.
    private const int RoleBoxSize = InventoryPanel.SlotSize;
    private Vector2 EquipSlotsOrigin => new(
        DesignWidth - InventoryPanel.EquipRowWidth - InventoryPanel.SlotSpacing - RoleBoxSize - HudEdgeMargin,
        HudBottom - InventoryPanel.SlotSize);
    private Vector2 RoleBoxOrigin => new(
        EquipSlotsOrigin.X + InventoryPanel.EquipRowWidth + InventoryPanel.SlotSpacing,
        EquipSlotsOrigin.Y);
    // Was centered in the middle of the HUD strip below the world viewport; moved into the
    // bottom-left corner instead, out of the way of the world view and clear of the centered
    // inventory row below it (different X ranges, so the two never overlap). 190 comfortably fits
    // the panel's own tallest case: enemy line + both bars + health (+ incapacitated line) + two
    // turret ammo/charge lines + a hint line.
    private Vector2 CombatPanelOrigin => new(HudEdgeMargin, HudBottom - 190f);
    // Further up the left edge than CombatPanelOrigin (own ~190px) and HelmTabBarOrigin - both
    // already claim the bottom-left corner while at a turret or the helm, exactly when a chat
    // message matters most, so the log needs its own clear spot above them instead of overlapping.
    private Vector2 ChatPanelOrigin => new(HudEdgeMargin, HudBottom - 400f);
    // Centered directly above the role/portrait box specifically (Barotrauma's own corner has the
    // health bar riding right above the portrait, not spanning the whole equip row) - meant to
    // always be visible the same way the inventory row below it always is.
    private Vector2 PlayerHealthPanelOrigin => new(
        RoleBoxOrigin.X - (PlayerHealthPanel.BarWidth - RoleBoxSize) / 2f,
        RoleBoxOrigin.Y - PlayerHealthPanel.BarHeight - 6f);
    private static readonly Vector2 GalaxyMapPanelOrigin = new(60, 64);
    private static readonly Vector2 StationPanelOrigin = new(60, 64);
    // Content-каталог отсеков - the Shipwright's own bottom-of-screen build catalog (StationBuildPanel),
    // anchored along the bottom edge the same way the reference screenshot's own toolbar sits, clear
    // of HelmTabBarOrigin's own bottom-left slot (this one is centered, not left-aligned).
    private static readonly Vector2 StationBuildPanelOrigin =
        new((DesignWidth - StationBuildPanel.PanelWidth) / 2f, DesignHeight - StationBuildPanel.PanelHeight - 8);
    // Window 3 of the helm redesign (M47 follow-up) - a fixed HUD corner, unlike window 2's own
    // draggable widget, since nothing about it ever needs to get out of the way of the panel
    // underneath (it already floats above window 1, not over any of its own controls). Was sized
    // off the old ShipSchematicPanel's own width (380) before EngineerDevicePanel (340) replaced it
    // here entirely - fixed to the panel that's actually drawn at this origin now, closing a 40px
    // gap that had crept in between its right edge and the intended margin.
    private static readonly Vector2 EngineerDevicePanelOrigin = new(DesignWidth - EngineerDevicePanel.Width - 12, 12);
    // M57 - the tab switcher sits at a fixed spot regardless of tab (same "mode switch, not an
    // instrument" reasoning EngineerDevicePanelOrigin's own comment gives for staying fixed rather
    // than draggable) - bottom-left, clear of both the always-on TEMP-DIAG FPS/Sim overlay (fixed
    // at (10,10), ~130px tall) up top and the permanent bottom HUD band (inventory hotbar/equip
    // row/role box/health bar) HelmButtonsWidget's own default position already dodges the same way.
    private static readonly Vector2 HelmTabBarOrigin = new(60, DesignHeight - 70 - HelmTabBar.Size.Y);
    private static readonly Vector2 InfoPanelOrigin = new(60, 64);
    private static readonly Vector2 ShipEditorPanelOrigin = new(60, 64);
    // Centered like PauseMenuPanel below - a 2-player minigame taking over the middle of the
    // screen, not another HUD corner panel competing for space with the rest of them.
    private static readonly Vector2 CardGamePanelOrigin =
        new((DesignWidth - CardGamePanel.PanelWidth) / 2f, (DesignHeight - CardGamePanel.PanelHeight) / 2f);
    // Same centered treatment as CardGamePanel above - the table's other game (World.FrontsGame.cs).
    private static readonly Vector2 FrontsGamePanelOrigin =
        new((DesignWidth - FrontsGamePanel.PanelWidth) / 2f, (DesignHeight - FrontsGamePanel.PanelHeight) / 2f);
    // The choice step before either game starts (World.CardTable.cs) - centered the same way.
    private static readonly Vector2 CardTableChoicePanelOrigin =
        new((DesignWidth - CardTableChoicePanel.PanelWidth) / 2f, (DesignHeight - CardTableChoicePanel.PanelHeight) / 2f);
    // Centered on the design canvas rather than a fixed HUD corner - this one's a modal, not a
    // panel that shares the screen with the rest of the HUD.
    private static readonly Vector2 PauseMenuPanelOrigin =
        new((DesignWidth - PauseMenuPanel.PanelWidth) / 2f, (DesignHeight - PauseMenuPanel.PanelHeight) / 2f);
    // Top-left corner, out of the way of the HUD's own top bar and side panels - a dev tool, not
    // something that needs a prime screen position.
    private static readonly Vector2 CheatPanelOrigin = new(20, 80);
    // The 3 top-bar buttons (game_design.md's newest ask): Crew (slide-out roster), Management
    // (placeholder, does nothing yet), Info (the full InfoPanel takeover). Sized bigger than an
    // inventory slot (InventoryPanel.SlotSize=34) so the affordance reads as a different kind of
    // control, not another item slot.
    private const int TopBarButtonSize = 44;
    private const int TopBarButtonGap = 8;
    // Pulled up to the very top edge (M48 follow-up - "поставь 3 кнопки слева сверху выше к
    // границе верхнего экрана") now that the tick counter that used to claim this corner is gone.
    private static readonly Vector2 TopBarOrigin = new(10, 6);
    private static readonly Vector2 CrewPanelOrigin = new(10, 88);
    // Sight reach in world units. The suit helmet's lamp is a forward cone - wide and long enough to
    // actually work by (mining, lining up an airlock) rather than a keyhole - plus a small all-round
    // pool of spill light, so whatever is right beside you isn't invisible. Unsuited, the
    // compartment lighting carries far enough to fill a room but not the whole deck.
    private const float SuitVisionRadius = 9f;
    private const float SuitVisionHalfAngleDegrees = 55f;
    // Kept deliberately tiny: this pool is shadow-cast independently of the cone, against walls
    // within its own short reach only - if a wall causing one of the cone's own shadows sits even a
    // little further out than this radius, the pool fills right up to its own edge regardless
    // (there's genuinely nothing blocking it that close), leaving a bright gap between the character
    // and that shadow's true edge. Small enough here that the gap stays inside the character's own
    // sprite instead of reading as a break in the shadow.
    private const float SuitAmbientRadius = 0.5f;
    private const float OpenVisionRadius = 13f;

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    // The one white pixel every renderer in this project builds its shapes from; Game1 needs its
    // own only to draw the item riding the cursor mid-drag, which belongs to no panel.
    private Texture2D _pixel = null!;
    // Either a SoloSession (playing on our own ship, with or without guests aboard) or a
    // NetworkSession (a guest on someone else's) - from here down only _client matters.
    private IDisposable? _session;
    private GameClient _client = null!;
    // The menu (game_design.md section 9) gates session startup: the session/GameClient stay
    // unconstructed (the null! above is a lie until this flips) until the player picks a class or
    // joins a crew, so every other field below can go on assuming _client is always live once used.
    private bool _sessionStarted;
    // Read once at startup so the select screen can offer "continue" (game_design.md section 5).
    private SaveGame? _existingSave;
    private ShipRenderer _shipRenderer = null!;
    private PowerPanel _powerPanel = null!;
    private CombatPanel _combatPanel = null!;
    private PlayerHealthPanel _playerHealthPanel = null!;
    private VoyagePanel _voyagePanel = null!;
    private InventoryPanel _inventoryPanel = null!;
    private ReactorPanel _reactorPanel = null!;
    private JukeboxPanel _jukeboxPanel = null!;
    private BatteryPanel _batteryPanel = null!;
    private SystemDevicePanel _systemDevicePanel = null!;
    private GalaxyMapPanel _galaxyMapPanel = null!;
    private GalacticMapPanel _galacticMapPanel = null!;
    private StationPanel _stationPanel = null!;
    private StationBuildPanel _stationBuildPanel = null!;
    private CardGamePanel _cardGamePanel = null!;
    private FrontsGamePanel _frontsGamePanel = null!;
    private CardTableChoicePanel _cardTableChoicePanel = null!;
    // Windows 2 and 3 of the helm redesign (M47 follow-up) - replace the old fixed HelmPanel/
    // ShipStatusPanel pair. Window 1 itself is _galaxyMapPanel, reused as-is (see the myIsAtHelm
    // draw branch below) since its own schematic/fog-of-war rendering already was what was asked
    // for, just not yet shown anywhere but the nav console.
    private HelmButtonsWidget _helmButtonsWidget = null!;
    // M57 - the 3 windows above become 3 switchable tabs (HelmTab.cs's own doc comment) instead of
    // all drawn at once; _helmTab is purely client-local, like _openBlock, so different players at
    // helm can watch different tabs. _helmTabBar switches it, _timeAccelerationWidget is the
    // captain tab's own speed-selector (its Draw call sits alongside _helmButtonsWidget's).
    private HelmTab _helmTab = HelmTab.Captain;
    private HelmTabBar _helmTabBar = null!;
    private TimeAccelerationWidget _timeAccelerationWidget = null!;
    // M57 - the Engineer tab's own device list + which device (if any) THIS client's own character
    // is remotely focused on. Sticky, not a one-shot pending flag (unlike _pendingToggleLanding
    // etc.) - resent every tick as-is (ClientCommand.EngineerFocusDeviceId's own doc comment) so
    // standing on a different tab or clicking a different row actually changes/clears it.
    private EngineerDevicePanel _engineerDevicePanel = null!;
    private string? _engineerFocusDeviceId;
    // Window 2's own dragged position (Game1.PanelDrag.cs's UpdateHelmWidgetDrag) - not keyed
    // through _panelPositions like the block-console panels, since this widget is visible whenever
    // the player is at the helm rather than tied to _openBlock.
    // Default position clears the permanent bottom HUD band (inventory hotbar/equip row/role box/
    // health bar, Game1.cs's own HudBottom) - a helm diagnostic screenshot (M47 follow-up) caught
    // the original bottom-right default sitting right on top of it.
    private Vector2 _helmWidgetPosition = new(DesignWidth - HelmButtonsWidget.Size.X - 12, DesignHeight - HelmButtonsWidget.Size.Y - 70);
    private bool _draggingHelmWidget;
    private Vector2 _helmWidgetDragGrab;
    private ButtonState _prevHelmWidgetDragButton = ButtonState.Released;
    // The console's own toggle-switch widget (M48 follow-up), console-operator only unlike
    // HelmButtonsWidget above - same "own dragged position, own drag state" treatment.
    private ScannerModeWidget _scannerModeWidget = null!;
    private Vector2 _scannerWidgetPosition = new(DesignWidth - ScannerModeWidget.Size.X - 12, 40);
    private bool _draggingScannerWidget;
    private Vector2 _scannerWidgetDragGrab;
    private ButtonState _prevScannerWidgetDragButton = ButtonState.Released;
    // The switch itself is what the client sends as ClientCommand.RequestedScannerMode (held,
    // continuous, same treatment ScannerSweepDegrees already gets) - purely a local "which half was
    // last clicked" choice until the server echoes it back onto CharacterState.ScannerMode.
    private ScannerMode _requestedScannerMode = ScannerMode.Directional;
    // Crew chat (direct user request, "как в Баротравме") - only reaches the field while explicitly
    // focused (Window.TextInput), so W/A/D/S/X/Z keep flying the ship while typing.
    // _pendingChatMessage is a one-shot outgoing field, same
    // capture-send-clear lifecycle as _pendingBuildRoom - set on submit (Enter), threaded into the
    // next SendInput call, then cleared so it isn't resent every frame after.
    private string _chatInput = "";
    private bool _chatFocused;
    private string? _pendingChatMessage;
    // True only for the remainder of the Update call that just opened the chat box - guards against
    // the same physical Enter keystroke that opened it also being read by OnChatTextInput's own
    // '\r' handling as an immediate submit/close, regardless of whether the platform's TextInput
    // event for that key happens to arrive before or after this frame's Update.
    private bool _chatJustOpenedThisFrame;
    private ChatPanel _chatPanel = null!;
    private readonly ChatBubbleTracker _chatBubbleTracker = new();
    // Push-to-talk voice chat (direct user request, "как в Баротравме", local + radio) - one
    // capture helper for THIS client's own mic, one playback helper for every OTHER player's
    // incoming voice (Audio/VoiceCapture.cs, Audio/VoicePlayback.cs).
    private readonly VoiceCapture _voiceCapture = new();
    private readonly VoicePlayback _voicePlayback = new();
    private FieldRenderer _fieldRenderer = null!;
    private ExternalCameraPanel _externalCameraPanel = null!;
    private StationRenderer _stationRenderer = null!;
    private BoardingRenderer _boardingRenderer = null!;
    private VisibilityMask _visibility = null!;
    private RoomLighting _roomLighting = null!;
    private ScenePost _scenePost = null!;
    private Texture2D? _menuBackdrop;
    private Texture2D? _editorFloorTexture; // Ship Editor tile-painting - crude placeholder, see LoadContent's own comment
    private Texture2D? _editorWallVerticalTexture;
    private Texture2D? _editorWallHorizontalTexture;
    private Texture2D? _editorWallCornerTexture;
    private Texture2D? _editorWallEndCapTexture;
    private Texture2D? _editorWallTJunctionTexture;
    private Texture2D? _editorReactorTexture;
    private bool _roomLightingReady;
    // What ApplyGraphicsSettings last actually applied - the Settings screen (Game1.Settings.cs)
    // reads this to seed its staged edits when opened, and to know what "Отмена" should revert to.
    private GraphicsSettings _graphicsSettings;
    private RackPanel _rackPanel = null!;
    private ConnectionsPanel _connectionsPanel = null!;
    private SuitLockerPanel _suitLockerPanel = null!;
    private SystemRepairPanel _systemRepairPanel = null!;
    private PauseMenuPanel _pauseMenuPanel = null!;
    private CheatPanel _cheatPanel = null!;
    private CrewPanel _crewPanel = null!;
    private InfoPanel _infoPanel = null!;
    private ShipEditorPanel _shipEditorPanel = null!;
    // The 3 top-bar buttons' own state - independent of _openBlock (which means "which physical
    // console am I standing at"), since none of these need the player to be anywhere in
    // particular. Crew is an overlay (drawn over whatever's already on screen); Info and the ship
    // editor are full takeovers like the galaxy map, so opening one closes the others.
    private bool _crewPanelOpen;
    private bool _infoPanelOpen;
    private InfoTab _infoPanelTab = InfoTab.Team;
    private bool _shipEditorOpen;
    // The inter-system map (GalacticMapPanel) - opened by the M key from anywhere (Update's own
    // edge-triggered check), not gated behind walking to a console like _openBlock's other targets.
    private bool _galacticMapOpen;
    private string? _shipEditorSelectedComponentId;
    private SlotRef? _dragFrom;
    // Oxygen-tank sockets: clicking one plugs in the tank you're holding, or pulls the tank back
    // out. Edge-triggered like the other click-driven commands, so they're queued here and sent
    // exactly once (World.OxygenTanks.cs).
    private (int From, int To)? _pendingTankAttach;
    private int? _pendingTankDetach;
    // Live drag-drop feedback (game_design.md section 13): which slot to paint green while a drag
    // is over a spot the item can actually land on, and which slot to flash red for a moment after
    // a release that got rejected and snapped the item back to where it started.
    private SlotRef? _dragHighlightSlot;
    // The local player own movement input while outside, in the ship frame - what the RCS plume is
    // aimed with. The snapshot carries jetpack fuel but not thrust direction, so this is the only
    // place that knows it.
    private Vec2 _evaThrustLocal;
    private SlotRef? _invalidDropSlot;
    private double _invalidDropFlashUntil = double.NegativeInfinity;
    private const double InvalidDropFlashSeconds = 0.35;
    private ButtonState _prevDragButton = ButtonState.Released;
    private bool _helmStabilizeLatched;
    private SlotRef? _lastClickedSlot;
    private double _lastSlotClickSeconds = double.NegativeInfinity;
    private int _selectedPowerSystem = -1;
    private bool _prevInteractDown;
    private bool _prevFireDown;
    private ButtonState _prevLeftMouseButton = ButtonState.Released;
    // Edge detection for the wire-lay "undo one step" click (World.Wiring.cs's HandleWireLayCancel) -
    // separate from the map panels' own continuous "is it held" RMB-drag checks, which don't track
    // an edge at all and would otherwise fire this once per frame the button stays down.
    private ButtonState _prevRightMouseButton = ButtonState.Released;
    // Edge detection for the 1-9/0 inventory hotkeys (ReadInventoryHotkeySlot) - a whole state
    // rather than one bool per key, since there are 10 of them and they're read together.
    private KeyboardState _prevGameplayKeyboard;
    private ClickTarget _openBlock = ClickTarget.None;
    // Set only while the plain ship-interior camera is what's actually drawn this frame (not the
    // navigation map/helm/info panel/boarded-enemy views, which all take the viewport over
    // instead) - null otherwise. Lets the HUD-batch wall-tool Hp bar (below) reuse the same screen
    // origin the scene batch just drew the ship at, without recomputing the camera or drawing the
    // bar inside the masked scene batch itself.
    private Vector2? _shipInteriorOrigin;
    private string? _talkingToNpcId;
    // Esc's own menu (Game1.Update) - opens only once nothing else is open, edge-triggered like
    // every other single-key toggle in this project (holding it down mustn't flip it every frame).
    private bool _pauseMenuOpen;
    private bool _prevEscapeDown;
    // Dev cheat panel (Rendering/CheatPanel.cs) - Ё/OemTilde toggles it, same edge-triggered
    // single-key convention as the pause menu and the galactic map above.
    private bool _cheatPanelOpen;
    // Set by HandleMouseClick (Game1.Input.cs) when the cheat panel's button is clicked, read and
    // cleared once this same frame when building the outgoing ClientCommand - same "side-effect
    // field for an overlay click" shape as _pendingReturnToMainMenu above.
    private bool _debugSpawnEnemyClickedThisFrame;
    private bool _debugAddCreditsClickedThisFrame;
    // Set by the pause menu's "ГЛАВНОЕ МЕНЮ" click (Game1.Input.cs), read and cleared once at the
    // top of the next Update - see that check's own comment for why this can't just call
    // ReturnToMainMenu() directly from inside the click handler.
    private bool _pendingReturnToMainMenu;
    // Edge-triggered hull purchase, cleared the frame after it's sent - HandleMouseClick's return
    // tuple is already at its practical limit, so this one rides as a field instead.
    private ShipKind? _pendingShipPurchase;
    // M60 - the Shipwright's own "Построить" list, same edge-triggered field pattern as
    // _pendingShipPurchase right above.
    private BuildRoomRequest? _pendingBuildRoom;
    // Content-каталог отсеков - click-to-place UI: which catalog entry is currently being placed
    // (set by StationBuildPanel's own module row, cleared on confirm or cancel - see
    // HandleMouseClick's own placement-confirm block and Update's right-click cancel check). Not
    // edge-triggered like _pendingBuildRoom above - this one persists across frames while the
    // player is busy pointing at a spot, not sent to the server until they actually click one.
    private string? _placingRoomCatalogId;
    private RoomCategory _buildPanelCategory = RoomCategory.Structural;
    // Whole-ship overview's own free camera - right-drag pans, scroll wheel zooms, same idiom the
    // galaxy map/helm schematic already use (_mapPanOffset/_mapZoom above) rather than a new one.
    // Reset the moment the overview opens fresh (_shipOverviewWasActive's own transition check in
    // Update) so re-talking to the Shipwright always starts back at the auto-fit view instead of
    // wherever the player last scrolled off to.
    private Vector2 _shipOverviewPanOffset = Vector2.Zero;
    private float _shipOverviewZoomMultiplier = 1f;
    private Point? _shipOverviewPanLastMouse;
    private Point? _shipOverviewRightPressPos;
    private bool _shipOverviewWasActive;
    // M61 - the symmetric "снести отсек" button, same edge-triggered field pattern.
    private string? _pendingDemolishRoomId;
    private QuestKind? _pendingQuestKind; // same pattern, for the Administrator's job board
    private bool _pendingDock; // and for the helm's "Стыковка" button
    private bool _pendingToggleControlMode; // window 2's own РСУ/ВИРАЖ button, same edge as the Z key
    private bool _pendingToggleLanding; // window 2's own "Посадка"/"Взлёт" button, same edge as the L key (M55)
    private int? _pendingTimeAccelerationLevel; // captain tab's own ×1/×10/×100/×1000 buttons (M57)
    private bool _pendingFlipHeading; // captain tab's own "Флип" button (M57)
    private bool _pendingScannerPing; // the scanner console's own "Скан" button (M47 follow-up)
    private string? _pendingHireCandidateId; // and for the Recruiter's board
    private PinRef? _pendingPinInteract; // wire-laying (World.Wiring.cs), M19-M23
    private Vec2? _pendingWireBendAt; // LMB click mid-lay that missed every pin - fixes a bend there instead
    private string? _pendingComponentMountInteractId; // install/uninstall/relay-operate a mount
    private string? _pendingSabotageDeviceId; // Gosha's screwdriver's LMB-on-a-device click (World.Wiring.cs)
    private string? _pendingPickupDroppedItemId; // click-to-pick-up (World.Mining.cs), any context
    // humble-soaring-cat.md - "Полный переход на клик как в Baro": click-driven twins of the E-key
    // interactions below, same edge-triggered/consume-then-null shape as the three fields above.
    private string? _pendingSuitLockerInteractId;
    private string? _pendingTurretInteractId;
    private string? _pendingAmmoStorageInteractId;
    private string? _pendingStealCrateId;
    private string? _pendingRepairDeviceId;
    private SlotRef? _pendingDropItemFrom; // drag ended over empty space (World.Storage.cs)
    private bool _pendingAbandonQuest; // Administrator's action button when the job can't be turned in here
    private string? _pendingWarpToSystemId; // clicked a system on GalaxyMapPanel's own list (World.StarSystems.cs)
    private CrewRole? _pendingSetOwnRoleTo; // clicked a role icon on the crew panel's own row
    private PlayingCard? _pendingPlayCard; // clicked a card in CardGamePanel - own hand or a defend/перевод play
    private bool _pendingCardGameTake; // CardGamePanel's "Взять" button
    private bool _pendingCardGameEndRound; // CardGamePanel's "Бито" button
    private CardTableGameKind? _pendingCardTableChoice; // clicked a game button in CardTableChoicePanel
    private int? _pendingFrontsAllocationIndex; // clicked a +/- button in FrontsGamePanel - which front
    private int? _pendingFrontsAllocationAmount; // ...and its new absolute allocation value
    private bool _pendingFrontsResolve; // FrontsGamePanel's "Провести бой" button
    // The reactor's 3 physical levers (ShipRenderer.GetReactorLeverRect) - edge-triggered like
    // the rest of the _pending* fields above, cleared/sent once per click.
    private bool _pendingToggleLights;
    private bool _pendingToggleReactorEmergency;
    private bool _pendingToggleDoorsLocked;
    // The jukebox's checkbox and two steppers (JukeboxPanel) - edge-triggered same as the reactor
    // levers above, cleared/sent once per click.
    private bool _pendingJukeboxToggle;
    private bool _pendingJukeboxNextTrack;
    private bool _pendingJukeboxPrevTrack;
    private bool _pendingJukeboxVolumeUp;
    private bool _pendingJukeboxVolumeDown;
    // The wall terminal's single on/off toggle - one click on the physical block itself, no panel,
    // edge-triggered/cleared the same way.
    private bool _pendingTerminalToggle;
    // The galaxy map's own camera - purely a client view of server-authoritative positions, so it
    // lives here rather than in any snapshot. Zoom via scroll wheel, pan via right-drag; both only
    // read while the navigation console is actually open.
    // "Изначально спавнилась в центре солнечной системы на солнце" (M52): ComputeMapOrigin now
    // anchors the star at screen-centre by construction, so this untouched Vector2.Zero starting
    // value already IS "centred on the sun" - no separate one-time initializer needed.
    private float _mapZoom = 1f;
    // Set once by UpdateCore the first frame a snapshot actually exists (M55 follow-up - "экран
    // изначально был отдален... чтобы было видно половину солнечной системы") - system width
    // isn't known yet at construction time, so _mapZoom's own literal default above stays the
    // fallback until that first frame replaces it with a size-aware value.
    private bool _mapZoomInitialized;
    // serverTotalSeconds - gameTime.TotalGameTime.TotalSeconds, solved once the first frame a
    // snapshot exists and reused every frame after (Draw's own comment on serverTotalSeconds has
    // the full story) - null only until that first snapshot arrives.
    private double? _serverTimeOffsetSeconds;
    private Vector2 _mapPanOffset = Vector2.Zero;
    // Temporary diagnostic (M51 - "все еще лагает"): a persistent "slideshow" complaint survived
    // several rendering-side fixes (LOD, SOI draw cost, trajectory throttling) with no visible
    // improvement, which points away from render cost and toward the server's own tick throughput -
    // GameServer.Run has no catch-up logic (a slow Tick() just delays nextTickAt further out), so a
    // tick that's grown too expensive shows up as the WHOLE simulation running in slow motion, not
    // as dropped frames. Sim measures itself client-side (counting distinct snapshot.Tick values
    // seen per real second) rather than needing any server/protocol change - if it reads well under
    // 30, the bottleneck is server-side tick cost; if it's near 30 while FPS is still low, the
    // bottleneck really is client rendering after all. Remove once the actual cause is found.
    private double _diagFpsWindowStartSeconds;
    private int _diagFrameCountInWindow;
    private float _diagDisplayedFps;
    private long _diagLastSeenTick = -1;
    private double _diagTickWindowStartSeconds;
    private int _diagTicksSeenInWindow;
    private int _diagDisplayedTicksPerSecond;
    // Rates, not raw cumulative counts - GC.CollectionCount/GetTotalAllocatedBytes only ever grow,
    // so a per-second delta is what actually says whether garbage collection is the thing eating the
    // missing time between what UpdateCore/DrawCore's own Stopwatch sees and what FPS/Sim show -
    // stop-the-world Gen1/Gen2 pauses freeze every managed thread in the process at once, which is
    // exactly the kind of "cost neither timer can see because it isn't inside either of them" gap
    // that would otherwise look inexplicable.
    private long _diagLastAllocBytes;
    private int _diagLastGc0;
    private int _diagLastGc1;
    private int _diagLastGc2;
    private float _diagAllocMbPerSecond;
    private int _diagGc0PerSecond;
    private int _diagGc1PerSecond;
    private int _diagGc2PerSecond;
    // Phase breakdown of DrawCore itself, once Draw's own total (_diagLastDrawMs) turned out to be
    // the actual cost (548ms, matching FPS 2 almost exactly) - narrows down WHICH of the mask/scene/
    // post/HUD phases is actually responsible instead of guessing from source alone.
    private double _diagMaskMs, _diagSceneMs, _diagPostMs, _diagHudMs;
    // Sub-phases of Scene itself (the "else" branch only - ship interior, not docked/boarding/helm) -
    // Release cut Scene from 514ms to 183ms (Debug JIT overhead was real) but 183ms is still nowhere
    // near acceptable, so this narrows down which of the three renderers it actually is.
    private double _diagShipMs, _diagStationMs, _diagFieldMs;
    private Point? _mapPanLastMouse;
    private int _prevScrollWheelValue;
    // The console's own housing can be dragged around the HUD by its own right-drag (M48 follow-up -
    // "панельку сонара можно было перетаскивать при зажатии ПКМ") - unlike the helm's free camera
    // pan above, right-drag has nothing left to pan inside the ship-locked console screen itself, so
    // it repositions the whole instrument instead. Added to GalaxyMapPanelOrigin only at the
    // console's own (non-pilotView) draw/hit-test call sites - the helm's own window 1 and the
    // galactic map keep their fixed corner untouched.
    private Vector2 _sonarPanelDragOffset = Vector2.Zero;
    // Dragging the console's own rim handle rotates the scanner sweep (World.Scanner.cs, M44; M48
    // follow-up made this Barotrauma-style - the handle only, not a drag-anywhere) - true only once
    // a press has actually landed on the handle (GalaxyMapPanel.HitTestScannerHandle); a press that
    // instead lands on one of this player's own scanner contacts places a shared marker there and
    // never starts a drag at all (see the drag-vs-click branch in Update()).
    private bool _scannerHandleDragging;
    private ButtonState _prevMapLeftButton = ButtonState.Released;
    // The cockpit<->system-map crossfade (M45) - see its own doc comment at the Update() site that
    // maintains these. Counts down from NavTransitionDuration each time _openBlock's Navigation
    // state just flipped; the overlay in Draw() reads it to fade the screen through black.
    private const float NavTransitionDuration = 0.3f;
    private float _navTransitionRemaining;
    private bool _wasNavigationOpen;

    // External hull cameras (game_design.md, M46; rebuilt as real wired-in devices in M48) -
    // _externalCameraMode/_externalCameraFullscreenIndex are still purely client state (which grid
    // tile is taken fullscreen isn't a physical thing anyone else needs to see); the cameras
    // themselves, their power/damage state, and their view are now server-driven
    // (WorldSnapshot.Cameras/SystemStates, ExternalCameraPanel). The static view has no mouse-look
    // any more (M48 follow-up - "статичный вид"), so there's no look-offset state left to track.
    private bool _externalCameraMode;
    private int? _externalCameraFullscreenIndex;
    // The galactic map's own camera - separate from the system map's above, since the two views
    // use completely different coordinate spaces/scales and are never open at once, but a shared
    // zoom/pan would still leak confusingly from one into the other.
    private float _galacticMapZoom = 1f;
    private Vector2 _galacticMapPanOffset = Vector2.Zero;
    private Point? _galacticMapPanLastMouse;
    private readonly EffectTracker _effectTracker = new();
    private readonly AtmosphereField _atmosphere = new();
    private WorldSnapshot? _previousSnapshot;

    // Every panel origin, viewport rect and hit-test box in this class is written in these fixed
    // "design" pixels. Rather than making all of that resolution-aware, the whole scene is drawn
    // at this size and scaled up to whatever the display actually is (see _renderScale) - so
    // fullscreen changes nothing about the layout code, only how big it ends up on screen.
    private const int DesignWidth = 1200;
    private const int DesignHeight = 560;

    private Matrix _renderScale = Matrix.Identity;
    private Vector2 _renderOffset = Vector2.Zero;
    private float _renderZoom = 1f;
    private bool _prevFullscreenToggleDown;
    private bool _showTileGridOverlay;
    // F3 toggles the top-left diagnostic HUD (fps/tick/timing text) - hidden by default, edge-
    // triggered the same way F11's fullscreen toggle is above.
    private bool _prevDiagOverlayToggleDown;
    private bool _showDiagOverlay;
    private Point _designMouse; // cursor in design pixels, refreshed once per Update

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        // Direct user request ("курсор мышки как в баротравме") - the OS's own plain arrow is
        // hidden; GameCursor.Draw (Game1.cs's own DrawCore, drawn last every frame) replaces it,
        // so it can change shape over something interactive.
        IsMouseVisible = false;
        // Set explicitly. Left alone, the caption is whatever the assembly happens to be called,
        // which is the one place a working title survives a rename without anybody noticing.
        Window.Title = "Unidentified Signal";
    }

    protected override void Initialize()
    {
        // Borderless-fullscreen at the display's own resolution: HardwareModeSwitch = false keeps
        // it a desktop-sized window rather than forcing a mode change, which alt-tabs cleanly and
        // avoids asking the monitor for an odd 1200x560 mode it may not have.
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        _graphics.PreferredBackBufferWidth = display.Width;
        _graphics.PreferredBackBufferHeight = display.Height;
        _graphics.HardwareModeSwitch = false;
        _graphics.IsFullScreen = true;
        _graphics.ApplyChanges();

        UpdateRenderScale();
        Window.ClientSizeChanged += (_, _) => UpdateRenderScale();
        Window.TextInput += OnMenuTextInput; // typing the host's address on the join screen
        Window.TextInput += OnChatTextInput; // crew chat input box, Enter to open/send (direct user request)
        base.Initialize();
    }

    // Fits the design-sized scene into the real backbuffer, preserving aspect ratio and centering
    // what's left over (letterboxing), so nothing stretches out of shape on a widescreen display.
    private void UpdateRenderScale()
    {
        var viewport = GraphicsDevice.Viewport;
        _renderZoom = Math.Min((float)viewport.Width / DesignWidth, (float)viewport.Height / DesignHeight);
        _renderOffset = new Vector2(
            (viewport.Width - DesignWidth * _renderZoom) / 2f,
            (viewport.Height - DesignHeight * _renderZoom) / 2f);
        _renderScale = Matrix.CreateScale(_renderZoom) * Matrix.CreateTranslation(_renderOffset.X, _renderOffset.Y, 0f);
    }

    // Real cursor position mapped back into design pixels - every hit test in this class works in
    // that space, so the mouse has to be un-scaled once here rather than at each call site.
    private Point ToDesignSpace(Point screenPosition) => new(
        (int)((screenPosition.X - _renderOffset.X) / _renderZoom),
        (int)((screenPosition.Y - _renderOffset.Y) / _renderZoom));

    private void ToggleFullscreen()
    {
        _graphics.IsFullScreen = !_graphics.IsFullScreen;
        if (!_graphics.IsFullScreen)
        {
            _graphics.PreferredBackBufferWidth = DesignWidth;
            _graphics.PreferredBackBufferHeight = DesignHeight;
        }
        else
        {
            var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            _graphics.PreferredBackBufferWidth = display.Width;
            _graphics.PreferredBackBufferHeight = display.Height;
        }
        _graphics.ApplyChanges();
        UpdateRenderScale();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("DebugFont");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Everything below this line runs before the game loop's own Draw() ever gets called, so
        // without a frame presented here the player stares at whatever blank colour the OS gave the
        // freshly created window (white, on most Windows setups) for however long the panel/audio
        // construction below takes. Checkpoints at each real stage of this fixed sequence report
        // genuine progress (percent is how much of THIS list has actually run, not an animation).
        DrawLoadingFrame("ЗАГРУЗКА...", 0);

        _shipRenderer = new ShipRenderer(GraphicsDevice, _font,
            new Rectangle((int)WorldViewportOrigin.X, (int)WorldViewportOrigin.Y, (int)WorldViewportSize.X, (int)WorldViewportSize.Y));
        _powerPanel = new PowerPanel(GraphicsDevice, _font);
        _combatPanel = new CombatPanel(GraphicsDevice, _font);
        _chatPanel = new ChatPanel(GraphicsDevice, _font);
        _playerHealthPanel = new PlayerHealthPanel(GraphicsDevice, _font);
        _voyagePanel = new VoyagePanel(_font);
        _inventoryPanel = new InventoryPanel(GraphicsDevice, _font);
        _reactorPanel = new ReactorPanel(GraphicsDevice, _font);
        _jukeboxPanel = new JukeboxPanel(GraphicsDevice, _font);
        _batteryPanel = new BatteryPanel(GraphicsDevice, _font);
        _systemDevicePanel = new SystemDevicePanel(GraphicsDevice, _font);
        _galaxyMapPanel = new GalaxyMapPanel(GraphicsDevice, _font, new Rectangle(0, 0, DesignWidth, DesignHeight));
        _galacticMapPanel = new GalacticMapPanel(GraphicsDevice, _font);
        _stationPanel = new StationPanel(_font);
        _stationBuildPanel = new StationBuildPanel(GraphicsDevice, _font);
        _cardGamePanel = new CardGamePanel(GraphicsDevice, _font);
        _frontsGamePanel = new FrontsGamePanel(GraphicsDevice, _font);
        _cardTableChoicePanel = new CardTableChoicePanel(GraphicsDevice, _font);
        _helmButtonsWidget = new HelmButtonsWidget(GraphicsDevice, _font);
        _helmTabBar = new HelmTabBar(GraphicsDevice, _font);
        _timeAccelerationWidget = new TimeAccelerationWidget(GraphicsDevice, _font);
        _engineerDevicePanel = new EngineerDevicePanel(GraphicsDevice, _font);
        _scannerModeWidget = new ScannerModeWidget(GraphicsDevice, _font);
        DrawLoadingFrame("ЗАГРУЗКА...", 25);

        // FieldRenderer's own ctor is the single heaviest step in this whole list - it bakes every
        // enemy hull class's armour onto a render target up front (EnemyHullSkin.cs), a handful of
        // real GPU round-trips rather than the cheap plain-object constructions everywhere else
        // here - worth its own checkpoint on both sides so the percentage doesn't visibly stall.
        _fieldRenderer = new FieldRenderer(GraphicsDevice, _font);
        DrawLoadingFrame("ЗАГРУЗКА...", 55);

        _externalCameraPanel = new ExternalCameraPanel(GraphicsDevice, _font, _fieldRenderer);
        _stationRenderer = new StationRenderer(_shipRenderer, GraphicsDevice, _font);
        _boardingRenderer = new BoardingRenderer(_shipRenderer, GraphicsDevice, _font);
        _visibility = new VisibilityMask(GraphicsDevice);
        // Re-enabled (direct user request, "тени как в Barotrauma" - the per-pixel falloff shader
        // reads more like real shadow-casting than the old BasicEffect vertex-colour fan, whose
        // triangle facets could show as visible brightness banding in a light's own glow). If this
        // regresses whatever it was disabled for originally, revert to `new RoomLighting(GraphicsDevice)`.
        _roomLighting = new RoomLighting(GraphicsDevice, Shaders.TryLoad(Content, "Shaders/Light"));
        // Null when the content build hasn't produced the effect - ScenePost then reports
        // itself unavailable and Draw keeps its original straight-to-backbuffer path.
        _scenePost = new ScenePost(GraphicsDevice, Shaders.TryLoad(Content, "Shaders/Post"));
        _rackPanel = new RackPanel(GraphicsDevice, _font);
        _connectionsPanel = new ConnectionsPanel(GraphicsDevice, _font);
        _suitLockerPanel = new SuitLockerPanel(GraphicsDevice, _font);
        _systemRepairPanel = new SystemRepairPanel(GraphicsDevice, _font);
        _pauseMenuPanel = new PauseMenuPanel(GraphicsDevice, _font);
        _cheatPanel = new CheatPanel(GraphicsDevice, _font);
        _crewPanel = new CrewPanel(GraphicsDevice, _font);
        _infoPanel = new InfoPanel(GraphicsDevice, _font);
        _shipEditorPanel = new ShipEditorPanel(GraphicsDevice, _font);
        DrawLoadingFrame("ЗАГРУЗКА...", 75);

        _existingSave = SaveStore.Load();
        _sounds = new GameSounds(Content);
        _music = new GameMusic(Content);
        _jukeboxAudio = new JukeboxAudio(Content);
        DrawLoadingFrame("ЗАГРУЗКА...", 90);
        // The one raster texture asset in an otherwise fully-procedural game (ItemIcons.cs draws
        // every other icon from flat primitives) - same defensive load as everything else here, so an
        // unbuilt/missing .xnb falls back to the old procedural DrawScrewdriver instead of crashing.
        // The hand-rendered main menu backdrop. Kept at its authored 286x186 and blown up with point
        // filtering at draw time - it is pixel art, and any smoothing on the way up destroys the
        // whole reason for drawing it that way.
        // The planet is not part of the backdrop image - it is drawn live so it can turn, from an
        // equirectangular strip plus Shaders/Planet. Both load defensively: without them the menu
        // still shows the backdrop, just with a hole where the planet would be, which is a far
        // better failure than a crash on the first screen anybody sees.
        _planetSurface = null;
        try { _planetSurface = Content.Load<Texture2D>("Textures/PlanetSurface"); }
        catch { _planetSurface = null; }
        _planetEffect = Shaders.TryLoad(Content, "Shaders/Planet");
        // The drawn backdrop, with a painted scene built out behind it by MenuBackdropArt - the image
        // on its own is a ship and a star against flat dark space, and no amount of detail fixes a
        // picture with one plane in it. If the bake throws, the plain image is still what shows, so
        // reverting to it is deleting the second statement.
        try { _menuBackdrop = Content.Load<Texture2D>("Textures/MenuBackdrop"); }
        catch { _menuBackdrop = null; } // and then MenuPlanetScene draws the pane instead
        if (_menuBackdrop is not null)
        {
            try { _menuBackdrop = MenuBackdropArt.Bake(GraphicsDevice, _menuBackdrop); }
            catch { /* keep the plain image */ }
        }
        try { ItemIcons.SetScrewdriverTexture(Content.Load<Texture2D>("Textures/Screwdriver")); }
        catch { /* ItemIcons.Draw falls back to the procedural silhouette when this is null */ }
        // Content-каталог отсеков - one reference-art texture per catalog room type (the player's
        // own screenshots), keyed by the exact catalog display name. Loaded defensively per entry,
        // same reasoning as every texture above: a room whose own .xnb didn't build for whatever
        // reason falls back to the ordinary procedural room rather than taking the whole load down.
        foreach (var (catalogName, textureName) in RoomDecor.CatalogTextureNames)
        {
            try { RoomDecor.SetCatalogTexture(catalogName, Content.Load<Texture2D>($"Textures/RoomCatalog/{textureName}")); }
            catch { /* ShipRenderer.DrawRoomFloor falls back to the procedural room when this is missing */ }
        }
        // Hand-made wall panel art (M74 follow-up, humble-soaring-cat.md) - same defensive load as
        // everything else here: a missing/unbuilt .xnb leaves the textures null, and
        // ShipRenderer.DrawWallBand/DrawCornerPlate fall back to the old procedural hull plate.
        try
        {
            // Content.Load caches by asset name, so loading the same three assets again below (for
            // the Ship Editor's own tile renderer, Game1.ShipEditor.Draw.cs's DrawEditorWallTile) is
            // free - not a second disk read, just a second reference to the same Texture2D.
            _editorWallVerticalTexture = Content.Load<Texture2D>("Textures/Walls/WallVertical");
            _editorWallHorizontalTexture = Content.Load<Texture2D>("Textures/Walls/WallHorizontal");
            _editorWallCornerTexture = Content.Load<Texture2D>("Textures/Walls/WallCorner");
            _editorWallEndCapTexture = Content.Load<Texture2D>("Textures/Walls/WallEndCap");
            _editorWallTJunctionTexture = Content.Load<Texture2D>("Textures/Walls/WallTJunction");
            _shipRenderer.SetWallTextures(_editorWallVerticalTexture, _editorWallHorizontalTexture, _editorWallCornerTexture, _editorWallEndCapTexture, _editorWallTJunctionTexture);
        }
        catch { /* ShipRenderer.DrawWallBand/DrawCornerPlate fall back to the procedural hull plate */ }
        // Reactor texture - baked procedurally (ReactorTexture) instead of loaded from Content.
        // AI-generated attempts at Textures/Devices/Reactor.png never matched the game's own
        // pixel-art style, so this follows a Barotrauma reactor reference directly in code the
        // same way TileTextures/HullSkin already build the hull and floor.
        _editorReactorTexture = ReactorTexture.Create(GraphicsDevice);
        _shipRenderer.SetReactorTexture(_editorReactorTexture);
        // Marching-engine art (direct user request) - real Control/Bulkhead/Nozzle textures instead
        // of the DeviceSkin placeholder faces; same defensive load, ShipRenderer falls back to the
        // old procedural look for whichever part's .xnb is missing/unbuilt.
        try
        {
            var engineControl = Content.Load<Texture2D>("Textures/Devices/EngineControl");
            var engineBulkhead = Content.Load<Texture2D>("Textures/Devices/EngineBulkhead");
            var engineNozzle = Content.Load<Texture2D>("Textures/Devices/EngineNozzle");
            _shipRenderer.SetEngineTextures(engineControl, engineBulkhead, engineNozzle);
        }
        catch { /* ShipRenderer.DrawShipEngine/DrawEngineNozzles fall back to the procedural design */ }
        // Ship Editor tile-painting redo - a deliberately crude placeholder floor tile (the user's
        // own words: "просто text текстуру пока что, потом заменим"), just so a painted floor tile
        // reads as something rather than a flat colour rectangle. Falls back to the flat rectangle
        // Game1.ShipEditor.Draw.cs already draws if this one PNG doesn't build.
        try { _editorFloorTexture = Content.Load<Texture2D>("Textures/Tiles/FloorPlaceholder"); }
        catch { _editorFloorTexture = null; }
        // Overrides the two volume-knob/window lines above with whatever the player last saved on
        // the Settings screen (Game1.Settings.cs) - defaults (WindowMode.Borderless, VSync on,
        // full volume, no particle cap change) exactly match the behavior above, so a machine that
        // never opens Settings sees no change at all.
        ApplyGraphicsSettings(PlayerSettingsStore.LoadGraphicsSettings());
    }

    // Presents a single static frame directly, bypassing the normal Update/Draw loop entirely -
    // LoadContent hasn't finished yet at the point this is called, so that loop isn't running.
    // percent, when given, is genuine progress through LoadContent's own known, fixed sequence of
    // steps (called at a handful of checkpoints below) - not an animation standing in for it.
    private void DrawLoadingFrame(string message, int percent = -1)
    {
        var text = percent >= 0 ? $"{message} {Math.Clamp(percent, 0, 100)}%" : message;
        var viewport = GraphicsDevice.Viewport;
        var size = _font.MeasureString(text);
        var position = new Vector2((viewport.Width - size.X) / 2f, (viewport.Height - size.Y) / 2f);

        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin();
        _spriteBatch.DrawString(_font, text, position, Color.White);
        _spriteBatch.End();
        GraphicsDevice.Present();
    }

    // The background-session counterpart of DrawLoadingFrame above (StartHostedSession, M50) -
    // drawn from the NORMAL Update/Draw loop instead of bypassing it (that loop is exactly what
    // needs to keep running while GalaxyMap.CreateStarter() works on another thread), so this
    // relies on the engine's own Present at the end of the frame rather than calling one itself.
    private void DrawSessionLoadingScreen()
    {
        // GeneratedProceduralCount (Shared/Model/GalaxyMap.cs) is genuine, live progress - however
        // many procedural systems the background construction has actually finished rolling so
        // far - read off the one instance CreateStarter is building right now (GalaxyMap.Current).
        // EstimatedStartupGalaxySystems is a rough ceiling (typically 6 hand-authored systems need
        // a handful of EnsureGenerated chunks of 20 each to get their own 3 starting neighbours),
        // not an exact prediction - the percentage it drives is real work completed either way,
        // just capped short of 100% until FinishPendingSessionIfReady actually confirms done.
        const int estimatedStartupGalaxySystems = 80;
        var generated = GalaxyMap.Current?.GeneratedProceduralCount ?? 0;
        var percent = Math.Clamp(generated * 100 / estimatedStartupGalaxySystems, 0, 99);
        var text = $"ЗАГРУЗКА... {percent}%";

        var viewport = GraphicsDevice.Viewport;
        var size = _font.MeasureString(text);
        var position = new Vector2((viewport.Width - size.X) / 2f, (viewport.Height - size.Y) / 2f);

        GraphicsDevice.Clear(Color.Black);
        _spriteBatch.Begin();
        _spriteBatch.DrawString(_font, text, position, Color.White);

        const int barWidth = 320;
        const int barHeight = 8;
        var barPosition = new Vector2((viewport.Width - barWidth) / 2f, position.Y + size.Y + 16f);
        _spriteBatch.Draw(_pixel, new Rectangle((int)barPosition.X, (int)barPosition.Y, barWidth, barHeight), Color.Black * 0.6f);
        _spriteBatch.Draw(_pixel, new Rectangle((int)barPosition.X, (int)barPosition.Y, barWidth * percent / 100, barHeight), Color.LightSteelBlue);
        _spriteBatch.End();
    }

    // The single place every graphics/audio setting actually takes effect - called once at startup
    // with whatever was last saved (or the defaults, matching the hardcoded setup this replaced),
    // and again from the Settings screen's own "Применить" button with the staged values the
    // player just picked. GraphicsSettings.ResolutionWidth/Height null means "use the desktop's
    // current mode" for Fullscreen/Borderless, or this game's own design size for Windowed.
    private void ApplyGraphicsSettings(GraphicsSettings settings)
    {
        _graphicsSettings = settings;
        var display = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        switch (settings.WindowMode)
        {
            case WindowMode.Fullscreen:
                _graphics.HardwareModeSwitch = true;
                _graphics.IsFullScreen = true;
                _graphics.PreferredBackBufferWidth = settings.ResolutionWidth ?? display.Width;
                _graphics.PreferredBackBufferHeight = settings.ResolutionHeight ?? display.Height;
                break;
            case WindowMode.Windowed:
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = settings.ResolutionWidth ?? DesignWidth * 2;
                _graphics.PreferredBackBufferHeight = settings.ResolutionHeight ?? DesignHeight * 2;
                break;
            default: // Borderless - a desktop-sized window, no real mode switch (alt-tabs cleanly)
                _graphics.HardwareModeSwitch = false;
                _graphics.IsFullScreen = true;
                _graphics.PreferredBackBufferWidth = display.Width;
                _graphics.PreferredBackBufferHeight = display.Height;
                break;
        }
        _graphics.SynchronizeWithVerticalRetrace = settings.VSync;
        _graphics.ApplyChanges();
        UpdateRenderScale();

        SoundEffect.MasterVolume = Math.Clamp(settings.MasterVolume, 0f, 1f);
        _music?.SetMasterVolume(settings.MasterVolume);
        if (_scenePost is not null)
        {
            _scenePost.BloomStrength = settings.BloomStrength;
            _scenePost.WideBloomStrength = settings.BloomStrength * 0.55f;
        }
        AtmosphereField.MaxParticles = Math.Max(0, settings.MaxParticles);
    }

    // TEMP-DIAG (M51 slowdown investigation): wraps the real Update body just to time it - renamed
    // to UpdateCore rather than inlining a Stopwatch at the top, since the body below has several
    // early returns (state-machine style) that a single top-of-method timer would miss entirely.
    private double _diagLastUpdateMs;

    protected override void Update(GameTime gameTime)
    {
        var diagUpdateStopwatch = System.Diagnostics.Stopwatch.StartNew();
        UpdateCore(gameTime);
        _diagLastUpdateMs = diagUpdateStopwatch.Elapsed.TotalMilliseconds;
    }

    private void UpdateCore(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        _chatJustOpenedThisFrame = false;

        // Edge-triggered (holding Escape down must fire this once, not every frame) - what it
        // actually does differs before vs. during a session, so the two branches below split on
        // _sessionStarted rather than sharing one action.
        var escapeDown = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape);
        var escapePressed = escapeDown && !_prevEscapeDown;
        _prevEscapeDown = escapeDown;

        // Before a session exists Escape steps back one screen toward the main menu. On the main
        // menu itself it now does nothing: quitting is what the ВЫХОД button is for, and a key that
        // closes the whole game the moment it is pressed one screen too early is a trap.
        if (escapePressed && !_sessionStarted)
            LeaveSubScreen();

        // F11 toggles back to a window - edge-triggered, or holding the key would flip the mode
        // every single frame.
        var fullscreenToggleDown = keyboard.IsKeyDown(Keys.F11);
        if (fullscreenToggleDown && !_prevFullscreenToggleDown)
            ToggleFullscreen();
        _prevFullscreenToggleDown = fullscreenToggleDown;

        // F3 toggles the diagnostic HUD - edge-triggered, or holding the key would flip it every frame.
        var diagOverlayToggleDown = keyboard.IsKeyDown(Keys.F3);
        if (diagOverlayToggleDown && !_prevDiagOverlayToggleDown)
            _showDiagOverlay = !_showDiagOverlay;
        _prevDiagOverlayToggleDown = diagOverlayToggleDown;

        // Debug aid (M74 follow-up) - held, not toggled: the grid shows only while the physical key
        // under Ъ on a Russian layout (OemCloseBrackets - the same key as US "]") is actually down.
        _showTileGridOverlay = keyboard.IsKeyDown(Keys.OemCloseBrackets);

        _designMouse = ToDesignSpace(Mouse.GetState().Position);

        var jukeboxSnapshotState = _client?.LatestSnapshot?.Jukebox;
        UpdateGameMusic(gameTime.TotalGameTime.TotalSeconds, jukeboxSnapshotState?.On ?? false);
        UpdateJukeboxAudio(jukeboxSnapshotState);

        if (!_sessionStarted)
        {
            // A background SoloSession construction in flight (Game1.Menu.cs's StartHostedSession,
            // M50) - polled here every frame instead of blocking on it, so Update/Draw keep running
            // (a loading screen, Draw below) rather than the window simply freezing for however
            // long GalaxyMap.CreateStarter() takes. HandleMenu is skipped meanwhile - there is no
            // menu state to react to clicks on while a session is already being built.
            if (_pendingSession is not null)
            {
                FinishPendingSessionIfReady();
                base.Update(gameTime);
                return;
            }
            HandleMenu(keyboard, (float)gameTime.ElapsedGameTime.TotalSeconds);
            base.Update(gameTime);
            return;
        }

        // Number keys / Q-E only reach the grid while the distribution block is open — you have
        // to "walk up and click in" first (game_design.md section 1).
        var distributionOpen = _openBlock.Kind == BlockKind.Distribution;
        var selection = distributionOpen ? ReadPowerSystemSelection(keyboard) : null;
        if (selection.HasValue)
            _selectedPowerSystem = selection.Value;
        var powerDirection = distributionOpen ? ReadPowerDirection(keyboard) : 0f;
        var powerSystemIndexToSend = distributionOpen ? _selectedPowerSystem : -1;

        var isManningTurret = _client.LatestSnapshot?.TurretStates.Any(t => t.MannedByPlayerId == _client.PlayerId) ?? false;
        var myCharacter = _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        var isAtHelm = myCharacter?.IsAtHelm ?? false;
        var isOutside = myCharacter?.IsOutside ?? false;

        // During a session: Esc closes whatever's open (a block/console, a top-bar panel, the
        // turret/helm) one thing at a time, same priority a second click on a console already has;
        // only once nothing is open does it bring up the pause menu instead. Unmanning a turret or
        // leaving the helm is server-side (World.Interact.cs's F handling, top-priority there too),
        // so that case is folded into this frame's interactPressed rather than duplicated here.
        var escapeSendsInteract = false;
        if (escapePressed && _chatFocused)
        {
            // Same "handle the more specific overlay first" priority as the search box below -
            // closing the chat input is the one thing this Escape press should do, not also
            // opening the pause menu or standing the captain up from the helm.
            _chatInput = "";
            _chatFocused = false;
        }
        else if (escapePressed)
        {
            if (_pauseMenuOpen)
            {
                _pauseMenuOpen = false;
            }
            else if (_externalCameraFullscreenIndex is not null)
            {
                // One step back to the grid rather than closing outright - the same "one thing at
                // a time" priority the block below already gives every other overlay, just with an
                // extra step since fullscreen-within-the-grid is itself two levels deep.
                _externalCameraFullscreenIndex = null;
            }
            else if (_openBlock.Kind != BlockKind.None || _crewPanelOpen || _infoPanelOpen || _shipEditorOpen
                     || _galacticMapOpen || _talkingToNpcId is not null || isManningTurret || isAtHelm || _externalCameraMode)
            {
                _openBlock = ClickTarget.None;
                _crewPanelOpen = false;
                _infoPanelOpen = false;
                _shipEditorOpen = false;
                _galacticMapOpen = false;
                _talkingToNpcId = null;
                _externalCameraMode = false;
                escapeSendsInteract = isManningTurret || isAtHelm;
            }
            else
            {
                _pauseMenuOpen = true;
            }
        }

        // Enter opens the crew chat box (direct user request, "как в Баротравме") - only from
        // gameplay, and only when nothing else already has keyboard focus, so it doesn't fire while
        // the pause menu is up. Edge-triggered like every other key here; _chatJustOpenedThisFrame
        // guards the same physical keystroke's TextInput '\r' from being read as an immediate
        // submit/close by OnChatTextInput below.
        var enterDown = keyboard.IsKeyDown(Keys.Enter);
        if (enterDown && !_prevGameplayKeyboard.IsKeyDown(Keys.Enter) && !_chatFocused && !_pauseMenuOpen)
        {
            _chatFocused = true;
            _chatJustOpenedThisFrame = true;
        }

        // M opens the GALACTIC map (game_design.md - two-tier map) from anywhere, unlike the
        // system-level one (GalaxyMapPanel), which still needs walking up to the navigation
        // console - edge-triggered like F11 above, or holding the key would flip it every frame.
        var galacticMapToggleDown = keyboard.IsKeyDown(Keys.M);
        if (galacticMapToggleDown && !_prevGameplayKeyboard.IsKeyDown(Keys.M) && !_pauseMenuOpen)
        {
            _galacticMapOpen = !_galacticMapOpen;
            if (_galacticMapOpen)
            {
                _openBlock = ClickTarget.None;
                _infoPanelOpen = false;
                _shipEditorOpen = false;
            }
        }

        // Dev cheat panel (Ё/OemTilde - "ё" sits there on a Russian keyboard) - same edge-triggered
        // toggle convention as M above.
        var cheatPanelToggleDown = keyboard.IsKeyDown(Keys.OemTilde);
        if (cheatPanelToggleDown && !_prevGameplayKeyboard.IsKeyDown(Keys.OemTilde) && !_pauseMenuOpen)
            _cheatPanelOpen = !_cheatPanelOpen;

        // Z swaps between Arc (banked turning, tied to speed) and Rcs (free rotation) at the helm
        // (World.ShipField.cs, M41) - edge-triggered like M above, or holding it down would flip
        // the mode every frame.
        var toggleControlModeKeyPressed = isAtHelm && !_chatFocused && keyboard.IsKeyDown(Keys.Z) && !_prevGameplayKeyboard.IsKeyDown(Keys.Z);

        // L lands/takes off (M55) - same edge-triggered shape as Z above. World.PlanetLanding.cs's
        // own CanLandNow is what actually refuses to arm it away from a landable body's surface, so
        // this is sent unconditionally too.
        var toggleLandingKeyPressed = isAtHelm && !_chatFocused && keyboard.IsKeyDown(Keys.L) && !_prevGameplayKeyboard.IsKeyDown(Keys.L);

        // Push-to-talk voice (direct user request, "как в Баротравме") - V for local (proximity),
        // R for radio (heard ship-wide through RadioVoiceFilter). Held, not edge-triggered like
        // Z/L above: BeginTalking/StopTalking key off the raw down/up transition instead, same
        // "gate on chat/search focus, not on being at the helm" shape as the movement keys since
        // talking should work anywhere in the ship, not just at a console. First key held wins
        // until released - the other PTT key is ignored while already recording, kept simple
        // rather than allowing a mid-transmission mode switch.
        // Also excluded while the ship editor is open: its own Engine tool (Game1.ShipEditor.cs's
        // HandleEngineToolInput) already binds R to cycle the pending engine's facing, so without
        // this the same keypress would both rotate the ghost AND start a radio transmission.
        var voiceLocalKeyDown = !_chatFocused && !_shipEditorOpen && keyboard.IsKeyDown(Keys.V);
        var voiceRadioKeyDown = !_chatFocused && !_shipEditorOpen && keyboard.IsKeyDown(Keys.R);
        if (!_voiceCapture.IsRecording)
        {
            if (voiceLocalKeyDown)
                _voiceCapture.BeginTalking(isRadio: false);
            else if (voiceRadioKeyDown)
                _voiceCapture.BeginTalking(isRadio: true);
        }
        else if ((_voiceCapture.IsRadio && !voiceRadioKeyDown) || (!_voiceCapture.IsRadio && !voiceLocalKeyDown))
        {
            _voiceCapture.StopTalking();
        }

        var interactDown = keyboard.IsKeyDown(Keys.E);
        var spaceDown = keyboard.IsKeyDown(Keys.Space);
        var interactPressed = (interactDown && !_prevInteractDown) || escapeSendsInteract;
        var spacePressed = spaceDown && !_prevFireDown;
        _prevInteractDown = interactDown;
        _prevFireDown = spaceDown;

        // The scanner console opens on E like every other physical interaction (M47) rather than a
        // mouse click on its housing - still sent to the server as an ordinary InteractPressed
        // below (harmless: nothing server-side listens for it at this exact spot), just also
        // intercepted here to flip the client-only _openBlock state. Only opens, never closes -
        // Esc is the one way out (Game1.Input.cs's own CloseBlockIfWalkedAway explicitly excludes
        // BlockKind.Navigation from its usual auto-close-on-distance sweep for the same reason).
        if (interactPressed && myCharacter is not null && _openBlock.Kind != BlockKind.Navigation &&
            (new Vec2(myCharacter.X, myCharacter.Y) - _client.LatestSnapshot!.NavigationConsole.Position).Length() < TurretInteractionRadius)
        {
            _openBlock = ClickTarget.Navigation;
            _infoPanelOpen = false;
            _shipEditorOpen = false;
        }

        // Space means something different outside (push off toward the cursor) than manning a
        // turret (fire) - never both at once, since turrets are strictly indoors.
        var firePressed = !isOutside && spacePressed;
        var pushOffPressed = isOutside && spacePressed;
        // Held, not edge-triggered - a manned turret fires every tick the trigger is down
        // (World.Combat.cs's TryFire), its own cooldown pacing the shots for whichever of the 3
        // weapons is mounted there.
        var fireHeld = !isOutside && spaceDown;

        // Chat eats WASD as typed characters while focused (same idea as window 3's own search box
        // above it) - the character just stands still rather than getting walked around by whatever
        // letters happen to spell the message.
        var move = (isManningTurret || isAtHelm || _chatFocused) ? Vec2.Zero : ReadMoveInput(keyboard);
        _evaThrustLocal = Vec2.Zero;
        // The barrel traverses toward wherever the cursor is; A/D still nudge it for anyone who
        // wants the keyboard. Either way it's a rate, not a snap - the gun swings at its own
        // traverse speed (World.Combat.cs), so leading a moving target is a skill.
        var keyboardAim = isManningTurret ? ReadAimDirection(keyboard) : 0f;
        var aimDirection = keyboardAim != 0f || !isManningTurret ? keyboardAim : ReadTurretAimTowardCursor();
        var mouse = Mouse.GetState();

        // M55 follow-up - "экран изначально был отдален... чтобы было видно половину солнечной
        // системы": a freshly-started session used to leave the helm's schematic zoomed in at a
        // flat 1f (this class's own field initializer, set before any session/system exists) until
        // the player scrolled out by hand - a system can be millions of units across, so that
        // read as "empty black screen" rather than "solar system". Done exactly once, the first
        // frame a snapshot actually exists, rather than every frame (which would fight right back
        // against the player's own subsequent scroll input).
        if (!_mapZoomInitialized && _client.LatestSnapshot is { } zoomInitSnapshot)
        {
            _mapZoomInitialized = true;
            var initialSystem = zoomInitSnapshot.StarSystems.First(s => s.Id == zoomInitSnapshot.CurrentSystemId);
            // Same "whole system fits at 0.9 of screen width" shape scrolling's own minHelmZoom
            // uses below, just aimed at HALF that width instead of the whole thing - exactly twice
            // the zoom level, since this ignores CompressedUnits the same conservative way
            // minHelmZoom already does (real, uncompressed width; compression only ever makes the
            // true extent smaller on screen than this assumes, never bigger).
            var halfSystemZoom = DesignWidth * 0.9f / (GalaxyMapPanel.PixelsPerUnit * initialSystem.Width * 0.5f);
            var minInitialZoom = MathF.Min(0.02f, DesignWidth * 0.9f / (GalaxyMapPanel.PixelsPerUnit * initialSystem.Width));
            _mapZoom = Math.Clamp(halfSystemZoom, minInitialZoom, 3f);
        }

        // Content-каталог отсеков - the whole-ship overview gets its own free camera, same
        // right-drag-pans/scroll-zooms idiom as the galaxy map right below rather than a new one.
        // Reset the instant the overview freshly opens (not every frame it's open, which would
        // fight the player's own scrolling) so re-talking to the Shipwright always starts back at
        // the auto-fit view instead of wherever a previous visit left it scrolled to.
        var shipOverviewSnapshot = _client.LatestSnapshot;
        var shipOverviewActive = shipOverviewSnapshot is not null && ShipBuildOverviewActive(shipOverviewSnapshot);
        if (shipOverviewActive && !_shipOverviewWasActive)
        {
            _shipOverviewPanOffset = Vector2.Zero;
            _shipOverviewZoomMultiplier = 1f;
        }
        _shipOverviewWasActive = shipOverviewActive;

        // Galaxy map camera: right-drag to pan, scroll wheel to zoom - both harmless to read even
        // when the map isn't open (they just accumulate into fields nothing else looks at then).
        var mapOpen = _openBlock.Kind == BlockKind.Navigation;
        // Window 1 of the helm redesign (M47 follow-up) reuses this exact same schematic panel -
        // the pilot can still pan/zoom its own free camera, just never drives the sweep beam or
        // drops a marker from up there (those stay gated on mapOpen alone, below).
        // The console's own screen is ship-locked now (M48 follow-up - "привяжи сканер ровно к
        // кораблю, чтобы в менюшке сканера в центре всегда был корабль") - right-drag has nothing
        // left to move there (GalaxyMapPanel.Draw's own !pilotView branch ignores panOffset
        // entirely), so only the helm's still-free camera reads it here.
        if (isAtHelm && mouse.RightButton == ButtonState.Pressed)
        {
            if (_mapPanLastMouse is { } lastMouse)
                _mapPanOffset += new Vector2(mouse.Position.X - lastMouse.X, mouse.Position.Y - lastMouse.Y);
            _mapPanLastMouse = mouse.Position;
        }
        else if (mapOpen && mouse.RightButton == ButtonState.Pressed)
        {
            // Repositions the console's own housing instead of panning within it - see
            // _sonarPanelDragOffset's own doc comment. Reuses _mapPanLastMouse for the same delta
            // tracking the helm's own pan above uses; isAtHelm and mapOpen are never both true for
            // one client at once, so nothing here can mix the two drags up.
            if (_mapPanLastMouse is { } lastSonarMouse)
                _sonarPanelDragOffset += new Vector2(mouse.Position.X - lastSonarMouse.X, mouse.Position.Y - lastSonarMouse.Y);
            _mapPanLastMouse = mouse.Position;
        }
        else if (shipOverviewActive && mouse.RightButton == ButtonState.Pressed)
        {
            // Divided by the current zoom, unlike the helm's own screen-space _mapPanOffset above -
            // this offset feeds ComputeCamera's anchor (ship-local units, scaled by SceneZoom at
            // draw time same as everything else in that frame), not a raw screen position, so
            // dragging has to undo that same scaling to track the cursor 1:1 regardless of how far
            // zoomed in/out the overview currently is.
            if (_shipOverviewPanLastMouse is { } lastOverviewMouse)
                _shipOverviewPanOffset += new Vector2(mouse.Position.X - lastOverviewMouse.X, mouse.Position.Y - lastOverviewMouse.Y) / SceneZoom(shipOverviewSnapshot!);
            _shipOverviewPanLastMouse = mouse.Position;
        }
        else
        {
            _mapPanLastMouse = null;
            _shipOverviewPanLastMouse = null;
        }
        var scrollDelta = mouse.ScrollWheelValue - _prevScrollWheelValue;
        _prevScrollWheelValue = mouse.ScrollWheelValue;
        if (mapOpen && scrollDelta != 0)
        {
            // No panOffset compensation needed here (unlike the helm branch below) - the console's
            // own view is always ship-centred, so zooming in/out already holds the ship (and
            // everything else, relative to it) still without anything to solve backward.
            // Floored at MinConsoleZoom, not the helm's own 0.02 (M48 follow-up - "нельзя было
            // камеру отдалить дальше чем визуальное действие лучевого сонара") - past that point
            // the rim's own fixed screen radius would already be showing more world distance than
            // the beam actually reaches, which would just be lying about the sonar's own range.
            _mapZoom = Math.Clamp(_mapZoom * MathF.Pow(1.1f, scrollDelta / 120f), GalaxyMapPanel.MinConsoleZoom, 3f);
        }
        else if (isAtHelm && scrollDelta != 0 && _client.LatestSnapshot is { } scrollSnapshot)
        {
            // Zooms toward wherever the cursor already is, not the star at screen-centre - the
            // world point currently under the cursor is computed at the OLD zoom/pan first, then
            // panOffset is solved backward so that same point still lands under the cursor at the
            // NEW zoom (GalaxyMapPanel.ComputeMapOrigin's own anchor shifts nonlinearly with zoom
            // on its own, so panOffset has to move to compensate or the view would drift while
            // zooming instead of holding the cursor's own spot still).
            var cursorScreen = new Vector2(_designMouse.X, _designMouse.Y);
            var helmSystem = scrollSnapshot.StarSystems.First(s => s.Id == scrollSnapshot.CurrentSystemId);
            var helmStarPosition = new Vec2(helmSystem.Width / 2f, helmSystem.Height / 2f);
            var helmScreenCenter = new Vector2(DesignWidth / 2f, DesignHeight / 2f);
            var mapOriginBefore = GalaxyMapPanel.ComputeMapOrigin(helmScreenCenter, helmStarPosition, _mapZoom, _mapPanOffset);
            var worldUnderCursor = GalaxyMapPanel.ScreenToField(cursorScreen, mapOriginBefore, _mapZoom);
            // Scaled to the CURRENT system's own real width, not a fixed magic number any more (M48
            // tuned a flat 0.02 for its own doubled field; M52's per-system-generated, often far
            // bigger real-proportions layout made that fixed floor wrong in both directions - too
            // tight to ever see a huge system whole, too loose on a small one) - never LESS
            // permissive than the old 0.02 floor, only more so when the system genuinely needs it
            // ("масштаб... можно было также далеко отдалять камеру").
            var minHelmZoom = MathF.Min(0.02f, DesignWidth * 0.9f / (GalaxyMapPanel.PixelsPerUnit * helmSystem.Width));
            var newZoom = Math.Clamp(_mapZoom * MathF.Pow(1.1f, scrollDelta / 120f), minHelmZoom, 3f);
            _mapPanOffset = cursorScreen - helmScreenCenter - (worldUnderCursor - new Vector2((float)helmStarPosition.X, (float)helmStarPosition.Y)) * GalaxyMapPanel.PixelsPerUnit * newZoom;
            _mapZoom = newZoom;
        }
        else if (shipOverviewActive && scrollDelta != 0)
        {
            // A multiplier on top of ShipOverviewZoom's own auto-fit, not a replacement for it - the
            // auto-fit baseline still keeps a freshly-opened or freshly-reset view holding the whole
            // hull, same as the moment the dialogue itself opens; this just lets the player push in
            // past that to see one compartment closer up, or pull out further for a bigger margin to
            // attach into. Clamped well short of either extreme - the auto-fit number already tends
            // toward very small (a big hull, or the intentionally short WorldViewportSize.Y), so a
            // wide multiplier range would make it trivial to zoom the hull down to a few pixels or
            // blow it up past the screen entirely.
            _shipOverviewZoomMultiplier = Math.Clamp(_shipOverviewZoomMultiplier * MathF.Pow(1.1f, scrollDelta / 120f), 0.4f, 3f);
        }

        // Scanner sweep (World.Scanner.cs, M44) - M48 follow-up made this Barotrauma-style: instead
        // of dragging anywhere in the circle, the bearing is set by grabbing the small handle
        // sitting right on the rim (GalaxyMapPanel.GetScannerHandleScreen) and dragging it around
        // the border - the same "read the current mouse angle" idea turret aim uses
        // (ReadTurretAimTowardCursor), just started only from that one spot instead of anywhere. A
        // press that instead lands on one of this player's own scanner contacts promotes it onto
        // the shared map (PlaceScannerMarkerAtX/Y) and never starts a drag.
        var scannerSweepDegrees = myCharacter?.ScannerSweepDegrees ?? 0f;
        float? placeScannerMarkerAtX = null;
        float? placeScannerMarkerAtY = null;
        if (mapOpen && myCharacter is not null && _client.LatestSnapshot is { } mapSnapshot)
        {
            // Ship-locked (M48 follow-up), matching GalaxyMapPanel.Draw's own console-only camera
            // exactly - a click has to land on the same screen position as whatever it's clicking.
            var mapOrigin = GalaxyMapPanel.ComputeShipLockedMapOrigin(GalaxyMapPanelOrigin + _sonarPanelDragOffset, mapSnapshot.Voyage.ShipMapPosition, _mapZoom);
            var shipScreen = mapOrigin + new Vector2((float)mapSnapshot.Voyage.ShipMapPosition.X, (float)mapSnapshot.Voyage.ShipMapPosition.Y) * GalaxyMapPanel.PixelsPerUnit * _mapZoom;
            var cursorScreen = new Vector2(_designMouse.X, _designMouse.Y);
            var leftPressedNow = mouse.LeftButton == ButtonState.Pressed;
            if (leftPressedNow && !_scannerHandleDragging && _prevMapLeftButton != ButtonState.Pressed)
            {
                var hit = GalaxyMapPanel.HitTestContact(cursorScreen, myCharacter, mapOrigin, _mapZoom);
                if (hit is not null)
                {
                    placeScannerMarkerAtX = (float)hit.X;
                    placeScannerMarkerAtY = (float)hit.Y;
                }
                else if (GalaxyMapPanel.HitTestScannerHandle(cursorScreen, GalaxyMapPanelOrigin + _sonarPanelDragOffset, scannerSweepDegrees))
                {
                    _scannerHandleDragging = true;
                }
            }
            else if (!leftPressedNow)
            {
                _scannerHandleDragging = false;
            }

            if (_scannerHandleDragging)
            {
                var toCursor = cursorScreen - shipScreen;
                if (toCursor.LengthSquared() > 1f)
                    scannerSweepDegrees = MathF.Atan2(toCursor.Y, toCursor.X) * (180f / MathF.PI);
            }
            _prevMapLeftButton = mouse.LeftButton;
        }
        else
        {
            _scannerHandleDragging = false;
            _prevMapLeftButton = ButtonState.Released;
        }

        // Galactic map camera - same right-drag/scroll gesture as the system map above, own
        // independent zoom/pan state (GalacticMapPanel.cs).
        if (_galacticMapOpen && mouse.RightButton == ButtonState.Pressed)
        {
            if (_galacticMapPanLastMouse is { } lastGalacticMouse)
                _galacticMapPanOffset += new Vector2(mouse.Position.X - lastGalacticMouse.X, mouse.Position.Y - lastGalacticMouse.Y);
            _galacticMapPanLastMouse = mouse.Position;
        }
        else
        {
            _galacticMapPanLastMouse = null;
        }
        if (_galacticMapOpen && scrollDelta != 0)
            _galacticMapZoom = Math.Clamp(_galacticMapZoom * MathF.Pow(1.1f, scrollDelta / 120f), 0.3f, 3f);

        // Dragging gets first refusal on the button: a press that lands on an item slot starts a
        // drag instead of counting as a click, so releasing over the rack doesn't also read as
        // "clicked empty space, close the panel".
        // Panel dragging gets first refusal ahead of item dragging: grabbing the housing edge of the
        // rack must not also pick up whatever slot is nearest the cursor.
        UpdatePanelSounds(gameTime.TotalGameTime.TotalSeconds);
        var panelDragTookIt = _openBlock.Kind != BlockKind.None && UpdatePanelDrag(mouse, CurrentPanelKey, CurrentPanelSize);
        // Window 2's own widget drag (M47 follow-up) - same first-refusal priority, checked
        // whenever the player is at the helm regardless of _openBlock (this widget isn't one of
        // its panels).
        var helmWidgetDragTookIt = !panelDragTookIt && isAtHelm && UpdateHelmWidgetDrag(mouse);
        // The scanner console's own toggle-switch widget (M48 follow-up) - same first-refusal
        // priority, console-operator only (BlockKind.Navigation) unlike the helm widget above.
        var scannerWidgetDragTookIt = !panelDragTookIt && !helmWidgetDragTookIt &&
            _openBlock.Kind == BlockKind.Navigation && UpdateScannerWidgetDrag(mouse);
        if (panelDragTookIt || helmWidgetDragTookIt || scannerWidgetDragTookIt)
        {
            _prevLeftMouseButton = mouse.LeftButton;
            _prevDragButton = mouse.LeftButton;
        }
        var (moveItemFrom, moveItemTo, dragTookTheClick) = panelDragTookIt || helmWidgetDragTookIt || scannerWidgetDragTookIt
            ? (null, null, true)
            : UpdateItemDrag(mouse, gameTime.TotalGameTime.TotalSeconds);
        if (dragTookTheClick)
            _prevLeftMouseButton = mouse.LeftButton; // keep HandleMouseClick's own edge detection in step
        var (toggleHoldSlotIndex, toggleReactorSlotIndex, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, doorToggleId) =
            dragTookTheClick
                ? (-1, -1, (ItemType?)null, -1, false, false, (ShipUpgradeTrack?)null, (string?)null)
                : HandleMouseClick(mouse);

        // Bail out of the rest of this frame immediately - _client is about to become null, and
        // everything below (up to and including this frame's _client.SendInput) assumes it isn't.
        if (_pendingReturnToMainMenu)
        {
            _pendingReturnToMainMenu = false;
            ReturnToMainMenu();
            base.Update(gameTime);
            return;
        }

        // Number-row hotkey for holding a slot's item, same edge-triggered field a hold-strip click
        // sends - only when nothing else already claimed this tick's toggle.
        if (toggleHoldSlotIndex < 0 && !distributionOpen && ReadInventoryHotkeySlot(keyboard) is { } hotkeySlot)
            toggleHoldSlotIndex = hotkeySlot;
        // Stabilization is a mode the pilot leaves on, not a one-frame pulse: the server takes it
        // as an instruction for this tick only, so the client latches it and keeps sending it until
        // the controls are touched again.
        // Window 3's search box eats W/A/D/S/X/Z as typed characters while focused (M47 follow-up) -
        // the ship just coasts on whatever heading it already had, same as while any other console
        // is open, rather than the pilot's own typing also steering it.
        var flightControlsLive = isAtHelm && !_chatFocused;
        if (flightControlsLive && keyboard.IsKeyDown(Keys.S))
            _helmStabilizeLatched = true;
        var (helmThrottle, helmTurn) = flightControlsLive ? ReadHelmInput(keyboard) : (0f, 0f);
        if (helmThrottle != 0f || helmTurn != 0f)
            _helmStabilizeLatched = false; // taking the controls back cancels the brake
        var stabilizeEngaged = isAtHelm && _helmStabilizeLatched;
        var pushOffDirection = isOutside ? ReadPushOffDirection() : Vec2.Zero;
        // The head follows the cursor whenever the player is a person standing somewhere - not at
        // a console, where the mouse belongs to that console's own controls.
        var lookDirection = isAtHelm || isManningTurret || _openBlock.Kind != BlockKind.None
            ? Vec2.Zero
            : ReadPushOffDirection();

        // Outside, both of those are read off the screen - which is the ship's own unrotated frame -
        // while the server's EVA physics runs in field coordinates. Rotate them across, or pushing
        // off "toward the cursor" on a rotated ship throws you somewhere else entirely.
        if (isOutside && _client.LatestSnapshot is { } outsideSnapshot)
        {
            var rotation = outsideSnapshot.ShipField.RotationDegrees;
            // Kept before the rotation: the scene is drawn in the ship's frame, and this is what the
            // RCS plume is aimed with. Rotating first and unrotating later would be the same number
            // twice with a rounding error in between.
            _evaThrustLocal = move;
            move = ShipLocalFrame.ToWorldDirection(move, rotation);
            pushOffDirection = ShipLocalFrame.ToWorldDirection(pushOffDirection, rotation);
            lookDirection = ShipLocalFrame.ToWorldDirection(lookDirection, rotation);
        }

        var shipPurchase = _pendingShipPurchase;
        var buildRoom = _pendingBuildRoom;
        _pendingBuildRoom = null;
        var demolishRoomId = _pendingDemolishRoomId;
        _pendingDemolishRoomId = null;
        var questKind = _pendingQuestKind;
        var dockPressed = _pendingDock;
        var hireCandidateId = _pendingHireCandidateId;
        var toggleControlModePressed = toggleControlModeKeyPressed || _pendingToggleControlMode;
        var toggleLandingPressed = toggleLandingKeyPressed || _pendingToggleLanding;
        // M57 - leaving the Engineer tab drops the remote-repair focus too, the same way standing
        // up from the helm already does server-side (World.Interact.cs) - otherwise switching to
        // Captain/Scientist and back would silently resume whatever device was focused before.
        if (_helmTab != HelmTab.Engineer)
            _engineerFocusDeviceId = null;
        var requestedTimeAccelerationLevel = _pendingTimeAccelerationLevel;
        var flipHeadingPressed = _pendingFlipHeading;
        var scannerPingPressed = _pendingScannerPing;
        var requestedScannerMode = _requestedScannerMode;
        _pendingShipPurchase = null; // edge-triggered: sent exactly once per click
        _pendingQuestKind = null;
        _pendingDock = false;
        _pendingHireCandidateId = null;
        _pendingToggleControlMode = false;
        _pendingToggleLanding = false;
        _pendingTimeAccelerationLevel = null;
        _pendingFlipHeading = false;
        _pendingScannerPing = false;

        var tankAttach = _pendingTankAttach;
        var tankDetach = _pendingTankDetach;
        _pendingTankAttach = null;
        _pendingTankDetach = null;

        var pinInteract = _pendingPinInteract;
        var wireBendAt = _pendingWireBendAt;
        var componentMountInteractId = _pendingComponentMountInteractId;
        var sabotageDeviceId = _pendingSabotageDeviceId;
        _pendingPinInteract = null;
        _pendingWireBendAt = null;
        _pendingComponentMountInteractId = null;
        _pendingSabotageDeviceId = null;

        var pickupDroppedItemId = _pendingPickupDroppedItemId;
        var dropItemFrom = _pendingDropItemFrom;
        _pendingPickupDroppedItemId = null;
        _pendingDropItemFrom = null;

        var suitLockerInteractId = _pendingSuitLockerInteractId;
        var turretInteractId = _pendingTurretInteractId;
        var ammoStorageInteractId = _pendingAmmoStorageInteractId;
        var stealCrateId = _pendingStealCrateId;
        var repairDeviceId = _pendingRepairDeviceId;
        _pendingSuitLockerInteractId = null;
        _pendingTurretInteractId = null;
        _pendingAmmoStorageInteractId = null;
        _pendingStealCrateId = null;
        _pendingRepairDeviceId = null;

        var abandonQuestPressed = _pendingAbandonQuest;
        _pendingAbandonQuest = false;

        var warpToSystemId = _pendingWarpToSystemId;
        _pendingWarpToSystemId = null;

        var setOwnRoleTo = _pendingSetOwnRoleTo;
        _pendingSetOwnRoleTo = null;

        var playCard = _pendingPlayCard;
        var cardGameTakePressed = _pendingCardGameTake;
        var cardGameEndRoundPressed = _pendingCardGameEndRound;
        _pendingPlayCard = null;
        _pendingCardGameTake = false;
        _pendingCardGameEndRound = false;

        var chooseCardTableGame = _pendingCardTableChoice;
        var frontsSetAllocationIndex = _pendingFrontsAllocationIndex;
        var frontsSetAllocationAmount = _pendingFrontsAllocationAmount;
        var frontsResolvePressed = _pendingFrontsResolve;
        _pendingCardTableChoice = null;
        _pendingFrontsAllocationIndex = null;
        _pendingFrontsAllocationAmount = null;
        _pendingFrontsResolve = false;

        var toggleLightsPressed = _pendingToggleLights;
        var toggleReactorEmergencyPressed = _pendingToggleReactorEmergency;
        var toggleDoorsLockedPressed = _pendingToggleDoorsLocked;
        _pendingToggleLights = false;
        _pendingToggleReactorEmergency = false;
        _pendingToggleDoorsLocked = false;

        var jukeboxTogglePressed = _pendingJukeboxToggle;
        var jukeboxNextTrackPressed = _pendingJukeboxNextTrack;
        var jukeboxPrevTrackPressed = _pendingJukeboxPrevTrack;
        var jukeboxVolumeUpPressed = _pendingJukeboxVolumeUp;
        var jukeboxVolumeDownPressed = _pendingJukeboxVolumeDown;
        _pendingJukeboxToggle = false;
        _pendingJukeboxNextTrack = false;
        _pendingJukeboxPrevTrack = false;
        _pendingJukeboxVolumeUp = false;
        _pendingJukeboxVolumeDown = false;

        var terminalTogglePressed = _pendingTerminalToggle;
        _pendingTerminalToggle = false;

        // Right-click backs out one step of a pending wire-lay without walking back to its start pin
        // - the last fixed bend if there is one, the whole anchor otherwise (World.Wiring.cs's
        // HandleWireLayCancel). Edge-triggered now (unlike before this could pop multiple bends in
        // the time it takes to release the button), separate from the map panels' own continuous
        // RMB-drag checks.
        var wireLayCancelPressed = mouse.RightButton == ButtonState.Pressed && _prevRightMouseButton == ButtonState.Released &&
            myCharacter?.LayingWireFromPin is not null;

        // Content-каталог отсеков - same right-click-cancel idea as the wire-lay check right above
        // (back out of an in-progress module placement without spending anything - no server round
        // trip needed, nothing was ever sent until a confirm click), extended to also leave the
        // Shipwright's whole-ship overview entirely when nothing is left to cancel (the fallthrough
        // in HandleMouseClick's own me.OnStation branch means clicking empty station floor no longer
        // closes it, so right-click is this mode's own "back out" gesture instead).
        //
        // Outside the overview this still fires the instant the button goes down, same as always -
        // but the overview's own free camera now also reads a HELD right button as pan-dragging
        // (right above), so a press there can no longer mean "cancel" until release tells the two
        // apart: released close to where it went down is a click (cancel/back-out); released well
        // away from it was a drag that just finished, and undoing the player's own placement pick or
        // closing their dialogue out from under a pan they were mid-gesture on would be exactly
        // backwards.
        var rightPressedThisFrame = mouse.RightButton == ButtonState.Pressed && _prevRightMouseButton == ButtonState.Released;
        var rightReleasedThisFrame = mouse.RightButton == ButtonState.Released && _prevRightMouseButton == ButtonState.Pressed;
        if (rightPressedThisFrame)
            _shipOverviewRightPressPos = mouse.Position;

        if (!shipOverviewActive && rightPressedThisFrame && _placingRoomCatalogId is not null)
        {
            _placingRoomCatalogId = null;
        }
        else if (shipOverviewActive && rightReleasedThisFrame)
        {
            const float dragThresholdPixels = 6f;
            var wasDrag = _shipOverviewRightPressPos is { } pressPos &&
                Vector2.Distance(pressPos.ToVector2(), mouse.Position.ToVector2()) > dragThresholdPixels;
            if (!wasDrag)
            {
                if (_placingRoomCatalogId is not null)
                    _placingRoomCatalogId = null;
                else
                {
                    _talkingToNpcId = null;
                    _openBlock = ClickTarget.None;
                }
            }
        }

        _prevRightMouseButton = mouse.RightButton;

        // Barotrauma's rule: the held tool works on the left button, aimed at the cursor. Held, not
        // clicked - the flame burns while the button is down (World.Cutting.cs) - and suppressed
        // while a drag is in flight so grabbing an item never lights the torch.
        var cutHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingCutter();
        var weldHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingWelder();
        var axeSwingHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingAxe();

        var debugSpawnEnemyPressed = _debugSpawnEnemyClickedThisFrame;
        _debugSpawnEnemyClickedThisFrame = false;
        var debugAddCreditsPressed = _debugAddCreditsClickedThisFrame;
        _debugAddCreditsClickedThisFrame = false;

        // One-shot outgoing chat message (OnChatTextInput's submit path sets this) - same
        // capture-send-clear lifecycle as _pendingBuildRoom/etc above, so it isn't resent every
        // frame after the one it was actually typed on.
        var chatMessage = _pendingChatMessage;
        _pendingChatMessage = null;

        // One-shot outgoing voice chunk (VoiceCapture's own BufferReady handler sets this) - same
        // capture-send-clear lifecycle as chatMessage above, so a mic buffer is never resent.
        var voiceChunk = _voiceCapture.TakePendingChunk();

        _client.SendInput(move, powerSystemIndexToSend, powerDirection, interactPressed, aimDirection, firePressed, toggleHoldSlotIndex, toggleReactorSlotIndex, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, helmThrottle, helmTurn, stabilizeEngaged, doorToggleId, pushOffPressed, (float)pushOffDirection.X, (float)pushOffDirection.Y, shipPurchase, questKind, dockPressed, moveItemFrom, moveItemTo, (float)lookDirection.X, (float)lookDirection.Y,
            tankAttach?.From, tankAttach?.To, tankDetach, cutHeld, hireCandidateId, weldHeld, pinInteract, wireLayCancelPressed, null, componentMountInteractId, dropItemFrom, pickupDroppedItemId, abandonQuestPressed, warpToSystemId,
            _nickname, setOwnRoleTo, playCard?.Rank, playCard?.Suit, cardGameTakePressed, cardGameEndRoundPressed,
            _client.LatestSnapshot?.ServerTimestampMs ?? 0, (float?)wireBendAt?.X, (float?)wireBendAt?.Y,
            toggleLightsPressed, toggleReactorEmergencyPressed, toggleDoorsLockedPressed, axeSwingHeld, sabotageDeviceId, toggleControlModePressed,
            scannerSweepDegrees, placeScannerMarkerAtX, placeScannerMarkerAtY, scannerPingPressed, requestedScannerMode,
            jukeboxTogglePressed, jukeboxNextTrackPressed, jukeboxPrevTrackPressed, jukeboxVolumeUpPressed, jukeboxVolumeDownPressed,
            fireHeld, debugSpawnEnemyPressed, toggleLandingPressed, requestedTimeAccelerationLevel, _engineerFocusDeviceId, flipHeadingPressed,
            buildRoom, demolishRoomId, debugAddCreditsPressed, chatMessage, voiceChunk,
            chooseCardTableGame, frontsSetAllocationIndex, frontsSetAllocationAmount, frontsResolvePressed, terminalTogglePressed,
            suitLockerInteractId, turretInteractId, ammoStorageInteractId, stealCrateId, repairDeviceId);
        _client.PollSnapshots();
        CloseBlockIfWalkedAway(_client.LatestSnapshot);
        UpdateCameraLookOffset(_client.LatestSnapshot, (float)gameTime.ElapsedGameTime.TotalSeconds);

        // A short fade-to-black-and-back across the moment the cockpit view and the system map
        // swap (M45) - a scoped-down "continuous zoom": the two scenes still each render at their
        // own existing scale (ShipRenderer/GalaxyMapPanel, untouched), this just softens the
        // otherwise instant cut between them into something that reads as one continuous motion
        // rather than a mode switch. Whichever way _openBlock just changed this frame (opened by a
        // console click, closed by walking away or Escape) restarts the same fade.
        var navigationOpenNow = _openBlock.Kind == BlockKind.Navigation;
        if (navigationOpenNow != _wasNavigationOpen)
            _navTransitionRemaining = NavTransitionDuration;
        else if (_navTransitionRemaining > 0f)
            _navTransitionRemaining = Math.Max(0f, _navTransitionRemaining - (float)gameTime.ElapsedGameTime.TotalSeconds);
        _wasNavigationOpen = navigationOpenNow;

        // One shared tracker (not one per renderer) so a message only ever spawns one bubble
        // regardless of which renderer context the sender is currently drawn in (Ship/Field).
        _chatBubbleTracker.Update(_client.LatestSnapshot?.ChatLog, (float)gameTime.ElapsedGameTime.TotalSeconds);

        // Voice chunks relayed this tick (World.Voice.cs) - proximity-mixed for local mode, always
        // full volume for radio (Audio/VoicePlayback.cs). myCharacter/snapshot are already resolved
        // above for this same Update call.
        if (_client.LatestSnapshot is { } snapshotForVoice)
        {
            var myVoicePosition = myCharacter is { } myVoiceChar ? new Vec2(myVoiceChar.X, myVoiceChar.Y) : Vec2.Zero;
            _voicePlayback.Update(snapshotForVoice.VoiceChunks, myVoicePosition,
                senderId => snapshotForVoice.Characters.FirstOrDefault(c => c.PlayerId == senderId) is { } sender
                    ? new Vec2(sender.X, sender.Y)
                    : (Vec2?)null);
        }

        _effectTracker.Step((float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_client.LatestSnapshot is { } latestForEffects)
        {
            _effectTracker.Detect(_previousSnapshot, latestForEffects);
            UpdateWorldSounds(_previousSnapshot, latestForEffects, gameTime.TotalGameTime.TotalSeconds);
            _previousSnapshot = latestForEffects;
        }
        _atmosphere.Step((float)gameTime.ElapsedGameTime.TotalSeconds, _client.LatestSnapshot);

        _prevGameplayKeyboard = keyboard;
        base.Update(gameTime);
    }


    // The screen origin that places a ship-local point at its correct on-screen position this
    // frame - the single following camera shared by the ship interior and the space around it
    // (game_design.md: "one continuous space, no hidden transition"). Always centers the local
    // player: indoors that's just their raw (ship-local) position; outside, their resolved world
    // position first gets folded back into the ship's own frame via ShipLocalFrame.ToLocal, so a
    // step through the airlock never causes the camera to jump.
    // How far past the muzzle the periscope view sits. The gun is bolted to the plating at one end
    // of the ship while its gunner sits at a console somewhere else entirely, so a camera on the
    // person shows neither the barrel nor anything it could shoot at - manning a turret puts the
    // view out on the gun instead, which is what a periscope is.
    internal static Rectangle GetTopBarButtonRect(int index) =>
        new((int)TopBarOrigin.X + index * (TopBarButtonSize + TopBarButtonGap), (int)TopBarOrigin.Y, TopBarButtonSize, TopBarButtonSize);

    private static Vector2 RectCenter(Rectangle rect) => new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

    private static readonly Color TopBarPlate = new(26, 27, 32);
    private static readonly Color TopBarGold = new(214, 178, 112);

    private void DrawTopBar(SpriteBatch spriteBatch, WorldSnapshot? snapshot)
    {
        var crewRect = GetTopBarButtonRect(0);
        var managementRect = GetTopBarButtonRect(1);
        var infoRect = GetTopBarButtonRect(2);

        DrawTopBarButtonFrame(spriteBatch, crewRect);
        HudIcons.DrawCrewGlyph(spriteBatch, _pixel, RectCenter(crewRect), 0.85f, TopBarGold);
        if (_crewPanelOpen)
            DrawSlotHighlight(crewRect, Color.LightSkyBlue);

        DrawTopBarButtonFrame(spriteBatch, managementRect);
        HudIcons.DrawShipGlyph(spriteBatch, _pixel, RectCenter(managementRect), 0.7f, TopBarGold);
        if (_shipEditorOpen)
            DrawSlotHighlight(managementRect, Color.LightSkyBlue);

        DrawTopBarButtonFrame(spriteBatch, infoRect);
        HudIcons.DrawBarsGlyph(spriteBatch, _pixel, RectCenter(infoRect), 0.85f, TopBarGold);
        if (_infoPanelOpen)
            DrawSlotHighlight(infoRect, Color.LightSkyBlue);
    }

    // A bevelled plate with a gold medallion behind the glyph, rather than a flat icon on a solid
    // square - closer to the "engraved emblem" look a roster/top-bar button reads best as.
    private void DrawTopBarButtonFrame(SpriteBatch spriteBatch, Rectangle rect)
    {
        spriteBatch.Draw(_pixel, rect, TopBarPlate);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), Color.White * 0.12f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), Color.White * 0.10f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), Color.Black * 0.4f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), Color.Black * 0.4f);

        var center = RectCenter(rect);
        HudIcons.FillCircle(spriteBatch, _pixel, center, rect.Width * 0.40f, new Color(40, 38, 34));
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, rect.Width * 0.40f, 0f, 360f, TopBarGold * 0.75f, 20, 1.6f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, TopBarGold * 0.55f, 1);
    }

    // TEMP-DIAG (M51 slowdown investigation): same wrap-and-time approach as UpdateCore above - Draw
    // has its own early returns (the pre-session menu/loading-screen branch), so timing it from a
    // thin outer wrapper is the only way to catch every path through it, not just the one that falls
    // through to the very end.
    private double _diagLastDrawMs;

    protected override void Draw(GameTime gameTime)
    {
        var diagDrawStopwatch = System.Diagnostics.Stopwatch.StartNew();
        DrawCore(gameTime);
        _diagLastDrawMs = diagDrawStopwatch.Elapsed.TotalMilliseconds;
    }

    private void DrawCore(GameTime gameTime)
    {
        if (!_sessionStarted)
        {
            if (_pendingSession is not null)
            {
                DrawSessionLoadingScreen();
                base.Draw(gameTime);
                return;
            }

            // The menu goes through the same post chain the world does. It used to return early,
            // straight past ScenePost, which meant the bloom, grade, vignette, grain and dither
            // built for the game simply did not exist on the first screen anybody ever sees.
            var menuSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
            var menuPost = _scenePost.Begin(Color.Black);
            if (!menuPost)
                GraphicsDevice.Clear(Color.Black);
            DrawMenu(menuSeconds);
            if (menuPost)
            {
                DrawMenuLightMask(menuSeconds);
                DrawMenuDistortion(menuSeconds);
                // The bright "poster" grading (high exposure/bloom/aberration) is tuned for the
                // actual main menu's planet backdrop, same as DrawMenuDistortion already restricts
                // itself to - applying it to a flat working screen like the Ship Editor's grid reads
                // as an unexplained haze/blur (direct user report) rather than a look, so other
                // screens just use Present with whatever look is already active (same as in-session).
                if (_menuScreen == MenuScreen.Main)
                {
                    var savedLook = ApplyMenuPostLook();
                    _scenePost.Present(_spriteBatch, menuSeconds);
                    RestorePostLook(savedLook);
                }
                else
                {
                    _scenePost.Present(_spriteBatch, menuSeconds);
                }
            }

            // The custom cursor (GameCursor's own doc comment) is drawn dead last in the in-session
            // HUD pass below, which this early return skips entirely - direct user report ("в главном
            // меню нет курсора"): with IsMouseVisible false and nothing else ever drawing one, the
            // menu (and every other pre-session screen) had no cursor at all. Plain arrow throughout -
            // there is no session snapshot yet to compute a hovered-interactable hand cursor from.
            _spriteBatch.Begin(transformMatrix: _renderScale);
            GameCursor.Draw(_spriteBatch, _pixel, _designMouse.ToVector2(), false);
            _spriteBatch.End();

            base.Draw(gameTime);
            return;
        }

        // The line-of-sight and room-lighting masks render into their own targets, which has to
        // happen before the backbuffer is cleared and drawn into - swapping render targets discards
        // whatever the backbuffer already held.
        var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        // M55 follow-up - "почему при отстыковке я оказался не около станции" / "на месте текущих
        // значков станций там нет" / "они находятся в другом месте": every fix so far this session
        // (compression consistency, elliptical-orbit position, host-relative anchoring) still
        // compared against a body's position computed from THIS - the client's own wall-clock
        // since the process launched (gameTime.TotalGameTime), completely unrelated to the
        // SERVER's own simulation clock (World.Gravity.cs's CurrentTotalSeconds = Tick /
        // SecondsPerTick, which starts from world/save creation, not from whenever this particular
        // client happened to connect or restart). Every orbiting body's angle is a function of
        // that time, so the two clocks disagreeing meant the map was always drawing every planet,
        // moon AND station (M52 made stations orbit too) at the WRONG point along its own orbit
        // relative to whatever the server's real physics (docking capture radius, "До причала",
        // Undock's own snap-to-station) actually has it at - no amount of fixing the SCREEN
        // projection math could ever paper over comparing against the wrong real position to
        // begin with. snapshot.Tick is the one number the server and client both agree on for the
        // current instant (already sent every snapshot) - dividing by the server's own fixed tick
        // rate (World.Gravity.cs's SecondsPerTick, 30/s, the same rate the "Тики N/30" diagnostic
        // line already assumes) reconstructs the exact time value CurrentTotalSeconds itself is.
        const float serverTicksPerSecond = 30f;
        // M55 follow-up - "на сильных приближениях корабль очень сильно дергается": the FIRST fix
        // here (tickSnapshot.Tick / serverTicksPerSecond, used directly) only advances once per
        // server tick - correct, but a visible STEP every 1/30s with nothing smooth in between,
        // which reads as fine at ordinary zoom (each step is a fraction of a pixel) and as
        // outright jitter once zoomed in far enough that a single tick's worth of real orbital
        // motion covers several pixels. gameTime.TotalGameTime itself already advances perfectly
        // smoothly every frame - it was only ever wrong by a fixed OFFSET (the client's own launch
        // time vs the server's own already-elapsed session time), not by rate. Solving for that
        // offset once, the moment a snapshot first exists, and just adding the client's own
        // already-smooth clock to it from then on gives both: correct (anchored to the server's
        // real elapsed time) AND smooth (never stops advancing between ticks). Solo sessions run
        // client and server in the same process (GameServer.Current) sharing one real clock, so
        // this offset does not need periodic re-syncing to avoid drifting apart over time.
        if (_serverTimeOffsetSeconds is null && _client.LatestSnapshot is { } offsetSnapshot)
            _serverTimeOffsetSeconds = offsetSnapshot.Tick / (double)serverTicksPerSecond - gameTime.TotalGameTime.TotalSeconds;
        var serverTotalSeconds = _serverTimeOffsetSeconds is { } offsetSeconds
            ? (float)(gameTime.TotalGameTime.TotalSeconds + offsetSeconds)
            : totalSeconds;

        // TEMP-DIAG-BEGIN (M51 slowdown investigation - see the fields' own comment)
        _diagFrameCountInWindow++;
        if (totalSeconds - _diagFpsWindowStartSeconds >= 1.0)
        {
            _diagDisplayedFps = (float)(_diagFrameCountInWindow / (totalSeconds - _diagFpsWindowStartSeconds));
            _diagFrameCountInWindow = 0;
            _diagFpsWindowStartSeconds = totalSeconds;
        }
        if (_client.LatestSnapshot is { } diagSnapshot && diagSnapshot.Tick != _diagLastSeenTick)
        {
            _diagLastSeenTick = diagSnapshot.Tick;
            _diagTicksSeenInWindow++;
        }
        if (totalSeconds - _diagTickWindowStartSeconds >= 1.0)
        {
            _diagDisplayedTicksPerSecond = _diagTicksSeenInWindow;
            _diagTicksSeenInWindow = 0;
            _diagTickWindowStartSeconds = totalSeconds;

            var diagAllocBytes = GC.GetTotalAllocatedBytes();
            var diagGc0 = GC.CollectionCount(0);
            var diagGc1 = GC.CollectionCount(1);
            var diagGc2 = GC.CollectionCount(2);
            _diagAllocMbPerSecond = (diagAllocBytes - _diagLastAllocBytes) / (1024f * 1024f);
            _diagGc0PerSecond = diagGc0 - _diagLastGc0;
            _diagGc1PerSecond = diagGc1 - _diagLastGc1;
            _diagGc2PerSecond = diagGc2 - _diagLastGc2;
            _diagLastAllocBytes = diagAllocBytes;
            _diagLastGc0 = diagGc0;
            _diagLastGc1 = diagGc1;
            _diagLastGc2 = diagGc2;
        }
        // TEMP-DIAG-END

        // TEMP-DIAG-BEGIN (M51 - narrowing down which phase of DrawCore is the 548ms cost)
        var diagPhaseStopwatch = System.Diagnostics.Stopwatch.StartNew();
        // TEMP-DIAG-END
        var maskReady = _client.LatestSnapshot is { } maskSnapshot && BuildVisibilityMask(maskSnapshot, totalSeconds);
        // TEMP-DIAG-BEGIN
        _diagMaskMs = diagPhaseStopwatch.Elapsed.TotalMilliseconds;
        diagPhaseStopwatch.Restart();
        // TEMP-DIAG-END

        // The scene and its light mask go into ScenePost's off-screen target so a full-screen
        // shader has a finished frame to sample; when the effect isn't loaded this is false and
        // everything below draws at the backbuffer directly, the way it always did. Either way it
        // has to happen here, before the backbuffer is touched - same render target discard rule
        // the mask building above is subject to.
        var postCapturing = _scenePost.Begin(Color.Black);
        if (!postCapturing)
            GraphicsDevice.Clear(Color.Black);

        // Manning a turret pulls the whole scene back to half scale (SceneZoom) so the gunner can
        // see as far as the gun shoots; everywhere else this is the identity.
        var sceneZoom = _client.LatestSnapshot is { } zoomSnapshot ? SceneZoom(zoomSnapshot) : 1f;
        // Also spun around the screen's own center (TurretViewRotationDegrees) while manning a
        // turret, so the gun's facing direction reads as screen-up - identity (0°) everywhere
        // else. The pivot is the same point ComputeCamera anchors the manned turret's view on, so
        // rotating around it leaves the turret itself fixed at screen-center instead of swinging
        // it off to one side.
        var sceneRotationDegrees = _client.LatestSnapshot is { } rotSnapshot
            ? MannedTurret(rotSnapshot) is not null ? TurretViewRotationDegrees(rotSnapshot) : LandingApproachRotationDegrees(rotSnapshot)
            : 0f;
        var screenPivot = (WorldViewportOrigin + WorldViewportSize / 2f) / sceneZoom;
        var sceneTransform =
            Matrix.CreateTranslation(-screenPivot.X, -screenPivot.Y, 0f) *
            Matrix.CreateRotationZ(MathHelper.ToRadians(sceneRotationDegrees)) *
            Matrix.CreateTranslation(screenPivot.X, screenPivot.Y, 0f) *
            Matrix.CreateScale(sceneZoom, sceneZoom, 1f) * _renderScale;
        _spriteBatch.Begin(transformMatrix: sceneTransform);
        _shipInteriorOrigin = null;
        // TEMP-DIAG-BEGIN (M55 follow-up - "слайдшоу для модельки корабля": these three only ever
        // get overwritten by the final "else" branch below (actually walking the ship interior) -
        // every other branch (helm, docked, boarding, map) left them holding whatever they were
        // the last time that branch ran, which reads as a frozen, wildly misleading number the
        // instant the player is somewhere else - e.g. a real "Поле 31.0мс" from minutes ago,
        // sitting there unchanged at the helm where this frame's real field-render cost is 0.
        // Cleared here so every branch that skips the measurement below honestly shows 0.0мс
        // instead of stale leftovers from a different camera mode.
        _diagShipMs = _diagStationMs = _diagFieldMs = 0;
        // TEMP-DIAG-END
        if (_client.LatestSnapshot is { } snapshot)
        {
            var myCharacter = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
            var myIsAtHelm = myCharacter?.IsAtHelm ?? false;

            // The galaxy map / station / wiring / helm views take over the ship-interior viewport
            // for as long as they're open — there's nowhere else on screen big enough to put them.
            // Everything else shares one continuous camera: the ship interior is always drawn,
            // with whatever's outside it (asteroids, ore, EVA characters) layered on top in the
            // same ship-local frame, so walking through the airlock never swaps renderer or scale.
            if (_galacticMapOpen)
            {
                // Nothing to draw here - the galactic map is a HUD-batch overlay now (see below),
                // exempt from the sight-cone/room-lighting mask like Info/Crew already are. It used
                // to live in this scene batch, which meant standing in a blind spot when pressing M
                // made the whole map fade to black along with everything else.
            }
            else if (_externalCameraMode)
            {
                // Same reasoning as the galactic map above - drawn later as its own HUD-batch
                // overlay (ExternalCameraPanel needs its own scissored sub-batches per quadrant
                // anyway, which this shared, rotated/masked scene batch has no room for).
            }
            else if (_infoPanelOpen)
                _infoPanel.Draw(_spriteBatch, snapshot, _client.PlayerId, _infoPanelTab, InfoPanelOrigin);
            else if (myIsAtHelm)
            {
                // M57 - the 3-window helm redesign (M47 follow-up) becomes 3 switchable tabs
                // (HelmTab.cs): Captain flies (window 2's button widget + the time-acceleration
                // selector), Scientist watches the sonar (window 1's schematic/fog-of-war map,
                // reused wholesale), Engineer works the device list (window 3's schematic).
                // Nothing here needs _shipInteriorOrigin - same as the Navigation/galactic-map/
                // external-camera branches above, which leave it null too.
                // Backdrop/instruments first, tab bar drawn LAST so it always sits on top instead
                // of being painted over by whichever window is behind it (M57 follow-up - the map
                // window is large enough to otherwise cover a same-corner overlay entirely).
                switch (_helmTab)
                {
                    case HelmTab.Captain:
                        // The captain still needs to see where the ship actually is/where it's
                        // headed to fly it - same near-fullscreen map window 1 always showed,
                        // just now specifically the captain's own tab rather than shared with
                        // everyone at helm regardless of what they were doing.
                        _galaxyMapPanel.Draw(_spriteBatch, snapshot, GalaxyMapPanelOrigin, _mapZoom, _mapPanOffset, _client.PlayerId, serverTotalSeconds, pilotView: true);
                        _helmButtonsWidget.Draw(_spriteBatch, snapshot, _helmWidgetPosition,
                            snapshot.Cameras.Count > 0 && ComputeShipPowerMood(snapshot).PowerFraction > 0.01f, _externalCameraMode);
                        _timeAccelerationWidget.Draw(_spriteBatch, snapshot.TimeAccelerationLevel, _helmWidgetPosition + new Vector2(0, -46));
                        break;
                    case HelmTab.Scientist:
                        _galaxyMapPanel.Draw(_spriteBatch, snapshot, GalaxyMapPanelOrigin, _mapZoom, _mapPanOffset, _client.PlayerId, serverTotalSeconds, pilotView: true);
                        break;
                    case HelmTab.Engineer:
                        _engineerDevicePanel.Draw(_spriteBatch, snapshot, EngineerDevicePanelOrigin, _engineerFocusDeviceId);
                        break;
                }
                _helmTabBar.Draw(_spriteBatch, _helmTab, HelmTabBarOrigin, _designMouse);
            }
            else if (myCharacter?.OnEnemyShip == true)
            {
                // Needed for the same HUD-batch tool-target bar the player's own ship gets further
                // down (Game1.cs's wallToolOrigin block) - left null this branch never ran there,
                // so a door/wall block actually being cut aboard a boarded hull never showed its
                // Hp bar even once the server started reporting a real target id.
                var boardingOrigin = ComputeStationCamera(myCharacter);
                _shipInteriorOrigin = boardingOrigin;
                _boardingRenderer.Draw(_spriteBatch, snapshot, boardingOrigin, totalSeconds);
            }
            else
            {
                var (origin, hullCenter, _) = myCharacter is not null
                    ? ComputeCamera(snapshot, myCharacter)
                    : (WorldViewportOrigin, ShipLocalFrame.GetHullCenter(snapshot.Rooms), Vec2.Zero);
                _shipInteriorOrigin = origin;
                // Behind the periscope you are outside the ship looking at it, so it's drawn closed
                // up - and so is the station it's docked to, for the same reason.
                var fromOutside = MannedTurret(snapshot) is not null;
                // TEMP-DIAG-BEGIN (M51 - Scene phase was 183-514ms; narrowing to which renderer)
                var diagSubStopwatch = System.Diagnostics.Stopwatch.StartNew();
                // TEMP-DIAG-END
                _shipRenderer.Draw(_spriteBatch, snapshot, origin, _openBlock, totalSeconds, _effectTracker.Effects, atmosphere: _atmosphere.Particles);
                // TEMP-DIAG-BEGIN
                _diagShipMs = diagSubStopwatch.Elapsed.TotalMilliseconds;
                diagSubStopwatch.Restart();
                // TEMP-DIAG-END

                if (_showTileGridOverlay)
                    _shipRenderer.DrawTileGridOverlay(_spriteBatch, snapshot, origin);

                // Content-каталог отсеков - the click-to-place grid/ghost overlay, drawn in this
                // same ship-local frame right on top of the real geometry so it lines up exactly
                // with whatever HandleMouseClick's own confirm-click hit-test computes.
                if (_placingRoomCatalogId is { } placingId && RoomCatalog.Find(placingId) is { } placingEntry)
                {
                    var candidates = RoomPlacementPreview.FindCandidates(snapshot, placingEntry);
                    var mouseLocal = ScreenToShipLocal(new Vector2(_designMouse.X, _designMouse.Y), origin, sceneZoom);
                    var nearest = RoomPlacementPreview.NearestTo(candidates, mouseLocal);
                    _shipRenderer.DrawPlacementOverlay(_spriteBatch, snapshot, candidates, nearest, origin);
                }
                // A docked station is laid out in these same coordinates, joined to the ship by the
                // shared airlock rectangle - drawn alongside the interior rather than instead of it,
                // so there's no moment where the view swaps to "the station screen".
                if (snapshot.Voyage.DockedPointId is not null && !fromOutside)
                    _stationRenderer.Draw(_spriteBatch, snapshot, origin, _talkingToNpcId, totalSeconds);
                // Drawn after the (optional) docked station above, not as part of _shipRenderer.Draw
                // itself - see ShipRenderer.DrawCharacters' own doc comment (bug report: a crewmate
                // standing near the ship/station boundary had their floating nameplate partly
                // covered by the station's own wall art, which draws after the ship).
                _shipRenderer.DrawCharacters(_spriteBatch, snapshot, origin, _chatBubbleTracker);
                // TEMP-DIAG-BEGIN
                _diagStationMs = diagSubStopwatch.Elapsed.TotalMilliseconds;
                diagSubStopwatch.Restart();
                // TEMP-DIAG-END
                // Viewport divided by the zoom for the same reason as the camera origin: the
                // off-screen markers clamp against the screen edges, which live at design
                // coordinates on the far side of the batch's scale.
                _fieldRenderer.Draw(_spriteBatch, snapshot, origin, hullCenter,
                    WorldViewportOrigin / sceneZoom, WorldViewportSize / sceneZoom, totalSeconds, _effectTracker.Effects,
                    seenFromOutside: fromOutside, chatBubbles: _chatBubbleTracker);
                // TEMP-DIAG-BEGIN
                _diagFieldMs = diagSubStopwatch.Elapsed.TotalMilliseconds;
                // TEMP-DIAG-END
            }
        }
        _spriteBatch.End();
        // TEMP-DIAG-BEGIN
        _diagSceneMs = diagPhaseStopwatch.Elapsed.TotalMilliseconds;
        diagPhaseStopwatch.Restart();
        // TEMP-DIAG-END

        // Multiplied over the finished scene, before any HUD is drawn. Room lighting already has
        // the player's own sight folded into it (MergeSight, called earlier - a lit room stays lit
        // beyond lamp reach; the player's own lamp still works with the ship's power out), so this
        // is a single multiply with no render target switching. If room lighting didn't build for
        // some reason but sight did, sight still applies on its own rather than nothing at all.
        // When the post chain is running it multiplies the light mask in itself, in high dynamic
        // range - see ScenePost. Doing it here as well would apply the light twice and, worse,
        // would clamp it to 8 bits before the bright pass ever saw it. The blend below is only the
        // fallback for when there is no post chain at all.
        if (!postCapturing)
        {
            if (_roomLightingReady)
                _roomLighting.Composite(_spriteBatch);
            else if (maskReady)
                _visibility.Composite(_spriteBatch);
        }

        // The suit lamp, last thing into the scene. It has to land here rather than with the ship:
        // it is additive and belongs over the plating it is lighting, and it must be inside the
        // capture, because everything after this point starts switching render targets.
        if (_shipInteriorOrigin is { } lampOrigin && _client.LatestSnapshot is { } lampSnapshot &&
            lampSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId) is { } lampMe)
        {
            DrawSuitLamp(lampSnapshot, lampMe, lampOrigin, sceneTransform);
        }

        // Puts the captured frame on the backbuffer through the post effect - the first thing all
        // frame to touch it. No-op when Begin returned false. The HUD below is drawn after this
        // point on purpose, so it stays out of the pass.
        // Heat shimmer over escaping atmosphere. The steam AtmosphereParticles already spawns
        // doubles as the distortion source, so nothing new has to know where the breaches are - and
        // the ripple stops by itself the moment a breach is welded and the steam stops coming.
        if (_shipInteriorOrigin is { } shimmerOrigin && _scenePost.BeginDistortion())
        {
            var blob = _scenePost.Blob;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, transformMatrix: sceneTransform);
            foreach (var particle in _atmosphere.Particles)
            {
                if (particle.Kind != AtmosphereKind.Steam)
                    continue;

                var centre = shimmerOrigin + new Vector2((float)particle.Position.X, (float)particle.Position.Y) * ShipRenderer.PixelsPerUnit;
                // Wider than the wisp ShipRenderer draws: air bends light well past the part of it
                // you can actually see.
                var radius = particle.Size * ShipRenderer.PixelsPerUnit * (1f + particle.Progress * 1.6f) * 3f;
                _spriteBatch.Draw(blob, centre, null, Color.White * (1f - particle.Progress), 0f,
                    new Vector2(blob.Width / 2f, blob.Height / 2f), radius * 2f / blob.Width, SpriteEffects.None, 0f);
            }
            _spriteBatch.End();
            _scenePost.EndDistortion();
        }

        // True normals for the deck, stamped into their own target with the same transform the
        // scene was drawn with. Opaque blending on purpose: the alpha channel is the flag saying
        // this pixel has a real normal, and alpha blending would fade that flag out.
        // True floor normals disabled on request - with nothing drawn into the normals target the
        // composite falls back to estimating slope from luminance, exactly as before they existed.
        if (false && _shipInteriorOrigin is { } normalsOrigin && _client.LatestSnapshot is { } normalsSnapshot
            && _scenePost.BeginNormals())
        {
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, transformMatrix: sceneTransform);
            _shipRenderer.DrawFloorNormals(_spriteBatch, normalsSnapshot, normalsOrigin);
            _shipRenderer.DrawHullNormals(_spriteBatch, normalsSnapshot, normalsOrigin);
            _spriteBatch.End();
            _scenePost.EndNormals();
        }

        // Which mask the chain reads for "how lit is this pixel": the room lighting one when it
        // built, since that already has the player sight folded into it; the plain sight mask when
        // it did not; nothing at all when neither ran, which zeroes the light-driven terms.
        _scenePost.SetLightMask(_roomLightingReady ? _roomLighting.Mask : maskReady ? _visibility.Mask : null);
        var outside = _client.LatestSnapshot?.Characters
            .FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.IsOutside ?? false;
        var savedVacuumLook = outside ? ApplyVacuumPostLook() : (PostLook?)null;
        _scenePost.Present(_spriteBatch, totalSeconds);
        if (savedVacuumLook is { } restoreVacuum)
            RestorePostLook(restoreVacuum);
        // TEMP-DIAG-BEGIN
        _diagPostMs = diagPhaseStopwatch.Elapsed.TotalMilliseconds;
        diagPhaseStopwatch.Restart();
        // TEMP-DIAG-END

        // The manoeuvring exhaust goes on after the composite rather than into the scene.
        //
        // Not the reason it was once invisible - that was a plain coordinate bug, drawing field
        // coordinates as if they were ship-local, which threw the gas clean off the screen. This is
        // here on its own merits: the light mask multiplies the captured scene, and the gas leaves
        // from behind the shoulders, which is outside the lamp cone by definition - you never point
        // a helmet lamp at your own back. A mask that decides what is lit has no business dimming
        // something that is producing its own light, the same way the starfield does not.
        //
        // The cost of being out here is no bloom around it, which is why the gas is drawn bright.
        if (_shipInteriorOrigin is { } rcsOrigin && _client.LatestSnapshot is { } rcsSnapshot &&
            rcsSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId) is { } rcsMe)
            DrawRcsPlume(rcsSnapshot, rcsMe, rcsOrigin, sceneTransform, (float)gameTime.ElapsedGameTime.TotalSeconds);

        // Cosmoteer-style marching engines (direct user request) - Nozzle goes on after the
        // composite for the same reason the RCS plume just above does (ShipRenderer.
        // DrawEngineNozzles's own doc comment explains why).
        if (_shipInteriorOrigin is { } engineOrigin && _client.LatestSnapshot is { } engineSnapshot)
            _shipRenderer.DrawEngineNozzles(_spriteBatch, engineSnapshot, engineOrigin, sceneTransform, totalSeconds);

        // Crew nameplates, also after the composite (direct user request, bug report: a nameplate
        // going dark in a wall's own cast shadow) - see ShipRenderer.DrawCharacterLabels' own doc
        // comment. Covers the ship/station/EVA and boarded-enemy-hull cases alike, since all of them
        // set _shipInteriorOrigin and share the one snapshot.Characters list.
        if (_shipInteriorOrigin is { } labelOrigin && _client.LatestSnapshot is { } labelSnapshot)
            _shipRenderer.DrawCharacterLabels(_spriteBatch, labelSnapshot, labelOrigin, sceneTransform);

        _spriteBatch.Begin(transformMatrix: _renderScale);
        // Peaks at the exact midpoint of the transition (fully opaque, hiding the underlying scene
        // swap) and is 0 at both ends - the same hump a cross-dissolve needs, without ever having
        // to render both scenes at once.
        if (_navTransitionRemaining > 0f)
        {
            var progress = _navTransitionRemaining / NavTransitionDuration;
            var alpha = 1f - MathF.Abs(progress * 2f - 1f);
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight), Color.Black * alpha);
        }
        if (_client.LatestSnapshot is { } hudSnapshot)
        {
            // Station dialogue is a HUD overlay on top of the physical scene (like the panels
            // below), not a full-screen takeover - drawn whenever talking to someone; it no-ops
            // internally if _talkingToNpcId is null.
            _stationPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, StationPanelOrigin, _talkingToNpcId);

            // Content-каталог отсеков - shown while actively talking to the Shipwright OR while a
            // module is still being placed (StationBuildPanel highlights whichever tile is selected,
            // so it has to stay visible even after the player has already walked off to point at
            // their own ship - the dialogue itself may have already closed by then).
            var talkingToShipwright = _talkingToNpcId is { } npcId &&
                hudSnapshot.Station.Npcs.FirstOrDefault(n => n.Id == npcId)?.Kind == NpcKind.Shipwright;
            if (talkingToShipwright || _placingRoomCatalogId is not null)
                _stationBuildPanel.Draw(_spriteBatch, hudSnapshot, StationBuildPanelOrigin, _buildPanelCategory, _placingRoomCatalogId, _designMouse);

            _cardGamePanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, CardGamePanelOrigin);
            _frontsGamePanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, FrontsGamePanelOrigin);
            // Direct user request ("чтобы в стол можно было зайти") - the choice step only shows
            // once the local player has actually clicked the table open (Game1.Input.cs), not just
            // from standing near it.
            if (_openBlock.Kind == BlockKind.CardTable)
                _cardTableChoicePanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, CardTableChoicePanelOrigin);

            // Only one block's terminal is shown at a time, at the same HUD slot — you have to
            // actually be "in" it (game_design.md section 1) rather than seeing everything always.
            switch (_openBlock.Kind)
            {
                case BlockKind.Distribution:
                    _powerPanel.Draw(_spriteBatch, hudSnapshot.Power, hudSnapshot.SystemStates, _selectedPowerSystem, PowerPanelOrigin, totalSeconds);
                    break;
                case BlockKind.Reactor:
                    _reactorPanel.Draw(_spriteBatch, hudSnapshot.Reactor, PowerPanelOrigin, totalSeconds);
                    break;
                case BlockKind.Jukebox when hudSnapshot.Jukebox is { } jukeboxState:
                    _jukeboxPanel.Draw(_spriteBatch, jukeboxState, PowerPanelOrigin, totalSeconds);
                    break;
                case BlockKind.Battery:
                    _batteryPanel.Draw(_spriteBatch, hudSnapshot.Power, PowerPanelOrigin, totalSeconds);
                    break;
                case BlockKind.System:
                    _systemDevicePanel.Draw(_spriteBatch, _openBlock.System, hudSnapshot.Power, hudSnapshot.Shield, hudSnapshot.SystemStates, PowerPanelOrigin, totalSeconds);
                    break;
                case BlockKind.Rack:
                    var rackOffset = CurrentOpenRackOffset(hudSnapshot);
                    _rackPanel.Draw(_spriteBatch, hudSnapshot, RackPanelOrigin, rackOffset, totalSeconds);
                    if (_dragFrom is null && HoveredRackSlotIndex(hudSnapshot, rackOffset) is { } hoveredRackSlot
                        && hudSnapshot.RackSlots[rackOffset + hoveredRackSlot] is { } hoveredRackItem)
                    {
                        var rackSlotRect = RackPanel.GetSlotRect(hoveredRackSlot, RackPanelOrigin);
                        _inventoryPanel.DrawTooltip(_spriteBatch, hoveredRackItem, null, new Vector2(rackSlotRect.X, rackSlotRect.Y - 16));
                    }
                    break;
                case BlockKind.Connections when _openBlock.TargetComponentId is { } targetComponentId:
                    // Height 0 asks the panel to size itself from its pin count; X is centred here
                    // because the width is fixed and known, Y follows the standard housing so it
                    // opens in the same place as every other terminal.
                    _connectionsPanel.Draw(_spriteBatch, hudSnapshot, targetComponentId,
                        new Rectangle((DesignWidth - ConnectionsPanel.Width) / 2,
                            (int)PowerPanelOrigin.Y - DevicePanelChrome.OriginInsetY, ConnectionsPanel.Width, 0), totalSeconds);
                    break;
                case BlockKind.SuitLocker when _openBlock.TargetComponentId is { } lockerId:
                    _suitLockerPanel.Draw(_spriteBatch, hudSnapshot, lockerId, _client.PlayerId, PowerPanelOrigin);
                    break;
                // M48 follow-up - "в режиме сонара открывался виджет... задний план это сам
                // корабль, а не карта": the console used to take over the whole scene batch above
                // (like the galactic map/helm/boarding views still do); now it's just another HUD
                // overlay like every other terminal in this switch, so the ship interior/exterior
                // keeps rendering normally behind it via the scene batch's own final `else` branch.
                case BlockKind.Navigation:
                    _galaxyMapPanel.Draw(_spriteBatch, hudSnapshot, GalaxyMapPanelOrigin + _sonarPanelDragOffset, _mapZoom, _mapPanOffset, _client.PlayerId, serverTotalSeconds);
                    if (hudSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId) is { } scannerMe)
                        _scannerModeWidget.Draw(_spriteBatch, scannerMe.ScannerMode, scannerMe.ScannerCooldownRemaining, _scannerWidgetPosition);
                    break;
            }

            _combatPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, ComputeHint(hudSnapshot, _client.PlayerId), CombatPanelOrigin);
            _chatPanel.Draw(_spriteBatch, hudSnapshot.ChatLog, _chatFocused, _chatInput, ChatPanelOrigin, (float)gameTime.ElapsedGameTime.TotalSeconds);
            // Push-to-talk indicator (direct user request) - just player feedback that a mic is
            // actually recording right now, same font/scale/color convention as ChatPanel's own
            // text above. Plain text, no icon glyph - DebugFont.spritefont only covers Latin-1/
            // Cyrillic/basic punctuation (its own doc comment: a missing glyph has crashed
            // DrawString outright before), so an emoji here would just draw as garbled '?' filler.
            if (_voiceCapture.IsRecording)
            {
                var voiceLabel = _voiceCapture.IsRadio ? "РАЦИЯ" : "ГОВОРИТ";
                _spriteBatch.DrawString(_font, voiceLabel, ChatPanelOrigin + new Vector2(0, -20f),
                    _voiceCapture.IsRadio ? Color.OrangeRed : Color.LightGreen, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            }
            _playerHealthPanel.Draw(_spriteBatch, hudSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId), PlayerHealthPanelOrigin);
            _voyagePanel.Draw(_spriteBatch, hudSnapshot, VoyagePanelOrigin);

            // The "ОБУЧЕНИЕ" run's own persistent banner (World.Tutorial.cs) - null on every other
            // session, so this simply doesn't draw outside it. Centered at the very top, above
            // everything else, since it's the one thing a fresh player is actually looking for.
            if (hudSnapshot.TutorialObjective is { } tutorialObjective)
            {
                var textSize = _font.MeasureString(tutorialObjective) * 0.6f;
                var bannerRect = new Rectangle((DesignWidth - (int)textSize.X - 24) / 2, 6, (int)textSize.X + 24, (int)textSize.Y + 10);
                _spriteBatch.Draw(_pixel, bannerRect, Color.Black * 0.75f);
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, bannerRect, Color.Gold, 1);
                _spriteBatch.DrawString(_font, tutorialObjective, new Vector2(bannerRect.X + 12, bannerRect.Y + 5),
                    Color.Gold, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            }

            // Drawn here, in the HUD batch, rather than inside ShipRenderer's own scene batch -
            // that batch gets multiplied by the sight-cone/room-lighting mask right after it ends,
            // which used to hide this bar the instant the wall itself fell into a blind spot, even
            // though the flame lighting it up is coming from the player's own hands. The HUD batch
            // runs after that composite, same exemption Info/Crew get.
            if (_shipInteriorOrigin is { } wallToolOrigin)
            {
                foreach (var character in hudSnapshot.Characters)
                {
                    if (character.WallToolTargetBlockId is not { } targetId)
                        continue;
                    // Station walls report their own target id the same way the ship's do (World.
                    // WallBlocks.cs's FindAimedStationWallBlock) - checked second since a station id
                    // never collides with a ship one, same "either list, whichever matches" shape as
                    // the door bar lookup just below.
                    var block = hudSnapshot.WallBlocks.FirstOrDefault(b => b.Id == targetId)
                        ?? hudSnapshot.Station.WallBlocks.FirstOrDefault(b => b.Id == targetId)
                        ?? hudSnapshot.EnemyShip.WallBlocks.FirstOrDefault(b => b.Id == targetId);
                    var state = hudSnapshot.WallBlockStates.FirstOrDefault(s => s.Id == targetId)
                        ?? hudSnapshot.Station.WallBlockStates.FirstOrDefault(s => s.Id == targetId)
                        ?? hudSnapshot.EnemyShip.WallBlockStates.FirstOrDefault(s => s.Id == targetId);
                    if (block is not null && state is not null)
                        _shipRenderer.DrawWallToolTargetBar(_spriteBatch, block, state, wallToolOrigin);

                    // An enemy hull's own locked airlock, aimed at from inside instead of a wall
                    // panel - AirlockOuterDoor isn't a WallBlock, so it can't go through the typed
                    // wrapper above; the underlying bar only needs a position and a fraction.
                    var airlockState = hudSnapshot.EnemyShip.AirlockStates.FirstOrDefault(s => s.Id == targetId);
                    if (block is null && airlockState is not null)
                    {
                        var airlock = hudSnapshot.EnemyShip.AirlockOuterDoors.FirstOrDefault(d => d.Id == targetId);
                        if (airlock is not null)
                            _shipRenderer.DrawToolTargetBar(_spriteBatch, new Vector2((float)airlock.Position.X, (float)airlock.Position.Y),
                                airlockState.Fraction, wallToolOrigin);
                    }
                }

                // Same bar, over a door the cutter is cutting through instead of a hull block -
                // DoorToolTargetId can name either an interior Door or an AirlockOuterDoor (both
                // share Id/X/Y but not a common base type), so both lists get checked.
                foreach (var character in hudSnapshot.Characters)
                {
                    if (character.DoorToolTargetId is not { } doorTargetId)
                        continue;
                    var doorState = hudSnapshot.DoorStates.FirstOrDefault(s => s.DoorId == doorTargetId);
                    if (doorState is null)
                        continue;
                    var doorPosition = hudSnapshot.Doors.FirstOrDefault(d => d.Id == doorTargetId)?.Position
                        ?? hudSnapshot.AirlockOuterDoors.FirstOrDefault(d => d.Id == doorTargetId)?.Position
                        ?? hudSnapshot.EnemyShip.Doors.FirstOrDefault(d => d.Id == doorTargetId)?.Position;
                    if (doorPosition is { } position)
                        _shipRenderer.DrawDoorToolTargetBar(_spriteBatch, new Vector2((float)position.X, (float)position.Y), doorState, wallToolOrigin);
                }

                // Same HUD-batch exemption as the wall bar just above - shown while standing next
                // to a damaged system device, same proximity TurretInteractionRadius uses for its
                // own repair hint (ComputeHint), so the card and the hint text never disagree about
                // whether you're "at" it.
                var repairMe = hudSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
                if (repairMe is not null)
                {
                    var repairPosition = new Vec2(repairMe.X, repairMe.Y);
                    var nearbyDamaged = hudSnapshot.SystemDevices.FirstOrDefault(d =>
                        (d.Position - repairPosition).Length() < TurretInteractionRadius &&
                        (hudSnapshot.SystemStates.FirstOrDefault(s => s.DeviceId == d.Id)?.Damaged ?? false));
                    if (nearbyDamaged is not null)
                    {
                        var holdingRepairTool = HeldItemTypes(repairMe.Inventory).Contains(ItemType.Wrench) ||
                                                 HeldItemTypes(repairMe.Inventory).Contains(ItemType.Screwdriver);
                        var repairState = hudSnapshot.SystemStates.FirstOrDefault(s => s.DeviceId == nearbyDamaged.Id);
                        var cardOrigin = wallToolOrigin + new Vector2(nearbyDamaged.X, nearbyDamaged.Y) * ShipRenderer.PixelsPerUnit
                            + new Vector2(-SystemRepairPanel.PanelWidth / 2f, -SystemRepairPanel.PanelHeight - 30);
                        _systemRepairPanel.Draw(_spriteBatch, ComponentRenderer.SystemLabel(nearbyDamaged.System), holdingRepairTool,
                            repairState?.RepairProgress ?? 0f, cardOrigin);
                    }

                    // Same card, same proximity radius, for a damaged Junction box instead of a
                    // damaged SystemDevice - World.Interact.cs's E-key repair treats both the same way.
                    var nearbyDamagedJunction = hudSnapshot.Wiring.Components.FirstOrDefault(c =>
                        c.Kind == ComponentKind.Junction && (c.Position - repairPosition).Length() < TurretInteractionRadius &&
                        (hudSnapshot.JunctionStates.FirstOrDefault(s => s.DeviceId == c.Id)?.Damaged ?? false));
                    if (nearbyDamagedJunction is not null)
                    {
                        var holdingRepairTool = HeldItemTypes(repairMe.Inventory).Contains(ItemType.Wrench) ||
                                                 HeldItemTypes(repairMe.Inventory).Contains(ItemType.Screwdriver);
                        var repairState = hudSnapshot.JunctionStates.FirstOrDefault(s => s.DeviceId == nearbyDamagedJunction.Id);
                        var cardOrigin = wallToolOrigin + new Vector2(nearbyDamagedJunction.X, nearbyDamagedJunction.Y) * ShipRenderer.PixelsPerUnit
                            + new Vector2(-SystemRepairPanel.PanelWidth / 2f, -SystemRepairPanel.PanelHeight - 30);
                        _systemRepairPanel.Draw(_spriteBatch, "Щиток", holdingRepairTool,
                            repairState?.RepairProgress ?? 0f, cardOrigin);
                    }

                    // Same card again, for a destroyed door (World.Doors.cs) - jammed open by its
                    // own hit points hitting zero, repaired the same wrench/screwdriver minigame way.
                    Vec2? DoorPosition(string doorId) =>
                        hudSnapshot.Doors.FirstOrDefault(d => d.Id == doorId) is { } door ? door.Position
                        : hudSnapshot.AirlockOuterDoors.FirstOrDefault(d => d.Id == doorId) is { } outer ? outer.Position
                        : null;

                    var nearbyDestroyedDoor = hudSnapshot.DoorStates.FirstOrDefault(s =>
                        s.Destroyed && DoorPosition(s.DoorId) is { } doorPos && (doorPos - repairPosition).Length() < TurretInteractionRadius);
                    if (nearbyDestroyedDoor is not null)
                    {
                        var holdingRepairTool = HeldItemTypes(repairMe.Inventory).Contains(ItemType.Wrench) ||
                                                 HeldItemTypes(repairMe.Inventory).Contains(ItemType.Screwdriver);
                        var doorPosition = DoorPosition(nearbyDestroyedDoor.DoorId)!.Value;
                        var cardOrigin = wallToolOrigin + new Vector2((float)doorPosition.X, (float)doorPosition.Y) * ShipRenderer.PixelsPerUnit
                            + new Vector2(-SystemRepairPanel.PanelWidth / 2f, -SystemRepairPanel.PanelHeight - 30);
                        _systemRepairPanel.Draw(_spriteBatch, "Дверь", holdingRepairTool,
                            nearbyDestroyedDoor.RepairProgress, cardOrigin);
                    }
                }
            }

            // HUD batch rather than the scene batch it used to share with the system map - exempt
            // from the sight-cone/room-lighting composite above, same reasoning as InfoPanel/Crew:
            // a full-screen overlay reachable from anywhere (the M key) shouldn't go dark just
            // because the ship interior underneath happens to be sitting in a blind spot right now.
            if (_galacticMapOpen)
                _galacticMapPanel.Draw(_spriteBatch, hudSnapshot, GalaxyMapPanelOrigin, _galacticMapZoom, _galacticMapPanOffset);

            // External cameras (M46) - same HUD-batch overlay treatment as the galactic map right
            // above: a full takeover reachable from anywhere power allows it, not masked by sight/
            // lighting like the ship-interior scene batch it replaces on screen.
            if (_externalCameraMode)
            {
                // The full design canvas, not just the ship-interior viewport strip (M48 follow-up -
                // "чтобы панелька камеры использовала весь ей доступный экран") - the 3D scene this
                // mode replaces is empty here anyway, so there's no reason to leave most of the
                // screen dark. The top bar/inventory row/role box below still draw over this
                // afterward, same "persistent HUD over a full-screen view" the galactic map already is.
                var cameraArea = new Rectangle(0, 0, DesignWidth, DesignHeight);
                if (_externalCameraFullscreenIndex is { } fsIndex)
                    _externalCameraPanel.DrawFullscreen(_spriteBatch, GraphicsDevice, hudSnapshot, cameraArea, _renderScale, fsIndex, totalSeconds);
                else
                    _externalCameraPanel.DrawGrid(_spriteBatch, GraphicsDevice, hudSnapshot, cameraArea, _renderScale, totalSeconds);
            }

            DrawTopBar(_spriteBatch, hudSnapshot);
            if (_crewPanelOpen)
                _crewPanel.Draw(_spriteBatch, hudSnapshot, CrewPanelOrigin, _client.PlayerId);
            // HUD batch rather than the scene batch InfoPanel uses - it's never rotated/zoomed or
            // masked by sight/lighting regardless of camera state, with no need to mirror
            // InfoPanel's SceneZoom/TurretViewRotationDegrees/BuildVisibilityMask exemptions.
            if (_shipEditorOpen)
                _shipEditorPanel.Draw(_spriteBatch, hudSnapshot, _shipEditorSelectedComponentId, _connectionsPanel, ShipEditorPanelOrigin, totalSeconds);
            var myInventory = hudSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
            var carriedSlotCount = myInventory?.MainSlots.Count ?? 0;
            var rowOrigin = InventoryRowOrigin(carriedSlotCount);
            var hoveredToolSlot = myInventory is not null ? HoveredToolSlotIndex(myInventory, rowOrigin) : null;
            _inventoryPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, rowOrigin, EquipSlotsOrigin, hoveredToolSlot, IsBeltBagPopupShown(hudSnapshot));

            // The role/portrait box past the equip row's right edge (Barotrauma's own corner) -
            // just a glyph on a plate, the same DrawRoleGlyph CrewPanel already uses for every
            // crew row, so a role reads as the same icon everywhere it appears.
            var roleBoxRect = new Rectangle((int)RoleBoxOrigin.X, (int)RoleBoxOrigin.Y, RoleBoxSize, RoleBoxSize);
            _spriteBatch.Draw(_pixel, roleBoxRect, new Color(40, 44, 52));
            var myRole = hudSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Role;
            HudIcons.DrawRoleGlyph(_spriteBatch, _pixel, new Vector2(roleBoxRect.Center.X, roleBoxRect.Center.Y), 0.9f, Color.White, myRole);

            // Drag-drop feedback (game_design.md section 13): green over a spot the dragged item can
            // actually land on right now, a brief red flash on the spot a rejected drop just bounced
            // off of. Drawn over both grids the drag can span - the carried row and the open rack.
            if (_dragHighlightSlot is { } highlighted)
                DrawSlotHighlight(GetSlotScreenRect(highlighted, rowOrigin), Color.LimeGreen);
            if (_invalidDropSlot is { } invalid && gameTime.TotalGameTime.TotalSeconds < _invalidDropFlashUntil)
                DrawSlotHighlight(GetSlotScreenRect(invalid, rowOrigin), Color.OrangeRed);

            // Last, so the item being dragged rides over every panel it passes across.
            if (_dragFrom is { } dragged && ItemInSlot(hudSnapshot, dragged) is { } draggedItem)
                InventoryPanel.DrawDraggedItem(_spriteBatch, _pixel, _font, _designMouse, draggedItem);

            // Full item info while hovering a slot - skipped mid-drag, where "what's under the
            // cursor" means the drag, not whatever slot it happens to be passing over.
            if (_dragFrom is null && myInventory is not null && HoveredMainSlotIndex(myInventory, rowOrigin) is { } hoveredSlot
                && myInventory.MainSlots[hoveredSlot] is { } hoveredItem)
            {
                var slotRect = InventoryPanel.GetMainSlotRect(hoveredSlot, rowOrigin);
                _inventoryPanel.DrawTooltip(_spriteBatch, hoveredItem, myInventory.MainSlotTanks[hoveredSlot], new Vector2(slotRect.X, slotRect.Y - 16));
            }

            // The gunner's reticle, drawn last so nothing else ever sits over it - tracks the raw
            // cursor in this unrotated HUD batch rather than the rotated scene, so it stays glued
            // to the mouse regardless of TurretViewRotationDegrees.
            if (MannedTurret(hudSnapshot) is not null)
                TurretReticle.Draw(_spriteBatch, _pixel, new Vector2(_designMouse.X, _designMouse.Y), new Color(190, 225, 240));

            // Last of all - the one overlay that's meant to sit over literally everything else,
            // including the reticle (there's nothing to aim at while it's up).
            if (_pauseMenuOpen)
                _pauseMenuPanel.Draw(_spriteBatch, PauseMenuPanelOrigin, _designMouse);
            else if (_cheatPanelOpen)
                _cheatPanel.Draw(_spriteBatch, CheatPanelOrigin, _designMouse);
        }
        // TEMP-DIAG-BEGIN
        _diagHudMs = diagPhaseStopwatch.Elapsed.TotalMilliseconds;
        // TEMP-DIAG-END

        // TEMP-DIAG-BEGIN (M51 slowdown investigation - see _diagDisplayedFps's own comment)
        // Hidden by default (direct user request) - toggled on with F3, see _showDiagOverlay's own
        // field comment.
        if (_showDiagOverlay)
        {
            var diagServer = GameServer.Current;
            var diagServerLine = diagServer is null
                ? "Сервер: нет"
                : $"Тик {diagServer.LastTickTotalMs:0.0}мс  Шаг {diagServer.LastStepMs:0.0}мс  Снап {diagServer.LastSnapshotMs:0.0}мс";
            var diagClientLine = $"Обновл {_diagLastUpdateMs:0.0}мс  Рендер {_diagLastDrawMs:0.0}мс";
            var diagPhaseLine = $"Маска {_diagMaskMs:0.0}мс  Сцена {_diagSceneMs:0.0}мс  Пост {_diagPostMs:0.0}мс  Хад {_diagHudMs:0.0}мс";
            var diagSceneSubLine = $"Корабль {_diagShipMs:0.0}мс  Станция {_diagStationMs:0.0}мс  Поле {_diagFieldMs:0.0}мс";
            var diagGcLine = $"ГК0/с {_diagGc0PerSecond}  ГК1/с {_diagGc1PerSecond}  ГК2/с {_diagGc2PerSecond}  Выд {_diagAllocMbPerSecond:0}МБ/с";
            var diagOrbitLine = _client.LatestSnapshot is { } diagOrbitSnapshot
                ? $"Тик {diagOrbitSnapshot.Tick}  Ускр x{diagOrbitSnapshot.TimeAccelerationLevel}  X {diagOrbitSnapshot.ShipField.X:0}  Y {diagOrbitSnapshot.ShipField.Y:0}  " +
                  $"VX {diagOrbitSnapshot.ShipField.VelocityX:0}  VY {diagOrbitSnapshot.ShipField.VelocityY:0}"
                : "нет снапшота";
            // "До причала" (HelmButtonsWidget) measures against DockBerthPosition, shown raw here -
            // whether it's actually tracking live (changing frame to frame roughly like ShipField.X/Y
            // does) or frozen/stale is the first thing to rule in or out.
            var diagBerthLine = _client.LatestSnapshot is { } diagBerthSnapshot
                ? $"DockBerth X {diagBerthSnapshot.DockBerthPosition.X:0} Y {diagBerthSnapshot.DockBerthPosition.Y:0}  " +
                  $"CanDock={diagBerthSnapshot.CanDock}  Docked={diagBerthSnapshot.Voyage.DockedPointId ?? "null"}  " +
                  $"BerthPointId={diagBerthSnapshot.DockBerthPointId ?? "null"}"
                : "";
            _spriteBatch.DrawString(_font, $"Кадры/с {_diagDisplayedFps:0}  Тики {_diagDisplayedTicksPerSecond}/30\n{diagServerLine}\n{diagClientLine}\n{diagPhaseLine}\n{diagSceneSubLine}\n{diagGcLine}\n{diagOrbitLine}\n{diagBerthLine}",
                new Vector2(10, 10), Color.Yellow, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }
        // TEMP-DIAG-END

        // The hovered device's own highlight, and the cursor's shape - both direct user request
        // ("как в баротравме"), both read the SAME hover rect (Game1.Interactables.cs) so they can
        // never show one without the other. Drawn dead last, after every panel, so the highlight
        // is never painted over and the cursor sits exactly where the OS's own (now hidden) one
        // would.
        var hoveredRect = ComputeHoveredInteractable(_client.LatestSnapshot);
        if (hoveredRect is { } highlightRect)
            ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, highlightRect, Color.Gold, 2);
        GameCursor.Draw(_spriteBatch, _pixel, _designMouse.ToVector2(), hoveredRect is not null);

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        // _session stays null (see the field's own doc comment) until a ship is actually picked on
        // the select screen - closing the window before that would otherwise crash here.
        _session?.Dispose();
        _scenePost?.Dispose();
        base.UnloadContent();
    }
}
