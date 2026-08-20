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
// Menu.cs) where the player draws their own hull: drag out rooms on a grid, click a shared wall to
// add a door or an outer wall to add an airlock, drop system devices from a palette. "Play" hands
// the finished CustomShipDefinition to StartHostedSession under ShipKind.Custom - see Ship.Custom.cs
// for how it becomes a real Ship, and CustomShipValidator for what "finished" requires.
//
// Every edit here auto-saves to CustomShipStore, the same way a physical sketch doesn't need a
// separate "save" step to survive - Save/New/Play only exist for explicit control, not because
// anything would otherwise be lost.
public partial class Game1
{
    private const int ShipEditorCellSize = 24;
    private const int ShipEditorGridCols = 32;
    private const int ShipEditorGridRows = 18;
    private static readonly Rectangle ShipEditorCanvas =
        new(20, 64, ShipEditorGridCols * ShipEditorCellSize, ShipEditorGridRows * ShipEditorCellSize);

    private enum EditorTool { Room, DoorAirlock, Device }
    private enum EditorAction { Back, Clear, Save, Play }

    private static readonly TurretMountSide[] EditorMountSides =
        { TurretMountSide.Aft, TurretMountSide.Fore, TurretMountSide.Port, TurretMountSide.Starboard };
    private static readonly float[] EditorForwardOptions = { 0f, 90f, 180f, -90f };

    private EditorTool _editorTool = EditorTool.Room;
    private CustomDeviceKind _editorDeviceKind = CustomDeviceKind.Reactor;
    private TurretMountSide _editorTurretMountSide = TurretMountSide.Aft;

    private List<CustomRoomDef> _editorRooms = new();
    private List<CustomDoorDef> _editorDoors = new();
    private List<CustomAirlockDef> _editorAirlocks = new();
    private List<CustomDeviceDef> _editorDevices = new();
    private string _editorShipName = "Мой корабль";
    private float _editorForwardDegrees = 0f;
    private int _editorRoomCounter = 1;

    private (int X, int Y)? _editorRoomDragStart;

    private ButtonState _prevEditorLeftMouseButton = ButtonState.Released;
    private ButtonState _prevEditorRightMouseButton = ButtonState.Released;

    // Reached from the main menu's КАМПАНИЯ section (Game1.Menu.cs) - loads whatever was there last
    // time, or a blank hull the first time.
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
        _editorRoomDragStart = null;
        _editorTool = EditorTool.Room;
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

    private CustomShipDefinition BuildEditorDefinition() => new(
        _editorShipName, _editorRooms.ToArray(), _editorDoors.ToArray(), _editorAirlocks.ToArray(),
        _editorDevices.ToArray(), _editorForwardDegrees);

    private void SaveEditorDefinition() => CustomShipStore.Save(BuildEditorDefinition());

    private void HandleShipEditorScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var leftDown = mouse.LeftButton == ButtonState.Pressed;
        var leftClicked = leftDown && _prevEditorLeftMouseButton == ButtonState.Released;
        var leftReleased = !leftDown && _prevEditorLeftMouseButton == ButtonState.Pressed;
        var rightClicked = mouse.RightButton == ButtonState.Pressed && _prevEditorRightMouseButton == ButtonState.Released;
        _prevEditorLeftMouseButton = mouse.LeftButton;
        _prevEditorRightMouseButton = mouse.RightButton;

        if (HandleShipEditorSidebarClick(leftClicked))
            return;

