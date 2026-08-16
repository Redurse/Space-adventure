using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Simulation root, split into topic partials: World.Movement.cs, World.Combat.cs,
// World.Interact.cs, World.Voyage.cs, World.EnemyAi.cs, World.Atmosphere.cs, World.Reactor.cs.
public sealed partial class World
{
    public long Tick { get; set; }
    // Replaced wholesale when the crew buys a different hull at a station's Shipwright
    // (World.ShipPurchase.cs) - everything keyed off it (turret runtimes, room oxygen, door
    // states, breaches) gets rebuilt at the same time.
    public Ship Ship { get; private set; }
    public PowerGrid PowerGrid { get; } = new();
    public ShieldSystem Shield { get; } = new();
    // Enemy is a computed view over the squadron actually in the field (World.EnemyFleet.cs).
    // One prebuilt layout per station kind (game_design.md section 10 - stations differ by which
    // services they offer). Which one Station resolves to depends on where the ship currently is,
    // so walking around "the station" means walking around whichever one you docked at.
    // Rebuilt whenever Ship is replaced: the layouts are anchored on the ship's own outer airlock
    // door so a docked station lines up with it exactly (World.StationDocking.cs), and a different
    // hull puts that door somewhere else. Room/door ids don't depend on the anchor, so the flat
    // door-state dictionary keyed by id survives the rebuild untouched.
    private Dictionary<StationKind, Station> _stationsByKind = new();

    private void RebuildStationLayouts()
    {
        var anchor = Ship.AirlockOuterDoors.First().Position;
        _stationsByKind = Enum.GetValues<StationKind>().ToDictionary(k => k, k => Station.Create(k, anchor));
    }

    public Station Station => _stationsByKind[CurrentStationKind];

    // Falls back to the destination while still flying in (the approach phase already needs the
    // right hull drawn ahead of it), then to the home station's kind when neither applies.
    private StationKind CurrentStationKind
    {
        get
        {
            var pointId = _dockedPointId ?? _travelTargetPointId;
            if (pointId is not null && GalaxyMap.Points.FirstOrDefault(p => p.Id == pointId) is { Kind: GalaxyPointKind.Station } point)
                return point.StationKind;
            return StationKind.Outpost;
        }
    }
    public AsteroidField AsteroidField { get; } = AsteroidField.CreateDefault();
    public VoyagePhase Phase { get; private set; } = VoyagePhase.Station;

    private readonly Dictionary<int, Character> _characters = new();
    private readonly Dictionary<int, Vec2> _moveInput = new();
    private readonly Dictionary<string, TurretRuntime> _turretRuntimes;
    private readonly Dictionary<string, float> _turretAimInput = new();
    private readonly HashSet<string> _breachedWallBlockIds = new();

    public ShipKind CurrentShipKind { get; private set; }

    // ShipKind defaults to the original M2 layout (Frigate) so every pre-existing `new World()`
    // call (the entire test suite) keeps compiling and behaving exactly as before — ship
    // selection (game_design.md section 9) is purely additive.
    public World(ShipKind shipKind = ShipKind.Frigate)
    {
        CurrentShipKind = shipKind;
        Ship = Ship.Create(shipKind);
        _turretRuntimes = Ship.Turrets.ToDictionary(t => t.Id, t => new TurretRuntime(t));
        InitializeShipState();
        // Every station kind's doors, not just the one currently resolved - door state is one flat
        // dictionary across all structures, and which station Station resolves to changes as the
        // ship travels (see CurrentStationKind).
        foreach (var door in _stationsByKind.Values.SelectMany(s => s.Doors))
            _doorOpen[door.Id] = true; // station is safe - no reason to ever close these
        // Every enemy hull class, not only the one currently in front of the guns - which ship of
        // the squadron is boardable changes mid-fight (World.EnemyFleet.cs), same as the station
        // above changes as the ship travels.
        foreach (var layout in EnemyShipLayout.All)
        {
            // Closed: a crew that has just been boarded seals its compartments, and opening one is
            // a decision with a cost now that the hull leaks air (World.EnemyAtmosphere.cs).
            foreach (var door in layout.Doors)
                _doorOpen[door.Id] = false;
            _doorOpen[layout.BoardingHatch.Id] = true; // it's a hull breach, not a working door
        }
        ResetEnemyCrew();
        foreach (var deposit in AsteroidField.OreDeposits)
            _oreDepositHp[deposit.Id] = deposit.MaxHp;

        var home = GalaxyMap.GetPoint(GalaxyMap.HomePointId);
        _shipMapPosition = home.Position;
        _dockedPointId = home.Id;
        // A fresh run starts docked, which is itself a save point (game_design.md section 5) -
        // set directly rather than via EnterStation, whose refuel/repair pass is meaningless on a
        // ship that hasn't flown yet.
        AutosavePending = true;
    }

