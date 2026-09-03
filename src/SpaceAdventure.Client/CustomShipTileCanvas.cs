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
    IReadOnlyList<CustomShipTileCanvas.ZoneRecord> Zones,
    IReadOnlyList<CustomShipTileCanvas.EngineRecord>? EnginesRaw = null)
{
    // Defaulted/nullable (same convention CustomShipDefinition.EnginesRaw/Engines already uses) so a
    // save file from before the real ShipEngine editor tool existed - which has no "EnginesRaw"
    // property at all - deserializes to an empty list instead of null/crashing.
    public IReadOnlyList<EngineRecord> Engines { get; init; } = EnginesRaw ?? new List<EngineRecord>();

    // One entry per tile the player ever painted (TileGrid.Cells only ever holds cells with
    // HasFloor true - SetFloor(false) removes the dictionary entry entirely - so this always is,
    // but the field stays explicit rather than assumed). Wall/DoorOpen/TerminalId/TerminalWallSide/
    // WallMaterial/DoorGroupId mirror TileCell directly; WallHp isn't persisted - the editor never
    // damages a wall, every reload gets a fresh full-health one, same as a newly-painted tile would.
    // WallMaterial defaults to Standard and DoorGroupId to null for every save from before either
    // existed, matching what a freshly-painted tile already defaulted to.
    public sealed record TileRecord(int X, int Y, bool HasFloor, TileWallKind Wall, bool DoorOpen,
        string? TerminalId, TileSide? TerminalWallSide,
        WallMaterial WallMaterial = WallMaterial.Standard, string? DoorGroupId = null);

    // Only the device's own anchor (top-left) tile and kind - DeviceFootprintSize(Kind) recomputes
    // which other tiles it occupies on load (Game1.ShipEditor.cs's own DeviceFootprintTiles), so a
    // multi-tile device's other occupied cells don't need their own separate record.
    public sealed record DeviceRecord(int X, int Y, CustomDeviceKind Kind);

    // A real ShipEngine (ShipEngine.cs) placed via the Engine editor tool - only the Control tile's
    // own anchor and facing; EngineFootprintTiles(anchor, Facing) recomputes the Bulkhead/Nozzle tiles
    // on load, same "anchor is enough" convention DeviceRecord already uses for a multi-tile device.
    public sealed record EngineRecord(int X, int Y, TileSide Facing);

    // Kind defaults to null for every save from before typed zones existed - an untyped, purely
    // cosmetic zone, same as today.
    public sealed record ZoneRecord(string Name, IReadOnlyList<TilePos> Tiles, ShipZoneKind? Kind = null);

    public readonly record struct TilePos(int X, int Y);
}
