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
// HalfSide (direct user request, "я хочу чтобы ты сделал отсек таким каким я его сохранил") - only
// meaningful for a CustomDeviceFootprint.IsHalfWidthKind device (null otherwise): which side of its
// own anchor the "half" tile sits on, same convention CustomDeviceDef.HalfWidthSide/TileRecord.
// HalfSide already use. Null falls back to CustomDeviceFootprint.ResolveHalfSide's own East/South-
// by-Rotated default, same as every entry authored before this field existed. Needed because a
// source blueprint can flush a console's half tile against the compartment's own wall ring on ANY
// of the 4 sides, not just whichever side the old Rotated-only fallback happens to produce -
// CompartmentPlacer.Stamp reads this to decide which one footprint tile goes through
// TileGrid.PlaceHalfWidthDevice instead of the ordinary PlaceDevice.
public sealed record CompartmentDeviceSpec(CustomDeviceKind Kind, TileCoord RelativePosition, bool IsCore,
    TurretMountSide MountSide = TurretMountSide.Aft, bool Rotated = false, TileSide? HalfSide = null);

// A Terminal/WallLamp recessed into an already-Solid, already-half-blocked wall tile (TileGrid.
// PlaceRecessedWallDevice's own precondition) - RelativePosition IS the wall tile itself, not a
// floor tile beside it (TileGrid.PlaceWallDevice's own different, floor-adjacent mode isn't
// supported here yet - every source blueprint transcribed so far only ever used the recessed mode).
// MountSide is never authored here - PlaceRecessedWallDevice computes it from the wall's own
// WallOpenSide automatically, so there's nothing to carry through rotation beyond the position
// itself. The wall tile itself must already be Solid with a matching half-block by the time this is
// applied (WallOpenSides above, or the compartment's own ring - CompartmentPlacer.Stamp orders its
// steps accordingly), or TileGrid.PlaceRecessedWallDevice throws.
public sealed record CompartmentWallDeviceSpec(CustomDeviceKind Kind, TileCoord RelativePosition);

