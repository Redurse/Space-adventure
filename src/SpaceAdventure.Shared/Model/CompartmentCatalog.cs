namespace SpaceAdventure.Shared.Model;

// M80 (humble-soaring-cat.md) - the first of 5 milestones (M80-M84) replacing free-tile painting
// with a Cosmoteer/Space-Haven-style "place a whole pre-baked compartment, then outfit it" building
// mode. This file is DATA ONLY - the pure catalog of the 10 base compartment types and their
// variants. CompartmentPlacer.cs is the pure algorithm that stamps one of these onto a real
// TileGrid. Nothing here is wired into the Ship Editor yet (that's M81+) - every entry is authored
// once, at rotation 0 (see CompartmentPlacer.Rotate for the 4-way rotation transform), in a purely
// LOCAL coordinate space with (0,0) at the compartment's own top-left corner.
//
// Every compartment is a plain filled W x H rectangle (Game1.ShipEditor.TileBridge.cs's
// BuildDefinitionFromTiles hard-requires every SealedRegion to be rectangular to convert to a
// CustomRoomDef) with a full Solid wall ring baked onto its ENTIRE own outer boundary and, usually,
// one "core" device/engine/airlock that a later milestone's UI must refuse to let the player remove
// once placed (M80 doesn't build that refusal itself - it just needs to be answerable which tile(s)
// are core, which CompartmentPlacer's own result record exposes as ProtectedTiles).
public enum CompartmentType
{
    Engine,
    Reactor,
    Distribution,
    LifeSupport,
    Engineering,
    Docking,
    Cockpit,
    Weapons,
    Medical,
    CrewQuarters,
}

// One device baked into a compartment template. RelativePosition MUST be strictly interior (never on
// the W x H rectangle's own outer ring - that's where the wall lives, and TileGrid.PlaceDevice
// refuses a device tile that already carries a wall). IsCore marks the one device (per compartment)
// that's meant to become permanently protected from removal once a later milestone's outfit-mode UI
// exists - every OTHER device on the same compartment is ordinary/removable in that future UI.
public sealed record CompartmentDeviceSpec(CustomDeviceKind Kind, TileCoord RelativePosition, bool IsCore, TurretMountSide MountSide = TurretMountSide.Aft);

// One marching/RCS engine assembly baked into a compartment template - RelativeControl is the
// Control tile's own local position (see ShipEngine.cs's own doc comment for the Control/Bulkhead/
// Nozzle 3-tile line along Facing). Always the compartment's own core/protected feature - an engine
// compartment's whole reason for existing.
public sealed record CompartmentEngineSpec(TileCoord RelativeControl, TileSide Facing, float MaxThrust, EngineRole Role);

// The Docking compartment's own airlock - baked as a Door tile centered on this side of the wall
// ring, rather than a CustomDeviceKind device (CustomAirlockDef has no device-kind equivalent at all
// - see CustomShipDefinition.cs's own doc comment). Always the compartment's own protected core.
public sealed record CompartmentAirlockSpec(TileSide Side);

public sealed record CompartmentCatalogEntry(
    string Id,
    string DisplayName,
    CompartmentType Type,
    int Width,
    int Height,
    IReadOnlyList<CompartmentDeviceSpec> Devices,
    IReadOnlyList<CompartmentEngineSpec> Engines,
    CompartmentAirlockSpec? Airlock = null);

public static class CompartmentCatalog
{
    private static CompartmentDeviceSpec[] OneDevice(CustomDeviceKind kind, int x, int y) =>
        new[] { new CompartmentDeviceSpec(kind, new TileCoord(x, y), IsCore: true) };

    private static readonly CompartmentDeviceSpec[] NoDevices = Array.Empty<CompartmentDeviceSpec>();
    private static readonly CompartmentEngineSpec[] NoEngines = Array.Empty<CompartmentEngineSpec>();

    // Cleared at the user's own direct request ("вместо всех текущих отсеков я буду присылать новые
    // вариации") - every hand-authored entry this milestone originally shipped with is gone, ready
    // for a fresh set to replace them. The schema above (CompartmentType/CompartmentDeviceSpec/
    // CompartmentEngineSpec/CompartmentAirlockSpec/CompartmentCatalogEntry) and CompartmentPlacer.cs's
    // own placement algorithm are untouched - only the data went away. Every client call site
    // (Game1.ShipEditor.DeviceTabs.cs's own palette, Game1.ShipEditor.cs/.Draw.cs's Find lookups)
    // already handles an empty/missing entry gracefully, so this compiles and runs fine as-is; the
    // Compartment tool's own palette will just show nothing to place until new entries land here.
    public static IReadOnlyList<CompartmentCatalogEntry> Entries { get; } = Array.Empty<CompartmentCatalogEntry>();

    // A non-overlapping grid of Distribution panels filling the compartment's own interior (never the
    // ring - see CompartmentDeviceSpec's own doc comment), the first one flagged as this compartment's
    // core/protected panel. Direct user request ("щитовой отсек... чтобы приборы там были не
    // вплотную") - panels now sit 2 tiles apart on both axes rather than on every interior tile, so
    // each entry's own Width/Height above grew to keep fitting `cols` panels per row (interior width
    // needed is `1 + 2*(cols-1) + 1`, i.e. the last panel's own column plus one tile of clearance to
    // the wall ring - see each call site's own Width). Distribution has no directional/footprint
    // constraint the way an engine does, so any non-overlapping interior grid works.
    private static CompartmentDeviceSpec[] DistributionPanels(int count, int cols)
    {
        var positions = new List<TileCoord>();
        var row = 1;
        while (positions.Count < count)
        {
            for (var col = 0; col < cols && positions.Count < count; col++)
                positions.Add(new TileCoord(1 + col * 2, row));
            row += 2;
        }
        return positions
            .Select((pos, i) => new CompartmentDeviceSpec(CustomDeviceKind.Distribution, pos, IsCore: i == 0))
            .ToArray();
    }

    public static CompartmentCatalogEntry? Find(string id) => Entries.FirstOrDefault(e => e.Id == id);
}
