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
    private static readonly Vector2 WiringPanelOrigin = new(120, 100);
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
    private WiringPanel _wiringPanel = null!;
    private HelmPanel _helmPanel = null!;
    private ShipStatusPanel _shipStatusPanel = null!;
    private FieldRenderer _fieldRenderer = null!;
    private StationRenderer _stationRenderer = null!;
    private BoardingRenderer _boardingRenderer = null!;
    private VisibilityMask _visibility = null!;
    private RackPanel _rackPanel = null!;
    private SlotRef? _dragFrom;
    // Oxygen-tank sockets: clicking one plugs in the tank you're holding, or pulls the tank back
    // out. Edge-triggered like the other click-driven commands, so they're queued here and sent
    // exactly once (World.OxygenTanks.cs).
    private (int From, int To)? _pendingTankAttach;
    private int? _pendingTankDetach;
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
    private readonly EffectTracker _effectTracker = new();
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
        _wiringPanel = new WiringPanel(GraphicsDevice, _font);
        _helmPanel = new HelmPanel(GraphicsDevice, _font);
        _shipStatusPanel = new ShipStatusPanel(GraphicsDevice, _font);
        _fieldRenderer = new FieldRenderer(GraphicsDevice, _font);
        _stationRenderer = new StationRenderer(_shipRenderer, GraphicsDevice, _font);
        _boardingRenderer = new BoardingRenderer(_shipRenderer, GraphicsDevice, _font);
        _visibility = new VisibilityMask(GraphicsDevice);
        _rackPanel = new RackPanel(GraphicsDevice, _font);
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
        // Dragging gets first refusal on the button: a press that lands on an item slot starts a
        // drag instead of counting as a click, so releasing over the rack doesn't also read as
        // "clicked empty space, close the panel".
        var (moveItemFrom, moveItemTo, dragTookTheClick) = UpdateItemDrag(mouse, gameTime.TotalGameTime.TotalSeconds);
        if (dragTookTheClick)
            _prevLeftMouseButton = mouse.LeftButton; // keep HandleMouseClick's own edge detection in step
        var (toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, wireLinkInteractId, helmStabilizePressed, doorToggleId) =
            dragTookTheClick
                ? (-1, -1, (string?)null, (ItemType?)null, -1, false, false, (ShipUpgradeTrack?)null, (string?)null, false, (string?)null)
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
        _pendingShipPurchase = null; // edge-triggered: sent exactly once per click
        _pendingQuestKind = null;
        _pendingDock = false;

        var tankAttach = _pendingTankAttach;
        var tankDetach = _pendingTankDetach;
        _pendingTankAttach = null;
        _pendingTankDetach = null;

        // Barotrauma's rule: the held tool works on the left button, aimed at the cursor. Held, not
        // clicked - the flame burns while the button is down (World.Cutting.cs) - and suppressed
        // while a drag is in flight so grabbing an item never lights the torch.
        var cutHeld = mouse.LeftButton == ButtonState.Pressed && _dragFrom is null && HoldingCutter();

        _client.SendInput(move, powerSystemIndexToSend, powerDirection, interactPressed, aimDirection, firePressed, toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId, buyItemType, sellSlotIndex, acceptCargoQuestPressed, turnInCargoQuestPressed, purchaseUpgradeTrack, wireLinkInteractId, helmThrottle, helmTurn, stabilizeEngaged, doorToggleId, pushOffPressed, pushOffDirection.X, pushOffDirection.Y, shipPurchase, questKind, dockPressed, moveItemFrom, moveItemTo, lookDirection.X, lookDirection.Y,
            tankAttach?.From, tankAttach?.To, tankDetach, cutHeld);
        _client.PollSnapshots();
        CloseBlockIfWalkedAway(_client.LatestSnapshot);

        _effectTracker.Step((float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_client.LatestSnapshot is { } latestForEffects)
        {
            _effectTracker.Detect(_previousSnapshot, latestForEffects);
            _previousSnapshot = latestForEffects;
        }

        base.Update(gameTime);
    }

    private static Vec2 ReadMoveInput(KeyboardState keyboard)
    {
        float x = 0, y = 0;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) x -= 1;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) x += 1;
        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) y -= 1;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) y += 1;
        return new Vec2(x, y);
    }

    // Which way to swing the barrel so it ends up pointing at the cursor. Returns a traverse
    // direction rather than an angle, so the server stays the one authority on how fast a gun can
    // slew and how far its arc goes; the deadband stops the barrel hunting back and forth by
    // fractions of a degree once it's on target.
    private float ReadTurretAimTowardCursor()
    {
        if (_client.LatestSnapshot is not { } snapshot ||
            snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId) is not { } me ||
            MannedTurret(snapshot) is not { } manned)
            return 0f;

        var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
        var origin = ComputeCamera(snapshot, me).Origin;
        // Through the same scale the scene batch draws with, or the cursor and the barrel would
        // disagree about where "over there" is.
        var mountOnScreen = (origin + new Vector2(mount.Position.X, mount.Position.Y) * ShipRenderer.PixelsPerUnit)
            * SceneZoom(snapshot);
        var toCursor = new Vector2(_designMouse.X - mountOnScreen.X, _designMouse.Y - mountOnScreen.Y);
        if (toCursor.LengthSquared() < 1f)
            return 0f;

        var cursorDegrees = MathF.Atan2(toCursor.Y, toCursor.X) * (180f / MathF.PI);
        var wanted = Math.Clamp(ShortestAngle(cursorDegrees - mount.OutwardDegrees),
            manned.Turret.MinAimDegrees, manned.Turret.MaxAimDegrees);
        var delta = wanted - manned.State.AimDegrees;
        return MathF.Abs(delta) < 1f ? 0f : MathF.Sign(delta);
    }

    private static float ShortestAngle(float degrees) => ((degrees % 360f) + 540f) % 360f - 180f;

    // Reused for aim while manning a turret — movement is locked server-side at that point.
    private static float ReadAimDirection(KeyboardState keyboard)
    {
        float dir = 0;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) dir -= 1;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) dir += 1;
        return dir;
    }

    // The field view centers its camera on the local player (FieldRenderer), so the direction from
    // screen-center to the cursor is exactly the world-space aim direction, no camera offset needed.
    // Mouse drag-and-drop between item slots (game_design.md section 13). Press on a slot that has
    // something in it picks it up; release over another slot moves it there (the server swaps, so
    // dropping onto an occupied slot exchanges the two). Release anywhere else just puts it back -
    // dropping an item into the void would be an easy way to lose your only cutter by accident.
    private (SlotRef? From, SlotRef? To, bool ConsumedPress) UpdateItemDrag(MouseState mouse, double nowSeconds)
    {
        var pressed = mouse.LeftButton == ButtonState.Pressed;
        var justPressed = pressed && _prevDragButton == ButtonState.Released;
        var justReleased = !pressed && _prevDragButton == ButtonState.Pressed;
        _prevDragButton = mouse.LeftButton;

        if (_client.LatestSnapshot is not { } snapshot)
        {
            _dragFrom = null;
            return (null, null, false);
        }

        if (justPressed)
        {
            if (HitTestItemSlot(snapshot) is { } slot && ItemInSlot(snapshot, slot) is not null)
            {
                var doubleClicked = _lastClickedSlot == slot && nowSeconds - _lastSlotClickSeconds < DoubleClickSeconds;
                _lastClickedSlot = slot;
                _lastSlotClickSeconds = doubleClicked ? double.NegativeInfinity : nowSeconds; // never chain a third click into a second move
                if (doubleClicked && QuickMoveTarget(snapshot, slot) is { } quickTarget)
                {
                    _dragFrom = null;
                    return (slot, quickTarget, true);
                }

                _dragFrom = slot;
                return (null, null, true);
            }
            return (null, null, false);
        }

        if (justReleased && _dragFrom is { } from)
        {
            _dragFrom = null;
            var target = HitTestItemSlot(snapshot);
            return target is { } to && to != from ? (from, to, true) : (null, null, true);
        }

        return (null, null, false);
    }

    // targetSlot: a row index, or -1 for the suit being worn (Inventory.WornSuitSlot).
    private void QueueSocketClick(InventoryState inventory, int targetSlot)
    {
        var charge = targetSlot < 0 ? inventory.WornSuitTank : inventory.MainSlotTanks[targetSlot];
        if (charge is not null)
        {
            _pendingTankDetach = targetSlot;
            return;
        }

        // The tank has to be in hand, not merely carried - plugging one in is a two-handed job at
        // the same level of ceremony as everything else in this inventory.
        foreach (var held in inventory.HeldMainSlotIndices)
            if (inventory.MainSlots[held] == ItemType.OxygenTank)
            {
                _pendingTankAttach = (held, targetSlot);
                return;
            }
    }

    private bool HoldingCutter() =>
        _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } inventory
        && inventory.HeldMainSlotIndices.Any(i => inventory.MainSlots[i] == ItemType.Cutter);

    private const double DoubleClickSeconds = 0.4;

    // Double-clicking an item sends it straight across to the container you have open, into the
    // first free slot. Clearing your hands into the rack is the common case, and dragging items
    // across one at a time to do it is busywork. Only armed while the rack is open — "across" has
    // to have somewhere to mean.
    private SlotRef? QuickMoveTarget(WorldSnapshot snapshot, SlotRef from)
    {
        if (_openBlock.Kind != BlockKind.Rack)
            return null;

        if (from.Kind == ItemSlotKind.Main)
        {
            for (var i = 0; i < snapshot.RackSlots.Count; i++)
                if (snapshot.RackSlots[i] is null)
                    return new SlotRef(ItemSlotKind.Rack, i);
            return null; // rack full — better to do nothing than to swap with an arbitrary slot
        }

        var inventory = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
        if (inventory is null)
            return null;

        for (var i = 0; i < inventory.MainSlots.Count; i++)
            if (inventory.MainSlots[i] is null)
                return new SlotRef(ItemSlotKind.Main, i);
        return null;
    }

    private SlotRef? HitTestItemSlot(WorldSnapshot snapshot)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me?.Inventory is { } inventory)
            for (var i = 0; i < inventory.MainSlots.Count; i++)
                if (InventoryPanel.GetMainSlotRect(i, InventoryRowOrigin(inventory.MainSlots.Count)).Contains(_designMouse))
                    return new SlotRef(ItemSlotKind.Main, i);

        if (_openBlock.Kind == BlockKind.Rack)
            for (var i = 0; i < StorageRack.Capacity; i++)
                if (RackPanel.GetSlotRect(i, PowerPanelOrigin).Contains(_designMouse))
                    return new SlotRef(ItemSlotKind.Rack, i);

        return null;
    }

    private ItemType? ItemInSlot(WorldSnapshot snapshot, SlotRef slot)
    {
        if (slot.Kind == ItemSlotKind.Rack)
            return slot.Index < snapshot.RackSlots.Count ? snapshot.RackSlots[slot.Index] : null;

        var inventory = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
        return inventory is not null && slot.Index < inventory.MainSlots.Count ? inventory.MainSlots[slot.Index] : null;
    }

    private Vec2 ReadPushOffDirection()
    {
        var screenCenter = WorldViewportOrigin + WorldViewportSize / 2f;
        var offset = new Vector2(_designMouse.X - screenCenter.X, _designMouse.Y - screenCenter.Y);
        var vec = new Vec2(offset.X, offset.Y);
        return vec.Length() > 0.0001f ? vec.Normalized() : Vec2.Zero;
    }

    private static int? ReadPowerSystemSelection(KeyboardState keyboard)
    {
        if (keyboard.IsKeyDown(Keys.D1)) return 0;
        if (keyboard.IsKeyDown(Keys.D2)) return 1;
        if (keyboard.IsKeyDown(Keys.D3)) return 2;
        if (keyboard.IsKeyDown(Keys.D4)) return 3;
        if (keyboard.IsKeyDown(Keys.D5)) return 4;
        return null;
    }

    private static float ReadPowerDirection(KeyboardState keyboard)
    {
        float direction = 0;
        if (keyboard.IsKeyDown(Keys.Q)) direction -= 1;
        if (keyboard.IsKeyDown(Keys.E)) direction += 1;
        return direction;
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
        MannedTurret(snapshot) is not null && _openBlock.Kind is not (BlockKind.Navigation or BlockKind.Wiring)
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
    // mask there.
    private bool BuildVisibilityMask(WorldSnapshot snapshot)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me is null || me.IsAtHelm || _openBlock.Kind is BlockKind.Navigation or BlockKind.Wiring)
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

        if (me.OnEnemyShip)
        {
            foreach (var door in snapshot.EnemyShipDoors)
                gaps.Add(Occluders.ToGap(door));
            gaps.Add(Occluders.ToGap(snapshot.EnemyShipBoardingHatch));
            walls = Occluders.Build(snapshot.EnemyShipRooms, gaps);
            origin = ComputeStationCamera(me);
            eye = new Vector2(me.X, me.Y);
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
            if (snapshot.Voyage.Phase == VoyagePhase.Station)
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
        }

        var radius = me.WearingSuit ? SuitVisionRadius : OpenVisionRadius;
        var halfAngle = me.WearingSuit ? SuitVisionHalfAngleDegrees : 180f;
        // Facing is stored in whatever frame the character moves in - field coordinates while
        // outside - but the mask is built in the ship's frame, same as the camera.
        var facing = new Vec2(me.FacingX, me.FacingY);
        if (me.IsOutside)
            facing = ShipLocalFrame.ToLocalDirection(facing, snapshot.ShipField.RotationDegrees);
        var ambient = me.WearingSuit ? SuitAmbientRadius : 0f;
        return _visibility.Build(walls, eye, new Vector2(facing.X, facing.Y), radius, halfAngle, ambient, origin, _renderScale);
    }

    // One left click handles, in priority order: (1) the Barotrauma-style hold strip under an
    // inventory slot, (2) a reactor fuel-rod slot while the reactor is open, (3) a galaxy map
    // point while the navigation console is open, (4) the Trader's buy/sell lists, the
    // Administrator's quest button, or the Mechanic's upgrade list while talking to them, (4.5)
    // a wire's line while the wiring panel is open, (4.6) the helm's stabilize button while
    // manning it, (5) opening/closing a block by clicking it on the ship view (requires standing
    // close), (6) clicking empty space closes whatever's open. Edge-triggered so a held button
    // doesn't spam. The helm joystick's continuous drag is handled separately (UpdateHelmThrustDrag)
    // since it isn't an edge-triggered click.
    private (int ToggleHoldSlotIndex, int ToggleReactorSlotIndex, string? TravelToPointId, ItemType? BuyItemType, int SellSlotIndex, bool AcceptCargoQuestPressed, bool TurnInCargoQuestPressed, ShipUpgradeTrack? PurchaseUpgradeTrack, string? WireLinkInteractId, bool HelmStabilizePressed, string? DoorToggleId) HandleMouseClick(MouseState mouse)
    {
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevLeftMouseButton == ButtonState.Released;
        _prevLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return (-1, -1, null, null, -1, false, false, null, null, false, null);

        var snapshot = _client.LatestSnapshot;
        var me = snapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);

        // Behind a periscope the mouse is the gunsight and nothing else. The scene is drawn from
        // out at the gun and at half scale, so every world-space hit test below would be pointing
        // at whatever used to be under the cursor rather than what's there now.
        if (snapshot is not null && MannedTurret(snapshot) is not null)
            return (-1, -1, null, null, -1, false, false, null, null, false, null);

        var slotCount = me?.Inventory?.MainSlots.Count ?? 0;
        for (var i = 0; i < slotCount; i++)
        {
            if (InventoryPanel.GetHoldStripRect(i, InventoryRowOrigin(slotCount)).Contains(_designMouse))
                return (i, -1, null, null, -1, false, false, null, null, false, null);
        }

        if (snapshot is null || me is null)
            return (-1, -1, null, null, -1, false, false, null, null, false, null);

        if (me.IsAtHelm && HelmPanel.GetStabilizeButtonRect(HelmPanelOrigin).Contains(_designMouse))
            return (-1, -1, null, null, -1, false, false, null, null, true, null);

        // Only armed while the server says the ship is actually alongside the berth - clicking a
        // dimmed "distance to port" readout does nothing.
        if (me.IsAtHelm && snapshot.CanDock && HelmPanel.GetDockButtonRect(HelmPanelOrigin).Contains(_designMouse))
        {
            _pendingDock = true;
            return (-1, -1, null, null, -1, false, false, null, null, false, null);
        }

        if (_openBlock.Kind == BlockKind.Reactor)
        {
            for (var i = 0; i < snapshot.Reactor.RodCharges.Count; i++)
            {
                if (ReactorPanel.GetSlotRect(i, PowerPanelOrigin).Contains(_designMouse))
                    return (-1, i, null, null, -1, false, false, null, null, false, null);
            }
        }

        if (_openBlock.Kind == BlockKind.Navigation)
        {
            var mapOrigin = GalaxyMapPanel.ComputeMapOrigin(GalaxyMapPanelOrigin, snapshot.GalaxyPoints);
            foreach (var point in snapshot.GalaxyPoints)
            {
                if (GalaxyMapPanel.GetPointRect(point, mapOrigin).Contains(_designMouse))
                    return (-1, -1, point.Id, null, -1, false, false, null, null, false, null);
            }
        }

        if (_openBlock.Kind == BlockKind.Station)
        {
            var talkingToKind = snapshot.StationNpcs.FirstOrDefault(n => n.Id == _talkingToNpcId)?.Kind;

            if (talkingToKind == NpcKind.Trader)
            {
                for (var i = 0; i < TradeCatalog.Goods.Count; i++)
                {
                    if (StationPanel.GetGoodRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, TradeCatalog.Goods[i].Item, -1, false, false, null, null, false, null);
                }

                for (var i = 0; i < slotCount; i++)
                {
                    if (StationPanel.GetSellRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, null, i, false, false, null, null, false, null);
                }
            }

            if (talkingToKind == NpcKind.Administrator)
            {
                if (snapshot.ActiveQuest is not { } quest)
                {
                    // Job board: one clickable row per kind on offer (StationPanel).
                    for (var i = 0; i < StationPanel.OfferedQuestKinds.Length; i++)
                    {
                        if (!StationPanel.GetQuestOfferRect(i, StationPanelOrigin).Contains(_designMouse))
                            continue;
                        _pendingQuestKind = StationPanel.OfferedQuestKinds[i];
                        return (-1, -1, null, null, -1, true, false, null, null, false, null);
                    }
                }
                else if (StationPanel.GetAdminActionRect(StationPanelOrigin).Contains(_designMouse))
                {
                    // Mirrors StationPanel.DrawAdminQuest's own turn-in test - deliveries hand in
                    // at the destination, everything else back where it was issued.
                    var turnInHere = quest.Kind == QuestKind.Delivery
                        ? quest.DestinationPointId == snapshot.Voyage.DockedPointId
                        : quest.IssuedByPointId == snapshot.Voyage.DockedPointId;
                    if (turnInHere)
                        return (-1, -1, null, null, -1, false, true, null, null, false, null);
                }
            }

            if (talkingToKind == NpcKind.Mechanic)
            {
                for (var i = 0; i < ShipUpgradeCatalog.Tracks.Count; i++)
                {
                    if (StationPanel.GetUpgradeRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, null, -1, false, false, ShipUpgradeCatalog.Tracks[i].Track, null, false, null);
                }
            }

            if (talkingToKind == NpcKind.Shipwright)
            {
                for (var i = 0; i < StationPanel.PurchasableShipKinds.Length; i++)
                {
                    if (!StationPanel.GetShipRect(i, StationPanelOrigin).Contains(_designMouse))
                        continue;
                    _pendingShipPurchase = StationPanel.PurchasableShipKinds[i];
                    return (-1, -1, null, null, -1, false, false, null, null, false, null);
                }
            }
        }

        // Physically standing on the station (game_design.md section 10 - walk up and click an
        // NPC in their own room). Same camera and coordinates as the ship's own interior now, but
        // none of the ship-block clicks below are reachable from over here anyway.
        if (me.OnStation)
        {
            var stationOrigin = ComputeCamera(snapshot, me).Origin;
            foreach (var npc in snapshot.StationNpcs)
            {
                if (npc.Kind == NpcKind.Security)
                    continue; // there's nothing to discuss with the guard - only to avoid them
                if (!StationRenderer.GetNpcRect(npc, stationOrigin).Contains(_designMouse))
                    continue;
                _talkingToNpcId = _talkingToNpcId == npc.Id ? null : npc.Id;
                _openBlock = _talkingToNpcId is null ? ClickTarget.None : ClickTarget.Station;
                return (-1, -1, null, null, -1, false, false, null, null, false, null);
            }

            _openBlock = ClickTarget.None;
            _talkingToNpcId = null;
            return (-1, -1, null, null, -1, false, false, null, null, false, null);
        }

        if (_openBlock.Kind == BlockKind.Wiring)
        {
            foreach (var link in snapshot.WireLinks)
            {
                if (WiringPanel.GetLinkClickRect(link, snapshot, WiringPanelOrigin).Contains(_designMouse))
                    return (-1, -1, null, null, -1, false, false, null, link.Id, false, null);
            }
        }

        var myPosition = new Vec2(me.X, me.Y);
        bool NearEnough(Vec2 blockPosition) => (blockPosition - myPosition).Length() < TurretInteractionRadius;
        var origin = ComputeCamera(snapshot, me).Origin;

        if (NearEnough(snapshot.ReactorBlock.Position) &&
            ShipRenderer.GetBlockRect(snapshot.ReactorBlock.Position, ShipRenderer.BigBlockSize, origin).Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Reactor ? ClickTarget.None : ClickTarget.Reactor;
            return (-1, -1, null, null, -1, false, false, null, null, false, null);
        }

        if (NearEnough(snapshot.DistributionBlock.Position) &&
            ShipRenderer.GetBlockRect(snapshot.DistributionBlock.Position, ShipRenderer.MediumBlockSize, origin).Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Distribution ? ClickTarget.None : ClickTarget.Distribution;
            return (-1, -1, null, null, -1, false, false, null, null, false, null);
        }

        if (NearEnough(snapshot.NavigationConsole.Position) &&
            ShipRenderer.GetBlockRect(snapshot.NavigationConsole.Position, ShipRenderer.MediumBlockSize, origin).Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Navigation ? ClickTarget.None : ClickTarget.Navigation;
            return (-1, -1, null, null, -1, false, false, null, null, false, null);
        }

        if (NearEnough(snapshot.WiringTerminal.Position) &&
            ShipRenderer.GetBlockRect(snapshot.WiringTerminal.Position, ShipRenderer.NormalBlockSize, origin).Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Wiring ? ClickTarget.None : ClickTarget.Wiring;
            return (-1, -1, null, null, -1, false, false, null, null, false, null);
        }

        if (NearEnough(snapshot.StorageRack.Position) &&
            ShipRenderer.GetBlockRect(snapshot.StorageRack.Position, ShipRenderer.MediumBlockSize, origin).Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Rack ? ClickTarget.None : ClickTarget.Rack;
            return (-1, -1, null, null, -1, false, false, null, null, false, null);
        }

        foreach (var device in snapshot.SystemDevices)
        {
            var size = device.System == PowerSystemId.Engine ? ShipRenderer.BigBlockSize : ShipRenderer.NormalBlockSize;
            if (NearEnough(device.Position) && ShipRenderer.GetBlockRect(device.Position, size, origin).Contains(_designMouse))
            {
                _openBlock = _openBlock.Kind == BlockKind.System && _openBlock.System == device.System
                    ? ClickTarget.None
                    : ClickTarget.ForSystem(device.System);
                return (-1, -1, null, null, -1, false, false, null, null, false, null);
            }
        }

        // Oxygen-tank sockets, before anything in the world: they sit under the inventory row, so a
        // click there is never meant for the deck behind it. Empty socket + a tank in hand plugs it
        // in; a filled one gives the tank back.
        if (snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } tankInventory)
        {
            for (var i = 0; i < tankInventory.MainSlots.Count; i++)
            {
                var item = tankInventory.MainSlots[i];
                if (item is not { } carried || !OxygenTankDefinitions.HasSocket(carried))
                    continue;
                if (!InventoryPanel.GetSocketRect(InventoryPanel.GetMainSlotRect(i, InventoryRowOrigin(tankInventory.MainSlots.Count))).Contains(_designMouse))
                    continue;
                QueueSocketClick(tankInventory, i);
                return (-1, -1, null, null, -1, false, false, null, null, false, null);
            }

            for (var i = 0; i < InventoryPanel.EquipSlots.Length; i++)
            {
                var worn = tankInventory.Equipped.TryGetValue(InventoryPanel.EquipSlots[i].Id, out var e) ? e : null;
                if (worn is not { } wornItem || !OxygenTankDefinitions.HasSocket(wornItem))
                    continue;
                if (!InventoryPanel.GetSocketRect(InventoryPanel.GetSlotRect(i, EquipSlotsOrigin), above: true).Contains(_designMouse))
                    continue;
                QueueSocketClick(tankInventory, -1); // Inventory.WornSuitSlot
                return (-1, -1, null, null, -1, false, false, null, null, false, null);
            }
        }

        // Doors toggle directly on click - no panel to open, just an immediate flip
        // (game_design.md Phase 3, M16).
        foreach (var door in snapshot.Doors)
        {
            if (NearEnough(door.Position) && ShipRenderer.GetDoorRect(door.Left, door.Top, door.Width, door.Height, origin).Contains(_designMouse))
                return (-1, -1, null, null, -1, false, false, null, null, false, door.Id);
        }

        foreach (var outerDoor in snapshot.AirlockOuterDoors)
        {
            if (NearEnough(outerDoor.Position) && ShipRenderer.GetDoorRect(outerDoor.Left, outerDoor.Top, outerDoor.Width, outerDoor.Height, origin).Contains(_designMouse))
                return (-1, -1, null, null, -1, false, false, null, null, false, outerDoor.Id);
        }

        // Aboard a boarded hull the doors are the fight: they start closed, and opening one lets
        // the breach through into the next compartment (World.EnemyAtmosphere.cs). Same click, same
        // proximity rule - the character's own coordinates are that structure's while aboard it.
        foreach (var door in snapshot.EnemyShipDoors)
        {
            if (NearEnough(door.Position) && ShipRenderer.GetDoorRect(door.Left, door.Top, door.Width, door.Height, origin).Contains(_designMouse))
                return (-1, -1, null, null, -1, false, false, null, null, false, door.Id);
        }

        _openBlock = ClickTarget.None;
        _talkingToNpcId = null;
        return (-1, -1, null, null, -1, false, false, null, null, false, null);
    }

    // Flying the ship: W ahead, X astern, A/D swing the bow, S brakes. The mouse used to drag a
    // joystick that set a world-space thrust vector, which meant the pilot could aim the ship's
    // course but never its heading - and on a hull whose guns and airlock face particular
    // directions, heading is the thing you actually steer.
    private static (float Throttle, float Turn) ReadHelmInput(KeyboardState keyboard)
    {
        var throttle = 0f;
        if (keyboard.IsKeyDown(Keys.W)) throttle += 1f;
        if (keyboard.IsKeyDown(Keys.X)) throttle -= 1f;

        var turn = 0f;
        if (keyboard.IsKeyDown(Keys.A)) turn -= 1f;
        if (keyboard.IsKeyDown(Keys.D)) turn += 1f;

        return (throttle, turn);
    }

    // Walking out of interaction range auto-closes whatever's open — matches the same radius
    // that gated opening it in the first place, so you can't keep adjusting a slider you've
    // wandered away from.
    private void CloseBlockIfWalkedAway(WorldSnapshot? snapshot)
    {
        if (_openBlock.Kind == BlockKind.None || snapshot is null)
            return;

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me is null)
            return;

        // Station dialogue closes as soon as you're not next to the NPC you were talking to (or
        // not on the station at all any more) - a separate coordinate space from every other
        // block below, so it can't share their myPosition-based distance check.
        if (_openBlock.Kind == BlockKind.Station)
        {
            var talkingTo = snapshot.StationNpcs.FirstOrDefault(n => n.Id == _talkingToNpcId);
            var stillNear = me.OnStation && talkingTo is not null &&
                (talkingTo.Position - new Vec2(me.X, me.Y)).Length() < TurretInteractionRadius;
            if (!stillNear)
            {
                _openBlock = ClickTarget.None;
                _talkingToNpcId = null;
            }
            return;
        }

        var myPosition = new Vec2(me.X, me.Y);
        var blockPosition = _openBlock.Kind switch
        {
            BlockKind.Reactor => snapshot.ReactorBlock.Position,
            BlockKind.Distribution => snapshot.DistributionBlock.Position,
            BlockKind.Navigation => snapshot.NavigationConsole.Position,
            BlockKind.Wiring => snapshot.WiringTerminal.Position,
            BlockKind.Rack => snapshot.StorageRack.Position,
            BlockKind.System => snapshot.SystemDevices.First(d => d.System == _openBlock.System).Position,
            _ => myPosition,
        };

        if ((blockPosition - myPosition).Length() >= TurretInteractionRadius)
        {
            _openBlock = ClickTarget.None;
            _talkingToNpcId = null;
        }
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

        // The line-of-sight mask renders into its own target, which has to happen before the
        // backbuffer is cleared and drawn into - swapping render targets discards whatever the
        // backbuffer already held.
        var maskReady = _client.LatestSnapshot is { } maskSnapshot && BuildVisibilityMask(maskSnapshot);

        GraphicsDevice.Clear(Color.Black);

        // Manning a turret pulls the whole scene back to half scale (SceneZoom) so the gunner can
        // see as far as the gun shoots; everywhere else this is the identity.
        var sceneZoom = _client.LatestSnapshot is { } zoomSnapshot ? SceneZoom(zoomSnapshot) : 1f;
        _spriteBatch.Begin(transformMatrix: Matrix.CreateScale(sceneZoom, sceneZoom, 1f) * _renderScale);
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
                _galaxyMapPanel.Draw(_spriteBatch, snapshot, GalaxyMapPanelOrigin);
            else if (_openBlock.Kind == BlockKind.Wiring)
                _wiringPanel.Draw(_spriteBatch, snapshot, WiringPanelOrigin);
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
                var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
                // Behind the periscope you are outside the ship looking at it, so it's drawn closed
                // up - and so is the station it's docked to, for the same reason.
                var fromOutside = MannedTurret(snapshot) is not null;
                _shipRenderer.Draw(_spriteBatch, snapshot, origin, _openBlock, totalSeconds, _effectTracker.Effects, hullPlating: fromOutside);
                // A docked station is laid out in these same coordinates, joined to the ship by the
                // shared airlock rectangle - drawn alongside the interior rather than instead of it,
                // so there's no moment where the view swaps to "the station screen".
                if (snapshot.Voyage.Phase == VoyagePhase.Station && !fromOutside)
                    _stationRenderer.Draw(_spriteBatch, snapshot, origin, _talkingToNpcId);
                // Viewport divided by the zoom for the same reason as the camera origin: the
                // off-screen markers clamp against the screen edges, which live at design
                // coordinates on the far side of the batch's scale.
                _fieldRenderer.Draw(_spriteBatch, snapshot, origin, hullCenter,
                    WorldViewportOrigin / sceneZoom, WorldViewportSize / sceneZoom, totalSeconds, _effectTracker.Effects,
                    seenFromOutside: fromOutside);
            }
        }
        _spriteBatch.End();

        // Multiplied over the finished scene, before any HUD is drawn: everything the player has
        // no line of sight to becomes absolutely black, while the panels below stay readable.
        if (maskReady)
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
                    _rackPanel.Draw(_spriteBatch, hudSnapshot, PowerPanelOrigin);
                    break;
            }

            _combatPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, ComputeHint(hudSnapshot, _client.PlayerId), CombatPanelOrigin);
            _voyagePanel.Draw(_spriteBatch, hudSnapshot, VoyagePanelOrigin);
            var carriedSlotCount = hudSnapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory?.MainSlots.Count ?? 0;
            _inventoryPanel.Draw(_spriteBatch, hudSnapshot, _client.PlayerId, InventoryRowOrigin(carriedSlotCount), EquipSlotsOrigin);

            // Last, so the item being dragged rides over every panel it passes across.
            if (_dragFrom is { } dragged && ItemInSlot(hudSnapshot, dragged) is { } draggedItem)
                InventoryPanel.DrawDraggedItem(_spriteBatch, _pixel, _font, _designMouse, draggedItem);
        }
        _debugOverlay.Draw(_spriteBatch, _client.LatestSnapshot);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private static string ComputeHint(WorldSnapshot snapshot, int playerId)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        if (me is null)
            return string.Empty;

        if (me.SuitActionRemaining > 0)
            return $"Экипировка... {me.SuitActionRemaining:0.0}с";

        if (snapshot.TurretStates.FirstOrDefault(t => t.MannedByPlayerId == playerId) is { } manned)
            return $"Наводка мышью ({manned.AimDegrees:0}°)  [Space] огонь  [F] встать";

        if (me.IsAtHelm)
            return "[W] ход  [X] назад  [A/D] поворот  [S] стабилизация  [F] встать";

        if (me.OnEnemyShip)
        {
            var boardingPosition = new Vec2(me.X, me.Y);
            var weapon = HeldItemTypes(me.Inventory).FirstOrDefault(WeaponDefinitions.IsWeapon);
            if (!WeaponDefinitions.IsWeapon(weapon))
                return "Нужно оружие в руках!  [WASD] отступить к пробоине";

            // CharacterState carries no RoomId, so the hint derives the room the same way the
            // interior hint already does for breaches - by which room rect contains the position.
            var boardingRoom = snapshot.EnemyShipRooms.FirstOrDefault(r => r.Contains(boardingPosition));
            var inRange = snapshot.EnemyCrew.Any(c => c.Alive && c.RoomId == boardingRoom?.Id &&
                (new Vec2(c.X, c.Y) - boardingPosition).Length() <= WeaponDefinitions.Range(weapon));
            var remaining = snapshot.EnemyCrew.Count(c => c.Alive);
            return inRange
                ? $"[Space] огонь ({ItemDefinitions.DisplayName(weapon)})  Осталось врагов: {remaining}"
                : $"Абордаж. Осталось врагов: {remaining}";
        }

        if (me.OnStation)
        {
            var stationPosition = new Vec2(me.X, me.Y);

            if (snapshot.StationGuards.Any(g => g.Alive && g.Alerted))
                return "Охрана открыла огонь!  [Space] отстреливаться  [WASD] к шлюзу";

            var nearCrate = snapshot.StationCrates.FirstOrDefault(c =>
                !(snapshot.StationCrateStates.FirstOrDefault(s => s.CrateId == c.Id)?.Looted ?? false) &&
                (c.Position - stationPosition).Length() < TurretInteractionRadius);
            if (nearCrate is not null)
                return $"[F] украсть: {ItemDefinitions.DisplayName(nearCrate.Item)} (охрана не должна увидеть)";

            var nearNpc = snapshot.StationNpcs.FirstOrDefault(n =>
                n.Kind != NpcKind.Security && (n.Position - stationPosition).Length() < TurretInteractionRadius);
            if (nearNpc is not null)
                return $"[ЛКМ] поговорить: {nearNpc.Name}";

            var nearGuard = snapshot.StationNpcs.Any(n =>
                n.Kind == NpcKind.Security && (n.Position - stationPosition).Length() < 4f);
            return nearGuard ? "Рядом охрана" : "На станции";
        }

        if (me.IsOutside)
        {
            var evaPosition = new Vec2(me.X, me.Y);
            var holdingCutter = HeldItemTypes(me.Inventory).Contains(ItemType.Cutter);

            var nearbyDropped = snapshot.DroppedItems.Any(d => (d.Position - evaPosition).Length() < TurretInteractionRadius);
            if (nearbyDropped)
                return "[F] подобрать";

            var nearbyDeposit = snapshot.OreDeposits.Any(d =>
                (snapshot.OreDepositStates.FirstOrDefault(s => s.DepositId == d.Id)?.Hp ?? 0f) > 0f &&
                (d.Position - evaPosition).Length() < 3f);
            if (nearbyDeposit)
            {
                // The cutter is aimed and held now, so the hint has to say what's missing: the tool,
                // the tank in it, or nothing at all - just point and hold.
                if (!holdingCutter)
                    return "Нужен резак в руке";
                return me.CutterTank is > 0f
                    ? $"[ЛКМ] резать (баллон: {me.CutterTank:0})"
                    : "В резаке нет кислородного баллона";
            }

            var suitAir = me.SuitTank is { } tank ? $"  Баллон: {tank:0}" : "  БАЛЛОНА НЕТ";
            return me.IsEvaAttached
                ? $"[Space] оттолкнуться (курсором)  Ранец: {me.JetpackFuel:0}{suitAir}"
                : $"В свободном полёте  [WASD] ранец  Ранец: {me.JetpackFuel:0}{suitAir}";
        }

        if (HeldItemTypes(me.Inventory).Contains(ItemType.MedKit) && me.Health < 100f)
            return "[F] использовать аптечку";

        var myPosition = new Vec2(me.X, me.Y);
        var nearTurret = snapshot.Turrets.Any(t => (t.PeriscopePosition - myPosition).Length() < TurretInteractionRadius);
        var nearBallisticTurret = snapshot.Turrets.Any(t =>
            t.WeaponType == TurretWeaponType.Ballistic && (t.PeriscopePosition - myPosition).Length() < TurretInteractionRadius);

        if (me.CarryingAmmoCrate)
            return nearBallisticTurret ? "[F] зарядить орудие" : "Несёте ящик патронов к орудию";

        var nearStorage = snapshot.AmmoStorages.Any(s => (s.Position - myPosition).Length() < TurretInteractionRadius);
        if (nearStorage)
            return "[F] взять ящик патронов";

        var nearToolStation = snapshot.ToolStations.FirstOrDefault(s => (s.Position - myPosition).Length() < TurretInteractionRadius);
        if (nearToolStation is not null)
            return $"[F] взять: {ItemDefinitions.DisplayName(nearToolStation.Item)}";

        var holding = HeldItemTypes(me.Inventory);

        var nearDamagedTurret = snapshot.Turrets.Any(t =>
            (t.PeriscopePosition - myPosition).Length() < TurretInteractionRadius &&
            (snapshot.TurretStates.FirstOrDefault(s => s.Id == t.Id)?.Damaged ?? false));
        if (nearDamagedTurret)
        {
            return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                ? "[F] почини турель"
                : "Нужен гаечный ключ или отвёртка в руке";
        }

        if (nearTurret)
            return "[F] сесть за орудие";

        var nearHelm = (snapshot.HelmConsole.Position - myPosition).Length() < TurretInteractionRadius;
        if (nearHelm)
            return "[F] встать за штурвал";

        var nearDamagedSystem = snapshot.SystemDevices.FirstOrDefault(d =>
            (d.Position - myPosition).Length() < TurretInteractionRadius &&
            (snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == d.Id)?.Damaged ?? false));
        if (nearDamagedSystem is not null)
        {
            return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                ? "[F] почини систему"
                : "Нужен гаечный ключ или отвёртка в руке";
        }

        var nearLocker = snapshot.SuitLockers.Any(l => (l.Position - myPosition).Length() < TurretInteractionRadius);
        if (nearLocker)
            return me.WearingSuit ? "[F] снять скафандр" : "[F] надеть скафандр";

        var myRoom = snapshot.Rooms.FirstOrDefault(r => r.Contains(myPosition));
        var nearBreachedBlock = myRoom is null
            ? null
            : snapshot.WallBlocks.FirstOrDefault(b =>
                b.RoomId == myRoom.Id &&
                (snapshot.WallBlockStates.FirstOrDefault(s => s.Id == b.Id)?.Breached ?? false) &&
                (b.Position - myPosition).Length() < TurretInteractionRadius);
        if (nearBreachedBlock is not null)
        {
            return holding.Contains(ItemType.WeldingTool)
                ? "[F] заварить пробоину"
                : "Нужен сварочный аппарат (обе руки)";
        }

        var nearDoor = snapshot.Doors.Any(d => (d.Position - myPosition).Length() < TurretInteractionRadius);
        var nearOuterDoor = snapshot.AirlockOuterDoors.Any(d => (d.Position - myPosition).Length() < TurretInteractionRadius);

        // The commonest way to be stuck aboard: suit on, socket empty. Said at the door, where the
        // player is standing when they find out nothing happens (World.Eva.cs gates on the tank).
        if (nearOuterDoor && me.WearingSuit && me.SuitTank is null)
            return "В скафандре нет баллона — наружу не выпустит";
        // Aboard a boarded hull the same click matters more: those doors start closed, and opening
        // one lets the breach through into the compartment behind it (World.EnemyAtmosphere.cs).
        if (me.OnEnemyShip &&
            snapshot.EnemyShipDoors.Any(d => (d.Position - myPosition).Length() < TurretInteractionRadius))
            return "[ЛКМ] открыть дверь (стравит воздух)";
        if (nearDoor || nearOuterDoor)
            return "[ЛКМ] открыть/закрыть дверь";

        var roomOxygen = myRoom is null ? 100f : snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == myRoom.Id)?.Oxygen ?? 100f;
        if (roomOxygen < 100f)
            return $"Кислород в отсеке: {roomOxygen:0}";

        return string.Empty;
    }

    private static IReadOnlyCollection<ItemType> HeldItemTypes(InventoryState? inventory) =>
        inventory is null
            ? Array.Empty<ItemType>()
            : inventory.HeldMainSlotIndices.Select(i => inventory.MainSlots[i]).OfType<ItemType>().ToArray();

    protected override void UnloadContent()
    {
        // _session stays null (see the field's own doc comment) until a ship is actually picked on
        // the select screen - closing the window before that would otherwise crash here.
        _session?.Dispose();
        base.UnloadContent();
    }
}
