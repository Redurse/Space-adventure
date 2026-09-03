namespace SpaceAdventure.Shared.Model;

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
    // rotation-transform test) separate from Stamp's TileGrid mutation.
    public readonly record struct RotatedCompartment(
        int Width,
        int Height,
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

            (w, h) = (h, w);
        }

        (TileSide Side, TileCoord DoorPosition)? airlock = airlockSide is { } finalSide && airlockDoor is { } finalDoor
            ? (finalSide, finalDoor)
            : null;

        return new RotatedCompartment(w, h, devices, engines, airlock);
    }

    private static TileCoord RotatePointClockwise(TileCoord point, int heightBeforeRotation) =>
        new(heightBeforeRotation - 1 - point.Y, point.X);

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

    // Which cardinal direction(s) a LOCAL tile sits on the compartment's own outer ring - a
    // non-corner edge tile has exactly one, a corner tile has two (both edges it belongs to), an
    // interior tile has none at all.
    private static List<TileSide> RingSides(TileCoord local, int w, int h)
    {
        var sides = new List<TileSide>(2);
        if (local.Y == 0) sides.Add(TileSide.North);
        if (local.Y == h - 1) sides.Add(TileSide.South);
        if (local.X == 0) sides.Add(TileSide.West);
        if (local.X == w - 1) sides.Add(TileSide.East);
        return sides;
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

        TileCoord Abs(TileCoord local) => new(anchor.X + local.X, anchor.Y + local.Y);

        // 1) Footprint overlap check - every tile this compartment would floor must currently be
        // empty (no floor at all yet). Checked BEFORE any mutation so a rejected placement never
        // corrupts the grid.
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var coord = Abs(new TileCoord(x, y));
                if (grid.CellAt(coord) is { HasFloor: true })
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
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                grid.SetFloor(Abs(new TileCoord(x, y)), true);

        // 4) Stamp the wall ring, with placement-time dedup: a new ring tile whose immediate outward
        // neighbor is ALREADY a wall (necessarily from an earlier, different compartment - our own
        // footprint never reaches past its own ring) becomes plain floor instead of a second wall,
        // leaving the existing neighbor's own wall as the sole 1-tile separator between the two
        // compartments' interiors (see this file's own doc comment / humble-soaring-cat.md's M80
        // plan). The existing compartment's wall is NEVER touched - only ever the new one's.
        var airlockDoorAbs = rotated.Airlock is { } airlockSpec ? Abs(airlockSpec.DoorPosition) : (TileCoord?)null;
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var local = new TileCoord(x, y);
                var ringSides = RingSides(local, w, h);
                if (ringSides.Count == 0)
                    continue; // interior tile - no wall here at all

                var coord = Abs(local);
                var touchesAnExistingWall = ringSides.Exists(side => grid.CellAt(side.Offset(coord)) is { HasFloor: true, Wall: not TileWallKind.None });
                if (touchesAnExistingWall)
                {
                    grid.SetWall(coord, TileWallKind.None); // dedup - the neighbor's wall is the shared boundary
                    continue;
                }

                var isAirlockDoor = airlockDoorAbs is { } doorCoord && doorCoord == coord;
                grid.SetWall(coord, isAirlockDoor ? TileWallKind.Door : TileWallKind.Solid);
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