        var cell = GridCellAt(_designMouse);
        switch (_editorTool)
        {
            case EditorTool.Room:
                HandleRoomToolInput(cell, leftClicked, leftReleased, rightClicked);
                break;
            case EditorTool.DoorAirlock:
                if (leftClicked || rightClicked)
                    ToggleNearestDoorOrAirlock(_designMouse);
                break;
            case EditorTool.Device:
                if (leftClicked && cell is { } placeCell)
                    PlaceEditorDevice(placeCell);
                if (rightClicked && cell is { } removeCell)
                    RemoveEditorDeviceAt(removeCell);
                break;
        }
    }

    private void HandleRoomToolInput((int X, int Y)? cell, bool leftClicked, bool leftReleased, bool rightClicked)
    {
        if (rightClicked && cell is { } removeCell)
        {
            var room = RoomAt(removeCell.X, removeCell.Y);
            if (room is not null)
                DeleteEditorRoom(room);
            return;
        }

        if (leftClicked && cell is { } startCell && RoomAt(startCell.X, startCell.Y) is null)
            _editorRoomDragStart = startCell;
        else if (leftReleased && _editorRoomDragStart is not null)
        {
            if (cell is { } endCell)
                CommitEditorRoomDrag(endCell.X, endCell.Y);
            else
                _editorRoomDragStart = null;
        }
    }

    private void CommitEditorRoomDrag(int endX, int endY)
    {
        var start = _editorRoomDragStart!.Value;
        _editorRoomDragStart = null;
        var x0 = Math.Min(start.X, endX);
        var y0 = Math.Min(start.Y, endY);
        var w = Math.Abs(endX - start.X) + 1;
        var h = Math.Abs(endY - start.Y) + 1;
        if (_editorRooms.Any(r => RoomsOverlap(r, x0, y0, w, h)))
            return;
        var n = _editorRoomCounter++;
        _editorRooms.Add(new CustomRoomDef($"room-{n}", $"Отсек {n}", x0, y0, w, h));
        SaveEditorDefinition();
    }

    private void DeleteEditorRoom(CustomRoomDef room)
    {
        _editorRooms.Remove(room);
        _editorDoors.RemoveAll(d => d.RoomAId == room.Id || d.RoomBId == room.Id);
        _editorAirlocks.RemoveAll(a => a.RoomId == room.Id);
        _editorDevices.RemoveAll(d => RoomContains(room, d.X, d.Y));
        SaveEditorDefinition();
    }

    private void PlaceEditorDevice((int X, int Y) cell)
    {
        if (RoomAt(cell.X, cell.Y) is null)
            return;
        var x = cell.X + 0.5f;
        var y = cell.Y + 0.5f;
        _editorDevices.RemoveAll(d => d.X == x && d.Y == y);
        if (CustomDeviceCatalog.IsSingleton(_editorDeviceKind))
            _editorDevices.RemoveAll(d => d.Kind == _editorDeviceKind);
        var mountSide = _editorDeviceKind is CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser
            ? _editorTurretMountSide
            : TurretMountSide.Aft;
        _editorDevices.Add(new CustomDeviceDef(_editorDeviceKind, x, y, mountSide));
        SaveEditorDefinition();
    }

    private void RemoveEditorDeviceAt((int X, int Y) cell)
    {
        var x = cell.X + 0.5f;
        var y = cell.Y + 0.5f;
        if (_editorDevices.RemoveAll(d => d.X == x && d.Y == y) > 0)
            SaveEditorDefinition();
    }

    private void ToggleNearestDoorOrAirlock(Point designMouse)
    {
        const float maxDistance = 14f;
        var mousePos = new Vector2(designMouse.X, designMouse.Y);
        var bestDistance = maxDistance;
        (string A, string B)? bestDoor = null;
        (string RoomId, EdgeSide Side)? bestAirlock = null;

        foreach (var candidate in GetEditorDoorCandidates())
        {
            var distance = Vector2.Distance(mousePos, candidate.ScreenPos);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            bestDoor = (candidate.RoomAId, candidate.RoomBId);
            bestAirlock = null;
        }
        foreach (var candidate in GetEditorAirlockCandidates())
        {
            var distance = Vector2.Distance(mousePos, candidate.ScreenPos);
            if (distance >= bestDistance)
                continue;
            bestDistance = distance;
            bestAirlock = (candidate.RoomId, candidate.Side);
            bestDoor = null;
        }

        if (bestDoor is { } door)
        {
            var index = _editorDoors.FindIndex(d =>
                (d.RoomAId == door.A && d.RoomBId == door.B) || (d.RoomAId == door.B && d.RoomBId == door.A));
            if (index >= 0)
                _editorDoors.RemoveAt(index);
            else
                _editorDoors.Add(new CustomDoorDef(door.A, door.B));
            SaveEditorDefinition();
        }
        else if (bestAirlock is { } airlock)
        {
            var index = _editorAirlocks.FindIndex(a => a.RoomId == airlock.RoomId && a.Side == airlock.Side);
            if (index >= 0)
                _editorAirlocks.RemoveAt(index);
            else
                _editorAirlocks.Add(new CustomAirlockDef(airlock.RoomId, airlock.Side));
            SaveEditorDefinition();
        }
    }

    private void HandleShipEditorPlayClicked()
    {
        var definition = BuildEditorDefinition();
        if (CustomShipValidator.Validate(definition).Count > 0)
            return; // Play is drawn disabled in this state - a stray click just does nothing
        CustomShipStore.Save(definition);
        SaveStore.Delete(); // a fresh run on this hull, same as picking a fixed class on ShipSelect
        StartHostedSession(ShipKind.Custom, loadFrom: null, customShip: definition);
    }

    private void HandleShipEditorClearClicked()
    {
        _editorRooms.Clear();
        _editorDoors.Clear();
        _editorAirlocks.Clear();
        _editorDevices.Clear();
        _editorRoomCounter = 1;
        SaveEditorDefinition();
    }

    private static (int X, int Y)? GridCellAt(Point designMouse)
    {
        if (!ShipEditorCanvas.Contains(designMouse))
            return null;
        return ((designMouse.X - ShipEditorCanvas.X) / ShipEditorCellSize,
            (designMouse.Y - ShipEditorCanvas.Y) / ShipEditorCellSize);
    }

    private static Vector2 WorldToEditorScreen(float worldX, float worldY) =>
        new(ShipEditorCanvas.X + worldX * ShipEditorCellSize, ShipEditorCanvas.Y + worldY * ShipEditorCellSize);

    private CustomRoomDef? RoomAt(int gridX, int gridY) =>
        _editorRooms.FirstOrDefault(r => gridX >= r.X && gridX < r.X + r.Width && gridY >= r.Y && gridY < r.Y + r.Height);

    private static bool RoomContains(CustomRoomDef room, float x, float y) =>
        x >= room.X && x <= room.X + room.Width && y >= room.Y && y <= room.Y + room.Height;

    private static bool RoomsOverlap(CustomRoomDef r, int x, int y, int w, int h) =>
        r.X < x + w && x < r.X + r.Width && r.Y < y + h && y < r.Y + r.Height;

    private IEnumerable<(string RoomAId, string RoomBId, Vector2 ScreenPos, bool HasDoor)> GetEditorDoorCandidates()
    {
        foreach (var overlap in ShipLayoutGeometry.FindRoomPairOverlaps(_editorRooms))
        {
            var worldX = overlap.Vertical ? overlap.At : overlap.OverlapCenter;
            var worldY = overlap.Vertical ? overlap.OverlapCenter : overlap.At;
            var hasDoor = _editorDoors.Any(d =>
                (d.RoomAId == overlap.RoomAId && d.RoomBId == overlap.RoomBId) ||
                (d.RoomAId == overlap.RoomBId && d.RoomBId == overlap.RoomAId));
            yield return (overlap.RoomAId, overlap.RoomBId, WorldToEditorScreen(worldX, worldY), hasDoor);
        }
    }

    private IEnumerable<(string RoomId, EdgeSide Side, Vector2 ScreenPos, bool HasAirlock)> GetEditorAirlockCandidates()
    {
        var overlaps = ShipLayoutGeometry.FindRoomPairOverlaps(_editorRooms);
        foreach (var room in _editorRooms)
        {
            foreach (var side in new[] { EdgeSide.Top, EdgeSide.Bottom, EdgeSide.Left, EdgeSide.Right })
            {
                if (ShipLayoutGeometry.SideHasNeighbor(room, side, overlaps))
                    continue;
                var (midX, midY) = ShipLayoutGeometry.SideMidpoint(room, side);
                var hasAirlock = _editorAirlocks.Any(a => a.RoomId == room.Id && a.Side == side);
                yield return (room.Id, side, WorldToEditorScreen(midX, midY), hasAirlock);
            }
        }
    }
}
