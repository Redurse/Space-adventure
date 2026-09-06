namespace Anabiosis.Shared.Model;

// M60 - "строить отсеки по ходу игры": the placeable-room catalog the Shipwright offers
// (World.ShipBuilding.cs's GetBuildableRoomCatalog/TryBuildRoom). PlatingCost (M62) is the
// ItemType.HullPlating consumed from the ship's own stock when a build starts - on top of Price,
// not instead of it: credits are still what a Shipwright charges, plating is the physical material
// actually being welded on, which is why building now needs BOTH a full wallet and a stocked hold.
//
// Content-каталог отсеков (see the plan's own "содержательный каталог отсеков" section) - every
// entry beyond the original 2 empty shells carries a real device (or two, for a bridge). Every
// device lands at the room's own centre point (World.ShipBuilding.cs's TryBuildRoom) - point-
// containment is all Ship.FromCustomDefinition's own RoomIdAt needs, and two devices sharing the
// exact same X/Y (a cockpit's Helm+Navigation pair) works fine too: nothing anywhere requires
// distinct positions, only "inside this room somewhere". ThrustBonus/TurnBonus/OutputBonus/
// CapacityBonus values below are placeholder balance numbers (same spirit as the original 2 shells'
// own prices) - easy to retune later, nothing downstream depends on their exact magnitude.
// Groups the catalog for the client's own category-tab bar (StationBuildPanel) - purely a display
// grouping, nothing server-side branches on it. Structural comes first/"All" conceptually covers
// everything; the rest roughly follow the order a player would reach for them (get power and
// engines sorted before worrying about crew comfort or weapons).
public enum RoomCategory
{
    Structural,
    Power,
    Propulsion,
    Crew,
    Weapons,
    Shields,
    Sensors,
}

public sealed record RoomCatalogEntry(
    string Id, string Name, float Width, float Height, int Price, int PlatingCost, RoomCategory Category,
    IReadOnlyList<CustomDeviceKind>? DeviceKinds = null,
    TurretMountSide MountSide = TurretMountSide.Aft,
    CameraMountSide? CameraSide = null,
    // Engine-kind only (content-каталог) - see CustomDeviceDef's own doc comment: ThrustBonus for a
    // marching-engine room, TurnBonus for an RCS room. Never both nonzero on the same entry.
    float ThrustBonus = 0f,
    float TurnBonus = 0f)
{
    public IReadOnlyList<CustomDeviceKind> Devices => DeviceKinds ?? Array.Empty<CustomDeviceKind>();
}

// The client's own placement request (ClientCommand.BuildRoom). X/Y (content-каталог, click-to-
// place UI) are the exact ship-local position the player chose in StationBuildPanel's own placement
// mode - null falls back to World.ShipBuilding.cs's TryBuildRoom picking a position itself, flush
// against whichever existing room currently reaches furthest (the pre-click-to-place M60 behavior,
// kept as a fallback rather than removed - nothing forces every caller to have a placement UI).
public sealed record BuildRoomRequest(string CatalogId, float? X = null, float? Y = null);

public static class RoomCatalog
{
    // Content-каталог отсеков - balance constants, referenced from World.ShipBuilding.cs's
    // RecomputeDeviceBonuses so the actual per-device contribution lives in exactly one place (here,
    // where the catalog entries that grant it are also defined) rather than being duplicated.
    public const float ReactorRoomBonusOutput = 15f; // matches ReactorOutputBonusPerLevel (World.Upgrades.cs) - one room ~= one station upgrade level
    public const float ShieldRoomCapacityBonus = 50f; // half again of ShieldSystem's own 100 base

