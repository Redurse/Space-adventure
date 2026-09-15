using System.Text.Json.Serialization;

namespace Anabiosis.Shared.Model;

// A player-drawn hull (in-game Ship Editor) - the moral equivalent of Ship.CreateStarter() etc.,
// but built at runtime from grid placements instead of hand-authored coordinates. Rooms/doors are
// stored as grid geometry and room-id references rather than raw coordinates, so the editor only
// ever has to reason about whole (or half, see CustomRoomDef) grid cells; Ship.FromCustomDefinition
// (Ship.Custom.cs) derives every actual Room/Door/WallBlock/device the same way the fixed hulls'
// factories do by hand.
public enum EdgeSide
{
    Top,
    Bottom,
    Left,
    Right,
}

// M60 follow-up - "Ship.ToDefinition() round-trip": widened from int to float so a hand-authored
// hull built on a half-unit grid (Ship.Corvette.cs/EnemyShipLayout.Classes.cs's Frigate, e.g. X=4.5)
// can be losslessly converted to/from a definition too, not just editor-drawn whole-unit hulls. Every
// place that compares these coordinates for exact touching-boundary equality (ShipLayoutGeometry.cs)
// stays exact under this change: a half-unit grid's own coordinates and their sums are still exactly
// representable in IEEE-754 float, the same way whole units always were - only genuinely irrational/
// arbitrary-precision placement would need an epsilon instead, which this project's grid-snapped
// placement (editor and future building UI alike) never produces.
//
// M86 follow-up (humble-soaring-cat.md, non-rectangular compartments) - Rects is a UNION of 1+
// pieces instead of exactly one; see Room.cs's own doc comment for why RoomId stays singular
// either way. The (X,Y,Width,Height) constructor below is kept as the compat path every existing
// hand-authored hull and editor-built single-rect room still uses unchanged.
[method: JsonConstructor]
public sealed record CustomRoomDef(string Id, string Name, IReadOnlyList<RectF> Rects)
{
    public CustomRoomDef(string Id, string Name, float X, float Y, float Width, float Height)
        : this(Id, Name, new[] { new RectF(X, Y, Width, Height) })
    {
    }

    public float X => Rects.Min(r => r.X);
    public float Y => Rects.Min(r => r.Y);
    public float Width => Rects.Max(r => r.Right) - X;
    public float Height => Rects.Max(r => r.Bottom) - Y;

    // See Room.cs's own doc comment on why this can't be left to record-synthesized equality.
    public bool Equals(CustomRoomDef? other) =>
        other is not null && Id == other.Id && Name == other.Name && Rects.SequenceEqual(other.Rects);

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(Id, Name);
        foreach (var rect in Rects)
            hash = HashCode.Combine(hash, rect);
        return hash;
    }
}

// A door is now a freely placed object (humble-soaring-cat.md "Дверь как свободный объект") -
// (X, Y) is its own exact center, in the SAME continuous world-unit convention Door.X/Y already
// uses (sitting exactly on whichever room boundary it's placed on), not derived from a room pair.
// Vertical means the shared wall it sits on is a vertical line (rooms side by side along X) - same
// meaning as ShipLayoutGeometry.RoomPairOverlap.Vertical, authored directly since the editor always
// knows it trivially at the moment of placement (which existing wall tile was clicked). Wide is a
// genuine authored choice now (previously always "as wide as the shared wall allows, up to
// Door.StandardSpanUnits") - false gives a 1-unit-span door, true the old up-to-2-unit span.
// RoomAId/RoomBId are no longer part of the input at all - Ship.Custom.cs's BuildDoors resolves
// them from geometry via ShipLayoutGeometry.FindOverlapAt, matching this exact position against
// the room layout's own shared boundaries (which also naturally supports more than one door on the
// same room pair - the old RoomAId/RoomBId-keyed model capped at one, see TileShipBuilder.cs's own
// doorPairs HashSet this replaces).
// Id is an optional override, defaulting to null for every ordinary editor-placed door (which never
// had a stable id concept to begin with - Ship.Custom.cs's BuildDoors auto-numbers "door-N" exactly
// as before whenever this is absent). ShipDefaultHull.cs's own frozen definition is the one caller
// that DOES set it - direct user request ("удали все текущие корабли... полностью удалить из кода")
// - so the old CreateStarter() hull's own literal door ids ("door-cockpit-reactor" etc, which a wide
// swath of the test suite hardcodes) survive being rebuilt through FromCustomDefinition instead of
// silently renumbering into something no existing test could possibly have anticipated.
public sealed record CustomDoorDef(float X, float Y, bool Vertical, bool Wide, string? Id = null);

