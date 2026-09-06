namespace Anabiosis.Shared.Model;

// ShipKind.Destroyer / ShipKind.Freighter (M85 follow-up, humble-soaring-cat.md) - two more starter
// hulls, this time laid out by stamping M80 compartment-catalog entries (CompartmentCatalog.cs/
// CompartmentPlacer.cs) instead of hand-typing Room-rectangle literals the way Scout/Frigate/Cruiser/
// Corvette are. Each compartment's own catalog Width/Height/anchor becomes its CustomRoomDef
// directly (its FULL footprint, wall ring included - Ship.FromCustomDefinition generates the actual
// walls around that boundary itself, exactly like every other hand-authored hull already does), and
// CompartmentPlacer.Stamp is still used to derive every device/engine/airlock position and to
// sanity-check that compartments don't overlap. A parallel, disposable TileGrid is also stamped and
// has its junction walls cut into real Door tiles (LinkBoundaryDoor) purely as a coordinate-level
// assertion that each hand-derived door position genuinely sits on a real Solid boundary wall between
// the two named compartments - a wrong coordinate throws immediately instead of silently producing a
// broken hull. That staging grid is otherwise discarded; the actual CustomDoorDef list is built
// directly from the two rooms' own known ids, not inferred from tile data.
//
// An earlier version of this file routed everything through TileShipBuilder.BuildDefinition (the
// free-tile Ship Editor's own TileGrid -> CustomShipDefinition inference, ported to Shared for exactly
// this purpose) - appropriate for that editor's real job (reconstructing an unknown room shape from
// painted tiles), but a real TEMP-DIAG run against this hull exposed two problems specific to
// directly-typed-in compartment geometry: (1) CompartmentPlacer's own wall-dedup could leave a later
// compartment's OWN CORNER tile floored with no wall at all whenever that corner's OTHER ring side (not
// the one facing the earlier compartment) was genuine exterior - which TileShipBuilder's rectangularity
// check (correctly) rejects as an invalid room shape; this has since been fixed directly in
// CompartmentPlacer.Stamp's own step-4 (require ALL of a corner's ring sides to touch an existing wall
// before deduping it away, not just one - see that method's own doc comment), so it's no longer a
// reason this file needs to avoid TileShipBuilder, but (2) below still is; (2) TileShipBuilder's own
// gap-closing requires the two touching rooms' FULL span on the perpendicular axis to match exactly,
// which never holds between differently-sized catalog compartments (e.g. a 7-tall reactor directly
// against a 5-tall cockpit) even though they share a perfectly good, differently-sized overlap a door
// belongs on - ShipLayoutGeometry.FindRoomPairOverlaps (used by every hand-authored hull already)
// handles that overlap-only case correctly, which is exactly why this file builds rooms/doors directly
// instead.
//
// Both hulls share the same "2D spine + branches" shape (direct user request - "чтобы было много
// отсеков и не в виде линии"): a 6-compartment horizontal spine (engine, cockpit, reactor,
// distribution, a 5th role-specific compartment, docking), with 3 more compartments branching
// perpendicular (above/below) off spine compartments 3/4/5, each attached through its own door.
// Every anchor/door coordinate below was hand-derived against each compartment's own catalog Width/
// Height, then confirmed against a real TEMP-DIAG run (stamp Success + door-tile Solid-before-
// conversion checks, plus the full end-to-end build) before being committed here - see this
// milestone's own report for the coordinates that diagnostic caught and fixed.
public sealed partial class Ship
{
    public static Ship CreateDestroyer()
    {
        var tiles = new TileGrid();
        var rooms = new List<CustomRoomDef>();
        var devices = new List<CustomDeviceDef>();
        var engines = new List<CustomEngineDef>();
        var airlocks = new List<CustomAirlockDef>();

        // ---- Spine. ----
        Stamp(tiles, rooms, devices, engines, airlocks, "engine-medium", new TileCoord(0, 8), "destroyer-engine");
        Stamp(tiles, rooms, devices, engines, airlocks, "cockpit-small", new TileCoord(5, 8), "destroyer-cockpit");
        Stamp(tiles, rooms, devices, engines, airlocks, "reactor-a-centered", new TileCoord(10, 7), "destroyer-reactor");
        Stamp(tiles, rooms, devices, engines, airlocks, "distribution-6", new TileCoord(17, 8), "destroyer-distribution");
        Stamp(tiles, rooms, devices, engines, airlocks, "weapons-2turret", new TileCoord(24, 7), "destroyer-weapons");
        Stamp(tiles, rooms, devices, engines, airlocks, "docking-small", new TileCoord(34, 8), "destroyer-docking");

        // ---- Branches. ----
        Stamp(tiles, rooms, devices, engines, airlocks, "life-support-medium", new TileCoord(11, 14), "destroyer-life-support"); // below reactor
        Stamp(tiles, rooms, devices, engines, airlocks, "medical-small", new TileCoord(18, 4), "destroyer-medical");             // above distribution
        Stamp(tiles, rooms, devices, engines, airlocks, "crew-quarters-medium", new TileCoord(26, 13), "destroyer-crew");        // below weapons

        // ---- Doors - one 2-tile-wide cut per junction (coordinate assertion) plus the matching
        // CustomDoorDef referencing the two compartments' own room ids directly. ----
        var doors = new List<CustomDoorDef>
        {
            LinkBoundaryDoor(tiles, new TileCoord(4, 9), new TileCoord(4, 10), "destroyer-engine", "destroyer-cockpit"),
            LinkBoundaryDoor(tiles, new TileCoord(9, 9), new TileCoord(9, 10), "destroyer-cockpit", "destroyer-reactor"),
            LinkBoundaryDoor(tiles, new TileCoord(16, 9), new TileCoord(16, 10), "destroyer-reactor", "destroyer-distribution"),
            LinkBoundaryDoor(tiles, new TileCoord(23, 9), new TileCoord(23, 10), "destroyer-distribution", "destroyer-weapons"),
            LinkBoundaryDoor(tiles, new TileCoord(33, 9), new TileCoord(33, 10), "destroyer-weapons", "destroyer-docking"),
            LinkBoundaryDoor(tiles, new TileCoord(12, 13), new TileCoord(13, 13), "destroyer-reactor", "destroyer-life-support"),
            LinkBoundaryDoor(tiles, new TileCoord(19, 8), new TileCoord(20, 8), "destroyer-distribution", "destroyer-medical"),
            LinkBoundaryDoor(tiles, new TileCoord(27, 12), new TileCoord(28, 12), "destroyer-weapons", "destroyer-crew"),
        };

        // ---- Extra outfit devices - neither docking-small nor weapons-2turret bakes a SuitLocker/
        // StorageRack, and CustomShipValidator requires at least one of each. Docking-small's full
        // footprint (anchor 34,8, 4x4) is X=34..38,Y=8..12 - (36,9) sits comfortably inside it. An
        // earlier hand-derived draft used (39,9), 2 tiles past the compartment's own east wall
        // entirely; the TEMP-DIAG placement-diagnostic pass caught this (PlaceDevice threw - no floor
        // at (39,9)) and it was corrected to (36,9) here.
        devices.Add(PlaceExtraDevice(tiles, new TileCoord(36, 9), CustomDeviceKind.SuitLocker, "destroyer-suit-locker"));
        devices.Add(PlaceExtraDevice(tiles, new TileCoord(30, 8), CustomDeviceKind.StorageRack, "destroyer-storage-rack"));

        return BuildOrThrow("Эсминец", forwardDegrees: 0f, rooms, doors, airlocks, devices, engines);
    }

