using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Networking;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

public class Game1 : Game
{
    private const float TurretInteractionRadius = 1.0f; // must match World.InteractionRadius

    private static readonly Vector2 ShipOrigin = new(40, 40);
    private static readonly Vector2 PowerPanelOrigin = new(40, 360);
    private static readonly Vector2 CombatPanelOrigin = new(480, 360);
    private static readonly Vector2 VoyagePanelOrigin = new(250, 12);
    private static readonly Vector2 InventoryPanelOrigin = new(860, 460);
    private static readonly Vector2 GalaxyMapPanelOrigin = new(60, 64);
    private static readonly Vector2 StationPanelOrigin = new(60, 64);
    private static readonly Rectangle VisionDarknessArea = new(0, 0, 1200, 340);

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SoloSession _session = null!;
    private GameClient _client = null!;
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
    private VisionOverlay _visionOverlay = null!;
    private int _selectedPowerSystem = -1;
    private bool _prevInteractDown;
    private bool _prevFireDown;
    private ButtonState _prevLeftMouseButton = ButtonState.Released;
    private ClickTarget _openBlock = ClickTarget.None;
    private string? _talkingToNpcId;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1200,
            PreferredBackBufferHeight = 560,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _session = new SoloSession();
        _client = new GameClient(_session.Connection, _session.PlayerId);

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        var font = Content.Load<SpriteFont>("DebugFont");
        _debugOverlay = new DebugOverlay(font);
        _shipRenderer = new ShipRenderer(GraphicsDevice, font);
        _powerPanel = new PowerPanel(GraphicsDevice, font);
        _combatPanel = new CombatPanel(GraphicsDevice, font);
        _voyagePanel = new VoyagePanel(font);
        _inventoryPanel = new InventoryPanel(GraphicsDevice, font);
        _reactorPanel = new ReactorPanel(GraphicsDevice, font);
        _systemDevicePanel = new SystemDevicePanel(font);
        _galaxyMapPanel = new GalaxyMapPanel(GraphicsDevice, font);
        _stationPanel = new StationPanel(GraphicsDevice, font);
        _visionOverlay = new VisionOverlay(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();

        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
            Exit();

        // Number keys / Q-E only reach the grid while the distribution block is open — you have
        // to "walk up and click in" first (game_design.md section 1).
        var distributionOpen = _openBlock.Kind == BlockKind.Distribution;
        var selection = distributionOpen ? ReadPowerSystemSelection(keyboard) : null;
        if (selection.HasValue)
            _selectedPowerSystem = selection.Value;
        var powerDirection = distributionOpen ? ReadPowerDirection(keyboard) : 0f;
        var powerSystemIndexToSend = distributionOpen ? _selectedPowerSystem : -1;

        var isManning = _client.LatestSnapshot?.TurretStates.Any(t => t.MannedByPlayerId == _client.PlayerId) ?? false;

        var interactDown = keyboard.IsKeyDown(Keys.F);
        var fireDown = keyboard.IsKeyDown(Keys.Space);
        var interactPressed = interactDown && !_prevInteractDown;
        var firePressed = fireDown && !_prevFireDown;
        _prevInteractDown = interactDown;
        _prevFireDown = fireDown;

        var move = isManning ? Vec2.Zero : ReadMoveInput(keyboard);
        var aimDirection = isManning ? ReadAimDirection(keyboard) : 0f;
        var (toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId) = HandleMouseClick(Mouse.GetState());

        _client.SendInput(move, powerSystemIndexToSend, powerDirection, interactPressed, aimDirection, firePressed, toggleHoldSlotIndex, toggleReactorSlotIndex, travelToPointId);
        _client.PollSnapshots();
        CloseBlockIfWalkedAway(_client.LatestSnapshot);

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

    // Reused for aim while manning a turret — movement is locked server-side at that point.
    private static float ReadAimDirection(KeyboardState keyboard)
    {
        float dir = 0;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) dir -= 1;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) dir += 1;
        return dir;
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

