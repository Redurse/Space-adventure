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

// Direct user bug report ("почему когда ты сохраняешь чертеж отсека... в нём отсутствуют
// полублоки стены, хотя в исходнике они есть?") - a wall-ring tile the SOURCE blueprint had
// deliberately painted as a half-block (TileCell.WallOpenSide, the free-tile editor's own manual
// Wall-tool toggle) used to be silently discarded the moment that blueprint was transcribed into a
// catalog entry - CompartmentPlacer.Stamp's own wall ring is unconditionally full-thickness
// (CompartmentPlacer.cs's own doc comment on why - AUTOMATIC inference from footprint shape is
// still never done), and until now there was nowhere on CompartmentCatalogEntry to even carry an
// AUTHORED half-block choice through. RelativePosition is the wall-ring tile's own local (X,Y)
// (same authored-at-rotation-0 space FootprintRects/Devices/Engines already use); Side is which
// half stays solid, same TileSide convention TileCell.WallOpenSide itself uses.
public sealed record CompartmentWallOpenSideSpec(TileCoord RelativePosition, TileSide Side);

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
    CompartmentAirlockSpec? Airlock = null,
    IReadOnlyList<CompartmentWallOpenSideSpec>? WallOpenSidesRaw = null)
{
    public CompartmentCatalogEntry(string Id, string DisplayName, CompartmentType Type, int Width, int Height,
        IReadOnlyList<CompartmentDeviceSpec> Devices, IReadOnlyList<CompartmentEngineSpec> Engines, CompartmentAirlockSpec? Airlock = null,
        IReadOnlyList<CompartmentWallOpenSideSpec>? WallOpenSidesRaw = null)
        : this(Id, DisplayName, Type, new[] { new RectF(0, 0, Width, Height) }, Devices, Engines, Airlock, WallOpenSidesRaw)
    {
    }

    // Empty for every entry that never authored a half-block wall (every entry before this
    // milestone, and most since - same optional-defaults convention CustomShipDefinition.
    // WallOpenSidesRaw already established).
    public IReadOnlyList<CompartmentWallOpenSideSpec> WallOpenSides { get; init; } = WallOpenSidesRaw ?? Array.Empty<CompartmentWallOpenSideSpec>();

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

    // Replacement round (direct user request, "давай сейчас я создам множество новых отсеков а все
    // старые сейчас удалим... как я сделаю новые отсеки я тебе скажу") - byte-exact transcription of
    // 10 of the user's own saved designs (%LocalAppData%\Anabiosis\custom-ships\), same method as
    // every earlier transcription in this project's history: RectilinearDecomposition.Decompose
    // against the FULL painted tile set from each .tiles.json (including its own wall ring),
    // coordinates normalized to (0,0), devices/engines/half-block walls read straight from
    // CustomShipTileCanvas.DeviceRecord/EngineRecord/TileRecord - a one-off DIAG=1 diagnostic script,
    // reverted after use. An 11th save ("Инжинерный отсек", no number) was skipped - it shares
    // engineering-a's own exact footprint but carries zero devices, reading as an earlier, since-
    // superseded draft of the same room rather than its own distinct design; flagged to the user
    // rather than guessed past.
    //
    // IsCore follows the same per-type convention already established before this catalog's own
    // clearing: Engine's engine and Reactor's reactor are always core (CompartmentEngineSpec/the
    // Reactor device are each their type's one defining reason to exist); Engineering marks every
    // device core (Fabricator/Deconstructor/ConstructionBench/StorageRack/Camera alike - "каждое —
    // отдельная, незаменимая причина существования отсека", the same rule this project's own history
    // already applied to it); Cockpit marks only Helm+Navigation core (CardTable/Jukebox/Camera are
    // replaceable furniture); Weapons marks its turrets/weapon panels core, Camera not; Distribution
    // marks exactly ONE Distribution panel core (DistributionPanels' own established rule), every
    // Battery/Junction/Camera replaceable. Docking ("Шлюз 1") authored no door of its own in the
    // canvas at all (an airlock's Side is a catalog-author choice, not something painted -
    // CompartmentAirlockSpec's own doc comment - RingCenter places its door automatically once a Side
    // is picked) - West chosen arbitrarily on this small, symmetric 4x3 vestibule with no other
    // constraint favouring any particular side.
    public static IReadOnlyList<CompartmentCatalogEntry> Entries { get; } = new[]
    {
        new CompartmentCatalogEntry(
            Id: "engine-a",
            DisplayName: "Двигатель 1",
            Type: CompartmentType.Engine,
            Width: 3, Height: 6,
            Devices: NoDevices,
            Engines: new[] { new CompartmentEngineSpec(new TileCoord(1, 4), TileSide.South, MaxThrust: 8f, Role: EngineRole.Marching) },
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 4), TileSide.East),
            }),
        new CompartmentCatalogEntry(
            Id: "engineering-a",
            DisplayName: "Инжинерный отсек 1",
            Type: CompartmentType.Engineering,
            FootprintRects: new[]
            {
                new RectF(2, 0, 5, 12),
                new RectF(1, 2, 1, 8),
                new RectF(7, 2, 1, 8),
                new RectF(0, 3, 1, 6),
                new RectF(8, 3, 1, 6),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Fabricator, new TileCoord(1, 4), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Deconstructor, new TileCoord(5, 4), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.ConstructionBench, new TileCoord(3, 1), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.StorageRack, new TileCoord(6, 8), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.StorageRack, new TileCoord(2, 8), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.StorageRack, new TileCoord(5, 8), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.StorageRack, new TileCoord(3, 8), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Camera, new TileCoord(3, 9), IsCore: true, Rotated: true),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(2, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 9), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 10), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 11), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 11), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 11), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 9), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 10), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 7), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 7), TileSide.West),
            }),
        new CompartmentCatalogEntry(
            Id: "weapons-a",
            DisplayName: "Орудийный отсек 1",
            Type: CompartmentType.Weapons,
            FootprintRects: new[]
            {
                new RectF(2, 0, 5, 12),
                new RectF(1, 2, 1, 8),
                new RectF(7, 2, 1, 8),
                new RectF(0, 3, 1, 6),
                new RectF(8, 3, 1, 6),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.TurretBallistic, new TileCoord(1, 4), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.TurretLaser, new TileCoord(5, 4), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.WeaponPanel, new TileCoord(6, 3), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.WeaponPanel, new TileCoord(2, 3), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Camera, new TileCoord(2, 8), IsCore: false),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 7), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 10), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 10), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 7), TileSide.East),
            }),
        new CompartmentCatalogEntry(
            Id: "cockpit-a",
            DisplayName: "Кокпит 1",
            Type: CompartmentType.Cockpit,
            FootprintRects: new[]
            {
                new RectF(2, 0, 3, 10),
                new RectF(1, 1, 1, 9),
                new RectF(5, 1, 1, 9),
                new RectF(0, 4, 1, 6),
                new RectF(6, 4, 1, 6),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Navigation, new TileCoord(4, 5), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Helm, new TileCoord(1, 5), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.CardTable, new TileCoord(4, 2), IsCore: false, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Jukebox, new TileCoord(2, 2), IsCore: false, Rotated: true),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(0, 8), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 7), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 8), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 9), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 9), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 9), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 9), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 9), TileSide.South),
            }),
        new CompartmentCatalogEntry(
            Id: "cockpit-b",
            DisplayName: "Кокпит 2",
            Type: CompartmentType.Cockpit,
            FootprintRects: new[]
            {
                new RectF(0, 0, 11, 4),
                new RectF(2, 4, 7, 3),
                new RectF(4, 7, 3, 1),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Helm, new TileCoord(3, 3), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Navigation, new TileCoord(6, 3), IsCore: true, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Camera, new TileCoord(9, 2), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Jukebox, new TileCoord(8, 2), IsCore: false, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.CardTable, new TileCoord(2, 2), IsCore: false, Rotated: true),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(2, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 6), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
            }),
        new CompartmentCatalogEntry(
            Id: "cockpit-c",
            DisplayName: "Кокпит 3",
            Type: CompartmentType.Cockpit,
            FootprintRects: new[]
            {
                new RectF(1, 0, 5, 10),
                new RectF(0, 1, 1, 8),
                new RectF(6, 1, 1, 8),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Helm, new TileCoord(2, 3), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Navigation, new TileCoord(2, 5), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Camera, new TileCoord(4, 1), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Jukebox, new TileCoord(3, 2), IsCore: false, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.CardTable, new TileCoord(3, 7), IsCore: false, Rotated: true),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(2, 9), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 9), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 9), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 7), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 2), TileSide.East),
            }),
        new CompartmentCatalogEntry(
            Id: "reactor-a",
            DisplayName: "Реакторный отсек 1",
            Type: CompartmentType.Reactor,
            Width: 9, Height: 8,
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Reactor, new TileCoord(1, 2), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.SmallStorage, new TileCoord(7, 6), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.FuelRodStorage, new TileCoord(7, 1), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Oxygen, new TileCoord(5, 5), IsCore: false, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Secondary, new TileCoord(5, 2), IsCore: false, Rotated: true),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
            }),
        new CompartmentCatalogEntry(
            Id: "docking-a",
            DisplayName: "Шлюз 1",
            Type: CompartmentType.Docking,
            Width: 4, Height: 3,
            Devices: NoDevices,
            Engines: NoEngines,
            Airlock: new CompartmentAirlockSpec(TileSide.West),
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 2), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 2), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 1), TileSide.East),
            }),
        new CompartmentCatalogEntry(
            Id: "distribution-a",
            DisplayName: "Щитовая 1",
            Type: CompartmentType.Distribution,
            FootprintRects: new[]
            {
                new RectF(1, 0, 5, 9),
                new RectF(0, 1, 1, 7),
                new RectF(6, 1, 1, 7),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Distribution, new TileCoord(3, 2), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Battery, new TileCoord(1, 6), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Battery, new TileCoord(5, 6), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(1, 5), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(1, 4), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(1, 3), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 3), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 4), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 5), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Camera, new TileCoord(4, 7), IsCore: false),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 8), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 8), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 8), TileSide.South),
            }),
        new CompartmentCatalogEntry(
            Id: "distribution-b",
            DisplayName: "Щитовая 2",
            Type: CompartmentType.Distribution,
            FootprintRects: new[]
            {
                new RectF(1, 0, 10, 4),
                new RectF(0, 1, 1, 3),
                new RectF(11, 1, 1, 3),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Distribution, new TileCoord(2, 1), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Battery, new TileCoord(9, 1), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 1), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(6, 1), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(7, 1), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 1), IsCore: false),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(9, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(11, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(8, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(9, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(10, 3), TileSide.South),
            }),
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
