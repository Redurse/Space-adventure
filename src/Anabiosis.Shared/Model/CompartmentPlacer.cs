namespace Anabiosis.Shared.Model;

// M80 (humble-soaring-cat.md) - the pure stamping algorithm that turns one CompartmentCatalogEntry
// into real TileGrid geometry: floor + a wall ring + core/extra devices + engines + (for Docking) an
// airlock door. No UI, no Ship Editor wiring - a later milestone (M81+) calls this from real editor
// input; this file only needs to be correct against a plain TileGrid, which is exactly what
// TestRunner.CompartmentCatalog.cs exercises directly.

// One device that ended up placed on the grid - Coord is its absolute footprint anchor (top-left,
// same convention the free-tile editor's own _editorDeviceFootprint uses - NOT necessarily the only
// tile it occupies, see CustomDeviceFootprint.Size), DeviceId is the string TileGrid itself tracks
// (TileCell.DeviceId is an opaque identifier to TileGrid - this is where its meaning, kind and
// instance, actually lives). Rotated (M91 follow-up, Helm/Navigation's own 3x2 footprint) is
// whether this specific placed instance's Width/Height ended up swapped from the catalog's own
// authored orientation.
public sealed record PlacedDevice(TileCoord Coord, CustomDeviceKind Kind, string DeviceId, bool IsCore, bool Rotated = false);

// One engine assembly that ended up placed - TileGrid has no ShipEngine concept of its own (Control
// is plain floor, Bulkhead is just a wall-ring tile, Nozzle is untouched open space), so this is the
// only place that Facing/MaxThrust/Role/instance-id actually live once the stamp is done.
public sealed record PlacedEngine(TileCoord ControlCoord, TileSide Facing, float MaxThrust, EngineRole Role, string EngineId);

// The Docking compartment's own airlock door tile, absolute.
public sealed record PlacedAirlock(TileCoord DoorCoord, TileSide Side);

// The result of one Stamp call. ProtectedTiles is every tile a later milestone's outfit-mode UI must
// refuse to let the player remove once placed (M80 itself never enforces that refusal - see
// CompartmentCatalog.cs's own doc comment on IsCore) - the core device tile(s), the whole engine
// assembly's 3 tiles (Control/Bulkhead/Nozzle - breaking any one of them cripples the engine), and
// the airlock door tile, if this compartment has one.
public sealed record CompartmentPlacementResult(
    bool Success,
    string? Error,
    IReadOnlyList<PlacedDevice> Devices,
    IReadOnlyList<PlacedEngine> Engines,
    PlacedAirlock? Airlock,
    IReadOnlyList<TileCoord> ProtectedTiles)
{
    public static CompartmentPlacementResult Fail(string error) =>
        new(false, error, Array.Empty<PlacedDevice>(), Array.Empty<PlacedEngine>(), null, Array.Empty<TileCoord>());
}

public static class CompartmentPlacer
{
    // A compartment's template, rotated by 0-3 steps of +90 clockwise, but not yet translated onto
    // an anchor - kept as its own pure, testable step (TestRunner.CompartmentCatalog.cs's own
    // rotation-transform test) separate from Stamp's TileGrid mutation. FootprintRects (M91,
    // humble-soaring-cat.md non-rectangular compartments) is the rotated union of the entry's own
    // pieces - a plain single-rect entry rotates to a single rotated rect, same as before.
    public readonly record struct RotatedCompartment(
        int Width,
        int Height,
        IReadOnlyList<RectF> FootprintRects,
        IReadOnlyList<(TileCoord Position, CustomDeviceKind Kind, bool IsCore, TurretMountSide MountSide, bool Rotated)> Devices,
        IReadOnlyList<(TileCoord Control, TileSide Facing, float MaxThrust, EngineRole Role)> Engines,
        (TileSide Side, TileCoord DoorPosition)? Airlock,
        IReadOnlyList<(TileCoord Position, TileSide Side)> WallOpenSides);