    // One left click handles, in priority order: (1) the Barotrauma-style hold strip under an
    // inventory slot, (2) a reactor fuel-rod slot while the reactor is open, (3) a galaxy map
    // point while the navigation console is open, (4) opening/closing a block by clicking it on
    // the ship view (requires standing close), (5) clicking empty space closes whatever's open.
    // Edge-triggered so a held button doesn't spam.
    private (int ToggleHoldSlotIndex, int ToggleReactorSlotIndex, string? TravelToPointId) HandleMouseClick(MouseState mouse)
    {
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevLeftMouseButton == ButtonState.Released;
        _prevLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return (-1, -1, null);

        var snapshot = _client.LatestSnapshot;
        var me = snapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);

        var slotCount = me?.Inventory?.MainSlots.Count ?? 0;
        for (var i = 0; i < slotCount; i++)
        {
            if (InventoryPanel.GetHoldStripRect(i, InventoryPanelOrigin).Contains(mouse.Position))
                return (i, -1, null);
        }

        if (snapshot is null || me is null)
            return (-1, -1, null);

        if (_openBlock.Kind == BlockKind.Reactor)
        {
            for (var i = 0; i < snapshot.Reactor.RodSlots.Count; i++)
            {
                if (ReactorPanel.GetSlotRect(i, PowerPanelOrigin).Contains(mouse.Position))
                    return (-1, i, null);
            }
        }

        if (_openBlock.Kind == BlockKind.Navigation)
        {
            var mapOrigin = GalaxyMapPanel.ComputeMapOrigin(GalaxyMapPanelOrigin, snapshot.GalaxyPoints);
            foreach (var point in snapshot.GalaxyPoints)
            {
                if (GalaxyMapPanel.GetPointRect(point, mapOrigin).Contains(mouse.Position))
                    return (-1, -1, point.Id);
            }
        }

        if (_openBlock.Kind == BlockKind.Station)
        {
            foreach (var npc in snapshot.StationNpcs)
            {
                if (!StationPanel.GetNpcRect(npc, StationPanelOrigin).Contains(mouse.Position))
                    continue;
                _talkingToNpcId = _talkingToNpcId == npc.Id ? null : npc.Id;
                return (-1, -1, null);
            }
        }

        var myPosition = new Vec2(me.X, me.Y);
        bool NearEnough(Vec2 blockPosition) => (blockPosition - myPosition).Length() < TurretInteractionRadius;

        if (NearEnough(snapshot.ReactorBlock.Position) &&
            ShipRenderer.GetBlockRect(snapshot.ReactorBlock.Position, ShipRenderer.BigBlockSize, ShipOrigin).Contains(mouse.Position))
        {
            _openBlock = _openBlock.Kind == BlockKind.Reactor ? ClickTarget.None : ClickTarget.Reactor;
            return (-1, -1, null);
        }

        if (NearEnough(snapshot.DistributionBlock.Position) &&
            ShipRenderer.GetBlockRect(snapshot.DistributionBlock.Position, ShipRenderer.MediumBlockSize, ShipOrigin).Contains(mouse.Position))
        {
            _openBlock = _openBlock.Kind == BlockKind.Distribution ? ClickTarget.None : ClickTarget.Distribution;
            return (-1, -1, null);
        }

        if (NearEnough(snapshot.NavigationConsole.Position) &&
            ShipRenderer.GetBlockRect(snapshot.NavigationConsole.Position, ShipRenderer.MediumBlockSize, ShipOrigin).Contains(mouse.Position))
        {
            _openBlock = _openBlock.Kind == BlockKind.Navigation ? ClickTarget.None : ClickTarget.Navigation;
            return (-1, -1, null);
        }

