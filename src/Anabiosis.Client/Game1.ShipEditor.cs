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

    private enum EditorTool { Floor, Wall, Door, Terminal, Device, Zone, Engine, Compartment, DoubleEngine }
    private enum EditorAction { Back, New, Save, SaveAs, Load, Play }

    private static readonly float[] EditorForwardOptions = { 0f, 90f, 180f, -90f };

    private EditorTool _editorTool = EditorTool.Floor;
    // M-doors-as-edges (humble-soaring-cat.md) - CustomDeviceKind.TripleDoor excluded here: it was a
    // purely cosmetic stand-in for a REAL 3-tile-span door before the edge model made one possible
    // (see Game1.ShipEditor.DeviceTabs.cs's own doc comment on WallItems's "Тройная дверь"), which
    // now lives in that palette as a real Door-tool span instead - listing the old cosmetic kind
    // here too (the "Все" tab's own auto-generated catch-all) would put two identically-labelled
    // "Тройная дверь" buttons in the palette, one a real door and one an inert decoration. The kind
    // itself, and every bit of code that renders/round-trips it, stays untouched for any save that
    // already placed one - only newly PLACING it from a palette button is what this removes.
    private static readonly CustomDeviceKind[] EditorDeviceKinds =
        Enum.GetValues<CustomDeviceKind>()
            .Where(k => k != CustomDeviceKind.TripleDoor)
            .ToArray();
    private CustomDeviceKind _editorSelectedDeviceKind = EditorDeviceKinds[0];
    // Which Wall-tool variant is currently selected (direct user request - "усиленная стена"/
    // "иллюминатор") - a palette sub-choice, not its own EditorTool, same as _editorSelectedDeviceKind
    // is for the Device tool. Applied by HandleWallToolInput to every tile SetWall places.
    private WallMaterial _editorWallMaterial = WallMaterial.Standard;
    // Direct user request ("удали механику что если ставим стены в ряд, они почти все превращаются
    // в полублоки... хочу сделать чтобы игрок сам выбирал") - half-block used to be inferred
    // automatically (RecomputeWallOpenSide, TileGridRasterizer.FromRooms's own claim-counting,
    // CompartmentPlacer.Stamp's own OpenSideOf - all three removed) from footprint shape; now it's
    // a genuine palette sub-choice, same shape _editorWallMaterial already is - "Полублочная стена"
    // sets this true instead of picking a WallMaterial. _editorWallHalfBlockSide is which side of
    // the tile stays solid (the OPPOSITE side is the free, walkable half - TileCell.WallOpenSide's
    // own doc comment), cycled with R the same rotate-then-place way Door/Engine already work.
    private bool _editorWallHalfBlock;
    private TileSide _editorWallHalfBlockSide = TileSide.North;
    private bool _prevWallRotateKeyDown;
    // Which Door-tool variant is selected (direct user request - "дверь занимающая 1 на 2 тайла",
    // then "сделай тоже самое с тройной дверью, чтобы она занимала 2 на 3 тайла") - how many
    // parallel edges PlaceEdgeDoor places at once, sharing one Id (DoorEdgeGroupAt's own doc
    // comment): 1 narrow, 2 wide, 3 triple. Was a bool (WideDoor) before the edge model existed; the
    // ONE remaining exception is span==2, which still tries the OLD tile-based compartment-boundary
    // special case FIRST (HandleDoorToolInput) - see DoorSpanTiles below, kept only for that.
    private int _editorDoorSpanTiles = 1;
    // Direct user request ("расставлял их как устройства со своим размером") - doors are placed the
    // same rotate-then-click way every other rotatable device is (Engine's own
    // _editorEnginePendingFacing), not by dragging: R toggles which axis a multi-segment door's
    // extra tiles extend along, matching CustomDoorDef.Vertical/ShipLayoutGeometry.RoomPairOverlap.
    // Vertical's own meaning (true = the shared wall is a vertical line, span runs along Y).
    // M-doors-as-edges (humble-soaring-cat.md) - a narrow (span==1) door reuses this same flag too,
    // for the exact same reason: which of the hovered tile's own East/South neighbor it pairs with
    // to form the new edge door's flanking floor tiles (PlaceEdgeDoor).
    private bool _editorDoorPendingVertical = true;
    private bool _prevDoorRotateKeyDown;
    // M-doors-as-edges - a stable, ever-increasing id so no two edge doors placed in the same
    // session ever collide (TileGrid.AddDoorEdge's own id, unlike the old tile-based door, is never
    // resynthesized later at export time - the editor is the sole source of truth for it).
    private int _editorNextDoorEdgeId;
    // Which of the two wall-mountable kinds the Terminal tool currently places (direct user request -
    // "на стену размером с полублок можно крепить только терминал и настенную лампу") - a palette
    // sub-choice, same shape as _editorWallMaterial/_editorDoorSpanTiles. Applied by
    // HandleTerminalToolInput.
    private CustomDeviceKind _editorSelectedWallDeviceKind = CustomDeviceKind.Terminal;
    // Direct user request ("добавь возможность вращать терминалы на r") - which mount side the
    // floor-adjacent placement mode prefers, cycled the same West->North->East->South->West way
    // _editorEnginePendingFacing is, for the (rare but real) case a hovered tile has more than one
    // valid full-thickness wall neighbor to choose from. HandleTerminalToolInput tries this side
    // FIRST and only falls back to the fixed scan order if it isn't actually valid here.
    private TileSide _editorWallDevicePendingSide = TileSide.North;
    private bool _prevWallDeviceRotateKeyDown;

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
    // Direct user bug report ("при повороте они не поворачиваются на все 4 стороны") - Helm/
    // Navigation's own half tile can sit on any of the 4 sides of its anchor, not just the East/South
    // _editorDeviceRotation's plain bool could ever express. Keyed by the SAME anchor as every other
    // per-device dictionary above; only ever populated for these two kinds - _editorDeviceRotation
    // keeps being written alongside it (true for South/North) so every OTHER consumer of that older
    // dictionary (TileShipBuilder's own footprint width/height swap, the canvas save/load format)
    // keeps seeing exactly the axis it always expected.
    private readonly Dictionary<TileCoord, TileSide> _editorDeviceHalfSides = new();
    // R rotates this PENDING flag (cycled before placement, not dragged per-click) - same
    // before-placement convention _editorCompartmentPendingRotation/_editorEnginePendingFacing
    // already use for their own tools. Used for every rotatable kind EXCEPT the half-width ones
    // (CustomDeviceFootprint.IsHalfWidthKind), which cycle _editorDevicePendingHalfSide (below)
    // through all 4 sides instead of this 2-state toggle.
    private bool _editorDevicePendingRotated;
    // R rotates this PENDING half side (East -> South -> West -> North -> East) whenever the Device
    // tool's selected kind is a half-width one (Helm/Navigation originally; Fabricator/Deconstructor
    // too, direct user request "по аналогии... полтора на 3") - direct user bug report, the OLD
    // 2-state _editorDevicePendingRotated above only ever reached East or South, never the West/North
    // mirror.
    private TileSide _editorDevicePendingHalfSide = TileSide.East;
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
    // Direct user request ("двойной двигатель... 2 наложенных друг на друга двигателя с общим
    // началом... сразу 2 вектора тяги") - an L-shaped PAIR of ordinary marching engines sharing one
    // Control tile, placed/rotated as a single unit. Kept as its own parallel dictionary rather than
    // widening _editorEngineFacing to hold more than one TileSide per anchor - every single-engine
    // code path (placement, removal, ghost, export) stays completely untouched; this tool's own
    // handlers just also populate _editorEngineFootprint so hover/removal see all 5 tiles as one
    // anchor, the same "shared lookup, separate bookkeeping" split _editorDeviceHalfSides already
    // uses alongside _editorDeviceRotation.
    private readonly Dictionary<TileCoord, (TileSide First, TileSide Second)> _editorDoubleEngineFacings = new();
    // The second arm is always the first rotated 90 degrees clockwise (TileSideExtensions has no
    // named "RotateClockwise", so this is spelled out via the same 4-state cycle
    // _editorEnginePendingFacing's own switch already uses) - only ONE pending facing to cycle
    // through the 4 possible corner orientations, not two independent ones.
    private TileSide _editorDoubleEnginePendingFacing = TileSide.West;
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
    // Direct user request ("я хочу чтобы ты сделал отсек таким каким я его сохранил") - an interior
    // partition (or any device flush against the compartment's own wall ring) can fragment the
    // generic flood-fill room decomposition (TileShipBuilder.BuildDefinition) into several pieces,
    // or leave a gap no piece claims - a real, pre-existing edge case in that algorithm, not
    // specific to this feature, just never reachable before a compartment authored an interior wall.
    // Direct user request for the actual fix ("отсеками должно считаться только то, что из раздела
    // отсеков") - a placed compartment instance's own FootprintRects is the room's SOLE authority
    // now: BuildDefinitionFromTiles overrides whatever the generic algorithm derived for a region
    // that turns out to be entirely one compartment instance with these Rects directly. Stored as
    // EntryId+Anchor+RotationSteps rather than the already-translated Rects themselves - the one
    // thing CompartmentInstanceRects (below) needs to recompute them on demand, and exactly what a
    // save file needs too (CustomShipTileCanvas.CompartmentInstanceRecord) to restore this same
    // bookkeeping after a reload without re-stamping the already-loaded grid.
    private readonly Dictionary<string, string> _editorCompartmentEntryId = new();
    private readonly Dictionary<string, (TileCoord Anchor, int RotationSteps)> _editorCompartmentPlacement = new();
    private int _editorNextCompartmentInstance;
    // Direct user request ("чтобы игра говорила что так делать нельзя") - a short-lived rejection
    // toast, drawn over the canvas by DrawEditorCanvas while Environment.TickCount64 (plain wall-
    // clock milliseconds - this is a transient UI cue, not gameplay state, so it doesn't need to be
    // threaded through from GameTime the way in-session timers are) is still under the deadline.
    private const long EditorToastMilliseconds = 2200;
    private string? _editorToastMessage;
    private long _editorToastUntilTicks;

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
            _editorCompartmentEntryId.Clear();
            _editorCompartmentPlacement.Clear();
        }
        _editorTool = EditorTool.Floor;
        _editorWallMaterial = WallMaterial.Standard;
        _editorDoorSpanTiles = 1;
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
            HandleEditorLoadListInput(leftClicked, keyboard);
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
                HandleWallToolInput(leftClicked, leftReleased, rightClicked, keyboard);
                break;
            case EditorTool.Door:
                HandleDoorToolInput(leftClicked, rightClicked, keyboard);
                break;
            case EditorTool.Terminal:
                HandleTerminalToolInput(leftClicked, rightClicked, keyboard);
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
            case EditorTool.DoubleEngine:
                HandleDoubleEngineToolInput(leftClicked, rightClicked, keyboard);
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
            {
                var coord = new TileCoord(x, y);
                _editorTiles.SetFloor(coord, true);
            }
    }

    // Point placement (one click, one tile) plus a line drag for speed (direct user request) - mouse-
    // down latches the start tile, mouse-up fills every tile along the longer axis between start and
    // the release point (a straight horizontal or vertical run, not a diagonal Bresenham line - the
    // simplest thing that covers "wall a corridor" without inventing a stairstep convention). Every
    // filled tile still needs a floor already there (TileGrid.SetWall's own precondition) - tiles
    // without one are silently skipped rather than refusing the whole line.
    private void HandleWallToolInput(bool leftClicked, bool leftReleased, bool rightClicked, KeyboardState keyboard)
    {
        // R cycles which side of the tile the SOLID half sits on (same rotate-then-place shape as
        // Door/Engine) - only meaningful while the half-block variant is actually selected, but read
        // unconditionally so switching to it later already shows a sensible last-picked side.
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevWallRotateKeyDown)
            _editorWallHalfBlockSide = _editorWallHalfBlockSide switch
            {
                TileSide.West => TileSide.North,
                TileSide.North => TileSide.East,
                TileSide.East => TileSide.South,
                _ => TileSide.West,
            };
        _prevWallRotateKeyDown = rDown;

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
            _editorTiles.SetWallOpenSide(coord, _editorWallHalfBlock ? _editorWallHalfBlockSide : null);
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

    // The 2 barrier tiles a WIDE door placed at `anchor` right now would occupy (Vertical: extend
    // along Y, same "shared wall is a vertical line" meaning CustomDoorDef.Vertical/
    // ShipLayoutGeometry.RoomPairOverlap.Vertical already use) - the ONLY caller left is
    // HandleDoorToolInput's own compartment-boundary special case (span==2 exclusively, the one
    // sub-case that stays on the OLD tile model - see that method's own doc comment); the narrow and
    // ordinary-floor wide/triple cases all moved to PlaceEdgeDoor's own span-generic geometry instead.
    private List<TileCoord> DoorSpanTiles(TileCoord anchor) => new()
    {
        anchor,
        _editorDoorPendingVertical ? new TileCoord(anchor.X, anchor.Y + 1) : new TileCoord(anchor.X + 1, anchor.Y),
    };

    // Every footprint tile of a door - barrier tile → anchor (its own first barrier tile) - so a
    // right-click ANYWHERE in the door's own footprint removes it, the same "click any occupied
    // tile" convenience _editorDeviceFootprint already gives the Reactor (direct user request -
    // "расставлял их как устройства со своим размером"). Populated on placement, pruned on removal.
    private readonly Dictionary<TileCoord, TileCoord> _editorDoorFootprint = new();

    // The orientation a NARROW door was actually placed with (_editorDoorPendingVertical at commit
    // time), keyed by its own barrier tile - direct user bug report ("после поворота двери при
    // выставлении она всё равно ставится под одним и тем же углом"): a lone barrier tile has no
    // partner to infer orientation from the way a wide door's pair does (DrawEditorDoorTile's own
    // `partner.X == coord.X` check), so DrawEditorDoorTile used to fall back to guessing from
    // floor-neighbors alone (InferDoorTileVertical) - which silently ignored R whenever a tile
    // happened to have floor on every side (both orientations geometrically "valid"). Remembering
    // the real placement-time choice here fixes that; InferDoorTileVertical stays only as the
    // fallback for a door tile that predates this dictionary (loaded from an old save).
    private readonly Dictionary<TileCoord, bool> _editorDoorVertical = new();

    // The 2 tiles flanking ONE barrier tile, perpendicular to the wall it sits on (West/East for a
    // Vertical wall, North/South otherwise) - together with the barrier tile itself, this is the
    // door's own footprint for editor bookkeeping purposes (humble-soaring-cat.md "Дверь как
    // устройство со своим footprint'ом"). Only the compartment-boundary special case still uses
    // this now (RegisterFootprint below) - every free-floor door span (narrow/wide/triple) moved to
    // the edge model (HandleEdgeDoorToolInput/PlaceEdgeDoor), which never touches a flanking tile at
    // all (CanPlaceDoorEdge simply refuses placement if one isn't already bare floor, instead of
    // clearing a stray wall the way the old tile-based placement used to).
    private static IEnumerable<TileCoord> DoorFlankingTiles(TileCoord barrier, bool vertical)
    {
        yield return (vertical ? TileSide.West : TileSide.North).Offset(barrier);
        yield return (vertical ? TileSide.East : TileSide.South).Offset(barrier);
    }

    // A door is its own toggleable wall variant (TileGrid.cs) - can go straight onto bare floor, or
    // replace an existing solid wall. Clicking an existing door removes it back to bare floor
    // (there's nothing else for this tool to do to a door tile, so both directions share one button).
    // Direct user request ("расставлял их как устройства со своим размером") - placed the same
    // rotate-then-click way every other rotatable device is (Engine's own R-cycled pending facing),
    // not by dragging: R toggles _editorDoorPendingVertical, a single click commits DoorSpanTiles'
    // own span at the hovered tile.
    private void HandleDoorToolInput(bool leftClicked, bool rightClicked, KeyboardState keyboard)
    {
        // M-doors-as-edges (humble-soaring-cat.md) - direct user requests ("я хочу полностью
        // переделать двери... давай вначале передалем дверь которая размером в 1 тайл", then "сделай
        // тоже самое с... широкой дверью", then "...с тройной дверью"): every span EXCEPT 2 is a
        // completely separate placement model now (N parallel edges between already-free floor
        // tiles, never a tile of its own) - split off into its own method entirely. Span==2 alone
        // stays threaded through the span-based logic below, since it's the one case that still has
        // to try the OLD tile-based compartment-boundary special case FIRST (never asked to change)
        // before falling back to the same edge model the other spans use exclusively.
        if (_editorDoorSpanTiles != 2)
        {
            HandleEdgeDoorToolInput(leftClicked, rightClicked, keyboard, _editorDoorSpanTiles);
            return;
        }

        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevDoorRotateKeyDown)
            _editorDoorPendingVertical = !_editorDoorPendingVertical;
        _prevDoorRotateKeyDown = rDown;

        if (rightClicked)
        {
            if (GridCellAt(_designMouse) is not { } removeCell)
                return;
            var clicked = new TileCoord(removeCell.X, removeCell.Y);
            // Clicking anywhere in the door's own footprint (device-style) resolves to its actual
            // barrier tile - clicking the barrier tile itself is just the anchor-equals-itself case.
            var removeCoord = _editorDoorFootprint.TryGetValue(clicked, out var doorAnchor) ? doorAnchor : clicked;
            if (_editorTiles.CellAt(removeCoord) is not { Wall: TileWallKind.Door } current)
            {
                // M-doors-as-edges (wide) - no OLD-style Door tile here (that only ever exists for
                // the compartment-boundary special case below), so this is either a wide door built
                // on plain free floor via the new edge model, or genuinely nothing at all.
                RemoveDoorEdgeGroupAt(clicked);
                return;
            }
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
            var removedBarriers = new List<TileCoord> { removeCoord };
            if (current.DoorGroupId is { } groupId)
                foreach (var partner in _editorTiles.Cells
                    .Where(kv => kv.Value.DoorGroupId == groupId && kv.Key != removeCoord)
                    .Select(kv => kv.Key).ToList())
                {
                    _editorTiles.SetWall(partner, RestoreKind(partner));
                    removedBarriers.Add(partner);
                }
            _editorTiles.SetWall(removeCoord, RestoreKind(removeCoord));
            // Prune every footprint tile this door claimed - both barrier tiles removed above, and
            // both their flanking floor tiles (never themselves touched, just bookkeeping entries).
            foreach (var barrier in removedBarriers)
            {
                _editorDoorFootprint.Remove(barrier);
                _editorDoorVertical.Remove(barrier);
                foreach (var flank in _editorDoorFootprint.Where(kv => kv.Value == barrier).Select(kv => kv.Key).ToList())
                    _editorDoorFootprint.Remove(flank);
            }
            return;
        }

        if (!leftClicked || GridCellAt(_designMouse) is not { } cell)
            return;
        var anchor = new TileCoord(cell.X, cell.Y);
        var span = DoorSpanTiles(anchor);

        // M82 (humble-soaring-cat.md) - direct user rule: a door may replace the wall on the boundary
        // between two ALREADY-PLACED compartments only if that boundary is exactly 2 tiles long and
        // neither of those tiles borders open space/vacuum (a genuine interior seam, never a stretch
        // of the outer hull). Tried FIRST, on the whole 2-tile span at once - both tiles must
        // independently qualify (TryResolveCompartmentBoundaryDoor) AND agree on the very same
        // compartment pair. If it doesn't apply (not a wall at all, an ordinary hull wall, or a
        // corner), fall through unchanged to the original floor-based interpretation below - the two
        // cases are mutually exclusive per tile (a tile is either bare floor or already carries a
        // wall), so there's no ambiguity about which one a given click means. No partial
        // application: either both tiles convert, or neither does.
        // Every footprint tile of `barriers` (the barrier tiles themselves plus each one's own 2
        // flanking floor tiles, DoorFlankingTiles) mapped to the first barrier as anchor - lets a
        // right-click ANYWHERE in the door's footprint remove it (see _editorDoorFootprint's own
        // doc comment).
        void RegisterFootprint(IReadOnlyList<TileCoord> barriers)
        {
            if (barriers.Count == 0)
                return;
            var footprintAnchor = barriers[0];
            foreach (var barrier in barriers)
            {
                _editorDoorFootprint[barrier] = footprintAnchor;
                _editorDoorVertical[barrier] = _editorDoorPendingVertical;
                foreach (var flank in DoorFlankingTiles(barrier, _editorDoorPendingVertical))
                    _editorDoorFootprint[flank] = footprintAnchor;
            }
        }

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
            RegisterFootprint(span);
            return;
        }

        // M-doors-as-edges (wide) - direct user request ("сделай по аналогии дверь 1 на 2... назови
        // ее широкой дверью"): not a compartment boundary (already handled above), so this is the
        // ordinary case - a genuine 4-tile free-floor footprint, placed via PlaceEdgeDoor rather
        // than converting `span`'s 2 tiles into Door TILES the old way (that old conversion loop,
        // including its own airlock-flank leniency, is gone - a wide edge door needs real floor on
        // both sides now, same tradeoff the narrow door already accepted; a genuine hull airlock
        // still works via the OLD tile model - see the compartment-boundary branch above and the
        // "Шлюз" tool, neither one touched here). `anchor` alone is enough (PlaceEdgeDoor derives
        // both its own axes from _editorDoorPendingVertical directly, the same source `span` itself
        // was computed from) - `span`/RegisterFootprint above are used ONLY by the compartment-
        // boundary branch now, not dead code.
        PlaceEdgeDoor(anchor, 2);
    }

    // M-doors-as-edges (humble-soaring-cat.md) - every span except 2 (narrow=1, triple=3, and any
    // future span) shares this ONE handler: a barrier made of `spanTiles` parallel edges sitting on
    // the EDGE between the hovered tile/its span-neighbors and their counterparts toward
    // _editorDoorPendingVertical's own direction (South if true, East if false), never a tile of its
    // own. Direct user rule, quoted verbatim for the narrow case: "эта дверь занимала ровно 2 тайла,
    // не больше не меньше... чтобы блоки рядом не удаляллись... дверь нельзя ставить никуда кроме
    // свободных клеток" - PlaceEdgeDoor never auto-clears a neighboring wall; CanPlaceDoorEdge simply
    // refuses the whole placement if ANY flanking tile isn't already bare, deviceless floor.
    private void HandleEdgeDoorToolInput(bool leftClicked, bool rightClicked, KeyboardState keyboard, int spanTiles)
    {
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevDoorRotateKeyDown)
            _editorDoorPendingVertical = !_editorDoorPendingVertical;
        _prevDoorRotateKeyDown = rDown;

        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var anchor = new TileCoord(cell.X, cell.Y);

        if (rightClicked)
        {
            RemoveDoorEdgeGroupAt(anchor);
            return;
        }

        if (leftClicked)
            PlaceEdgeDoor(anchor, spanTiles);
    }

    // Every (Coord, Side) key in _editorTiles.DoorEdges that shares an Id with whatever edge (if
    // any) touches `anchor` on one of its 4 sides - a narrow door is always a group of 1, a wide/
    // triple door (PlaceEdgeDoor below) a group of 2/3 sharing one Id, same "one id, several
    // physical edges" trick World.Doors.cs's own ToggleDoor/_doorEdgeOpen already rely on to toggle
    // every segment of a multi-tile door together with no extra plumbing. Empty if `anchor` touches
    // no edge.
    private List<(TileCoord Coord, TileSide Side)> DoorEdgeGroupAt(TileCoord anchor)
    {
        string? id = null;
        foreach (var side in TileSideExtensions.All)
            if (_editorTiles.DoorEdgeAt(anchor, side) is { } edge)
            {
                id = edge.Id;
                break;
            }
        if (id is null)
            return new List<(TileCoord, TileSide)>();
        return _editorTiles.DoorEdges.Where(kv => kv.Value.Id == id).Select(kv => (kv.Key.Coord, kv.Key.Side)).ToList();
    }

    // Right-click removal for EVERY edge-based door span (narrow/wide/triple alike) - removes every
    // physical edge in the group at once, so a multi-segment door never gets left half-removed (one
    // segment gone, others still standing) no matter which of its own tiles the player actually
    // clicked.
    private void RemoveDoorEdgeGroupAt(TileCoord anchor)
    {
        foreach (var (coord, side) in DoorEdgeGroupAt(anchor))
            _editorTiles.RemoveDoorEdge(coord, side);
    }

    // Direct user requests ("сделай по аналогии дверь 1 на 2 т е дверь занимающую 4 клетки и назови
    // ее широкой дверью", then "сделай тоже самое с тройной дверью, чтобы она занимала 2 на 3
    // тайла") - the exact same edge-between-2-tiles primitive as the narrow door, just `spanTiles`
    // PARALLEL edges (sharing one Id, see DoorEdgeGroupAt's own doc comment) instead of one, so the
    // barrier's own span runs `spanTiles` tiles along the seam instead of 1 - a genuine
    // spanTiles-by-2-free-floor footprint (spanTiles*2 tiles total: 2 for narrow, 4 for wide, 6 for
    // triple), never a tile of its own, same "no auto-clearing, no partial placement" rules the
    // narrow door already had. `side` (perpendicular to the seam, which room is A vs B) reuses
    // _editorDoorPendingVertical exactly like the narrow door; the span axis (which direction the
    // door's own width runs) is simply whichever axis `side` ISN'T - always extended toward +1 (no
    // separate rotation state for it, same 2-choices-via-1-key simplicity the OLD tile-based Wide
    // door already had). All-or-nothing: either every one of the `spanTiles` positions is already
    // free floor, or nothing gets placed at all.
    private void PlaceEdgeDoor(TileCoord anchor, int spanTiles)
    {
        var side = _editorDoorPendingVertical ? TileSide.South : TileSide.East;
        var spanOffset = side is TileSide.East or TileSide.West ? new TileCoord(0, 1) : new TileCoord(1, 0);
        var anchors = Enumerable.Range(0, spanTiles)
            .Select(i => new TileCoord(anchor.X + spanOffset.X * i, anchor.Y + spanOffset.Y * i))
            .ToList();

        if (anchors.Any(a => !_editorTiles.CanPlaceDoorEdge(a, side)))
        {
            // Direct user request ("сделай возможным поставить дверь если 1 клетка это пол а
            // вторая космос") - CanPlaceDoorEdge now also accepts a flank that's genuinely open
            // space (an airlock onto vacuum), not just floor on both sides.
            _editorToastMessage = spanTiles switch
            {
                1 => "Нельзя поставить дверь - нужна свободная клетка пола по одну сторону, а с другой - пол другой комнаты или открытый космос.",
                2 => "Нельзя поставить широкую дверь - каждая из 2 пар клеток должна иметь пол хотя бы с одной стороны.",
                _ => $"Нельзя поставить тройную дверь - каждая из {spanTiles} пар клеток должна иметь пол хотя бы с одной стороны.",
            };
            _editorToastUntilTicks = Environment.TickCount64 + EditorToastMilliseconds;
            return;
        }
        var id = $"door-edge-{_editorNextDoorEdgeId++}";
        foreach (var a in anchors)
            _editorTiles.AddDoorEdge(a, side, id);
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

    // Which side a floor-adjacent wall device would mount to at `coord` - the player's own rotated
    // preference (_editorWallDevicePendingSide, direct user request "добавь возможность вращать
    // терминалы на r") if it's actually valid here, else the first valid side in fixed North/South/
    // East/West order (the original, pre-rotation behavior - covers the overwhelming majority of
    // tiles, which only ever have exactly one qualifying neighbor anyway). A qualifying neighbor
    // must be a Solid/Door wall that is NOT itself half-thick (direct user report - "стены в пол
    // блока... это можно было сделать только в том же тайле что и стена": a half-thick wall
    // neighbor already has its own free half to recess into instead, via PlaceRecessedWallDevice).
    private TileSide? FindWallDeviceMountSide(TileCoord coord)
    {
        bool Qualifies(TileSide side) => _editorTiles.CellAt(side.Offset(coord)) is { Wall: not TileWallKind.None, WallOpenSide: null };
        if (Qualifies(_editorWallDevicePendingSide))
            return _editorWallDevicePendingSide;
        foreach (var side in TileSideExtensions.All)
            if (Qualifies(side))
                return side;
        return null;
    }

    // Mounts to whichever side actually has a wall/door neighbor (TileGrid.PlaceWallDevice's own
    // precondition) - FindWallDeviceMountSide's own rotated-preference-then-fixed-order search.
    // Refused entirely (direct user request) if the tile itself sits at a construction junction -
    // see IsAtConstructionJunction's own doc comment. Places whichever kind the palette sub-choice
    // currently selects (direct user request, restricting this tool to Terminal and WallLamp only) -
    // _editorSelectedWallDeviceKind.
    private void HandleTerminalToolInput(bool leftClicked, bool rightClicked, KeyboardState keyboard)
    {
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevWallDeviceRotateKeyDown)
        {
            _editorWallDevicePendingSide = _editorWallDevicePendingSide switch
            {
                TileSide.North => TileSide.East,
                TileSide.East => TileSide.South,
                TileSide.South => TileSide.West,
                _ => TileSide.North,
            };
        }
        _prevWallDeviceRotateKeyDown = rDown;

        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var coord = new TileCoord(cell.X, cell.Y);
        var kind = _editorSelectedWallDeviceKind;
        if (rightClicked)
        {
            if (_editorTiles.CellAt(coord) is { WallDeviceId: not null })
                _editorTiles.RemoveWallDevice(coord);
            return;
        }
        if (_editorTiles.CellAt(coord) is not { } targetCell)
            return;

        // Direct user request (a device can mount into a half-thick wall's own free half) - clicking
        // directly ON a non-corner half-thick wall tile recesses the device there instead of the
        // floor-adjacent mount below. No junction guard needed here - a genuine corner already has
        // WallOpenSide == null, and a straight wall run's own neighbors can never satisfy
        // IsAtConstructionJunction's own two-adjacent-sides test.
        if (leftClicked && targetCell is { Wall: TileWallKind.Solid, WallOpenSide: not null, WallDeviceId: null })
        {
            _editorTiles.PlaceRecessedWallDevice(coord, kind, $"{kind}-{coord.X}-{coord.Y}".ToLowerInvariant());
            return;
        }

        if (!leftClicked || targetCell is not { HasFloor: true, WallDeviceId: null })
            return;
        if (IsAtConstructionJunction(coord))
            return;
        if (FindWallDeviceMountSide(coord) is { } mountSide)
            _editorTiles.PlaceWallDevice(coord, mountSide, kind, $"{kind}-{coord.X}-{coord.Y}".ToLowerInvariant());
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
            if (_editorTiles.CellAt(neighbor) is { WallDeviceId: not null } && IsAtConstructionJunction(neighbor))
                _editorTiles.RemoveWallDevice(neighbor);
        }
    }

    // Direct user request ("удали механику что если ставим стены в ряд, они почти все превращаются
    // в полублоки") - live per-tile WallOpenSide classification used to happen here automatically on
    // every Wall/Floor/Door edit; removed. Half-block is a deliberate palette choice now
    // (_editorWallHalfBlock/_editorWallHalfBlockSide, applied directly in HandleWallToolInput).

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
        {
            if (CustomDeviceFootprint.IsHalfWidthKind(_editorSelectedDeviceKind))
                _editorDevicePendingHalfSide = _editorDevicePendingHalfSide switch
                {
                    TileSide.East => TileSide.South,
                    TileSide.South => TileSide.West,
                    TileSide.West => TileSide.North,
                    _ => TileSide.East,
                };
            else
                _editorDevicePendingRotated = !_editorDevicePendingRotated;
        }
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
                var removedKind = _editorDeviceKinds[anchor];
                var (removeWidth, removeHeight) = DeviceFootprintSize(removedKind, wasRotated);
                foreach (var occupied in DeviceFootprintTiles(anchor, removeWidth, removeHeight))
                {
                    _editorTiles.RemoveDevice(occupied);
                    _editorDeviceFootprint.Remove(occupied);
                }
                _editorDeviceKinds.Remove(anchor);
                _editorDeviceRotation.Remove(anchor);
                _editorDeviceHalfSides.Remove(anchor);
                if (IsTurretKind(removedKind))
                    ClearTurretMountSkirt(anchor, wasRotated);
            }
            return;
        }
        if (!leftClicked)
            return;
        var isHalfWidth = CustomDeviceFootprint.IsHalfWidthKind(_editorSelectedDeviceKind);
        var halfSide = isHalfWidth ? _editorDevicePendingHalfSide : TileSide.East;
        var pendingRotated = isHalfWidth ? halfSide is TileSide.South or TileSide.North : _editorDevicePendingRotated;
        var (width, height) = DeviceFootprintSize(_editorSelectedDeviceKind, pendingRotated);
        var placeAnchor = FootprintAnchorFor(coord, width, height);
        var footprint = DeviceFootprintTiles(placeAnchor, width, height).ToList();
        if (!CanPlaceDeviceFootprint(_editorSelectedDeviceKind, footprint, placeAnchor, halfSide))
        {
            // Direct user bug report (screenshot - a half-width kind refused to place on a spot that
            // LOOKS like open floor) - the ghost preview's red outline already says "not here", but
            // gives no reason why, and the actual reason is often non-obvious: a half-width
            // neighbor's own free-but-reserved half tile (IsWalkable's own combined truth table)
            // reads as plain bare floor to the eye, since only the BLOCKED half of that tile gets the
            // neighbor's own baked icon drawn over it. Same toast convention
            // HandleCompartmentToolInput's own rejected-stamp case already uses, replacing what used
            // to be a silent no-op plus a TEMP-DIAG debug overlay (removed - this is its permanent
            // replacement).
            _editorToastMessage = DeviceRejectionToastMessage(footprint, placeAnchor, halfSide);
            _editorToastUntilTicks = Environment.TickCount64 + EditorToastMilliseconds;
            return;
        }
        var deviceId = $"device-{placeAnchor.X}-{placeAnchor.Y}";
        PlaceDeviceFootprint(_editorSelectedDeviceKind, footprint, placeAnchor, halfSide, deviceId);
        foreach (var occupied in footprint)
            _editorDeviceFootprint[occupied] = placeAnchor;
        _editorDeviceKinds[placeAnchor] = _editorSelectedDeviceKind;
        if (pendingRotated)
            _editorDeviceRotation[placeAnchor] = true;
        if (isHalfWidth)
            _editorDeviceHalfSides[placeAnchor] = halfSide;
        foreach (var occupied in footprint)
            EvictTerminalsAtJunctions(occupied);
        // Direct user request (screenshot of a turret mount built out of wall tiles - "реальные
        // такие границы... при установке в редакторе") - stamped immediately so the border is
        // visible while designing, not just after a build (Ship.Custom.cs derives the same shape
        // again at build time regardless, TurretMountSkirt.cs's own doc comment explains why).
        if (IsTurretKind(_editorSelectedDeviceKind))
            StampTurretMountSkirt(placeAnchor, pendingRotated);
    }

    private static bool IsTurretKind(CustomDeviceKind kind) => kind is CustomDeviceKind.TurretBallistic
        or CustomDeviceKind.TurretLaser or CustomDeviceKind.TurretMachineGun or CustomDeviceKind.DefensiveTurret;

    private void StampTurretMountSkirt(TileCoord anchor, bool rotated)
    {
        foreach (var skirt in TurretMountSkirt.SkirtTiles(anchor, rotated))
        {
            if (_editorTiles.CellAt(skirt.Position) is { DeviceId: not null })
                continue;
            _editorTiles.SetFloor(skirt.Position, true);
            _editorTiles.SetWall(skirt.Position, TileWallKind.Solid);
            if (skirt.OpenSide is { } side)
                _editorTiles.SetWallOpenSide(skirt.Position, side);
        }
    }

    private void ClearTurretMountSkirt(TileCoord anchor, bool rotated)
    {
        foreach (var skirt in TurretMountSkirt.SkirtTiles(anchor, rotated))
            if (_editorTiles.CellAt(skirt.Position) is { Wall: TileWallKind.Solid, DeviceId: null })
                _editorTiles.SetWall(skirt.Position, TileWallKind.None);
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

    // Every Device-tool kind needs bare floor on EVERY tile of its footprint (TileGrid.PlaceDevice's
    // own precondition) - EXCEPT Helm/Navigation, whose SECOND (half) tile along the halved axis only
    // needs TileGrid.CanPlaceHalfWidthDevice (bare floor, OR an already-matching half-block wall to
    // coexist with - see that method's own doc comment). No other kind gets this - it's specific to
    // these two consoles' own genuine 1.5-tile footprint, not a general placement rule change.
    private bool CanPlaceDeviceFootprint(CustomDeviceKind kind, IReadOnlyList<TileCoord> footprint, TileCoord anchor, TileSide halfSide)
    {
        if (!CustomDeviceFootprint.IsHalfWidthKind(kind))
            return footprint.All(t => _editorTiles.CellAt(t) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null });

        var halfOpenSide = HalfOpenSideForHalfWidthDevice(halfSide);
        foreach (var coord in footprint)
        {
            var ok = IsHalfTileOfHalfWidthFootprint(coord, anchor, halfSide)
                ? _editorTiles.CanPlaceHalfWidthDevice(coord, halfOpenSide)
                : _editorTiles.CellAt(coord) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null };
            if (!ok)
                return false;
        }
        return true;
    }

    // Direct user bug report (screenshot - trying to place a Щиток/Junction next to an existing one
    // refused with no visible reason) - a short, translated toast (HandleCompartmentToolInput's own
    // rejected-stamp convention) shown only on an actual rejected click, replacing the old per-frame
    // TEMP-DIAG yellow text (removed) that dumped raw TileCoord/DeviceId internals. The single most
    // common real cause: a half-width neighbor's own free-but-reserved half tile (e.g. this exact
    // report - a second Щиток's half tile landing on the first one's already-claimed half) reads as
    // plain bare floor to the eye, since only the BLOCKED half of that tile gets the neighbor's own
    // baked icon drawn over it (DrawEditorDeviceAt) - the other half stays walkable and undrawn.
    private string DeviceRejectionToastMessage(IReadOnlyList<TileCoord> footprint, TileCoord anchor, TileSide halfSide)
    {
        var isHalfWidth = CustomDeviceFootprint.IsHalfWidthKind(_editorSelectedDeviceKind);
        var requiredOpenSide = HalfOpenSideForHalfWidthDevice(halfSide);
        foreach (var coord in footprint)
        {
            var isHalf = isHalfWidth && IsHalfTileOfHalfWidthFootprint(coord, anchor, halfSide);
            var ok = isHalf
                ? _editorTiles.CanPlaceHalfWidthDevice(coord, requiredOpenSide)
                : _editorTiles.CellAt(coord) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null };
            if (ok)
                continue;
            var c = _editorTiles.CellAt(coord);
            if (c is null)
                return "Нельзя разместить устройство - здесь нет тайла корабля.";
            if (!isHalf && !c.HasFloor)
                return "Нельзя разместить устройство - здесь нет пола.";
            if (c.DeviceId is not null)
                return "Нельзя разместить устройство - место уже занято другим устройством (например, половиной соседнего прибора).";
            if (c.Wall != TileWallKind.None)
                return "Нельзя разместить устройство - здесь стена.";
            return "Нельзя разместить устройство - недостаточно места.";
        }
        return "Нельзя разместить устройство.";
    }

    // Commits a footprint already validated by CanPlaceDeviceFootprint above - Helm/Navigation's
    // "half" tile goes through PlaceHalfWidthDevice (bare floor OR a matching half-block wall it then
    // coexists with, never destroyed), every other tile of every kind through ordinary PlaceDevice,
    // unchanged. Shared by fresh placement (HandleDeviceToolInput) and canvas reload
    // (Game1.ShipEditor.TileSave.cs's ApplyEditorTileCanvas) so both always agree on which tile of a
    // Helm/Navigation footprint is the half one.
    private void PlaceDeviceFootprint(CustomDeviceKind kind, IReadOnlyList<TileCoord> footprint, TileCoord anchor, TileSide halfSide, string deviceId)
    {
        if (!CustomDeviceFootprint.IsHalfWidthKind(kind))
        {
            foreach (var occupied in footprint)
                _editorTiles.PlaceDevice(occupied, deviceId);
            return;
        }

        var halfOpenSide = HalfOpenSideForHalfWidthDevice(halfSide);
        foreach (var coord in footprint)
        {
            if (IsHalfTileOfHalfWidthFootprint(coord, anchor, halfSide))
                _editorTiles.PlaceHalfWidthDevice(coord, halfOpenSide, deviceId);
            else
                _editorTiles.PlaceDevice(coord, deviceId);
        }
    }

    // Moved to CustomDeviceFootprint.cs (Shared) so CompartmentPlacer.cs can share the exact same
    // "which tile of a half-width footprint is the half one" rule instead of risking a second copy
    // drifting out of sync - kept as thin aliases here so every existing call site in this file
    // reads unchanged.
    private static bool IsHalfTileOfHalfWidthFootprint(TileCoord coord, TileCoord anchor, TileSide halfSide) =>
        CustomDeviceFootprint.IsHalfTileOfHalfWidthFootprint(coord, anchor, halfSide);

    private static TileSide HalfOpenSideForHalfWidthDevice(TileSide halfSide) =>
        CustomDeviceFootprint.HalfOpenSideForHalfWidthDevice(halfSide);

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

    // Direct user request ("двойной двигатель... 5 клеток... 2 сопла под углом 90 градусов") - the
    // 5 tiles of a double engine: the shared Control, then each arm's own Bulkhead+Nozzle
    // (EngineFootprintTiles(control, facing) already gives Control+Bulkhead+Nozzle for one arm -
    // Skip(1) drops the Control tile the second arm would otherwise duplicate).
    private static IEnumerable<TileCoord> DoubleEngineFootprintTiles(TileCoord control, TileSide facingA, TileSide facingB)
    {
        yield return control;
        foreach (var t in EngineFootprintTiles(control, facingA).Skip(1))
            yield return t;
        foreach (var t in EngineFootprintTiles(control, facingB).Skip(1))
            yield return t;
    }

    // The second arm is always the first rotated 90 degrees clockwise - direct user request ("2
    // сопла двигателя смотрели под углом 90 градусов в их разнице"), one fixed relationship rather
    // than letting the player choose each arm independently (R cycles the PAIR through all 4 corner
    // orientations, same "rotate the pending facing before placing" shape HandleEngineToolInput
    // already uses for a single engine).
    private static TileSide DoubleEngineSecondFacing(TileSide first) => first switch
    {
        TileSide.West => TileSide.North,
        TileSide.North => TileSide.East,
        TileSide.East => TileSide.South,
        _ => TileSide.West, // South
    };

    private void HandleDoubleEngineToolInput(bool leftClicked, bool rightClicked, KeyboardState keyboard)
    {
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (rDown && !_prevEngineRotateKeyDown)
        {
            _editorDoubleEnginePendingFacing = _editorDoubleEnginePendingFacing switch
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
                RemoveDoubleEngineAt(anchor);
            return;
        }
        if (!leftClicked)
            return;

        var facingA = _editorDoubleEnginePendingFacing;
        var facingB = DoubleEngineSecondFacing(facingA);
        var bulkheadA = facingA.Offset(control);
        var nozzleA = facingA.Offset(bulkheadA);
        var bulkheadB = facingB.Offset(control);
        var nozzleB = facingB.Offset(bulkheadB);

        if (_editorTiles.CellAt(control) is not { HasFloor: true, Wall: TileWallKind.None, DeviceId: null })
            return;
        if (_editorTiles.CellAt(bulkheadA) is not { Wall: TileWallKind.Solid })
            return;
        if (_editorTiles.CellAt(nozzleA) is { HasFloor: true })
            return;
        if (_editorTiles.CellAt(bulkheadB) is not { Wall: TileWallKind.Solid })
            return;
        if (_editorTiles.CellAt(nozzleB) is { HasFloor: true })
            return;
        // Only the 4 arm tiles need to be free of every OTHER engine (single or double) - the
        // Control tile itself is a fresh anchor being placed here, not shared with an existing one
        // (the plain PlaceDevice precondition just above already guarantees DeviceId is null there).
        if (DoubleEngineFootprintTiles(control, facingA, facingB).Skip(1).Any(_editorEngineFootprint.ContainsKey))
            return;

        var deviceId = $"doubleengine-{control.X}-{control.Y}";
        _editorTiles.PlaceDevice(control, deviceId);
        _editorDoubleEngineFacings[control] = (facingA, facingB);
        foreach (var t in DoubleEngineFootprintTiles(control, facingA, facingB))
            _editorEngineFootprint[t] = control;
    }

    private void RemoveDoubleEngineAt(TileCoord anchor)
    {
        if (IsProtectedCompartmentCore(anchor))
            return;
        if (!_editorDoubleEngineFacings.TryGetValue(anchor, out var facings))
            return;
        foreach (var t in DoubleEngineFootprintTiles(anchor, facings.First, facings.Second))
            _editorEngineFootprint.Remove(t);
        _editorDoubleEngineFacings.Remove(anchor);
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
        {
            // Direct user request ("чтобы игра говорила что так делать нельзя") - a short-lived
            // toast over the canvas instead of the silent reject every other tool's own precondition
            // check still uses; overlapping another compartment is common enough to click into by
            // accident (unlike, say, a device tool's occupied-tile check) that it earns real feedback.
            _editorToastMessage = "Нельзя разместить отсек - место уже занято.";
            _editorToastUntilTicks = Environment.TickCount64 + EditorToastMilliseconds;
            return;
        }

        foreach (var device in result.Devices)
        {
            _editorDeviceKinds[device.Coord] = device.Kind;
            if (device.Rotated)
                _editorDeviceRotation[device.Coord] = true;
            // Direct user request ("я хочу чтобы ты сделал отсек таким каким я его сохранил") - the
            // free-tile Device tool's own HandleDeviceToolInput already does this for a hand-placed
            // Helm/Navigation; a compartment-placed half-width device needs the exact same
            // bookkeeping or its own authored orientation resets to the East/South-by-Rotated
            // fallback the moment the ship is exported (TileShipBuilder.BuildDefinition's own
            // deviceHalfSides lookup would simply never find an entry for it).
            if (device.HalfSide is { } halfSide)
                _editorDeviceHalfSides[device.Coord] = halfSide;
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

        // CompartmentPlacer.Stamp already rejected this placement outright if any of these tiles
        // belonged to anything else at all (including another compartment's own wall ring - direct
        // user request, "убери механику... наезжать друг на друга"), so every tile here is
        // guaranteed to be exclusively this compartment's own - no shared-ownership bookkeeping
        // needed any more.
        foreach (var t in allTiles)
            _editorCompartmentAt[t] = instance;
        _editorCompartmentTiles[instance] = allTiles;
        _editorCompartmentProtected[instance] = new HashSet<TileCoord>(result.ProtectedTiles);
        _editorCompartmentEntryId[instance] = compartmentId;
        _editorCompartmentPlacement[instance] = (anchor, _editorCompartmentPendingRotation);
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

    // Every tile a compartment owns is now exclusively its own (CompartmentPlacer.Stamp rejects
    // placement outright if any tile - wall-ring or interior - would land on something else's
    // ground at all), so removing one can never leave a neighboring, still-standing compartment
    // missing a wall it depended on - there is no shared tile left to repair.
    private void RemoveCompartmentAt(string instanceId)
    {
        if (!_editorCompartmentTiles.TryGetValue(instanceId, out var tiles))
            return;

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
        _editorCompartmentEntryId.Remove(instanceId);
        _editorCompartmentPlacement.Remove(instanceId);
    }

    // Recomputes a placed compartment instance's own authoritative FootprintRects on demand
    // (Game1.ShipEditor.TileBridge.cs's own BuildDefinitionFromTiles, and BuildEditorTileCanvas's
    // own save) rather than caching them separately - EntryId+Anchor+RotationSteps is the one
    // source of truth, the exact same data a save file itself persists
    // (CustomShipTileCanvas.CompartmentInstanceRecord) to restore this after a reload.
    private IReadOnlyList<RectF>? CompartmentInstanceRects(string instanceId)
    {
        if (!_editorCompartmentEntryId.TryGetValue(instanceId, out var entryId) || CompartmentCatalog.Find(entryId) is not { } entry)
            return null;
        if (!_editorCompartmentPlacement.TryGetValue(instanceId, out var placement))
            return null;
        var rotated = CompartmentPlacer.Rotate(entry, placement.RotationSteps);
        return rotated.FootprintRects.Select(r => new RectF(r.X + placement.Anchor.X, r.Y + placement.Anchor.Y, r.Width, r.Height)).ToList();
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
        StartHostedSession(ShipKind.Custom, loadFrom: null, customShip: definition, fromShipEditor: true);
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
        _editorCompartmentEntryId.Clear();
        _editorCompartmentPlacement.Clear();
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
