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
    // Direct user request ("система отсеков по-другому" follow-up, Helm/Navigation's own new 3x2
    // rotatable footprint) - checked at EVERY rotation step (0-3), not just the authored orientation,
    // via a real Stamp call: a multi-tile device's rotated anchor (CompartmentPlacer's own
    // RotateDeviceAnchorClockwise) landing even one tile outside the room or on the ring would make
    // TileGrid.PlaceDevice throw inside Stamp - a real, sharp failure mode a purely-geometric check
    // against `entry.Devices` alone (the OLD version of this test) could never catch, since it only
    // ever inspected the UNROTATED authored position.
    private static bool CompartmentCatalog_EveryEntry_HasDevicesStrictlyInteriorAndInBounds()
    {
        foreach (var entry in CompartmentCatalog.Entries)
            for (var steps = 0; steps < 4; steps++)
            {
                var grid = new TileGrid();
                var result = CompartmentPlacer.Stamp(grid, entry, new TileCoord(30 * steps, 0), steps, $"{entry.Id}-{steps}");
                if (!result.Success)
                    return false;

                var tiles = new HashSet<TileCoord>();
                var rotated = CompartmentPlacer.Rotate(entry, steps);
                foreach (var rect in rotated.FootprintRects)
                    for (var x = (int)rect.X; x < (int)rect.Right; x++)
                        for (var y = (int)rect.Y; y < (int)rect.Bottom; y++)
                            tiles.Add(new TileCoord(x, y));

                bool Inside(TileCoord p) => tiles.Contains(p);
                // Diagonal neighbors count too (M91 follow-up, "стены не обрезались... по диагонали") -
                // CompartmentPlacer.Stamp now walls a tile whose only exposure to the void is
                // diagonal, so a device sitting there would conflict with that new wall - this check
                // must agree with Stamp's own IsRingTile.
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

                foreach (var (position, kind, _, _, deviceRotated, halfSide) in rotated.Devices)
                {
                    var (baseWidth, baseHeight) = CustomDeviceFootprint.Size(kind);
                    var (width, height) = deviceRotated ? (baseHeight, baseWidth) : (baseWidth, baseHeight);
                    // Direct user request ("я хочу чтобы ты сделал отсек таким каким я его
                    // сохранил") - an IsHalfWidthKind device's own "half" tile is now allowed to
                    // sit ON the ring (CompartmentPlacer.Stamp places it via PlaceHalfWidthDevice,
                    // coexisting with the wall rather than needing bare floor there); every OTHER
                    // tile of every device, half-width or not, still must be strictly interior.
                    var resolvedHalfSide = CustomDeviceFootprint.IsHalfWidthKind(kind)
                        ? halfSide ?? CustomDeviceFootprint.ResolveHalfSide(null, deviceRotated)
                        : (TileSide?)null;
                    for (var dx = 0; dx < width; dx++)
                        for (var dy = 0; dy < height; dy++)
                        {
                            var tile = new TileCoord(position.X + dx, position.Y + dy);
                            var isHalfTile = resolvedHalfSide is { } hs && CustomDeviceFootprint.IsHalfTileOfHalfWidthFootprint(tile, position, hs);
                            if (!Inside(tile) || (OnRing(tile) && !isHalfTile))
                                return false;
                        }
                }

                if (rotated.Airlock is { } airlock && (!Inside(airlock.DoorPosition) || !OnRing(airlock.DoorPosition)))
                    return false;
            }
        return true;
    }

    // The reactor-d/engine-a-specific tests that used to live here (multi-rect stamping/rotation,
    // authored half-block walls) were removed along with those entries when the catalog was cleared
    // again (direct user request, "давай сейчас я создам множество новых отсеков а все старые
    // сейчас удалим") - same "goes with the entry it named" precedent this file's own top comment
    // already documents from the first clearing. CompartmentCatalog_EveryEntry_HasDevicesStrictly
    // InteriorAndInBounds above is entry-agnostic and stays exactly as it was.
}
