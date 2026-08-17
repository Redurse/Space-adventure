using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
    private static readonly Vector2 PowerPanelOrigin = new(40, 360);
    private static readonly Vector2 CombatPanelOrigin = new(480, 360);
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
    private Vector2 InventoryRowOrigin(int slotCount) => new(
        (DesignWidth - InventoryPanel.RowWidth(slotCount)) / 2f,
        HudBottom - InventoryPanel.RowHeight);
    private Vector2 EquipSlotsOrigin => new(
        DesignWidth - InventoryPanel.EquipRowWidth - HudEdgeMargin,
        HudBottom - InventoryPanel.SlotSize);
    private static readonly Vector2 GalaxyMapPanelOrigin = new(60, 64);
    private static readonly Vector2 StationPanelOrigin = new(60, 64);
    private static readonly Vector2 HelmPanelOrigin = new(120, 100);
    // To the right of the radar, which is why it's expressed relative to the helm's own origin -
    // the two are one console and should move together.
    private static readonly Vector2 ShipStatusPanelOffset = new(560, 20);
    // Sight reach in world units. The suit helmet's lamp is a forward cone - wide and long enough to
    // actually work by (mining, lining up an airlock) rather than a keyhole - plus a small all-round
    // pool of spill light, so whatever is right beside you isn't invisible. Unsuited, the
    // compartment lighting carries far enough to fill a room but not the whole deck.
    private const float SuitVisionRadius = 9f;
    private const float SuitVisionHalfAngleDegrees = 55f;
    private const float SuitAmbientRadius = 3f;
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
    private VoyagePanel _voyagePanel = null!;
    private InventoryPanel _inventoryPanel = null!;
    private ReactorPanel _reactorPanel = null!;
    private SystemDevicePanel _systemDevicePanel = null!;
    private GalaxyMapPanel _galaxyMapPanel = null!;
    private StationPanel _stationPanel = null!;
    private HelmPanel _helmPanel = null!;
    private ShipStatusPanel _shipStatusPanel = null!;
    private FieldRenderer _fieldRenderer = null!;
    private StationRenderer _stationRenderer = null!;
    private BoardingRenderer _boardingRenderer = null!;
    private VisibilityMask _visibility = null!;
    private RoomLighting _roomLighting = null!;
    private bool _roomLightingReady;
    private RackPanel _rackPanel = null!;
    private ConnectionsPanel _connectionsPanel = null!;
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
    private ClickTarget _openBlock = ClickTarget.None;
    private string? _talkingToNpcId;
    // Edge-triggered hull purchase, cleared the frame after it's sent - HandleMouseClick's return
    // tuple is already at its practical limit, so this one rides as a field instead.
    private ShipKind? _pendingShipPurchase;
    private QuestKind? _pendingQuestKind; // same pattern, for the Administrator's job board
    private bool _pendingDock; // and for the helm's "Стыковка" button
    private string? _pendingHireCandidateId; // and for the Recruiter's board
    private PinRef? _pendingPinInteract; // wire-laying (World.Wiring.cs), M19-M23
    private string? _pendingComponentMountInteractId; // install/uninstall/relay-operate a mount
    private string? _pendingPickupDroppedItemId; // click-to-pick-up (World.Mining.cs), any context
    private SlotRef? _pendingDropItemFrom; // drag ended over empty space (World.Storage.cs)
    private bool _pendingAbandonQuest; // Administrator's action button when the job can't be turned in here
    private string? _pendingWarpToSystemId; // clicked a system on GalaxyMapPanel's own list (World.StarSystems.cs)
    // The galaxy map's own camera - purely a client view of server-authoritative positions, so it
    // lives here rather than in any snapshot. Zoom via scroll wheel, pan via right-drag; both only
    // read while the navigation console is actually open.
    private float _mapZoom = 1f;
    private Vector2 _mapPanOffset = Vector2.Zero;
    private Point? _mapPanLastMouse;
    private int _prevScrollWheelValue;
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
        _shipRenderer = new ShipRenderer(GraphicsDevice, _font);
        _powerPanel = new PowerPanel(GraphicsDevice, _font);
        _combatPanel = new CombatPanel(GraphicsDevice, _font);
        _voyagePanel = new VoyagePanel(_font);
        _inventoryPanel = new InventoryPanel(GraphicsDevice, _font);
        _reactorPanel = new ReactorPanel(GraphicsDevice, _font);
        _systemDevicePanel = new SystemDevicePanel(_font);
        _galaxyMapPanel = new GalaxyMapPanel(GraphicsDevice, _font);
        _stationPanel = new StationPanel(_font);
        _helmPanel = new HelmPanel(GraphicsDevice, _font);
        _shipStatusPanel = new ShipStatusPanel(GraphicsDevice, _font);
        _fieldRenderer = new FieldRenderer(GraphicsDevice, _font);
        _stationRenderer = new StationRenderer(_shipRenderer, GraphicsDevice, _font);
        _boardingRenderer = new BoardingRenderer(_shipRenderer, GraphicsDevice, _font);
        _visibility = new VisibilityMask(GraphicsDevice);
        _roomLighting = new RoomLighting(GraphicsDevice);
        _rackPanel = new RackPanel(GraphicsDevice, _font);
        _connectionsPanel = new ConnectionsPanel(_font);
        _existingSave = SaveStore.Load();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        // Escape leaves the game - except on the join screen, where it's "never mind" and steps back
        // to the ship list rather than quitting outright.
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
        {
            if (_sessionStarted || !LeaveJoinScreen())
                Exit();
        }

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

        var interactDown = keyboard.IsKeyDown(Keys.F);
        var spaceDown = keyboard.IsKeyDown(Keys.Space);
        var interactPressed = interactDown && !_prevInteractDown;
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

        // Dragging gets first refusal on the button: a press that lands on an item slot starts a
        // drag instead of counting as a click, so releasing over the rack doesn't also read as
        // "clicked empty space, close the panel".
        var (moveItemFrom, moveItemTo, dragTookTheClick) = UpdateItemDrag(mouse, gameTime.TotalGameTime.TotalSeconds);
        if (dragTookTheClick)
            _prevLeftMouseButton = mouse.LeftButton; // keep HandleMouseClick's own edge detection in step
        var (toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, helmStabilizePressed, doorToggleId) =
            dragTookTheClick
                ? (-1, -1, (string?)null, (ItemType?)null, -1, false, false, (ShipUpgradeTrack?)null, false, (string?)null)
                : HandleMouseClick(mouse);
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
        var componentMountInteractId = _pendingComponentMountInteractId;
        _pendingPinInteract = null;
        _pendingComponentMountInteractId = null;

        var pickupDroppedItemId = _pendingPickupDroppedItemId;
        var dropItemFrom = _pendingDropItemFrom;
        _pendingPickupDroppedItemId = null;
        _pendingDropItemFrom = null;

        var abandonQuestPressed = _pendingAbandonQuest;
        _pendingAbandonQuest = false;

        var warpToSystemId = _pendingWarpToSystemId;
        _pendingWarpToSystemId = null;

        // Right-click backs out of a pending wire-lay without walking back to its start pin -
        // harmless to send every frame it's held, the server just clears an already-null anchor.
        var wireLayCancelPressed = mouse.RightButton == ButtonState.Pressed && myCharacter?.LayingWireFromPin is not null;

        // Barotrauma's rule: the held tool works on the left button, aimed at the cursor. Held, not
        // clicked - the flame burns while the button is down (World.Cutting.cs) - and suppressed
        // while a drag is in flight so grabbing an item never lights the torch.
        var cutHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingCutter();
        var weldHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingWelder();

        _client.SendInput(move, powerSystemIndexToSend, powerDirection, interactPressed, aimDirection, firePressed, toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, helmThrottle, helmTurn, stabilizeEngaged, doorToggleId, pushOffPressed, pushOffDirection.X, pushOffDirection.Y, shipPurchase, questKind, dockPressed, moveItemFrom, moveItemTo, lookDirection.X, lookDirection.Y,
            tankAttach?.From, tankAttach?.To, tankDetach, cutHeld, hireCandidateId, weldHeld, pinInteract, wireLayCancelPressed, null, componentMountInteractId, dropItemFrom, pickupDroppedItemId, abandonQuestPressed, warpToSystemId);
        _client.PollSnapshots();
        CloseBlockIfWalkedAway(_client.LatestSnapshot);

        _effectTracker.Step((float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_client.LatestSnapshot is { } latestForEffects)
        {
            _effectTracker.Detect(_previousSnapshot, latestForEffects);
            _previousSnapshot = latestForEffects;
        }
        _atmosphere.Step((float)gameTime.ElapsedGameTime.TotalSeconds, _client.LatestSnapshot);

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

    // Applied to the whole scene batch, so one number moves the camera, the world and the hit
    // tests together instead of each renderer growing a scale parameter.
    private float SceneZoom(WorldSnapshot snapshot) =>
        MannedTurret(snapshot) is not null && _openBlock.Kind is not BlockKind.Navigation
            ? TurretViewZoom
            : 1f;

    private (Turret Turret, TurretState State)? MannedTurret(WorldSnapshot snapshot)
    {
        var state = snapshot.TurretStates.FirstOrDefault(t => t.MannedByPlayerId == _client.PlayerId);
        if (state is null)
            return null;
        var turret = snapshot.Turrets.FirstOrDefault(t => t.Id == state.Id);
        return turret is null ? null : (turret, state);
    }

    // Degrees to spin the whole scene batch by while manning a turret, so the gun's own outward
    // facing (TurretMount.OutwardDegrees, ship-local) reads as screen-up instead of the view
    // always staying upright regardless of which side of the hull the gun sits on. 0 everywhere
    // else - the ship interior/field view is never rotated except behind a periscope.
    private float TurretViewRotationDegrees(WorldSnapshot snapshot)
    {
        if (MannedTurret(snapshot) is not { } manned || _openBlock.Kind is BlockKind.Navigation)
            return 0f;
        var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
        return -90f - mount.OutwardDegrees;
    }

    private (Vector2 Origin, Vec2 HullCenter, Vec2 Anchor) ComputeCamera(WorldSnapshot snapshot, CharacterState me)
    {
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        Vec2 anchorLocal;
        if (MannedTurret(snapshot) is { } manned)
        {
            var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
            anchorLocal = mount.Position + TurretMount.FromDegrees(mount.OutwardDegrees) * PeriscopeViewLead;
        }
        else
        {
            anchorLocal = me.IsOutside
                ? ShipLocalFrame.ToLocal(new Vec2(me.X, me.Y), snapshot.ShipField, hullCenter)
                : new Vec2(me.X, me.Y);
        }
        // Divided by the zoom because the scene batch scales everything drawn at this origin: the
        // anchor has to land on the middle of the screen *after* that scaling, not before it.
        var screenCenter = (WorldViewportOrigin + WorldViewportSize / 2f) / SceneZoom(snapshot);
        var origin = screenCenter - new Vector2(anchorLocal.X, anchorLocal.Y) * ShipRenderer.PixelsPerUnit;
        return (origin, hullCenter, anchorLocal);
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
        if (me is null || me.IsAtHelm || _openBlock.Kind is BlockKind.Navigation)
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

            // While docked the station's compartments are part of the same layout, in the same
            // coordinates - its walls block the view exactly like the ship's own.
            var rooms = snapshot.Rooms;
            var docked = snapshot.Voyage.Phase == VoyagePhase.Station;
            if (docked)
            {
                foreach (var door in snapshot.StationDoors)
                    gaps.Add(Occluders.ToGap(door));
                rooms = snapshot.Rooms.Concat(snapshot.StationRooms).ToList();
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
                AddStationLights(lights, snapshot.StationRooms);
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
        _roomLightingReady = _roomLighting.Build(walls, lights, floor, origin, _renderScale);
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
            GraphicsDevice.Clear(Color.Black);
            DrawMenu();
            base.Draw(gameTime);
            return;
        }

        // The line-of-sight and room-lighting masks render into their own targets, which has to
        // happen before the backbuffer is cleared and drawn into - swapping render targets discards
        // whatever the backbuffer already held.
        var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        var maskReady = _client.LatestSnapshot is { } maskSnapshot && BuildVisibilityMask(maskSnapshot, totalSeconds);

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
        if (_client.LatestSnapshot is { } snapshot)
        {
            var myCharacter = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
            var myIsAtHelm = myCharacter?.IsAtHelm ?? false;

            // The galaxy map / station / wiring / helm views take over the ship-interior viewport
            // for as long as they're open — there's nowhere else on screen big enough to put them.
            // Everything else shares one continuous camera: the ship interior is always drawn,
            // with whatever's outside it (asteroids, ore, EVA characters) layered on top in the
            // same ship-local frame, so walking through the airlock never swaps renderer or scale.
            if (_openBlock.Kind == BlockKind.Navigation)
                _galaxyMapPanel.Draw(_spriteBatch, snapshot, GalaxyMapPanelOrigin, _mapZoom, _mapPanOffset);
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
                // Behind the periscope you are outside the ship looking at it, so it's drawn closed
                // up - and so is the station it's docked to, for the same reason.
                var fromOutside = MannedTurret(snapshot) is not null;
                _shipRenderer.Draw(_spriteBatch, snapshot, origin, _openBlock, totalSeconds, _effectTracker.Effects, hullPlating: fromOutside, atmosphere: _atmosphere.Particles);
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
        if (_roomLightingReady)
            _roomLighting.Composite(_spriteBatch);
        else if (maskReady)
            _visibility.Composite(_spriteBatch);

        _spriteBatch.Begin(transformMatrix: _renderScale);
        if (_client.LatestSnapshot is { } hudSnapshot)
        {
            // Station dialogue is a HUD overlay on top of the physical scene (like the panels
            // below), not a full-screen takeover - drawn whenever talking to someone; it no-ops
            // internally if _talkingToNpcId is null.
            _stationPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, StationPanelOrigin, _talkingToNpcId);

            // Only one block's terminal is shown at a time, at the same HUD slot — you have to
            // actually be "in" it (game_design.md section 1) rather than seeing everything always.
            switch (_openBlock.Kind)
            {
                case BlockKind.Distribution:
                    _powerPanel.Draw(_spriteBatch, hudSnapshot.Power, hudSnapshot.SystemStates, _selectedPowerSystem, PowerPanelOrigin);
                    break;
                case BlockKind.Reactor:
                    _reactorPanel.Draw(_spriteBatch, hudSnapshot.Reactor, PowerPanelOrigin);
                    break;
                case BlockKind.System:
                    _systemDevicePanel.Draw(_spriteBatch, _openBlock.System, hudSnapshot.Power, hudSnapshot.Shield, hudSnapshot.SystemStates, PowerPanelOrigin);
                    break;
                case BlockKind.Rack:
                    _rackPanel.Draw(_spriteBatch, hudSnapshot, PowerPanelOrigin, CurrentOpenRackOffset(hudSnapshot));
                    break;
                case BlockKind.Connections when _openBlock.TargetComponentId is { } targetComponentId:
                    _connectionsPanel.Draw(_spriteBatch, hudSnapshot, targetComponentId, PowerPanelOrigin);
                    break;
            }

            _combatPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, ComputeHint(hudSnapshot, _client.PlayerId), CombatPanelOrigin);
            _voyagePanel.Draw(_spriteBatch, hudSnapshot, VoyagePanelOrigin);
            var myInventory = hudSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
            var carriedSlotCount = myInventory?.MainSlots.Count ?? 0;
            var rowOrigin = InventoryRowOrigin(carriedSlotCount);
            var hoveredToolSlot = myInventory is not null ? HoveredToolSlotIndex(myInventory, rowOrigin) : null;
            _inventoryPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, rowOrigin, EquipSlotsOrigin, hoveredToolSlot);

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
        base.UnloadContent();
    }
}