    public static IReadOnlyList<RoomCatalogEntry> Entries { get; } = new[]
    {
        new RoomCatalogEntry("empty-small", "Пустой отсек (малый)", 3f, 4f, 150, PlatingCost: 4, Category: RoomCategory.Structural),
        new RoomCatalogEntry("empty-large", "Пустой отсек (большой)", 5f, 6f, 260, PlatingCost: 8, Category: RoomCategory.Structural),

        new RoomCatalogEntry("reactor", "Реакторный отсек", 9f, 9f, 900, PlatingCost: 25, Category: RoomCategory.Power,
            DeviceKinds: new[] { CustomDeviceKind.Reactor }),

        new RoomCatalogEntry("engine-small", "Двигатель маршевый (малый)", 3f, 6f, 220, PlatingCost: 6, Category: RoomCategory.Propulsion,
            DeviceKinds: new[] { CustomDeviceKind.Engine }, ThrustBonus: 5f),

        new RoomCatalogEntry("cockpit-small", "Кокпит (малый)", 9f, 9f, 700, PlatingCost: 20, Category: RoomCategory.Crew,
            DeviceKinds: new[] { CustomDeviceKind.Helm, CustomDeviceKind.Navigation }),

        new RoomCatalogEntry("turret-laser", "Турель лазерная", 3f, 6f, 500, PlatingCost: 10, Category: RoomCategory.Weapons,
            DeviceKinds: new[] { CustomDeviceKind.TurretLaser }, MountSide: TurretMountSide.Aft),

        new RoomCatalogEntry("turret-ballistic", "Турель пушечная", 6f, 6f, 550, PlatingCost: 12, Category: RoomCategory.Weapons,
            DeviceKinds: new[] { CustomDeviceKind.TurretBallistic }, MountSide: TurretMountSide.Aft),

        new RoomCatalogEntry("quarters", "Каюта", 3f, 6f, 150, PlatingCost: 5, Category: RoomCategory.Crew),

        new RoomCatalogEntry("rcs-2way", "Манёвровый двигатель (двусторонний)", 3f, 3f, 180, PlatingCost: 4, Category: RoomCategory.Propulsion,
            DeviceKinds: new[] { CustomDeviceKind.Engine }, TurnBonus: 15f),

        new RoomCatalogEntry("rcs-3way", "Манёвровый двигатель (трёхсторонний)", 3f, 3f, 240, PlatingCost: 5, Category: RoomCategory.Propulsion,
            DeviceKinds: new[] { CustomDeviceKind.Engine }, TurnBonus: 22f),

        new RoomCatalogEntry("engine-big", "Двигатель маршевый (большой)", 6f, 6f, 500, PlatingCost: 14, Category: RoomCategory.Propulsion,
            DeviceKinds: new[] { CustomDeviceKind.Engine }, ThrustBonus: 12f),

        new RoomCatalogEntry("shield-generator", "Генератор щита", 6f, 9f, 650, PlatingCost: 16, Category: RoomCategory.Shields,
            DeviceKinds: new[] { CustomDeviceKind.Shields }),

        new RoomCatalogEntry("bridge-large", "Капитанский мостик (большой)", 12f, 12f, 1400, PlatingCost: 40, Category: RoomCategory.Crew,
            DeviceKinds: new[] { CustomDeviceKind.Helm, CustomDeviceKind.Navigation }),

        new RoomCatalogEntry("rcs-1way", "Манёвровый двигатель (однонаправленный)", 3f, 3f, 130, PlatingCost: 3, Category: RoomCategory.Propulsion,
            DeviceKinds: new[] { CustomDeviceKind.Engine }, TurnBonus: 8f),

        new RoomCatalogEntry("camera", "Камера", 3f, 3f, 120, PlatingCost: 3, Category: RoomCategory.Sensors,
            DeviceKinds: new[] { CustomDeviceKind.Camera }, CameraSide: CameraMountSide.Aft),

        new RoomCatalogEntry("corridor", "Коридор", 3f, 3f, 60, PlatingCost: 2, Category: RoomCategory.Structural),

        // Редактор корабля в духе Cosmoteer (humble-soaring-cat.md) - до этих двух записей каталог
        // никогда не производил Distribution/Oxygen/SuitLocker/StorageRack, а CustomShipValidator
        // требует хотя бы один каждого - корабль, собранный ТОЛЬКО из модулей каталога, физически не
        // мог пройти проверку. Размер/цена - первое приближение по аналогии с остальным каталогом
        // (см. открытый вопрос №1 в плане), не финальный баланс.
        new RoomCatalogEntry("utility-bay", "Служебный отсек", 6f, 6f, 380, PlatingCost: 10, Category: RoomCategory.Power,
            DeviceKinds: new[] { CustomDeviceKind.Distribution, CustomDeviceKind.Oxygen }),

        new RoomCatalogEntry("storage-bay", "Склад", 6f, 6f, 320, PlatingCost: 9, Category: RoomCategory.Crew,
            DeviceKinds: new[] { CustomDeviceKind.SuitLocker, CustomDeviceKind.StorageRack }),
    };

    public static RoomCatalogEntry? Find(string id) => Entries.FirstOrDefault(e => e.Id == id);

    // Which catalog rooms have real hand-drawn reference art (RoomDecor.Catalog.cs, Client-only -
    // the actual textures live there) instead of the generic procedural floor. Kept here too,
    // duplicated by name rather than referenced, because Ship.DeviceObstacles is a SHARED physics
    // rule (the server enforces it for real, not just the client's own rendering) and needs this
    // same fact without depending on Client at all - a reactor room's own "big machine" obstacle
    // only makes sense sized to match a picture that's actually there; every other reactor room
    // (every hand-authored hull, or a custom room the player names differently) gets no such
    // obstacle. Keep this list in sync with RoomDecor.CatalogTextureNames's own room-name column.
    public static readonly IReadOnlySet<string> NamesWithReferenceArt = new HashSet<string>
    {
        "Реакторный отсек", "Кокпит (малый)", "Двигатель маршевый (малый)", "Турель лазерная",
        "Капитанский мостик (большой)", "Турель пушечная", "Каюта", "Манёвровый двигатель (однонаправленный)",
        "Манёвровый двигатель (двусторонний)", "Камера", "Двигатель маршевый (большой)",
        "Манёвровый двигатель (трёхсторонний)", "Генератор щита",
    };

