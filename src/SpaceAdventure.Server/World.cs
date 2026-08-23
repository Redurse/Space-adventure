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
    // One generated instance per station actually visited so far this session (M49 - every station
    // now gets its own procedural shape, Station.Procedural.cs), cached forever once built:
    // generation is a pure, cheap function of the point's own id, so even a long playthrough that
    // visits most of the galaxy caches at most a couple hundred small Station objects. Replaces the
    // old "one shared instance per StationKind, repositioned to whichever same-kind point is
    // nearest" model, which made every station of a given kind literally the same object.
    private readonly Dictionary<string, Station> _stationsByPointId = new();

    public Station Station => GetOrCreateStation(_dockedPointId ?? _nearestStationPointId ?? GalaxyMap.HomePointId);

    private Station GetOrCreateStation(string pointId)
    {
        if (_stationsByPointId.TryGetValue(pointId, out var existing))
            return existing;

        // Anchored on the ship's own outer airlock door so a docked station lines up with it
        // exactly (World.StationDocking.cs) - a hull swap moves that door, which is why
        // RebuildStationLayouts (World.ShipPurchase.cs) clears this cache instead of leaving stale
        // instances anchored to a door that no longer exists there. The station's own SHAPE stays
        // fixed forever (seeded from pointId alone, Station.Procedural.cs) - only this anchor
        // translation gets redone.
        var kind = GalaxyMap.GetPoint(pointId).StationKind;
        var anchor = Ship.AirlockOuterDoors.First().Position;
        var station = Station.CreateProcedural(pointId, kind, anchor);
        _stationsByPointId[pointId] = station;
        // Every station's doors, not just the one currently resolved - door state is one flat
        // dictionary across all structures in the game. TryAdd rather than a raw assignment so
        // re-generating an already-visited point after a hull swap can't stomp a door a player had
        // genuinely closed (never happens today - a station is always safe, nobody has a reason to
        // close one - but costs nothing to guard against).
        foreach (var door in station.Doors)
            _doorOpen.TryAdd(door.Id, true);
        return station;
    }

    // Called from World.ShipPurchase.cs's InitializeShipState on every hull swap (and once from
    // this constructor) - every cached station is anchored to the OLD hull's airlock position, so
    // simply dropping them all is enough: the next access to Station lazily regenerates whichever
    // one is actually needed, anchored to the new hull instead.
    private void RebuildStationLayouts() => _stationsByPointId.Clear();
    // Which system's field is "the" field right now (World.StarSystems.cs) - a computed lookup
    // rather than a stored instance, so every existing reader (World.ShipField.cs, Cutting.cs,
    // Eva.cs, Quests.cs, Projectiles.cs, CrewAi.cs) keeps working unchanged even though there's no
    // longer a single field for the whole game.
    public AsteroidField AsteroidField => GalaxyMap.GetSystem(_currentSystemId).Field;

    // Replaces the old VoyagePhase enum (M39): docking/combat/mining are all continuous, proximity-
    // driven states now rather than an exclusive mode the ship is "in", so there is no single flag
    // left to switch on - just these two independent, overlappable facts about where the ship is.
    public bool IsDocked => _dockedPointId is not null;
    // Either a hostile sector/station's squadron (World.Voyage.cs) or a persistent Military NPC
    // that turned hostile and closed the distance (World.NpcShips.cs) - the two are mutually
    // exclusive, never both set at once.
    public bool IsInBattle => _battleSectorPointId is not null || _battleNpcShipId is not null;

    // What the galaxy map actually plots as the ship's marker. While docked, _shipFieldPosition
    // holds DockBerthPosition - a field-space point anchored to the ship's OWN airlock door
    // (World.StationDocking.cs), used purely to line up the ship's and station's interiors for
    // walking between them. That point has no relationship to the docked GalaxyPoint's real
    // position on the map (e.g. home-station sits at (35,141) in Sol), so plotting it directly
    // put the marker floating off in open space instead of on the station it's actually docked
    // at. Undocked, _shipFieldPosition is already real GalaxyPoint-space, so only the docked case
    // needs to substitute the docked point's own position instead.
    private Vec2 ShipMapPosition =>
        _dockedPointId is { } dockedId ? GalaxyMap.GetPoint(dockedId).Position : _shipFieldPosition;

    private readonly Dictionary<int, Character> _characters = new();
    private readonly Dictionary<int, Vec2> _moveInput = new();
    private readonly Dictionary<string, TurretRuntime> _turretRuntimes;
    private readonly Dictionary<string, float> _turretAimInput = new();

    public ShipKind CurrentShipKind { get; private set; }

    // The reactor's other two physical levers (its own EmergencyShutdown is the third, kept on
    // Reactor itself since it's genuinely reactor state). Both default to the ship's normal
    // operating state so a crew that never touches these levers sees no behavior change at all.
    public bool LightsOn { get; private set; } = true;
    public bool DoorsLocked { get; private set; } = false;

    // The jukebox's on/off + selected track + volume - meaningless while Ship.Jukebox is null
    // (no such device on this hull), same as every other block-specific state above.
    public bool JukeboxOn { get; private set; } = false;
    public int JukeboxTrackIndex { get; private set; } = 0;
    public int JukeboxVolume { get; private set; } = 50;

    // Retained only so CreateSave() can round-trip a Custom hull - null whenever flying a fixed
    // class. Set here and in ApplySave, the only two places CurrentShipKind can become Custom.
    private CustomShipDefinition? _customShipDefinition;

    // ShipKind defaults to the original M2 layout (Frigate) so every pre-existing `new World()`
    // call (the entire test suite) keeps compiling and behaving exactly as before — ship
    // selection (game_design.md section 9) is purely additive. customShip is required exactly
    // when shipKind is Custom (Ship Editor - Ship.Custom.cs); ignored otherwise.
    public World(ShipKind shipKind = ShipKind.Frigate, CustomShipDefinition? customShip = null)
    {
        // Set before anything below touches AsteroidField (which resolves through it) - a fresh
        // crew always starts in whichever system the home station actually sits in.
        _currentSystemId = GalaxyMap.SystemOf(GalaxyMap.HomePointId).Id;
        CurrentShipKind = shipKind;
        _customShipDefinition = shipKind == ShipKind.Custom ? customShip : null;
        Ship = shipKind == ShipKind.Custom ? Ship.FromCustomDefinition(customShip!) : Ship.Create(shipKind);
        _turretRuntimes = Ship.Turrets.ToDictionary(t => t.Id, t => new TurretRuntime(t));
        InitializeShipState();
        // Every enemy hull class, not only the one currently in front of the guns - which ship of
        // the squadron is boardable changes mid-fight (World.EnemyFleet.cs), same as the station
        // above changes as the ship travels.
        foreach (var layout in EnemyShipLayout.All)
        {
            // Closed: a crew that has just been boarded seals its compartments, and opening one is
            // a decision with a cost now that the hull leaks air (World.EnemyAtmosphere.cs). The
            // hull's own AirlockOuterDoors are locked hatches now too - cutting one open (or a wall
            // panel instead) is tracked per hull instance (EnemyShipRuntime), not in this shared
            // dictionary, so they get no entry here at all.
            foreach (var door in layout.Doors)
                _doorOpen[door.Id] = false;
        }
        ResetEnemyCrew();
        foreach (var deposit in AsteroidField.OreDeposits)
            _oreDepositHp[deposit.Id] = deposit.MaxHp;

        var home = GalaxyMap.GetPoint(GalaxyMap.HomePointId);
        _dockedPointId = home.Id;
        // Anchored to the home point's own map position (Station.RepositionTo, same as any other
        // arrival - World.Voyage.cs's Arrive) rather than left at Station.Default.cs's fixed build
        // spot, so a fresh crew's very first "docked" position already matches where the map says
        // home is.
        Station.RepositionTo(home.Position);
        _shipFieldPosition = DockBerthPosition;
        // A fresh run starts docked, which is itself a save point (game_design.md section 5) -
        // set directly rather than via EnterStation, whose refuel/repair pass is meaningless on a
        // ship that hasn't flown yet.
        AutosavePending = true;
        RegenerateRecruitRoster();
    }

    public void SpawnCharacter(int playerId) => _characters[playerId] = new Character(playerId, Ship.SpawnPoint, Ship.SpawnRoomId);

    // A player left the session (GameServer.Tick). Everything keyed by their id goes with them,
    // including a seat they were occupying - a turret nobody is sitting at must not stay manned by
    // a crew member who is no longer aboard.
    public void RemoveCharacter(int playerId)
    {
        _characters.Remove(playerId);
        _moveInput.Remove(playerId);
        _cutInput.Remove(playerId);
        _weldInput.Remove(playerId);
        _weaponCooldowns.Remove(playerId);
        _stolenItemCount.Remove(playerId);
        foreach (var runtime in _turretRuntimes.Values.Where(t => t.MannedByPlayerId == playerId))
        {
            runtime.MannedByPlayerId = null;
            _turretAimInput.Remove(runtime.Definition.Id); // otherwise the barrel keeps swinging by itself
        }
        // A hand of Дурак переводной can't continue solo any more than a turret can stay manned
        // by someone who just left.
        if (_cardGame is { } cardGame && (cardGame.Player1Id == playerId || cardGame.Player2Id == playerId))
            _cardGame = null;
    }

    public void ApplyCommand(int playerId, ClientCommand command)
    {
        // A command can outlive its sender by a tick - the socket dies between the client's last
        // send and the server draining the queue.
        if (!_characters.ContainsKey(playerId))
            return;

        _moveInput[playerId] = new Vec2(command.MoveX, command.MoveY);
        _characters[playerId].LookDirection = new Vec2(command.LookX, command.LookY);
        PowerGrid.ApplyInput(playerId, command.PowerSystemIndex, command.PowerDirection);

        var character = _characters[playerId];
        ObserveTutorialInput(character, command);

        // Sent every tick once the client knows it - ignore an empty/missing one rather than
        // overwrite an already-known name with nothing (e.g. a stray command that raced ahead of
        // the menu's own first send).
        if (!string.IsNullOrEmpty(command.Nickname))
            character.Nickname = command.Nickname;
        // A live player's own role is a self-identification label only (unlike a hired bot's,
        // World.CrewAi.cs never reads it) - no docked/proximity gate needed, same as Nickname above.
        if (command.SetOwnRoleTo is { } roleToSet)
            character.Role = roleToSet;
        else if (command.ClearOwnRolePressed)
            character.Role = null;

        if (command.PlayCardRank is { } cardRank && command.PlayCardSuit is { } cardSuit)
            TryPlayCard(character, cardRank, cardSuit);
        if (command.CardGameTakePressed)
            TryCardGameTake(character);
        if (command.CardGameEndRoundPressed)
            TryCardGameEndRound(character);
        // 0 means "no snapshot seen yet" (WorldSnapshot.ServerTimestampMs's own doc comment) -
        // nothing to measure a round trip against on that first tick.
        if (command.LastServerTimestampMs > 0)
            character.PingMs = Math.Max(0, Environment.TickCount64 - command.LastServerTimestampMs);

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
        _weldInput[playerId] = command.WeldHeld;

        if (command.ToggleReactorSlotIndex >= 0)
            ToggleReactorSlot(character, command.ToggleReactorSlotIndex);

        // The 3 reactor levers (game_design.md - drawn on the reactor block itself): a physical
        // interaction, so proximity-checked like ToggleReactorSlot rather than trusted client-side
        // like the panel clicks (DoorToggleId etc.) below.
        if ((command.ToggleLightsPressed || command.ToggleReactorEmergencyPressed || command.ToggleDoorsLockedPressed) &&
            (Ship.ReactorBlock.Position - character.Position).Length() < InteractionRadius)
        {
            if (command.ToggleLightsPressed)
                LightsOn = !LightsOn;
            if (command.ToggleReactorEmergencyPressed)
                PowerGrid.Reactor.EmergencyShutdown = !PowerGrid.Reactor.EmergencyShutdown;
            if (command.ToggleDoorsLockedPressed)
                DoorsLocked = !DoorsLocked;
        }

        // Dev cheat panel (Ё key, Game1.cs's CheatPanel) - no proximity/role gate, it's a testing
        // tool for hit resolution, not a game action.
        if (command.DebugSpawnEnemyPressed)
            DebugSpawnEnemyNearby();

        // The jukebox's checkbox and two steppers (JukeboxPanel) - same physical, proximity-checked
        // treatment as the reactor levers above, gated on Ship.Jukebox actually existing since a
        // hull built without one in the Ship Editor has nothing here to walk up to.
        if (Ship.Jukebox is { } jukeboxBlock &&
            (command.JukeboxTogglePressed || command.JukeboxNextTrackPressed || command.JukeboxPrevTrackPressed ||
             command.JukeboxVolumeUpPressed || command.JukeboxVolumeDownPressed) &&
            (jukeboxBlock.Position - character.Position).Length() < InteractionRadius)
        {
            if (command.JukeboxTogglePressed)
                JukeboxOn = !JukeboxOn;
            if (command.JukeboxNextTrackPressed)
                JukeboxTrackIndex = (JukeboxTrackIndex + 1) % JukeboxCatalog.TrackCount;
            if (command.JukeboxPrevTrackPressed)
                JukeboxTrackIndex = (JukeboxTrackIndex - 1 + JukeboxCatalog.TrackCount) % JukeboxCatalog.TrackCount;
            if (command.JukeboxVolumeUpPressed)
                JukeboxVolume = Math.Min(100, JukeboxVolume + 5);
            if (command.JukeboxVolumeDownPressed)
                JukeboxVolume = Math.Max(0, JukeboxVolume - 5);
        }

        if (command.BuyItemType is { } buyItemType)
            TryBuyItem(character, buyItemType);

        if (command.SellSlotIndex >= 0)
            TrySellItem(character, command.SellSlotIndex);

        if (command.AcceptCargoQuestPressed)
            TryAcceptQuest(command.AcceptQuestKind);

        if (command.TurnInCargoQuestPressed)
            TryTurnInQuest(character);

        if (command.AbandonQuestPressed)
            TryAbandonQuest();

        if (command.WarpToSystemId is not null)
            TryWarpTo(command.WarpToSystemId);

        if (command.PurchaseUpgradeTrack is { } upgradeTrack)
            TryPurchaseUpgrade(upgradeTrack);

        if (command.PurchaseShipKind is { } shipKindToBuy)
            TryPurchaseShip(shipKindToBuy);

        if (command.DockPressed)
            HandleDockButtonPressed();

        if (command.DoorToggleId is { } doorId)
            ToggleDoor(doorId);

        if (command.HireCandidateId is { } candidateId)
            TryHireCandidate(candidateId);

        if (command.PinInteractId is { } pinRef)
            HandlePinInteract(character, pinRef);

        if (command.WireLayCancelPressed)
            HandleWireLayCancel(character);

        if (command.WireBendAtX is { } wireBendX && command.WireBendAtY is { } wireBendY)
            HandleWireBend(character, new Vec2(wireBendX, wireBendY));

        if (command.ComponentOperateId is { } operateId)
            ToggleRelay(operateId);

        if (command.ComponentMountInteractId is { } mountId)
            HandleComponentMountInteract(character, mountId);

        if (command.SabotageDeviceId is { } sabotageId)
            HandleSabotageDevice(character, sabotageId);

        if (command.DropItemFrom is { } dropFrom)
            TryDropItem(character, dropFrom);

        if (command.PickupDroppedItemId is { } pickupId)
            TryPickupDroppedItem(character, pickupId);

        if (command.PushOffPressed)
            HandlePushOff(character, new Vec2(command.PushOffDirectionX, command.PushOffDirectionY));

        HandleScannerInput(character, command);

        if (character.IsAtHelm && !HelmConsoleBroken)
        {
            if (command.HelmStabilizePressed)
                EngageAutoStabilize();
            else
                SetHelmInput(command.HelmThrottle, command.HelmTurn); // zero is a real "hands off the controls" state, not "no input" - it still overwrites what was commanded

            if (command.ToggleControlModePressed)
                ToggleControlMode();
        }

        // Space means "fire the turret" at a periscope and "fire the held weapon" while boarding -
        // never both, since the two are mutually exclusive places to be (World.Boarding.cs).
        // Anywhere on foot: aboard your own ship there is nothing to hit, but a weapon that only
        // works in someone else’s hull is one you can never practise with.
        if (command.FirePressed && !character.IsOutside && character.ManningTurretId is null && !character.IsAtHelm)
            TryFirePersonalWeapon(character);

        // LMB, not Space - the axe swings like the cutter/welder held-tool convention above, not
        // like a fired weapon (World.Doors.cs's TryChopDoor already gates on actually holding one).
        if (command.AxeSwingHeld && !character.IsOutside && character.ManningTurretId is null && !character.IsAtHelm)
            TryChopDoor(character);

        if (character.ManningTurretId is { } turretId)
        {
            _turretAimInput[turretId] = command.TurretAimDirection;
            // FireHeld is additive to FirePressed, not a replacement - TryFire's own
            // CooldownRemaining already makes calling it redundantly on the same tick harmless, and
            // keeping FirePressed live here means a single edge-triggered press still fires exactly
            // one shot (existing tests rely on that). FireHeld is what lets holding the trigger down
            // rip through the magnetic cannon's magazine, sustain the laser's beam, or keep the
            // machine gun bursting (World.Combat.cs, TurretBalance) instead of needing one press per shot.
            if (command.FirePressed || command.FireHeld)
                TryFire(_turretRuntimes[turretId]);
        }
    }

    public void Step(double deltaSeconds)
    {
        StepCharacters(deltaSeconds);
        StepTurrets(deltaSeconds);
        StepCutting(deltaSeconds);
        StepWelding(deltaSeconds);
        StepAxeCooldowns(deltaSeconds);
        StepSystemRepair(deltaSeconds);
        StepPersonalShots(deltaSeconds);
        StepOxygenTanks(deltaSeconds);
        StepBoarding(deltaSeconds);
        StepStationCrime(deltaSeconds);
        // Fleet before voyage so the enemy moves and shoots on the same tick the shells advance,
        // and voyage's loss check sees this tick's damage rather than the previous one's.
        StepEnemyFleet(deltaSeconds);
        StepProjectiles(deltaSeconds);
        StepVoyage(deltaSeconds);
        // After voyage, so a fresh engagement (TryEngageHostileNpc) spawns its squadron against
        // this tick's already-updated ship position, same freshness UpdateNearestStation's own
        // station-defense check relies on.
        StepNpcFleet(deltaSeconds);
        StepScanners(deltaSeconds);
        StepCampaign();
        StepTutorial();
        StepAtmosphere(deltaSeconds);
        StepInjuries(deltaSeconds);
        // After everything else so a bot reacts to this tick's state (a fresh breach, a target that
        // just came into a fight) rather than lagging a tick behind it, and before PowerGrid.Step so
        // an Engineer bot's nudge this tick is actually reflected in this tick's allocation.
        StepCrewBots(deltaSeconds);
        // After crew bots (so a bot's own action this tick is visible to a sensor) and before
        // PowerGrid.Step (so a PowerLossSensor reads last tick's settled allocation, same timing
        // every other GetEffectivePower caller already relies on).
        StepComponentLogic(deltaSeconds);
        StepCardGame(deltaSeconds);
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
        CreateAmmoStorageStates(),
        Ship.SuitLockers,
        Ship.SystemDevices,
        Ship.SystemDevices.Select(d =>
        {
            var (percent, tickPosition) = GetSystemRepairDisplay(d.Id);
            return new ShipSystemState(d.Id, d.System, !IsDeviceConnected(d.Id), percent, tickPosition);
        })
        // Hull cameras reuse ShipSystemState's exact shape for the same reason Junctions do
        // (World.Wiring.cs's CreateJunctionStates) - "device id / system / damaged / repair
        // progress" is exactly what a camera needs too, and the client's ExternalCameraPanel reads
        // this list by DeviceId to know which camera tiles are dark, same as any other device.
        .Concat(Ship.Cameras.Select(c =>
        {
            var (percent, tickPosition) = GetSystemRepairDisplay(c.Id);
            return new ShipSystemState(c.Id, PowerSystemId.Secondary, !IsDeviceConnected(c.Id), percent, tickPosition);
        }))
        .ToArray(),
        CreateJunctionStates(),
        Ship.ReactorBlock,
        Ship.DistributionBlock,
        Ship.BatteryBlock,
        Ship.NavigationConsole,
        CreateGalaxyPoints(),
        Ship.HelmConsole,
        Ship.StorageRacks,
        RackSlots,
        new StationSnapshot(
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
            Station.WallBlocks,
            CreateStationWallBlockStates()),
        DockBerthPosition,
        CanDockNow,
        new EnemyShipSnapshot(
            EnemyShipLayout.Rooms,
            EnemyShipLayout.Doors,
            EnemyShipLayout.AirlockOuterDoors,
            EnemyShipLayout.Name,
            CreateEnemyRoomOxygenStates(),
            EnemyShipFieldPosition,
            CreateEnemyShipStates(),
            CreateEnemyCrewStates(),
            BoardableEnemy?.Layout.WallBlocks ?? Array.Empty<WallBlock>(),
            CreateEnemyHullWallBlockStates(),
            CreateEnemyAirlockStates()),
        CreateProjectileStates(),
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
        CreateWallBlockStates(),
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
                    c.Inventory.TankCharge(Inventory.WornSuitSlot),
                    c.Inventory.BeltBagSlots.ToArray()),
                c.IsBleeding, c.IsAtHelm, c.IsOutside, c.JetpackFuel, c.EvaAttachedTo != EvaAttachment.None, c.OnStation, c.OnEnemyShip,
                c.Inventory.TankCharge(Inventory.WornSuitSlot),
                c.Inventory.HeldSlotOf(ItemType.Cutter) is var cutterSlot && cutterSlot >= 0
                    ? c.Inventory.TankCharge(cutterSlot)
                    : null,
                IsCutting(c.PlayerId),
                c.IsBot, c.BotName, c.Role,
                c.Inventory.HeldSlotOf(ItemType.WeldingTool) is var welderSlot && welderSlot >= 0
                    ? c.Inventory.TankCharge(welderSlot)
                    : null,
                IsWelding(c.PlayerId),
                c.LayingWireFromPin,
                c.Nickname,
                c.PingMs,
                GetWallToolTargetId(c),
                c.LayingWireBends.ToArray(),
                GetDoorToolTargetId(c),
                c.MagneticBootsOn,
                c.ScannerSweepDegrees,
                CreateScannerContacts(c.PlayerId),
                c.ScannerCooldownRemaining,
                c.ScannerMode);
        }).ToArray(),
        PowerGrid.CreateState(),
        new VoyageState(ShipMapPosition, _dockedPointId, IsInBattle, IsDocked || _nearestStationPointId is not null),
        Credits,
        ActiveQuest,
        new Dictionary<ShipUpgradeTrack, int>(UpgradeLevels),
        new WiringSnapshot(Components, CreateComponentStates(), Wires, CreateWireStates(), Ship.ComponentMounts, CreateComponentMountStates()),
        new AsteroidFieldSnapshot(AsteroidField.Asteroids, AsteroidField.OreDeposits, CreateOreDepositStates()),
        _droppedItems.ToArray(),
        new ShipFieldState(
            _shipFieldPosition.X, _shipFieldPosition.Y, _shipRotationDegrees,
            _shipVelocity.X, _shipVelocity.Y, _shipThrust.X, _shipThrust.Y, _shipAutoStabilize, ControlMode),
        _recruitRoster,
        CreateStarSystemSummaries(),
        _currentSystemId,
        CanWarpNow,
        Environment.TickCount64,
        CreateSuitLockerStates(),
        Ship.CardTable,
        CreateCardGameState(),
        Ship.ForwardDegrees,
        new ReactorLeverState(LightsOn, PowerGrid.Reactor.EmergencyShutdown, DoorsLocked),
        StoryLog,
        GetTutorialObjective(),
        CreateNpcShipStates(),
        _manualScannerMarkers.ToArray(),
        Ship.Cameras,
        Ship.Jukebox is { } jukeboxBlock ? new JukeboxState(jukeboxBlock, JukeboxOn, JukeboxTrackIndex, JukeboxVolume) : null);
}
