namespace Anabiosis.Shared.Model;

// M85 follow-up (humble-soaring-cat.md) - Shared, MonoGame-free port of Game1.ShipEditor.TileBridge.
// cs's own BuildDefinitionFromTiles algorithm - the free-tile Ship Editor's "Играть" button (both
// hand-painted floor and the editor's own "Отсек/Compartment" catalog-stamping tool, which paints
// onto this same TileGrid) is the sole production caller today (Ship.CreateDestroyer()/
// CreateFreighter() build their rooms directly from CompartmentCatalog's own Width/Height instead -
// an earlier version of that file DID route through here, moved away from it for reasons that file's
// own doc comment explains; this comment used to claim otherwise, corrected). Game1.ShipEditor.
// TileBridge.cs's own BuildDefinitionFromTiles just forwards its own fields into this. See that
// file's own doc comment for the deeper "why" behind the wall/gap-closing model this ports unchanged.
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
        // Every door tile found while gap-closing below (humble-soaring-cat.md "Дверь как свободный
        // объект") - replaces the old HashSet<(int A, int B)> room-pair flag, which only ever
        // recorded THAT a door existed somewhere on a boundary, collapsing both its exact position
        // and any second door on the same boundary into one bit. GroupId mirrors TileCell.
        // DoorGroupId (null for an ordinary single-tile door) so a player-linked wide door still
        // exports as one 2-tile-span door instead of two narrow ones.
        var doorTiles = new List<(TileCoord Coord, bool Vertical, string? GroupId)>();
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
                CloseGapIfAdjacent(tiles, regionId, i, TileSide.East, rects, doorTiles, sidesWithNeighbor);
                CloseGapIfAdjacent(tiles, regionId, i, TileSide.South, rects, doorTiles, sidesWithNeighbor);
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

        // 3.5) Direct user bug report ("комната в редакторе больше, чем в игре, на 2 по каждой
        // стороне") - `region.Tiles` above is pure open floor (Wall:None), so every subrect built
        // from it so far is short by exactly 1 tile on every side that has a real wall/door ring,
        // while TileGridRasterizer.FromRooms (the code that actually rasterizes ANY Room back into
        // a TileGrid, live in the running game) rasterizes a room's OWN walls onto its own rect's
        // outermost ring - the convention every hand-authored hull's Room already assumes (a
        // Room(X,Y,W,H) INCLUDES its own wall ring, same as CompartmentCatalog's own entries - see
        // that file's own doc comment). Left unfixed, that's a double deduction: -1 tile per side
        // here, then ANOTHER -1 per side when the exported room is rasterized back into a live
        // Ship's tiles, landing on (Width-2) x (Height-2) actually walkable in the running game
        // versus what was actually painted. Expanding each subrect's genuinely-exterior sides here
        // (absorbing the wall/door ring back into the rect, the same way a hand-authored hull's
        // literal already does) fixes it at the source.
        //
        // IsWallLine's own "the WHOLE line is Solid/Door" check is what tells a genuine exterior
        // side apart from an internal seam of the SAME multi-piece room (two pieces of one L-shaped
        // room already touch with zero gap by construction of the decomposition above - no wall
        // tile sits between them at all, so IsWallLine correctly returns false there and leaves the
        // seam alone) - no separate same-region check needed. Sides already recorded in
        // sidesWithNeighbor (absorbed into ANOTHER room via CloseGapIfAdjacent above) are skipped so
        // the same wall/door tile is never claimed by two rooms at once. Every side is decided from
        // the ORIGINAL (pre-expansion) rect and applied in one `with`, so the four sides of one
        // subrect can never see each other's expansion mid-decision.
        foreach (var regionId in rects.Keys.ToList())
            for (var i = 0; i < rects[regionId].Count; i++)
            {
                var rect = rects[regionId][i];
                var expandWest = !sidesWithNeighbor.Contains((regionId, i, TileSide.West)) && IsWallLine(tiles, rect, TileSide.West)
                    && !WallLineTouchesSiblingSubrect(rect, TileSide.West, rects[regionId], i);
                var expandEast = !sidesWithNeighbor.Contains((regionId, i, TileSide.East)) && IsWallLine(tiles, rect, TileSide.East)
                    && !WallLineTouchesSiblingSubrect(rect, TileSide.East, rects[regionId], i);
                var expandNorth = !sidesWithNeighbor.Contains((regionId, i, TileSide.North)) && IsWallLine(tiles, rect, TileSide.North)
                    && !WallLineTouchesSiblingSubrect(rect, TileSide.North, rects[regionId], i);
                var expandSouth = !sidesWithNeighbor.Contains((regionId, i, TileSide.South)) && IsWallLine(tiles, rect, TileSide.South)
                    && !WallLineTouchesSiblingSubrect(rect, TileSide.South, rects[regionId], i);
                rects[regionId][i] = rect with
                {
                    MinX = rect.MinX - (expandWest ? 1 : 0),
                    MaxX = rect.MaxX + (expandEast ? 1 : 0),
                    MinY = rect.MinY - (expandNorth ? 1 : 0),
                    MaxY = rect.MaxY + (expandSouth ? 1 : 0),
                };
            }

        // 3.6) Direct user bug report ("отсеки в игре опять не совпадают") - step 3.5 above only
        // absorbs a subrect's own ring on a side that is uniformly wall/door across that side's
        // WHOLE length. A T-junction - one subrect's own edge needing ring-absorption for only PART
        // of its length, because an adjacent same-room piece only starts partway along (e.g. a
        // spine's own cap sitting a row or two above where a perpendicular arm begins) - leaves a
        // residue of real, painted wall tiles that no subrect's bounds include at all: neither the
        // spine (not uniform - the arm covers PART of its edge) nor the arm (its own edge doesn't
        // reach that far) ever claims them. TileGridRasterizer.FromRooms then rasterizes that
        // residue as true vacuum, not the wall surface actually painted there - a visible hole, and
        // the room reading as a different shape than the editor's own canvas.
        //
        // Recomputed per WHOLE REGION (a residual tile can straddle more than one subrect's own
        // reach) via ExpandToPrivateWallRing: flood-fill the region's own uncontested Solid/Door
        // ring outward from its open floor, stopping at anything that also touches a DIFFERENT
        // region's own floor (a genuinely shared boundary - left entirely to the existing gap-
        // closing/wall-tracing rules above, unchanged).
        //
        // Deliberately NOT folded into any room's own Rects (an earlier version of this fix tried
        // decomposing the residue into extra RectF pieces and appending them there) - TileGridRasterizer.
        // FromRooms treats EVERY entry in Rects as a genuine floor-bearing piece whose own edges
        // suppress a NEIGHBORING piece's wall when they touch (the same-room-internal-seam rule),
        // and ShipLayoutGeometry.FindRoomPairOverlaps treats any two rects across DIFFERENT rooms
        // touching as a real shared boundary - neither is true of a residual sliver, which is pure
        // wall material with no interior behind it at all. A thin residual piece touching an
        // adjacent real piece of the SAME room made that real piece stop walling its own edge there
        // (a wall silently vanishing where none should), and a residual piece merely happening to
        // sit next to an UNRELATED room's own wall (two compartments independently walled right up
        // against each other, common at a tight pinch point) got misread as those two compartments
        // sharing a boundary, breaking airlock-side validation for a room the residue doesn't even
        // belong to. Recorded as a flat coordinate list on CustomShipDefinition instead
        // (SupplementalWallTiles) and painted directly onto the real Ship's own Tiles grid after
        // TileGridRasterizer.FromRooms has already run (Ship.Custom.cs) - genuinely just extra wall
        // tiles bolted on, never room geometry, so none of the above rules ever see them.
        var supplementalWallTiles = new HashSet<TileCoord>();
        foreach (var (regionId, region) in tiles.Regions)
        {
            if (!rects.TryGetValue(regionId, out var subrects) || subrects.Count == 0)
                continue;

            var covered = new HashSet<TileCoord>();
            foreach (var r in subrects)
                for (var x = r.MinX; x <= r.MaxX; x++)
                    for (var y = r.MinY; y <= r.MaxY; y++)
                        covered.Add(new TileCoord(x, y));

            foreach (var coord in ExpandToPrivateWallRing(tiles, region.Tiles))
                if (!covered.Contains(coord))
                    supplementalWallTiles.Add(coord);
        }

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
            var isRotated = deviceRotations.TryGetValue(coord, out var rotatedFlag) && rotatedFlag;
            if (isRotated)
                (footprintWidth, footprintHeight) = (footprintHeight, footprintWidth);
            devices.Add(new CustomDeviceDef(kind, coord.X + footprintWidth / 2f, coord.Y + footprintHeight / 2f, Rotated: isRotated));
        }

        // Closes the gap flagged this session: TileCell.WallDeviceId ("Терминал"/"Настенная лампа"
        // editor tools - both the floor-adjacent PlaceWallDevice mode and the wall-recessed
        // PlaceRecessedWallDevice mode, direct user request) used to never reach a real Ship at all -
        // it drew/saved fine in the editor but silently vanished on Play/export. Exports one
        // CustomDeviceDef per wall-device tile (tile-center, same convention every 1x1 device above
        // uses) - no per-ship limit any more (Ship.Custom.cs now builds a List<T>, same "many
        // independent instances" shape AmmoStorage/SuitLocker already have). WallDeviceFacingSide is
        // WallDeviceMountSide directly - already "which side the half-block visual sits on" in
        // either mode (TileCell's own doc comment), no recessed/protruding branch needed here.
        foreach (var (coord, cell) in tiles.Cells)
            if (cell.WallDeviceId is not null && cell.WallDeviceKind is { } wallDeviceKind)
                devices.Add(new CustomDeviceDef(wallDeviceKind, coord.X + 0.5f, coord.Y + 0.5f,
                    WallDeviceFacingSide: cell.WallDeviceMountSide));

        var rooms = rects.Select(kv => new CustomRoomDef(
            roomIds[kv.Key], ZoneNameFor(zones, tiles.Regions[kv.Key].Tiles) ?? $"Отсек {kv.Key}",
            kv.Value.Select(r => new RectF(r.MinX, r.MinY, r.Width, r.Height)).ToArray())).ToList();
        var doors = BuildDoorDefs(doorTiles);

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

        // Half-block walls (direct user request - "хочу сделать чтобы игрок сам выбирал") - every
        // Solid tile the Wall tool's own half-block variant explicitly painted (TileCell.
        // WallOpenSide), same export shape as wallMaterials above - Ship.Custom.cs's
        // ApplyWallOpenSides looks these up the identical way ApplyWallMaterials already does.
        var wallOpenSides = tiles.Cells
            .Where(kv => kv.Value.Wall == TileWallKind.Solid && kv.Value.WallOpenSide is not null)
            .Select(kv => new CustomWallOpenSideDef(kv.Key.X, kv.Key.Y, kv.Value.WallOpenSide!.Value))
            .ToList();

        // Real engines (ShipEngine.cs, the Engine editor tool) - anchored at the Control tile's own
        // centre (X+0.5/Y+0.5), the same tile-center convention the `devices` loop above already uses
        // for a 1x1 footprint (coord.X + footprintSize/2f, footprintSize=1). CustomShipValidator
        // treats a non-empty Engines list as satisfying the "needs a way to move" rule on its own, no
        // flat CustomDeviceKind.Engine required alongside it.
        var engineDefs = engines
            .Select(kv => new CustomEngineDef(kv.Key.X + 0.5f, kv.Key.Y + 0.5f, kv.Value.Facing, kv.Value.MaxThrust))
            .ToList();

        var forcedFloorTiles = tiles.Regions.Values.SelectMany(r => r.Tiles).ToList();

        // M-doors-as-edges (humble-soaring-cat.md) - unlike every other export above, this needs no
        // geometric detection at all: the editor's own TileGrid.DoorEdges IS the data (the editor is
        // the only place these get created, via AddDoorEdge), so exporting is a plain copy.
        var doorEdges = tiles.DoorEdges
            .Select(kv => new CustomDoorEdgeDef(kv.Key.Coord.X, kv.Key.Coord.Y, kv.Key.Side, kv.Value.Id))
            .ToList();

        return (new CustomShipDefinition(shipName, rooms, doors, airlocks, devices, forwardDegrees, wallMaterials, engineDefs,
            SupplementalWallTilesRaw: supplementalWallTiles.ToList(), ForcedFloorTilesRaw: forcedFloorTiles,
            WallOpenSidesRaw: wallOpenSides, DoorEdgesRaw: doorEdges), errors);
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
        Dictionary<int, List<TileRoomRect>> rects, List<(TileCoord Coord, bool Vertical, string? GroupId)> doorTiles,
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
        // direction == East means this boundary is a vertical line (rooms side by side along X) -
        // the same Vertical convention ShipLayoutGeometry.RoomPairOverlap already uses.
        foreach (var coord in wallLine)
            if (tiles.CellAt(coord) is { Wall: TileWallKind.Door } doorCell)
                doorTiles.Add((coord, direction == TileSide.East, doorCell.DoorGroupId));
    }

    // Turns the flat list of door tiles found while gap-closing above into actual CustomDoorDefs -
    // a lone tile (DoorGroupId null) becomes a 1-unit-span door centered on that tile; two tiles
    // sharing a DoorGroupId (TileCell.DoorGroupId's own "wide door" pairing, TileGrid.LinkDoors)
    // become one 2-unit-span door anchored at the pair's lower coordinate. Both cases place the
    // door's own center at the SAME boundary coordinate CloseGapIfAdjacent just merged the gap tile
    // into (tile coordinate + 1 on the perpendicular axis - see this file's own doc comment on why
    // the merged rect's edge, not the tile's own center, is where Door.X/Y needs to sit), matching
    // exactly what ShipLayoutGeometry.FindRoomPairOverlaps would have computed for the same boundary.
    private static List<CustomDoorDef> BuildDoorDefs(List<(TileCoord Coord, bool Vertical, string? GroupId)> doorTiles)
    {
        CustomDoorDef MakeDoorDef(TileCoord coord, bool vertical, bool wide)
        {
            var perpendicular = (vertical ? coord.X : coord.Y) + 1;
            var span = (vertical ? coord.Y : coord.X) + (wide ? 1f : 0.5f);
            return vertical ? new CustomDoorDef(perpendicular, span, true, wide) : new CustomDoorDef(span, perpendicular, false, wide);
        }

        var doors = new List<CustomDoorDef>();
        foreach (var group in doorTiles.GroupBy(t => t.GroupId))
        {
            var members = group.ToList();
            // A group of exactly 2 tiles, same orientation, is a genuine linked wide door; anything
            // else (a lone tile, or a malformed group) falls back to one narrow door per tile rather
            // than losing data.
            if (group.Key is not null && members.Count == 2 && members[0].Vertical == members[1].Vertical)
            {
                var vertical = members[0].Vertical;
                var anchor = vertical
                    ? (members[0].Coord.Y <= members[1].Coord.Y ? members[0].Coord : members[1].Coord)
                    : (members[0].Coord.X <= members[1].Coord.X ? members[0].Coord : members[1].Coord);
                doors.Add(MakeDoorDef(anchor, vertical, wide: true));
            }
            else
            {
                foreach (var member in members)
                    doors.Add(MakeDoorDef(member.Coord, member.Vertical, wide: false));
            }
        }
        return doors;
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

    // Whether the whole line of tiles immediately beyond this side of the rect is a clean, unbroken
    // wall/door run - same Line()-per-side shape SideIsAirlock already uses just below, but checking
    // for "solidly walled the whole way across" rather than "has at least one door in it". See the
    // room-rect-expansion step above (3.5) for what this decides.
    private static bool IsWallLine(TileGrid tiles, TileRoomRect rect, TileSide side)
    {
        IEnumerable<TileCoord> Line() => side switch
        {
            TileSide.West => Enumerable.Range(rect.MinY, rect.Height).Select(y => new TileCoord(rect.MinX - 1, y)),
            TileSide.East => Enumerable.Range(rect.MinY, rect.Height).Select(y => new TileCoord(rect.MaxX + 1, y)),
            TileSide.North => Enumerable.Range(rect.MinX, rect.Width).Select(x => new TileCoord(x, rect.MinY - 1)),
            TileSide.South => Enumerable.Range(rect.MinX, rect.Width).Select(x => new TileCoord(x, rect.MaxY + 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(side)),
        };
        return Line().All(c => tiles.CellAt(c) is { Wall: TileWallKind.Solid or TileWallKind.Door });
    }

    // Direct user bug report ("4 половублочных стены между 2 отсеками видны в редакторе, но
    // пропадают в игре") - a half-block wall the player paints right at the edge of an otherwise
    // uniform open floor region (e.g. two nubs narrowing a corridor from both sides) makes
    // RectilinearDecomposition carve off a thin, one-tile-wide "wing" strip on either side of the
    // narrowing. IsWallLine alone can't tell that wing's own genuine exterior boundary apart from
    // this player-painted internal notch - both look like "a clean Solid/Door line sits right
    // outside this side". Wrongly absorbing the notch tile here (step 3.5) lands the wing's own
    // Left and Right (or Top/Bottom) edge on the SAME single-tile column/row - Station.
    // IsUnitCoveredBySameRoom correctly suppresses one of the two resulting wall passes (a sibling
    // subrect already covers the far side), but the near side still walls the whole column, so the
    // wing ends up with ZERO real floor: the player's notch silently swallows what should have
    // stayed open floor right next to it (confirmed via a synthetic repro - the notch tile turned
    // into a full column of solid wall instead of a half-block one).
    //
    // The distinguishing signature: a genuine exterior wall has nothing but vacuum or another
    // region on its far side. This internal notch instead has the SAME region's own open floor
    // sitting right beside the candidate wall tile, one step ALONG the line itself (not into the
    // rect being expanded) - exactly where a sibling subrect of the same multi-piece room already
    // claims that floor. Refusing to absorb in that case leaves the notch tile uncovered by any
    // rect, so step 3.6's ExpandToPrivateWallRing/SupplementalWallTiles picks it up instead - the
    // same "bolt it on afterward, never touch Room geometry" path a T-junction's own residual wall
    // already uses, and it keeps its WallOpenSide (exported separately, unconditionally, regardless
    // of which room-building path picks up the base tile).
    private static bool WallLineTouchesSiblingSubrect(TileRoomRect rect, TileSide side, IReadOnlyList<TileRoomRect> siblings, int selfIndex)
    {
        IEnumerable<TileCoord> Line() => side switch
        {
            TileSide.West => Enumerable.Range(rect.MinY, rect.Height).Select(y => new TileCoord(rect.MinX - 1, y)),
            TileSide.East => Enumerable.Range(rect.MinY, rect.Height).Select(y => new TileCoord(rect.MaxX + 1, y)),
            TileSide.North => Enumerable.Range(rect.MinX, rect.Width).Select(x => new TileCoord(x, rect.MinY - 1)),
            TileSide.South => Enumerable.Range(rect.MinX, rect.Width).Select(x => new TileCoord(x, rect.MaxY + 1)),
            _ => throw new ArgumentOutOfRangeException(nameof(side)),
        };

        bool InSibling(TileCoord c)
        {
            for (var j = 0; j < siblings.Count; j++)
            {
                if (j == selfIndex)
                    continue;
                var s = siblings[j];
                if (c.X >= s.MinX && c.X <= s.MaxX && c.Y >= s.MinY && c.Y <= s.MaxY)
                    return true;
            }
            return false;
        }

        foreach (var coord in Line())
            foreach (var neighborSide in TileSideExtensions.All)
                if (InSibling(neighborSide.Offset(coord)))
                    return true;
        return false;
    }

    // Step 3.6's own flood-fill: every Solid/Door wall tile reachable from `floor` by repeatedly
    // stepping onto an already-absorbed tile, stopping at anything that ALSO touches open floor
    // outside `floor` (BordersForeignFloor below) - a genuinely shared boundary with some other
    // region, left untouched for the existing gap-closing/wall-tracing rules to resolve exactly as
    // they already do. Returns floor UNION the absorbed ring, i.e. this region's own true painted
    // footprint - not just its interior.
    private static HashSet<TileCoord> ExpandToPrivateWallRing(TileGrid tiles, IReadOnlySet<TileCoord> floor)
    {
        var footprint = new HashSet<TileCoord>(floor);
        var queue = new Queue<TileCoord>(floor);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var side in TileSideExtensions.All)
            {
                var neighbor = side.Offset(current);
                if (footprint.Contains(neighbor))
                    continue;
                if (tiles.CellAt(neighbor) is not { Wall: TileWallKind.Solid or TileWallKind.Door })
                    continue; // open floor, or nothing at all - not wall material to absorb
                if (BordersForeignFloor(tiles, neighbor, floor))
                    continue; // a genuinely shared wall with another region - not this region's alone
                footprint.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }
        return footprint;
    }

    // True if `wallCoord` has an orthogonal neighbor that's open floor NOT belonging to `ownFloor` -
    // i.e. this wall tile is a shared boundary with some other region, not private ring material.
    private static bool BordersForeignFloor(TileGrid tiles, TileCoord wallCoord, IReadOnlySet<TileCoord> ownFloor)
    {
        foreach (var side in TileSideExtensions.All)
        {
            var far = side.Offset(wallCoord);
            if (tiles.CellAt(far) is { Wall: TileWallKind.None } && !ownFloor.Contains(far))
                return true;
        }
        return false;
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