    public static Ship CreateFreighter()
    {
        var tiles = new TileGrid();
        var rooms = new List<CustomRoomDef>();
        var devices = new List<CustomDeviceDef>();
        var engines = new List<CustomEngineDef>();
        var airlocks = new List<CustomAirlockDef>();

        // ---- Spine. ----
        Stamp(tiles, rooms, devices, engines, airlocks, "engine-medium", new TileCoord(0, 8), "freighter-engine");
        Stamp(tiles, rooms, devices, engines, airlocks, "cockpit-small", new TileCoord(5, 8), "freighter-cockpit");
        Stamp(tiles, rooms, devices, engines, airlocks, "reactor-b-wide", new TileCoord(10, 8), "freighter-reactor");
        Stamp(tiles, rooms, devices, engines, airlocks, "distribution-6", new TileCoord(18, 8), "freighter-distribution");
        Stamp(tiles, rooms, devices, engines, airlocks, "engineering-medium", new TileCoord(25, 8), "freighter-engineering");
        Stamp(tiles, rooms, devices, engines, airlocks, "docking-medium", new TileCoord(30, 8), "freighter-docking");

        // ---- Branches. ----
        Stamp(tiles, rooms, devices, engines, airlocks, "life-support-medium", new TileCoord(11, 13), "freighter-life-support"); // below reactor
        Stamp(tiles, rooms, devices, engines, airlocks, "crew-quarters-medium", new TileCoord(19, 4), "freighter-crew");         // above distribution
        Stamp(tiles, rooms, devices, engines, airlocks, "medical-small", new TileCoord(25, 12), "freighter-medical");           // below engineering

        // ---- Doors. ----
        var doors = new List<CustomDoorDef>
        {
            LinkBoundaryDoor(tiles, new TileCoord(4, 9), new TileCoord(4, 10), "freighter-engine", "freighter-cockpit"),
            LinkBoundaryDoor(tiles, new TileCoord(9, 9), new TileCoord(9, 10), "freighter-cockpit", "freighter-reactor"),
            LinkBoundaryDoor(tiles, new TileCoord(17, 9), new TileCoord(17, 10), "freighter-reactor", "freighter-distribution"),
            LinkBoundaryDoor(tiles, new TileCoord(24, 9), new TileCoord(24, 10), "freighter-distribution", "freighter-engineering"),
            LinkBoundaryDoor(tiles, new TileCoord(29, 9), new TileCoord(29, 10), "freighter-engineering", "freighter-docking"),
            LinkBoundaryDoor(tiles, new TileCoord(12, 12), new TileCoord(13, 12), "freighter-reactor", "freighter-life-support"),
            LinkBoundaryDoor(tiles, new TileCoord(20, 8), new TileCoord(21, 8), "freighter-distribution", "freighter-crew"),
            LinkBoundaryDoor(tiles, new TileCoord(26, 11), new TileCoord(27, 11), "freighter-engineering", "freighter-medical"),
        };

        // ---- Extra outfit devices - docking-medium's full footprint (anchor 30,8, 5x5) is
        // X=30..35,Y=8..13 - (32,10) sits comfortably inside it. crew-quarters-medium's own baked Bed
        // sits at local (2,1) -> absolute (19+2, 4+1) = (21,5) (anchor 19,4); (20,6) is a different
        // tile of that same compartment's own footprint (X=19..24,Y=4..8), so it doesn't collide.
        devices.Add(PlaceExtraDevice(tiles, new TileCoord(32, 10), CustomDeviceKind.SuitLocker, "freighter-suit-locker"));
        devices.Add(PlaceExtraDevice(tiles, new TileCoord(20, 6), CustomDeviceKind.StorageRack, "freighter-storage-rack"));

        return BuildOrThrow("Транспорт", forwardDegrees: 0f, rooms, doors, airlocks, devices, engines);
    }