    // Rotates a whole catalog entry (authored at 0 deg) by rotationSteps * 90 deg clockwise. Screen
    // convention (Y grows downward, same as everywhere else in this project - see TileSideExtensions.
    // Offset's own doc comment): rotating 90 deg clockwise maps a local point (x,y) in a W x H box to
    // (H-1-y, x) in the resulting H x W box - verified by hand for 2 of the 4 steps in the test file,
    // and by the fact that 4 steps composed always returns the exact original point and dimensions.
    public static RotatedCompartment Rotate(CompartmentCatalogEntry entry, int rotationSteps)
    {
        var steps = ((rotationSteps % 4) + 4) % 4;
        var w = entry.Width;
        var h = entry.Height;

        var devices = entry.Devices
            .Select(d => (d.RelativePosition, d.Kind, d.IsCore, d.MountSide, d.Rotated))
            .ToList();
        var engines = entry.Engines
            .Select(e => (e.RelativeControl, e.Facing, e.MaxThrust, e.Role))
            .ToList();
        var footprintRects = entry.FootprintRects.ToList();
        var wallOpenSides = entry.WallOpenSides
            .Select(o => (o.RelativePosition, o.Side))
            .ToList();

        TileCoord? airlockDoor = entry.Airlock is { } authoredAirlock
            ? RingCenter(authoredAirlock.Side, w, h)
            : null;
        var airlockSide = entry.Airlock?.Side;

        for (var step = 0; step < steps; step++)
        {
            // Box-aware, not point-aware: a device's own anchor is its footprint's TOP-LEFT corner,
            // not a dimensionless point - rotating just the anchor via the plain point formula
            // (correct only for a 1x1 device, where "anchor" and "the one tile it occupies" are the
            // same thing) silently misplaced any bigger square device too (the Reactor) whenever a
            // compartment carrying one got rotated, a pre-existing gap nobody had hit before Helm/
            // Navigation's own new 3x2 footprint made it impossible to ignore. RotateDeviceAnchorClockwise
            // below is the same corner-rotation reasoning RotateRectClockwise already uses for
            // FootprintRects, just in discrete tile-index space. A device's own effective rotation
            // flips every step (rotating the whole compartment 90 degrees rotates everything baked
            // into it 90 degrees too), tracked per-device since two devices could have started with
            // different Rotated flags.
            for (var i = 0; i < devices.Count; i++)
            {
                var (baseWidth, baseHeight) = CustomDeviceFootprint.Size(devices[i].Kind);
                var (curWidth, curHeight) = devices[i].Rotated ? (baseHeight, baseWidth) : (baseWidth, baseHeight);
                _ = curWidth; // only curHeight is needed for the anchor formula - kept for symmetry/clarity
                var newPosition = RotateDeviceAnchorClockwise(devices[i].RelativePosition, curHeight, h);
                devices[i] = (newPosition, devices[i].Kind, devices[i].IsCore, devices[i].MountSide, !devices[i].Rotated);
            }

            for (var i = 0; i < engines.Count; i++)
                engines[i] = (RotatePointClockwise(engines[i].RelativeControl, h), RotateSideClockwise(engines[i].Facing), engines[i].MaxThrust, engines[i].Role);

            if (airlockDoor is { } door)
                airlockDoor = RotatePointClockwise(door, h);
            if (airlockSide is { } side)
                airlockSide = RotateSideClockwise(side);

            for (var i = 0; i < footprintRects.Count; i++)
                footprintRects[i] = RotateRectClockwise(footprintRects[i], h);

            // A wall-ring tile's own position is a plain point (not a footprint anchor - a half-
            // block wall is always exactly one tile), same RotatePointClockwise formula the airlock
            // door/engine control positions above already use; its solid Side rotates the same way
            // Engine.Facing/the airlock's own Side do.
            for (var i = 0; i < wallOpenSides.Count; i++)
                wallOpenSides[i] = (RotatePointClockwise(wallOpenSides[i].RelativePosition, h), RotateSideClockwise(wallOpenSides[i].Side));

            (w, h) = (h, w);
        }

        (TileSide Side, TileCoord DoorPosition)? airlock = airlockSide is { } finalSide && airlockDoor is { } finalDoor
            ? (finalSide, finalDoor)
            : null;

        return new RotatedCompartment(w, h, footprintRects, devices, engines, airlock, wallOpenSides);
    }

    private static TileCoord RotatePointClockwise(TileCoord point, int heightBeforeRotation) =>
        new(heightBeforeRotation - 1 - point.Y, point.X);

