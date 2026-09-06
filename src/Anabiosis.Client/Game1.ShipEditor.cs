using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Rendering;
using Anabiosis.Server; // SaveStore only - deleting the run save when starting a fresh custom hull
using Anabiosis.Shared.Model;

namespace Anabiosis.Client;

// The Ship Editor - a pre-session screen (MenuScreen.ShipEditor, alongside ShipSelect in Game1.
// Menu.cs) where the player draws their own hull.
//
// Full redo (humble-soaring-cat.md, M76 follow-up, direct user request) - point-primitive tile
// painting instead of authoring a Room rectangle up front: pick a tool (Floor/Wall/Door/Terminal/
// Device), click a tile to place one, same tool again to place another - no catalog stamps, no
// drawn rectangles. Wall additionally supports a press-drag-release straight line for speed. A
// "Zone" is a purely cosmetic named label the player drags over an already-painted group of floor
// tiles AFTER painting - it carries no validation requirement (the user's own explicit answer: a
// device doesn't need to sit inside any particular zone to count).
//
// Canvas state is a real Shared-model TileGrid (TileGrid.cs) - the exact same type Ship.Tiles uses
// server-side - so painting here means exactly what painting means everywhere else in the tile-model
// rewrite, not a parallel reinvention.
//
// Play/Save now DO work off the tile canvas (direct user request, follow-up session) - see
// Game1.ShipEditor.TileBridge.cs, which converts _editorTiles into a CustomShipDefinition on demand
// (rooms from each sealed region's bounding box, doors/airlocks inferred from door tiles, devices
// from _editorDeviceKinds). The old Room-rectangle fields (_editorRooms/_editorDoors/_editorAirlocks/
// _editorDevices) are still written by EnterShipEditor/HandleShipEditorNewClicked below and left in
// place structurally so the Save/Load modal machinery (Game1.ShipEditor.Ships.cs) keeps compiling,
// but nothing reads them any more - BuildEditorDefinition is backed entirely by the tile bridge.
public partial class Game1
{
    private const int ShipEditorBaseCellSize = 24;
    private const int ShipEditorGridCols = 32;
    // Direct user request ("часть меню на скрине была сверху, а менюшка со всеми блоками в самом
    // низу") - Название/Нос/статус/action buttons moved up top (Game1.ShipEditor.Draw.cs's own
    // DrawEditorBottomBar, still that name for history's sake, draws near the title now) and the
    // device-tab panel now sits flush against the true bottom of the design canvas (DeviceTabs.cs's
    // own DevicePanelBottom=556) instead of the old y=496. Once that panel itself got compacted
    // (DeviceItemsTop's own doc comment - no more dead space below each icon), the canvas grew back
    // to fill the freed height instead of leaving it blank between the two.
    private const int ShipEditorGridRows = 14;
    private const float ShipEditorMinZoom = 0.4f;
    private const float ShipEditorMaxZoom = 2.5f;
    private static readonly Rectangle ShipEditorCanvas =
        new(20, 94, ShipEditorGridCols * ShipEditorBaseCellSize, ShipEditorGridRows * ShipEditorBaseCellSize);

    // Direct user request ("сделать чтобы редактор можно было уменьшать и отдалять камеру") - scroll
    // wheel over the canvas scales this; the canvas rect itself stays fixed in screen space (so the
    // sidebar/bottom bar never move), only how many world tiles fit into it changes. World origin
    // (0,0) stays pinned to the canvas's own top-left corner - this is zoom only, no pan, since
    // panning wasn't asked for.
    private float _editorZoom = 1f;
    private int EditorCellSize => (int)Math.Round(ShipEditorBaseCellSize * _editorZoom);
    private int _prevEditorScrollWheelValue;

    // Direct user request ("сделай чтобы при зажатии пкм можно было двигать камеру") - holding RMB
    // and moving pans the world under the fixed canvas rect; a plain right-click (press+release with
    // no real movement) still has to do its existing per-tool job (remove a tile), so a click and a
    // drag on the same button are told apart by how far the mouse actually travelled while held down,
    // not just by the up/down edge. World (0,0) is no longer pinned to the canvas's own corner once
    // this is nonzero - every screen<->world conversion below reads it.
    private Point _editorPanOffset = Point.Zero;
    private Point? _editorPanDragAnchorMouse;
    private Point _editorPanDragAnchorOffset;
    private bool _editorPanDragEngaged;
    private const int EditorPanDragThreshold = 6;

    private enum EditorTool { Floor, Wall, Door, Terminal, Device, Zone, Engine, Compartment }
    private enum EditorAction { Back, New, Save, SaveAs, Load, Play }

    private static readonly float[] EditorForwardOptions = { 0f, 90f, 180f, -90f };

    private EditorTool _editorTool = EditorTool.Floor;
    private static readonly CustomDeviceKind[] EditorDeviceKinds = Enum.GetValues<CustomDeviceKind>();
    private CustomDeviceKind _editorSelectedDeviceKind = EditorDeviceKinds[0];
    // Which Wall-tool variant is currently selected (direct user request - "усиленная стена"/
    // "иллюминатор") - a palette sub-choice, not its own EditorTool, same as _editorSelectedDeviceKind
    // is for the Device tool. Applied by HandleWallToolInput to every tile SetWall places.
    private WallMaterial _editorWallMaterial = WallMaterial.Standard;
    // Which Door-tool variant is selected (direct user request - "дверь занимающая 1 на 2 тайла"):
    // false places one ordinary 1-tile door per click/drag-endpoint, true links exactly the first 2
    // tiles of a drag into one wide door via TileGrid.LinkDoors. See HandleDoorToolInput.
    private bool _editorDoorWide;

    // The tile canvas itself, plus a couple of things TileCell doesn't carry that the editor still
    // needs to know for rendering/removal: which CustomDeviceKind a given device tile actually is
    // (TileCell.DeviceId is just an opaque string, same as the real game - the kind lives one layer
    // up, on ShipDevice there and here in this parallel dictionary), and named zones.
    private TileGrid _editorTiles = new();
    private readonly Dictionary<TileCoord, CustomDeviceKind> _editorDeviceKinds = new();
    // Direct user request ("реактор это устройство 4 на 4 тайла") - the first device with a real
    // footprint bigger than 1x1. Every tile a placed device occupies (all 16 for a 4x4 reactor, just
    // the 1 tile itself for everything else) maps here to its device's own anchor (top-left) tile -
    // _editorDeviceKinds only ever holds ONE entry per device, keyed by that same anchor, so drawing/
    // export/removal all look the device up by anchor and its full occupied-tile set via this map.
    private readonly Dictionary<TileCoord, TileCoord> _editorDeviceFootprint = new();
    // Direct user request ("стеллаж... можно поворачивать") - true for a placed non-square device
    // (StorageRack/LargeStorage/Helm/Navigation) whose own authored Width/Height got swapped before
    // stamping, keyed by the SAME anchor _editorDeviceKinds/_editorDeviceFootprint already use. A
    // square device (every 1x1 kind, the 4x4 Reactor) never needs an entry here at all - swapping
    // equal dimensions changes nothing, so DeviceFootprintSize's own `rotated` flag is simply never
    // consulted for those regardless of what this dictionary says.
    private readonly Dictionary<TileCoord, bool> _editorDeviceRotation = new();
    // R rotates this PENDING flag (cycled before placement, not dragged per-click) - same
    // before-placement convention _editorCompartmentPendingRotation/_editorEnginePendingFacing
    // already use for their own tools.
    private bool _editorDevicePendingRotated;
    private bool _prevDeviceRotateKeyDown;
    // Kind (direct user request - all 4 described zone types, not just one) is set by picking one of
    // the 4 quick-select buttons in the naming prompt instead of typing a name; null means the player
    // typed a free-form name instead - purely cosmetic, exactly like every zone before this existed.
    private sealed record EditorZone(string Name, HashSet<TileCoord> Tiles, ShipZoneKind? Kind = null);
    private readonly List<EditorZone> _editorZones = new();