// One optional outer hull door on a room's side that has no neighboring room at all - the whole
// side stops being breachable hull once this exists (matches the hand-authored hulls' own airlock
// chambers, whose dedicated outer wall never gets ordinary WallBlocks either - see Ship.cs). Id is
// the same optional override CustomDoorDef's own doc comment explains, for the exact same reason.
public sealed record CustomAirlockDef(string RoomId, EdgeSide Side, string? Id = null);

// A painted wall tile whose material isn't the default Standard (direct user request - "усиленная
// стена"/"иллюминатор", humble-soaring-cat.md M76 follow-up). X/Y are the SAME hull-local tile
// coordinates the Ship Editor's tile canvas already uses (1 unit = 1 tile), which is also exactly
// where Ship.Custom.cs's auto-generated WallBlocks land - Ship.FromCustomDefinition looks each
// generated block's own tile coordinate up here (via TileGridRasterizer.WallBlockTileCoord) and
// copies the match onto that WallBlock. Standard tiles simply have no entry here at all.
public sealed record CustomWallMaterialDef(int X, int Y, WallMaterial Material);

// Direct user request ("удали механику что если ставим стены в ряд, они почти все превращаются в
// полублоки... хочу сделать чтобы игрок сам выбирал") - which tile is a deliberately-placed half-
// block wall, and which side of it stays solid (TileCell.WallOpenSide's own doc comment - the free,
// walkable half is the OPPOSITE side). X/Y are the same hull-local tile coordinates
// CustomWallMaterialDef already uses. Only ever present for a tile the free-tile editor's own Wall
// tool explicitly painted as half-block - nothing infers this from footprint shape any more.
public sealed record CustomWallOpenSideDef(int X, int Y, TileSide Side);

// M-doors-as-edges (humble-soaring-cat.md) - a narrow door edge, keyed the same canonical way
// TileGrid.CanonicalEdgeKey stores it (Side is always East or South). Unlike CustomWallOpenSideDef,
// this is never inferred/detected from geometry on export (TileShipBuilder just copies
// TileGrid.DoorEdges verbatim) - the editor is the only source of truth for where these sit.
public sealed record CustomDoorEdgeDef(int X, int Y, TileSide Side, string Id);

public enum CustomDeviceKind
{
    Reactor,
    Distribution,
    Helm,
    Navigation,
    Engine,
    Shields,
    WeaponCharger,
    Oxygen,
    Secondary,
    TurretBallistic,
    TurretLaser,
    // M60 follow-up - the Cruiser's own third turret (Ship.Cruiser.cs) uses this weapon type, but
    // the Ship Editor never offered placing one - added so Ship.ToDefinition() can round-trip it
    // instead of silently dropping it.
    TurretMachineGun,
    AmmoStorage,
    SuitLocker,
    StorageRack,
    CardTable,
    Jukebox,
    Terminal,
    // M60 follow-up - neither of these had a CustomDeviceKind before (the Ship Editor doesn't offer
    // placing either), which meant Ship.FromCustomDefinition always produced zero cameras/mounts -
    // fine for an editor-drawn hull with none to begin with, but silently deleted them from a
    // hand-authored hull the moment it went through a build/definition round trip.
    Camera,
    ComponentMount,
    // Placeable but inert for now (humble-soaring-cat.md's own M70+ plan: "короб проводки... без
    // какой-либо функции в этой фазе") - Ship.FromCustomDefinition has no case for it, so placing
    // one is purely cosmetic in the built Ship today, the same "заготовка под будущий редактор"
    // status DeviceKind.Junction (ShipDevice.cs, M74) already carries in the shared ECS list.
    Junction,
    // The one physical fixture (BatteryBlock) that had no CustomDeviceKind at all before this -
    // Ship.Custom.cs used to always auto-place it right next to the reactor rather than let the
    // player choose. Now genuinely optional/positioned like CardTable: a placed one wins, otherwise
    // the old auto-placement is the fallback - so an editor-drawn hull that never places one keeps
    // working exactly as before.
    Battery,

