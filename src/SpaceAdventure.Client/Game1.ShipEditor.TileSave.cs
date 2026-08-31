using System.Collections.Generic;
using System.Linq;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

// Round-trips the tile canvas (_editorTiles/_editorDeviceKinds/_editorZones) through
// CustomShipTileCanvas - direct user request ("сохранять построенные корабли между сессиями и
// потом их загружать"). Separate from Game1.ShipEditor.TileBridge.cs's CustomShipDefinition export:
// that conversion is lossy (Room rectangles can't remember individual wall/door/terminal tiles or
// zone names), so restoring a design faithfully on Load replays the SAME tile canvas data instead
// of trying to reconstruct it from the derived rooms.
public partial class Game1
{
    private CustomShipTileCanvas BuildEditorTileCanvas()
    {
        var tiles = _editorTiles.Cells
            .Select(kv => new CustomShipTileCanvas.TileRecord(
                kv.Key.X, kv.Key.Y, kv.Value.HasFloor, kv.Value.Wall, kv.Value.DoorOpen,
                kv.Value.TerminalId, kv.Value.TerminalWallSide))
            .ToList();
        var devices = _editorDeviceKinds
            .Select(kv => new CustomShipTileCanvas.DeviceRecord(kv.Key.X, kv.Key.Y, kv.Value))
            .ToList();
        var zones = _editorZones
            .Select(z => new CustomShipTileCanvas.ZoneRecord(
                z.Name, z.Tiles.Select(t => new CustomShipTileCanvas.TilePos(t.X, t.Y)).ToList()))
            .ToList();
        return new CustomShipTileCanvas(tiles, devices, zones);
    }

    // Replays the saved data through the SAME TileGrid mutators the editor's own tools use (floors
    // first, then walls/doors - a wall/device/terminal's own precondition needs the floor already
    // there - then devices via the same anchor+footprint helper HandleDeviceToolInput uses, then
    // terminals, which need their mount-side wall neighbour already placed). Wall HP isn't restored
    // - every reloaded wall comes back full-health, same as a freshly-painted one would.
    private void ApplyEditorTileCanvas(CustomShipTileCanvas canvas)
    {
        _editorTiles = new TileGrid();
        _editorDeviceKinds.Clear();
        _editorDeviceFootprint.Clear();
        _editorZones.Clear();

        foreach (var t in canvas.Tiles)
            _editorTiles.SetFloor(new TileCoord(t.X, t.Y), true);
        foreach (var t in canvas.Tiles)
        {
            if (t.Wall == TileWallKind.None)
                continue;
            var coord = new TileCoord(t.X, t.Y);
            _editorTiles.SetWall(coord, t.Wall);
            if (t.Wall == TileWallKind.Door && t.DoorOpen)
                _editorTiles.SetDoorOpen(coord, true);
        }
        foreach (var d in canvas.Devices)
        {
            var anchor = new TileCoord(d.X, d.Y);
            var deviceId = $"device-{d.X}-{d.Y}";
            foreach (var occupied in DeviceFootprintTiles(anchor, DeviceFootprintSize(d.Kind)))
            {
                _editorTiles.PlaceDevice(occupied, deviceId);
                _editorDeviceFootprint[occupied] = anchor;
            }
            _editorDeviceKinds[anchor] = d.Kind;
        }
        foreach (var t in canvas.Tiles)
            if (t.TerminalId is not null && t.TerminalWallSide is { } side)
                _editorTiles.PlaceTerminal(new TileCoord(t.X, t.Y), side, t.TerminalId);
        foreach (var z in canvas.Zones)
            _editorZones.Add(new EditorZone(z.Name, z.Tiles.Select(p => new TileCoord(p.X, p.Y)).ToHashSet()));
    }
}
