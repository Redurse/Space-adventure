namespace Anabiosis.Shared.Model;

// M85 follow-up (humble-soaring-cat.md) - Shared, MonoGame-free port of Game1.ShipEditor.TileBridge.
// cs's own BuildDefinitionFromTiles algorithm, so a hand-authored, compartment-catalog-built hull
// (Ship.cs's Destroyer/Freighter factories, built from CompartmentPlacer.Stamp calls onto a plain
// TileGrid) can go through the EXACT same TileGrid -> CustomShipDefinition conversion the player's own
// free-tile Ship Editor already uses and has proven against Ship.FromCustomDefinition/
// CustomShipValidator, instead of a second hand-rolled Room/Door export path. Game1.ShipEditor.
// TileBridge.cs's own BuildDefinitionFromTiles now just forwards its own fields into this - same
// behavior, still covered by its own existing tests (TestRunner.CompartmentEditor.cs etc.) - this file
// only replaces WHERE the algorithm lives, not what it does. See that file's own doc comment for the
// deeper "why" behind the wall/gap-closing model this ports unchanged.
public static class TileShipBuilder
{
    private readonly record struct TileRoomRect(int MinX, int MinY, int MaxX, int MaxY)
    {
        public int Width => MaxX - MinX + 1;
        public int Height => MaxY - MinY + 1;
    }

    // One placed engine's own facing+thrust, keyed by its Control tile. Carries the REAL per-catalog-
    // entry MaxThrust (e.g. CompartmentCatalog's engine-medium: 12f) unlike the free-tile editor's own
    // single flat EngineMaxThrust constant (Game1.ShipEditor.cs) - a compartment-built hull's engines
    // keep their own correct catalog thrust instead of being flattened to one editor-wide value.
    public readonly record struct EngineSpec(TileSide Facing, float MaxThrust);