    // Stamps one catalog compartment onto the (disposable, validation-only) staging grid, and appends
    // its own contribution to every list the final CustomShipDefinition needs directly: the room IS
    // the compartment's own full catalog footprint (rotationSteps: 0 - neither hull needs a rotated
    // compartment) - exactly how every other hand-authored hull already declares its own Room
    // rectangles, Ship.FromCustomDefinition generates the walls around that boundary itself. Throws
    // loudly rather than silently building a broken hull if the catalog id is unknown or the
    // placement itself fails (a real geometry mistake in the anchors above must be loud, not silently
    // swallowed - direct requirement of this milestone).
    private static void Stamp(TileGrid tiles, List<CustomRoomDef> rooms, List<CustomDeviceDef> devices,
        List<CustomEngineDef> engines, List<CustomAirlockDef> airlocks,
        string compartmentId, TileCoord anchor, string instanceId)
    {
        var entry = CompartmentCatalog.Find(compartmentId)
            ?? throw new InvalidOperationException($"Unknown compartment catalog id '{compartmentId}'.");
        var result = CompartmentPlacer.Stamp(tiles, entry, anchor, rotationSteps: 0, instanceId);
        if (!result.Success)
            throw new InvalidOperationException($"Failed to stamp compartment '{compartmentId}' ({instanceId}) at {anchor}: {result.Error}");

        rooms.Add(new CustomRoomDef(instanceId, entry.DisplayName, anchor.X, anchor.Y, entry.Width, entry.Height));

        // Center of each device's own footprint (footprintSize=4 for Reactor, 1 for everything else -
        // mirrors Game1.ShipEditor.cs's own DeviceFootprintSize/TileShipBuilder's device export
        // convention), not its raw anchor tile - keeps a multi-tile device like the Reactor positioned
        // where CustomDeviceDef's point-containment check (Ship.Custom.cs's RoomIdAt) expects it.
        foreach (var device in result.Devices)
        {
            var footprint = device.Kind == CustomDeviceKind.Reactor ? 4 : 1;
            devices.Add(new CustomDeviceDef(device.Kind, device.Coord.X + footprint / 2f, device.Coord.Y + footprint / 2f));
        }
        foreach (var engine in result.Engines)
            engines.Add(new CustomEngineDef(engine.ControlCoord.X + 0.5f, engine.ControlCoord.Y + 0.5f, engine.Facing, engine.MaxThrust));
        if (result.Airlock is { } airlock)
            airlocks.Add(new CustomAirlockDef(instanceId, ToEdgeSide(airlock.Side)));
    }

