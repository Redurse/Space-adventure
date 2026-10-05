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

    // Replacement round (direct user request, "добавь в раздел готовых отсеков все отсеки с дополнением
    // НОВЫЙ, а все старые удали оттуда") - every earlier entry is gone; this catalog is now exactly the
    // 9 designs the user saved in the Ship Editor with "новый/новая" in their name
    // (%LocalAppData%\Anabiosis\custom-ships\), transcribed the same way every earlier round was:
    // RectilinearDecomposition.Decompose over the full painted tile set of each .tiles.json (wall ring
    // included), coordinates normalised to (0,0), devices/engines/half-block walls/recessed wall devices
    // read straight from CustomShipTileCanvas, interior Solid walls (not on the ring) carried as extra
    // walls. A one-off converter, not kept in the repo.
    //
    // IsCore follows the per-type convention already used here: Engine's engines are always core;
    // Engineering marks every device core; Cockpit only Helm+Navigation; Reactor only the Reactor;
    // Weapons its turrets and weapon panels; Distribution its first Distribution panel (the "Щиток новый"
    // design has batteries and junctions only, so none of its devices is protected). The docking
    // compartment ("Шлюз новый") painted no door of its own - an airlock's side is a catalog-author
    // choice (CompartmentAirlockSpec) - West, as before. Engines keep the editor's MaxThrust 8 /
    // Marching defaults; the saves do not carry thrust or role.
    public static IReadOnlyList<CompartmentCatalogEntry> Entries { get; } = new[]
    {
        new CompartmentCatalogEntry(
            Id: "engine-new-1",
            DisplayName: "Двигатель новый (1)",
            Type: CompartmentType.Engine,
            FootprintRects: new[]
            {
                new RectF(0, 0, 4, 4),
            },
            Devices: NoDevices,
            Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(1, 2), TileSide.South, MaxThrust: 8f, Role: EngineRole.Marching),
                new CompartmentEngineSpec(new TileCoord(2, 2), TileSide.South, MaxThrust: 8f, Role: EngineRole.Marching),
            },
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 2), TileSide.East),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(0, 1)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(3, 1)),
            }),
        new CompartmentCatalogEntry(
            Id: "engine-new-1-1",
            DisplayName: "Двигатель новый (1.1)",
            Type: CompartmentType.Engine,
            FootprintRects: new[]
            {
                new RectF(0, 0, 4, 8),
            },
            Devices: NoDevices,
            Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(2, 6), TileSide.South, MaxThrust: 8f, Role: EngineRole.Marching),
                new CompartmentEngineSpec(new TileCoord(1, 6), TileSide.South, MaxThrust: 8f, Role: EngineRole.Marching),
            },
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 6), TileSide.East),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(0, 2)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(3, 2)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(0, 5)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(3, 5)),
            }),
        new CompartmentCatalogEntry(
            Id: "engine-new-3",
            DisplayName: "Двигатель новый (3)",
            Type: CompartmentType.Engine,
            FootprintRects: new[]
            {
                new RectF(0, 0, 4, 4),
            },
            Devices: NoDevices,
            Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(1, 2), TileSide.South, MaxThrust: 8f, Role: EngineRole.Marching),
                new CompartmentEngineSpec(new TileCoord(1, 2), TileSide.West, MaxThrust: 8f, Role: EngineRole.Marching),
                new CompartmentEngineSpec(new TileCoord(2, 2), TileSide.East, MaxThrust: 8f, Role: EngineRole.Marching),
                new CompartmentEngineSpec(new TileCoord(2, 2), TileSide.South, MaxThrust: 8f, Role: EngineRole.Marching),
            },
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 1), TileSide.East),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(0, 1)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(3, 1)),
            }),
        new CompartmentCatalogEntry(
            Id: "engineering-new",
            DisplayName: "Инжинерный отсек новый",
            Type: CompartmentType.Engineering,
            FootprintRects: new[]
            {
                new RectF(0, 0, 8, 8),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Deconstructor, new TileCoord(1, 0), IsCore: true, Rotated: true, HalfSide: TileSide.North),
                new CompartmentDeviceSpec(CustomDeviceKind.Fabricator, new TileCoord(4, 0), IsCore: true, Rotated: true, HalfSide: TileSide.North),
                new CompartmentDeviceSpec(CustomDeviceKind.WeaponWorkbench, new TileCoord(2, 4), IsCore: true, Rotated: true),
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
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
            },
            ExtraWallsRaw: new[]
            {
                new CompartmentExtraWallSpec(new TileCoord(2, 3)),
                new CompartmentExtraWallSpec(new TileCoord(3, 3)),
                new CompartmentExtraWallSpec(new TileCoord(4, 3)),
                new CompartmentExtraWallSpec(new TileCoord(5, 3)),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(0, 1)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(7, 1)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 7)),
            }),
        new CompartmentCatalogEntry(
            Id: "cockpit-new",
            DisplayName: "Новый кокпит",
            Type: CompartmentType.Cockpit,
            FootprintRects: new[]
            {
                new RectF(0, 0, 8, 8),
            },
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
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 4), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 4), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 4), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 4), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
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
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(2, 4)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(3, 4)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(4, 4)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.Terminal, new TileCoord(5, 4)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 7)),
            }),
        new CompartmentCatalogEntry(
            Id: "reactor-new",
            DisplayName: "Реакторный отсек новый",
            Type: CompartmentType.Reactor,
            FootprintRects: new[]
            {
                new RectF(0, 0, 8, 8),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Reactor, new TileCoord(2, 3), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Distribution, new TileCoord(4, 2), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.FuelRodStorage, new TileCoord(3, 2), IsCore: false),
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
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 2), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 2), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
            },
            ExtraWallsRaw: new[]
            {
                new CompartmentExtraWallSpec(new TileCoord(2, 2)),
                new CompartmentExtraWallSpec(new TileCoord(5, 2)),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 7)),
            }),
        new CompartmentCatalogEntry(
            Id: "weapons-new",
            DisplayName: "Турель новая",
            Type: CompartmentType.Weapons,
            FootprintRects: new[]
            {
                new RectF(0, 0, 4, 4),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.TurretBallistic, new TileCoord(0, 0), IsCore: true),
            },
            Engines: NoEngines,
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 3), TileSide.South),
            }),
        new CompartmentCatalogEntry(
            Id: "docking-new",
            DisplayName: "Шлюз новый",
            Type: CompartmentType.Docking,
            FootprintRects: new[]
            {
                new RectF(0, 0, 4, 4),
            },
            Devices: NoDevices,
            Engines: NoEngines,
            Airlock: new CompartmentAirlockSpec(TileSide.West),
            WallOpenSidesRaw: new[]
            {
                new CompartmentWallOpenSideSpec(new TileCoord(1, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 0), TileSide.North),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 3), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 3), TileSide.South),
            }),
        new CompartmentCatalogEntry(
            Id: "distribution-new",
            DisplayName: "Щиток новый",
            Type: CompartmentType.Distribution,
            FootprintRects: new[]
            {
                new RectF(0, 0, 8, 8),
            },
            Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Battery, new TileCoord(3, 2), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 3), IsCore: false, HalfSide: TileSide.East),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 4), IsCore: false, HalfSide: TileSide.East),
                new CompartmentDeviceSpec(CustomDeviceKind.Battery, new TileCoord(4, 2), IsCore: false),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(2, 4), IsCore: false, HalfSide: TileSide.West),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(2, 3), IsCore: false, HalfSide: TileSide.West),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(2, 5), IsCore: false, HalfSide: TileSide.West),
                new CompartmentDeviceSpec(CustomDeviceKind.Junction, new TileCoord(4, 5), IsCore: false, HalfSide: TileSide.East),
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
                new CompartmentWallOpenSideSpec(new TileCoord(0, 1), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 1), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 2), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 2), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 3), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 3), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 4), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 4), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 5), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 5), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(0, 6), TileSide.West),
                new CompartmentWallOpenSideSpec(new TileCoord(7, 6), TileSide.East),
                new CompartmentWallOpenSideSpec(new TileCoord(1, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(2, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(3, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(4, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(5, 7), TileSide.South),
                new CompartmentWallOpenSideSpec(new TileCoord(6, 7), TileSide.South),
            },
            WallDevicesRaw: new[]
            {
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 0)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(1, 7)),
                new CompartmentWallDeviceSpec(CustomDeviceKind.WallLamp, new TileCoord(6, 7)),
            }),
    };

    public static CompartmentCatalogEntry? Find(string id) => Entries.FirstOrDefault(e => e.Id == id);
}
