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
    private const float TurretInteractionRadius = 1.0f; // must match World.InteractionRadius
    private const float WelderHintReachUnits = 1.7f; // must match World.WelderReachUnits

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
    // Centered directly above the role/portrait box specifically (Barotrauma's own corner has the
    // health bar riding right above the portrait, not spanning the whole equip row) - meant to
    // always be visible the same way the inventory row below it always is.
    private Vector2 PlayerHealthPanelOrigin => new(
        RoleBoxOrigin.X - (PlayerHealthPanel.BarWidth - RoleBoxSize) / 2f,
        RoleBoxOrigin.Y - PlayerHealthPanel.BarHeight - 6f);
    private static readonly Vector2 GalaxyMapPanelOrigin = new(60, 64);
    private static readonly Vector2 StationPanelOrigin = new(60, 64);
    private static readonly Vector2 HelmPanelOrigin = new(120, 100);
    private static readonly Vector2 InfoPanelOrigin = new(60, 64);
    private static readonly Vector2 ShipEditorPanelOrigin = new(60, 64);
    // Centered like PauseMenuPanel below - a 2-player minigame taking over the middle of the
    // screen, not another HUD corner panel competing for space with the rest of them.
    private static readonly Vector2 CardGamePanelOrigin =
        new((DesignWidth - CardGamePanel.PanelWidth) / 2f, (DesignHeight - CardGamePanel.PanelHeight) / 2f);
    // Centered on the design canvas rather than a fixed HUD corner - this one's a modal, not a
    // panel that shares the screen with the rest of the HUD.
    private static readonly Vector2 PauseMenuPanelOrigin =
        new((DesignWidth - PauseMenuPanel.PanelWidth) / 2f, (DesignHeight - PauseMenuPanel.PanelHeight) / 2f);
    // The 3 top-bar buttons (game_design.md's newest ask): Crew (slide-out roster), Management
    // (placeholder, does nothing yet), Info (the full InfoPanel takeover). Sized bigger than an
    // inventory slot (InventoryPanel.SlotSize=34) so the affordance reads as a different kind of
    // control, not another item slot.
    private const int TopBarButtonSize = 44;
    private const int TopBarButtonGap = 8;
    // y=34, not the corner itself - DebugOverlay's "Tick: N" text already lives at (10,10).
    private static readonly Vector2 TopBarOrigin = new(10, 34);
    private static readonly Vector2 CrewPanelOrigin = new(10, 88);
    // To the right of the radar, which is why it's expressed relative to the helm's own origin -
    // the two are one console and should move together.
    private static readonly Vector2 ShipStatusPanelOffset = new(560, 20);
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
    private DebugOverlay _debugOverlay = null!;
    private ShipRenderer _shipRenderer = null!;
    private PowerPanel _powerPanel = null!;
    private CombatPanel _combatPanel = null!;
    private PlayerHealthPanel _playerHealthPanel = null!;
    private VoyagePanel _voyagePanel = null!;
    private InventoryPanel _inventoryPanel = null!;
    private ReactorPanel _reactorPanel = null!;
    private BatteryPanel _batteryPanel = null!;
    private SystemDevicePanel _systemDevicePanel = null!;
    private GalaxyMapPanel _galaxyMapPanel = null!;
    private GalacticMapPanel _galacticMapPanel = null!;
    private StationPanel _stationPanel = null!;
    private CardGamePanel _cardGamePanel = null!;
    private HelmPanel _helmPanel = null!;
    private ShipStatusPanel _shipStatusPanel = null!;
    private FieldRenderer _fieldRenderer = null!;
    private StationRenderer _stationRenderer = null!;
    private BoardingRenderer _boardingRenderer = null!;
    private VisibilityMask _visibility = null!;
    private RoomLighting _roomLighting = null!;
    private ScenePost _scenePost = null!;
    private bool _roomLightingReady;
    // What ApplyGraphicsSettings last actually applied - the Settings screen (Game1.Settings.cs)
    // reads this to seed its staged edits when opened, and to know what "Отмена" should revert to.
    private GraphicsSettings _graphicsSettings;
    private RackPanel _rackPanel = null!;
    private ConnectionsPanel _connectionsPanel = null!;
    private SuitLockerPanel _suitLockerPanel = null!;
    private SystemRepairPanel _systemRepairPanel = null!;
    private PauseMenuPanel _pauseMenuPanel = null!;
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
    // Set by the pause menu's "ГЛАВНОЕ МЕНЮ" click (Game1.Input.cs), read and cleared once at the
    // top of the next Update - see that check's own comment for why this can't just call
    // ReturnToMainMenu() directly from inside the click handler.
    private bool _pendingReturnToMainMenu;
    // Edge-triggered hull purchase, cleared the frame after it's sent - HandleMouseClick's return
    // tuple is already at its practical limit, so this one rides as a field instead.
    private ShipKind? _pendingShipPurchase;
    private QuestKind? _pendingQuestKind; // same pattern, for the Administrator's job board
    private bool _pendingDock; // and for the helm's "Стыковка" button
    private string? _pendingHireCandidateId; // and for the Recruiter's board
    private PinRef? _pendingPinInteract; // wire-laying (World.Wiring.cs), M19-M23
    private Vec2? _pendingWireBendAt; // LMB click mid-lay that missed every pin - fixes a bend there instead
    private string? _pendingComponentMountInteractId; // install/uninstall/relay-operate a mount
    private string? _pendingPickupDroppedItemId; // click-to-pick-up (World.Mining.cs), any context
    private SlotRef? _pendingDropItemFrom; // drag ended over empty space (World.Storage.cs)
    private bool _pendingAbandonQuest; // Administrator's action button when the job can't be turned in here
    private string? _pendingWarpToSystemId; // clicked a system on GalaxyMapPanel's own list (World.StarSystems.cs)
    private Vector2? _pendingTravelToPosition; // clicked empty map background - a free-form destination (World.Voyage.cs)
    private CrewRole? _pendingSetOwnRoleTo; // clicked a role icon on the crew panel's own row
    private bool _pendingClearOwnRole; // clicked the same icon a second time, or the "none" option
    private PlayingCard? _pendingPlayCard; // clicked a card in CardGamePanel - own hand or a defend/перевод play
    private bool _pendingCardGameTake; // CardGamePanel's "Взять" button
    private bool _pendingCardGameEndRound; // CardGamePanel's "Бито" button
    // The reactor's 3 physical levers (ShipRenderer.GetReactorLeverRect) - edge-triggered like
    // the rest of the _pending* fields above, cleared/sent once per click.
    private bool _pendingToggleLights;
    private bool _pendingToggleReactorEmergency;
    private bool _pendingToggleDoorsLocked;
    // The galaxy map's own camera - purely a client view of server-authoritative positions, so it
    // lives here rather than in any snapshot. Zoom via scroll wheel, pan via right-drag; both only
    // read while the navigation console is actually open.
    private float _mapZoom = 1f;
    private Vector2 _mapPanOffset = Vector2.Zero;
    private Point? _mapPanLastMouse;
    private int _prevScrollWheelValue;
    // The galactic map's own camera - separate from the system map's above, since the two views
    // use completely different coordinate spaces/scales and are never open at once, but a shared
    // zoom/pan would still leak confusingly from one into the other.
    private float _galacticMapZoom = 1f;
    private Vector2 _galacticMapPanOffset = Vector2.Zero;
    private Point? _galacticMapPanLastMouse;
    private readonly EffectTracker _effectTracker = new();
    private readonly AtmosphereField _atmosphere = new();
    private WorldSnapshot? _previousSnapshot;
    // The meme sound effect, whoever's axe just finished off a door - null if the content build
    // couldn't produce it (Shaders.TryLoad's own reasoning: a missing/failed asset build is worth
    // silently skipping the effect, not crashing the whole game over).
    private SoundEffect? _doorBreakSound;
    // How many overlapping copies PlayDoorBreakSoundIfAnyDoorJustBroke fires per door - the only
    // way to push the meme louder than volume 1f's hard ceiling (see that method's own comment).
    private const int DoorBreakSoundLayers = 3;

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
    private Point _designMouse; // cursor in design pixels, refreshed once per Update

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
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
        _debugOverlay = new DebugOverlay(_font);
        _shipRenderer = new ShipRenderer(GraphicsDevice, _font,
            new Rectangle((int)WorldViewportOrigin.X, (int)WorldViewportOrigin.Y, (int)WorldViewportSize.X, (int)WorldViewportSize.Y));
        _powerPanel = new PowerPanel(GraphicsDevice, _font);
        _combatPanel = new CombatPanel(GraphicsDevice, _font);
        _playerHealthPanel = new PlayerHealthPanel(GraphicsDevice, _font);
        _voyagePanel = new VoyagePanel(_font);
        _inventoryPanel = new InventoryPanel(GraphicsDevice, _font);
        _reactorPanel = new ReactorPanel(GraphicsDevice, _font);
        _batteryPanel = new BatteryPanel(GraphicsDevice, _font);
        _systemDevicePanel = new SystemDevicePanel(GraphicsDevice, _font);
        _galaxyMapPanel = new GalaxyMapPanel(GraphicsDevice, _font, new Rectangle(0, 0, DesignWidth, DesignHeight));
        _galacticMapPanel = new GalacticMapPanel(GraphicsDevice, _font);
        _stationPanel = new StationPanel(_font);
        _cardGamePanel = new CardGamePanel(GraphicsDevice, _font);
        _helmPanel = new HelmPanel(GraphicsDevice, _font);
        _shipStatusPanel = new ShipStatusPanel(GraphicsDevice, _font);
        _fieldRenderer = new FieldRenderer(GraphicsDevice, _font);
        _stationRenderer = new StationRenderer(_shipRenderer, GraphicsDevice, _font);
        _boardingRenderer = new BoardingRenderer(_shipRenderer, GraphicsDevice, _font);
        _visibility = new VisibilityMask(GraphicsDevice);
        // Per-pixel lamp shader disabled on request - falls back to the BasicEffect vertex-colour
        // path. Re-enable by passing Shaders.TryLoad(Content, "Shaders/Light") again.
        _roomLighting = new RoomLighting(GraphicsDevice);
        // Null when the content build hasn't produced the effect - ScenePost then reports
        // itself unavailable and Draw keeps its original straight-to-backbuffer path.
        _scenePost = new ScenePost(GraphicsDevice, Shaders.TryLoad(Content, "Shaders/Post"));
        _rackPanel = new RackPanel(GraphicsDevice, _font);
        _connectionsPanel = new ConnectionsPanel(GraphicsDevice, _font);
        _suitLockerPanel = new SuitLockerPanel(GraphicsDevice, _font);
        _systemRepairPanel = new SystemRepairPanel(GraphicsDevice, _font);
        _pauseMenuPanel = new PauseMenuPanel(GraphicsDevice, _font);
        _crewPanel = new CrewPanel(GraphicsDevice, _font);
        _infoPanel = new InfoPanel(GraphicsDevice, _font);
        _shipEditorPanel = new ShipEditorPanel(GraphicsDevice, _font);
        _existingSave = SaveStore.Load();
        _sounds = new GameSounds(Content);
        try { _doorBreakSound = Content.Load<SoundEffect>("Sounds/DoorBreak"); }
        catch { _doorBreakSound = null; } // same "missing content build shouldn't crash the game" reasoning as Shaders.TryLoad
        // The one raster texture asset in an otherwise fully-procedural game (ItemIcons.cs draws
        // every other icon from flat primitives) - same defensive load as the sound above, so an
        // unbuilt/missing .xnb falls back to the old procedural DrawScrewdriver instead of crashing.
        try { ItemIcons.SetScrewdriverTexture(Content.Load<Texture2D>("Textures/Screwdriver")); }
        catch { /* ItemIcons.Draw falls back to the procedural silhouette when this is null */ }
        // Overrides the two volume-knob/window lines above with whatever the player last saved on
        // the Settings screen (Game1.Settings.cs) - defaults (WindowMode.Borderless, VSync on,
        // full volume, no particle cap change) exactly match the behavior above, so a machine that
        // never opens Settings sees no change at all.
        ApplyGraphicsSettings(PlayerSettingsStore.LoadGraphicsSettings());
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
        if (_scenePost is not null)
        {
            _scenePost.BloomStrength = settings.BloomStrength;
            _scenePost.WideBloomStrength = settings.BloomStrength * 0.55f;
        }
        AtmosphereField.MaxParticles = Math.Max(0, settings.MaxParticles);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

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

        _designMouse = ToDesignSpace(Mouse.GetState().Position);

        if (!_sessionStarted)
        {
            HandleMenu(keyboard);
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
        if (escapePressed)
        {
            if (_pauseMenuOpen)
            {
                _pauseMenuOpen = false;
            }
            else if (_openBlock.Kind != BlockKind.None || _crewPanelOpen || _infoPanelOpen || _shipEditorOpen
                     || _galacticMapOpen || _talkingToNpcId is not null || isManningTurret || isAtHelm)
            {
                _openBlock = ClickTarget.None;
                _crewPanelOpen = false;
                _infoPanelOpen = false;
                _shipEditorOpen = false;
                _galacticMapOpen = false;
                _talkingToNpcId = null;
                escapeSendsInteract = isManningTurret || isAtHelm;
            }
            else
            {
                _pauseMenuOpen = true;
            }
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

        var interactDown = keyboard.IsKeyDown(Keys.E);
        var spaceDown = keyboard.IsKeyDown(Keys.Space);
        var interactPressed = (interactDown && !_prevInteractDown) || escapeSendsInteract;
        var spacePressed = spaceDown && !_prevFireDown;
        _prevInteractDown = interactDown;
        _prevFireDown = spaceDown;

        // Space means something different outside (push off toward the cursor) than manning a
        // turret (fire) - never both at once, since turrets are strictly indoors.
        var firePressed = !isOutside && spacePressed;
        var pushOffPressed = isOutside && spacePressed;

        var move = (isManningTurret || isAtHelm) ? Vec2.Zero : ReadMoveInput(keyboard);
        // The barrel traverses toward wherever the cursor is; A/D still nudge it for anyone who
        // wants the keyboard. Either way it's a rate, not a snap - the gun swings at its own
        // traverse speed (World.Combat.cs), so leading a moving target is a skill.
        var keyboardAim = isManningTurret ? ReadAimDirection(keyboard) : 0f;
        var aimDirection = keyboardAim != 0f || !isManningTurret ? keyboardAim : ReadTurretAimTowardCursor();
        var mouse = Mouse.GetState();

        // Galaxy map camera: right-drag to pan, scroll wheel to zoom - both harmless to read even
        // when the map isn't open (they just accumulate into fields nothing else looks at then).
        var mapOpen = _openBlock.Kind == BlockKind.Navigation;
        if (mapOpen && mouse.RightButton == ButtonState.Pressed)
        {
            if (_mapPanLastMouse is { } lastMouse)
                _mapPanOffset += new Vector2(mouse.Position.X - lastMouse.X, mouse.Position.Y - lastMouse.Y);
            _mapPanLastMouse = mouse.Position;
        }
        else
        {
            _mapPanLastMouse = null;
        }
        var scrollDelta = mouse.ScrollWheelValue - _prevScrollWheelValue;
        _prevScrollWheelValue = mouse.ScrollWheelValue;
        if (mapOpen && scrollDelta != 0)
            _mapZoom = Math.Clamp(_mapZoom * MathF.Pow(1.1f, scrollDelta / 120f), 0.3f, 3f);

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
        if (panelDragTookIt)
        {
            _prevLeftMouseButton = mouse.LeftButton;
            _prevDragButton = mouse.LeftButton;
        }
        var (moveItemFrom, moveItemTo, dragTookTheClick) = panelDragTookIt
            ? (null, null, true)
            : UpdateItemDrag(mouse, gameTime.TotalGameTime.TotalSeconds);
        if (dragTookTheClick)
            _prevLeftMouseButton = mouse.LeftButton; // keep HandleMouseClick's own edge detection in step
        var (toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, helmStabilizePressed, doorToggleId) =
            dragTookTheClick
                ? (-1, -1, (string?)null, (ItemType?)null, -1, false, false, (ShipUpgradeTrack?)null, false, (string?)null)
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
        if (helmStabilizePressed || (isAtHelm && keyboard.IsKeyDown(Keys.S)))
            _helmStabilizeLatched = true;
        var (helmThrottle, helmTurn) = isAtHelm ? ReadHelmInput(keyboard) : (0f, 0f);
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
            move = ShipLocalFrame.ToWorldDirection(move, rotation);
            pushOffDirection = ShipLocalFrame.ToWorldDirection(pushOffDirection, rotation);
            lookDirection = ShipLocalFrame.ToWorldDirection(lookDirection, rotation);
        }

        var shipPurchase = _pendingShipPurchase;
        var questKind = _pendingQuestKind;
        var dockPressed = _pendingDock;
        var hireCandidateId = _pendingHireCandidateId;
        _pendingShipPurchase = null; // edge-triggered: sent exactly once per click
        _pendingQuestKind = null;
        _pendingDock = false;
        _pendingHireCandidateId = null;

        var tankAttach = _pendingTankAttach;
        var tankDetach = _pendingTankDetach;
        _pendingTankAttach = null;
        _pendingTankDetach = null;

        var pinInteract = _pendingPinInteract;
        var wireBendAt = _pendingWireBendAt;
        var componentMountInteractId = _pendingComponentMountInteractId;
        _pendingPinInteract = null;
        _pendingWireBendAt = null;
        _pendingComponentMountInteractId = null;

        var pickupDroppedItemId = _pendingPickupDroppedItemId;
        var dropItemFrom = _pendingDropItemFrom;
        _pendingPickupDroppedItemId = null;
        _pendingDropItemFrom = null;

        var abandonQuestPressed = _pendingAbandonQuest;
        _pendingAbandonQuest = false;

        var warpToSystemId = _pendingWarpToSystemId;
        _pendingWarpToSystemId = null;

        var travelToPosition = _pendingTravelToPosition;
        _pendingTravelToPosition = null;

        var setOwnRoleTo = _pendingSetOwnRoleTo;
        var clearOwnRolePressed = _pendingClearOwnRole;
        _pendingSetOwnRoleTo = null;
        _pendingClearOwnRole = false;

        var playCard = _pendingPlayCard;
        var cardGameTakePressed = _pendingCardGameTake;
        var cardGameEndRoundPressed = _pendingCardGameEndRound;
        _pendingPlayCard = null;
        _pendingCardGameTake = false;
        _pendingCardGameEndRound = false;

        var toggleLightsPressed = _pendingToggleLights;
        var toggleReactorEmergencyPressed = _pendingToggleReactorEmergency;
        var toggleDoorsLockedPressed = _pendingToggleDoorsLocked;
        _pendingToggleLights = false;
        _pendingToggleReactorEmergency = false;
        _pendingToggleDoorsLocked = false;

        // Right-click backs out one step of a pending wire-lay without walking back to its start pin
        // - the last fixed bend if there is one, the whole anchor otherwise (World.Wiring.cs's
        // HandleWireLayCancel). Edge-triggered now (unlike before this could pop multiple bends in
        // the time it takes to release the button), separate from the map panels' own continuous
        // RMB-drag checks.
        var wireLayCancelPressed = mouse.RightButton == ButtonState.Pressed && _prevRightMouseButton == ButtonState.Released &&
            myCharacter?.LayingWireFromPin is not null;
        _prevRightMouseButton = mouse.RightButton;

        // Barotrauma's rule: the held tool works on the left button, aimed at the cursor. Held, not
        // clicked - the flame burns while the button is down (World.Cutting.cs) - and suppressed
        // while a drag is in flight so grabbing an item never lights the torch.
        var cutHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingCutter();
        var weldHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingWelder();
        var axeSwingHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingAxe();

        _client.SendInput(move, powerSystemIndexToSend, powerDirection, interactPressed, aimDirection, firePressed, toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, helmThrottle, helmTurn, stabilizeEngaged, doorToggleId, pushOffPressed, pushOffDirection.X, pushOffDirection.Y, shipPurchase, questKind, dockPressed, moveItemFrom, moveItemTo, lookDirection.X, lookDirection.Y,
            tankAttach?.From, tankAttach?.To, tankDetach, cutHeld, hireCandidateId, weldHeld, pinInteract, wireLayCancelPressed, null, componentMountInteractId, dropItemFrom, pickupDroppedItemId, abandonQuestPressed, warpToSystemId,
            _nickname, setOwnRoleTo, clearOwnRolePressed, playCard?.Rank, playCard?.Suit, cardGameTakePressed, cardGameEndRoundPressed,
            _client.LatestSnapshot?.ServerTimestampMs ?? 0, travelToPosition?.X, travelToPosition?.Y, wireBendAt?.X, wireBendAt?.Y,
            toggleLightsPressed, toggleReactorEmergencyPressed, toggleDoorsLockedPressed, axeSwingHeld);
        _client.PollSnapshots();
        CloseBlockIfWalkedAway(_client.LatestSnapshot);
        UpdateCameraLookOffset(_client.LatestSnapshot, (float)gameTime.ElapsedGameTime.TotalSeconds);

        _effectTracker.Step((float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_client.LatestSnapshot is { } latestForEffects)
        {
            _effectTracker.Detect(_previousSnapshot, latestForEffects);
            PlayDoorBreakSoundIfAnyDoorJustBroke(_previousSnapshot, latestForEffects);
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
    private const float PeriscopeViewLead = 6f;
    // Half scale = twice the reach. A gunner has to see a raider holding station 22 units out and
    // the shell crossing the gap to it; at the interior's own scale that all happens off-screen.
    private const float TurretViewZoom = 0.5f;

    // Barotrauma-style cursor lookahead: the camera doesn't center strictly on the character while
    // walking around - it eases partway toward wherever the mouse is pointing, so you see a bit
    // more of what's ahead/around a corner without losing sight of yourself. Fraction of the
    // distance to the cursor, clamped to a max offset so flinging the mouse to a screen edge
    // doesn't pull the camera arbitrarily far away; smoothed over time so a sudden mouse jump pans
    // there rather than snapping.
    private const float CameraLookAheadFactor = 0.25f;
    private const float CameraLookAheadMaxDistance = 3.5f; // ship-local units
    private const float CameraLookAheadSmoothingPerSecond = 8f;
    // Manning a turret exaggerates the same effect (bigger factor, further cap): the periscope
    // view is already zoomed out (TurretViewZoom) to show more of the field, so the cursor panning
    // it further toward whatever's at the edge of that field reads as looking where you're about
    // to shoot, the way swinging a real periscope would.
    private const float TurretLookAheadFactor = 0.5f;
    private const float TurretLookAheadMaxDistance = 10f;
    private Vec2 _cameraLookOffset = Vec2.Zero;

    // Applied to the whole scene batch, so one number moves the camera, the world and the hit
    // tests together instead of each renderer growing a scale parameter.
    private float SceneZoom(WorldSnapshot snapshot) =>
        MannedTurret(snapshot) is not null && _openBlock.Kind is not BlockKind.Navigation && !_infoPanelOpen
            ? TurretViewZoom
            : 1f;

    internal static Rectangle GetTopBarButtonRect(int index) =>
        new((int)TopBarOrigin.X + index * (TopBarButtonSize + TopBarButtonGap), (int)TopBarOrigin.Y, TopBarButtonSize, TopBarButtonSize);

    private static Vector2 RectCenter(Rectangle rect) => new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

    private static readonly Color TopBarPlate = new(26, 27, 32);
    private static readonly Color TopBarGold = new(214, 178, 112);

    private void DrawTopBar(SpriteBatch spriteBatch)
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

    private (Turret Turret, TurretState State)? MannedTurret(WorldSnapshot snapshot)
    {
        var state = snapshot.TurretStates.FirstOrDefault(t => t.MannedByPlayerId == _client.PlayerId);
        if (state is null)
            return null;
        var turret = snapshot.Turrets.FirstOrDefault(t => t.Id == state.Id);
        return turret is null ? null : (turret, state);
    }

    // Degrees to spin the whole scene batch by while manning a turret, so the barrel's own live
    // facing (TurretMount.FireDegrees(AimDegrees), ship-local - outward normal plus however far
    // it's currently traversed) reads as screen-up. This is what makes the view a real gun-cam:
    // swinging the turret pans the whole scene the way looking down a swiveling barrel would,
    // rather than the view staying pinned to the mount's fixed outward side. 0 everywhere else -
    // the ship interior/field view is never rotated except behind a periscope.
    private float TurretViewRotationDegrees(WorldSnapshot snapshot)
    {
        if (MannedTurret(snapshot) is not { } manned || _openBlock.Kind is BlockKind.Navigation || _infoPanelOpen)
            return 0f;
        var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
        return -90f - mount.FireDegrees(manned.State.AimDegrees);
    }

    private (Vector2 Origin, Vec2 HullCenter, Vec2 Anchor) ComputeCamera(WorldSnapshot snapshot, CharacterState me)
    {
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        Vec2 anchorLocal;
        if (MannedTurret(snapshot) is { } manned)
        {
            var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
            // Along the live aim direction, not the mount's fixed outward normal - the camera
            // sits out past the muzzle looking whichever way the barrel is actually pointed right
            // now, the same "camera near the barrel" TurretViewRotationDegrees rotates the view to
            // match.
            anchorLocal = mount.Position + mount.FireDirection(manned.State.AimDegrees) * PeriscopeViewLead;
        }
        else
        {
            anchorLocal = me.IsOutside
                ? ShipLocalFrame.ToLocal(new Vec2(me.X, me.Y), snapshot.ShipField, hullCenter)
                : new Vec2(me.X, me.Y);
        }
        // _cameraLookOffset (Barotrauma-style cursor pan) only ever shifts where the camera itself
        // centers on screen - never the returned Anchor, which BuildVisibilityMask uses as the
        // sight cone's true apex. Baking the pan into Anchor too would drag the cone off of the
        // character's real position the moment the camera panned away from them.
        var cameraAnchor = anchorLocal + _cameraLookOffset;
        // Divided by the zoom because the scene batch scales everything drawn at this origin: the
        // anchor has to land on the middle of the screen *after* that scaling, not before it.
        var screenCenter = (WorldViewportOrigin + WorldViewportSize / 2f) / SceneZoom(snapshot);
        var origin = screenCenter - new Vector2(cameraAnchor.X, cameraAnchor.Y) * ShipRenderer.PixelsPerUnit;
        return (origin, hullCenter, anchorLocal);
    }

    // "ТОПОР ГОШИ ДЛЯ ЛОМАНИЯ ДВЕРЕЙ" meme payoff - fires for every crew member's client the
    // instant any door's own DoorState flips into Destroyed (World.Doors.cs's ChopDoor reaching 0
    // HP), the same snapshot-diff detection EffectTracker already uses for welds/breaches/kills,
    // just triggering a sound instead of a rendered effect. Whole-crew broadcast is deliberate -
    // the shared DoorStates list means whoever's watching hears it too, not just whoever swung.
    private void PlayDoorBreakSoundIfAnyDoorJustBroke(WorldSnapshot? previous, WorldSnapshot current)
    {
        if (previous is null || _doorBreakSound is null)
            return;

        foreach (var state in current.DoorStates)
        {
            var before = previous.DoorStates.FirstOrDefault(s => s.DoorId == state.DoorId);
            if (before is { Destroyed: false } && state.Destroyed)
            {
                // Volume 1f is already the hard ceiling both XNA/MonoGame volume knobs allow (this
                // instance's own Volume and the global SoundEffect.MasterVolume set in LoadContent) -
                // software can't ask a mixer for more than "full scale". The one lever actually left
                // is firing several overlapping instances of the same clip: their waveforms sum in
                // the audio engine before the OS output stage, so 3 copies genuinely hit louder (and
                // rougher/more distorted right at the ceiling) than one - not a fake number, real
                // summed amplitude. AS LOUD AS POSSIBLE.
                for (var i = 0; i < DoorBreakSoundLayers; i++)
                    _doorBreakSound.Play(volume: 1f, pitch: 0f, pan: 0f);
                return; // one meme per tick is plenty, even if several doors somehow broke at once
            }
        }
    }

    // Recomputes the target lookahead from this frame's fresh mouse/snapshot and eases the smoothed
    // offset toward it - called once per Update (not from inside ComputeCamera itself, which runs
    // several times a frame for hit-testing and would otherwise re-blend that many times over).
    private void UpdateCameraLookOffset(WorldSnapshot? snapshot, float deltaSeconds)
    {
        var me = snapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        Vec2 target;
        if (snapshot is null || me is null || me.IsAtHelm || me.OnStation || me.OnEnemyShip ||
            _openBlock.Kind == BlockKind.Navigation || _infoPanelOpen)
        {
            target = Vec2.Zero; // eases back to centered rather than freezing wherever it was
        }
        else if (MannedTurret(snapshot) is { } manned)
        {
            var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
            var baseAnchor = mount.Position + mount.FireDirection(manned.State.AimDegrees) * PeriscopeViewLead;
            target = CursorLookAheadFrom(snapshot, baseAnchor, TurretLookAheadFactor, TurretLookAheadMaxDistance);
        }
        else
        {
            var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
            var baseAnchor = me.IsOutside
                ? ShipLocalFrame.ToLocal(new Vec2(me.X, me.Y), snapshot.ShipField, hullCenter)
                : new Vec2(me.X, me.Y);
            target = CursorLookAheadFrom(snapshot, baseAnchor, CameraLookAheadFactor, CameraLookAheadMaxDistance);
        }

        var blend = MathHelper.Clamp(deltaSeconds * CameraLookAheadSmoothingPerSecond, 0f, 1f);
        _cameraLookOffset += (target - _cameraLookOffset) * blend;
    }

    // Where the cursor sits relative to baseAnchor, converted out of screen space through that
    // anchor's own (not-yet-offset) camera - so the same formula works whether baseAnchor is a
    // walking character or a turret's barrel-lead point, each with its own zoom/scale already
    // folded in via SceneZoom.
    private Vec2 CursorLookAheadFrom(WorldSnapshot snapshot, Vec2 baseAnchor, float factor, float maxDistance)
    {
        var zoom = SceneZoom(snapshot);
        var screenCenter = (WorldViewportOrigin + WorldViewportSize / 2f) / zoom;
        var baseOrigin = screenCenter - new Vector2(baseAnchor.X, baseAnchor.Y) * ShipRenderer.PixelsPerUnit;
        var mouseLocal = (new Vector2(_designMouse.X, _designMouse.Y) / zoom - baseOrigin) / ShipRenderer.PixelsPerUnit;

        var toCursor = new Vec2(mouseLocal.X, mouseLocal.Y) - baseAnchor;
        var lookAhead = toCursor * factor;
        var length = lookAhead.Length();
        return length > maxDistance ? lookAhead * (maxDistance / length) : lookAhead;
    }

    // The station never moves or rotates (Station.cs), so its own camera is simpler than the
    // ship's: the station's room-local coordinates already are the following camera's frame,
    // no ShipLocalFrame folding needed.
    private static Vector2 ComputeStationCamera(CharacterState me)
    {
        var screenCenter = WorldViewportOrigin + WorldViewportSize / 2f;
        return screenCenter - new Vector2(me.X, me.Y) * ShipRenderer.PixelsPerUnit;
    }

    // Line of sight for whichever physical space the player is standing in. The occluders are that
    // space's own walls with its currently-open doorways cut out, so sight carries through an open
    // door into the next compartment and stops dead at everything else. A suit helmet keeps the
    // narrow forward cone it always had (game_design.md section 2); unsuited the light is all-round
    // but still bounded, so a corridor reads as a corridor rather than the whole deck plan.
    // Returns false for the views that replace the scene entirely (map/wiring/helm) - nothing to
    // mask there. Also builds the room-lighting mask (_roomLightingReady) alongside the sight mask,
    // over the same walls/origin - the two share every input except what they do with it.
    private bool BuildVisibilityMask(WorldSnapshot snapshot, float totalSeconds)
    {
        _roomLightingReady = false;
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        // Info takes over the viewport the same way the galaxy map/helm do - a HUD screen, not
        // something seen through the character's own eyes, so it reads the same regardless of
        // where they're standing or how dark the room is (matches Navigation/IsAtHelm above).
        if (me is null || me.IsAtHelm || _openBlock.Kind is BlockKind.Navigation || _infoPanelOpen)
            return false;

        // A gunner at a periscope is looking through the hull, not standing in a dark corridor:
        // sight goes wide open the moment they man a turret, same as the helm above, because you
        // cannot aim at something you cannot see.
        if (snapshot.TurretStates.Any(t => t.MannedByPlayerId == _client.PlayerId))
            return false;

        var gaps = new List<SightGap>();
        List<WallSegment> walls;
        Vector2 origin;
        Vector2 eye;
        List<PointLight> lights;
        Color floor;

        if (me.OnEnemyShip)
        {
            foreach (var door in snapshot.EnemyShipDoors)
                gaps.Add(Occluders.ToGap(door));
            gaps.Add(Occluders.ToGap(snapshot.EnemyShipBoardingHatch));
            walls = Occluders.Build(snapshot.EnemyShipRooms, gaps);
            origin = ComputeStationCamera(me);
            eye = new Vector2(me.X, me.Y);
            // A boarded ship is a hostile hull running on its own damaged grid, not the player's -
            // dim, reddish, and flickering rather than tied to the player's own power state.
            lights = BuildEnemyShipLights(snapshot.EnemyShipRooms, totalSeconds);
            floor = EnemyShipFloor;
        }
        else
        {
            foreach (var door in snapshot.Doors)
                if (snapshot.DoorStates.FirstOrDefault(s => s.DoorId == door.Id)?.IsOpen ?? true)
                    gaps.Add(Occluders.ToGap(door));
            foreach (var outerDoor in snapshot.AirlockOuterDoors)
                if (snapshot.DoorStates.FirstOrDefault(s => s.DoorId == outerDoor.Id)?.IsOpen ?? false)
                    gaps.Add(Occluders.ToGap(outerDoor));
            // A cockpit window is glass, not plating - sight carries through it into open space
            // exactly like an open door, even though (unlike a door) nothing can walk through it.
            foreach (var pane in CockpitWindows.Panes(snapshot.Rooms))
                gaps.Add(new SightGap(pane.Left, pane.Top, pane.Right, pane.Bottom));

            // While docked the station's compartments are part of the same layout, in the same
            // coordinates - its walls block the view exactly like the ship's own.
            var rooms = snapshot.Rooms;
            var docked = snapshot.Voyage.Phase == VoyagePhase.Station;
            if (docked)
            {
                foreach (var door in snapshot.Station.Doors)
                    gaps.Add(Occluders.ToGap(door));
                rooms = snapshot.Rooms.Concat(snapshot.Station.Rooms).ToList();
            }
            walls = Occluders.Build(rooms, gaps);
            // Outside the hull the camera folds the player's world position back into the ship's
            // own frame, and so must the eye - otherwise the mask would sit where the ship isn't.
            var camera = ComputeCamera(snapshot, me);
            origin = camera.Origin;
            eye = new Vector2(camera.Anchor.X, camera.Anchor.Y);

            var mood = ComputeShipPowerMood(snapshot);
            lights = BuildShipRoomLights(snapshot.Rooms, mood.PowerFraction, snapshot.Power, totalSeconds);
            // A docked station has its own external power - always lit regardless of what shape the
            // player's own ship's grid is in.
            if (docked)
                AddStationLights(lights, snapshot.Station.Rooms);
            floor = mood.Floor;
        }

        var radius = me.WearingSuit ? SuitVisionRadius : OpenVisionRadius;
        var halfAngle = me.WearingSuit ? SuitVisionHalfAngleDegrees : 180f;
        // Facing is stored in whatever frame the character moves in - field coordinates while
        // outside - but the mask is built in the ship's frame, same as the camera.
        var facing = new Vec2(me.FacingX, me.FacingY);
        if (me.IsOutside)
            facing = ShipLocalFrame.ToLocalDirection(facing, snapshot.ShipField.RotationDegrees);
        var ambient = me.WearingSuit ? SuitAmbientRadius : 0f;
        var sightReady = _visibility.Build(walls, eye, new Vector2(facing.X, facing.Y), radius, halfAngle, ambient, origin, _renderScale);
        // The reactor's light lever (World.cs) kills the room lighting overlay ship-wide - the
        // sight-only fallback right below already exists for exactly this ("nothing built this
        // frame"), so flipping the lever just means everything beyond the player's own lamp goes dark.
        _roomLightingReady = snapshot.ReactorLevers.LightsOn && _roomLighting.Build(walls, lights, floor, origin, _renderScale);
        // Has to happen here, before the backbuffer is touched - see MergeSight's own comment.
        if (_roomLightingReady && sightReady)
            _roomLighting.MergeSight(_spriteBatch, _visibility);
        return sightReady;
    }

    // Never above ~92% brightness even at full power - room art is already painted as if lit, so
    // this only has to darken things down from there, never brighten past the original.
    private static readonly Color PoweredFloor = new(232, 236, 244);
    // Dark and red rather than plain black: an unpowered room still has to read as a place (and as
    // an emergency, not a void) once the player's own suit lamp picks it out.
    private static readonly Color UnpoweredFloor = new(46, 16, 14);
    private static readonly Color EnemyShipFloor = new(22, 14, 16);

    private readonly record struct ShipPowerMood(float PowerFraction, Color Floor);

    // How lit the ship's own lamps/scanner/airlocks are (the "Secondary" power slider,
    // game_design.md section 1) - the slider's own allocation share of the reactor's rated output,
    // scaled down further if the reactor isn't actually delivering that much (low fuel, damage) or
    // the Secondary system itself is damaged (PowerGrid always zeroes a damaged system's output).
    private static ShipPowerMood ComputeShipPowerMood(WorldSnapshot snapshot)
    {
        var damaged = snapshot.SystemStates.FirstOrDefault(s => s.System == PowerSystemId.Secondary)?.Damaged ?? false;
        var maxOutput = snapshot.Power.ReactorMaxOutput;
        var allocFraction = maxOutput > 0f && snapshot.Power.Allocated.TryGetValue(PowerSystemId.Secondary, out var allocated)
            ? MathHelper.Clamp(allocated / maxOutput, 0f, 1f)
            : 0f;
        var outputFraction = maxOutput > 0f ? MathHelper.Clamp(snapshot.Power.ReactorOutput / maxOutput, 0f, 1f) : 0f;
        var fraction = damaged ? 0f : allocFraction * outputFraction;
        return new ShipPowerMood(fraction, Color.Lerp(UnpoweredFloor, PoweredFloor, fraction));
    }

    // One lamp per compartment, tinted with the same department colour RoomDecor paints the floor
    // with, plus the reactor's own glow (present even with the lights off, as long as it's actually
    // producing power) - flickering once its fuel runs critically low.
    private static List<PointLight> BuildShipRoomLights(IReadOnlyList<Room> rooms, float powerFraction, PowerState power, float totalSeconds)
    {
        var lights = new List<PointLight>(rooms.Count + 1);
        var lampIntensity = MathHelper.Lerp(0.05f, 0.55f, powerFraction);
        foreach (var room in rooms)
        {
            var tint = Color.Lerp(Color.White, RoomDecor.Accent(room.Id), 0.22f);
            var radius = MathF.Max(room.Width, room.Height) * 0.9f + 1.5f;
            lights.Add(new PointLight(new Vector2(room.Center.X, room.Center.Y), radius, tint * lampIntensity));
        }

        var reactorRoom = rooms.FirstOrDefault(r => r.Id.Contains("reactor") || r.Id.Contains("engine"));
        if (reactorRoom is not null && power.ReactorMaxOutput > 0f)
        {
            var outputFraction = MathHelper.Clamp(power.ReactorOutput / power.ReactorMaxOutput, 0f, 1f);
            var fuelFraction = power.ReactorMaxFuel > 0f ? power.ReactorFuel / power.ReactorMaxFuel : 1f;
            var flicker = fuelFraction < 0.15f
                ? 0.7f + 0.3f * MathF.Sin(totalSeconds * 17f) * MathF.Sin(totalSeconds * 6.1f)
                : 1f;
            var radius = MathF.Max(reactorRoom.Width, reactorRoom.Height) * 0.75f + 1f;
            lights.Add(new PointLight(new Vector2(reactorRoom.Center.X, reactorRoom.Center.Y), radius,
                new Color(255, 150, 70) * (0.35f * outputFraction * flicker)));
        }

        return lights;
    }

    // A docked station runs on its own power, not the ship's - always lit, no flicker.
    private static void AddStationLights(List<PointLight> lights, IReadOnlyList<Room> stationRooms)
    {
        foreach (var room in stationRooms)
        {
            var radius = MathF.Max(room.Width, room.Height) * 0.95f + 1.5f;
            lights.Add(new PointLight(new Vector2(room.Center.X, room.Center.Y), radius, Color.White * 0.6f));
        }
    }

    // A boarded enemy hull: no power state to read (it isn't the player's grid), so a fixed dim,
    // uneven red emergency light stands in for "this ship has taken damage and is running dark".
    // The sine offsets are seeded from room position so neighbouring compartments don't flicker in
    // lockstep.
    private static List<PointLight> BuildEnemyShipLights(IReadOnlyList<Room> rooms, float totalSeconds)
    {
        var lights = new List<PointLight>(rooms.Count);
        foreach (var room in rooms)
        {
            var flicker = 0.55f + 0.25f * MathF.Sin(totalSeconds * 9f + room.X) * MathF.Sin(totalSeconds * 2.3f + room.Y);
            var radius = MathF.Max(room.Width, room.Height) * 0.85f + 1.2f;
            lights.Add(new PointLight(new Vector2(room.Center.X, room.Center.Y), radius,
                new Color(210, 90, 70) * MathHelper.Clamp(flicker, 0.2f, 0.85f)));
        }
        return lights;
    }


    protected override void Draw(GameTime gameTime)
    {
        if (!_sessionStarted)
        {
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
                var savedLook = ApplyMenuPostLook();
                _scenePost.Present(_spriteBatch, menuSeconds);
                RestorePostLook(savedLook);
            }
            base.Draw(gameTime);
            return;
        }

        // The line-of-sight and room-lighting masks render into their own targets, which has to
        // happen before the backbuffer is cleared and drawn into - swapping render targets discards
        // whatever the backbuffer already held.
        var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        var maskReady = _client.LatestSnapshot is { } maskSnapshot && BuildVisibilityMask(maskSnapshot, totalSeconds);

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
        var sceneRotationDegrees = _client.LatestSnapshot is { } rotSnapshot ? TurretViewRotationDegrees(rotSnapshot) : 0f;
        var screenPivot = (WorldViewportOrigin + WorldViewportSize / 2f) / sceneZoom;
        var sceneTransform =
            Matrix.CreateTranslation(-screenPivot.X, -screenPivot.Y, 0f) *
            Matrix.CreateRotationZ(MathHelper.ToRadians(sceneRotationDegrees)) *
            Matrix.CreateTranslation(screenPivot.X, screenPivot.Y, 0f) *
            Matrix.CreateScale(sceneZoom, sceneZoom, 1f) * _renderScale;
        _spriteBatch.Begin(transformMatrix: sceneTransform);
        _shipInteriorOrigin = null;
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
            else if (_openBlock.Kind == BlockKind.Navigation)
                _galaxyMapPanel.Draw(_spriteBatch, snapshot, GalaxyMapPanelOrigin, _mapZoom, _mapPanOffset, totalSeconds);
            else if (_infoPanelOpen)
                _infoPanel.Draw(_spriteBatch, snapshot, _client.PlayerId, _infoPanelTab, InfoPanelOrigin);
            else if (myIsAtHelm)
            {
                _helmPanel.Draw(_spriteBatch, snapshot, HelmPanelOrigin);
                _shipStatusPanel.Draw(_spriteBatch, snapshot, HelmPanelOrigin + ShipStatusPanelOffset);
            }
            else if (myCharacter?.OnEnemyShip == true)
                _boardingRenderer.Draw(_spriteBatch, snapshot, ComputeStationCamera(myCharacter));
            else
            {
                var (origin, hullCenter, _) = myCharacter is not null
                    ? ComputeCamera(snapshot, myCharacter)
                    : (WorldViewportOrigin, ShipLocalFrame.GetHullCenter(snapshot.Rooms), Vec2.Zero);
                _shipInteriorOrigin = origin;
                // Behind the periscope you are outside the ship looking at it, so it's drawn closed
                // up - and so is the station it's docked to, for the same reason.
                var fromOutside = MannedTurret(snapshot) is not null;
                _shipRenderer.Draw(_spriteBatch, snapshot, origin, _openBlock, totalSeconds, _effectTracker.Effects, atmosphere: _atmosphere.Particles);
                // A docked station is laid out in these same coordinates, joined to the ship by the
                // shared airlock rectangle - drawn alongside the interior rather than instead of it,
                // so there's no moment where the view swaps to "the station screen".
                if (snapshot.Voyage.Phase == VoyagePhase.Station && !fromOutside)
                    _stationRenderer.Draw(_spriteBatch, snapshot, origin, _talkingToNpcId, totalSeconds);
                // Viewport divided by the zoom for the same reason as the camera origin: the
                // off-screen markers clamp against the screen edges, which live at design
                // coordinates on the far side of the batch's scale.
                _fieldRenderer.Draw(_spriteBatch, snapshot, origin, hullCenter,
                    WorldViewportOrigin / sceneZoom, WorldViewportSize / sceneZoom, totalSeconds, _effectTracker.Effects,
                    seenFromOutside: fromOutside);
            }
        }
        _spriteBatch.End();

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

                var centre = shimmerOrigin + new Vector2(particle.Position.X, particle.Position.Y) * ShipRenderer.PixelsPerUnit;
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
        _scenePost.Present(_spriteBatch, totalSeconds);

        _spriteBatch.Begin(transformMatrix: _renderScale);
        if (_client.LatestSnapshot is { } hudSnapshot)
        {
            // Station dialogue is a HUD overlay on top of the physical scene (like the panels
            // below), not a full-screen takeover - drawn whenever talking to someone; it no-ops
            // internally if _talkingToNpcId is null.
            _stationPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, StationPanelOrigin, _talkingToNpcId);
            _cardGamePanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, CardGamePanelOrigin);

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
                case BlockKind.Battery:
                    _batteryPanel.Draw(_spriteBatch, hudSnapshot.Power, PowerPanelOrigin, totalSeconds);
                    break;
                case BlockKind.System:
                    _systemDevicePanel.Draw(_spriteBatch, _openBlock.System, hudSnapshot.Power, hudSnapshot.Shield, hudSnapshot.SystemStates, PowerPanelOrigin, totalSeconds);
                    break;
                case BlockKind.Rack:
                    _rackPanel.Draw(_spriteBatch, hudSnapshot, RackPanelOrigin, CurrentOpenRackOffset(hudSnapshot), totalSeconds);
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
            }

            _combatPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, ComputeHint(hudSnapshot, _client.PlayerId), CombatPanelOrigin);
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
                        ?? hudSnapshot.Station.WallBlocks.FirstOrDefault(b => b.Id == targetId);
                    var state = hudSnapshot.WallBlockStates.FirstOrDefault(s => s.Id == targetId)
                        ?? hudSnapshot.Station.WallBlockStates.FirstOrDefault(s => s.Id == targetId);
                    if (block is not null && state is not null)
                        _shipRenderer.DrawWallToolTargetBar(_spriteBatch, block, state, wallToolOrigin);
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
                        ?? hudSnapshot.AirlockOuterDoors.FirstOrDefault(d => d.Id == doorTargetId)?.Position;
                    if (doorPosition is { } position)
                        _shipRenderer.DrawDoorToolTargetBar(_spriteBatch, new Vector2(position.X, position.Y), doorState, wallToolOrigin);
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
                            repairState?.RepairProgress ?? 0f, repairState?.RepairTickPosition ?? 0f, cardOrigin);
                    }

                    // Same card, same proximity radius, for a damaged Junction box instead of a
                    // damaged SystemDevice - World.Interact.cs's E-key repair treats both the same way.
                    var nearbyDamagedJunction = hudSnapshot.Components.FirstOrDefault(c =>
                        c.Kind == ComponentKind.Junction && (c.Position - repairPosition).Length() < TurretInteractionRadius &&
                        (hudSnapshot.JunctionStates.FirstOrDefault(s => s.DeviceId == c.Id)?.Damaged ?? false));
                    if (nearbyDamagedJunction is not null)
                    {
                        var holdingRepairTool = HeldItemTypes(repairMe.Inventory).Contains(ItemType.Wrench) ||
                                                 HeldItemTypes(repairMe.Inventory).Contains(ItemType.Screwdriver);
                        var repairState = hudSnapshot.JunctionStates.FirstOrDefault(s => s.DeviceId == nearbyDamagedJunction.Id);
                        var cardOrigin = wallToolOrigin + new Vector2(nearbyDamagedJunction.X, nearbyDamagedJunction.Y) * ShipRenderer.PixelsPerUnit
                            + new Vector2(-SystemRepairPanel.PanelWidth / 2f, -SystemRepairPanel.PanelHeight - 30);
                        _systemRepairPanel.Draw(_spriteBatch, "Распред. коробка", holdingRepairTool,
                            repairState?.RepairProgress ?? 0f, repairState?.RepairTickPosition ?? 0f, cardOrigin);
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
                        var cardOrigin = wallToolOrigin + new Vector2(doorPosition.X, doorPosition.Y) * ShipRenderer.PixelsPerUnit
                            + new Vector2(-SystemRepairPanel.PanelWidth / 2f, -SystemRepairPanel.PanelHeight - 30);
                        _systemRepairPanel.Draw(_spriteBatch, "Дверь", holdingRepairTool,
                            nearbyDestroyedDoor.RepairProgress, nearbyDestroyedDoor.RepairTickPosition, cardOrigin);
                    }
                }
            }

            // HUD batch rather than the scene batch it used to share with the system map - exempt
            // from the sight-cone/room-lighting composite above, same reasoning as InfoPanel/Crew:
            // a full-screen overlay reachable from anywhere (the M key) shouldn't go dark just
            // because the ship interior underneath happens to be sitting in a blind spot right now.
            if (_galacticMapOpen)
                _galacticMapPanel.Draw(_spriteBatch, hudSnapshot, GalaxyMapPanelOrigin, _galacticMapZoom, _galacticMapPanOffset);

            DrawTopBar(_spriteBatch);
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
        }
        _debugOverlay.Draw(_spriteBatch, _client.LatestSnapshot);
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