    // Real Cosmoteer-style engine (ShipEngine.cs) - a directional 3-tile line (Control/Bulkhead/
    // Nozzle), NOT the generic NxN device footprint above, so it gets its own parallel bookkeeping
    // rather than being forced through _editorDeviceKinds/_editorDeviceFootprint (direct user
    // decision, see the session context: the 3 tiles have different per-tile placement preconditions,
    // fighting the generic machinery instead of reusing it). Keyed by the Control tile (the anchor the
    // player actually clicks) - _editorEngineFacing holds one entry per placed engine,
    // _editorEngineFootprint maps every one of its 3 occupied tiles back to that same anchor, same
    // "anchor vs full footprint" split _editorDeviceFootprint already uses for devices.
    private readonly Dictionary<TileCoord, TileSide> _editorEngineFacing = new();
    private readonly Dictionary<TileCoord, TileCoord> _editorEngineFootprint = new();
    // Facing is chosen with a rotate key BEFORE placing, not per-click drag (direct user decision) -
    // this is the tool's own live "what would placing right now produce" state, like
    // _editorSelectedDeviceKind is for the Device tool.
    private TileSide _editorEnginePendingFacing = TileSide.West;
    private bool _prevEngineRotateKeyDown;
    // A middling single constant (RoomCatalog.EnginesFor's own engine-small=5f/engine-big=12f) since
    // the editor only gets ONE engine tool, not several size tiers.
    private const float EngineMaxThrust = 8f;

    // M81 (humble-soaring-cat.md) - wires the already-built CompartmentCatalog/CompartmentPlacer
    // (M80) into the free-tile Ship Editor as its own placeable palette category: pick a variant,
    // rotate with R (same pending-rotation-before-placement convention _editorEnginePendingFacing
    // already uses), click to stamp the whole pre-baked compartment, right-click any of its own tiles
    // to remove the whole thing at once. _editorCompartmentAt/_editorCompartmentTiles are the "anchor
    // vs full footprint" split every other multi-tile placement here already uses (_editorDeviceFootprint,
    // _editorEngineFootprint) - every tile of a placed compartment maps to its own instance id, and
    // that id maps back to the full set of tiles it occupies (floor + wall ring) for removal.
    // _editorCompartmentProtected tracks each instance's own ProtectedTiles (CompartmentPlacer's own
    // core-device/engine/airlock tiles) for a LATER milestone's outfit-mode UI to refuse removing -
    // this milestone only records them, it doesn't enforce anything against them yet.
    private string? _editorSelectedCompartmentId;
    private int _editorCompartmentPendingRotation; // 0-3, cycles on R while EditorTool.Compartment is active
    private bool _prevCompartmentRotateKeyDown;
    private readonly Dictionary<TileCoord, string> _editorCompartmentAt = new();
    private readonly Dictionary<string, HashSet<TileCoord>> _editorCompartmentTiles = new();
    private readonly Dictionary<string, HashSet<TileCoord>> _editorCompartmentProtected = new();
    private int _editorNextCompartmentInstance;

    private (int X, int Y)? _editorFloorDragStart;
    private TileCoord? _editorWallDragStart;
    private (int X, int Y)? _editorZoneDragStart;
    private HashSet<TileCoord>? _editorPendingZoneTiles;
    private bool _editorZoneNamePrompting;
    private string _editorZoneNameInput = "";
    // Set by clicking one of the 4 type quick-select buttons in the naming prompt (direct user
    // request); cleared back to null the moment the player edits the text field by hand, so a typed
    // zone whose name is then hand-edited away from the canonical label doesn't silently keep acting
    // as that type.
    private ShipZoneKind? _editorZonePendingKind;

    // Left over from the old Room-rectangle editor - still written by EnterShipEditor/
    // HandleShipEditorNewClicked below so the Save/Load slot machinery (Game1.ShipEditor.Ships.cs)
    // keeps compiling, but nothing reads them any more - BuildEditorDefinition is backed entirely by
    // the tile canvas (see this class's own doc comment, Game1.ShipEditor.TileBridge.cs).
    private List<CustomRoomDef> _editorRooms = new();
    private List<CustomDoorDef> _editorDoors = new();
    private List<CustomAirlockDef> _editorAirlocks = new();
    private List<CustomDeviceDef> _editorDevices = new();
    private string _editorShipName = "Мой корабль";
    private float _editorForwardDegrees = 0f;
    private int _editorRoomCounter = 1;

    private ButtonState _prevEditorLeftMouseButton = ButtonState.Released;
    private ButtonState _prevEditorRightMouseButton = ButtonState.Released;

    // Reached from the main menu's КАМПАНИЯ section (Game1.Menu.cs) - loads whatever was there last
    // time, or a blank hull the first time. The Room-rectangle fields still load from the real
    // CustomShipStore file (so Save/Load/Play keep working exactly as before on whatever was already
    // saved there); the new tile canvas always starts blank - nothing persists it yet.
    private void EnterShipEditor()
    {
        var loaded = CustomShipStore.Load();
        _editorRooms = loaded?.Rooms.ToList() ?? new List<CustomRoomDef>();
        _editorDoors = loaded?.Doors.ToList() ?? new List<CustomDoorDef>();
        _editorAirlocks = loaded?.Airlocks.ToList() ?? new List<CustomAirlockDef>();
        _editorDevices = loaded?.Devices.ToList() ?? new List<CustomDeviceDef>();
        _editorShipName = loaded?.Name ?? "Мой корабль";
        _editorForwardDegrees = loaded?.ForwardDegrees ?? 0f;
        _editorRoomCounter = NextRoomCounter(_editorRooms);
        // Direct user request ("сохранять построенные корабли между сессиями") - restores the real
        // tile drawing if this scratch slot has one saved; a slot saved before this feature existed
        // (or one that's genuinely never been touched) has no .tiles.json sibling, so this falls
        // back to the blank canvas exactly as before.
        if (CustomShipStore.LoadTileCanvas() is { } savedCanvas)
            ApplyEditorTileCanvas(savedCanvas);
        else
        {
            _editorTiles = new TileGrid();
            _editorDeviceKinds.Clear();
            _editorDeviceFootprint.Clear();
            _editorZones.Clear();
            _editorEngineFacing.Clear();
            _editorEngineFootprint.Clear();
            _editorCompartmentAt.Clear();
            _editorCompartmentTiles.Clear();
            _editorCompartmentProtected.Clear();
        }
        _editorTool = EditorTool.Floor;
        _editorWallMaterial = WallMaterial.Standard;
        _editorDoorWide = false;
        _editorDoorDragStart = null;
        _editorCurrentSlotName = null; // the scratch slot isn't necessarily saved under any name yet
        _editorSaveAsPrompting = false;
        _editorLoadListOpen = false;
        _editorZoneNamePrompting = false;
        _editorZonePendingKind = null;
        _menuScreen = MenuScreen.ShipEditor;
    }

