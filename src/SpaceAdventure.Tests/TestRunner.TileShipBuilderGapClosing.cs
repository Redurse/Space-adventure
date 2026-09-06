using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M85 bugfix follow-up (humble-soaring-cat.md) - TileShipBuilder.CloseGapIfAdjacent's spanMatches
    // check used to require the FULL perpendicular span of both regions to match exactly, which wrongly
    // rejected two touching compartments of DIFFERENT sizes (confirmed via a real diagnostic against
    // Ship.CatalogHulls.cs's Destroyer hull: reactor-a-centered, 7 tall, directly touching cockpit-
    // small, 5 tall). Generalized to a partial-overlap check (Math.Max/Math.Min, mirroring
    // ShipLayoutGeometry.FindRoomPairOverlaps's own overlap-length formula). The test that reproduced
    // the original break via those two named catalog entries was removed along with
    // CompartmentCatalog.Entries itself (direct user request); these two remain - confirming the
    // original exact-match case is untouched, and that a genuinely non-touching pair still doesn't
    // get a false-positive merge.

    private static (CustomShipDefinition? Definition, IReadOnlyList<string> Errors) BuildTileDefinition(TileGrid tiles) =>
        TileShipBuilder.BuildDefinition(
            tiles,
            new Dictionary<TileCoord, CustomDeviceKind>(),
            new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(),
            "Test Ship",
            forwardDegrees: 0f);

    private static CustomRoomDef? FindRoom(IReadOnlyList<CustomRoomDef> rooms, float x, float y, float w, float h) =>
        rooms.FirstOrDefault(r => r.X == x && r.Y == y && r.Width == w && r.Height == h);

    // ---- Regression: the ORIGINAL exact-match case (two same-height regions, full span matching
    // exactly) must still close and connect exactly as before. Built directly on the TileGrid (not via
    // CompartmentPlacer) - this is the free-tile Ship Editor's own original use case the exact-match
    // check was designed for: two independently-painted floor rectangles of the SAME height, separated
    // by a straight 1-tile wall/door column the whole way across. ----
    private static bool TileShipBuilder_ExactSpanMatch_StillClosesGapAndConnectsWithDoor()
    {
        var tiles = new TileGrid();
        for (var y = 0; y < 3; y++)
        {
            tiles.SetFloor(new TileCoord(0, y), true);
            tiles.SetFloor(new TileCoord(1, y), true);
            tiles.SetFloor(new TileCoord(2, y), true);
            tiles.SetFloor(new TileCoord(4, y), true);
            tiles.SetFloor(new TileCoord(5, y), true);
            tiles.SetFloor(new TileCoord(6, y), true);
        }
        // The 1-tile wall/door separator column at X=3, full height (Y=0..2) - a door in the middle.
        tiles.SetFloor(new TileCoord(3, 0), true);
        tiles.SetFloor(new TileCoord(3, 1), true);
        tiles.SetFloor(new TileCoord(3, 2), true);
        tiles.SetWall(new TileCoord(3, 0), TileWallKind.Solid);
        tiles.SetWall(new TileCoord(3, 1), TileWallKind.Door);
        tiles.SetWall(new TileCoord(3, 2), TileWallKind.Solid);

        var (definition, errors) = BuildTileDefinition(tiles);
        if (definition is null || errors.Count > 0)
            return false;
        if (definition.Rooms.Count != 2)
            return false;

        var roomA = FindRoom(definition.Rooms, x: 0, y: 0, w: 4, h: 3); // extended east by 1
        var roomB = FindRoom(definition.Rooms, x: 4, y: 0, w: 3, h: 3);
        if (roomA is null || roomB is null)
            return false;
        if (roomA.X + roomA.Width != roomB.X)
            return false;
        if (definition.Doors.Count != 1)
            return false;
        var door = definition.Doors[0];
        return (door.RoomAId == roomA.Id && door.RoomBId == roomB.Id) ||
               (door.RoomAId == roomB.Id && door.RoomBId == roomA.Id);
    }

    // ---- Negative case: two regions that genuinely don't touch at all (far apart, well beyond any
    // wall/door separator) must be left completely alone - no false-positive merge, no door. ----
    private static bool TileShipBuilder_NoRealOverlap_LeavesRegionsUntouched()
    {
        var tiles = new TileGrid();
        for (var y = 0; y < 3; y++)
        {
            tiles.SetFloor(new TileCoord(0, y), true);
            tiles.SetFloor(new TileCoord(1, y), true);
            tiles.SetFloor(new TileCoord(2, y), true);
        }
        tiles.SetFloor(new TileCoord(3, 0), true);
        tiles.SetFloor(new TileCoord(3, 1), true);
        tiles.SetFloor(new TileCoord(3, 2), true);
        tiles.SetWall(new TileCoord(3, 0), TileWallKind.Solid);
        tiles.SetWall(new TileCoord(3, 1), TileWallKind.Solid);
        tiles.SetWall(new TileCoord(3, 2), TileWallKind.Solid);

        // A second region far away - not 2 tiles east of the first, and not aligned with it at all.
        for (var y = 10; y < 13; y++)
        {
            tiles.SetFloor(new TileCoord(20, y), true);
            tiles.SetFloor(new TileCoord(21, y), true);
        }

        var (definition, errors) = BuildTileDefinition(tiles);
        if (definition is null || errors.Count > 0)
            return false;
        if (definition.Rooms.Count != 2)
            return false;

        // Both regions kept their own original, un-merged rectangles, and no door was invented.
        var roomA = FindRoom(definition.Rooms, x: 0, y: 0, w: 3, h: 3);
        var roomB = FindRoom(definition.Rooms, x: 20, y: 10, w: 2, h: 3);
        return roomA is not null && roomB is not null && definition.Doors.Count == 0;
    }

    // ---- M88 (humble-soaring-cat.md, non-rectangular compartments) - a genuinely L-shaped region
    // (a 4x2 arm plus a 2x2 arm below its left half - a step, not a rectangle) must decompose into
    // a multi-rect CustomRoomDef instead of being rejected, AND still gap-close/door-connect
    // correctly against a separate rectangular neighbour touching its east side. ----
    private static bool TileShipBuilder_LShapedRegion_DecomposesAndConnectsWithDoor()
    {
        var tiles = new TileGrid();
        for (var y = 0; y < 2; y++)
            for (var x = 0; x < 4; x++)
                tiles.SetFloor(new TileCoord(x, y), true);
        for (var y = 2; y < 4; y++)
            for (var x = 0; x < 2; x++)
                tiles.SetFloor(new TileCoord(x, y), true);

        // A separate rectangular region east of the L's top arm, joined by a 1-tile wall/door column.
        for (var y = 0; y < 2; y++)
        {
            tiles.SetFloor(new TileCoord(4, y), true);
            tiles.SetFloor(new TileCoord(5, y), true);
        }
        tiles.SetWall(new TileCoord(4, 0), TileWallKind.Solid);
        tiles.SetWall(new TileCoord(4, 1), TileWallKind.Door);

        var (definition, errors) = BuildTileDefinition(tiles);
        if (definition is null || errors.Count > 0)
            return false;
        if (definition.Rooms.Count != 2)
            return false;

        var lRoom = definition.Rooms.FirstOrDefault(r => r.Rects.Count > 1);
        if (lRoom is null)
            return false;

        if (definition.Doors.Count != 1)
            return false;
        var door = definition.Doors[0];
        return door.RoomAId == lRoom.Id || door.RoomBId == lRoom.Id;
    }
}