    // Rotates a device's own footprint anchor (top-left corner of an ownHeight-tall box, not a bare
    // point) 90 degrees clockwise - equivalent to RotatePointClockwise when ownHeight == 1 (a 1x1
    // device's anchor IS the one tile it occupies, so the two formulas agree exactly), but correctly
    // accounts for a taller box's own far corner otherwise. Derived the same way RotateRectClockwise
    // is: the box's own bottom-right corner (anchor.Y + ownHeight - 1) maps through the ordinary
    // point rotation to become the rotated box's new LEFT edge.
    private static TileCoord RotateDeviceAnchorClockwise(TileCoord anchor, int ownHeight, int heightBeforeRotation) =>
        new(heightBeforeRotation - anchor.Y - ownHeight, anchor.X);

    // Continuous-coordinate counterpart of RotatePointClockwise above (no "-1": a RectF's own X/Y is
    // a boundary VALUE, not a discrete tile index, so a point (x,y) in a WxH box maps to (H-y,x) in
    // the resulting HxW box with no off-by-one adjustment). A rect's two opposite corners both map
    // under that same rule; taking the new min corner and swapping Width/Height reproduces the
    // rotated rect. Verified by hand: a rect spanning the WHOLE box (0,0,W,H) maps to (0,0,H,W) -
    // the entire new box, exactly as rotating "everything" should.
    private static RectF RotateRectClockwise(RectF rect, float heightBeforeRotation) =>
        new(heightBeforeRotation - rect.Y - rect.Height, rect.X, rect.Height, rect.Width);