    // Everything below is genuinely new - none of these have any Ship.FromCustomDefinition case,
    // so placing one is purely cosmetic today (same "заготовка, функционал добавим поэтапно" status
    // Junction already carries). Grouped by the tab they'll live under once the Space Haven-style
    // tabbed palette (a separate, later step) replaces the flat device list - "управление кораблём":
    EngineSmall,
    EngineMedium,
    EngineLarge,
    WarpEngine,
    // "шлюз" (шлюз/airlock itself reuses the existing Door tool - no device needed for it):
    ShuttleHangar,
    DroneHangar,
    // "хранение":
    SmallStorage,
    LargeStorage,
    Morgue,
    FuelRodStorage,
    // "производство":
    ConstructionBench,
    Fabricator,
    Deconstructor,
    WeaponWorkbench,
    // "электроэнергия" - a placeable conduit tile for a future proper wiring rework; today's actual
    // WireSpool-based wire-laying (World.Wiring.cs) is unrelated and untouched by this.
    PowerConduit,
    // "мебель" (Terminal reuses the existing Terminal tool - no device needed for it):
    Table,
    Chair,
    Sofa,
    Bed,
    Nightstand,
    WallLamp,
    Spotlight,
    Lamp,
    DecorativePlant,
    // "оружие" - лазерное орудие/автопушка/рельсотрон map onto the existing TurretLaser/
    // TurretMachineGun/TurretBallistic (just newly categorized under this tab, no rename); these
    // four are the genuinely new additions this tab needs.
    DefensiveTurret,
    ShieldGeneratorSmall,
    ShieldGeneratorLarge,
    WeaponPanel,
    // Direct user request ("тройная дверь... по аналогии как работают остальные устройства") - a
    // real 3-tile-span door isn't representable by the actual Door/tile-editor pipeline without a
    // much larger change (CustomDoorDef.Wide is a bool, not a size; TileGrid.LinkDoors only ever
    // links a strict PAIR of tiles into one door). Same "placeable but cosmetic" workaround every
    // other genuinely new kind on this list already carries - no Ship.FromCustomDefinition case,
    // themed (DeviceSkin.Face.TripleDoor) to look like the real in-game door art rather than
    // inventing a new look.
    TripleDoor,
}

public sealed record CustomDeviceDef(
    CustomDeviceKind Kind, float X, float Y, TurretMountSide MountSide = TurretMountSide.Aft,
    // Camera-only (CustomDeviceKind.Camera) - HullCamera's own MountSide is a different, narrower
    // enum (Fore/Aft only) than a turret's, so it needs its own field rather than reusing MountSide.
    CameraMountSide? CameraSide = null,
    // ComponentMount-only (CustomDeviceKind.ComponentMount) - mirrors ComponentMount.TargetDoorId
    // (an auto-door-controller mount wired to a specific door, e.g. the airlock's own).
    string? TargetDoorId = null,
    // Engine-only (content-каталог отсеков) - a catalog engine/RCS room's own contribution to the
    // ship-wide thrust/turn-rate bonus (World.ShipBuilding.cs's RecomputeDeviceBonuses). 0 for every
    // hand-authored hull's own Engine devices - zero balance change for any existing fixed-class ship.
    float ThrustBonus = 0f,
    float TurnBonus = 0f,
    // Shields-only (content-каталог отсеков) - a catalog shield-generator room's own contribution to
    // ShieldSystem.MaxPoints. Deliberately its OWN field rather than inferred from "count of Shields-
    // kind devices": every hand-authored hull already ships 2 Shields-system devices for wiring/
    // allocation purposes unrelated to a physical generator room, so a raw count would silently
    // double the starting shield capacity of every existing fixed-class ship. This stays 0 for those.
    float CapacityBonus = 0f,
    // Terminal/WallLamp-only - which side of this device's own (X,Y) tile its half-block visual
    // sits on (direct user request, "занимал половину блока и визуально выглядел в соответствии с
    // полублоком"; TileGrid.TileCell.WallDeviceMountSide's own doc comment explains recessed vs
    // protruding). Null only for a save from before this field existed - Ship.Custom.cs falls back
    // to North rather than crashing on an old definition.
    TileSide? WallDeviceFacingSide = null,
    // Direct user bug report ("в кокпите в самой игре устройства не повернуты как в редакторе") -
    // whether this device's own footprint had Width/Height swapped before it was placed (the free-
    // tile editor's own _editorDeviceRotation, R key - CustomDeviceFootprint.Size(Kind) gives the
    // UNrotated shape). TileShipBuilder.BuildDefinition already used this to compute the CENTER
    // position it exports here, but never exported the flag itself - so a rotated Helm/Navigation
    // (the only two rotatable kinds with a real, rendered Ship-side object; every other rotatable
    // kind - workbenches, Bed, ShuttleHangar - stays cosmetic-only in real gameplay regardless) came
    // out the wrong way round in the actual game every time. Defaults to false for every call site
    // that predates this (hand-authored hulls, older saves) - exactly today's unrotated behavior.
    bool Rotated = false,
    // Optional override, defaulting to null for every ordinary editor-placed device - Ship.Custom.cs's
    // BuildTurrets/BuildSimpleDevices/BuildWallDevices all auto-number an id ("turret-N", "kind-N",
    // ...) exactly as before whenever this is absent, no behavior change for a real player's own
    // design. ShipDefaultHull.cs's own frozen definition sets it (direct user request, "удали все
    // текущие корабли... полностью удалить из кода") so the old CreateStarter() hull's own literal
    // ids ("turret-bow", "ammo-storage-quarters", ...) survive the rebuild - a wide swath of the test
    // suite hardcodes those exact strings.
    string? Id = null);

