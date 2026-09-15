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

    // Direct user request ("в таком случае будет полностью заполнен тайл") - a floor-adjacent wall
    // device now fully occupies its own tile once placed (unlike the old always-walkable mode), but
    // still never affects region topology (only the floor/wall LAYER does that).
    private static bool TileGrid_WallDeviceRequiresAdjacentWallAndFullyBlocksItsOwnTile()
    {
        var grid = new TileGrid();
        var floor = new TileCoord(0, 0);
        grid.SetFloor(floor, true);

        var threwWithoutWall = false;
        try
        {
            grid.PlaceWallDevice(floor, TileSide.East, CustomDeviceKind.Terminal, "terminal-1");
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

        grid.PlaceWallDevice(floor, TileSide.East, CustomDeviceKind.Terminal, "terminal-1");

        var regionAfter = grid.RegionIdAt(floor);
        var walkableAfter = TileGrid.IsWalkable(grid.CellAt(floor)!);
        return regionBefore == regionAfter && walkableBefore && !walkableAfter;
    }

    // Direct user report ("стены в пол блока являются стенами и на них якобы можно крепить лампу.
    // для таких стен это должно быть невозможно и это можно было сделать только в том же тайле что
    // и стена") - a half-thick wall neighbor must refuse PlaceWallDevice (recess into it instead,
    // via PlaceRecessedWallDevice), even though it's still a genuine `Wall != None` neighbor.
    private static bool TileGrid_WallDevice_RefusesProtrudingOntoAHalfThickWallNeighbor()
    {
        var grid = new TileGrid();
        var floor = new TileCoord(0, 0);
        var wallCoord = new TileCoord(1, 0);
        grid.SetFloor(floor, true);
        grid.SetFloor(wallCoord, true);
        grid.SetWall(wallCoord, TileWallKind.Solid);
        grid.SetWallOpenSide(wallCoord, TileSide.East); // a half-thick wall, recess-only

        try
        {
            grid.PlaceWallDevice(floor, TileSide.East, CustomDeviceKind.Terminal, "terminal-1");
            return false;
        }
        catch (InvalidOperationException) { /* expected */ }

        // The SAME wall tile still accepts a recessed device just fine.
        grid.PlaceRecessedWallDevice(wallCoord, CustomDeviceKind.Terminal, "terminal-1");
        return grid.CellAt(wallCoord)!.WallDeviceId == "terminal-1";
    }

    // Direct user request ("на стену размером с полублок можно крепить только терминал и настенную
    // лампу") - the restriction is enforced at the TileGrid API level itself, for either mode.
    private static bool TileGrid_WallDevice_RejectsKindsOtherThanTerminalOrWallLamp()
    {
        var grid = new TileGrid();
        var floor = new TileCoord(0, 0);
        var wallCoord = new TileCoord(1, 0);
        grid.SetFloor(floor, true);
        grid.SetFloor(wallCoord, true);
        grid.SetWall(wallCoord, TileWallKind.Solid);

        try
        {
            grid.PlaceWallDevice(floor, TileSide.East, CustomDeviceKind.Reactor, "reactor-1");
            return false;
        }
        catch (InvalidOperationException) { /* expected */ }

        grid.SetWallOpenSide(wallCoord, TileSide.East);
        try
        {
            grid.PlaceRecessedWallDevice(wallCoord, CustomDeviceKind.Reactor, "reactor-1");
            return false;
        }
        catch (InvalidOperationException) { return true; }
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

    // Direct user request ("не угловые клетки занимали только половину блока которая была ближе к
    // космосу") - the position-aware IsWalkable overload, checked directly against a hand-built
    // half-thick wall tile for every one of the 4 possible open sides. The solid half sits on
    // WallOpenSide itself; the opposite half is free.
    private static bool TileGrid_HalfThickWall_SolidHalfBlockedFreeHalfWalkable()
    {
        foreach (var openSide in TileSideExtensions.All)
        {
            var grid = new TileGrid();
            var coord = new TileCoord(5, 5);
            grid.SetFloor(coord, true);
            grid.SetWall(coord, TileWallKind.Solid);
            grid.SetWallOpenSide(coord, openSide);
            var cell = grid.CellAt(coord)!;

            // Sample the exact center of each half - unambiguously inside one side or the other,
            // regardless of which axis openSide is on.
            var (solidX, solidY) = openSide switch
            {
                TileSide.North => (5.5, 5.25),
                TileSide.South => (5.5, 5.75),
                TileSide.West => (5.25, 5.5),
                TileSide.East => (5.75, 5.5),
                _ => throw new ArgumentOutOfRangeException(),
            };
            var (freeX, freeY) = openSide switch
            {
                TileSide.North => (5.5, 5.75),
                TileSide.South => (5.5, 5.25),
                TileSide.West => (5.75, 5.5),
                TileSide.East => (5.25, 5.5),
                _ => throw new ArgumentOutOfRangeException(),
            };

            if (TileGrid.IsWalkable(cell, coord, new Vec2(solidX, solidY)))
                return false; // the solid half must block
            if (!TileGrid.IsWalkable(cell, coord, new Vec2(freeX, freeY)))
                return false; // the free half must not
        }
        return true;
    }

    // A genuine corner (WallOpenSide == null) stays fully blocked everywhere in the tile - a
    // regression guard that corners are untouched by the half-thickness feature.
    private static bool TileGrid_CornerWall_StaysFullyBlockedAtEveryPosition()
    {
        var grid = new TileGrid();
        var coord = new TileCoord(5, 5);
        grid.SetFloor(coord, true);
        grid.SetWall(coord, TileWallKind.Solid); // WallOpenSide left null - never set
        var cell = grid.CellAt(coord)!;

        return !TileGrid.IsWalkable(cell, coord, new Vec2(5.25, 5.25))
            && !TileGrid.IsWalkable(cell, coord, new Vec2(5.75, 5.75))
            && !TileGrid.IsWalkable(cell, coord, new Vec2(5.5, 5.5));
    }

    // Direct user request ("на пустой стороне можно поставить терминал и он будет занимать весь
    // полублок") - PlaceRecessedTerminal refuses a corner (no WallOpenSide), a door, and plain open
    // floor, but succeeds on a genuine half-thick wall tile - and once placed, blocks BOTH halves,
    // unlike the old floor-adjacent PlaceTerminal which never blocks anything.
    private static bool TileGrid_PlaceRecessedTerminal_RequiresNonCornerWallAndThenBlocksWholeTile()
    {
        var grid = new TileGrid();
        var corner = new TileCoord(0, 0);
        var door = new TileCoord(1, 0);
        var straight = new TileCoord(2, 0);
        var floor = new TileCoord(3, 0);
        grid.SetFloor(corner, true);
        grid.SetFloor(door, true);
        grid.SetFloor(straight, true);
        grid.SetFloor(floor, true);
        grid.SetWall(corner, TileWallKind.Solid); // WallOpenSide left null - stands in for a corner
        grid.SetWall(door, TileWallKind.Door);
        grid.SetWall(straight, TileWallKind.Solid);
        grid.SetWallOpenSide(straight, TileSide.North);

        bool Throws(TileCoord coord)
        {
            try
            {
                grid.PlaceRecessedWallDevice(coord, CustomDeviceKind.Terminal, "terminal-x");
                return false;
            }
            catch (InvalidOperationException) { return true; }
        }

        if (!Throws(corner) || !Throws(door) || !Throws(floor))
            return false;

        grid.PlaceRecessedWallDevice(straight, CustomDeviceKind.Terminal, "terminal-1");
        var cell = grid.CellAt(straight)!;
        if (cell.WallDeviceId != "terminal-1" || !cell.WallDeviceRecessed)
            return false;

        // Both halves now blocked, not just the wall's own solid North half.
        return !TileGrid.IsWalkable(cell, straight, new Vec2(2.5, 2.25))
            && !TileGrid.IsWalkable(cell, straight, new Vec2(2.5, 2.75));
    }
}
