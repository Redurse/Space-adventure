using Anabiosis.Shared.Model;

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
        var overlap = ShipLayoutGeometry.FindOverlapAt(definition.Rooms, door.X, door.Y, door.Vertical);
        return overlap is { } o &&
            ((o.RoomAId == roomA.Id && o.RoomBId == roomB.Id) || (o.RoomAId == roomB.Id && o.RoomBId == roomA.Id));
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
        // Room A's own genuinely-exterior wall column at x=3 is now absorbed into its rect
        // (3x3 -> 4x3) per the room-rect/wall-ring convention fix - it has nothing beyond it, so
        // this is the same kind of hand-authored-hull wall ring every other room already gets.
        // Room B has no painted wall of its own anywhere around it, so it is left untouched.
        var roomA = FindRoom(definition.Rooms, x: 0, y: 0, w: 4, h: 3);
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
        var overlap = ShipLayoutGeometry.FindOverlapAt(definition.Rooms, door.X, door.Y, door.Vertical);
        return overlap is { } o && (o.RoomAId == lRoom.Id || o.RoomBId == lRoom.Id);
    }

    // Direct user request ("это в будущем будет одно из главных устройств, их будет много") -
    // closes the gap this session found live: TileCell.TerminalId used to never reach a real Ship
    // at all (editor-only, dropped silently on export). A 3x3 room whose own North row is wall
    // (row 0), with a recessed terminal on its one non-corner tile (1,0) - BuildDefinition must
    // export it as a real CustomDeviceDef(Terminal), positioned at the wall tile's own center (same
    // "tile-center" convention every 1x1 device already uses), not silently dropped.
    private static TileGrid BuildThreeByThreeRoomWithNorthWall()
    {
        var tiles = new TileGrid();
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
                tiles.SetFloor(new TileCoord(x, y), true);
        for (var x = 0; x < 3; x++)
            tiles.SetWall(new TileCoord(x, 0), TileWallKind.Solid);
        tiles.SetWallOpenSide(new TileCoord(1, 0), TileSide.North);
        return tiles;
    }

    private static bool TileShipBuilder_RecessedTerminal_ExportsAsTerminalDeviceAtTileCenter()
    {
        var tiles = BuildThreeByThreeRoomWithNorthWall();
        tiles.PlaceRecessedWallDevice(new TileCoord(1, 0), CustomDeviceKind.Terminal, "terminal-1");

        var (definition, errors) = BuildTileDefinition(tiles);
        if (definition is null || errors.Count > 0)
            return false;
        var terminal = definition.Devices.FirstOrDefault(d => d.Kind == CustomDeviceKind.Terminal);
        return terminal is not null && Math.Abs(terminal.X - 1.5f) < 0.01f && Math.Abs(terminal.Y - 0.5f) < 0.01f
            && terminal.WallDeviceFacingSide == TileSide.South; // opposite of the wall's own North open side
    }

    // No per-ship limit any more (Ship.Custom.cs now builds a List<Terminal>, same shape
    // AmmoStorage/SuitLocker already have) - two terminals (one recessed, one floor-adjacent) both
    // export as their own independent CustomDeviceDef(Terminal).
    private static bool TileShipBuilder_MultipleTerminals_EachExportsAsItsOwnDevice()
    {
        var tiles = BuildThreeByThreeRoomWithNorthWall();
        tiles.PlaceRecessedWallDevice(new TileCoord(1, 0), CustomDeviceKind.Terminal, "terminal-1");
        // (0,0) is a genuine corner (full-thickness, WallOpenSide null) - (1,0) is half-thick now and
        // recess-only (direct user report, "стены в пол блока... это можно было сделать только в
        // том же тайле что и стена"), so the floor-adjacent mode needs a DIFFERENT wall neighbor.
        tiles.PlaceWallDevice(new TileCoord(0, 1), TileSide.North, CustomDeviceKind.Terminal, "terminal-2"); // floor-adjacent mode

        var (definition, errors) = BuildTileDefinition(tiles);
        if (definition is null || errors.Count > 0)
            return false;
        return definition.Devices.Count(d => d.Kind == CustomDeviceKind.Terminal) == 2;
    }

    // Direct user request ("на стену размером с полублок можно крепить только терминал и настенную
    // лампу... сделай лампу тоже") - WallLamp exports through the exact same path as Terminal, just
    // its own CustomDeviceKind, and a Terminal/WallLamp placed side by side don't interfere with
    // each other's own count.
    private static bool TileShipBuilder_WallLamp_ExportsAsWallLampDeviceAlongsideTerminal()
    {
        var tiles = BuildThreeByThreeRoomWithNorthWall();
        tiles.PlaceRecessedWallDevice(new TileCoord(1, 0), CustomDeviceKind.WallLamp, "lamp-1");
        tiles.PlaceWallDevice(new TileCoord(0, 1), TileSide.North, CustomDeviceKind.Terminal, "terminal-1"); // (0,0) is a full-thickness corner

        var (definition, errors) = BuildTileDefinition(tiles);
        if (definition is null || errors.Count > 0)
            return false;
        var lamp = definition.Devices.FirstOrDefault(d => d.Kind == CustomDeviceKind.WallLamp);
        return lamp is not null && Math.Abs(lamp.X - 1.5f) < 0.01f && Math.Abs(lamp.Y - 0.5f) < 0.01f
            && definition.Devices.Count(d => d.Kind == CustomDeviceKind.Terminal) == 1;
    }
}