    private static EdgeSide ToEdgeSide(TileSide side) => side switch
    {
        TileSide.North => EdgeSide.Top,
        TileSide.South => EdgeSide.Bottom,
        TileSide.East => EdgeSide.Right,
        TileSide.West => EdgeSide.Left,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    // Cuts a 2-tile-wide door into the single surviving Solid boundary wall between two already-
    // stamped compartments (CompartmentPlacer's own dedup leaves exactly one wall tile per boundary
    // row/column - see this file's own doc comment) and links the pair into one wide door
    // (TileCell.DoorGroupId's own doc comment - the same "дверь на 1x2 тайла" convention the free-
    // tile editor uses). This is a coordinate-level ASSERTION only (both tiles must be genuinely
    // Solid immediately before conversion, so a wrong hand-derived coordinate fails loudly here
    // instead of silently producing a broken hull) - the returned CustomDoorDef, built directly from
    // the two rooms' own known ids, is what actually reaches the final ship.
    private static CustomDoorDef LinkBoundaryDoor(TileGrid tiles, TileCoord a, TileCoord b, string roomAId, string roomBId)
    {
        if (tiles.CellAt(a) is not { Wall: TileWallKind.Solid })
            throw new InvalidOperationException($"Expected a solid compartment-boundary wall at {a} before cutting a door there.");
        if (tiles.CellAt(b) is not { Wall: TileWallKind.Solid })
            throw new InvalidOperationException($"Expected a solid compartment-boundary wall at {b} before cutting a door there.");
        tiles.SetWall(a, TileWallKind.Door);
        tiles.SetWall(b, TileWallKind.Door);
        tiles.LinkDoors(a, b);
        return new CustomDoorDef(roomAId, roomBId);
    }

    // Places a device the compartment catalog doesn't bake in on its own (neither docking nor weapons
    // compartments include a SuitLocker/StorageRack, both required by CustomShipValidator) onto the
    // staging grid (so the placement is at least checked for a real floor tile there) and returns the
    // matching CustomDeviceDef, tile-centered like every other device above.
    private static CustomDeviceDef PlaceExtraDevice(TileGrid tiles, TileCoord coord, CustomDeviceKind kind, string deviceId)
    {
        tiles.PlaceDevice(coord, deviceId);
        return new CustomDeviceDef(kind, coord.X + 0.5f, coord.Y + 0.5f);
    }

    private static Ship BuildOrThrow(string shipName, float forwardDegrees, List<CustomRoomDef> rooms,
        List<CustomDoorDef> doors, List<CustomAirlockDef> airlocks, List<CustomDeviceDef> devices, List<CustomEngineDef> engines)
    {
        var definition = new CustomShipDefinition(shipName, rooms, doors, airlocks, devices, forwardDegrees, EnginesRaw: engines);
        var errors = CustomShipValidator.Validate(definition);
        if (errors.Count > 0)
            throw new InvalidOperationException($"Hand-authored hull '{shipName}' failed validation: {string.Join("; ", errors)}");
        return FromCustomDefinition(definition);
    }
}
