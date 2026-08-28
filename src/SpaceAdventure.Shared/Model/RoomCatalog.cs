namespace SpaceAdventure.Shared.Model;

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
    };

    public static RoomCatalogEntry? Find(string id) => Entries.FirstOrDefault(e => e.Id == id);
}
