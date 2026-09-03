using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M83 (humble-soaring-cat.md) - enforces the user's own long-standing design rule ("игрок может
    // поставить устройство на пустых тайлах в отсеке, но самая важная часть отсека никак нельзя
    // снести") against SINGLE-tile removal tools (Device/Engine/Door), on top of M80's ProtectedTiles
    // and M81's own _editorCompartmentAt/_editorCompartmentProtected bookkeeping. Same reasoning as
    // M81/M82's own test files (TestRunner.CompartmentEditor.cs/TestRunner.CompartmentDoors.cs):
    // Game1's fields/methods are private and simulated input isn't reliable headless, so this mirrors
    // the real private IsProtectedCompartmentCore check against a plain TileGrid plus the same
    // TileCoord->instanceId / instanceId->protected-tiles maps _editorCompartmentAt/
    // _editorCompartmentProtected keep, reusing M81's own CompartmentFootprint/
    // RemoveCompartmentWithRepair helpers (same partial class) for test 4.

    // Mirrors IsProtectedCompartmentCore (Game1.ShipEditor.cs) exactly.
    private static bool IsProtectedCompartmentCoreTest(Dictionary<TileCoord, string> compartmentAt,
        Dictionary<string, HashSet<TileCoord>> compartmentProtected, TileCoord coord) =>
        compartmentAt.TryGetValue(coord, out var instanceId)
        && compartmentProtected.TryGetValue(instanceId, out var protectedTiles)
        && protectedTiles.Contains(coord);

    // ---- A compartment's own core DEVICE tile (a Reactor compartment's own reactor) is protected;
    // a non-core device tile elsewhere is not. ----
    private static bool CompartmentProtection_CoreDeviceTile_IsProtectedButNonCoreIsNot()
    {
        var grid = new TileGrid();
        var reactorEntry = CompartmentCatalog.Find("reactor-a-centered");
        if (reactorEntry is null)
            return false;

        var reactorAnchor = new TileCoord(0, 0);
        var reactorResult = CompartmentPlacer.Stamp(grid, reactorEntry, reactorAnchor, rotationSteps: 0, instanceId: "reactor");
        if (!reactorResult.Success || reactorResult.Devices.Count == 0)
            return false;

        var compartmentAt = new Dictionary<TileCoord, string>();
        foreach (var t in CompartmentFootprint(reactorEntry, reactorAnchor, 0))
            compartmentAt[t] = "reactor";
        var compartmentProtected = new Dictionary<string, HashSet<TileCoord>>
        {
            ["reactor"] = new HashSet<TileCoord>(reactorResult.ProtectedTiles),
        };

        // The reactor's own (only) device is its core - IsCore:true (CompartmentCatalog's own
        // ReactorDevices helper).
        var reactorCoreTile = reactorResult.Devices[0].Coord;
        if (!reactorResult.Devices[0].IsCore)
            return false; // sanity
        if (!IsProtectedCompartmentCoreTest(compartmentAt, compartmentProtected, reactorCoreTile))
            return false;

        // weapons-2turret has one core turret and one genuinely non-core turret on two DISTINCT
        // tiles (CompartmentCatalog's own layout) - the real case this milestone must NOT over-block:
        // freely adding/removing a non-core device in an otherwise-protected compartment.
        var weaponsEntry = CompartmentCatalog.Find("weapons-2turret");
        if (weaponsEntry is null)
            return false;
        var weaponsAnchor = new TileCoord(40, 40);
        var weaponsResult = CompartmentPlacer.Stamp(grid, weaponsEntry, weaponsAnchor, rotationSteps: 0, instanceId: "weapons");
        if (!weaponsResult.Success || weaponsResult.Devices.Count != 2)
            return false;
        var weaponsCompartmentAt = new Dictionary<TileCoord, string>();
        foreach (var t in CompartmentFootprint(weaponsEntry, weaponsAnchor, 0))
            weaponsCompartmentAt[t] = "weapons";
        var weaponsProtected = new Dictionary<string, HashSet<TileCoord>>
        {
            ["weapons"] = new HashSet<TileCoord>(weaponsResult.ProtectedTiles),
        };
        var coreTurret = weaponsResult.Devices.First(d => d.IsCore).Coord;
        var nonCoreTurret = weaponsResult.Devices.First(d => !d.IsCore).Coord;
        if (!IsProtectedCompartmentCoreTest(weaponsCompartmentAt, weaponsProtected, coreTurret))
            return false;
        return !IsProtectedCompartmentCoreTest(weaponsCompartmentAt, weaponsProtected, nonCoreTurret);
    }

    // ---- A compartment's own baked ENGINE's Control tile is protected. ----
    private static bool CompartmentProtection_EngineControlTile_IsProtected()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("engine-small-1way");
        if (entry is null)
            return false;

        var anchor = new TileCoord(0, 0);
        var result = CompartmentPlacer.Stamp(grid, entry, anchor, rotationSteps: 0, instanceId: "engine");
        if (!result.Success || result.Engines.Count == 0)
            return false;

        var compartmentAt = new Dictionary<TileCoord, string>();
        foreach (var t in CompartmentFootprint(entry, anchor, 0))
            compartmentAt[t] = "engine";
        var compartmentProtected = new Dictionary<string, HashSet<TileCoord>>
        {
            ["engine"] = new HashSet<TileCoord>(result.ProtectedTiles),
        };

        var controlCoord = result.Engines[0].ControlCoord;
        return IsProtectedCompartmentCoreTest(compartmentAt, compartmentProtected, controlCoord);
    }

    // ---- A Docking compartment's own airlock door tile is protected. ----
    private static bool CompartmentProtection_DockingAirlockDoorTile_IsProtected()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("docking-small");
        if (entry is null)
            return false;

        var anchor = new TileCoord(0, 0);
        var result = CompartmentPlacer.Stamp(grid, entry, anchor, rotationSteps: 0, instanceId: "docking");
        if (!result.Success || result.Airlock is not { } airlock)
            return false;

        var compartmentAt = new Dictionary<TileCoord, string>();
        foreach (var t in CompartmentFootprint(entry, anchor, 0))
            compartmentAt[t] = "docking";
        var compartmentProtected = new Dictionary<string, HashSet<TileCoord>>
        {
            ["docking"] = new HashSet<TileCoord>(result.ProtectedTiles),
        };

        return IsProtectedCompartmentCoreTest(compartmentAt, compartmentProtected, airlock.DoorCoord);
    }

    // ---- The most important non-regression: whole-compartment removal (RemoveCompartmentAt's own
    // pattern - M81's RemoveCompartmentWithRepair helper) must remain completely unrestricted and
    // still clear a core tile (the reactor's own device tile) along with everything else - this
    // milestone's guard must NEVER apply to that path. ----
    private static bool CompartmentProtection_WholeCompartmentRemoval_StillClearsCoreTileUnrestricted()
    {
        var grid = new TileGrid();
        var entry = CompartmentCatalog.Find("reactor-a-centered");
        if (entry is null)
            return false;

        var anchor = new TileCoord(0, 0);
        var result = CompartmentPlacer.Stamp(grid, entry, anchor, rotationSteps: 0, instanceId: "reactor");
        if (!result.Success || result.Devices.Count == 0)
            return false;
        var coreTile = result.Devices[0].Coord;
        if (!result.Devices[0].IsCore)
            return false; // sanity
        if (result.ProtectedTiles.Count == 0 || !result.ProtectedTiles.Contains(coreTile))
            return false; // sanity - confirm this really is a protected core tile before removing it

        var compartmentAt = new Dictionary<TileCoord, string>();
        var compartmentTiles = new Dictionary<string, HashSet<TileCoord>>
        {
            ["reactor"] = CompartmentFootprint(entry, anchor, 0),
        };
        foreach (var t in compartmentTiles["reactor"])
            compartmentAt[t] = "reactor";

        // Confirm the core device tile is really there (a device) before whole-compartment removal.
        if (grid.CellAt(coreTile) is not { DeviceId: not null })
            return false;

        RemoveCompartmentWithRepair(grid, compartmentAt, compartmentTiles, "reactor");

        // The whole-compartment tool is unrestricted - the core tile's device must be gone, same as
        // every other tile of the removed footprint, with no special-case refusal for being protected.
        if (grid.CellAt(coreTile) is { HasFloor: true })
            return false;
        return !compartmentAt.ContainsKey(coreTile) && !compartmentTiles.ContainsKey("reactor");
    }
}