        // Only actually opens while docked — clicking it mid-flight or mid-fight does nothing.
        if (snapshot.Voyage.Phase == VoyagePhase.Station &&
            NearEnough(snapshot.AirlockConsole.Position) &&
            ShipRenderer.GetBlockRect(snapshot.AirlockConsole.Position, ShipRenderer.MediumBlockSize, ShipOrigin).Contains(mouse.Position))
        {
            _openBlock = _openBlock.Kind == BlockKind.Station ? ClickTarget.None : ClickTarget.Station;
            _talkingToNpcId = null;
            return (-1, -1, null);
        }

        foreach (var device in snapshot.SystemDevices)
        {
            var size = device.System == PowerSystemId.Engine ? ShipRenderer.BigBlockSize : ShipRenderer.NormalBlockSize;
            if (NearEnough(device.Position) && ShipRenderer.GetBlockRect(device.Position, size, ShipOrigin).Contains(mouse.Position))
            {
                _openBlock = _openBlock.Kind == BlockKind.System && _openBlock.System == device.System
                    ? ClickTarget.None
                    : ClickTarget.ForSystem(device.System);
                return (-1, -1, null);
            }
        }

        _openBlock = ClickTarget.None;
        _talkingToNpcId = null;
        return (-1, -1, null);
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

        var myPosition = new Vec2(me.X, me.Y);
        var blockPosition = _openBlock.Kind switch
        {
            BlockKind.Reactor => snapshot.ReactorBlock.Position,
            BlockKind.Distribution => snapshot.DistributionBlock.Position,
            BlockKind.Navigation => snapshot.NavigationConsole.Position,
            BlockKind.Station => snapshot.AirlockConsole.Position,
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
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin();
        if (_client.LatestSnapshot is { } snapshot)
        {
            // The galaxy map / station view take over the ship-interior viewport for as long as
            // they're open — there's nowhere else on screen big enough to put them.
            if (_openBlock.Kind == BlockKind.Navigation)
                _galaxyMapPanel.Draw(_spriteBatch, snapshot, GalaxyMapPanelOrigin);
            else if (_openBlock.Kind == BlockKind.Station)
                _stationPanel.Draw(_spriteBatch, snapshot, StationPanelOrigin, _talkingToNpcId);
            else
            {
                _shipRenderer.Draw(_spriteBatch, snapshot, ShipOrigin, _openBlock);
                _visionOverlay.Draw(_spriteBatch, snapshot, _client.PlayerId, ShipOrigin, VisionDarknessArea);
            }

            // Only one block's terminal is shown at a time, at the same HUD slot — you have to
            // actually be "in" it (game_design.md section 1) rather than seeing everything always.
            switch (_openBlock.Kind)
            {
                case BlockKind.Distribution:
                    _powerPanel.Draw(_spriteBatch, snapshot.Power, snapshot.SystemStates, _selectedPowerSystem, PowerPanelOrigin);
                    break;
                case BlockKind.Reactor:
                    _reactorPanel.Draw(_spriteBatch, snapshot.Reactor, PowerPanelOrigin);
                    break;
                case BlockKind.System:
                    _systemDevicePanel.Draw(_spriteBatch, _openBlock.System, snapshot.Power, snapshot.Shield, snapshot.SystemStates, PowerPanelOrigin);
                    break;
            }

            _combatPanel.Draw(_spriteBatch, snapshot, _client.PlayerId, ComputeHint(snapshot, _client.PlayerId), CombatPanelOrigin);
            _voyagePanel.Draw(_spriteBatch, snapshot, VoyagePanelOrigin);
            _inventoryPanel.Draw(_spriteBatch, snapshot, _client.PlayerId, InventoryPanelOrigin);
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

        if (snapshot.TurretStates.Any(t => t.MannedByPlayerId == playerId))
            return "[A/D] наводка  [Space] огонь  [F] встать";

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

        var nearDamagedSystem = snapshot.SystemDevices.FirstOrDefault(d =>
            (d.Position - myPosition).Length() < TurretInteractionRadius &&
            (snapshot.SystemStates.FirstOrDefault(s => s.System == d.System)?.Damaged ?? false));
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
        _session.Dispose();
        base.UnloadContent();
    }
}
