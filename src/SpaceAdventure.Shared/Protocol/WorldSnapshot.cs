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
    IReadOnlyList<AmmoStorageState> AmmoStorageStates,
    IReadOnlyList<SuitLocker> SuitLockers,
    IReadOnlyList<ShipSystemDevice> SystemDevices,
    IReadOnlyList<ShipSystemState> SystemStates,
    // A Junction box is now its own breakable/movable device (game_design.md - "щитки"), reusing
    // ShipSystemState's exact shape: DeviceId is the Junction's own Component.Id ("junction-oxygen"
    // etc.), System is the one power system it serves. Damaged means its own trunk wire (Distribution
    // -> Junction) is cut - World.Wiring.cs's IsJunctionDamaged - not any downstream device's drop wire.
    IReadOnlyList<ShipSystemState> JunctionStates,
    ReactorBlock ReactorBlock,
    PowerDistributionBlock DistributionBlock,
    BatteryBlock BatteryBlock,
    NavigationConsole NavigationConsole,
    IReadOnlyList<GalaxyPoint> GalaxyPoints,
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
    IReadOnlyList<BotCandidate> RecruitCandidates,
    // The inter-system map (World.StarSystems.cs) - every known system's id/name/galactic position
    // (a valid warp target is any system within GalaxyMap.WarpJumpRadius of the current one - a
    // circle, not a hand-authored edge list, computed client-side straight from GalaxyX/Y), which
    // one the ship is in now, and whether it's out past that system's own warp zone
    // (GalaxyMap.WarpZoneRadius from the field's centre) slowly enough to actually jump - the same
    // "arms the button" pattern CanDock already uses for docking.
    IReadOnlyList<StarSystemSummary> StarSystems,
    string CurrentSystemId,
    bool CanWarpNow,
    // The server's own clock at the moment this snapshot was built (Environment.TickCount64) -
    // clients echo it straight back in their next ClientCommand (LastServerTimestampMs) purely so
    // the server can measure round-trip time off its own clock without needing to synchronize
    // clocks across machines (World.cs's ApplyCommand, CharacterState.PingMs).
    long ServerTimestampMs,
    // Which suit lockers currently have a suit to hand out (World.SuitLockers.cs) - each locker
    // holds exactly one, unlike the old unlimited equip toggle this replaced.
    IReadOnlyList<SuitLockerState> SuitLockerStates,
    // The card table's fixed position (Ship.CardTable) and, whenever a hand of Дурак переводной
    // is actually in progress there, its full state (World.CardGame.cs) - null the rest of the
    // time, the same "unbound session" shape ActiveQuest above already uses.
    CardTable CardTable,
    CardGameState? CardGame,
    // Ship.ForwardDegrees itself, not a ShipKind lookup (ShipCatalog.ForwardDegrees) - a custom
    // hull built in the Ship Editor has no catalog entry to look up, so the renderer needs the
    // real value straight off the live Ship (ShipRenderer.cs).
    float ShipForwardDegrees,
    // The reactor's 3 levers (World.cs) - appended at the end like every other field above, for
    // the same reason (doesn't shift positional args at World.cs's CreateSnapshot call site).
    ReactorLeverState ReactorLevers,
    // The scripted intro campaign's narrative beats reached so far, oldest first (World.Campaign.cs) -
    // plain flavor text over the existing quest/faction/combat systems, not a new mechanic of its
    // own. Shown in InfoPanel's Missions tab alongside the active quest.
    IReadOnlyList<string> StoryLog);