    public void SpawnCharacter(int playerId) => _characters[playerId] = new Character(playerId, Ship.SpawnPoint, Ship.SpawnRoomId);

    public void ApplyCommand(int playerId, ClientCommand command)
    {
        _moveInput[playerId] = new Vec2(command.MoveX, command.MoveY);
        _characters[playerId].LookDirection = new Vec2(command.LookX, command.LookY);
        PowerGrid.ApplyInput(command.PowerSystemIndex, command.PowerDirection);

        var character = _characters[playerId];

        if (command.InteractPressed)
            HandleInteract(character);

        if (command.ToggleHoldSlotIndex >= 0)
            character.Inventory.ToggleHold(command.ToggleHoldSlotIndex);

        if (command.MoveItemFrom is { } moveFrom && command.MoveItemTo is { } moveTo)
            TryMoveItem(character, moveFrom, moveTo);

        if (command.AttachTankFromSlot is { } tankFrom && command.AttachTankToSlot is { } tankTo)
            TryAttachTank(character, tankFrom, tankTo);

        if (command.DetachTankSlot is { } detachSlot)
            TryDetachTank(character, detachSlot);

        // Held rather than edge-triggered: the flame burns while the button is down, so this is
        // state to remember for the tick, not an action to perform on the spot.
        _cutInput[playerId] = command.CutHeld;

        if (command.ToggleReactorSlotIndex >= 0)
            ToggleReactorSlot(character, command.ToggleReactorSlotIndex);

        if (command.TravelToPointId is not null)
            TryStartTravel(command.TravelToPointId);

        if (command.BuyItemType is { } buyItemType)
            TryBuyItem(character, buyItemType);

        if (command.SellSlotIndex >= 0)
            TrySellItem(character, command.SellSlotIndex);

        if (command.AcceptCargoQuestPressed)
            TryAcceptQuest(command.AcceptQuestKind);

        if (command.TurnInCargoQuestPressed)
            TryTurnInQuest(character);

        if (command.PurchaseUpgradeTrack is { } upgradeTrack)
            TryPurchaseUpgrade(upgradeTrack);

        if (command.PurchaseShipKind is { } shipKindToBuy)
            TryPurchaseShip(shipKindToBuy);

        if (command.DockPressed)
            TryDockAtStation();

        if (command.WireLinkInteractId is { } wireLinkId)
            HandleWireLinkInteract(character, wireLinkId);

        if (command.DoorToggleId is { } doorId)
            ToggleDoor(doorId);

        if (command.PushOffPressed)
            HandlePushOff(character, new Vec2(command.PushOffDirectionX, command.PushOffDirectionY));

        if (character.IsAtHelm)
        {
            if (command.HelmStabilizePressed)
                EngageAutoStabilize();
            else
                SetHelmInput(command.HelmThrottle, command.HelmTurn); // zero is a real "hands off the controls" state, not "no input" - it still overwrites what was commanded
        }

        // Space means "fire the turret" at a periscope and "fire the held weapon" while boarding -
        // never both, since the two are mutually exclusive places to be (World.Boarding.cs).
        // Anywhere on foot: aboard your own ship there is nothing to hit, but a weapon that only
        // works in someone else’s hull is one you can never practise with.
        if (command.FirePressed && !character.IsOutside && character.ManningTurretId is null && !character.IsAtHelm)
            TryFirePersonalWeapon(character);

        if (character.ManningTurretId is { } turretId)
        {
            _turretAimInput[turretId] = command.TurretAimDirection;
            if (command.FirePressed)
                TryFire(_turretRuntimes[turretId]);
        }
    }

    public void Step(double deltaSeconds)
    {
        StepCharacters(deltaSeconds);
        StepTurrets(deltaSeconds);
        StepCutting(deltaSeconds);
        StepPersonalShots(deltaSeconds);
        StepOxygenTanks(deltaSeconds);
        StepBoarding(deltaSeconds);
        StepStationCrime(deltaSeconds);
        // Fleet before voyage so the enemy moves and shoots on the same tick the shells advance,
        // and voyage's loss check sees this tick's damage rather than the previous one's.
        StepEnemyFleet(deltaSeconds);
        StepProjectiles(deltaSeconds);
        StepVoyage(deltaSeconds);
        StepAtmosphere(deltaSeconds);
        StepInjuries(deltaSeconds);
        PowerGrid.Step(deltaSeconds);
        Shield.Step(deltaSeconds, GetEffectivePower(PowerSystemId.Shields));
    }

