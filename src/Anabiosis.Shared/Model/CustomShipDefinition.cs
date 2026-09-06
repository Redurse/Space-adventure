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

// One optional passage on the boundary shared by these two rooms - at most one per room pair,
// centered on whatever range they actually share (Ship.Custom.cs works out where and how big).
public sealed record CustomDoorDef(string RoomAId, string RoomBId);

// One optional outer hull door on a room's side that has no neighboring room at all - the whole
// side stops being breachable hull once this exists (matches the hand-authored hulls' own airlock
// chambers, whose dedicated outer wall never gets ordinary WallBlocks either - see Ship.cs).
public sealed record CustomAirlockDef(string RoomId, EdgeSide Side);

// A painted wall tile whose material isn't the default Standard (direct user request - "усиленная
// стена"/"иллюминатор", humble-soaring-cat.md M76 follow-up). X/Y are the SAME hull-local tile
// coordinates the Ship Editor's tile canvas already uses (1 unit = 1 tile), which is also exactly
// where Ship.Custom.cs's auto-generated WallBlocks land - Ship.FromCustomDefinition looks each
// generated block's own tile coordinate up here (via TileGridRasterizer.WallBlockTileCoord) and
// copies the match onto that WallBlock. Standard tiles simply have no entry here at all.
public sealed record CustomWallMaterialDef(int X, int Y, WallMaterial Material);

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
    float CapacityBonus = 0f);

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
    IReadOnlyList<CustomEngineDef>? EnginesRaw = null)
{
    public IReadOnlyList<CustomWallMaterialDef> WallMaterials { get; init; } = WallMaterialsRaw ?? Array.Empty<CustomWallMaterialDef>();
    public IReadOnlyList<CustomEngineDef> Engines { get; init; } = EnginesRaw ?? Array.Empty<CustomEngineDef>();

    public static CustomShipDefinition Empty { get; } = new(
        "Мой корабль", Array.Empty<CustomRoomDef>(), Array.Empty<CustomDoorDef>(),
        Array.Empty<CustomAirlockDef>(), Array.Empty<CustomDeviceDef>(), 0f);
}
