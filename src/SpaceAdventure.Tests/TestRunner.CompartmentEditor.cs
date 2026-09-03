using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M81 (humble-soaring-cat.md) - wires M80's CompartmentCatalog/CompartmentPlacer into the free-
    // tile Ship Editor's own private HandleCompartmentToolInput/RemoveCompartmentAt
    // (Game1.ShipEditor.cs). Game1's Ship Editor fields are private and simulated mouse/keyboard input
    // isn't reliable in this headless test environment (see this project's own established lesson),
    // so these tests exercise the SAME underlying CompartmentPlacer/TileGrid layer those private
    // methods are built from - proving the claims those methods rely on, without needing Game1 at all.

    // ---- Sanity re-check of M80's own wall-dedup claim from this milestone's own perspective: two
    // compartments stamped directly touching must end up with exactly one wall tile between them. This
    // doesn't re-test anything new about CompartmentPlacer - it confirms nothing about M81's own design
    // assumptions (in particular, RemoveCompartmentAt's wall-repair logic below) contradicts it. ----
    private static bool CompartmentEditor_TouchingCompartments_StillDedupToExactlyOneWallTile()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4
        if (entry is null)
            return false;

        var resultA = CompartmentPlacer.Stamp(grid, entry, new TileCoord(0, 0), rotationSteps: 0, instanceId: "a");
        var resultB = CompartmentPlacer.Stamp(grid, entry, new TileCoord(4, 0), rotationSteps: 0, instanceId: "b");
        if (!resultA.Success || !resultB.Success)
            return false;

        var wallTileCount = 0;
        for (var x = 1; x <= 6; x++)
            if (grid.CellAt(new TileCoord(x, 1)) is { Wall: not TileWallKind.None })
                wallTileCount++;
        return wallTileCount == 1;
    }

    // Mirrors HandleCompartmentToolInput's own footprint bookkeeping - every local (x,y) in
    // [0,Width) x [0,Height) of the rotated entry, offset by anchor. A small local helper (not a call
    // into Game1, which is private) so the removal-repair test below can build the same
    // "instance -> full tile set" map the real private field _editorCompartmentTiles keeps.
    private static HashSet<TileCoord> CompartmentFootprint(CompartmentCatalogEntry entry, TileCoord anchor, int rotationSteps)
    {
        var rotated = CompartmentPlacer.Rotate(entry, rotationSteps);
        var tiles = new HashSet<TileCoord>();
        for (var y = 0; y < rotated.Height; y++)
            for (var x = 0; x < rotated.Width; x++)
                tiles.Add(new TileCoord(anchor.X + x, anchor.Y + y));
        return tiles;
    }

    // Mirrors RemoveCompartmentAt's own wall-repair-then-clear algorithm exactly (Game1.ShipEditor.cs)
    // - operates directly on a plain TileGrid plus the same TileCoord->instanceId map
    // _editorCompartmentAt keeps, since the real method lives on the private Game1 class this test
    // file can't call into.
    private static void RemoveCompartmentWithRepair(TileGrid grid, Dictionary<TileCoord, string> compartmentAt,
        Dictionary<string, HashSet<TileCoord>> compartmentTiles, string instanceId)
    {
        if (!compartmentTiles.TryGetValue(instanceId, out var tiles))
            return;

        foreach (var coord in tiles)
        {
            if (grid.CellAt(coord) is not { Wall: TileWallKind.Solid })
                continue;
            foreach (var side in TileSideExtensions.All)
            {
                var outward = side.Offset(coord);
                if (!compartmentAt.TryGetValue(outward, out var neighborInstance) || neighborInstance == instanceId)
                    continue;
                if (grid.CellAt(outward) is { Wall: TileWallKind.None, HasFloor: true })
                    grid.SetWall(outward, TileWallKind.Solid);
            }
        }

        foreach (var coord in tiles)
        {
            compartmentAt.Remove(coord);
            if (grid.CellAt(coord) is { DeviceId: not null })
                grid.RemoveDevice(coord);
            grid.SetFloor(coord, false);
        }
        compartmentTiles.Remove(instanceId);
    }

    // ---- The core removal claim: stamp two touching compartments (A placed first, B placed second -
    // per M80's own dedup rule, B's own boundary ring is the one that got deduped away to plain floor,
    // while A's own east ring column is the surviving 1-tile separator), then remove A (the one whose
    // wall is actually still standing on the shared boundary) via the repair-then-clear algorithm
    // above. B, still standing, must end up with its own Solid wall restored on that boundary - not a
    // hole where A used to be. ----
    private static bool CompartmentEditor_RemovingCompartment_RepairsNeighborsDedupedWall()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4
        if (entry is null)
            return false;

        var anchorA = new TileCoord(0, 0);
        var anchorB = new TileCoord(4, 0);
        var resultA = CompartmentPlacer.Stamp(grid, entry, anchorA, rotationSteps: 0, instanceId: "a");
        var resultB = CompartmentPlacer.Stamp(grid, entry, anchorB, rotationSteps: 0, instanceId: "b");
        if (!resultA.Success || !resultB.Success)
            return false;

        // Confirm the dedup landed exactly where M80's own design says it does before removing
        // anything: A's east ring column (x=3) still Solid, B's west ring column (x=4) deduped to
        // plain floor (HasFloor true, Wall None).
        for (var y = 0; y < 4; y++)
        {
            if (grid.CellAt(new TileCoord(3, y)) is not { Wall: TileWallKind.Solid })
                return false;
            if (grid.CellAt(new TileCoord(4, y)) is not { HasFloor: true, Wall: TileWallKind.None })
                return false;
        }

        var compartmentAt = new Dictionary<TileCoord, string>();
        var compartmentTiles = new Dictionary<string, HashSet<TileCoord>>
        {
            ["a"] = CompartmentFootprint(entry, anchorA, 0),
            ["b"] = CompartmentFootprint(entry, anchorB, 0),
        };
        foreach (var (instance, tiles) in compartmentTiles)
            foreach (var t in tiles)
                compartmentAt[t] = instance;

        RemoveCompartmentWithRepair(grid, compartmentAt, compartmentTiles, "a");

        // A's own tiles are gone entirely (checked by the next test more thoroughly); the real claim
        // here is that B's west ring column (the boundary that used to be deduped against A) now
        // carries its own freshly-restored Solid wall, not a hole into vacated space.
        for (var y = 0; y < 4; y++)
            if (grid.CellAt(new TileCoord(4, y)) is not { Wall: TileWallKind.Solid })
                return false;

        // And B's own interior/region topology is still sound - it didn't silently merge with the now-
        // empty space where A used to be.
        var regionB = grid.RegionIdAt(new TileCoord(5, 1));
        return regionB is not null;
    }

    // ---- Removing a compartment clears every tile of its own footprint back to HasFloor: false. ----
    private static bool CompartmentEditor_RemovingCompartment_ClearsEveryFootprintTileToNoFloor()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small");
        if (entry is null)
            return false;

        var anchor = new TileCoord(10, 10);
        var result = CompartmentPlacer.Stamp(grid, entry, anchor, rotationSteps: 0, instanceId: "solo");
        if (!result.Success)
            return false;

        var compartmentAt = new Dictionary<TileCoord, string>();
        var footprint = CompartmentFootprint(entry, anchor, 0);
        var compartmentTiles = new Dictionary<string, HashSet<TileCoord>> { ["solo"] = footprint };
        foreach (var t in footprint)
            compartmentAt[t] = "solo";

        RemoveCompartmentWithRepair(grid, compartmentAt, compartmentTiles, "solo");

        foreach (var t in footprint)
            if (grid.CellAt(t) is { HasFloor: true })
                return false; // TileGrid.SetFloor(coord, false) removes the cell entirely - CellAt
                               // should come back null, but this also catches a hypothetical future
                               // change that instead left a stale HasFloor:true cell behind.

        return !compartmentTiles.ContainsKey("solo") && compartmentAt.Count == 0;
    }
}
