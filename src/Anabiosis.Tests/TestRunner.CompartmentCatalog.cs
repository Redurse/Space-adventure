using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // M80 (humble-soaring-cat.md) - pure data/algorithm tests against a plain TileGrid, exactly like
    // TestRunner.TileGrid.cs's own tests. Nothing here touches Ship/World/the Client - CompartmentPlacer
    // isn't wired into the Ship Editor yet (that's M81+).
    //
    // Every test that named a specific catalog entry (rotation transform, engine tier geometry,
    // wall-dedup, overlap rejection) was removed along with CompartmentCatalog.Entries itself
    // (direct user request, "вместо всех текущих отсеков я буду присылать новые вариации") - this
    // one survives because it's genuinely entry-agnostic: it iterates whatever Entries actually
    // holds, so it's still exactly the right smoke check to have once new entries land there.

    // ---- Every catalog entry is internally sane - a cheap smoke check across the whole catalog
    // (rectangular-or-multi-rect-union by construction, but the device/airlock positions are
    // hand-authored data, so a typo landing one on the ring or out of bounds would otherwise go
    // unnoticed). Generalized (M91, humble-soaring-cat.md non-rectangular compartments) to test
    // against the entry's own FootprintRects tile SET instead of a single W x H box - byte-identical
    // to the old box-edge test whenever an entry has exactly one rect. ----
    private static bool CompartmentCatalog_EveryEntry_HasDevicesStrictlyInteriorAndInBounds()
    {
        foreach (var entry in CompartmentCatalog.Entries)
        {
            var tiles = new HashSet<TileCoord>();
            foreach (var rect in entry.FootprintRects)
                for (var x = (int)rect.X; x < (int)rect.Right; x++)
                    for (var y = (int)rect.Y; y < (int)rect.Bottom; y++)
                        tiles.Add(new TileCoord(x, y));

            bool Inside(TileCoord p) => tiles.Contains(p);
            // Diagonal neighbors count too (M91 follow-up, "стены не обрезались... по диагонали") -
            // CompartmentPlacer.Stamp now walls a tile whose only exposure to the void is diagonal,
            // so a device sitting there would conflict with that new wall (TileGrid.PlaceDevice
            // refuses a tile that already carries one) - this check must agree with Stamp's own.
            bool OnRing(TileCoord p)
            {
                for (var dx = -1; dx <= 1; dx++)
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        if (!tiles.Contains(new TileCoord(p.X + dx, p.Y + dy)))
                            return true;
                    }
                return false;
            }

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

    // ---- M91 (humble-soaring-cat.md) - stamping a genuinely multi-rect entry (reactor-d, the
    // notched-corner octagon) produces exactly its own footprint as floor, walls only the true
    // exterior boundary (never the internal seams between its 3 constituent rects), and rotates
    // cleanly through all 4 steps (each step's floor tile count must stay the same - rotation only
    // repositions the shape, never changes its area). ----
    private static bool CompartmentPlacer_MultiRectEntry_StampsExactFootprintWithNoInteriorWalls()
    {
        var entry = CompartmentCatalog.Find("reactor-d")!;
        var grid = new TileGrid();
        var result = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 0), rotationSteps: 0, instanceId: "test");
        if (!result.Success)
            return false;

        var expectedTiles = new HashSet<TileCoord>();
        foreach (var rect in entry.FootprintRects)
            for (var x = (int)rect.X; x < (int)rect.Right; x++)
                for (var y = (int)rect.Y; y < (int)rect.Bottom; y++)
                    expectedTiles.Add(new TileCoord(x, y));

        foreach (var coord in expectedTiles)
            if (grid.CellAt(coord) is not { HasFloor: true })
                return false;

        // The 2x2 corners cut from the bounding box must have NO floor at all (never touched).
        var bboxWidth = entry.Width;
        var bboxHeight = entry.Height;
        foreach (var (cx, cy) in new[] { (0, 0), (bboxWidth - 1, 0), (0, bboxHeight - 1), (bboxWidth - 1, bboxHeight - 1) })
            if (grid.CellAt(new TileCoord(cx, cy)) is { HasFloor: true })
                return false;

        // Internal seams (where the middle band meets the top/bottom bands) must carry no wall -
        // e.g. (4,1) sits on the top band's own bottom row, immediately above the middle band's own
        // top row at (4,2) - both part of this SAME footprint, so neither gets a wall tile.
        if (grid.CellAt(new TileCoord(4, 1)) is not { Wall: TileWallKind.None })
            return false;
        if (grid.CellAt(new TileCoord(4, 2)) is not { Wall: TileWallKind.None })
            return false;

        // But the top band's own genuine exterior top edge does get walled.
        if (grid.CellAt(new TileCoord(4, 0)) is not { Wall: TileWallKind.Solid })
            return false;

        // Direct user request ("стены не обрезались в местах клетки которых граничат с космосом по
        // диагонали") - (2,2) sits at the reentrant step corner where the top band meets the middle
        // band: all 4 of its ORTHOGONAL neighbors are genuine floor (interior under the old rule),
        // but its diagonal NW neighbor (1,1) is void (part of the cut corner) - it must still get a
        // wall, sealing what would otherwise be a one-tile diagonal gap into the void.
        if (grid.CellAt(new TileCoord(2, 2)) is not { Wall: TileWallKind.Solid })
            return false;

        return true;
    }

    private static bool CompartmentPlacer_MultiRectEntry_RotatesThroughAllFourStepsPreservingArea()
    {
        var entry = CompartmentCatalog.Find("reactor-d")!;
        var unrotatedTileCount = entry.FootprintRects.Sum(r => (int)r.Width * (int)r.Height);
        for (var steps = 0; steps < 4; steps++)
        {
            var rotated = CompartmentPlacer.Rotate(entry, steps);
            var tileCount = rotated.FootprintRects.Sum(r => (int)r.Width * (int)r.Height);
            if (tileCount != unrotatedTileCount)
                return false;

            var grid = new TileGrid();
            var result = CompartmentPlacer.Stamp(grid, entry, new TileCoord(20 * steps, 0), steps, $"test-{steps}");
            if (!result.Success)
                return false;
        }
        return true;
    }
}