    // Room.Count + 1 collides once a room in the middle has ever been deleted (e.g. rooms
    // room-1..room-6, delete room-3, save+reload: Count is 5 but room-6 still exists, so the next
    // new room would also claim "room-6" - CustomShipValidator's Rooms.ToDictionary(r => r.Id)
    // then throws on the duplicate key). Deriving the next id from the highest surviving suffix
    // instead of the room count is immune to gaps left by deletion.
    private static int NextRoomCounter(List<CustomRoomDef> rooms)
    {
        var max = 0;
        foreach (var room in rooms)
            if (room.Id.StartsWith("room-") && int.TryParse(room.Id.AsSpan(5), out var n) && n > max)
                max = n;
        return max + 1;
    }

    // Derived from the tile canvas (see Game1.ShipEditor.TileBridge.cs) rather than the legacy
    // _editorRooms/_editorDoors/_editorAirlocks/_editorDevices fields - those are only still written
    // by EnterShipEditor/HandleShipEditorNewClicked below so Load keeps compiling, nothing reads them.
    private CustomShipDefinition BuildEditorDefinition() => BuildAndValidateEditorDefinition().Definition;

    private void SaveEditorDefinition()
    {
        CustomShipStore.Save(BuildEditorDefinition());
        CustomShipStore.SaveTileCanvas(BuildEditorTileCanvas());
    }

    private void HandleShipEditorScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var leftDown = mouse.LeftButton == ButtonState.Pressed;
        var leftClicked = leftDown && _prevEditorLeftMouseButton == ButtonState.Released;
        var leftReleased = !leftDown && _prevEditorLeftMouseButton == ButtonState.Pressed;

        var rightDown = mouse.RightButton == ButtonState.Pressed;
        var rightPressed = rightDown && _prevEditorRightMouseButton == ButtonState.Released;
        var rightReleased = !rightDown && _prevEditorRightMouseButton == ButtonState.Pressed;
        _prevEditorLeftMouseButton = mouse.LeftButton;
        _prevEditorRightMouseButton = mouse.RightButton;

        if (rightPressed && ShipEditorCanvas.Contains(_designMouse))
        {
            _editorPanDragAnchorMouse = _designMouse;
            _editorPanDragAnchorOffset = _editorPanOffset;
            _editorPanDragEngaged = false;
        }
        if (rightDown && _editorPanDragAnchorMouse is { } panAnchor)
        {
            var dx = _designMouse.X - panAnchor.X;
            var dy = _designMouse.Y - panAnchor.Y;
            if (_editorPanDragEngaged || Math.Abs(dx) > EditorPanDragThreshold || Math.Abs(dy) > EditorPanDragThreshold)
            {
                _editorPanDragEngaged = true;
                _editorPanOffset = new Point(_editorPanDragAnchorOffset.X - dx, _editorPanDragAnchorOffset.Y - dy);
            }
        }
        // A drag that crossed the threshold was a pan, not a click - suppress the per-tool removal
        // that a bare right-click would otherwise trigger. Fires on release rather than press (unlike
        // the old edge-triggered version) since there's no way to know it was "just a click" any
        // earlier than that.
        var rightClicked = rightReleased && !_editorPanDragEngaged;
        if (rightReleased)
        {
            _editorPanDragAnchorMouse = null;
            _editorPanDragEngaged = false;
        }

        var scrollDelta = mouse.ScrollWheelValue - _prevEditorScrollWheelValue;
        _prevEditorScrollWheelValue = mouse.ScrollWheelValue;
        if (scrollDelta != 0 && ShipEditorCanvas.Contains(_designMouse))
        {
            const float stepPerNotch = 0.1f;
            _editorZoom = Math.Clamp(_editorZoom + scrollDelta / 120f * stepPerNotch, ShipEditorMinZoom, ShipEditorMaxZoom);
        }

        if (_editorZoneNamePrompting)
        {
            HandleEditorZoneNamePromptInput(keyboard, leftClicked);
            return;
        }
        if (_editorSaveAsPrompting)
        {
            HandleEditorSaveAsPromptInput(keyboard, leftClicked);
            return;
        }
        if (_editorLoadListOpen)
        {
            HandleEditorLoadListInput(leftClicked);
            return;
        }

        if (HandleShipEditorSidebarClick(leftClicked))
            return;