    // Named zones are purely cosmetic room-naming (Game1.ShipEditor.TileBridge.cs's own ZoneNameFor
    // doc comment) - optional, defaults to none, which is exactly what a compartment-catalog-built
    // hull needs (its compartments already have their own catalog DisplayName-derived identity, no
    // player-drawn zones involved).
    public static (CustomShipDefinition? Definition, IReadOnlyList<string> Errors) BuildDefinition(
        TileGrid tiles,
        IReadOnlyDictionary<TileCoord, CustomDeviceKind> deviceKinds,
        IReadOnlyDictionary<TileCoord, EngineSpec> engines,
        string shipName,
        float forwardDegrees,
        IReadOnlyList<(string Name, IReadOnlySet<TileCoord> Tiles)>? zones = null,
        // Direct user request ("стеллаж... можно поворачивать") - keyed by the SAME anchor
        // deviceKinds already uses, true for a placed non-square device whose own authored
        // Width/Height got swapped before stamping (Game1.ShipEditor.cs's own
        // _editorDeviceRotation) - defaults to null/empty for the one test call site that never
        // needed rotation, same convention WallMaterialsRaw/EnginesRaw already use.
        IReadOnlyDictionary<TileCoord, bool>? deviceRotations = null)
    {
        zones ??= Array.Empty<(string, IReadOnlySet<TileCoord>)>();
        deviceRotations ??= new Dictionary<TileCoord, bool>();
        var errors = new List<string>();
        if (tiles.Regions.Count == 0)
        {
            errors.Add("Нарисуйте хотя бы один отсек (пол внутри стен), прежде чем играть.");
            return (null, errors);
        }

        // 1) Decompose every region's tile set into a small union of non-overlapping rectangles
        // (RectilinearDecomposition, humble-soaring-cat.md M87) instead of rejecting anything that
        // isn't a single perfectly-filled rectangle outright - a plain rectangular region (every
        // hand-authored/editor-drawn room so far) still yields exactly 1 piece; an L/plus/notched-
        // corner shape now yields a few pieces instead of an error.
        var rects = new Dictionary<int, List<TileRoomRect>>();
        foreach (var (regionId, region) in tiles.Regions)
        {
            if (region.Tiles.Count == 0)
                continue;
            var (decomposed, error) = RectilinearDecomposition.Decompose(region.Tiles);
            if (error is not null || decomposed is null)
            {
                errors.Add($"{ZoneNameFor(zones, region.Tiles) ?? $"Отсек {regionId}"}: {error ?? "не удалось разобрать форму отсека."}");
                continue;
            }
            rects[regionId] = decomposed.Select(r => new TileRoomRect(r.MinX, r.MinY, r.MaxX, r.MaxY)).ToList();
        }
        if (errors.Count > 0)
            return (null, errors);

        var roomIds = rects.Keys.ToDictionary(id => id, id => $"room-{id}");
        var doorPairs = new HashSet<(int A, int B)>();
        // Every (region, subrect, side) that turned out to face ANOTHER room's subrect once gap-
        // closing ran - excluded from airlock consideration below regardless of whether that shared
        // boundary got a door or stayed solid, since either way it's no longer a genuinely exterior
        // hull side (the same "no neighbouring room on this side" condition CustomShipValidator
        // itself enforces). Keyed per subrect, not per whole region, since a multi-piece room can
        // have one piece touching a neighbour while another piece's same-direction side is still
        // genuine open hull.
        var sidesWithNeighbor = new HashSet<(int RegionId, int SubrectIndex, TileSide Side)>();

        // 2) Close the 1-tile gap wherever two DIFFERENT regions' subrects are separated by a
        // single wall/door column or row - only checking East/South from each subrect's own
        // perspective means every adjacent pair gets examined exactly once (the pair's other half
        // would find it via West/North). Never fires between two subrects of the SAME region - they
        // already tile that region's floor with zero gap by construction of the decomposition above,
        // so CloseGapIfAdjacent's own neighborId==regionId guard naturally excludes them.
        foreach (var regionId in rects.Keys.ToList())
            for (var i = 0; i < rects[regionId].Count; i++)
            {
                CloseGapIfAdjacent(tiles, regionId, i, TileSide.East, rects, doorPairs, sidesWithNeighbor);
                CloseGapIfAdjacent(tiles, regionId, i, TileSide.South, rects, doorPairs, sidesWithNeighbor);
            }

        // 3) Airlocks - a Door tile immediately outside one of a subrect's sides, with genuine open
        // space (no other region) beyond it, marks that whole side as the ship's hull airlock -
        // exactly the "no neighbouring room on this side" condition CustomShipValidator itself
        // requires (rule 7), just derived here instead of authored as a separate editor tool. A
        // multi-piece room can in principle qualify on more than one subrect - ShipLayoutGeometry's
        // own M89 follow-up is where "at most one subrect per (room, side)" gets enforced/validated;
        // this step stays a faithful per-subrect generalization of the old per-region check.
        var airlocks = new List<CustomAirlockDef>();
        foreach (var (regionId, subrects) in rects)
            for (var i = 0; i < subrects.Count; i++)
                foreach (var side in TileSideExtensions.All)
                    if (!sidesWithNeighbor.Contains((regionId, i, side)) && SideIsAirlock(tiles, subrects[i], side))
                        airlocks.Add(new CustomAirlockDef(roomIds[regionId], ToEdgeSide(side)));

        // 4) Devices - a device tile always sits on open floor (TileGrid.PlaceDevice's own
        // precondition), so it's always a member of exactly one region/room. `deviceKinds` is keyed
        // by each device's own anchor (top-left) tile only - exporting the CENTER of its full
        // footprint (not the anchor corner) keeps a multi-tile device like the Reactor positioned
        // where CustomDeviceDef's point-containment check (Ship.Custom.cs's RoomIdAt) expects it -
        // still well inside its own room's rectangle either way.
        var devices = new List<CustomDeviceDef>();
        foreach (var (coord, cell) in tiles.Cells)
        {
            if (cell.DeviceId is null || !deviceKinds.TryGetValue(coord, out var kind))
                continue;
            var (footprintWidth, footprintHeight) = CustomDeviceFootprint.Size(kind);
            if (deviceRotations.TryGetValue(coord, out var isRotated) && isRotated)
                (footprintWidth, footprintHeight) = (footprintHeight, footprintWidth);
            devices.Add(new CustomDeviceDef(kind, coord.X + footprintWidth / 2f, coord.Y + footprintHeight / 2f));
        }

        var rooms = rects.Select(kv => new CustomRoomDef(
            roomIds[kv.Key], ZoneNameFor(zones, tiles.Regions[kv.Key].Tiles) ?? $"Отсек {kv.Key}",
            kv.Value.Select(r => new RectF(r.MinX, r.MinY, r.Width, r.Height)).ToArray())).ToList();
        var doors = doorPairs.Select(p => new CustomDoorDef(roomIds[p.A], roomIds[p.B])).ToList();

        // Wall materials (direct user request - "усиленная стена"/"иллюминатор") - every painted
        // Solid tile whose material isn't the default Standard, keyed by the SAME tile coordinate
        // Ship.Custom.cs's ApplyWallMaterials looks up against each auto-generated WallBlock's own
        // position (via TileGridRasterizer.WallBlockTileCoord). A tile that closed a gap (extended
        // into what CloseGapIfAdjacent turned into a room-interior wall) still exports correctly -
        // Ship.FromCustomDefinition regenerates its own interior WallBlock at that exact tile.
        var wallMaterials = tiles.Cells
            .Where(kv => kv.Value.Wall == TileWallKind.Solid && kv.Value.WallMaterial != WallMaterial.Standard)
            .Select(kv => new CustomWallMaterialDef(kv.Key.X, kv.Key.Y, kv.Value.WallMaterial))
            .ToList();

        // Real engines (ShipEngine.cs, the Engine editor tool) - anchored at the Control tile's own
        // centre (X+0.5/Y+0.5), the same tile-center convention the `devices` loop above already uses
        // for a 1x1 footprint (coord.X + footprintSize/2f, footprintSize=1). CustomShipValidator
        // treats a non-empty Engines list as satisfying the "needs a way to move" rule on its own, no
        // flat CustomDeviceKind.Engine required alongside it.
        var engineDefs = engines
            .Select(kv => new CustomEngineDef(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Value.Facing, kv.Value.MaxThrust))
            .ToList();

        return (new CustomShipDefinition(shipName, rooms, doors, airlocks, devices, forwardDegrees, wallMaterials, engineDefs), errors);
    }

