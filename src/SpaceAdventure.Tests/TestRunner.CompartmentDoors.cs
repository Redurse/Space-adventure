using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M82 (humble-soaring-cat.md) - restricted door placement on the wall boundary between two
    // already-placed compartments (Game1.ShipEditor.cs's own HandleDoorToolInput/
    // TryResolveCompartmentBoundaryDoor). Same reasoning as TestRunner.CompartmentEditor.cs (M81):
    // Game1's fields/methods are private and simulated input isn't reliable headless, so these tests
    // mirror the real private method's algorithm exactly against a plain TileGrid plus the same
    // TileCoord->instanceId map _editorCompartmentAt keeps, reusing M81's own CompartmentFootprint
    // helper (same partial class) to build it from a Stamp result.

    // Mirrors TryResolveCompartmentBoundaryDoor (Game1.ShipEditor.cs) exactly.
    private static bool TryResolveCompartmentBoundaryDoorTest(TileGrid grid, Dictionary<TileCoord, string> compartmentAt,
        TileCoord coord, out string ownerA, out string ownerB)
    {
        ownerA = "";
        ownerB = "";
        if (grid.CellAt(coord) is not { Wall: TileWallKind.Solid })
            return false;
        if (!compartmentAt.TryGetValue(coord, out var owner))
            return false;

        TileSide? inward = null;
        foreach (var side in TileSideExtensions.All)
        {
            var neighbor = side.Offset(coord);
            if (compartmentAt.TryGetValue(neighbor, out var neighborOwner) && neighborOwner == owner
                && grid.CellAt(neighbor) is { HasFloor: true, Wall: TileWallKind.None })
            {
                if (inward is not null)
                    return false;
                inward = side;
            }
        }
        if (inward is not { } inwardSide)
            return false;

        var outward = inwardSide.Opposite().Offset(coord);
        if (!compartmentAt.TryGetValue(outward, out var outwardOwner) || outwardOwner == owner)
            return false;
        if (grid.CellAt(outward) is not { HasFloor: true, Wall: TileWallKind.None })
            return false;

        ownerA = owner;
        ownerB = outwardOwner;
        return true;
    }

    // Mirrors the rightClicked branch's own per-tile restoration rule exactly.
    private static TileWallKind CompartmentDoorRestoreKindTest(Dictionary<TileCoord, string> compartmentAt, TileCoord tile) =>
        compartmentAt.ContainsKey(tile) ? TileWallKind.Solid : TileWallKind.None;

    // Builds the same TileCoord->instanceId map _editorCompartmentAt keeps, for one or more already-
    // stamped compartments (id -> anchor), reusing M81's own CompartmentFootprint helper.
    private static Dictionary<TileCoord, string> BuildCompartmentAtMap(CompartmentCatalogEntry entry, params (string Id, TileCoord Anchor)[] placements)
    {
        var compartmentAt = new Dictionary<TileCoord, string>();
        foreach (var (id, anchor) in placements)
            foreach (var t in CompartmentFootprint(entry, anchor, 0))
                compartmentAt[t] = id;
        return compartmentAt;
    }

    // ---- Core happy path: two compartments stamped touching share a >=2-tile boundary; a 2-tile span
    // squarely on it must both qualify and resolve to the correct other instance on each side. ----
    private static bool CompartmentDoors_ValidBoundarySpan_ResolvesToCorrectNeighborPair()
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

        var compartmentAt = BuildCompartmentAtMap(entry, ("a", anchorA), ("b", anchorB));

        // A's east ring column x=3 is the surviving wall (B's west column x=4 deduped to plain floor,
        // same as M81's own dedup test) - the two non-corner tiles on it are y=1 and y=2.
        var tile1 = new TileCoord(3, 1);
        var tile2 = new TileCoord(3, 2);

        if (!TryResolveCompartmentBoundaryDoorTest(grid, compartmentAt, tile1, out var ownerA1, out var ownerB1))
            return false;
        if (!TryResolveCompartmentBoundaryDoorTest(grid, compartmentAt, tile2, out var ownerA2, out var ownerB2))
            return false;

        return ownerA1 == "a" && ownerB1 == "b" && ownerA2 == "a" && ownerB2 == "b";
    }

    // ---- The core exclusion rule: a wall tile whose outward side is genuine exterior/vacuum (nothing
    // placed there at all) must be rejected - this is what "neither tile may border open space" means
    // in practice, and it's the highest-value case in this whole milestone. ----
    private static bool CompartmentDoors_WallBorderingOpenSpace_IsRejected()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4
        if (entry is null)
            return false;

        var anchor = new TileCoord(10, 10);
        var result = CompartmentPlacer.Stamp(grid, entry, anchor, rotationSteps: 0, instanceId: "solo");
        if (!result.Success)
            return false;

        var compartmentAt = BuildCompartmentAtMap(entry, ("solo", anchor));

        // East ring column x=13 (anchor.X+3), non-corner tile y=11 (anchor.Y+1): a real Solid wall
        // with the compartment's own interior floor behind it, but nothing at all beyond it - true
        // vacuum, not another compartment.
        var tile = new TileCoord(13, 11);
        if (grid.CellAt(tile) is not { Wall: TileWallKind.Solid })
            return false; // sanity: confirm this really is a standing wall tile before testing rejection
        if (grid.CellAt(new TileCoord(14, 11)) is not null)
            return false; // sanity: confirm the outward tile really is empty vacuum, not floored

        return !TryResolveCompartmentBoundaryDoorTest(grid, compartmentAt, tile, out _, out _);
    }

    // ---- A corner tile of a compartment's own wall ring must be rejected - the wall ring's inset
    // interior floor never actually touches a true rectangle corner (zero matching inward neighbors,
    // not the "straight edge" case's one), so the "exactly one inward neighbor" requirement already
    // covers corners without a separate special case. ----
    private static bool CompartmentDoors_CornerTile_IsRejected()
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

        var compartmentAt = BuildCompartmentAtMap(entry, ("a", anchorA), ("b", anchorB));

        // (3,0) is A's true corner tile (top row AND east column both perimeter) - still Solid.
        var corner = new TileCoord(3, 0);
        if (grid.CellAt(corner) is not { Wall: TileWallKind.Solid })
            return false; // sanity

        return !TryResolveCompartmentBoundaryDoorTest(grid, compartmentAt, corner, out _, out _);
    }

    // ---- Removal restores the correct wall kind: Solid for a door that replaced a compartment's own
    // wall-ring tile (resealing the boundary), None for an ordinary free-tile-painted door with no
    // compartment association at all (today's existing behavior, unchanged). ----
    private static bool CompartmentDoors_Removal_RestoresSolidForCompartmentBoundaryButNoneForOrdinaryDoor()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("life-support-small"); // W=4,H=4
        var anchorA = new TileCoord(0, 0);
        var anchorB = new TileCoord(4, 0);
        if (entry is null)
            return false;
        var resultA = CompartmentPlacer.Stamp(grid, entry, anchorA, rotationSteps: 0, instanceId: "a");
        var resultB = CompartmentPlacer.Stamp(grid, entry, anchorB, rotationSteps: 0, instanceId: "b");
        if (!resultA.Success || !resultB.Success)
            return false;
        var compartmentAt = BuildCompartmentAtMap(entry, ("a", anchorA), ("b", anchorB));

        // Convert a real compartment-boundary wall tile into a door (as HandleDoorToolInput's own
        // wide-door branch would, after a successful TryResolveCompartmentBoundaryDoor).
        var boundaryTile = new TileCoord(3, 1);
        grid.SetWall(boundaryTile, TileWallKind.Door);

        // An ordinary free-tile-painted door, far away, with no compartment involvement at all.
        var ordinaryTile = new TileCoord(50, 50);
        grid.SetFloor(ordinaryTile, true);
        grid.SetWall(ordinaryTile, TileWallKind.Door);

        var boundaryRestored = CompartmentDoorRestoreKindTest(compartmentAt, boundaryTile);
        var ordinaryRestored = CompartmentDoorRestoreKindTest(compartmentAt, ordinaryTile);

        return boundaryRestored == TileWallKind.Solid && ordinaryRestored == TileWallKind.None;
    }
}
