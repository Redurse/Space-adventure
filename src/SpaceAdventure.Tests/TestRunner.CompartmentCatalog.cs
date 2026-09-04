using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M80 (humble-soaring-cat.md) - pure data/algorithm tests against a plain TileGrid, exactly like
    // TestRunner.TileGrid.cs's own tests. Nothing here touches Ship/World/the Client - CompartmentPlacer
    // isn't wired into the Ship Editor yet (that's M81+).

    // ---- Rotation transform correctness ----
    // "reactor-b-wide" (W=8,H=5, Reactor at local (2,1)) is asymmetric on both axes, so a wrong
    // rotation formula would produce a device position distinguishable from the correct one at every
    // step. Hand-computed ground truth (RotatePointClockwise(p, h) = (h-1-p.Y, p.X), dims swap each
    // step) - recomputed after the entry grew from 6x4 to 8x5 (direct user request, "увеличь размер
    // реакторного отсека"), same relative off-center-on-both-axes placement convention as before:
    //   0 steps: dims (8,5), pos (2,1)            [authored]
    //   1 step : dims (5,8), pos (5-1-1,2)=(3,2)
    //   2 steps: dims (8,5), pos (8-1-2,3)=(5,3)   [matches the direct 180 formula (W-1-x,H-1-y)=(5,3)]
    //   3 steps: dims (5,8), pos (5-1-3,5)=(1,5)
    // A 4th step (not asserted below, but checked here as a consistency guard) returns to (2,1)/(8,5).
    private static bool CompartmentCatalog_RotationTransform_MatchesHandComputedCoordinates()
    {
        var entry = CompartmentCatalog.Find("reactor-b-wide");
        if (entry is null || entry.Devices.Count != 1)
            return false;

        var r0 = CompartmentPlacer.Rotate(entry, 0);
        var r1 = CompartmentPlacer.Rotate(entry, 1);
        var r2 = CompartmentPlacer.Rotate(entry, 2);
        var r3 = CompartmentPlacer.Rotate(entry, 3);
        var r4 = CompartmentPlacer.Rotate(entry, 4); // full circle - must match r0 exactly

        bool DeviceAt(CompartmentPlacer.RotatedCompartment r, int width, int height, TileCoord expected) =>
            r.Width == width && r.Height == height && r.Devices.Count == 1 && r.Devices[0].Position == expected;

        return DeviceAt(r0, 8, 5, new TileCoord(2, 1))
            && DeviceAt(r1, 5, 8, new TileCoord(3, 2))
            && DeviceAt(r2, 8, 5, new TileCoord(5, 3))
            && DeviceAt(r3, 5, 8, new TileCoord(1, 5))
            && DeviceAt(r4, 8, 5, new TileCoord(2, 1));
    }

    // ---- Engine tier geometry: non-overlapping footprints, Bulkhead on the ring, Nozzle outside ----
    private static TileCoord StepLocal(TileCoord origin, TileSide side, int steps) => side switch
    {
        TileSide.North => origin with { Y = origin.Y - steps },
        TileSide.South => origin with { Y = origin.Y + steps },
        TileSide.East => origin with { X = origin.X + steps },
        TileSide.West => origin with { X = origin.X - steps },
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static bool ValidateEngineCompartmentLayout(string catalogId)
    {
        var entry = CompartmentCatalog.Find(catalogId);
        if (entry is null || entry.Engines.Count == 0)
            return false;

        bool Inside(TileCoord p) => p.X >= 0 && p.X < entry.Width && p.Y >= 0 && p.Y < entry.Height;
        bool OnRing(TileCoord p) => p.X == 0 || p.X == entry.Width - 1 || p.Y == 0 || p.Y == entry.Height - 1;

        var footprints = new List<HashSet<TileCoord>>();
        foreach (var engine in entry.Engines)
        {
            var control = engine.RelativeControl;
            var bulkhead = StepLocal(control, engine.Facing, 1);
            var nozzle = StepLocal(control, engine.Facing, 2);

            if (!Inside(control) || OnRing(control))
                return false; // Control must be ordinary interior floor, not on the ring itself
            if (!Inside(bulkhead) || !OnRing(bulkhead))
                return false; // Bulkhead must land exactly on the compartment's own wall ring
            if (Inside(nozzle))
                return false; // Nozzle must land genuinely outside the footprint

            footprints.Add(new HashSet<TileCoord> { control, bulkhead, nozzle });
        }

        for (var i = 0; i < footprints.Count; i++)
            for (var j = i + 1; j < footprints.Count; j++)
                if (footprints[i].Overlaps(footprints[j]))
                    return false; // two engines' 3-tile lines must never share a tile

        return true;
    }

    private static bool CompartmentCatalog_EngineSmall1Way_HasNonOverlappingValidLayout() =>
        ValidateEngineCompartmentLayout("engine-small-1way");

    private static bool CompartmentCatalog_EngineSmall2Way_HasNonOverlappingValidLayout() =>
        ValidateEngineCompartmentLayout("engine-small-2way");

    private static bool CompartmentCatalog_EngineSmall3Way_HasNonOverlappingValidLayout() =>
        ValidateEngineCompartmentLayout("engine-small-3way");

    private static bool CompartmentCatalog_EngineMedium_HasNonOverlappingValidLayout() =>
        ValidateEngineCompartmentLayout("engine-medium");

    private static bool CompartmentCatalog_EngineLarge_HasNonOverlappingValidLayout() =>
        ValidateEngineCompartmentLayout("engine-large");

    // ---- The core wall-dedup claim: two compartments stamped directly touching (zero gap) must end
    // up with exactly ONE wall tile between their two floor regions, not zero and not two. ----
    private static bool CompartmentCatalog_TouchingCompartments_HaveExactlyOneWallTileBetweenThem()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4
        if (entry is null)
            return false;

        var resultA = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a");
        var resultB = CompartmentPlacer.Stamp(grid, entry, new TileCoord(4, 0), rotationSteps: 0, instanceId: "b"); // A's footprint ends at x=3, B starts at x=4: zero gap
        if (!resultA.Success || !resultB.Success)
            return false;

        // Row y=1 crosses straight from A's interior (x=1,2) through A's own right wall (x=3, still
        // standing - only the NEW compartment's own tile is ever allowed to change) through what
        // would have been B's own left wall ring (x=4) - deduped away to plain floor - into B's
        // interior (x=5,6).
        var wallTileCount = 0;
        for (var x = 1; x <= 6; x++)
            if (grid.CellAt(new TileCoord(x, 1)) is { Wall: not TileWallKind.None })
                wallTileCount++;
        if (wallTileCount != 1)
            return false;

        var regionA = grid.RegionIdAt(new TileCoord(2, 1));
        var regionB = grid.RegionIdAt(new TileCoord(4, 1));
        if (regionA is null || regionB is null || regionA == regionB)
            return false; // the single remaining wall tile (x=3) must still keep them as two separate regions

        // A keeps its own untouched 2x2 interior (4 tiles) - its ring was never a "new" stamp once B
        // arrived, so nothing about it changes. B's dedup fires for the two NON-corner rows along the
        // touching boundary (y=1,2) - each of those tiles has only ONE ring side (West), and that side
        // touches A's already-solid east wall, so it's unambiguously the shared interior boundary.
        // B's own two CORNER tiles on that same column (y=0,3 - see the dedicated corner-vs-hole test
        // below for the full story) stay Solid instead: each has a SECOND ring side (North/South) that
        // is genuine open space (this row is the very top/bottom of the whole combined shape), and a
        // single TileCell.Wall value can't be "open toward A, solid toward vacuum" at once - keeping it
        // Solid is what correctly seals that side, at the cost of a harmless, redundant extra wall tile
        // sitting right next to A's own corner. So B's region is "west column's 2 non-corner tiles (2)
        // + interior (4)" = 6, not 8 - and, unlike the old (buggy) 8-tile shape, this one is a genuine
        // rectangle (3 wide x 2 tall), which is exactly what TileShipBuilder.BuildDefinition's own
        // "region must be rectangular" check requires.
        return grid.Regions[regionA.Value].Tiles.Count == 4 && grid.Regions[regionB.Value].Tiles.Count == 6;
    }

    // ---- The confirmed real bug (see CompartmentPlacer.Stamp's own step-4 doc comment): stamping two
    // SAME-SIZE compartments directly touching (zero gap, full-height match) used to leave the second
    // one's own corner tile(s) on the shared boundary floored with Wall.None - a literal hole in that
    // corner's genuine exterior side, and a non-rectangular SealedRegion (TileShipBuilder.BuildDefinition
    // would reject it as "must be rectangular"). Reproduced with engine-medium/cockpit-small, the exact
    // same-size (5x5) pair Ship.CatalogHulls.cs's own CreateDestroyer/CreateFreighter place touching at
    // (0,8)/(5,8) - see the end-to-end test below for that real hull data. ----
    private static bool CompartmentCatalog_TouchingSameSizeCompartments_SecondOnesSealedRegionIsRectangular()
    {
        var grid = new TileGrid();
        var entryA = CompartmentCatalog.Find("engine-medium");   // 5x5
        var entryB = CompartmentCatalog.Find("cockpit-small");    // 5x5 - same size as A
        if (entryA is null || entryB is null)
            return false;

        var resultA = CompartmentPlacer.Stamp(grid, entryA, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a");
        var resultB = CompartmentPlacer.Stamp(grid, entryB, new TileCoord(5, 0), rotationSteps: 0, instanceId: "b"); // A ends at x=4, B starts at x=5: zero gap
        if (!resultA.Success || !resultB.Success)
            return false;

        // The shared boundary (A's east ring column x=4) must be exactly one wall tile thick along
        // every row, corners included - checking the actual tile states directly, not just the region.
        for (var y = 0; y <= 4; y++)
        {
            if (grid.CellAt(new TileCoord(4, y)) is not { Wall: TileWallKind.Solid })
                return false; // A's own wall must still be standing the whole way down
            if (grid.CellAt(new TileCoord(5, y)) is not { HasFloor: true } farCell)
                return false;
            var expectSolid = y == 0 || y == 4; // B's own corners (genuine top/bottom exterior) stay Solid
            if (expectSolid && farCell.Wall != TileWallKind.Solid)
                return false;
            if (!expectSolid && farCell.Wall != TileWallKind.None)
                return false;
        }

        var regionB = grid.RegionIdAt(new TileCoord(6, 2));
        if (regionB is not { } regionBId)
            return false;
        var tiles = grid.Regions[regionBId].Tiles;
        var minX = tiles.Min(t => t.X);
        var maxX = tiles.Max(t => t.X);
        var minY = tiles.Min(t => t.Y);
        var maxY = tiles.Max(t => t.Y);
        var bboxArea = (maxX - minX + 1) * (maxY - minY + 1);
        return tiles.Count == bboxArea; // genuinely rectangular - the exact assertion TileShipBuilder needs
    }

    // ---- The exterior-corner case the fix must NOT break: when only ONE of a corner tile's two ring
    // sides touches another compartment and the OTHER side is genuine open space, that tile must still
    // come out Wall.Solid - a real hull wall, not a hole. Reuses the same touching pair as the test
    // above/below (its own NW/SW corners on the shared boundary are exactly this case), asserted
    // directly against tile state so a regression that clears BOTH directions (undoing the fix) or ONLY
    // fixes one specific corner would still be caught. ----
    private static bool CompartmentCatalog_TouchingCompartments_ExteriorSideOfMixedCornerStaysSolid()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4
        if (entry is null)
            return false;

        var resultA = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a");
        var resultB = CompartmentPlacer.Stamp(grid, entry, new TileCoord(4, 0), rotationSteps: 0, instanceId: "b");
        if (!resultA.Success || !resultB.Success)
            return false;

        // B's local (0,0) and (0,3) - NW/SW corners - sit on the shared West boundary AND on B's own
        // genuine North/South exterior edge (the very top/bottom row of the whole combined shape).
        bool IsSolidHullCorner(TileCoord c) => grid.CellAt(c) is { HasFloor: true, Wall: TileWallKind.Solid };
        if (!IsSolidHullCorner(new TileCoord(4, 0)))
            return false; // NW corner - would leak to vacuum above if this were cleared to None
        if (!IsSolidHullCorner(new TileCoord(4, 3)))
            return false; // SW corner - would leak to vacuum below if this were cleared to None

        // A's own corresponding corners (never touched by any dedup at all) must obviously still be
        // Solid too - the fix only changes B's tiles, never an earlier compartment's.
        return IsSolidHullCorner(new TileCoord(3, 0)) && IsSolidHullCorner(new TileCoord(3, 3));
    }

    // ---- The genuinely-interior counterpart: a corner tile whose BOTH ring sides touch an existing
    // wall (a 4-way junction - this compartment slots into the inside corner formed by 3 already-placed
    // neighbors) has no genuine exterior side left at all, so THIS corner must dedup away to open floor,
    // unlike the mixed corner above - confirming the fix distinguishes the two cases rather than just
    // always keeping corners Solid. ----
    private static bool CompartmentCatalog_FourWayJunctionCorner_StillDedupsToOpenFloor()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4
        if (entry is null)
            return false;

        var resultA = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a"); // top-left
        var resultB = CompartmentPlacer.Stamp(grid, entry, new TileCoord(4, 0), rotationSteps: 0, instanceId: "b"); // top-right, east of A
        var resultC = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 4), rotationSteps: 0, instanceId: "c"); // bottom-left, south of A
        var resultD = CompartmentPlacer.Stamp(grid, entry, new TileCoord(4, 4), rotationSteps: 0, instanceId: "d"); // bottom-right - touches B (north) and C (west) at once
        if (!resultA.Success || !resultB.Success || !resultC.Success || !resultD.Success)
            return false;

        // D's own NW corner (local (0,0), absolute (4,4)): West neighbor is C's east wall, North
        // neighbor is B's south wall - both already Solid before D's own ring is stamped.
        return grid.CellAt(new TileCoord(4, 4)) is { HasFloor: true, Wall: TileWallKind.None };
    }

    // ---- Real end-to-end proof: the SAME two compartments, at the SAME anchors, Ship.CatalogHulls.cs's
    // own CreateDestroyer/CreateFreighter actually use for their touching engine/cockpit pair (both
    // "engine-medium" and "cockpit-small" are 5x5 - the only same-height touching pair either hull's
    // spine has). Confirms the fix isn't just correct for a synthetic square (life-support-small) but
    // for the exact real hull data this bug report was about. ----
    private static bool CompartmentCatalog_RealDestroyerEngineCockpitPair_ProducesRectangularRegions()
    {
        var grid = new TileGrid();
        var engineEntry = CompartmentCatalog.Find("engine-medium");
        var cockpitEntry = CompartmentCatalog.Find("cockpit-small");
        if (engineEntry is null || cockpitEntry is null)
            return false;
        if (engineEntry.Width != cockpitEntry.Width || engineEntry.Height != cockpitEntry.Height)
            return false; // sanity - this test only means what it claims if they really are same-size

        var resultEngine = CompartmentPlacer.Stamp(grid, engineEntry, new TileCoord(0, 8), rotationSteps: 0, instanceId: "destroyer-engine");
        var resultCockpit = CompartmentPlacer.Stamp(grid, cockpitEntry, new TileCoord(5, 8), rotationSteps: 0, instanceId: "destroyer-cockpit");
        if (!resultEngine.Success || !resultCockpit.Success)
            return false;

        bool IsRectangular(TileCoord probe)
        {
            var regionId = grid.RegionIdAt(probe);
            if (regionId is not { } id)
                return false;
            var tiles = grid.Regions[id].Tiles;
            var minX = tiles.Min(t => t.X);
            var maxX = tiles.Max(t => t.X);
            var minY = tiles.Min(t => t.Y);
            var maxY = tiles.Max(t => t.Y);
            return tiles.Count == (maxX - minX + 1) * (maxY - minY + 1);
        }

        return IsRectangular(new TileCoord(2, 10)) && IsRectangular(new TileCoord(7, 10));
    }

    // ---- Overlap rejection: stamping a compartment onto already-occupied floor must fail cleanly,
    // without throwing and without corrupting whatever was already there. ----
    private static bool CompartmentCatalog_OverlappingPlacement_IsRefusedCleanlyWithoutCorruptingTheGrid()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small");
        if (entry is null)
            return false;

        var first = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 0), rotationSteps: 0, instanceId: "first");
        if (!first.Success)
            return false;
        var regionCountBefore = grid.Regions.Count;
        var cellCountBefore = grid.Cells.Count;

        // Anchored 1 tile inside the first compartment's own footprint - guaranteed overlap.
        var second = CompartmentPlacer.Stamp(grid, entry, new TileCoord(1, 1), rotationSteps: 0, instanceId: "second");
        if (second.Success || second.Error is null)
            return false;

        return grid.Regions.Count == regionCountBefore && grid.Cells.Count == cellCountBefore;
    }

    // ---- A rotated placement's wall dedup still works correctly against a neighbor. "life-support-
    // small" is square (4x4) so rotation doesn't change its overall footprint dimensions, keeping the
    // anchor math identical to the unrotated dedup test above while still exercising a genuinely
    // rotated stamp (its device position/ring classification go through the full rotation transform). ----
    private static bool CompartmentCatalog_RotatedTouchingCompartment_StillDedupsToOneWallTile()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4 (square - rotation keeps the same footprint dims)
        if (entry is null)
            return false;

        var resultA = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a");
        var resultB = CompartmentPlacer.Stamp(grid, entry, new TileCoord(4, 0), rotationSteps: 1, instanceId: "b"); // rotated 90 deg
        if (!resultA.Success || !resultB.Success)
            return false;

        var wallTileCount = 0;
        for (var x = 1; x <= 6; x++)
            if (grid.CellAt(new TileCoord(x, 1)) is { Wall: not TileWallKind.None })
                wallTileCount++;
        if (wallTileCount != 1)
            return false;

        var regionA = grid.RegionIdAt(new TileCoord(2, 1));
        var regionB = grid.RegionIdAt(new TileCoord(4, 1));
        return regionA is not null && regionB is not null && regionA != regionB;
    }

    // ---- Every catalog entry is internally sane - a cheap smoke check across all ~30 entries
    // (rectangular by construction, but the device/airlock positions are hand-authored data, so a
    // typo landing one on the ring or out of bounds would otherwise go unnoticed). ----
    private static bool CompartmentCatalog_EveryEntry_HasDevicesStrictlyInteriorAndInBounds()
    {
        foreach (var entry in CompartmentCatalog.Entries)
        {
            bool Inside(TileCoord p) => p.X >= 0 && p.X < entry.Width && p.Y >= 0 && p.Y < entry.Height;
            bool OnRing(TileCoord p) => p.X == 0 || p.X == entry.Width - 1 || p.Y == 0 || p.Y == entry.Height - 1;

            foreach (var device in entry.Devices)
                if (!Inside(device.RelativePosition) || OnRing(device.RelativePosition))
                    return false;

            if (entry.Airlock is { } airlock)
            {
                var doorPos = CompartmentPlacer.Rotate(entry, 0).Airlock?.DoorPosition;
                if (doorPos is null || !Inside(doorPos.Value) || !OnRing(doorPos.Value))
                    return false;
            }
        }
        return true;
    }
}