    // Looks for exactly one other already-converted region's SUBRECT sitting 2 tiles away in
    // `direction` (1 tile of wall/door in between), with a matching span on the perpendicular axis -
    // a clean, straight shared wall, not a partial/offset touch. When found, extends this subrect by
    // 1 tile onto that wall/door tile so the two rooms end up touching exactly (see this file's own
    // doc comment), and records the pair as door-connected if any tile along that boundary is a door
    // rather than a plain solid wall. Never matches a subrect of `regionId` itself (its own
    // neighborId==regionId guard below) - two pieces of the same multi-rect room are already flush
    // by construction, never separated by a 1-tile wall/door gap.
    private static void CloseGapIfAdjacent(TileGrid tiles, int regionId, int subrectIndex, TileSide direction,
        Dictionary<int, List<TileRoomRect>> rects, HashSet<(int A, int B)> doorPairs,
        HashSet<(int RegionId, int SubrectIndex, TileSide Side)> sidesWithNeighbor)
    {
        var rect = rects[regionId][subrectIndex];
        IEnumerable<TileCoord> BoundaryLine(int offset) => direction switch
        {
            TileSide.East => Enumerable.Range(rect.MinY, rect.Height).Select(y => new TileCoord(rect.MaxX + offset, y)),
            TileSide.South => Enumerable.Range(rect.MinX, rect.Width).Select(x => new TileCoord(x, rect.MaxY + offset)),
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };

        var wallLine = BoundaryLine(1).ToList();
        if (wallLine.Any(c => tiles.CellAt(c) is not { Wall: not TileWallKind.None }))
            return; // not a clean 1-tile wall/door separator the whole way across

        var farRegionIds = BoundaryLine(2).Select(c => tiles.RegionIdAt(c)).Distinct().ToList();
        if (farRegionIds.Count != 1 || farRegionIds[0] is not { } neighborId || neighborId == regionId ||
            !rects.TryGetValue(neighborId, out var neighborSubrects))
            return;

        // Only the two subrects actually being 2 tiles apart in `direction` (exact) is required; the
        // PERPENDICULAR span only needs to overlap by at least one tile, not match exactly - two
        // compartments of different sizes (e.g. a 7-tall reactor next to a 5-tall cockpit) can still
        // share a real, partial boundary. This mirrors FindRoomPairOverlaps's own Math.Max/Math.Min
        // overlap check (ShipLayoutGeometry.cs) - it operates on rooms already known to touch with
        // zero gap and just asks whether SOME shared span exists, not that the whole span matches.
        // MinY/MaxY here are inclusive tile coordinates (TileRoomRect.Height = MaxY - MinY + 1), so
        // the overlap condition is the inclusive-range one: Max(near.Min, far.Min) <= Min(near.Max,
        // far.Max). E.g. Y-ranges [7,13] and [8,12] overlap (8 <= 12); [7,13] and [14,18] do not
        // (14 <= 13 is false). A clean straight join lands on at most one of the neighbor's own
        // pieces, so the first match found is the only one there ever is.
        var neighborIndex = -1;
        for (var j = 0; j < neighborSubrects.Count; j++)
        {
            var candidate = neighborSubrects[j];
            var matches = direction == TileSide.East
                ? candidate.MinX == rect.MaxX + 2 && Math.Max(rect.MinY, candidate.MinY) <= Math.Min(rect.MaxY, candidate.MaxY)
                : candidate.MinY == rect.MaxY + 2 && Math.Max(rect.MinX, candidate.MinX) <= Math.Min(rect.MaxX, candidate.MaxX);
            if (matches)
            {
                neighborIndex = j;
                break;
            }
        }
        if (neighborIndex < 0)
            return; // genuinely no shared span - leave both regions alone rather than guess

        rects[regionId][subrectIndex] = direction == TileSide.East ? rect with { MaxX = rect.MaxX + 1 } : rect with { MaxY = rect.MaxY + 1 };
        sidesWithNeighbor.Add((regionId, subrectIndex, direction));
        sidesWithNeighbor.Add((neighborId, neighborIndex, direction.Opposite()));
        if (wallLine.Any(c => tiles.CellAt(c) is { Wall: TileWallKind.Door }))
            doorPairs.Add((Math.Min(regionId, neighborId), Math.Max(regionId, neighborId)));
    }

