using SpaceAdventure.Shared.Model;

// M77 (humble-soaring-cat.md) - TileRegionConnectivity is a pure Shared-module utility, tested
// directly against a small hand-built TileGrid the same way TestRunner.TileGrid.cs already tests
// TileGrid itself, and TestRunner.ShipBuilding.cs's own RoomGraphConnectivity_
// DetectsConnectedAndDisconnectedGraphs tests the old DTO-based equivalent.
internal static partial class TestRunner
{
    // Two 3x3 rooms (x in [0,3) and x in [4,7), both y in [0,3)) bridged by a single door tile at
    // (3,1) - the exact "two rooms joined by one door tile" shape the M77 spec calls for. Floor is
    // placed everywhere BEFORE the door tile's own wall is set, the same order TileGridRasterizer
    // itself always uses (floor first, walls/doors after) - see TileGrid_
    // OpenDoorNeverMergesRegionsButIsWalkable in TestRunner.TileGrid.cs for the same recipe.
    private static TileGrid BuildTwoRoomsJoinedByOneDoor(out int roomARegionId, out int roomBRegionId)
    {
        var grid = new TileGrid();
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        for (var x = 4; x < 7; x++)
            for (var y = 0; y < 3; y++)
                grid.SetFloor(new TileCoord(x, y), true);

        var door = new TileCoord(3, 1);
        grid.SetFloor(door, true); // touches both rooms - briefly merges them into one region
        grid.SetWall(door, TileWallKind.Door); // now blocking again - splits back into exactly two

        roomARegionId = grid.RegionIdAt(new TileCoord(0, 0))!.Value;
        roomBRegionId = grid.RegionIdAt(new TileCoord(4, 0))!.Value;
        return grid;
    }

    private static bool TileRegionConnectivity_DoorConnectsTwoRegions_BothReachableFromEachOther()
    {
        var grid = BuildTwoRoomsJoinedByOneDoor(out var roomA, out var roomB);
        if (grid.Regions.Count != 2 || roomA == roomB)
            return false; // setup problem - the door didn't actually keep them as two regions

        var fromA = TileRegionConnectivity.ReachableRegionsFrom(grid, roomA);
        var fromB = TileRegionConnectivity.ReachableRegionsFrom(grid, roomB);
        return fromA.SetEquals(new[] { roomA, roomB }) && fromB.SetEquals(new[] { roomA, roomB });
    }

    // Simulates one room being fully destroyed (SetFloor(false) for every one of its own tiles - the
    // exact recipe World.ShipDebris.cs's DestroyRoomAndDetach uses on its own TileGrid.Clone()) and
    // confirms the surviving region is completely unaffected: still there, still reachable from
    // itself, and no longer reachable to (or even aware of) the now-gone region id.
    private static bool TileRegionConnectivity_DestroyingOneRoomsFloor_LeavesTheOtherRegionAloneAndUnreachable()
    {
        var grid = BuildTwoRoomsJoinedByOneDoor(out var roomA, out var roomB);

        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
                grid.SetFloor(new TileCoord(x, y), false);

        if (grid.RegionIdAt(new TileCoord(0, 0)) is not null)
            return false; // setup problem - room A's tiles didn't actually clear
        if (grid.Regions.ContainsKey(roomA))
            return false; // the old region itself must be gone too, not just its tiles

        var stillThere = grid.RegionIdAt(new TileCoord(4, 0)) == roomB;
        var reachableFromB = TileRegionConnectivity.ReachableRegionsFrom(grid, roomB);
        return stillThere && reachableFromB.SetEquals(new[] { roomB });
    }
}
