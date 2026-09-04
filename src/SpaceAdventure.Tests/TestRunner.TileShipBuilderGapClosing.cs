using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M85 bugfix follow-up (humble-soaring-cat.md) - TileShipBuilder.CloseGapIfAdjacent's spanMatches
    // check used to require the FULL perpendicular span of both regions to match exactly, which wrongly
    // rejected two touching compartments of DIFFERENT sizes (confirmed via a real diagnostic against
    // Ship.CatalogHulls.cs's Destroyer hull: reactor-a-centered, 7 tall, directly touching cockpit-
    // small, 5 tall). Generalized to a partial-overlap check (Math.Max/Math.Min, mirroring
    // ShipLayoutGeometry.FindRoomPairOverlaps's own overlap-length formula) - these tests cover the
    // exact scenario that broke, confirm the original exact-match case is untouched, and confirm a
    // genuinely non-touching pair still doesn't get a false-positive merge.

    private static (CustomShipDefinition? Definition, IReadOnlyList<string> Errors) BuildTileDefinition(TileGrid tiles) =>
        TileShipBuilder.BuildDefinition(
            tiles,
            new Dictionary<TileCoord, CustomDeviceKind>(),
            new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(),
            "Test Ship",
            forwardDegrees: 0f);

    private static CustomRoomDef? FindRoom(IReadOnlyList<CustomRoomDef> rooms, float x, float y, float w, float h) =>
        rooms.FirstOrDefault(r => r.X == x && r.Y == y && r.Width == w && r.Height == h);

    // ---- The exact scenario that broke: reactor-a-centered (7 tall) stamped touching cockpit-small
    // (5 tall) - the SAME pairing from the real Destroyer hull (Ship.CatalogHulls.cs's own anchors,
    // (5,8) and (10,7), reused here verbatim). Because cockpit is stamped FIRST, its region stays a
    // clean, untouched interior rectangle (X6-8,Y9-11); reactor's own West wall ring only dedups
    // against cockpit's wall for the rows the two footprints actually share (abs Y 8-12), which happens
    // to land exactly on reactor's own interior Y-range here, so reactor's post-dedup region is ALSO a
    // clean rectangle (X10-15,Y8-12) - the two regions are genuinely adjacent with a real, but partial,
    // overlap on their shared boundary (cockpit's Y-span [9,11] sits strictly inside reactor's
    // [8,12]), which is exactly what the old exact-match spanMatches rejected. ----
    private static bool TileShipBuilder_PartialHeightOverlap_ClosesGapAndConnectsWithDoor()
    {
        var tiles = new TileGrid();
        var cockpit = CompartmentCatalog.Find("cockpit-small"); // W=5,H=5
        var reactor = CompartmentCatalog.Find("reactor-a-centered"); // W=7,H=7
        if (cockpit is null || reactor is null)
            return false;

        var cockpitResult = CompartmentPlacer.Stamp(tiles, cockpit, new TileCoord(5, 8), rotationSteps: 0, instanceId: "cockpit");
        var reactorResult = CompartmentPlacer.Stamp(tiles, reactor, new TileCoord(10, 7), rotationSteps: 0, instanceId: "reactor");
        if (!cockpitResult.Success || !reactorResult.Success)
            return false;

        // Cut a door on the shared boundary, within the actual overlapping span (cockpit's own East
        // wall column spans Y=9..11, entirely inside the two rects' real overlap [9,11] - the middle
        // row Y=10 is comfortably inside both).
        var doorCoord = new TileCoord(9, 10);
        if (tiles.CellAt(doorCoord) is not { Wall: TileWallKind.Solid })
            return false; // sanity: this must be a still-solid wall tile before we cut the door
        tiles.SetWall(doorCoord, TileWallKind.Door);

        var (definition, errors) = BuildTileDefinition(tiles);
        if (definition is null || errors.Count > 0)
            return false;
        if (definition.Rooms.Count != 2)
            return false;

        var cockpitRoom = FindRoom(definition.Rooms, x: 6, y: 9, w: 4, h: 3); // extended by 1 (gap closed)
        var reactorRoom = FindRoom(definition.Rooms, x: 10, y: 8, w: 6, h: 5);
        if (cockpitRoom is null || reactorRoom is null)
            return false;

        // Genuinely touching with zero gap now (ShipLayoutGeometry.FindRoomPairOverlaps's own
        // adjacency condition), and connected by exactly one door.
        if (cockpitRoom.X + cockpitRoom.Width != reactorRoom.X)
            return false;
        if (definition.Doors.Count != 1)
            return false;
        var door = definition.Doors[0];
        var doorConnectsBoth =
            (door.RoomAId == cockpitRoom.Id && door.RoomBId == reactorRoom.Id) ||
            (door.RoomAId == reactorRoom.Id && door.RoomBId == cockpitRoom.Id);
        return doorConnectsBoth;
    }

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
}
