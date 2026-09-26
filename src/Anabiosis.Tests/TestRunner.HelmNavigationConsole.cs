using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // Direct user request - this session's CORRECTION of a feature implemented twice before and
    // rejected twice: once as a "wall-mounted panel" (TestRunner.WallConsolePair.cs, deleted), once
    // as a full-2x2-reservation-with-a-purely-cosmetic-smaller-drawn-box (CustomDeviceFootprint.
    // VisualSize + TileGrid.CanPlaceFootprintWithHalfBlockWallLeniency/PlaceDeviceWithHalfBlockWallLeniency,
    // both deleted - the latter destroyed a real half-block wall to make room, direct user rejection).
    // Doubly-confirmed via 2 rounds of clarifying multiple-choice questions: the console must
    // genuinely occupy exactly 1.5 tiles (real collision, not a cosmetic box) even far from any wall,
    // and when it docks against an existing half-block wall, that wall must survive completely
    // unchanged - the console just uses the wall's own already-open half as its own.
    //
    // These tests cover: (a) the anchor-relative footprint still spans 2 whole-tile COORDINATES
    // (CustomDeviceFootprint.Size, unchanged - there's no fractional TileCoord); (b)-(f) TileGrid.
    // IsWalkable's combined truth table (TileCell.DeviceOpenSide alone, and combined with an existing
    // half-block wall's own WallOpenSide), verified both directly and through real TileMovement.
    // MoveAlongAxis calls (not just by inspection); (g)-(l) the new TileGrid.CanPlaceHalfWidthDevice/
    // PlaceHalfWidthDevice placement API, including the exact "wall must survive unchanged" assertion;
    // (m) the full TileShipBuilder -> Ship pipeline round trip for a wall-adjacent placement; (n) the
    // critical backward-compat guarantee for a hull built before any of this existed.

    // (a) - the tile-grid footprint still spans a plain 2x2 whole-tile bounding box (only the SECOND
    // tile along the halved axis is now half-claimed rather than fully reserved - TileGrid.
    // IsWalkable's own combined truth table below is where the real 1.5-tile behavior actually lives).
    private static bool CustomDeviceFootprint_HelmAndNavigation_ReserveTwoByTwoForCollision()
    {
        var (helmWidth, helmHeight) = CustomDeviceFootprint.Size(CustomDeviceKind.Helm);
        var (navWidth, navHeight) = CustomDeviceFootprint.Size(CustomDeviceKind.Navigation);
        return helmWidth == 2 && helmHeight == 2 && navWidth == 2 && navHeight == 2;
    }

    private static TileGrid BuildFourByFourOpenFloor()
    {
        var tiles = new TileGrid();
        for (var x = 0; x < 4; x++)
            for (var y = 0; y < 4; y++)
                tiles.SetFloor(new TileCoord(x, y), true);
        return tiles;
    }

    // (b) - an ordinary device (DeviceOpenSide left null) still blocks its whole tile, completely
    // unchanged - the new field must never affect any of the game's existing furniture.
    private static bool TileGrid_IsWalkable_OrdinaryDevice_BlocksBothHalves()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.PlaceDevice(coord, "bed");
        var cell = tiles.CellAt(coord)!;
        return !TileGrid.IsWalkable(cell, coord, new Vec2(1.25, 1.5)) && !TileGrid.IsWalkable(cell, coord, new Vec2(1.75, 1.5));
    }

    // (c) - a Helm/Navigation "half" tile standing alone on open floor (no wall nearby at all) blocks
    // only its own named half, walkable in the opposite half - "Всегда ровно 1.5, даже вдали от стен".
    private static bool TileGrid_IsWalkable_StandaloneHalfWidthDevice_BlocksOnlyItsOwnHalf()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.PlaceHalfWidthDevice(coord, TileSide.East, "helm-half");
        var cell = tiles.CellAt(coord)!;
        var westWalkable = TileGrid.IsWalkable(cell, coord, new Vec2(1.25, 1.5));
        var eastBlocked = !TileGrid.IsWalkable(cell, coord, new Vec2(1.75, 1.5));
        return westWalkable && eastBlocked;
    }

    // (d) - an ordinary half-block wall tile with no device on it at all is completely unchanged
    // (TestRunner.HalfThickWalls.cs's own real-movement regression already covers this too).
    private static bool TileGrid_IsWalkable_HalfBlockWallAlone_OnlyItsOwnHalfIsBlocked()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid);
        tiles.SetWallOpenSide(coord, TileSide.North);
        var cell = tiles.CellAt(coord)!;
        var northBlocked = !TileGrid.IsWalkable(cell, coord, new Vec2(1.5, 1.25));
        var southWalkable = TileGrid.IsWalkable(cell, coord, new Vec2(1.5, 1.75));
        return northBlocked && southWalkable;
    }

    // (e) - a half-block wall with a genuine wall-mounted device (Terminal/WallLamp, WallDeviceId - a
    // completely different field from the new DeviceId/DeviceOpenSide pairing) still fully blocks its
    // tile, unchanged.
    private static bool TileGrid_IsWalkable_HalfBlockWallWithWallDevice_BlocksEverywhere()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid);
        tiles.SetWallOpenSide(coord, TileSide.North);
        tiles.PlaceRecessedWallDevice(coord, CustomDeviceKind.Terminal, "terminal-1");
        var cell = tiles.CellAt(coord)!;
        return !TileGrid.IsWalkable(cell, coord, new Vec2(1.5, 1.25)) && !TileGrid.IsWalkable(cell, coord, new Vec2(1.5, 1.75));
    }

    // (f) - THE combined case this whole session is about: a Helm/Navigation half tile coexisting
    // with an already-open half-block wall blocks the WHOLE tile - the wall's own half was already
    // blocked, and the device now claims the tile's other, formerly-open half too.
    private static bool TileGrid_IsWalkable_HalfBlockWallCoexistingWithHalfWidthDevice_BlocksWholeTile()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid);
        tiles.SetWallOpenSide(coord, TileSide.North);
        tiles.PlaceHalfWidthDevice(coord, TileSide.North, "helm-half");
        var cell = tiles.CellAt(coord)!;
        return !TileGrid.IsWalkable(cell, coord, new Vec2(1.5, 1.25)) && !TileGrid.IsWalkable(cell, coord, new Vec2(1.5, 1.75));
    }

    // Real-movement regression for (c) above, mirroring TestRunner.HalfThickWalls.cs's own style -
    // not just the position-aware IsWalkable unit test, the actual TileMovement.MoveAlongAxis path
    // every character in the game moves through.
    private static bool TileMovement_MoveAlongAxis_StandaloneHalfWidthDevice_StopsAtRealOneAndAHalfTileBoundary()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        grid.PlaceHalfWidthDevice(new TileCoord(2, 2), TileSide.East, "helm-half");

        // Delta lands the naive endpoint INSIDE the blocked half itself (this movement model checks
        // the candidate endpoint's own clearance, not a continuous sweep - same reason every other
        // MoveAlongAxis test in this codebase, e.g. TestRunner.HalfThickWalls.cs, always aims a delta
        // that overshoots INTO the obstacle rather than clean over it) so the binary search inside
        // MoveAlongAxis actually engages and finds the real boundary.
        var next = TileMovement.MoveAlongAxis(grid, new Vec2(0.5, 2.5), new Vec2(2, 0));
        return Math.Abs(next.X - (2.5 - RoomLayout.CharacterRadius)) < 0.01;
    }

    // Real-movement regression for (f) above - the same half-block wall row TestRunner.
    // HalfThickWalls.cs's own free-half test uses (a character can normally walk into its open South
    // half, stopping only at the tile's own Y=0.5 midpoint), but with a Helm/Navigation half tile now
    // coexisting on that same cell: the whole tile is blocked, so the character stops a full tile
    // earlier instead, at Y=1.0 - exactly TileMovement_MoveAlongAxis_StillBlocksFullyAtACorner's own
    // full-thickness result, just reached via wall+device coexistence instead of a genuine corner.
    private static bool TileMovement_MoveAlongAxis_HalfBlockWallCoexistingWithHalfWidthDevice_BlocksTheFormerlyOpenHalfToo()
    {
        var grid = new TileGrid();
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                grid.SetFloor(new TileCoord(x, y), true);
        grid.SetWall(new TileCoord(2, 0), TileWallKind.Solid);
        grid.SetWallOpenSide(new TileCoord(2, 0), TileSide.North);
        grid.PlaceHalfWidthDevice(new TileCoord(2, 0), TileSide.North, "helm-half");

        var next = TileMovement.MoveAlongAxis(grid, new Vec2(2.5, 3), new Vec2(0, -3));
        return Math.Abs(next.Y - (1f + RoomLayout.CharacterRadius)) < 0.01;
    }

    // (g)/(h) - standalone placement on plain open floor, same ordinary precondition every other
    // Device-tool kind already has.
    private static bool TileGrid_CanPlaceHalfWidthDevice_SucceedsOnOpenFloor()
    {
        var tiles = BuildFourByFourOpenFloor();
        return tiles.CanPlaceHalfWidthDevice(new TileCoord(1, 1), TileSide.East);
    }

    private static bool TileGrid_PlaceHalfWidthDevice_StandaloneOnOpenFloor_SetsDeviceIdAndOpenSide()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.PlaceHalfWidthDevice(coord, TileSide.East, "helm-half");
        return tiles.CellAt(coord) is { DeviceId: "helm-half", DeviceOpenSide: TileSide.East, Wall: TileWallKind.None };
    }

    // (i) - the bonus wall-adjacent case: a tile that's ALREADY a half-block wall on the matching
    // side qualifies too.
    private static bool TileGrid_CanPlaceHalfWidthDevice_SucceedsOnMatchingHalfBlockWall()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid);
        tiles.SetWallOpenSide(coord, TileSide.East);
        return tiles.CanPlaceHalfWidthDevice(coord, TileSide.East);
    }

    // (j) - THE exact bug this session fixes: committing a wall-adjacent placement must leave the
    // wall's own Wall/WallHp/WallOpenSide completely untouched - the old, deleted
    // PlaceDeviceWithHalfBlockWallLeniency used to call SetWall(coord, TileWallKind.None) and destroy
    // it outright (direct user rejection: "стена остаётся, консоль просто использует её открытую
    // половину").
    private static bool TileGrid_PlaceHalfWidthDevice_OnHalfBlockWall_PreservesWallFieldsUnchanged()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid, hp: 77f);
        tiles.SetWallOpenSide(coord, TileSide.East);
        tiles.PlaceHalfWidthDevice(coord, TileSide.East, "helm-half");
        return tiles.CellAt(coord) is
        {
            Wall: TileWallKind.Solid, WallOpenSide: TileSide.East, WallHp: 77f,
            DeviceId: "helm-half", DeviceOpenSide: TileSide.East,
        };
    }

    // A half-block wall whose own open side doesn't match what the device needs never qualifies -
    // the device's own body has to actually land where the wall is already open, not just anywhere
    // half-block.
    private static bool TileGrid_CanPlaceHalfWidthDevice_RejectsMismatchedHalfBlockWallOpenSide()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid);
        tiles.SetWallOpenSide(coord, TileSide.North);
        return !tiles.CanPlaceHalfWidthDevice(coord, TileSide.East);
    }

    // A genuine full-thickness wall tile (Solid, no WallOpenSide at all) never qualifies, even though
    // it's still technically "Solid" - the leniency is specifically for half-block wall material.
    private static bool TileGrid_CanPlaceHalfWidthDevice_RejectsFullThicknessWall()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid);
        return !tiles.CanPlaceHalfWidthDevice(coord, TileSide.East);
    }

    // Direct user bug report (screenshot - placing a Щиток/Junction right next to an existing one
    // was silently refused, on a spot that visually looked like plain open floor) - the tile at
    // (1,1) is genuinely fully claimed here (one neighbor's own "full" tile is at (0,1), its "half"
    // is (1,1)), even though only the WEST portion of (1,1) ever gets that neighbor's own baked icon
    // drawn over it (DrawEditorDeviceAt) - the east portion stays walkable and undrawn, reading as
    // bare floor. CanPlaceHalfWidthDevice's own very first check (DeviceId: null) already refuses a
    // second device trying to claim ANY part of that same tile, regardless of which side it asks
    // for - this pins that down explicitly, since CustomDeviceFootprint.cs's own rulebook doc
    // comment calls this out as the one gap the rest of this file's tests don't cover on their own.
    private static bool TileGrid_CanPlaceHalfWidthDevice_RejectsTileAlreadyClaimedByNeighborsHalf()
    {
        var tiles = BuildFourByFourOpenFloor();
        tiles.PlaceDevice(new TileCoord(0, 1), "junction-a"); // the neighbor's own "full" tile
        tiles.PlaceHalfWidthDevice(new TileCoord(1, 1), TileSide.West, "junction-a"); // its own claimed half

        // A second half-width device trying to use that SAME tile as its own half must be refused,
        // regardless of which side it asks for - the tile already belongs to junction-a.
        return !tiles.CanPlaceHalfWidthDevice(new TileCoord(1, 1), TileSide.East)
            && !tiles.CanPlaceHalfWidthDevice(new TileCoord(1, 1), TileSide.West);
    }

    // (k) - removal clears DeviceOpenSide too, and never disturbs a wall that was coexisting there -
    // the wall goes right back to being an ordinary, fully-usable half-block wall.
    private static bool TileGrid_RemoveDevice_ClearsDeviceOpenSide_LeavesCoexistingWallUntouched()
    {
        var tiles = BuildFourByFourOpenFloor();
        var coord = new TileCoord(1, 1);
        tiles.SetWall(coord, TileWallKind.Solid, hp: 55f);
        tiles.SetWallOpenSide(coord, TileSide.East);
        tiles.PlaceHalfWidthDevice(coord, TileSide.East, "helm-half");
        tiles.RemoveDevice(coord);
        return tiles.CellAt(coord) is
        {
            DeviceId: null, DeviceOpenSide: null,
            Wall: TileWallKind.Solid, WallOpenSide: TileSide.East, WallHp: 55f,
        };
    }

    // (m) - the full TileShipBuilder -> Ship pipeline round trip for a wall-adjacent placement. Reuses
    // the same big-room-with-a-wall-ring-and-airlock-door pattern TestRunner.DeviceRotation.cs's own
    // Ship_FromCustomDefinition_RotatedHelmAndNavigation_KeepTheirOwnRotatedFlag already proved builds
    // successfully end to end. One ring tile becomes a half-block wall a Helm "half" tile coexists
    // with - deliberately at a DIFFERENT coordinate than the Helm anchor placed below (TileShipBuilder.
    // BuildDefinition's own device-export loop only ever reads the ANCHOR tile registered in
    // deviceKinds - see its own doc comment - so this test's real point, kept decoupled from that
    // export mechanics, is only whether a tile carrying BOTH Wall/WallOpenSide AND DeviceId/
    // DeviceOpenSide survives room decomposition + WallOpenSide export/ApplyWallOpenSides +
    // Ship.FromCustomDefinition with its own Wall/WallOpenSide/WallHp completely unchanged.
    private static bool TileShipBuilder_HelmWallAdjacentPlacement_RoundTripsThroughShip_WallSurvivesUnchanged()
    {
        var tiles = new TileGrid();
        for (var x = 0; x < 10; x++)
            for (var y = 0; y < 10; y++)
                tiles.SetFloor(new TileCoord(x, y), true);
        for (var x = -1; x <= 10; x++)
        {
            tiles.SetFloor(new TileCoord(x, -1), true);
            tiles.SetWall(new TileCoord(x, -1), TileWallKind.Solid);
            tiles.SetFloor(new TileCoord(x, 10), true);
            tiles.SetWall(new TileCoord(x, 10), TileWallKind.Solid);
        }
        for (var y = -1; y <= 10; y++)
        {
            tiles.SetFloor(new TileCoord(-1, y), true);
            tiles.SetWall(new TileCoord(-1, y), TileWallKind.Solid);
            tiles.SetFloor(new TileCoord(10, y), true);
            tiles.SetWall(new TileCoord(10, y), TileWallKind.Solid);
        }
        tiles.SetWall(new TileCoord(10, 5), TileWallKind.Door); // airlock, same as DeviceRotation.cs's own pattern

        var wallAdjacentHalf = new TileCoord(-1, 2);
        tiles.SetWallOpenSide(wallAdjacentHalf, TileSide.East);
        tiles.PlaceHalfWidthDevice(wallAdjacentHalf, TileSide.East, "helm-half");

        var helmAnchor = new TileCoord(0, 0);
        var navAnchor = new TileCoord(2, 0);
        tiles.PlaceDevice(helmAnchor, "helm");
        tiles.PlaceDevice(navAnchor, "nav");
        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind>
        {
            [helmAnchor] = CustomDeviceKind.Helm,
            [navAnchor] = CustomDeviceKind.Navigation,
            [new TileCoord(4, 0)] = CustomDeviceKind.Reactor,
            [new TileCoord(4, 4)] = CustomDeviceKind.Distribution,
            [new TileCoord(5, 4)] = CustomDeviceKind.Engine,
            [new TileCoord(6, 4)] = CustomDeviceKind.Oxygen,
            [new TileCoord(7, 4)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(8, 4)] = CustomDeviceKind.StorageRack,
        };
        foreach (var (anchor, kind) in deviceKinds)
        {
            if (anchor == helmAnchor || anchor == navAnchor)
                continue; // already placed above with a fixed id
            tiles.PlaceDevice(anchor, $"device-{anchor.X}-{anchor.Y}");
        }

        var (definition, errors) = TileShipBuilder.BuildDefinition(
            tiles, deviceKinds, new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(), "Тест", 0f);
        if (definition is null || errors.Count > 0)
            return false;

        var ship = Ship.FromCustomDefinition(definition);
        return ship.Tiles.CellAt(wallAdjacentHalf) is { Wall: TileWallKind.Solid, WallOpenSide: TileSide.East } wallCell && wallCell.WallHp > 0;
    }

    // (n) - CRITICAL backward-compat guarantee: ShipDefaultHull.Definition is a literal, frozen
    // old-style CustomShipDefinition, authored long before this whole feature (or its two rejected
    // predecessors) existed, carrying no half-tile orientation data at all. It must still build and
    // keep its exact hand-authored Helm/Navigation positions - automatic here, since no new field
    // exists anywhere in CustomDeviceDef/HelmConsole/NavigationConsole for old data to be missing FROM
    // (this session deliberately derives the half tile's position/side from the device's own already-
    // exported X/Y/Rotated instead of persisting anything new - TileGrid.PlaceHalfWidthDevice's own
    // doc comment explains why that's sufficient) - verified explicitly anyway per this task's own
    // instructions, since it's the one guarantee that must never break.
    private static bool Ship_FromCustomDefinition_OldStyleHelmNavigation_StillBuildsFromFrozenHull()
    {
        var ship = Ship.FromCustomDefinition(ShipDefaultHull.Definition);
        // Exact literal positions from the frozen hull blob (Ship.DefaultHull.cs):
        // {"Kind":"Helm","X":1.4,"Y":1.3} / {"Kind":"Navigation","X":2.8,"Y":1.3}
        if (MathF.Abs(ship.HelmConsole.X - 1.4f) > 0.01f || MathF.Abs(ship.HelmConsole.Y - 1.3f) > 0.01f)
            return false;
        if (MathF.Abs(ship.NavigationConsole.X - 2.8f) > 0.01f || MathF.Abs(ship.NavigationConsole.Y - 1.3f) > 0.01f)
            return false;
        if (ship.HelmConsole.Rotated || ship.NavigationConsole.Rotated)
            return false;

        // Round-tripping through ToDefinition() must keep behaving identically - no field exists any
        // more for a stray half-tile-orientation flag to sneak back in through.
        var roundTripped = ship.ToDefinition();
        var helm = roundTripped.Devices.Single(d => d.Kind == CustomDeviceKind.Helm);
        var nav = roundTripped.Devices.Single(d => d.Kind == CustomDeviceKind.Navigation);
        return MathF.Abs(helm.X - 1.4f) < 0.01f && MathF.Abs(helm.Y - 1.3f) < 0.01f
            && MathF.Abs(nav.X - 2.8f) < 0.01f && MathF.Abs(nav.Y - 1.3f) < 0.01f;
    }
}