// An INTERIOR wall tile, beyond the compartment's own outer ring (CompartmentPlacer's own IsRingTile
// never walls one of these on its own) - direct user request ("стены не касаются внешней оболочки...
// всё должно остаться и быть единым отсеком"): a source blueprint's own interior partition, painted
// by hand in the free-tile editor, that CompartmentPlacer previously had no way to reproduce at all.
// Always stamped Solid first (CompartmentPlacer.Stamp's own new step, right alongside the ring); a
// half-block override or a recessed CompartmentWallDeviceSpec on the SAME position then applies on
// top of it exactly like it would for any ordinary ring tile - RelativePosition must be an interior
// tile of the compartment's own FootprintRects (never on the ring itself, and never already claimed
// by a device), the caller's responsibility same as every other authored position here.
public sealed record CompartmentExtraWallSpec(TileCoord RelativePosition);

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
    IReadOnlyList<CompartmentWallOpenSideSpec>? WallOpenSidesRaw = null,
    // Direct user request ("я хочу чтобы ты сделал отсек таким каким я его сохранил") - see
    // CompartmentExtraWallSpec/CompartmentWallDeviceSpec's own doc comments.
    IReadOnlyList<CompartmentExtraWallSpec>? ExtraWallsRaw = null,
    IReadOnlyList<CompartmentWallDeviceSpec>? WallDevicesRaw = null)
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
    public IReadOnlyList<CompartmentExtraWallSpec> ExtraWalls { get; init; } = ExtraWallsRaw ?? Array.Empty<CompartmentExtraWallSpec>();
    public IReadOnlyList<CompartmentWallDeviceSpec> WallDevices { get; init; } = WallDevicesRaw ?? Array.Empty<CompartmentWallDeviceSpec>();

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
        // Direct user request ("я хочу чтобы ты сделал отсек таким каким я его сохранил" - a
        // follow-up after an earlier, simplified pass dropped several fixtures CompartmentPlacer
        // genuinely couldn't represent yet) - byte-exact transcription of the user's own saved
        // "Новый кокпит" design, now that CompartmentPlacer.cs itself was extended with the 3
        // mechanisms this specific room actually needs (CompartmentDeviceSpec.HalfSide,
        // CompartmentExtraWallSpec, CompartmentWallDeviceSpec - see each one's own doc comment in
        // this file for why). Verified against the real placement engine (all 4 rotations succeed,
        // 4 devices + 10 wall devices + 4 extra-wall tiles all land exactly where authored) rather
        // than just assumed correct - see the room's own real design: two consoles (Navigation/
        // Helm) flush against the north wall, two (ShipStatusMonitor/CommsConsole) flush against
        // the south wall, and a half-block partition across the middle with 4 terminals recessed
        // into it, splitting the room into a "bridge" half and a "status/comms" half without fully
        // blocking passage between them (the partition's own open south half, plus the untouched
        // gaps at columns 0-1 and 6-7, both stay walkable).
        new CompartmentCatalogEntry(
            Id: "cockpit-d",
            DisplayName: "Кокпит 4",
            Type: CompartmentType.Cockpit,
            FootprintRects: new[] { new RectF(0, 0, 8, 8) },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Navigation, new TileCoord(2, 0), IsCore: true, Rotated: true, HalfSide: TileSide.North),
                new CompartmentDeviceSpec(CustomDeviceKind.Helm, new TileCoord(4, 0), IsCore: true, Rotated: true, HalfSide: TileSide.North),
                new CompartmentDeviceSpec(CustomDeviceKind.ShipStatusMonitor, new TileCoord(2, 6), IsCore: false, Rotated: true, HalfSide: TileSide.South),
                new CompartmentDeviceSpec(CustomDeviceKind.CommsConsole, new TileCoord(4, 6), IsCore: false, Rotated: true, HalfSide: TileSide.South),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.East),
                // The interior partition's own half-block open sides (CompartmentExtraWalls below
                // stamps these 4 tiles Solid first) - North stays solid, South is the walkable half.
                new CompartmentWallOpenSideSpec(new TileCoord(2, 4), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 4), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 4), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 4), TileSide.North),
            },
            ExtraWallsRaw: new[]
            {
                new CompartmentExtraWallSpec(new TileCoord(2, 4)),
                new CompartmentExtraWallSpec(new TileCoord(3, 4)),
                new CompartmentExtraWallSpec(new TileCoord(4, 4)),
                new CompartmentExtraWallSpec(new TileCoord(5, 4)),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(0, 2)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(7, 5)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(2, 4)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(3, 4)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(4, 4)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(5, 4)),
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
        // Direct user request ("добавь 2 новых отсека которые я сохранил в игру (новый реакторный
        // отсек и щитовой)") - byte-exact transcription of the user's own saved "Реакторный отсек
        // новый" design, same method as reactor-a and cockpit-d above (RectilinearDecomposition
        // against the full painted tile set, coordinates normalized to (0,0)). Same 8x8 wall-ring
        // shell as cockpit-d (4 corner WallLamps, 4 recessed Terminals near opposite corners, no
        // interior partition this time) with a single Reactor device in the middle - Reactor stays
        // the compartment's one core/protected feature, same convention reactor-a already uses.
        new CompartmentCatalogEntry(
            Id: "reactor-b",
            DisplayName: "Реакторный отсек 2",
            Type: CompartmentType.Reactor,
            FootprintRects: new[] { new RectF(0, 0, 8, 8) },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Reactor, new TileCoord(2, 2), IsCore: true),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.East),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(2, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(0, 2)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(5, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(7, 5)),
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
                // Direct user request ("занимал размер полтора на 1 блок") - the right-hand column
                // moved from X=5 to X=4: at X=5 its own half tile would land at X=6, the ring column
                // that only stays walkable there because of an AUTHORED half-block WallOpenSide
                // (below) - a rotation-fragile coincidence (Rotate()'s own box-vs-point asymmetry for
                // a device anchor can drift a half tile off the wall position it depended on lining
                // up with). X=4 is plain bare interior floor instead - no wall involved at all, so
                // nothing to misalign under rotation (verified: all 4 rotations stamp cleanly).
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(1, 5), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(1, 4), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(1, 3), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 3), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 4), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 5), IsCore: false),
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
                // Direct user request ("занимал размер полтора на 1 блок") - these 4 used to sit 1
                // tile apart (fine at the old 1x1 footprint, guaranteed to overlap each other at the
                // new 2-wide one); Rotated:true turns each 90 degrees so its own half tile claims the
                // row below (Y=2, otherwise unused in this 4-tall room) instead of the column beside
                // it - same X spacing as before, no horizontal collision risk at all now.
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 1), IsCore: false, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(6, 1), IsCore: false, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(7, 1), IsCore: false, Rotated: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 1), IsCore: false, Rotated: true),
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
        // Direct user request ("добавь 2 новых отсека которые я сохранил в игру (новый реакторный
        // отсек и щитовой)") - byte-exact transcription of the user's own saved "щитовой отсек
        // новый" design, same 8x8 shell as reactor-b just above (identical wall ring/WallLamp/
        // Terminal layout - both new rooms share one template, only their interior devices differ).
        // No Distribution panel was placed in this particular save (2 Battery + 6 Junction only,
        // an expansion/breaker room rather than a from-scratch power hub) - unlike distribution-a/b,
        // there's no single device here that's this room's own "reason to exist", so every device is
        // IsCore: false (informational only today - M80's own doc comment, CompartmentCatalog.cs).
        new CompartmentCatalogEntry(
            Id: "distribution-c",
            DisplayName: "Щитовая 3",
            Type: CompartmentType.Distribution,
            FootprintRects: new[] { new RectF(0, 0, 8, 8) },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Battery, new TileCoord(2, 2), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Battery, new TileCoord(5, 2), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(2, 3), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(2, 4), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(2, 5), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 3), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 4), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(5, 5), IsCore: false),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.East),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(2, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(0, 2)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(5, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(7, 5)),
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
