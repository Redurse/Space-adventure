using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Rooms are resent every tick for now (fixed layout, small data) — full-state sync is fine at
// hobby scale; revisit (delta sync / send-once) per the open question in architecture.md.
// Credits is a single shared crew wallet (game_design.md section 6, M10 economy) rather than
// per-player — matches the co-op framing where the whole crew shares one ship account.
public sealed record WorldSnapshot(
    long Tick,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Door> Doors,
    IReadOnlyList<AirlockOuterDoor> AirlockOuterDoors,
    IReadOnlyList<DoorState> DoorStates,
    IReadOnlyList<Turret> Turrets,
    IReadOnlyList<TurretState> TurretStates,
    IReadOnlyList<AmmoStorage> AmmoStorages,
    IReadOnlyList<SuitLocker> SuitLockers,
    IReadOnlyList<ShipSystemDevice> SystemDevices,
    IReadOnlyList<ShipSystemState> SystemStates,
    ReactorBlock ReactorBlock,
    PowerDistributionBlock DistributionBlock,
    NavigationConsole NavigationConsole,
    IReadOnlyList<GalaxyPoint> GalaxyPoints,
    AirlockConsole AirlockConsole,
    HelmConsole HelmConsole,
    // The ship's cargo shelving (now two per hull, World.ShipPurchase.cs's InitializeRackSlots) and
    // what's currently on it - one flat array covering every shelf, RackFor's own
    // "index / StorageRack.Capacity" is what maps a slot back to which physical shelf it's on.
    IReadOnlyList<StorageRack> StorageRacks,
    IReadOnlyList<ItemType?> RackSlots,
    IReadOnlyList<StationNpc> StationNpcs,
    IReadOnlyList<StationCrate> StationCrates,
    IReadOnlyList<StationCrateState> StationCrateStates,
    IReadOnlyList<StationGuardState> StationGuards,
    IReadOnlyList<Room> StationRooms,
    IReadOnlyList<Door> StationDoors,
    AirlockOuterDoor StationShipConnector,
    Vec2 StationPosition,
    // StationRooms/Doors/Npcs/Crates are all in the *docked* frame - the ship's own interior
    // coordinates - so a docked station needs no conversion to draw. Add this to get field/world
    // coordinates instead, which is what the exterior view and the radar plot in.
    Vec2 StationWorldOffset,
    // The mouth of the berth, the hull position that mates with it, and whether the ship is
    // currently parked there well enough to dock (World.StationDocking.cs) - the last is what arms
    // the helm's "Стыковка" button.
    Vec2 StationDockingPortPosition,
    Vec2 DockBerthPosition,
    bool CanDock,
    IReadOnlyList<Room> EnemyShipRooms,
    IReadOnlyList<Door> EnemyShipDoors,
    AirlockOuterDoor EnemyShipBoardingHatch,
    // Which hull class is in front of the guns right now, and the air left in each of its
    // compartments - the boarding party fights by both (World.EnemyAtmosphere.cs).
    string EnemyShipClassName,
    IReadOnlyList<RoomOxygenState> EnemyRoomOxygen,
    // The boardable enemy's position. EnemyShips is the whole squadron out there right now, each
    // with its own place in the field (World.EnemyFleet.cs).
    Vec2 EnemyShipPosition,
    IReadOnlyList<EnemyShipFieldState> EnemyShips,
    IReadOnlyList<ProjectileState> Projectiles,
    IReadOnlyList<EnemyCrewState> EnemyCrew,
    // Bullets and bolts from personal weapons, in flight (World.PersonalShots.cs).
    IReadOnlyList<PersonalShotState> PersonalShots,
    IReadOnlyList<FactionStandingState> FactionStandings,
    ShipKind CurrentShipKind,
    ReactorState Reactor,
    ShieldState Shield,
    IReadOnlyList<WallBlock> WallBlocks,
    IReadOnlyList<WallBlockState> WallBlockStates,
    IReadOnlyList<RoomOxygenState> RoomOxygen,
    EnemyShipState Enemy,
    IReadOnlyList<CharacterState> Characters,
    PowerState Power,
    VoyageState Voyage,
    int Credits,
    Quest? ActiveQuest,
    IReadOnlyDictionary<ShipUpgradeTrack, int> ShipUpgradeLevels,
    IReadOnlyList<Component> Components,
    IReadOnlyList<ComponentState> ComponentStates,
    IReadOnlyList<Wire> Wires,
    IReadOnlyList<WireState> WireStates,
    IReadOnlyList<ComponentMount> ComponentMounts,
    IReadOnlyList<ComponentMountState> ComponentMountStates,
    IReadOnlyList<Asteroid> Asteroids,
    IReadOnlyList<OreDeposit> OreDeposits,
    IReadOnlyList<OreDepositState> OreDepositStates,
    IReadOnlyList<DroppedItem> DroppedItems,
    ShipFieldState ShipField,
    // Who's currently on offer at the docked station's Recruiter, if it has one (World.Recruiting.cs,
    // game_design.md section 10) - empty away from a Recruiter or undocked.
    IReadOnlyList<BotCandidate> RecruitCandidates);
