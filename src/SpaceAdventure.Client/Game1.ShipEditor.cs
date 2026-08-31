using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Server; // SaveStore only - deleting the run save when starting a fresh custom hull
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

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

    private enum EditorTool { Floor, Wall, Door, Terminal, Device, Zone }
    private enum EditorAction { Back, New, Save, SaveAs, Load, Play }

    private static readonly float[] EditorForwardOptions = { 0f, 90f, 180f, -90f };

    private EditorTool _editorTool = EditorTool.Floor;
    private static readonly CustomDeviceKind[] EditorDeviceKinds = Enum.GetValues<CustomDeviceKind>();
    private CustomDeviceKind _editorSelectedDeviceKind = EditorDeviceKinds[0];

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
    private sealed record EditorZone(string Name, HashSet<TileCoord> Tiles);
    private readonly List<EditorZone> _editorZones = new();

    // 1x1 for every kind except the Reactor, which is now a real 4x4-tile footprint everywhere in
    // the game (ShipRenderer.ReactorBlockSize) - a per-kind lookup rather than a special case
    // scattered through placement/removal/drawing/export, so a future multi-tile device just adds
    // one more entry here.
    private static int DeviceFootprintSize(CustomDeviceKind kind) => kind == CustomDeviceKind.Reactor ? 4 : 1;

    private (int X, int Y)? _editorFloorDragStart;
    private TileCoord? _editorWallDragStart;
    private (int X, int Y)? _editorZoneDragStart;
    private HashSet<TileCoord>? _editorPendingZoneTiles;
    private bool _editorZoneNamePrompting;
    private string _editorZoneNameInput = "";

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
        }
        _editorTool = EditorTool.Floor;
        _editorCurrentSlotName = null; // the scratch slot isn't necessarily saved under any name yet
        _editorSaveAsPrompting = false;
        _editorLoadListOpen = false;
        _editorZoneNamePrompting = false;
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
                HandleDoorToolInput(leftClicked, rightClicked);
                break;
            case EditorTool.Terminal:
                HandleTerminalToolInput(leftClicked, rightClicked);
                break;
            case EditorTool.Device:
                HandleDeviceToolInput(leftClicked, rightClicked);
                break;
            case EditorTool.Zone:
                HandleZoneToolInput(leftClicked, leftReleased);
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
                _editorTiles.SetWall(new TileCoord(cell.X, cell.Y), TileWallKind.None);
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
            _editorTiles.SetWall(coord, TileWallKind.Solid);
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

    // A door is its own toggleable wall variant (TileGrid.cs) - can go straight onto bare floor, or
    // replace an existing solid wall. Clicking an existing door removes it back to bare floor
    // (there's nothing else for this tool to do to a door tile, so both directions share one button).
    private void HandleDoorToolInput(bool leftClicked, bool rightClicked)
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var coord = new TileCoord(cell.X, cell.Y);
        var current = _editorTiles.CellAt(coord);
        if (rightClicked)
        {
            if (current is { Wall: TileWallKind.Door })
                _editorTiles.SetWall(coord, TileWallKind.None);
            return;
        }
        if (!leftClicked || current is not { HasFloor: true, DeviceId: null })
            return;
        _editorTiles.SetWall(coord, TileWallKind.Door);
        EvictTerminalsAtJunctions(coord);
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
    private void HandleDeviceToolInput(bool leftClicked, bool rightClicked)
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var coord = new TileCoord(cell.X, cell.Y);
        if (rightClicked)
        {
            if (_editorDeviceFootprint.TryGetValue(coord, out var anchor))
            {
                foreach (var occupied in DeviceFootprintTiles(anchor, DeviceFootprintSize(_editorDeviceKinds[anchor])))
                {
                    _editorTiles.RemoveDevice(occupied);
                    _editorDeviceFootprint.Remove(occupied);
                }
                _editorDeviceKinds.Remove(anchor);
            }
            return;
        }
        if (!leftClicked)
            return;
        var size = DeviceFootprintSize(_editorSelectedDeviceKind);
        var placeAnchor = FootprintAnchorFor(coord, size);
        var footprint = DeviceFootprintTiles(placeAnchor, size).ToList();
        if (footprint.Any(t => _editorTiles.CellAt(t) is not { HasFloor: true, Wall: TileWallKind.None, DeviceId: null }))
            return;
        var deviceId = $"device-{placeAnchor.X}-{placeAnchor.Y}";
        foreach (var occupied in footprint)
        {
            _editorTiles.PlaceDevice(occupied, deviceId);
            _editorDeviceFootprint[occupied] = placeAnchor;
        }
        _editorDeviceKinds[placeAnchor] = _editorSelectedDeviceKind;
        foreach (var occupied in footprint)
            EvictTerminalsAtJunctions(occupied);
    }

    private static IEnumerable<TileCoord> DeviceFootprintTiles(TileCoord anchor, int size)
    {
        for (var dx = 0; dx < size; dx++)
            for (var dy = 0; dy < size; dy++)
                yield return new TileCoord(anchor.X + dx, anchor.Y + dy);
    }

    // Direct user request ("привязана не к краю а к центру") - the tile under the cursor becomes the
    // footprint's CENTER, not its top-left corner (matching RimWorld-style placement, where a big
    // object follows the cursor from its own middle). For an even size like 4, there's no single
    // centre tile, so the anchor lands 2 tiles back on each axis - the cursor's own tile ends up one
    // of the 4 centre-most tiles rather than a true geometric midpoint, the closest an even footprint
    // can get. Returns the top-left anchor DeviceFootprintTiles/_editorDeviceFootprint already key on
    // internally - a 1x1 device (size 1) resolves to anchor == clicked, unchanged from before.
    private static TileCoord FootprintAnchorFor(TileCoord clicked, int size) =>
        new(clicked.X - size / 2, clicked.Y - size / 2);

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
        _editorZones.Add(new EditorZone(name, tiles));
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