    private static TileSide RotateSideClockwise(TileSide side) => side switch
    {
        TileSide.North => TileSide.East,
        TileSide.East => TileSide.South,
        TileSide.South => TileSide.West,
        TileSide.West => TileSide.North,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    // Where a compartment's airlock door sits, centered on the given side of its own (unrotated) W x
    // H wall ring - matches Game1.ShipEditor.TileBridge.cs's own SideIsAirlock/CloseGapIfAdjacent
    // convention (a Door tile on an otherwise-clean exterior side).
    private static TileCoord RingCenter(TileSide side, int w, int h) => side switch
    {
        TileSide.North => new TileCoord(w / 2, 0),
        TileSide.South => new TileCoord(w / 2, h - 1),
        TileSide.West => new TileCoord(0, h / 2),
        TileSide.East => new TileCoord(w - 1, h / 2),
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    private static TileCoord Offset(TileCoord origin, TileSide side, int steps) => side switch
    {
        TileSide.North => origin with { Y = origin.Y - steps },
        TileSide.South => origin with { Y = origin.Y + steps },
        TileSide.East => origin with { X = origin.X + steps },
        TileSide.West => origin with { X = origin.X - steps },
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    // Whether a LOCAL tile sits on the compartment's own outer ring - true whenever ANY of its 8
    // surrounding tiles (the 4 orthogonal neighbors AND the 4 diagonal ones) is NOT part of this
    // footprint. Direct user request ("стены не обрезались в местах клетки которых граничат с
    // космосом по диагонали") - a notched-corner shape like reactor-d has tiles whose only exposure
    // to the void is diagonal (all 4 orthogonal neighbors are genuine floor, but a diagonal neighbor
    // isn't) - checking orthogonal sides alone missed these, leaving a single-tile gap in the wall
    // ring at every reentrant corner that a character could see or clip through. A tile whose
    // diagonal neighbor is part of THIS SAME footprint (an internal seam/corner between two of its
    // own pieces, or the interior of a single rectangular piece) still correctly counts as interior.
    private static bool IsRingTile(TileCoord local, HashSet<TileCoord> footprintTiles)
    {
        for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;
                if (!footprintTiles.Contains(new TileCoord(local.X + dx, local.Y + dy)))
                    return true;
            }
        return false;
    }

    private static HashSet<TileCoord> FootprintTiles(IReadOnlyList<RectF> footprintRects)
    {
        var tiles = new HashSet<TileCoord>();
        foreach (var rect in footprintRects)
            for (var x = (int)rect.X; x < (int)rect.Right; x++)
                for (var y = (int)rect.Y; y < (int)rect.Bottom; y++)
                    tiles.Add(new TileCoord(x, y));
        return tiles;
    }

    // Stamps `entry` (rotated by rotationSteps * 90 deg clockwise) onto `grid`, anchored so the
    // rotated footprint's own local (0,0) lands at `anchor`. `instanceId` seeds every device/engine id
    // this stamp creates (must be unique per placed compartment - the caller's responsibility, same
    // as every other id-generating call in this codebase). Reject cleanly (Success=false, grid left
    // untouched) rather than throwing or partially stamping, mirroring the free-tile editor's own
    // "reject cleanly, don't corrupt" convention (Game1.ShipEditor.TileBridge.cs).
    public static CompartmentPlacementResult Stamp(TileGrid grid, CompartmentCatalogEntry entry, TileCoord anchor, int rotationSteps, string instanceId)
    {
        var rotated = Rotate(entry, rotationSteps);
        var w = rotated.Width;
        var h = rotated.Height;
        var footprintTiles = FootprintTiles(rotated.FootprintRects);

        TileCoord Abs(TileCoord local) => new(anchor.X + local.X, anchor.Y + local.Y);

        // 1) Footprint overlap check. Checked BEFORE any mutation so a rejected placement never
        // corrupts the grid. Direct user request ("убери механику чтобы при накладывании отсеков...
        // они могли наезжать друг на друга... сделай это невозможным") - EVERY footprint tile, wall-
        // ring or interior alike, must land on completely empty space. This replaces an earlier,
        // more permissive rule ("система отсеков по-другому") that let a wall-ring tile coincide
        // with an EXISTING wall tile (from any origin), representing two compartments placed flush/
        // overlapping and sharing that one tile - the player found that let compartments visibly
        // overlap each other, which is exactly what this tightens back up. A cell only ever exists
        // in TileGrid.Cells once it has a floor (TileGrid.SetFloor's own doc comment), so "does a
        // cell exist here at all" is already the exact "is this space occupied by anything" test.
        foreach (var local in footprintTiles)
        {
            var coord = Abs(local);
            if (grid.CellAt(coord) is not null)
                return CompartmentPlacementResult.Fail($"Cannot place '{entry.DisplayName}' at {coord} - already occupied.");
        }

        // 2) Every engine's Nozzle lands outside the footprint by design (CompartmentCatalog.cs's own
        // worked-out layouts) - but it still needs to be genuine open space at PLACEMENT time, not
        // already floored by some other compartment sitting just past this one's own wall ring.
        foreach (var (control, facing, _, _) in rotated.Engines)
        {
            var nozzle = Abs(Offset(control, facing, 2));
            if (grid.CellAt(nozzle) is { HasFloor: true })
                return CompartmentPlacementResult.Fail($"Cannot place '{entry.DisplayName}' - engine nozzle at {nozzle} would open into occupied floor.");
        }

        // 3) Stamp the floor for the whole footprint.
        foreach (var local in footprintTiles)
            grid.SetFloor(Abs(local), true);

        // 4) Stamp the wall ring. Direct user request ("стены не удалялись" - placing a compartment
        // next to another must never silently thin/remove either one's wall) - every ring tile
        // always gets its own full wall. Two compartments placed merely touching (not overlapping)
        // end up with a genuine 2-tile-thick double wall at their shared boundary instead of a
        // thinned single tile - a deliberate trade, direct user request, in exchange for never
        // losing a wall just by placing something next to it. Step 1 above already guarantees every
        // tile here is on completely empty ground (overlap is no longer possible at all), so unlike
        // before there's no "already a wall here" case left to check for.
        //
        // Direct user request ("удали механику что если ставим стены в ряд, они почти все
        // превращаются в полублоки... хочу сделать чтобы игрок сам выбирал") - a compartment's own
        // wall ring stamps full-thickness walls only now; half-block is a deliberate choice made
        // with the free-tile editor's own Wall tool afterward (Game1.ShipEditor.cs), never inferred
        // automatically from footprint shape here.
        var airlockDoorAbs = rotated.Airlock is { } airlockSpec ? Abs(airlockSpec.DoorPosition) : (TileCoord?)null;
        foreach (var local in footprintTiles)
        {
            if (!IsRingTile(local, footprintTiles))
                continue; // interior tile - no wall here at all (includes an internal seam tile
                          // whose every neighbor, orthogonal or diagonal, is part of this same footprint)

            var coord = Abs(local);
            var isAirlockDoor = airlockDoorAbs is { } doorCoord && doorCoord == coord;
            grid.SetWall(coord, isAirlockDoor ? TileWallKind.Door : TileWallKind.Solid, fromCompartment: true);
        }

        // 4.5) Half-block overrides - direct user bug report ("почему... в нём отсутствуют
        // полублоки стены, хотя в исходнике они есть?"). Applied strictly after the full-thickness
        // ring above, on top of it, same "paint the base geometry, patch in per-tile detail
        // afterward" shape TileShipBuilder.cs's own WallMaterials/WallOpenSides overrides already
        // use. SetWallOpenSide's own guard (TileGrid.cs) already no-ops for anything that isn't an
        // intact Solid, non-corner tile, so an authored override that (after rotation) happens to
        // land on a corner or the airlock door tile is silently ignored rather than corrupting it.
        foreach (var (position, side) in rotated.WallOpenSides)
            grid.SetWallOpenSide(Abs(position), side);

        // 5) Devices - every tile of the device's own REAL footprint (CustomDeviceFootprint.Size,
        // swapped when this instance is Rotated) gets the SAME deviceId, not just its anchor tile -
        // a multi-tile device (Reactor, or Helm/Navigation's own new 3x2) must actually occupy and
        // block every tile it visually covers, the same way the free-tile editor's own
        // DeviceFootprintTiles already does for a hand-placed one.
        var placedDevices = new List<PlacedDevice>();
        var protectedTiles = new List<TileCoord>();
        var deviceIndex = 0;
        foreach (var (position, kind, isCore, _, deviceRotated) in rotated.Devices)
        {
            var deviceAnchor = Abs(position);
            var deviceId = $"{instanceId}-device-{deviceIndex++}";
            var (baseWidth, baseHeight) = CustomDeviceFootprint.Size(kind);
            var (deviceWidth, deviceHeight) = deviceRotated ? (baseHeight, baseWidth) : (baseWidth, baseHeight);
            for (var dx = 0; dx < deviceWidth; dx++)
                for (var dy = 0; dy < deviceHeight; dy++)
                    grid.PlaceDevice(new TileCoord(deviceAnchor.X + dx, deviceAnchor.Y + dy), deviceId);
            placedDevices.Add(new PlacedDevice(deviceAnchor, kind, deviceId, isCore, deviceRotated));
            if (isCore)
                protectedTiles.Add(deviceAnchor);
        }

        // 6) Engines - Control stays plain, un-flagged open floor (ShipEngine.cs's own doc comment:
        // "ordinary interior floor"), never a TileGrid device; Bulkhead already landed on the wall
        // ring in step 4 above. All 3 tiles of the assembly are protected - breaking any one of them
        // cripples the whole engine (Control freezes the throttle, Bulkhead/Nozzle both kill thrust).
        var placedEngines = new List<PlacedEngine>();
        var engineIndex = 0;
        foreach (var (control, facing, maxThrust, role) in rotated.Engines)
        {
            var controlCoord = Abs(control);
            var bulkheadCoord = Abs(Offset(control, facing, 1));
            var nozzleCoord = Abs(Offset(control, facing, 2));
            var engineId = $"{instanceId}-engine-{engineIndex++}";
            placedEngines.Add(new PlacedEngine(controlCoord, facing, maxThrust, role, engineId));
            protectedTiles.Add(controlCoord);
            protectedTiles.Add(bulkheadCoord);
            protectedTiles.Add(nozzleCoord);
        }

        // 7) Airlock.
        PlacedAirlock? placedAirlock = null;
        if (rotated.Airlock is { } finalAirlock && airlockDoorAbs is { } finalDoorAbs)
        {
            placedAirlock = new PlacedAirlock(finalDoorAbs, finalAirlock.Side);
            protectedTiles.Add(finalDoorAbs);
        }

        return new CompartmentPlacementResult(true, null, placedDevices, placedEngines, placedAirlock, protectedTiles);
    }
}