    // Every tile directly beyond this side of the room (after any gap-closing above) must be either
    // a door, a plain solid wall, or genuine open space (no cell at all) - i.e. this side never
    // touches another region - and at least one of those tiles is a door, for this side to become an
    // airlock rather than a plain sealed hull wall.
    private static bool SideIsAirlock(TileGrid tiles, TileRoomRect rect, TileSide side)
    {
        IEnumerable<TileCoord> Line() => side switch
        {
            TileSide.North => Enumerable.Range(rect.MinX, rect.Width).Select(x => new TileCoord(x, rect.MinY - 1)),
            TileSide.South => Enumerable.Range(rect.MinX, rect.Width).Select(x => new TileCoord(x, rect.MaxY + 1)),
            TileSide.East => Enumerable.Range(rect.MinY, rect.Height).Select(y => new TileCoord(rect.MaxX + 1, y)),
            TileSide.West => Enumerable.Range(rect.MinY, rect.Height).Select(y => new TileCoord(rect.MinX - 1, y)),
            _ => throw new ArgumentOutOfRangeException(nameof(side)),
        };

        var hasDoor = false;
        foreach (var coord in Line())
        {
            var cell = tiles.CellAt(coord);
            if (cell is { Wall: TileWallKind.Door })
                hasDoor = true;
            else if (cell is { Wall: TileWallKind.None })
                return false; // open floor with no wall right next to us - this side touches another region's territory directly, not clean hull
        }
        return hasDoor;
    }

    private static EdgeSide ToEdgeSide(TileSide side) => side switch
    {
        TileSide.North => EdgeSide.Top,
        TileSide.South => EdgeSide.Bottom,
        TileSide.East => EdgeSide.Right,
        TileSide.West => EdgeSide.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    // Friendly room naming (direct user feature, M76 follow-up: "Зоны") - prefers whichever named
    // zone overlaps the most tiles of this region, falling back to a generic "Отсек N" label if
    // there's no zone (or no overlapping one) at all - which is every compartment-catalog-built hull,
    // today (see this file's own doc comment on the `zones` parameter).
    private static string? ZoneNameFor(IReadOnlyList<(string Name, IReadOnlySet<TileCoord> Tiles)> zones, IEnumerable<TileCoord> tiles)
    {
        var tileSet = tiles as HashSet<TileCoord> ?? tiles.ToHashSet();
        return zones
            .Select(z => (z.Name, Overlap: z.Tiles.Count(tileSet.Contains)))
            .Where(x => x.Overlap > 0)
            .OrderByDescending(x => x.Overlap)
            .Select(x => x.Name)
            .FirstOrDefault();
    }

}
