namespace Anabiosis.Shared.Model;

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

// One device baked into a compartment template. RelativePosition is the device's own footprint
// TOP-LEFT anchor (CustomDeviceFootprint.Size(Kind), same anchor convention the free-tile editor's
// own DeviceFootprintTiles uses) - every tile of its real footprint (1 for most kinds, 4x4 for the
// Reactor, 3x2 for Helm/Navigation, swapped to 2x3 when Rotated) MUST be strictly interior (never on
// the W x H rectangle's own outer ring - that's where the wall lives, and TileGrid.PlaceDevice
// refuses a device tile that already carries a wall). IsCore marks the one device (per compartment)
// that's meant to become permanently protected from removal once a later milestone's outfit-mode UI
// exists - every OTHER device on the same compartment is ordinary/removable in that future UI.
public sealed record CompartmentDeviceSpec(CustomDeviceKind Kind, TileCoord RelativePosition, bool IsCore,
    TurretMountSide MountSide = TurretMountSide.Aft, bool Rotated = false);

// One marching/RCS engine assembly baked into a compartment template - RelativeControl is the
// Control tile's own local position (see ShipEngine.cs's own doc comment for the Control/Bulkhead/
// Nozzle 3-tile line along Facing). Always the compartment's own core/protected feature - an engine
// compartment's whole reason for existing.
public sealed record CompartmentEngineSpec(TileCoord RelativeControl, TileSide Facing, float MaxThrust, EngineRole Role);

// The Docking compartment's own airlock - baked as a Door tile centered on this side of the wall
// ring, rather than a CustomDeviceKind device (CustomAirlockDef has no device-kind equivalent at all
// - see CustomShipDefinition.cs's own doc comment). Always the compartment's own protected core.
public sealed record CompartmentAirlockSpec(TileSide Side);

// M91 (humble-soaring-cat.md, non-rectangular compartments) - FootprintRects is a UNION of 1+
// axis-aligned pieces instead of exactly one W x H rectangle, same generalization as Room.cs/
// CustomRoomDef's own M86. The (Width, Height) constructor is kept as the compat path every
// existing single-rect entry (reactor-a/b/c) still uses unchanged.
public sealed record CompartmentCatalogEntry(
    string Id,
    string DisplayName,
    CompartmentType Type,
    IReadOnlyList<RectF> FootprintRects,
    IReadOnlyList<CompartmentDeviceSpec> Devices,
    IReadOnlyList<CompartmentEngineSpec> Engines,
    CompartmentAirlockSpec? Airlock = null)
{
    public CompartmentCatalogEntry(string Id, string DisplayName, CompartmentType Type, int Width, int Height,
        IReadOnlyList<CompartmentDeviceSpec> Devices, IReadOnlyList<CompartmentEngineSpec> Engines, CompartmentAirlockSpec? Airlock = null)
        : this(Id, DisplayName, Type, new[] { new RectF(0, 0, Width, Height) }, Devices, Engines, Airlock)
    {
    }

    // Derived bounding box - kept for every existing read site (Ship Editor palette previews etc.)
    // that only ever wants "a" size. Equals the true single rect exactly whenever FootprintRects has
    // one element (every entry authored before this milestone, forever).
    public int Width => (int)FootprintRects.Max(r => r.Right);
    public int Height => (int)FootprintRects.Max(r => r.Bottom);
}

public static class CompartmentCatalog
{
    private static CompartmentDeviceSpec[] OneDevice(CustomDeviceKind kind, int x, int y) =>
        new[] { new CompartmentDeviceSpec(kind, new TileCoord(x, y), IsCore: true) };

    private static readonly CompartmentDeviceSpec[] NoDevices = Array.Empty<CompartmentDeviceSpec>();
    private static readonly CompartmentEngineSpec[] NoEngines = Array.Empty<CompartmentEngineSpec>();

