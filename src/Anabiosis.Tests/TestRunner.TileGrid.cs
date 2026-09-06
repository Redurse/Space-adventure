using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // M70 (humble-soaring-cat.md) - TileGrid is a pure Shared-module data structure at this stage,
    // not yet wired into Ship/World (that's M71+). Tests exercise it directly, the same way
    // TestRunner.ShipBuilding.cs tests RoomGraphConnectivity against hand-built graphs rather than
    // through a real hull.

    private static bool TileGrid_AdjacentFloorTilesMergeIntoOneRegion()
    {
        var grid = new TileGrid();
        grid.SetFloor(new TileCoord(0, 0), true);
        grid.SetFloor(new TileCoord(1, 0), true);

        var regionA = grid.RegionIdAt(new TileCoord(0, 0));
        var regionB = grid.RegionIdAt(new TileCoord(1, 0));
        if (regionA is null || regionA != regionB)
            return false;
        return grid.Regions.Count == 1 && grid.Regions[regionA.Value].Tiles.Count == 2;
    }

    private static bool TileGrid_SolidWallSplitsRegionThenNoneMergesItBack()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var bridge = new TileCoord(1, 0);
        var c = new TileCoord(2, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(bridge, true);
        grid.SetFloor(c, true);
        if (grid.Regions.Count != 1)
            return false;

        grid.SetWall(bridge, TileWallKind.Solid);
        if (grid.Regions.Count != 2)
            return false;
        if (grid.RegionIdAt(bridge) is not null)
            return false; // a solid wall tile is not a region member at all
        var regionA = grid.RegionIdAt(a);
        var regionC = grid.RegionIdAt(c);
        if (regionA is null || regionC is null || regionA == regionC)
            return false;

        grid.SetWall(bridge, TileWallKind.None);
        if (grid.Regions.Count != 1)
            return false;
        return grid.RegionIdAt(a) == grid.RegionIdAt(c) && grid.Regions[grid.RegionIdAt(a)!.Value].Tiles.Count == 3;
    }

    // A door is a toggleable wall variant, but for REGION topology it behaves like a solid wall no
    // matter its open/closed state - only movement (M73) cares about DoorOpen. Confirmed directly
    // against TileGrid.IsWalkable so the two concepts don't get conflated.
    private static bool TileGrid_OpenDoorNeverMergesRegionsButIsWalkable()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var door = new TileCoord(1, 0);
        var c = new TileCoord(2, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(door, true);
        grid.SetFloor(c, true);
        grid.SetWall(door, TileWallKind.Door);
        grid.SetDoorOpen(door, true);

        if (grid.Regions.Count != 2)
            return false;
        if (grid.RegionIdAt(a) == grid.RegionIdAt(c))
            return false;
        return TileGrid.IsWalkable(grid.CellAt(door)!);
    }

    private static bool TileGrid_LeaksToVacuumOnlyAtGenuinelyOpenEdge()
    {
        var openGrid = new TileGrid();
        openGrid.SetFloor(new TileCoord(0, 0), true);
        var openRegionId = openGrid.RegionIdAt(new TileCoord(0, 0))!.Value;
        if (!openGrid.Regions[openRegionId].LeaksToVacuum)
            return false; // a lone tile with nothing around it borders vacuum on all four sides

        var sealedGrid = new TileGrid();
        var center = new TileCoord(1, 1);
        sealedGrid.SetFloor(center, true);
        foreach (var side in TileSideExtensions.All)
        {
            var wallCoord = side.Offset(center);
            sealedGrid.SetFloor(wallCoord, true);
            sealedGrid.SetWall(wallCoord, TileWallKind.Solid);
        }
        var sealedRegionId = sealedGrid.RegionIdAt(center)!.Value;
        return !sealedGrid.Regions[sealedRegionId].LeaksToVacuum;
    }

    private static bool TileGrid_PartialWallDamageKeepsTopologyBreachMergesRepairSplitsAgain()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var wall = new TileCoord(1, 0);
        var c = new TileCoord(2, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(wall, true);
        grid.SetFloor(c, true);
        grid.SetWall(wall, TileWallKind.Solid, hp: 100f);
        if (grid.Regions.Count != 2)
            return false;

        grid.DamageWall(wall, 40f); // 100 -> 60, still intact
        if (grid.Regions.Count != 2 || grid.RegionIdAt(a) == grid.RegionIdAt(c))
            return false;

        grid.DamageWall(wall, 60f); // 60 -> 0, breached: merges back into one region
        if (grid.Regions.Count != 1)
            return false;
        if (grid.RegionIdAt(a) != grid.RegionIdAt(c))
            return false;

        grid.RepairWall(wall, 10f, maxHp: 100f); // 0 -> 10, intact again: splits back apart
        if (grid.Regions.Count != 2)
            return false;
        return grid.RegionIdAt(a) != grid.RegionIdAt(c);
    }

    private static bool TileGrid_TerminalRequiresAdjacentWallAndNeverAffectsRegionsOrWalkability()
    {
        var grid = new TileGrid();
        var floor = new TileCoord(0, 0);
        grid.SetFloor(floor, true);

        var threwWithoutWall = false;
        try
        {
            grid.PlaceTerminal(floor, TileSide.East, "terminal-1");
        }
        catch (InvalidOperationException)
        {
            threwWithoutWall = true;
        }
        if (!threwWithoutWall)
            return false;

        var wallCoord = TileSide.East.Offset(floor);
        grid.SetFloor(wallCoord, true);
        grid.SetWall(wallCoord, TileWallKind.Solid);

        var regionBefore = grid.RegionIdAt(floor);
        var walkableBefore = TileGrid.IsWalkable(grid.CellAt(floor)!);

        grid.PlaceTerminal(floor, TileSide.East, "terminal-1");

        var regionAfter = grid.RegionIdAt(floor);
        var walkableAfter = TileGrid.IsWalkable(grid.CellAt(floor)!);
        return regionBefore == regionAfter && walkableBefore == walkableAfter && walkableAfter;
    }

    private static bool TileGrid_DevicePlacementBlocksWalkableButNotRegionMembership()
    {
        var grid = new TileGrid();
        var coord = new TileCoord(0, 0);
        grid.SetFloor(coord, true);
        var regionBefore = grid.RegionIdAt(coord);
        if (!TileGrid.IsWalkable(grid.CellAt(coord)!))
            return false;

        grid.PlaceDevice(coord, "reactor-1");

        var regionAfter = grid.RegionIdAt(coord);
        return regionBefore == regionAfter && !TileGrid.IsWalkable(grid.CellAt(coord)!);
    }

    // Removing floor from the middle of a long corridor must split it into exactly two pieces sized
    // by how far each half actually is from the break - the incremental recompute is a BFS bounded
    // by the OLD region's own tile count, not the whole grid, so this stays correct (and cheap) even
    // at a size where a naive full-grid flood-fill would be the wrong shape of algorithm.
    private static bool TileGrid_RemovingFloorFromLargeCorridorSplitsProportionally()
    {
        var grid = new TileGrid();
        const int length = 50;
        for (var x = 0; x < length; x++)
            grid.SetFloor(new TileCoord(x, 0), true);
        if (grid.Regions.Count != 1)
            return false;

        const int breakAt = 25;
        grid.SetFloor(new TileCoord(breakAt, 0), false);

        if (grid.Regions.Count != 2)
            return false;
        if (grid.RegionIdAt(new TileCoord(breakAt, 0)) is not null)
            return false;

        var leftRegionId = grid.RegionIdAt(new TileCoord(0, 0))!.Value;
        var rightRegionId = grid.RegionIdAt(new TileCoord(length - 1, 0))!.Value;
        if (leftRegionId == rightRegionId)
            return false;

        return grid.Regions[leftRegionId].Tiles.Count == breakAt
            && grid.Regions[rightRegionId].Tiles.Count == length - breakAt - 1;
    }
}
