using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // Direct user request ("убери механику чтобы при накладывании отсеков... они могли наезжать
    // друг на друга... сделай это невозможным") - a compartment's own footprint, wall-ring tiles
    // included, must now land on completely empty space; landing on ANY existing tile at all -
    // even just one wall coinciding with another compartment's own wall - is rejected. This
    // supersedes an earlier, more permissive rule ("система отсеков по-другому") that allowed
    // exactly the wall-over-wall overlap this test now proves is refused. These tests exercise
    // CompartmentPlacer.Stamp directly with two plain 5x5 entries (no devices/engines - irrelevant
    // to this rule), not real catalog content.
    private static CompartmentCatalogEntry PlainSquareEntry(string id) => new(
        id, "Тест", CompartmentType.Cockpit, Width: 5, Height: 5,
        Devices: Array.Empty<CompartmentDeviceSpec>(), Engines: Array.Empty<CompartmentEngineSpec>());

    // B's anchor is offset by exactly (Width-1) from A - B's own left wall column (local x=0) would
    // land on the SAME absolute column as A's own right wall column (local x=4), a perfect wall-
    // over-wall overlap the whole column's height, even though both interiors (A: x1-3, B: x5-7)
    // stay well apart - still rejected outright, and the grid is left completely untouched by B's
    // own failed attempt (A's own wall column survives exactly as it was).
    private static bool CompartmentPlacer_WallOverWallOverlap_IsRejected()
    {
        var grid = new TileGrid();
        var a = PlainSquareEntry("a");
        var b = PlainSquareEntry("b");

        var resultA = CompartmentPlacer.Stamp(grid, a, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a-1");
        if (!resultA.Success)
            return false;

        var resultB = CompartmentPlacer.Stamp(grid, b, new TileCoord(4, 0), rotationSteps: 0, instanceId: "b-1");
        if (resultB.Success)
            return false;

        // A's own wall column (x=4) must be completely unaffected by B's rejected attempt.
        for (var y = 0; y < 5; y++)
        {
            var cell = grid.CellAt(new TileCoord(4, y));
            if (cell is not { Wall: TileWallKind.Solid, WallFromCompartment: true })
                return false;
        }
        // B never got so much as its own interior floor down - a rejected Stamp must leave the grid
        // completely untouched, not partially applied.
        return grid.CellAt(new TileCoord(6, 2)) is null;
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