    // Cleared at the user's own direct request ("вместо всех текущих отсеков я буду присылать новые
    // вариации") - every hand-authored entry this milestone originally shipped with is gone, and is
    // being replaced one reference image at a time. Every client call site (Game1.ShipEditor.
    // DeviceTabs.cs's own palette, Game1.ShipEditor.cs/.Draw.cs's Find lookups) already handles an
    // empty/missing entry gracefully, so the catalog compiles and runs fine at any point during this
    // replacement.
    //
    // reactor-a/b/c: 3 reference screenshots from the user, all labelled "N тип реакторного отсека" -
    // same reactor prop, same 3-door composition (double door north, single doors east/west), each
    // reference just drawn with progressively more open floor around that core and a chamfered/
    // octagon-ish OUTER hull silhouette that gets more pronounced from a to c. Originally the
    // chamfered corners were dropped as unreproducible (this milestone's architecture used to be
    // plain WxH rectangles only) - the user then asked for genuine non-rectangular compartments in
    // general (humble-soaring-cat.md M86-M91), so reactor-d below is the first entry that actually
    // reproduces the cut-corner silhouette, using the same reactor/door composition as a/b/c. Anchor
    // in each centers the Reactor's 4x4 footprint (CustomDeviceFootprint.Size) inside the ring - see
    // TileShipBuilder.cs's own doc comment on why a multi-tile device's RelativePosition is its
    // footprint's top-left anchor, not its center. Doors are NOT baked in here - they come from
    // TileShipBuilder's own gap-closing when two placed compartments touch (confirmed with the
    // user), not CompartmentAirlockSpec (which stays Docking-only).
    public static IReadOnlyList<CompartmentCatalogEntry> Entries { get; } = new[]
    {
        new CompartmentCatalogEntry(
            Id: "reactor-a",
            DisplayName: "Реакторный отсек (тип 1)",
            Type: CompartmentType.Reactor,
            Width: 10,
            Height: 9,
            Devices: OneDevice(CustomDeviceKind.Reactor, 3, 2),
            Engines: NoEngines),
        new CompartmentCatalogEntry(
            Id: "reactor-b",
            DisplayName: "Реакторный отсек (тип 2)",
            Type: CompartmentType.Reactor,
            Width: 12,
            Height: 10,
            Devices: OneDevice(CustomDeviceKind.Reactor, 4, 3),
            Engines: NoEngines),
        new CompartmentCatalogEntry(
            Id: "reactor-c",
            DisplayName: "Реакторный отсек (тип 3)",
            Type: CompartmentType.Reactor,
            Width: 13,
            Height: 9,
            Devices: OneDevice(CustomDeviceKind.Reactor, 4, 2),
            Engines: NoEngines),
        // 12x10 bbox with a 2x2 square cut off each of its 4 corners - a 3-rectangle octagon
        // decomposition (middle band the full width, top/bottom bands narrowed by the cut) - the
        // first entry that actually reproduces references 2/3's chamfered silhouette instead of
        // approximating it with a plain rectangle.
        new CompartmentCatalogEntry(
            Id: "reactor-d",
            DisplayName: "Реакторный отсек (тип 4, со срезанными углами)",
            Type: CompartmentType.Reactor,
            FootprintRects: new[]
            {
                new RectF(0, 2, 12, 6),
                new RectF(2, 0, 8, 2),
                new RectF(2, 8, 8, 2),
            },
            Devices: OneDevice(CustomDeviceKind.Reactor, 4, 3),
            Engines: NoEngines),
        // 14x10 bbox, same 2x2-cut-corner octagon convention as reactor-d - a side-by-side
        // Navigation ("сканер")/Helm ("навигационная панель") console pair, from the user's own
        // reference screenshot ("1 тип кокпита"). Both placed Rotated (their own authored 3x2
        // becomes a 2-wide x 3-tall standing console, matching the reference's tall panel look),
        // with a walking corridor between them and along both flanks leading to the left/right
        // doors (auto-generated at stitching, same as every other entry here - not baked in).
        new CompartmentCatalogEntry(
            Id: "cockpit-a",
            DisplayName: "Кокпит (тип 1)",
            Type: CompartmentType.Cockpit,
            FootprintRects: new[]
            {
                new RectF(0, 2, 14, 6),
                new RectF(2, 0, 10, 2),
                new RectF(2, 8, 10, 2),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Navigation, new TileCoord(3, 3), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Helm, new TileCoord(9, 3), IsCore: true, Rotated: true),
            },
            Engines: NoEngines),
    };

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
