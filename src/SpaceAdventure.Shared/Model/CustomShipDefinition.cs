namespace SpaceAdventure.Shared.Model;

// A player-drawn hull (in-game Ship Editor) - the moral equivalent of Ship.CreateStarter() etc.,
// but built at runtime from grid placements instead of hand-authored coordinates. Rooms/doors are
// stored as grid-integer geometry and room-id references rather than raw coordinates, so the editor
// only ever has to reason about whole grid cells; Ship.FromCustomDefinition (Ship.Custom.cs) derives
// every actual Room/Door/WallBlock/device the same way the fixed hulls' factories do by hand.
public enum EdgeSide
{
    Top,
    Bottom,
    Left,
    Right,
}

public sealed record CustomRoomDef(string Id, string Name, int X, int Y, int Width, int Height);

// One optional passage on the boundary shared by these two rooms - at most one per room pair,
// centered on whatever range they actually share (Ship.Custom.cs works out where and how big).
public sealed record CustomDoorDef(string RoomAId, string RoomBId);

// One optional outer hull door on a room's side that has no neighboring room at all - the whole
// side stops being breachable hull once this exists (matches the hand-authored hulls' own airlock
// chambers, whose dedicated outer wall never gets ordinary WallBlocks either - see Ship.cs).
public sealed record CustomAirlockDef(string RoomId, EdgeSide Side);

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
    AmmoStorage,
    SuitLocker,
    StorageRack,
    CardTable,
    Jukebox,
}

public sealed record CustomDeviceDef(CustomDeviceKind Kind, float X, float Y, TurretMountSide MountSide = TurretMountSide.Aft);

public sealed record CustomShipDefinition(
    string Name,
    IReadOnlyList<CustomRoomDef> Rooms,
    IReadOnlyList<CustomDoorDef> Doors,
    IReadOnlyList<CustomAirlockDef> Airlocks,
    IReadOnlyList<CustomDeviceDef> Devices,
    float ForwardDegrees)
{
    public static CustomShipDefinition Empty { get; } = new(
        "Мой корабль", Array.Empty<CustomRoomDef>(), Array.Empty<CustomDoorDef>(),
        Array.Empty<CustomAirlockDef>(), Array.Empty<CustomDeviceDef>(), 0f);
}