    // Moved here from World.ShipBuilding.cs's own (formerly private) DevicesForCatalogEntry (see the
    // plan's own Cosmoteer-редактор branch, Step 2) - the offline Ship Editor (Client project) needs
    // the exact same "module -> device(s) centred in its own room" logic the in-game Shipwright
    // build path already uses, and neither project can see the other's private server-side code.
    // Every device a catalog entry carries lands at the room's own centre (point-containment is all
    // Ship.FromCustomDefinition's own RoomIdAt needs, and several devices sharing one exact position
    // - a cockpit's Helm+Navigation pair - is harmless). ThrustBonus/TurnBonus only ever apply to the
    // Engine kind, CapacityBonus only to Shields - RoomCatalogEntry never mixes them on one entry.
    // A single-direction RCS room converts to a real ShipEngine too (direct user request - "по его
    // образу сделаем все остальные") - "rcs-2way"/"rcs-3way" don't, see EnginesFor's own doc comment.
    private static bool ConvertsToRealEngine(RoomCatalogEntry entry) => entry.ThrustBonus > 0f || entry.Id == "rcs-1way";

    public static IReadOnlyList<CustomDeviceDef> DevicesFor(RoomCatalogEntry entry, CustomRoomDef room)
    {
        var centerX = room.X + room.Width / 2f;
        var centerY = room.Y + room.Height / 2f;
        return entry.Devices
            // A room that converts to a real ShipEngine (ConvertsToRealEngine) has its own Engine
            // device go through EnginesFor/ShipEngine below instead (direct user request -
            // Cosmoteer-style engines) - keeping both would double-count its own thrust/turn.
            .Where(kind => !(kind == CustomDeviceKind.Engine && ConvertsToRealEngine(entry)))
            .Select(kind => new CustomDeviceDef(kind, centerX, centerY, MountSide: entry.MountSide, CameraSide: entry.CameraSide,
                ThrustBonus: kind == CustomDeviceKind.Engine ? entry.ThrustBonus : 0f,
                TurnBonus: kind == CustomDeviceKind.Engine ? entry.TurnBonus : 0f,
                CapacityBonus: kind == CustomDeviceKind.Shields ? ShieldRoomCapacityBonus : 0f)).ToList();
    }

    // Direct user request (Cosmoteer-style engines, ShipEngine.cs's own doc comment) - a real 3-tile
    // engine for every marching-engine catalog entry (ThrustBonus > 0f: "engine-small"/"engine-big")
    // and for "rcs-1way" (a single straight line of thrusters fits a 3x3 room the same way a marching
    // engine fits its own room). "rcs-2way"/"rcs-3way" deliberately DON'T convert yet - thrusters in
    // more than one direction need more than one straight line of tiles, and a 3x3 room has no clean
    // way to fit two 1x3 engines without them sharing tiles; still the old flat TurnBonus device
    // (DevicesFor above) until that's actually designed, not guessed at here.
    //
    // Control sits 1 tile in from the room's own LEFT edge, Facing West - matches the hand-authored
    // hulls' own "ship travels nose-first along +X" convention (Ship.cs's ForwardDegrees doc
    // comment), so a marching engine's exhaust naturally points aft (and rcs-1way's own single port
    // faces the same way - the player picks WHICH room to place it in for whichever turn direction
    // they actually want, same responsibility an Aft-mounted turret room already carries). Control's
    // own Y lands exactly on a wall-segment CENTER (row index + 0.5, the same convention Ship.cs's
    // GenerateOuterWallBlocks/BuildWallBlocks already place every WallBlock at) so BulkheadPosition
    // (one tile further out, on the room's own left wall) coincides exactly with the WallBlock it
    // needs to replace (Ship.cs's own doc comment on why that WallBlock gets dropped).
    public static IReadOnlyList<CustomEngineDef> EnginesFor(RoomCatalogEntry entry, CustomRoomDef room)
    {
        if (!ConvertsToRealEngine(entry))
            return Array.Empty<CustomEngineDef>();
        var rowIndex = (int)(room.Height / 2f) - 1;
        var rowCenterY = room.Y + rowIndex + 0.5f;
        return entry.Id == "rcs-1way"
            ? new[] { new CustomEngineDef(room.X + 1f, rowCenterY, TileSide.West, entry.TurnBonus, EngineRole.Rcs) }
            : new[] { new CustomEngineDef(room.X + 1f, rowCenterY, TileSide.West, entry.ThrustBonus) };
    }
}