        switch (_editorTool)
        {
            case EditorTool.Floor:
                HandleFloorToolInput(leftClicked, leftReleased, rightClicked);
                break;
            case EditorTool.Wall:
                HandleWallToolInput(leftClicked, leftReleased, rightClicked);
                break;
            case EditorTool.Door:
                HandleDoorToolInput(leftClicked, leftReleased, rightClicked);
                break;
            case EditorTool.Terminal:
                HandleTerminalToolInput(leftClicked, rightClicked);
                break;
            case EditorTool.Device:
                HandleDeviceToolInput(leftClicked, rightClicked, keyboard);
                break;
            case EditorTool.Zone:
                HandleZoneToolInput(leftClicked, leftReleased);
                break;
            case EditorTool.Engine:
                HandleEngineToolInput(leftClicked, rightClicked, keyboard);
                break;
            case EditorTool.Compartment:
                HandleCompartmentToolInput(leftClicked, rightClicked, keyboard);
                break;
        }
    }

    // Press-drag-release fills the whole rectangle between start and release (direct user request -
    // "чтобы создать квадрат или линию"): a drag that never leaves its own row or column IS a line,
    // a drag with both axes moving IS a square/rectangle - one rule covers both, no separate mode.
    // Right-click still removes a single tile at a time (not asked for, left as-is).
    private void HandleFloorToolInput(bool leftClicked, bool leftReleased, bool rightClicked)
    {
        if (rightClicked)
        {
            if (GridCellAt(_designMouse) is { } removeCell)
                _editorTiles.SetFloor(new TileCoord(removeCell.X, removeCell.Y), false);
            return;
        }

        if (leftClicked)
        {
            _editorFloorDragStart = GridCellAt(_designMouse);
            return;
        }

        if (!leftReleased || _editorFloorDragStart is not { } start)
            return;
        _editorFloorDragStart = null;

        var endCell = GridCellAt(_designMouse) ?? start;
        var minX = Math.Min(start.X, endCell.X);
        var minY = Math.Min(start.Y, endCell.Y);
        var maxX = Math.Max(start.X, endCell.X);
        var maxY = Math.Max(start.Y, endCell.Y);
        for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY; y++)
                _editorTiles.SetFloor(new TileCoord(x, y), true);
    }

    // Point placement (one click, one tile) plus a line drag for speed (direct user request) - mouse-
    // down latches the start tile, mouse-up fills every tile along the longer axis between start and
    // the release point (a straight horizontal or vertical run, not a diagonal Bresenham line - the
    // simplest thing that covers "wall a corridor" without inventing a stairstep convention). Every
    // filled tile still needs a floor already there (TileGrid.SetWall's own precondition) - tiles
    // without one are silently skipped rather than refusing the whole line.
    private void HandleWallToolInput(bool leftClicked, bool leftReleased, bool rightClicked)
    {
        if (rightClicked)
        {
            if (GridCellAt(_designMouse) is { } cell && _editorTiles.CellAt(new TileCoord(cell.X, cell.Y)) is { Wall: not TileWallKind.None })
            {
                var coord = new TileCoord(cell.X, cell.Y);
                // Removing a wall tile that's currently serving as some engine's Bulkhead (see
                // HandleEngineToolInput's own doc comment) would otherwise leave that engine facing a
                // non-wall - a dangling invalid state. Take the engine with it instead.
                if (_editorEngineFootprint.TryGetValue(coord, out var engineAnchor))
                    RemoveEngineAt(engineAnchor);
                _editorTiles.SetWall(coord, TileWallKind.None);
            }
            return;
        }

        if (leftClicked)
        {
            if (GridCellAt(_designMouse) is { } cell)
                _editorWallDragStart = new TileCoord(cell.X, cell.Y);
            return;
        }

        if (!leftReleased || _editorWallDragStart is not { } start)
            return;
        _editorWallDragStart = null;

        var end = GridCellAt(_designMouse) is { } endCell ? new TileCoord(endCell.X, endCell.Y) : start;
        foreach (var coord in LineBetween(start, end))
        {
            // A device already occupies this tile's floor slot (TileGrid.PlaceDevice's own
            // precondition forbids the reverse order too) - skip it rather than silently stacking a
            // wall on top, direct user request ("на месте которое занимает устройство уже ничего
            // нельзя было построить").
            if (_editorTiles.CellAt(coord) is not { HasFloor: true, DeviceId: null })
                continue;
            _editorTiles.SetWall(coord, TileWallKind.Solid, material: _editorWallMaterial);
            EvictTerminalsAtJunctions(coord);
        }
    }

    // Whichever axis has the bigger span wins (an axis-aligned run, not a diagonal) - a release with
    // no real drag (a bare click) still yields exactly the one start tile.
    private static IEnumerable<TileCoord> LineBetween(TileCoord start, TileCoord end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            var step = Math.Sign(dx);
            if (step == 0) { yield return start; yield break; }
            for (var x = start.X; x != end.X + step; x += step)
                yield return new TileCoord(x, start.Y);
        }
        else
        {
            var step = Math.Sign(dy);
            for (var y = start.Y; y != end.Y + step; y += step)
                yield return new TileCoord(start.X, y);
        }
    }

    private TileCoord? _editorDoorDragStart;

    // A door is its own toggleable wall variant (TileGrid.cs) - can go straight onto bare floor, or
    // replace an existing solid wall. Clicking an existing door removes it back to bare floor
    // (there's nothing else for this tool to do to a door tile, so both directions share one button).
    // Wide mode (_editorDoorWide, direct user request - "дверь занимающая 1 на 2 тайла") reuses the
    // Wall tool's own drag-a-line gesture, but only ever links the FIRST 2 tiles of that drag into
    // one door (TileGrid.LinkDoors) - a longer drag doesn't chain more doors, and a bare click (no
    // real drag) still places one ordinary single-tile door.
    private void HandleDoorToolInput(bool leftClicked, bool leftReleased, bool rightClicked)
    {
        if (rightClicked)
        {
            if (GridCellAt(_designMouse) is not { } removeCell)
                return;
            var removeCoord = new TileCoord(removeCell.X, removeCell.Y);
            if (_editorTiles.CellAt(removeCoord) is not { Wall: TileWallKind.Door } current)
                return;
            // M83 - a Docking compartment's own airlock door tile is a protected core tile (per
            // CompartmentPlacer.Stamp's own ProtectedTiles) and must never be touched by this tool at
            // all - not converted to None, not "restored" to Solid either (M82's own RestoreKind logic
            // below), it just can't be removed while the compartment stands.
            if (IsProtectedCompartmentCore(removeCoord))
                return;
            // M82 - a door that replaced one of a compartment's own wall-ring tiles (still tracked in
            // _editorCompartmentAt from M81's placement bookkeeping) reseals back to a Solid wall on
            // removal, not bare None - None would punch a permanent hole straight through the hull
            // between the two compartments' own interiors, silently merging their regions. An ordinary
            // free-tile-painted door with no compartment involvement at all keeps today's plain-floor
            // behavior exactly as before. Each tile of a linked wide-door pair is judged independently
            // by its OWN membership, not a single shared decision for the pair.
            TileWallKind RestoreKind(TileCoord tile) =>
                _editorCompartmentAt.ContainsKey(tile) ? TileWallKind.Solid : TileWallKind.None;

            // Removing one tile of a linked wide door takes its partner with it - the pair reads as
            // ONE door to the player, not two narrow ones that happen to touch.
            if (current.DoorGroupId is { } groupId)
                foreach (var partner in _editorTiles.Cells
                    .Where(kv => kv.Value.DoorGroupId == groupId && kv.Key != removeCoord)
                    .Select(kv => kv.Key).ToList())
                    _editorTiles.SetWall(partner, RestoreKind(partner));
            _editorTiles.SetWall(removeCoord, RestoreKind(removeCoord));
            return;
        }

        if (!_editorDoorWide)
        {
            if (!leftClicked || GridCellAt(_designMouse) is not { } cell)
                return;
            var coord = new TileCoord(cell.X, cell.Y);
            if (_editorTiles.CellAt(coord) is not { HasFloor: true, DeviceId: null })
                return;
            _editorTiles.SetWall(coord, TileWallKind.Door);
            EvictTerminalsAtJunctions(coord);
            return;
        }

        if (leftClicked)
        {
            if (GridCellAt(_designMouse) is { } cell)
                _editorDoorDragStart = new TileCoord(cell.X, cell.Y);
            return;
        }
        if (!leftReleased || _editorDoorDragStart is not { } start)
            return;
        _editorDoorDragStart = null;

        var end = GridCellAt(_designMouse) is { } endCell ? new TileCoord(endCell.X, endCell.Y) : start;
        var span = LineBetween(start, end).Take(2).ToList();

        // M82 (humble-soaring-cat.md) - direct user rule: a door may replace the wall on the boundary
        // between two ALREADY-PLACED compartments only if that boundary is exactly 2 tiles long and
        // neither of those tiles borders open space/vacuum (a genuine interior seam, never a stretch
        // of the outer hull). Tried FIRST, on the whole 2-tile span at once - both tiles must
        // independently qualify (TryResolveCompartmentBoundaryDoor) AND agree on the very same
        // compartment pair, so a drag can't straddle a corner into a third compartment. If it doesn't
        // apply (not a wall at all, an ordinary hull wall, or a corner), fall through unchanged to the
        // original floor-based interpretation below - the two cases are mutually exclusive per tile (a
        // tile is either bare floor or already carries a wall), so there's no ambiguity about which
        // one a given drag means. No partial application: either both tiles convert, or neither does.
        if (span.Count == 2
            && TryResolveCompartmentBoundaryDoor(span[0], out var boundaryOwnerA, out var boundaryOwnerB)
            && TryResolveCompartmentBoundaryDoor(span[1], out var otherOwnerA, out var otherOwnerB)
            && boundaryOwnerA == otherOwnerA && boundaryOwnerB == otherOwnerB)
        {
            foreach (var spanCoord in span)
            {
                _editorTiles.SetWall(spanCoord, TileWallKind.Door);
                EvictTerminalsAtJunctions(spanCoord);
            }
            _editorTiles.LinkDoors(span[0], span[1]);
            return;
        }

        var placed = new List<TileCoord>();
        foreach (var spanCoord in span)
        {
            if (_editorTiles.CellAt(spanCoord) is not { HasFloor: true, DeviceId: null })
                continue;
            _editorTiles.SetWall(spanCoord, TileWallKind.Door);
            EvictTerminalsAtJunctions(spanCoord);
            placed.Add(spanCoord);
        }
        if (placed.Count == 2)
            _editorTiles.LinkDoors(placed[0], placed[1]);
    }

    // M82 - does `coord` sit on a genuine interior seam between two already-placed compartments? Only
    // true for a Solid wall tile that is (a) part of some compartment's own wall ring (per M81's
    // _editorCompartmentAt tracking, which covers a compartment's FULL footprint including its wall
    // tiles), (b) has EXACTLY ONE neighbor that is that same compartment's own open interior floor
    // (the "inward" side - zero means this isn't really a ring tile, two means it's a CORNER tile,
    // where the wall ring never actually touches the inset interior floor at all; doors are only valid
    // on straight, non-corner segments), and (c) has, on the OPPOSITE ("outward") side, a DIFFERENT
    // already-placed compartment's own open floor - not real exterior/vacuum (no owner at all), not
    // the same compartment somehow, and not itself another wall. On success, ownerA/ownerB are the two
    // compartments' own instance ids (ownerA is whichever owns `coord` itself).
    private bool TryResolveCompartmentBoundaryDoor(TileCoord coord, out string ownerA, out string ownerB)
    {
        ownerA = "";
        ownerB = "";
        if (_editorTiles.CellAt(coord) is not { Wall: TileWallKind.Solid })
            return false;
        if (!_editorCompartmentAt.TryGetValue(coord, out var owner))
            return false;

        TileSide? inward = null;
        foreach (var side in TileSideExtensions.All)
        {
            var neighbor = side.Offset(coord);
            if (_editorCompartmentAt.TryGetValue(neighbor, out var neighborOwner) && neighborOwner == owner
                && _editorTiles.CellAt(neighbor) is { HasFloor: true, Wall: TileWallKind.None })
            {
                if (inward is not null)
                    return false; // a second match - this is a corner tile, reject
                inward = side;
            }
        }
        if (inward is not { } inwardSide)
            return false; // zero matches - not a straight ring tile of its own compartment

        var outward = inwardSide.Opposite().Offset(coord);
        if (!_editorCompartmentAt.TryGetValue(outward, out var outwardOwner) || outwardOwner == owner)
            return false; // real exterior/vacuum, or somehow still the same compartment
        if (_editorTiles.CellAt(outward) is not { HasFloor: true, Wall: TileWallKind.None })
            return false; // not genuinely open floor on the other side

        ownerA = owner;
        ownerB = outwardOwner;
        return true;
    }

    // Mounts to whichever side actually has a wall/door neighbor (TileGrid.PlaceTerminal's own
    // precondition), checked in a fixed North/South/East/West order rather than asking the player to
    // aim at a specific edge - a terminal tile usually only has one wall-kind neighbor to begin with.
    // Refused entirely (direct user request) if the tile itself sits at a construction junction - see
    // IsAtConstructionJunction's own doc comment.
    private void HandleTerminalToolInput(bool leftClicked, bool rightClicked)
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var coord = new TileCoord(cell.X, cell.Y);
        if (rightClicked)
        {
            if (_editorTiles.CellAt(coord) is { TerminalId: not null })
                _editorTiles.RemoveTerminal(coord);
            return;
        }
        if (!leftClicked || _editorTiles.CellAt(coord) is not { HasFloor: true, TerminalId: null })
            return;
        if (IsAtConstructionJunction(coord))
            return;
        foreach (var side in TileSideExtensions.All)
        {
            if (_editorTiles.CellAt(side.Offset(coord)) is not { Wall: not TileWallKind.None })
                continue;
            _editorTiles.PlaceTerminal(coord, side, $"terminal-{coord.X}-{coord.Y}");
            return;
        }
    }

    // Something meant to hang flat against a single wall (today, only Terminal) reads as wrong if
    // its own tile ALSO touches a second construction (wall or device) on a side PERPENDICULAR to
    // whichever side it would mount to - it would sit right in the corner where the two meet,
    // whichever combination that is (direct user request: "2 стены, стена и устройство, 2
    // устройства" - all three are just "wall or device" on two adjacent sides at once). Opposite
    // sides both occupied (a plain 1-wide corridor between two parallel walls) is fine - only
    // ADJACENT pairs count as a seam, not a straight run.
    private bool IsAtConstructionJunction(TileCoord coord)
    {
        bool IsConstruction(TileSide side) =>
            _editorTiles.CellAt(side.Offset(coord)) is { } c && (c.Wall != TileWallKind.None || c.DeviceId != null);

        var north = IsConstruction(TileSide.North);
        var south = IsConstruction(TileSide.South);
        var east = IsConstruction(TileSide.East);
        var west = IsConstruction(TileSide.West);

        return (north && east) || (north && west) || (south && east) || (south && west);
    }

    // IsAtConstructionJunction only guards a terminal at the moment it's PLACED - it says nothing
    // about a wall/door/device added AFTERWARD right next to an already-placed terminal, which can
    // just as easily turn that terminal's tile into a junction (build a straight wall, place a
    // terminal on it, then extend the wall into a corner beside it - direct user report, a terminal
    // ended up sitting between what read as 2 walls). Every call site that adds new construction
    // re-checks its own immediate neighbors afterward and evicts any terminal that no longer
    // qualifies, rather than leaving a stale one behind. Removing construction never needs this - it
    // can only ever resolve a junction, not create one.
    private void EvictTerminalsAtJunctions(TileCoord coord)
    {
        foreach (var side in TileSideExtensions.All)
        {
            var neighbor = side.Offset(coord);
            if (_editorTiles.CellAt(neighbor) is { TerminalId: not null } && IsAtConstructionJunction(neighbor))
                _editorTiles.RemoveTerminal(neighbor);
        }
    }

    // A device needs bare floor on EVERY tile of its footprint (no wall/door/other device already on
    // any of them, TileGrid.PlaceDevice's own precondition, checked tile-by-tile before placing any
    // of them) - CustomDeviceKind itself isn't stored on TileCell at all (it's an opaque DeviceId
    // there, same as the real game), so _editorDeviceKinds/_editorDeviceFootprint are the parallel
    // lookups the renderer/removal/tile-bridge export all need. The clicked tile is the footprint's
    // own top-left anchor (simplest, most predictable convention - no centering guesswork).
    private void HandleDeviceToolInput(bool leftClicked, bool rightClicked, KeyboardState keyboard)
    {
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevDeviceRotateKeyDown)
            _editorDevicePendingRotated = !_editorDevicePendingRotated;
        _prevDeviceRotateKeyDown = rDown;

        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var coord = new TileCoord(cell.X, cell.Y);
        if (rightClicked)
        {
            if (_editorDeviceFootprint.TryGetValue(coord, out var anchor))
            {
                // M83 - refuse the whole removal (not a partial one) if this device's own anchor tile
                // is some still-placed compartment's protected core device. In practice every
                // compartment-placed device is stamped as its own 1x1 footprint (CompartmentPlacer.
                // Stamp's own PlacedDevice.Coord IS the anchor _editorDeviceKinds/_editorDeviceFootprint
                // key - see HandleCompartmentToolInput above), so checking the anchor alone is exact,
                // not just an approximation for the multi-tile case (e.g. a free-tile-painted 4x4
                // Reactor with no compartment involvement at all is never in _editorCompartmentAt to
                // begin with, so this never blocks that).
                if (IsProtectedCompartmentCore(anchor))
                    return;
                var wasRotated = _editorDeviceRotation.TryGetValue(anchor, out var rotatedFlag) && rotatedFlag;
                var (removeWidth, removeHeight) = DeviceFootprintSize(_editorDeviceKinds[anchor], wasRotated);
                foreach (var occupied in DeviceFootprintTiles(anchor, removeWidth, removeHeight))
                {
                    _editorTiles.RemoveDevice(occupied);
                    _editorDeviceFootprint.Remove(occupied);
                }
                _editorDeviceKinds.Remove(anchor);
                _editorDeviceRotation.Remove(anchor);
            }
            return;
        }
        if (!leftClicked)
            return;
        var (width, height) = DeviceFootprintSize(_editorSelectedDeviceKind, _editorDevicePendingRotated);
        var placeAnchor = FootprintAnchorFor(coord, width, height);
        var footprint = DeviceFootprintTiles(placeAnchor, width, height).ToList();
        if (footprint.Any(t => _editorTiles.CellAt(t) is not { HasFloor: true, Wall: TileWallKind.None, DeviceId: null }))
            return;
        var deviceId = $"device-{placeAnchor.X}-{placeAnchor.Y}";
        foreach (var occupied in footprint)
        {
            _editorTiles.PlaceDevice(occupied, deviceId);
            _editorDeviceFootprint[occupied] = placeAnchor;
        }
        _editorDeviceKinds[placeAnchor] = _editorSelectedDeviceKind;
        if (_editorDevicePendingRotated)
            _editorDeviceRotation[placeAnchor] = true;
        foreach (var occupied in footprint)
            EvictTerminalsAtJunctions(occupied);
    }

    // Direct user request ("стеллаж... можно поворачивать") - a non-square device (StorageRack/
    // LargeStorage/Helm/Navigation) can be placed rotated 90 degrees, swapping its own authored
    // Width/Height (CustomDeviceFootprint.Size) - a SQUARE device (every 1x1 kind, and the 4x4
    // Reactor) ignores rotation entirely, since swapping equal dimensions changes nothing.
    private static (int Width, int Height) DeviceFootprintSize(CustomDeviceKind kind, bool rotated = false)
    {
        var (width, height) = CustomDeviceFootprint.Size(kind);
        return rotated ? (height, width) : (width, height);
    }

    private static IEnumerable<TileCoord> DeviceFootprintTiles(TileCoord anchor, int width, int height)
    {
        for (var dx = 0; dx < width; dx++)
            for (var dy = 0; dy < height; dy++)
                yield return new TileCoord(anchor.X + dx, anchor.Y + dy);
    }

    // Direct user request ("привязана не к краю а к центру") - the tile under the cursor becomes the
    // footprint's CENTER, not its top-left corner (matching RimWorld-style placement, where a big
    // object follows the cursor from its own middle). For an even size like 4, there's no single
    // centre tile, so the anchor lands 2 tiles back on each axis - the cursor's own tile ends up one
    // of the 4 centre-most tiles rather than a true geometric midpoint, the closest an even footprint
    // can get. Returns the top-left anchor DeviceFootprintTiles/_editorDeviceFootprint already key on
    // internally - a 1x1 device (size 1x1) resolves to anchor == clicked, unchanged from before.
    private static TileCoord FootprintAnchorFor(TileCoord clicked, int width, int height) =>
        new(clicked.X - width / 2, clicked.Y - height / 2);

    // The engine's own 3-tile line: Control (the clicked anchor), Bulkhead 1 tile further in
    // `facing`, Nozzle 2 tiles further - exactly ShipEngine.cs's own ControlPosition/BulkheadPosition/
    // NozzlePosition convention, just in integer tile space instead of continuous Vec2 world units.
    private static IEnumerable<TileCoord> EngineFootprintTiles(TileCoord control, TileSide facing)
    {
        yield return control;
        var bulkhead = facing.Offset(control);
        yield return bulkhead;
        yield return facing.Offset(bulkhead);
    }

    // R rotates the PENDING facing (cycled before placement, not dragged per-click - direct user
    // decision). Control needs bare floor, same precondition every device needs. Bulkhead must
    // ALREADY be a Solid wall tile the player painted themselves - that's how the player marks "this
    // is my hull edge," and it can't collide with anything else since a Solid wall tile can't also
    // host a device/floor-only content. Nozzle must be genuinely open (no floor tile there) - real
    // exterior space beyond the hull. Ship.cs's constructor already excludes the auto-generated
    // WallBlock that would otherwise coincide with the Bulkhead position (Engines.Any(e =>
    // (e.BulkheadPosition - b.Position).Length() < 0.1)), so this needs no further server-side wiring.
    private void HandleEngineToolInput(bool leftClicked, bool rightClicked, KeyboardState keyboard)
    {
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevEngineRotateKeyDown)
        {
            _editorEnginePendingFacing = _editorEnginePendingFacing switch
            {
                TileSide.West => TileSide.North,
                TileSide.North => TileSide.East,
                TileSide.East => TileSide.South,
                _ => TileSide.West,
            };
        }
        _prevEngineRotateKeyDown = rDown;

        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var control = new TileCoord(cell.X, cell.Y);

        if (rightClicked)
        {
            if (_editorEngineFootprint.TryGetValue(control, out var anchor))
                RemoveEngineAt(anchor);
            return;
        }
        if (!leftClicked)
            return;

        var facing = _editorEnginePendingFacing;
        var bulkhead = facing.Offset(control);
        var nozzle = facing.Offset(bulkhead);

        if (_editorTiles.CellAt(control) is not { HasFloor: true, Wall: TileWallKind.None, DeviceId: null })
            return;
        if (_editorTiles.CellAt(bulkhead) is not { Wall: TileWallKind.Solid })
            return;
        if (_editorTiles.CellAt(nozzle) is { HasFloor: true })
            return;
        // Two engines can never legally share ANY tile of their 3-tile line - most importantly the
        // Bulkhead, since a second engine placed against the same hull-wall tile would silently
        // overwrite the first's facing/footprint bookkeeping the moment it's removed.
        if (EngineFootprintTiles(control, facing).Any(_editorEngineFootprint.ContainsKey))
            return;

        var deviceId = $"engine-{control.X}-{control.Y}";
        _editorTiles.PlaceDevice(control, deviceId);
        _editorEngineFacing[control] = facing;
        foreach (var t in EngineFootprintTiles(control, facing))
            _editorEngineFootprint[t] = control;
    }

    private void RemoveEngineAt(TileCoord anchor)
    {
        // M83 - `anchor` is the engine's own Control tile (_editorEngineFacing's own key convention,
        // set at placement time in HandleEngineToolInput/HandleCompartmentToolInput), which is one of
        // the 3 tiles CompartmentPlacer.Stamp marks protected for a baked engine assembly - refuse
        // before any mutation if it's still a still-placed compartment's own protected core.
        if (IsProtectedCompartmentCore(anchor))
            return;
        if (!_editorEngineFacing.TryGetValue(anchor, out var facing))
            return;
        foreach (var t in EngineFootprintTiles(anchor, facing))
            _editorEngineFootprint.Remove(t);
        _editorEngineFacing.Remove(anchor);
        _editorTiles.RemoveDevice(anchor);
    }

    // R rotates the PENDING rotation step (0-3), same before-placement convention the Engine tool's
    // own _editorEnginePendingFacing already uses. The clicked tile is the rotated footprint's own
    // CENTER (FootprintAnchorFor's own convention for the Device tool) rather than its top-left corner
    // - the anchor CompartmentPlacer.Stamp itself wants is derived by walking back Width/2,Height/2
    // from the clicked tile, same math FootprintAnchorFor already uses for square devices.
    private void HandleCompartmentToolInput(bool leftClicked, bool rightClicked, KeyboardState keyboard)
    {
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevCompartmentRotateKeyDown)
            _editorCompartmentPendingRotation = (_editorCompartmentPendingRotation + 1) % 4;
        _prevCompartmentRotateKeyDown = rDown;

        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var clicked = new TileCoord(cell.X, cell.Y);

        if (rightClicked)
        {
            if (_editorCompartmentAt.TryGetValue(clicked, out var instanceId))
                RemoveCompartmentAt(instanceId);
            return;
        }
        if (!leftClicked || _editorSelectedCompartmentId is not { } compartmentId)
            return;
        if (CompartmentCatalog.Find(compartmentId) is not { } entry)
            return;

        var rotated = CompartmentPlacer.Rotate(entry, _editorCompartmentPendingRotation);
        var anchor = new TileCoord(clicked.X - rotated.Width / 2, clicked.Y - rotated.Height / 2);
        var instance = $"compartment-{_editorNextCompartmentInstance++}";
        var result = CompartmentPlacer.Stamp(_editorTiles, entry, anchor, _editorCompartmentPendingRotation, instance);
        if (!result.Success)
            return; // silent reject, same convention every other tool's own precondition checks use

        foreach (var device in result.Devices)
        {
            _editorDeviceKinds[device.Coord] = device.Kind;
            if (device.Rotated)
                _editorDeviceRotation[device.Coord] = true;
            var (deviceWidth, deviceHeight) = DeviceFootprintSize(device.Kind, device.Rotated);
            foreach (var occupied in DeviceFootprintTiles(device.Coord, deviceWidth, deviceHeight))
                _editorDeviceFootprint[occupied] = device.Coord;
        }
        foreach (var engine in result.Engines)
        {
            _editorEngineFacing[engine.ControlCoord] = engine.Facing;
            foreach (var t in EngineFootprintTiles(engine.ControlCoord, engine.Facing))
                _editorEngineFootprint[t] = engine.ControlCoord;
        }

        // Every tile the compartment actually occupies - its real FootprintRects tiles (floor + wall
        // ring), not just the bounding box (M91 follow-up: a non-rectangular entry like reactor-d
        // has cut-corner tiles inside its bbox that Stamp never actually floors at all).
        var allTiles = new HashSet<TileCoord>();
        foreach (var footprintRect in rotated.FootprintRects)
            for (var x = (int)footprintRect.X; x < (int)footprintRect.Right; x++)
                for (var y = (int)footprintRect.Y; y < (int)footprintRect.Bottom; y++)
                    allTiles.Add(new TileCoord(anchor.X + x, anchor.Y + y));

        // Direct user request ("система отсеков по-другому") - a wall-ring tile is now allowed to
        // coincide with an EXISTING compartment's own wall tile (CompartmentPlacer.Stamp's own new
        // overlap rule); that shared tile stays owned by whichever compartment claimed it FIRST, so
        // this placement's own bookkeeping must not steal it - removing this NEW compartment later
        // must never clear a tile the earlier one still depends on. (The reverse - removing the
        // EARLIER compartment while this one still needs the shared tile - is a known, accepted
        // limitation: the tile really is singular, "doesn't matter which stays" per the user's own
        // answer, and removal wasn't part of that request.)
        var ownedTiles = new HashSet<TileCoord>();
        foreach (var t in allTiles)
        {
            if (_editorCompartmentAt.ContainsKey(t))
                continue; // already owned by another, still-standing compartment - leave it be
            _editorCompartmentAt[t] = instance;
            ownedTiles.Add(t);
        }
        _editorCompartmentTiles[instance] = ownedTiles;
        _editorCompartmentProtected[instance] = new HashSet<TileCoord>(result.ProtectedTiles);
    }

    // M83 - true if `coord` is a still-placed compartment's own protected "core" tile (its core device,
    // any tile of a baked engine assembly, or a Docking compartment's own airlock door) - per the user's
    // own rule, this specific tile can never be individually demolished while the rest of its compartment
    // stands. Removing the WHOLE compartment (RemoveCompartmentAt below, the Compartment tool's own
    // right-click) is a different, unrestricted action and does NOT go through this check - only
    // single-tile removal tools (Device/Engine/Door) do.
    private bool IsProtectedCompartmentCore(TileCoord coord) =>
        _editorCompartmentAt.TryGetValue(coord, out var instanceId)
        && _editorCompartmentProtected.TryGetValue(instanceId, out var protectedTiles)
        && protectedTiles.Contains(coord);

    // The one genuinely tricky part of removal: CompartmentPlacer.Stamp's own placement-time wall
    // dedup (M80) means a compartment placed touching an EARLIER one never grew its own wall on the
    // shared boundary - it deduped down to plain floor there instead, leaving the earlier compartment's
    // own wall as the sole 1-tile separator. Removing THIS compartment (whichever one it is) is safe on
    // its own shared-boundary tiles that still carry a wall (nothing else depended on them), but if a
    // NEIGHBORING, still-standing compartment was the one whose ring got deduped away against THIS one
    // (i.e. this compartment was placed FIRST and the neighbor came second, deduping against it), that
    // neighbor's boundary now has a hole where this compartment's own wall used to be its shared
    // separator - repaint a fresh wall there before this compartment's own tiles are cleared.
    private void RemoveCompartmentAt(string instanceId)
    {
        if (!_editorCompartmentTiles.TryGetValue(instanceId, out var tiles))
            return;

        foreach (var coord in tiles)
        {
            if (_editorTiles.CellAt(coord) is not { Wall: TileWallKind.Solid })
                continue; // not one of this compartment's own ring tiles - interior tiles have no wall
            foreach (var side in TileSideExtensions.All)
            {
                var outward = side.Offset(coord);
                if (!_editorCompartmentAt.TryGetValue(outward, out var neighborInstance) || neighborInstance == instanceId)
                    continue; // exterior space, or still this same compartment - nothing to repair
                if (_editorTiles.CellAt(outward) is { Wall: TileWallKind.None, HasFloor: true })
                    _editorTiles.SetWall(outward, TileWallKind.Solid); // restore the neighbor's own boundary
            }
        }

        foreach (var coord in tiles)
        {
            _editorDeviceKinds.Remove(coord);
            _editorDeviceFootprint.Remove(coord);
            _editorEngineFacing.Remove(coord);
            _editorEngineFootprint.Remove(coord);
            _editorCompartmentAt.Remove(coord);
            if (_editorTiles.CellAt(coord) is { DeviceId: not null })
                _editorTiles.RemoveDevice(coord);
            _editorTiles.SetFloor(coord, false);
        }
        _editorCompartmentTiles.Remove(instanceId);
        _editorCompartmentProtected.Remove(instanceId);
    }

    // Purely cosmetic (direct user answer: no validation requirement) - drag a rectangle over
    // already-painted floor tiles, release to name it. An empty selection (no floor tiles inside the
    // dragged rectangle) is silently ignored rather than prompting for a name nobody would want.
    private void HandleZoneToolInput(bool leftClicked, bool leftReleased)
    {
        if (leftClicked)
        {
            _editorZoneDragStart = GridCellAt(_designMouse);
            return;
        }
        if (!leftReleased || _editorZoneDragStart is not { } start)
            return;
        _editorZoneDragStart = null;

        var endCell = GridCellAt(_designMouse) ?? start;
        var minX = Math.Min(start.X, endCell.X);
        var minY = Math.Min(start.Y, endCell.Y);
        var maxX = Math.Max(start.X, endCell.X);
        var maxY = Math.Max(start.Y, endCell.Y);

        var tiles = new HashSet<TileCoord>();
        for (var x = minX; x <= maxX; x++)
            for (var y = minY; y <= maxY; y++)
                if (_editorTiles.CellAt(new TileCoord(x, y)) is { HasFloor: true })
                    tiles.Add(new TileCoord(x, y));

        if (tiles.Count == 0)
            return;
        _editorPendingZoneTiles = tiles;
        _editorZoneNameInput = $"Отсек {_editorZones.Count + 1}";
        _editorZonePendingKind = null;
        _editorZoneNamePrompting = true;
    }

    private void HandleEditorZoneNamePromptInput(KeyboardState keyboard, bool leftClicked)
    {
        if (Pressed(keyboard, Keys.Enter))
        {
            ConfirmEditorZoneName();
            return;
        }
        if (!leftClicked)
            return;
        for (var i = 0; i < ShipZoneKinds.All.Length; i++)
        {
            if (!GetEditorZoneTypeButtonRect(i).Contains(_designMouse))
                continue;
            var kind = ShipZoneKinds.All[i];
            _editorZonePendingKind = kind;
            _editorZoneNameInput = ShipZoneKinds.CanonicalName(kind);
            return;
        }
        if (GetEditorZoneNameConfirmRect().Contains(_designMouse))
            ConfirmEditorZoneName();
        else if (GetEditorZoneNameCancelRect().Contains(_designMouse))
        {
            _editorZoneNamePrompting = false;
            _editorPendingZoneTiles = null;
        }
    }

    private void ConfirmEditorZoneName()
    {
        var name = _editorZoneNameInput.Trim();
        if (name.Length == 0 || _editorPendingZoneTiles is not { } tiles)
            return;
        // A tile already labelled by an older zone drops out of that zone - one label per tile, the
        // newest drag wins, rather than stacking overlapping names on the same cell.
        foreach (var zone in _editorZones)
            zone.Tiles.ExceptWith(tiles);
        _editorZones.RemoveAll(z => z.Tiles.Count == 0);
        _editorZones.Add(new EditorZone(name, tiles, _editorZonePendingKind));
        _editorPendingZoneTiles = null;
        _editorZoneNamePrompting = false;
    }

    private void HandleShipEditorPlayClicked()
    {
        var (definition, errors) = BuildAndValidateEditorDefinition();
        if (errors.Count > 0)
            return; // Play is drawn disabled in this state - a stray click just does nothing
        CustomShipStore.Save(definition);
        SaveStore.Delete(); // a fresh run on this hull, same as picking a fixed class on ShipSelect
        StartHostedSession(ShipKind.Custom, loadFrom: null, customShip: definition);
    }

    private void HandleShipEditorNewClicked()
    {
        _editorRooms.Clear();
        _editorDoors.Clear();
        _editorAirlocks.Clear();
        _editorDevices.Clear();
        _editorRoomCounter = 1;
        _editorTiles = new TileGrid();
        _editorDeviceKinds.Clear();
        _editorDeviceFootprint.Clear();
        _editorZones.Clear();
        _editorEngineFacing.Clear();
        _editorEngineFootprint.Clear();
        _editorCompartmentAt.Clear();
        _editorCompartmentTiles.Clear();
        _editorCompartmentProtected.Clear();
        _editorCurrentSlotName = null; // a blank hull isn't the previously-open named slot any more
        SaveEditorDefinition();
    }

    private (int X, int Y)? GridCellAt(Point designMouse)
    {
        if (!ShipEditorCanvas.Contains(designMouse))
            return null;
        return (
            FloorDiv(designMouse.X - ShipEditorCanvas.X + _editorPanOffset.X, EditorCellSize),
            FloorDiv(designMouse.Y - ShipEditorCanvas.Y + _editorPanOffset.Y, EditorCellSize));
    }

    // Plain integer "/" truncates toward zero, which misplaces tiles by one column/row once panning
    // makes the local coordinate go negative (e.g. -1 / 24 == 0 in C#, but the tile that actually
    // covers screen positions just left of the origin is tile -1, not tile 0).
    private static int FloorDiv(int a, int b) => (int)Math.Floor((double)a / b);

    private Vector2 WorldToEditorScreen(float worldX, float worldY) =>
        new(ShipEditorCanvas.X + worldX * EditorCellSize - _editorPanOffset.X,
            ShipEditorCanvas.Y + worldY * EditorCellSize - _editorPanOffset.Y);

    private Vec2 EditorMouseWorldLocal() => new(
        (_designMouse.X - ShipEditorCanvas.X + _editorPanOffset.X) / (double)EditorCellSize,
        (_designMouse.Y - ShipEditorCanvas.Y + _editorPanOffset.Y) / (double)EditorCellSize);
}
