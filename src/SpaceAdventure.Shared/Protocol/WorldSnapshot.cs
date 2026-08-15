using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Rooms are resent every tick for now (fixed layout, small data) — full-state sync is fine at
// hobby scale; revisit (delta sync / send-once) per the open question in architecture.md.
public sealed record WorldSnapshot(
    long Tick,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Door> Doors,
    IReadOnlyList<Turret> Turrets,
    IReadOnlyList<TurretState> TurretStates,
    IReadOnlyList<AmmoStorage> AmmoStorages,
    IReadOnlyList<SuitLocker> SuitLockers,
    IReadOnlyList<ToolStation> ToolStations,
    IReadOnlyList<ShipSystemDevice> SystemDevices,
    IReadOnlyList<ShipSystemState> SystemStates,
    ReactorBlock ReactorBlock,
    PowerDistributionBlock DistributionBlock,
    NavigationConsole NavigationConsole,
    IReadOnlyList<GalaxyPoint> GalaxyPoints,
    AirlockConsole AirlockConsole,
    IReadOnlyList<StationNpc> StationNpcs,
    ReactorState Reactor,
    ShieldState Shield,
    IReadOnlyList<WallBlock> WallBlocks,
    IReadOnlyList<WallBlockState> WallBlockStates,
    IReadOnlyList<RoomOxygenState> RoomOxygen,
    EnemyShipState Enemy,
    IReadOnlyList<CharacterState> Characters,
    PowerState Power,
    VoyageState Voyage);