    public WorldSnapshot CreateSnapshot() => new(
        Tick,
        Ship.Rooms,
        Ship.Doors,
        Ship.AirlockOuterDoors,
        CreateDoorStates(),
        Ship.Turrets,
        _turretRuntimes.Values.Select(t => new TurretState(
            t.Definition.Id, t.AimDegrees, t.MannedByPlayerId, t.CooldownRemaining,
            t.AmmoRemaining, t.Definition.MagazineCapacity, t.Charge, t.Definition.MaxCharge, t.Damaged)).ToArray(),
        Ship.AmmoStorages,
        Ship.SuitLockers,
        Ship.ToolStations,
        Ship.SystemDevices,
        Ship.SystemDevices.Select(d => new ShipSystemState(d.Id, d.System, !IsDeviceConnected(d.Id))).ToArray(),
        Ship.ReactorBlock,
        Ship.DistributionBlock,
        Ship.NavigationConsole,
        GalaxyMap.Points,
        Ship.AirlockConsole,
        Ship.WiringTerminal,
        Ship.HelmConsole,
        Ship.StorageRack,
        RackSlots,
        Station.Npcs,
        Station.Crates,
        CreateStationCrateStates(),
        CreateStationGuardStates(),
        Station.Rooms,
        Station.Doors,
        Station.ShipConnector,
        Station.Position,
        Station.WorldOffset,
        Station.DockingPortPosition,
        DockBerthPosition,
        CanDockNow,
        EnemyShipLayout.Rooms,
        EnemyShipLayout.Doors,
        EnemyShipLayout.BoardingHatch,
        EnemyShipLayout.Name,
        CreateEnemyRoomOxygenStates(),
        EnemyShipFieldPosition,
        CreateEnemyShipStates(),
        CreateProjectileStates(),
        CreateEnemyCrewStates(),
        CreatePersonalShotStates(),
        CreateFactionStandings(),
        CurrentShipKind,
        new ReactorState(
            PowerGrid.Reactor.Rods.Select(charge => charge / PowerGrid.Reactor.RodCapacity).ToArray(),
            PowerGrid.Reactor.Fuel,
            PowerGrid.Reactor.MaxFuel,
            PowerGrid.Reactor.CurrentOutput,
            PowerGrid.Reactor.MaxOutput),
        new ShieldState(Shield.Points, ShieldSystem.MaxPoints),
        Ship.WallBlocks,
        Ship.WallBlocks.Select(b => new WallBlockState(b.Id, _breachedWallBlockIds.Contains(b.Id))).ToArray(),
        Ship.Rooms.Select(r => new RoomOxygenState(r.Id, _roomOxygen[r.Id])).ToArray(),
        new EnemyShipState(Enemy.Hp, Enemy.MaxHp, Enemy.IsRetreating, _remainingEnemyShips),
        _characters.Values.Select(c =>
        {
            // While outside, X/Y mean an absolute AsteroidField world position instead of ship-
            // interior coordinates - the client picks which "scene" to place them in from IsOutside.
            var renderPosition = c.IsOutside ? GetEvaWorldPosition(c) : c.Position;
            return new CharacterState(
                c.PlayerId, renderPosition.X, renderPosition.Y, c.CarryingAmmoCrate, c.Health, c.WearingSuit, c.SuitActionRemaining,
                // What the client draws the sight cone along: the head if it's aimed, otherwise
                // whichever way the body last walked.
                c.LookDirection != Vec2.Zero ? c.LookDirection.X : c.FacingDirection.X,
                c.LookDirection != Vec2.Zero ? c.LookDirection.Y : c.FacingDirection.Y,
                new InventoryState(
                    c.Inventory.MainSlots.ToArray(),
                    new Dictionary<EquipSlot, ItemType?>(c.Inventory.Equipped),
                    c.Inventory.HeldSlotIndices.ToArray(),
                    c.Inventory.MainSlotTanks.ToArray(),
                    c.Inventory.TankCharge(Inventory.WornSuitSlot)),
                c.IsBleeding, c.IsAtHelm, c.IsOutside, c.JetpackFuel, c.EvaAttachedTo != EvaAttachment.None, c.OnStation, c.OnEnemyShip,
                c.Inventory.TankCharge(Inventory.WornSuitSlot),
                c.Inventory.HeldSlotOf(ItemType.Cutter) is var cutterSlot && cutterSlot >= 0
                    ? c.Inventory.TankCharge(cutterSlot)
                    : null,
                IsCutting(c.PlayerId));
        }).ToArray(),
        PowerGrid.CreateState(),
        new VoyageState(Phase, _shipMapPosition, _dockedPointId, _travelTargetPointId),
        Credits,
        ActiveQuest,
        new Dictionary<ShipUpgradeTrack, int>(UpgradeLevels),
        WireNetwork.Nodes,
        WireNetwork.Links,
        CreateWireLinkStates(),
        AsteroidField.Asteroids,
        AsteroidField.OreDeposits,
        CreateOreDepositStates(),
        _droppedItems.ToArray(),
        new ShipFieldState(
            _shipFieldPosition.X, _shipFieldPosition.Y, _shipRotationDegrees,
            _shipVelocity.X, _shipVelocity.Y, _shipThrust.X, _shipThrust.Y, _shipAutoStabilize));
}
