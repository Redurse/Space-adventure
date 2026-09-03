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

    public static IReadOnlyList<CompartmentCatalogEntry> Entries { get; } = new[]
    {
        // ---- 1. Двигательный (Engine) - 5 tiers, worked out against ShipEngine.cs's own
        // Control/Bulkhead/Nozzle geometry (see this milestone's own plan doc for the by-hand
        // verification: no two engines' 3-tile footprints overlap, every Bulkhead lands exactly on
        // the compartment's own wall ring, every Nozzle lands genuinely outside the footprint). ----
        new CompartmentCatalogEntry("engine-small-1way", "Двигатель маршевый (малый, однонаправленный)", CompartmentType.Engine,
            Width: 3, Height: 3, Devices: NoDevices, Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(1, 1), TileSide.West, 5f, EngineRole.Marching),
            }),

        new CompartmentCatalogEntry("engine-small-2way", "Двигатель манёвровый (малый, 2-направленный)", CompartmentType.Engine,
            Width: 5, Height: 3, Devices: NoDevices, Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(1, 1), TileSide.West, 7f, EngineRole.Rcs),
                new CompartmentEngineSpec(new TileCoord(3, 1), TileSide.East, 7f, EngineRole.Rcs),
            }),

        new CompartmentCatalogEntry("engine-small-3way", "Двигатель манёвровый (малый, 3-направленный)", CompartmentType.Engine,
            Width: 7, Height: 4, Devices: NoDevices, Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(1, 2), TileSide.South, 22f / 3f, EngineRole.Rcs),
                new CompartmentEngineSpec(new TileCoord(3, 2), TileSide.South, 22f / 3f, EngineRole.Rcs),
                new CompartmentEngineSpec(new TileCoord(5, 2), TileSide.South, 22f / 3f, EngineRole.Rcs),
            }),

        new CompartmentCatalogEntry("engine-medium", "Двигатель маршевый (средний)", CompartmentType.Engine,
            Width: 5, Height: 5, Devices: NoDevices, Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(1, 2), TileSide.West, 12f, EngineRole.Marching),
            }),

        new CompartmentCatalogEntry("engine-large", "Двигатель маршевый (большой)", CompartmentType.Engine,
            Width: 7, Height: 7, Devices: NoDevices, Engines: new[]
            {
                new CompartmentEngineSpec(new TileCoord(1, 3), TileSide.West, 20f, EngineRole.Marching),
            }),

        // ---- 2. Реакторный (Reactor) - 5 shape/placement variants, identical reactor stats (no
        // SizeScale/output difference - purely a layout choice, per the user's own confirmation).
        // Enlarged + 2 new variants added (direct user request, "увеличь размер реакторного отсека и
        // добавь ещё несколько вариаций") - reactor-b-wide keeps the SAME relative (off-center-on-
        // both-axes) placement convention it always had, just scaled up; TestRunner.CompartmentCatalog.
        // cs's own rotation-transform test hand-derives its expected coordinates from this entry's
        // exact Width/Height/position, so its comment block was updated to match these new numbers -
        // touch both together if either changes again. ----
        new CompartmentCatalogEntry("reactor-a-centered", "Реакторный отсек (центрированный)", CompartmentType.Reactor,
            Width: 7, Height: 7, Devices: OneDevice(CustomDeviceKind.Reactor, 3, 3), Engines: NoEngines),

        new CompartmentCatalogEntry("reactor-b-wide", "Реакторный отсек (широкий)", CompartmentType.Reactor,
            Width: 8, Height: 5, Devices: OneDevice(CustomDeviceKind.Reactor, 2, 1), Engines: NoEngines),

        new CompartmentCatalogEntry("reactor-c-tall", "Реакторный отсек (вытянутый)", CompartmentType.Reactor,
            Width: 5, Height: 8, Devices: OneDevice(CustomDeviceKind.Reactor, 2, 4), Engines: NoEngines),

        new CompartmentCatalogEntry("reactor-d-large", "Реакторный отсек (большой)", CompartmentType.Reactor,
            Width: 9, Height: 9, Devices: OneDevice(CustomDeviceKind.Reactor, 4, 4), Engines: NoEngines),

        new CompartmentCatalogEntry("reactor-e-offset", "Реакторный отсек (смещённый)", CompartmentType.Reactor,
            Width: 7, Height: 6, Devices: OneDevice(CustomDeviceKind.Reactor, 2, 3), Engines: NoEngines),

        // ---- 3. Щитовая (Distribution) - panel-count variants (6..10 Distribution devices) double
        // as the shape variants (the room grows to comfortably fit more panels). Direct user request
        // ("щитовой отсек гораздо больше, чтобы приборы были не вплотную") - DistributionPanels now
        // spaces panels 2 tiles apart on both axes instead of packing them onto every interior tile,
        // and each room's own Width/Height grew to match. The first panel in each entry is the
        // compartment's own core/protected one; the rest are ordinary/removable in a future outfit-
        // mode UI. ----
        new CompartmentCatalogEntry("distribution-6", "Щитовая (6 панелей)", CompartmentType.Distribution,
            Width: 7, Height: 5, Devices: DistributionPanels(6, 3), Engines: NoEngines),
        new CompartmentCatalogEntry("distribution-7", "Щитовая (7 панелей)", CompartmentType.Distribution,
            Width: 7, Height: 7, Devices: DistributionPanels(7, 3), Engines: NoEngines),
        new CompartmentCatalogEntry("distribution-8", "Щитовая (8 панелей)", CompartmentType.Distribution,
            Width: 9, Height: 5, Devices: DistributionPanels(8, 4), Engines: NoEngines),
        new CompartmentCatalogEntry("distribution-9", "Щитовая (9 панелей)", CompartmentType.Distribution,
            Width: 9, Height: 7, Devices: DistributionPanels(9, 4), Engines: NoEngines),
        new CompartmentCatalogEntry("distribution-10", "Щитовая (10 панелей)", CompartmentType.Distribution,
            Width: 9, Height: 7, Devices: DistributionPanels(10, 4), Engines: NoEngines),

        // ---- 4. Жизнеобеспечение (Life support) - Oxygen is the closest existing CustomDeviceKind. ----
        new CompartmentCatalogEntry("life-support-small", "Жизнеобеспечение (малый)", CompartmentType.LifeSupport,
            Width: 4, Height: 4, Devices: OneDevice(CustomDeviceKind.Oxygen, 2, 2), Engines: NoEngines),
        new CompartmentCatalogEntry("life-support-medium", "Жизнеобеспечение (средний)", CompartmentType.LifeSupport,
            Width: 5, Height: 4, Devices: OneDevice(CustomDeviceKind.Oxygen, 2, 2), Engines: NoEngines),
        new CompartmentCatalogEntry("life-support-large", "Жизнеобеспечение (большой)", CompartmentType.LifeSupport,
            Width: 5, Height: 5, Devices: OneDevice(CustomDeviceKind.Oxygen, 2, 2), Engines: NoEngines),

        // ---- 5. Инженерный (Engineering) - no dedicated "workbench" CustomDeviceKind exists yet, so
        // ComponentMount (a generic engineering-panel fixture) stands in - documented in the M80
        // report as a first-pass placeholder pick, easy to swap later. ----
        new CompartmentCatalogEntry("engineering-small", "Инженерный отсек (малый)", CompartmentType.Engineering,
            Width: 4, Height: 4, Devices: OneDevice(CustomDeviceKind.ComponentMount, 2, 2), Engines: NoEngines),
        new CompartmentCatalogEntry("engineering-medium", "Инженерный отсек (средний)", CompartmentType.Engineering,
            Width: 5, Height: 4, Devices: OneDevice(CustomDeviceKind.ComponentMount, 2, 2), Engines: NoEngines),
        new CompartmentCatalogEntry("engineering-large", "Инженерный отсек (большой)", CompartmentType.Engineering,
            Width: 6, Height: 5, Devices: OneDevice(CustomDeviceKind.ComponentMount, 3, 2), Engines: NoEngines),

        // ---- 6. Стыковочный (Docking) - an Airlock baked into the wall ring, not a device at all
        // (see CompartmentAirlockSpec's own doc comment). Authored with the door on the East side;
        // CompartmentPlacer.Rotate carries it to whichever side the player actually wants. ----
        new CompartmentCatalogEntry("docking-small", "Стыковочный отсек (малый)", CompartmentType.Docking,
            Width: 4, Height: 4, Devices: NoDevices, Engines: NoEngines, Airlock: new CompartmentAirlockSpec(TileSide.East)),
        new CompartmentCatalogEntry("docking-medium", "Стыковочный отсек (средний)", CompartmentType.Docking,
            Width: 5, Height: 5, Devices: NoDevices, Engines: NoEngines, Airlock: new CompartmentAirlockSpec(TileSide.East)),

        // ---- 7. Кокпит (Cockpit) - Helm + Navigation, sharing one position exactly (harmless - see
        // RoomCatalog.DevicesFor's own doc comment on this same pattern). Helm is the core seat. ----
        new CompartmentCatalogEntry("cockpit-small", "Кокпит (малый)", CompartmentType.Cockpit,
            Width: 5, Height: 5, Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Helm, new TileCoord(2, 2), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Navigation, new TileCoord(2, 2), IsCore: false),
            }, Engines: NoEngines),
        new CompartmentCatalogEntry("cockpit-medium", "Кокпит (средний)", CompartmentType.Cockpit,
            Width: 6, Height: 5, Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.Helm, new TileCoord(3, 2), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.Navigation, new TileCoord(3, 2), IsCore: false),
            }, Engines: NoEngines),

        // ---- 8. Оружейный (Weapons) - turret-count variants (1 or 2 TurretBallistic). The lone
        // turret is core when there's only one; the first-placed one is core when there are two.
        // Enlarged (direct user request, "оружейные отсеки гораздо больше") - both variants grew
        // well past the old cramped 4x5/6x5, and the 2-turret room's own turrets now sit far enough
        // apart (5 tiles) to read as two distinct mounts rather than a tight pair. ----
        new CompartmentCatalogEntry("weapons-1turret", "Оружейный отсек (1 турель)", CompartmentType.Weapons,
            Width: 6, Height: 6, Devices: OneDevice(CustomDeviceKind.TurretBallistic, 3, 3), Engines: NoEngines),
        new CompartmentCatalogEntry("weapons-2turret", "Оружейный отсек (2 турели)", CompartmentType.Weapons,
            Width: 10, Height: 6, Devices: new[]
            {
                new CompartmentDeviceSpec(CustomDeviceKind.TurretBallistic, new TileCoord(2, 3), IsCore: true),
                new CompartmentDeviceSpec(CustomDeviceKind.TurretBallistic, new TileCoord(7, 3), IsCore: false),
            }, Engines: NoEngines),

        // ---- 9. Медицинский (Medical) - no dedicated med-bay CustomDeviceKind exists yet; Morgue is
        // the closest sensible existing kind (documented as a first-pass placeholder pick). ----
        new CompartmentCatalogEntry("medical-small", "Медицинский отсек (малый)", CompartmentType.Medical,
            Width: 4, Height: 4, Devices: OneDevice(CustomDeviceKind.Morgue, 2, 2), Engines: NoEngines),
        new CompartmentCatalogEntry("medical-medium", "Медицинский отсек (средний)", CompartmentType.Medical,
            Width: 5, Height: 4, Devices: OneDevice(CustomDeviceKind.Morgue, 2, 2), Engines: NoEngines),

        // ---- 10. Каюта экипажа (Crew quarters) - Bed already exists and is the obviously correct
        // core device (better than a storage-kind fallback). ----
        new CompartmentCatalogEntry("crew-quarters-small", "Каюта экипажа (малая)", CompartmentType.CrewQuarters,
            Width: 4, Height: 4, Devices: OneDevice(CustomDeviceKind.Bed, 2, 2), Engines: NoEngines),
        new CompartmentCatalogEntry("crew-quarters-medium", "Каюта экипажа (средняя)", CompartmentType.CrewQuarters,
            Width: 5, Height: 4, Devices: OneDevice(CustomDeviceKind.Bed, 2, 1), Engines: NoEngines),
        new CompartmentCatalogEntry("crew-quarters-large", "Каюта экипажа (большая)", CompartmentType.CrewQuarters,
            Width: 6, Height: 5, Devices: OneDevice(CustomDeviceKind.Bed, 3, 2), Engines: NoEngines),
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
