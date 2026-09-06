namespace Anabiosis.Shared.Model;

// M80 (humble-soaring-cat.md) - the pure stamping algorithm that turns one CompartmentCatalogEntry
// into real TileGrid geometry: floor + a wall ring + core/extra devices + engines + (for Docking) an
// airlock door. No UI, no Ship Editor wiring - a later milestone (M81+) calls this from real editor
// input; this file only needs to be correct against a plain TileGrid, which is exactly what
// TestRunner.CompartmentCatalog.cs exercises directly.

// One device that ended up placed on the grid - Coord is its absolute tile, DeviceId is the string
// TileGrid itself tracks (TileCell.DeviceId is an opaque identifier to TileGrid - this is where its
// meaning, kind and instance, actually lives).
public sealed record PlacedDevice(TileCoord Coord, CustomDeviceKind Kind, string DeviceId, bool IsCore);

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
        IReadOnlyList<(TileCoord Position, CustomDeviceKind Kind, bool IsCore, TurretMountSide MountSide)> Devices,
        IReadOnlyList<(TileCoord Control, TileSide Facing, float MaxThrust, EngineRole Role)> Engines,
        (TileSide Side, TileCoord DoorPosition)? Airlock);

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
            .Select(d => (d.RelativePosition, d.Kind, d.IsCore, d.MountSide))
            .ToList();
        var engines = entry.Engines
            .Select(e => (e.RelativeControl, e.Facing, e.MaxThrust, e.Role))
            .ToList();
        var footprintRects = entry.FootprintRects.ToList();

        TileCoord? airlockDoor = entry.Airlock is { } authoredAirlock
            ? RingCenter(authoredAirlock.Side, w, h)
            : null;
        var airlockSide = entry.Airlock?.Side;

        for (var step = 0; step < steps; step++)
        {
            for (var i = 0; i < devices.Count; i++)
                devices[i] = (RotatePointClockwise(devices[i].RelativePosition, h), devices[i].Kind, devices[i].IsCore, devices[i].MountSide);

            for (var i = 0; i < engines.Count; i++)
                engines[i] = (RotatePointClockwise(engines[i].RelativeControl, h), RotateSideClockwise(engines[i].Facing), engines[i].MaxThrust, engines[i].Role);

            if (airlockDoor is { } door)
                airlockDoor = RotatePointClockwise(door, h);
            if (airlockSide is { } side)
                airlockSide = RotateSideClockwise(side);

            for (var i = 0; i < footprintRects.Count; i++)
                footprintRects[i] = RotateRectClockwise(footprintRects[i], h);

            (w, h) = (h, w);
        }

        (TileSide Side, TileCoord DoorPosition)? airlock = airlockSide is { } finalSide && airlockDoor is { } finalDoor
            ? (finalSide, finalDoor)
            : null;

        return new RotatedCompartment(w, h, footprintRects, devices, engines, airlock);
    }

    private static TileCoord RotatePointClockwise(TileCoord point, int heightBeforeRotation) =>
        new(heightBeforeRotation - 1 - point.Y, point.X);

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
        // corrupts the grid. Direct user request ("система отсеков по-другому") - an INTERIOR tile
        // (no ring side at all) must always land on completely empty space, never allowed to
        // overlap another compartment's interior OR its walls. A WALL-RING tile is more permissive:
        // it's allowed to coincide with an EXISTING wall tile (any origin - hand-painted or another
        // compartment's own ring), representing two compartments placed flush/overlapping and
        // sharing that one wall tile - but still rejected if it would land on someone else's open
        // interior floor.
        foreach (var local in footprintTiles)
        {
            var coord = Abs(local);
            var existing = grid.CellAt(coord);
            if (existing is not { HasFloor: true })
                continue; // empty space - always fine

            var isRingTile = IsRingTile(local, footprintTiles);
            if (isRingTile && existing.Wall != TileWallKind.None)
                continue; // wall-over-wall - explicitly allowed, see step 4 below

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
        // always gets its own full wall, no neighbor-based dedup at all anymore. Two compartments
        // placed merely touching (not literally overlapping) end up with a genuine 2-tile-thick
        // double wall at their shared boundary instead of a thinned single tile - a deliberate
        // trade, direct user request, in exchange for never losing a wall just by placing something
        // next to it. The ONLY case a ring tile's own wall doesn't get (re-)stamped is when this
        // EXACT tile already carries one (the wall-over-wall overlap case, step 1 above already
        // allowed it): that existing wall is left completely untouched (material/HP/origin all
        // preserved) rather than replaced - doesn't matter whose wall "wins," there's only ever one
        // physical wall tile there either way.
        var airlockDoorAbs = rotated.Airlock is { } airlockSpec ? Abs(airlockSpec.DoorPosition) : (TileCoord?)null;
        foreach (var local in footprintTiles)
        {
            if (!IsRingTile(local, footprintTiles))
                continue; // interior tile - no wall here at all (includes an internal seam tile
                          // whose every neighbor, orthogonal or diagonal, is part of this same footprint)

            var coord = Abs(local);
            if (grid.CellAt(coord) is { Wall: not TileWallKind.None })
                continue; // already a wall here (the overlap case) - leave it exactly as it was

            var isAirlockDoor = airlockDoorAbs is { } doorCoord && doorCoord == coord;
            grid.SetWall(coord, isAirlockDoor ? TileWallKind.Door : TileWallKind.Solid, fromCompartment: true);
        }

        // 5) Devices.
        var placedDevices = new List<PlacedDevice>();
        var protectedTiles = new List<TileCoord>();
        var deviceIndex = 0;
        foreach (var (position, kind, isCore, _) in rotated.Devices)
        {
            var coord = Abs(position);
            var deviceId = $"{instanceId}-device-{deviceIndex++}";
            grid.PlaceDevice(coord, deviceId);
            placedDevices.Add(new PlacedDevice(coord, kind, deviceId, isCore));
            if (isCore)
                protectedTiles.Add(coord);
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
