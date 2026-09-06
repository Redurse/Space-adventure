using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // Direct user request ("система отсеков по-другому") - a compartment's own wall-ring tiles are
    // now allowed to coincide with an EXISTING compartment's wall tiles (representing two
    // compartments placed flush/overlapping and sharing that one wall), while its INTERIOR must
    // still never overlap anything at all. These tests exercise CompartmentPlacer.Stamp directly
    // with two plain 5x5 entries (no devices/engines - irrelevant to this rule), not real catalog
    // content.
    private static CompartmentCatalogEntry PlainSquareEntry(string id) => new(
        id, "Тест", CompartmentType.Cockpit, Width: 5, Height: 5,
        Devices: Array.Empty<CompartmentDeviceSpec>(), Engines: Array.Empty<CompartmentEngineSpec>());

    // B's anchor is offset by exactly (Width-1) from A - B's own left wall column (local x=0) lands
    // on the SAME absolute column as A's own right wall column (local x=4), a perfect wall-over-wall
    // overlap the whole column's height, while both interiors (A: x1-3, B: x5-7) stay well apart.
    private static bool CompartmentPlacer_WallOverWallOverlap_IsAllowedAndDedupes()
    {
        var grid = new TileGrid();
        var a = PlainSquareEntry("a");
        var b = PlainSquareEntry("b");

        var resultA = CompartmentPlacer.Stamp(grid, a, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a-1");
        if (!resultA.Success)
            return false;

        var resultB = CompartmentPlacer.Stamp(grid, b, new TileCoord(4, 0), rotationSteps: 0, instanceId: "b-1");
        if (!resultB.Success)
            return false;

        // The shared column (x=4) must still carry exactly one, undamaged wall tile at every row,
        // tagged as compartment-placed (from A's original stamp - B's own attempt at that same tile
        // was skipped entirely, never overwriting it).
        for (var y = 0; y < 5; y++)
        {
            var cell = grid.CellAt(new TileCoord(4, y));
            if (cell is not { Wall: TileWallKind.Solid, WallFromCompartment: true })
                return false;
        }

        // Both interiors are still genuinely open floor with no wall.
        for (var y = 1; y <= 3; y++)
        {
            if (grid.CellAt(new TileCoord(2, y)) is not { HasFloor: true, Wall: TileWallKind.None })
                return false;
            if (grid.CellAt(new TileCoord(6, y)) is not { HasFloor: true, Wall: TileWallKind.None })
                return false;
        }
        return true;
    }

    // B's anchor is offset by (Width-2) from A - B's own left wall column would land one tile INSIDE
    // A's own interior (not on A's wall at all) - still rejected, exactly as before this milestone.
    private static bool CompartmentPlacer_WallOverlappingAnotherRoomsInterior_IsRejected()
    {
        var grid = new TileGrid();
        var a = PlainSquareEntry("a");
        var b = PlainSquareEntry("b");

        var resultA = CompartmentPlacer.Stamp(grid, a, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a-1");
        if (!resultA.Success)
            return false;

        var resultB = CompartmentPlacer.Stamp(grid, b, new TileCoord(3, 0), rotationSteps: 0, instanceId: "b-1");
        return !resultB.Success;
    }
}
