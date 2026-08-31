using System.Collections.Generic;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

// Serializable snapshot of the Ship Editor's tile canvas (Game1.ShipEditor.cs's _editorTiles/
// _editorDeviceKinds/_editorZones) - direct user request ("сохранять построенные корабли и потом
// их загружать"). CustomShipDefinition (Room rectangles, already saved/loaded via CustomShipStore)
// is a LOSSY, one-way export of this data - Game1.ShipEditor.TileBridge.cs derives room rectangles
// from sealed regions, doors/airlocks from door tiles, but there's no way back from a
// CustomRoomDef to the original wall/door/terminal/zone-name layout that produced it. So a design
// gets saved and reloaded from THIS file instead, sitting alongside the existing
// CustomShipDefinition file rather than replacing it (Play/the status line still read the derived
// definition, computed fresh from whatever this file restores into the canvas).
public sealed record CustomShipTileCanvas(
    IReadOnlyList<CustomShipTileCanvas.TileRecord> Tiles,
    IReadOnlyList<CustomShipTileCanvas.DeviceRecord> Devices,
    IReadOnlyList<CustomShipTileCanvas.ZoneRecord> Zones)
{
    // One entry per tile the player ever painted (TileGrid.Cells only ever holds cells with
    // HasFloor true - SetFloor(false) removes the dictionary entry entirely - so this always is,
    // but the field stays explicit rather than assumed). Wall/DoorOpen/TerminalId/TerminalWallSide
    // mirror TileCell directly; WallHp isn't persisted - the editor never damages a wall, every
    // reload gets a fresh full-health one, same as a newly-painted tile would.
    public sealed record TileRecord(int X, int Y, bool HasFloor, TileWallKind Wall, bool DoorOpen,
        string? TerminalId, TileSide? TerminalWallSide);

    // Only the device's own anchor (top-left) tile and kind - DeviceFootprintSize(Kind) recomputes
    // which other tiles it occupies on load (Game1.ShipEditor.cs's own DeviceFootprintTiles), so a
    // multi-tile device's other occupied cells don't need their own separate record.
    public sealed record DeviceRecord(int X, int Y, CustomDeviceKind Kind);

    public sealed record ZoneRecord(string Name, IReadOnlyList<TilePos> Tiles);

    public readonly record struct TilePos(int X, int Y);
}
