using Anabiosis.Shared.Model;

// Direct user bug report ("4 половублочных стены между 2 отсеками видны в редакторе, но пропадают
// в игре") - a half-block wall the player paints right at the edge of an otherwise uniform open
// floor region (e.g. two nubs narrowing a corridor from both sides) makes RectilinearDecomposition
// carve off a thin one-tile-wide "wing" strip on either side of the narrowing. TileShipBuilder's
// step 3.5 used to wrongly absorb that notch tile into the wing's own rect (indistinguishable from
// a genuine exterior wall by IsWallLine alone) - Station.IsUnitCoveredBySameRoom then correctly
// suppressed one of the two resulting wall passes, but the other one walled the wing's entire
// column, silently swallowing what should have stayed open floor next to the half-block notch.
internal static partial class TestRunner
{
    // One 7-wide, 14-tall open floor region (two "compartments" joined by a corridor with no door
    // between them - this bug only needs one connected region) with 4 half-block wall tiles
    // narrowing rows 6-7: two nubs on the West edge (column 0) and two on the East edge (column 6),
    // leaving columns 1-5 open the whole way through.
    private static TileGrid BuildHalfBlockNotchGrid()
    {
        var grid = new TileGrid();
        for (var y = 0; y < 14; y++)
            for (var x = 0; x <= 6; x++)
                grid.SetFloor(new TileCoord(x, y), true);

        grid.SetWall(new TileCoord(0, 6), TileWallKind.Solid);
        grid.SetWall(new TileCoord(0, 7), TileWallKind.Solid);
        grid.SetWall(new TileCoord(6, 6), TileWallKind.Solid);
        grid.SetWall(new TileCoord(6, 7), TileWallKind.Solid);
        grid.SetWallOpenSide(new TileCoord(0, 6), TileSide.West);
        grid.SetWallOpenSide(new TileCoord(0, 7), TileSide.West);
        grid.SetWallOpenSide(new TileCoord(6, 6), TileSide.East);
        grid.SetWallOpenSide(new TileCoord(6, 7), TileSide.East);
        return grid;
    }

    private static bool TileShipBuilder_HalfBlockNotchAtRegionEdge_StaysOpenAndKeepsItsWallOpenSide()
    {
        var (definition, errors) = BuildTileDefinition(BuildHalfBlockNotchGrid());
        if (definition is null || errors.Count > 0)
            return false;

        var notchTiles = new[]
        {
            new TileCoord(0, 6), new TileCoord(0, 7), new TileCoord(6, 6), new TileCoord(6, 7),
        };
        // The bug: these 4 tiles used to get silently absorbed into a wing subrect's own boundary
        // instead of staying uncovered - confirm they're still exported as bolted-on supplemental
        // wall material (not swallowed into any room's Rects) and still carry their WallOpenSide.
        foreach (var coord in notchTiles)
            if (!definition.SupplementalWallTiles.Contains(coord))
                return false;
        if (definition.WallOpenSides.Count != 4)
            return false;

        // End-to-end: rasterize the exported rooms exactly as Ship.Custom.cs does, then apply the
        // same corrective passes, and confirm the ACTUAL final tiles land correctly - the notch
        // stays a half-block wall, and the floor immediately above/below it (rows 5, 8) is not
        // consumed by the bug's own "whole column walled" failure mode.
        var rooms = definition.Rooms.Select(r => new Room(r.Id, r.Name, r.Rects)).ToList();
        var finalTiles = TileGridRasterizer.FromRooms(rooms, new List<Door>(), new List<AirlockOuterDoor>());
        foreach (var coord in definition.SupplementalWallTiles)
        {
            finalTiles.SetFloor(coord, true);
            finalTiles.SetWall(coord, TileWallKind.Solid);
        }
        foreach (var coord in definition.ForcedFloorTiles)
            if (finalTiles.CellAt(coord) is { Wall: not TileWallKind.None })
                finalTiles.SetWall(coord, TileWallKind.None);
        foreach (var openSide in definition.WallOpenSides)
            finalTiles.SetWallOpenSide(new TileCoord(openSide.X, openSide.Y), openSide.Side);

        foreach (var coord in notchTiles)
            if (finalTiles.CellAt(coord) is not { Wall: TileWallKind.Solid, WallOpenSide: not null })
                return false; // the bug: notch either vanished, or lost its half-block flag

        return finalTiles.CellAt(new TileCoord(0, 5)) is { Wall: TileWallKind.None }
            && finalTiles.CellAt(new TileCoord(0, 8)) is { Wall: TileWallKind.None }
            && finalTiles.CellAt(new TileCoord(6, 5)) is { Wall: TileWallKind.None }
            && finalTiles.CellAt(new TileCoord(6, 8)) is { Wall: TileWallKind.None };
    }
}
