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
    // The docked station's own rooms/doors/NPCs/crates/wall blocks (StationSnapshot.cs) - grouped
    // together since StationRenderer/StationPanel/the radar blip are the only readers and always
    // want the whole thing, never one field of it in isolation.
    StationSnapshot Station,
    // The mouth of the berth, the hull position that mates with it, and whether the ship is
    // currently parked there well enough to dock (World.StationDocking.cs) - the last is what arms
    // the helm's "Стыковка" button. Ship-side/approach-physics concerns (HelmPanel/FieldRenderer's
    // exterior view), not station-interior content, so these stay out of StationSnapshot itself.
    Vec2 DockBerthPosition,
    bool CanDock,
    // The boardable enemy hull's own interior/crew/position (EnemyShipSnapshot.cs) - grouped the
    // same way Station/AsteroidField are; BoardingRenderer always wants the whole thing together.
    EnemyShipSnapshot EnemyShip,
    IReadOnlyList<ProjectileState> Projectiles,
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
    // The asteroid field's own rocks/ore veins (AsteroidFieldSnapshot.cs) - grouped the same way
    // Station's fields are, since FieldRenderer/HelmPanel's radar/EffectTracker always want all
    // three together.
    AsteroidFieldSnapshot Field,
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
    IReadOnlyList<string> StoryLog,
    // The "ОБУЧЕНИЕ" run's current instruction (World.Tutorial.cs), null outside it entirely - the
    // client just draws a persistent top banner whenever this is non-null.
    string? TutorialObjective,
    // Persistent ambient traffic in the current system (World.NpcShips.cs, M43) - cargo/military/
    // scout hulls that fly around whether or not the player has ever come near them.
    IReadOnlyList<NpcShipFieldState> NpcShips,
    // Manually placed by a Scientist promoting one of their own private ScannerContacts onto the
    // shared map (World.Scanner.cs, M44's "чтобы капитан увидел метку... учёный должен сам её туда
    // поставить") - unlike ScannerContacts these are the same for every player, plain field-space
    // points with no further identity (the discovery itself is what mattered; the marker is just a
    // pin left behind for the rest of the crew).
    IReadOnlyList<Vec2> ManualScannerMarkers);
