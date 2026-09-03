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
        // arrived, so nothing about it changes. B's dedup fires for EVERY row along the touching
        // boundary (A's entire east ring column is solid top to bottom, not just the middle row), so
        // B's whole west ring column (4 tiles, corners included) turns to floor too, joining its own
        // 2x2 interior into one connected 8-tile region - not 4+4=8 by coincidence, but literally
        // "west column (4) + interior (4)" once the boundary dedup is traced all the way across.
        return grid.Regions[regionA.Value].Tiles.Count == 4 && grid.Regions[regionB.Value].Tiles.Count == 8;
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