public sealed record CustomShipDefinition(
    string Name,
    IReadOnlyList<CustomRoomDef> Rooms,
    IReadOnlyList<CustomDoorDef> Doors,
    IReadOnlyList<CustomAirlockDef> Airlocks,
    IReadOnlyList<CustomDeviceDef> Devices,
    float ForwardDegrees,
    // Defaults to empty for every call site that predates wall materials (round-tripped hand-
    // authored hulls, older saved definitions) - an empty list means "every wall is Standard",
    // exactly today's behavior.
    IReadOnlyList<CustomWallMaterialDef>? WallMaterialsRaw = null,
    // Direct user request (Cosmoteer-style marching engines) - defaults to empty for every call site
    // that predates them, exactly like WallMaterialsRaw above.
    IReadOnlyList<CustomEngineDef>? EnginesRaw = null,
    // TileShipBuilder.BuildDefinition's own step 3.6 ("отсеки в игре опять не совпадают") - extra
    // wall tiles a T-junction's own private ring needed that no room's Rects could safely include
    // (see that step's own doc comment for why: TileGridRasterizer.FromRooms and ShipLayoutGeometry.
    // FindRoomPairOverlaps both treat every Rects entry as real room geometry, which a residual
    // wall-only sliver isn't). Painted directly onto the real Ship's own Tiles grid after
    // TileGridRasterizer.FromRooms has already built it (Ship.Custom.cs) - never part of Rooms/
    // Rects at all. Defaults to empty for every call site that predates this (hand-authored hulls,
    // older saves, the one test call site) - exactly today's behavior, no change.
    IReadOnlyList<TileCoord>? SupplementalWallTilesRaw = null,
    // The other half of the same step 3.6 fix: every tile that was genuinely open floor in the
    // player's own painted canvas (TileGrid.SealedRegion.Tiles, before any Rects/ring-absorption
    // reasoning), so it can be forced back to plain floor after TileGridRasterizer.FromRooms - which
    // rasterizes purely from Rects/room-edge-adjacency and has no way to know that a particular
    // edge tile of one room's own rect is supposed to stay open because the true hull wall sits one
    // tile further out (a supplemental wall tile, above) instead. Forcing every one of these open
    // is always safe (by construction, none of them was ever a wall/door in the original canvas) and
    // needs no per-tile judgment about why the rasterizer might have walled it. Defaults to empty
    // for every pre-existing call site, same as SupplementalWallTilesRaw.
    IReadOnlyList<TileCoord>? ForcedFloorTilesRaw = null,
    // Direct user request ("хочу сделать чтобы игрок сам выбирал") - defaults to empty for every
    // call site that predates manual half-block walls (hand-authored hulls, older saved
    // definitions) - exactly today's "every wall full-thickness" behavior.
    IReadOnlyList<CustomWallOpenSideDef>? WallOpenSidesRaw = null,
    // M-doors-as-edges (humble-soaring-cat.md) - the new narrow-door-as-edge-between-2-tiles
    // primitive (TileGrid.DoorEdges). Defaults to empty for every call site that predates this
    // (hand-authored hulls, older saves) - exactly today's "no edge doors" behavior.
    IReadOnlyList<CustomDoorEdgeDef>? DoorEdgesRaw = null)
{
    public IReadOnlyList<CustomWallMaterialDef> WallMaterials { get; init; } = WallMaterialsRaw ?? Array.Empty<CustomWallMaterialDef>();
    public IReadOnlyList<CustomEngineDef> Engines { get; init; } = EnginesRaw ?? Array.Empty<CustomEngineDef>();
    public IReadOnlyList<TileCoord> SupplementalWallTiles { get; init; } = SupplementalWallTilesRaw ?? Array.Empty<TileCoord>();
    public IReadOnlyList<TileCoord> ForcedFloorTiles { get; init; } = ForcedFloorTilesRaw ?? Array.Empty<TileCoord>();
    public IReadOnlyList<CustomWallOpenSideDef> WallOpenSides { get; init; } = WallOpenSidesRaw ?? Array.Empty<CustomWallOpenSideDef>();
    public IReadOnlyList<CustomDoorEdgeDef> DoorEdges { get; init; } = DoorEdgesRaw ?? Array.Empty<CustomDoorEdgeDef>();

    public static CustomShipDefinition Empty { get; } = new(
        "Мой корабль", Array.Empty<CustomRoomDef>(), Array.Empty<CustomDoorDef>(),
        Array.Empty<CustomAirlockDef>(), Array.Empty<CustomDeviceDef>(), 0f);
}
