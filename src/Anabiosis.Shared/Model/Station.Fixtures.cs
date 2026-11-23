namespace Anabiosis.Shared.Model;

// Everything the author drew INTO a station besides its rooms: lamps, terminals, junction boxes, furniture, workbenches, the reactor, the
// helm... Same record types a Ship carries (so the shared renderers, lighting and interaction code handle them as they do on a ship), in the
// station's docked frame - the ship's own interior frame, so while docked they simply join the ship's lists in the snapshot.
public sealed record StationFixtures(
    IReadOnlyList<WallLamp> WallLamps,
    IReadOnlyList<Terminal> Terminals,
    IReadOnlyList<JunctionBox> JunctionBoxes,
    IReadOnlyList<DecorativeDevice> DecorativeDevices,
    IReadOnlyList<CommsConsole> CommsConsoles,
    IReadOnlyList<ShipStatusMonitor> StatusMonitors,
    IReadOnlyList<ReactorBlock> Reactors,
    IReadOnlyList<PowerDistributionBlock> Distributions,
    IReadOnlyList<BatteryBlock> Batteries,
    IReadOnlyList<NavigationConsole> Navigations,
    IReadOnlyList<HelmConsole> Helms)
{
    public static StationFixtures Empty { get; } = new(
        Array.Empty<WallLamp>(), Array.Empty<Terminal>(), Array.Empty<JunctionBox>(), Array.Empty<DecorativeDevice>(), Array.Empty<CommsConsole>(),
        Array.Empty<ShipStatusMonitor>(), Array.Empty<ReactorBlock>(), Array.Empty<PowerDistributionBlock>(), Array.Empty<BatteryBlock>(),
        Array.Empty<NavigationConsole>(), Array.Empty<HelmConsole>());

    // Whether a character standing at `position` (docked frame) is within reach of a device of one of the given kinds.
    public bool HasDecorativeNear(CustomDeviceKind kind, Vec2 position, float reach) =>
        DecorativeDevices.Any(d => d.Kind == kind && (d.Position - position).Length() <= reach);
}

// The wall corrections of the drawing that live outside the rooms' rectangles - the extra wall tiles, the tiles forced back to floor, and the half-block
// (half-thick) walls, e.g. the partitions inside a compartment. Same three lists, same order, as a Ship carries (Ship.Custom.cs, ClientTileGrid.
// ApplySupplementalTiles): the server stamps them onto Station.Tiles, the client replays them on its own copy so what is drawn is what is bumped into.
public sealed record StationTileFixes(
    IReadOnlyList<TileCoord> SupplementalWalls,
    IReadOnlyList<TileCoord> ForcedFloor,
    IReadOnlyList<CustomWallOpenSideDef> WallOpenSides)
{
    public static StationTileFixes Empty { get; } = new(Array.Empty<TileCoord>(), Array.Empty<TileCoord>(), Array.Empty<CustomWallOpenSideDef>());

    public bool IsEmpty => SupplementalWalls.Count == 0 && ForcedFloor.Count == 0 && WallOpenSides.Count == 0;

    public void Apply(TileGrid tiles)
    {
        foreach (var coord in SupplementalWalls)
        {
            tiles.SetFloor(coord, true);
            tiles.SetWall(coord, TileWallKind.Solid);
        }
        foreach (var coord in ForcedFloor)
            if (tiles.CellAt(coord) is { Wall: not TileWallKind.None })
                tiles.SetWall(coord, TileWallKind.None);
        foreach (var open in WallOpenSides)
            tiles.SetWallOpenSide(new TileCoord(open.X, open.Y), open.Side);
    }
}
