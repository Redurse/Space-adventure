using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static class TestRunner
{
    private static readonly (string Name, Func<bool> Run)[] Tests =
    {
        ("Smoke_ProjectsWireUpCorrectly", Smoke_ProjectsWireUpCorrectly),
        ("InProcessTransport_DeliversCommandToServer", InProcessTransport_DeliversCommandToServer),
        ("GameServer_TickIncrementsAndBroadcastsSnapshot", GameServer_TickIncrementsAndBroadcastsSnapshot),
        ("World_Step_MovesCharacterTowardInput", World_Step_MovesCharacterTowardInput),
        ("World_Step_ClampsToShipBounds", World_Step_ClampsToShipBounds),
        ("GameServer_Tick_AppliesMoveCommandFromClient", GameServer_Tick_AppliesMoveCommandFromClient),
        ("Ship_MoveAlongAxis_BlocksAtWallWithoutDoor", Ship_MoveAlongAxis_BlocksAtWallWithoutDoor),
        ("Ship_MoveAlongAxis_PassesThroughAlignedDoor", Ship_MoveAlongAxis_PassesThroughAlignedDoor),
        ("Ship_MoveAlongAxis_BlockedWhenMisalignedWithDoor", Ship_MoveAlongAxis_BlockedWhenMisalignedWithDoor),
        ("Reactor_Step_DepletesFuelProportionalToUsage", Reactor_Step_DepletesFuelProportionalToUsage),
        ("Reactor_CurrentOutput_DropsToZeroWhenFuelDepleted", Reactor_CurrentOutput_DropsToZeroWhenFuelDepleted),
        ("PowerGrid_Allocation_CannotExceedReactorOutput", PowerGrid_Allocation_CannotExceedReactorOutput),
        ("PowerGrid_Battery_ChargesFromSurplus", PowerGrid_Battery_ChargesFromSurplus),
        ("World_ToggleManning_RequiresProximityToPeriscope", World_ToggleManning_RequiresProximityToPeriscope),
        ("World_ToggleManning_SucceedsNearPeriscope", World_ToggleManning_SucceedsNearPeriscope),
        ("World_TurretAim_ClampsToDefinitionLimits", World_TurretAim_ClampsToDefinitionLimits),
        ("World_Fire_DamagesEnemyAndRespectsCooldown", World_Fire_DamagesEnemyAndRespectsCooldown),
        ("World_Battle_SquadronSpawnsInFieldAndClosesOnTheShip", World_Battle_SquadronSpawnsInFieldAndClosesOnTheShip),
        ("World_Battle_EnemyFire_IsBlockedByAnAsteroid", World_Battle_EnemyFire_IsBlockedByAnAsteroid),
        ("World_Battle_ShipsDoNotOverlapWhenTheyCollide", World_Battle_ShipsDoNotOverlapWhenTheyCollide),
        ("World_Fire_ShellMissesWhenTheGunIsPointedAway", World_Fire_ShellMissesWhenTheGunIsPointedAway),
        ("World_Movement_LockedWhileManningTurret", World_Movement_LockedWhileManningTurret),
        ("World_Fire_EmptiesMagazineThenRefusesWithoutDamage", World_Fire_EmptiesMagazineThenRefusesWithoutDamage),
        ("World_PickUpAmmoCrate_RequiresProximityToStorage", World_PickUpAmmoCrate_RequiresProximityToStorage),
        ("World_PickUpAmmoCrate_SucceedsNearStorage", World_PickUpAmmoCrate_SucceedsNearStorage),
        ("World_ReloadTurret_RefillsAmmoAndClearsCarrying", World_ReloadTurret_RefillsAmmoAndClearsCarrying),
        ("EnemyShip_IsRetreating_BelowThreshold", EnemyShip_IsRetreating_BelowThreshold),
        ("EnemyShip_IsRetreating_FalseAboveThreshold", EnemyShip_IsRetreating_FalseAboveThreshold),
        ("World_EnemyAi_EventuallyBreachesEveryRoom", World_EnemyAi_EventuallyBreachesEveryRoom),
        ("World_Decompression_DrainsHealthInBreachedRoom", World_Decompression_DrainsHealthInBreachedRoom),
        ("World_Oxygen_GeneratorRestoresRoomOxygenWhenPowered", World_Oxygen_GeneratorRestoresRoomOxygenWhenPowered),
        ("World_RepairBreach_ClearsItViaInteract", World_RepairBreach_ClearsItViaInteract),
        ("World_Voyage_TravelingToHostileSectorStartsBattle", World_Voyage_TravelingToHostileSectorStartsBattle),
        ("World_Voyage_DefeatingEnemyReturnsToTraveling", World_Voyage_DefeatingEnemyReturnsToTraveling),
        ("World_Voyage_StationRefuelsAndClearsBreaches", World_Voyage_StationRefuelsAndClearsBreaches),
        ("World_EnemyAi_DormantWhileTraveling", World_EnemyAi_DormantWhileTraveling),
        ("World_SuitAction_RequiresProximityToLocker", World_SuitAction_RequiresProximityToLocker),
        ("World_SuitAction_TakesTimeAndLocksMovement", World_SuitAction_TakesTimeAndLocksMovement),
        ("World_SuitedCharacter_ImmuneToDecompression", World_SuitedCharacter_ImmuneToDecompression),
        ("World_SuitAction_IgnoredWhileMidAction", World_SuitAction_IgnoredWhileMidAction),
        ("World_Character_FacingTracksLastMoveDirection", World_Character_FacingTracksLastMoveDirection),
        ("World_LaserTurret_FiresUsingChargeWithoutAmmoCrate", World_LaserTurret_FiresUsingChargeWithoutAmmoCrate),
        ("World_LaserTurret_RechargesOnlyFromWeaponChargerAllocation", World_LaserTurret_RechargesOnlyFromWeaponChargerAllocation),
        ("World_Inventory_PickUpAmmoCrate_OccupiesMainSlot", World_Inventory_PickUpAmmoCrate_OccupiesMainSlot),
        ("World_Inventory_ReloadTurret_ClearsAmmoCrateFromSlot", World_Inventory_ReloadTurret_ClearsAmmoCrateFromSlot),
        ("World_Inventory_DonningSuit_OccupiesClothingSlot", World_Inventory_DonningSuit_OccupiesClothingSlot),
        ("Inventory_ToggleHold_OneHandedItemsShareBothHands", Inventory_ToggleHold_OneHandedItemsShareBothHands),
        ("Inventory_ToggleHold_TwoHandedItemDropsExistingHeldItem", Inventory_ToggleHold_TwoHandedItemDropsExistingHeldItem),
        ("Inventory_ToggleHold_ClickingHeldSlotAgainUnholds", Inventory_ToggleHold_ClickingHeldSlotAgainUnholds),
        ("World_PickUpToolFromStation_AddsItToInventory", World_PickUpToolFromStation_AddsItToInventory),
        ("World_WeldBreach_DoesNothingWithoutToolHeld", World_WeldBreach_DoesNothingWithoutToolHeld),
        ("World_SystemDamage_ZerosEffectiveAllocation", World_SystemDamage_ZerosEffectiveAllocation),
        ("World_RepairSystem_RequiresWrenchHeldInHand", World_RepairSystem_RequiresWrenchHeldInHand),
        ("Reactor_RemovingAllRods_ZerosOutput", Reactor_RemovingAllRods_ZerosOutput),
        ("Reactor_FreshRodIntoSpentSlot_ComesFullyCharged", Reactor_FreshRodIntoSpentSlot_ComesFullyCharged),
        ("Reactor_Step_BurnsRodsOneAtATime", Reactor_Step_BurnsRodsOneAtATime),
        ("World_ReactorSlot_InsertingCarriedRod_RefuelsFromEmpty", World_ReactorSlot_InsertingCarriedRod_RefuelsFromEmpty),
        ("World_ReactorSlot_RequiresProximityToReactor", World_ReactorSlot_RequiresProximityToReactor),
        ("World_ReactorSlot_RemoveRodReturnsItToInventory", World_ReactorSlot_RemoveRodReturnsItToInventory),
        ("World_ReactorSlot_InsertRequiresHoldingRod", World_ReactorSlot_InsertRequiresHoldingRod),
        ("World_ReactorSlot_ReinsertHeldRod", World_ReactorSlot_ReinsertHeldRod),
        ("Shield_TryAbsorbHit_DepletesPointsUntilEmpty", Shield_TryAbsorbHit_DepletesPointsUntilEmpty),
        ("World_Shield_AbsorbsFirstAttackWithoutDamagingShip", World_Shield_AbsorbsFirstAttackWithoutDamagingShip),
        ("World_Voyage_ShipMovesContinuouslyTowardTarget", World_Voyage_ShipMovesContinuouslyTowardTarget),
        ("World_Voyage_CannotChangeDestinationMidBattle", World_Voyage_CannotChangeDestinationMidBattle),
        ("World_Trade_BuyItem_DeductsCreditsAndAddsToInventory", World_Trade_BuyItem_DeductsCreditsAndAddsToInventory),
        ("World_Trade_BuyItem_FailsWithoutEnoughCredits", World_Trade_BuyItem_FailsWithoutEnoughCredits),
        ("World_Trade_BuyItem_FailsWhenInventoryFull", World_Trade_BuyItem_FailsWhenInventoryFull),
        ("World_Trade_SellItem_RefundsCreditsAndClearsSlot", World_Trade_SellItem_RefundsCreditsAndClearsSlot),
        ("World_Trade_BuyAndSell_FailWhileNotDocked", World_Trade_BuyAndSell_FailWhileNotDocked),
        ("World_Quest_Accept_SetsDestinationDifferentFromCurrentStation", World_Quest_Accept_SetsDestinationDifferentFromCurrentStation),
        ("World_Quest_Accept_FailsWhenAlreadyActive", World_Quest_Accept_FailsWhenAlreadyActive),
        ("World_Quest_TurnIn_FailsAtWrongStation", World_Quest_TurnIn_FailsAtWrongStation),
        ("World_Quest_TurnIn_AtDestination_AwardsCreditsAndClearsQuest", World_Quest_TurnIn_AtDestination_AwardsCreditsAndClearsQuest),
        ("World_Quest_Accept_FailsWhileNotDocked", World_Quest_Accept_FailsWhileNotDocked),
        ("World_MedKit_PickupFromToolStation_AddsToInventory", World_MedKit_PickupFromToolStation_AddsToInventory),
        ("World_MedKit_HealsSelfAndConsumesItem", World_MedKit_HealsSelfAndConsumesItem),
        ("World_MedKit_DoesNothingAtFullHealth", World_MedKit_DoesNothingAtFullHealth),
        ("World_Bleeding_DrainsHealthFasterThanDecompressionAlone", World_Bleeding_DrainsHealthFasterThanDecompressionAlone),
        ("World_Upgrade_PurchaseReactorOutput_IncreasesMaxOutputAndDeductsCredits", World_Upgrade_PurchaseReactorOutput_IncreasesMaxOutputAndDeductsCredits),
        ("World_Upgrade_Purchase_FailsWithoutEnoughCredits", World_Upgrade_Purchase_FailsWithoutEnoughCredits),
        ("World_Upgrade_Purchase_FailsWhileNotDocked", World_Upgrade_Purchase_FailsWhileNotDocked),
        ("World_Upgrade_WeaponDamage_IncreasesShotDamage", World_Upgrade_WeaponDamage_IncreasesShotDamage),
        ("World_Wiring_LayBackup_KeepsSystemPoweredAfterTrunkCut", World_Wiring_LayBackup_KeepsSystemPoweredAfterTrunkCut),
        ("World_Wiring_RepairViaPanel_RestoresConnectionNoProximityNeeded", World_Wiring_RepairViaPanel_RestoresConnectionNoProximityNeeded),
        ("World_Wiring_ShieldsOneDropCut_HalvesEffectivePower", World_Wiring_ShieldsOneDropCut_HalvesEffectivePower),
        ("World_Wiring_LayBackup_RequiresSpoolAndOnlyOnce", World_Wiring_LayBackup_RequiresSpoolAndOnlyOnce),
        ("World_Helm_Thrust_AcceleratesShipWithInertia", World_Helm_Thrust_AcceleratesShipWithInertia),
        ("World_Helm_WasdSteersByHeadingAndReverseBacksOut", World_Helm_WasdSteersByHeadingAndReverseBacksOut),
        ("World_Helm_ThrustPersists_AfterStandingUp", World_Helm_ThrustPersists_AfterStandingUp),
        ("World_Helm_Stabilize_BringsShipToStop", World_Helm_Stabilize_BringsShipToStop),
        ("World_Helm_NoEnginePower_ShipDoesNotAccelerate", World_Helm_NoEnginePower_ShipDoesNotAccelerate),
        ("World_Ship_CollidesWithAsteroid_StopsShipAndBreachesHull", World_Ship_CollidesWithAsteroid_StopsShipAndBreachesHull),
        ("AsteroidShape_IsAStableNonCircularOutline", AsteroidShape_IsAStableNonCircularOutline),
        ("HullSilhouette_TreatsTheGapBetweenPylonsAsOpenSpace", HullSilhouette_TreatsTheGapBetweenPylonsAsOpenSpace),
        ("World_ToggleDoor_ViaClientCommand_FlipsState", World_ToggleDoor_ViaClientCommand_FlipsState),
        ("World_Door_Closed_BlocksMovementLikeWall", World_Door_Closed_BlocksMovementLikeWall),
        ("World_AirlockOuterDoor_Open_LeaksChamberToVacuum", World_AirlockOuterDoor_Open_LeaksChamberToVacuum),
        ("World_AirlockOuterDoor_Closed_ChamberStaysPressurized", World_AirlockOuterDoor_Closed_ChamberStaysPressurized),
        ("World_ClosedInnerDoor_KeepsRestOfShipSealedFromVentedChamber", World_ClosedInnerDoor_KeepsRestOfShipSealedFromVentedChamber),
        ("World_OpenInnerDoor_LetsVentedChamberDrainRestOfShip", World_OpenInnerDoor_LetsVentedChamberDrainRestOfShip),
        ("World_Eva_ExitRequiresSuit", World_Eva_ExitRequiresSuit),
        ("World_Eva_ExitSuited_SetsIsOutsideAndAttachesToShip", World_Eva_ExitSuited_SetsIsOutsideAndAttachesToShip),
        ("World_Eva_AttachedToShip_MovesWithShipWhenShipMoves", World_Eva_AttachedToShip_MovesWithShipWhenShipMoves),
        ("World_Eva_PushOff_BecomesFreeFloatingWithVelocity", World_Eva_PushOff_BecomesFreeFloatingWithVelocity),
        ("World_Eva_Jetpack_ExhaustsFuelThenKeepsDriftingAtLastVelocity", World_Eva_Jetpack_ExhaustsFuelThenKeepsDriftingAtLastVelocity),
        ("World_Eva_AutoReattachToShip_WhenDriftingBack", World_Eva_AutoReattachToShip_WhenDriftingBack),
        ("World_Eva_ReenterShip_ReturnsInsideAtAirlockChamber", World_Eva_ReenterShip_ReturnsInsideAtAirlockChamber),
        ("World_Rack_DragFromInventory_StowsItem", World_Rack_DragFromInventory_StowsItem),
        ("World_Rack_DropOntoOccupiedSlot_SwapsTheTwo", World_Rack_DropOntoOccupiedSlot_SwapsTheTwo),
        ("World_Rack_AwayFromTheRack_MoveIsRefused", World_Rack_AwayFromTheRack_MoveIsRefused),
        ("World_Inventory_DragBetweenOwnSlots_MovesAndEmptiesHands", World_Inventory_DragBetweenOwnSlots_MovesAndEmptiesHands),
        ("World_Save_RoundTripsRackContents", World_Save_RoundTripsRackContents),
        ("World_Eva_MagnetizedWalk_StaysFlushAgainstTheHull", World_Eva_MagnetizedWalk_StaysFlushAgainstTheHull),
        ("World_Eva_WalkingAwayFromTheDoor_StaysOutside", World_Eva_WalkingAwayFromTheDoor_StaysOutside),
        ("World_Eva_BootsGrabOnContact_NotAcrossTheGap", World_Eva_BootsGrabOnContact_NotAcrossTheGap),
        ("World_Mining_CutterFlameBreaksBlockIntoPickableItem", World_Mining_CutterFlameBreaksBlockIntoPickableItem),
        ("World_Mining_CutterWithoutTank_CutsNothing", World_Mining_CutterWithoutTank_CutsNothing),
        ("World_Mining_CutBlock_DropsOnceAndIsGone", World_Mining_CutBlock_DropsOnceAndIsGone),
        ("World_Eva_SuitWithoutTank_CannotStepOutside", World_Eva_SuitWithoutTank_CannotStepOutside),
        ("World_Eva_SuitTankRunsDownInVacuum", World_Eva_SuitTankRunsDownInVacuum),
        ("World_Mining_SellMineralAtStation_RefundsCreditsAndClearsSlot", World_Mining_SellMineralAtStation_RefundsCreditsAndClearsSlot),
        ("Ship_Scout_HasAirlockChamberAndSameWireDeviceIds", Ship_Scout_HasAirlockChamberAndSameWireDeviceIds),
        ("Ship_Corvette_HasSideGunsTwoPortsAndSameWireDeviceIds", Ship_Corvette_HasSideGunsTwoPortsAndSameWireDeviceIds),
        ("Ship_Corvette_CrewWalksTheSpineAndOutToBothBays", Ship_Corvette_CrewWalksTheSpineAndOutToBothBays),
        ("World_Eva_CorvetteCrewGoesOutThroughABeamPort", World_Eva_CorvetteCrewGoesOutThroughABeamPort),
        ("World_ShipField_CorvetteFliesNoseFirst", World_ShipField_CorvetteFliesNoseFirst),
        ("Ship_Cruiser_HasAirlockChamberAndThreeTurrets", Ship_Cruiser_HasAirlockChamberAndThreeTurrets),
        ("World_ShipKindScout_SpawnsAndSteps", World_ShipKindScout_SpawnsAndSteps),
        ("World_ShipKindCruiser_SpawnsAndSteps", World_ShipKindCruiser_SpawnsAndSteps),
        ("RoomLayout_MoveAlongAxis_BlocksAtWallWithoutDoor", RoomLayout_MoveAlongAxis_BlocksAtWallWithoutDoor),
        ("World_Station_ArrivingSetsStationApproachNotInstantDock", World_Station_ArrivingSetsStationApproachNotInstantDock),
        ("World_Station_DockAtStation_ReachesStationPhase", World_Station_DockAtStation_ReachesStationPhase),
        ("World_Station_WalkThroughOpenOuterDoor_EntersStation", World_Station_WalkThroughOpenOuterDoor_EntersStation),
        ("World_Station_WalkBackThroughConnector_ReturnsToShip", World_Station_WalkBackThroughConnector_ReturnsToShip),
        ("World_Station_CannotCrossOuterDoorWhileNotDocked", World_Station_CannotCrossOuterDoorWhileNotDocked),
        ("World_Boarding_EvaDuringBattle_ReachesEnemyShip", World_Boarding_EvaDuringBattle_ReachesEnemyShip),
        ("World_Boarding_FireWeaponDamagesCrewInSameRoom", World_Boarding_FireWeaponDamagesCrewInSameRoom),
        ("World_Boarding_WithoutWeaponHeld_DoesNothing", World_Boarding_WithoutWeaponHeld_DoesNothing),
        ("World_Boarding_KillingAllCrew_DestroysEnemyShip", World_Boarding_KillingAllCrew_DestroysEnemyShip),
        ("World_Boarding_CrewFightsBack_DamagesBoarder", World_Boarding_CrewFightsBack_DamagesBoarder),
        ("EnemyShipClasses_AreDistinctStructures", EnemyShipClasses_AreDistinctStructures),
        ("World_Boarding_SectorAlwaysFieldsTheSameHull", World_Boarding_SectorAlwaysFieldsTheSameHull),
        ("World_Boarding_OpeningDoors_VentsTheHullAndSuffocatesUnsuitedCrew", World_Boarding_OpeningDoors_VentsTheHullAndSuffocatesUnsuitedCrew),
        ("World_Boarding_HullDestroyedUnderneath_EjectsTheBoardingParty", World_Boarding_HullDestroyedUnderneath_EjectsTheBoardingParty),
        ("World_Faction_DestroyingShip_LowersOwnerRaisesRival", World_Faction_DestroyingShip_LowersOwnerRaisesRival),
        ("World_Faction_QuestTurnIn_RaisesStanding", World_Faction_QuestTurnIn_RaisesStanding),
        ("World_Faction_HostileStanding_BlocksQuestOffers", World_Faction_HostileStanding_BlocksQuestOffers),
        ("World_Faction_HostileStanding_RaisesPrices", World_Faction_HostileStanding_RaisesPrices),
        ("World_Faction_IndependentsNeverShift", World_Faction_IndependentsNeverShift),
        ("World_Shipyard_BuyCheaperHull_SwapsShipAndRefunds", World_Shipyard_BuyCheaperHull_SwapsShipAndRefunds),
        ("World_Shipyard_Buy_FailsWithoutEnoughCredits", World_Shipyard_Buy_FailsWithoutEnoughCredits),
        ("World_Shipyard_Buy_FailsWhileNotDocked", World_Shipyard_Buy_FailsWhileNotDocked),
        ("World_Shipyard_SwapKeepsCreditsAndClearsBreaches", World_Shipyard_SwapKeepsCreditsAndClearsBreaches),
        ("World_Quest_Bounty_CompletesOnKillAndPaysAtIssuer", World_Quest_Bounty_CompletesOnKillAndPaysAtIssuer),
        ("World_Quest_Bounty_TurnIn_FailsBeforeKill", World_Quest_Bounty_TurnIn_FailsBeforeKill),
        ("World_Quest_Mining_ConsumesOreAndPays", World_Quest_Mining_ConsumesOreAndPays),
        ("World_Quest_Mining_TurnIn_FailsWithoutEnoughOre", World_Quest_Mining_TurnIn_FailsWithoutEnoughOre),
        ("World_Quest_Accept_FailsAtStationWithoutAdministrator", World_Quest_Accept_FailsAtStationWithoutAdministrator),
        ("World_Save_RoundTripsCampaignProgress", World_Save_RoundTripsCampaignProgress),
        ("World_Save_AutosavePendingSetOnDocking", World_Save_AutosavePendingSetOnDocking),
        ("SaveStore_RoundTripsThroughFile", SaveStore_RoundTripsThroughFile),
        ("SaveStore_MissingOrCorruptFile_LoadsAsNoSave", SaveStore_MissingOrCorruptFile_LoadsAsNoSave),
        ("GameServer_AutosavesOnDocking", GameServer_AutosavesOnDocking),
        ("World_Crime_StealCrate_AddsItemAndMarksLooted", World_Crime_StealCrate_AddsItemAndMarksLooted),
        ("World_Crime_CaughtByGuard_FinesConfiscatesAndLowersStanding", World_Crime_CaughtByGuard_FinesConfiscatesAndLowersStanding),
        ("World_Crime_UnseenTheft_GoesUnpunished", World_Crime_UnseenTheft_GoesUnpunished),
        ("World_Crime_ShootingGuard_AlertsStationAndGuardFightsBack", World_Crime_ShootingGuard_AlertsStationAndGuardFightsBack),
        ("World_Crime_KillingGuard_CostsHeavyStanding", World_Crime_KillingGuard_CostsHeavyStanding),
        ("World_Crime_RedockingRestocksCrates", World_Crime_RedockingRestocksCrates),
        ("World_Squadron_NextShipEngagesAfterEachKill", World_Squadron_NextShipEngagesAfterEachKill),
        ("World_Squadron_EveryKillCostsOwnerStanding", World_Squadron_EveryKillCostsOwnerStanding),
        ("World_Squadron_BountyCompletesOnlyWhenSectorCleared", World_Squadron_BountyCompletesOnlyWhenSectorCleared),
        ("World_Docking_ProximityAloneDoesNotDock", World_Docking_ProximityAloneDoesNotDock),
        ("World_Docking_ButtonFarFromPort_DoesNothing", World_Docking_ButtonFarFromPort_DoesNothing),
        ("World_Docking_TooFastAtPort_ButtonStaysDisarmed", World_Docking_TooFastAtPort_ButtonStaysDisarmed),
        ("World_Docking_StationHullBlocksTheShip", World_Docking_StationHullBlocksTheShip),
        ("World_Docking_MatesAirlockOntoStationConnector", World_Docking_MatesAirlockOntoStationConnector),
        ("World_Station_CrossingConnector_MovesContinuously", World_Station_CrossingConnector_MovesContinuously),
        ("World_Station_Departing_PullsCrewBackAboard", World_Station_Departing_PullsCrewBackAboard),
        ("World_Station_OpenAirlockWhileDocked_DoesNotVentTheShip", World_Station_OpenAirlockWhileDocked_DoesNotVentTheShip),
    };

    public static int Run()
    {
        int failed = 0;
        foreach (var (name, test) in Tests)
        {
            bool ok;
            try { ok = test(); }
            catch (Exception ex) { ok = false; Console.WriteLine($"  {name}: EXCEPTION {ex}"); }

            Console.WriteLine(ok ? $"OK   {name}" : $"FAIL {name}");
            if (!ok) failed++;
        }

        Console.WriteLine($"\n{Tests.Length - failed}/{Tests.Length} passed");
        return failed == 0 ? 0 : 1;
    }

    // Заглушка на время каркаса: реальные тесты появятся вместе с логикой в Shared/Server.
    private static bool Smoke_ProjectsWireUpCorrectly() => true;

    private static bool InProcessTransport_DeliversCommandToServer()
    {
        var transport = new InProcessTransport();
        IClientConnection clientSide = transport;
        IServerConnection serverSide = transport;

        var command = new ClientCommand(PlayerId: 1);
        clientSide.Send(command);

        var received = serverSide.ReceiveCommands();
        return received.Count == 1 && received[0] == command;
    }

    private static bool GameServer_TickIncrementsAndBroadcastsSnapshot()
    {
        var server = new GameServer();
        var transport = new InProcessTransport();
        server.Connect(transport);

        server.Tick();
        server.Tick();

        IClientConnection clientSide = transport;
        var latest = clientSide.ReceiveLatestSnapshot();
        return latest is not null && latest.Tick == 2;
    }

    // Real usage (GameServer.Tick) steps in small ~1/30s increments — door crossings only work
    // when the per-step distance stays within a door's depth, so tests replicate that cadence
    // rather than one huge Step() jump.
    private const double RealtimeStep = 1.0 / 30;

    private static bool World_Step_MovesCharacterTowardInput()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var start = world.Ship.SpawnPoint;

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep); // ~1 second at full speed, crosses into the next room via its door

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > start.X + 1f && Math.Abs(character.Y - start.Y) < 0.01f;
    }

    private static bool World_Step_ClampsToShipBounds()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 300; i++)
            world.Step(RealtimeStep); // far more than enough to walk through every door into the hull wall

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var maxX = world.Ship.Rooms.Max(r => r.Right);
        return Math.Abs(character.X - maxX) < 0.01f;
    }

    private static bool GameServer_Tick_AppliesMoveCommandFromClient()
    {
        var spawn = Ship.CreateStarter().SpawnPoint;

        var server = new GameServer();
        var transport = new InProcessTransport();
        var playerId = server.Connect(transport);

        IClientConnection clientSide = transport;
        clientSide.Send(new ClientCommand(playerId, MoveX: 1, MoveY: 0));

        server.Tick();

        var snapshot = clientSide.ReceiveLatestSnapshot();
        var character = snapshot?.Characters.SingleOrDefault(c => c.PlayerId == playerId);
        return character is not null && character.X > spawn.X;
    }

    private static bool Ship_MoveAlongAxis_BlocksAtWallWithoutDoor()
    {
        var ship = Ship.CreateStarter();
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(2.5f, 0.5f), "cockpit", new Vec2(0, -1f), _ => true);
        return roomId == "cockpit" && Math.Abs(pos.Y - 0f) < 0.01f; // clamped at the top hull wall
    }

    private static bool Ship_MoveAlongAxis_PassesThroughAlignedDoor()
    {
        var ship = Ship.CreateStarter();
        // Near the cockpit/reactor wall (x=5) at the door's y=3 — should cross through.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.9f, 3f), "cockpit", new Vec2(0.3f, 0), _ => true);
        return roomId == "reactor" && Math.Abs(pos.X - 5.2f) < 0.01f;
    }

    private static bool Ship_MoveAlongAxis_BlockedWhenMisalignedWithDoor()
    {
        var ship = Ship.CreateStarter();
        // Same wall, but y=0.5 is outside the door's 2.1..3.9 opening — should hit the wall.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.9f, 0.5f), "cockpit", new Vec2(0.3f, 0), _ => true);
        return roomId == "cockpit" && Math.Abs(pos.X - 5f) < 0.01f;
    }

    private static bool Reactor_Step_DepletesFuelProportionalToUsage()
    {
        var reactor = new Reactor(maxOutput: 10f, maxFuel: 10f, fuelPerPowerUnitPerSecond: 1f);
        reactor.Step(1.0, totalAllocatedPower: 5f); // 5 power * 1 fuel/power/sec * 1s
        return Math.Abs(reactor.Fuel - 5f) < 0.01f;
    }

    private static bool Reactor_CurrentOutput_DropsToZeroWhenFuelDepleted()
    {
        var reactor = new Reactor(maxOutput: 10f, maxFuel: 2f, fuelPerPowerUnitPerSecond: 1f);
        reactor.Step(1.0, totalAllocatedPower: 10f); // would need 10 fuel, only 2 available
        return reactor.Fuel == 0f && reactor.CurrentOutput == 0f;
    }

    private static bool PowerGrid_Allocation_CannotExceedReactorOutput()
    {
        var grid = new PowerGrid();
        grid.ApplyInput(systemIndex: 0, direction: 1f);
        for (var i = 0; i < 5; i++)
            grid.Step(1.0); // enough seconds at the adjust rate to try to overshoot the cap

        var state = grid.CreateState();
        var total = state.Allocated.Values.Sum();
        return total <= state.ReactorOutput + 0.01f && total > 0f;
    }

    private static bool PowerGrid_Battery_ChargesFromSurplus()
    {
        var grid = new PowerGrid();
        // No allocation adjustment at all -> the whole reactor output is surplus.
        for (var i = 0; i < 10; i++)
            grid.Step(1.0);

        var state = grid.CreateState();
        return state.BatteryCharge > 0f;
    }

    // Bang-bang controller: drives the character toward a target via small realtime steps
    // (same cadence GameServer.Tick uses), so it can also cross doors along the way.
    private static void MoveCharacterTo(World world, int playerId, float targetX, float targetY)
    {
        for (var i = 0; i < 400; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId);
            var dx = targetX - me.X;
            var dy = targetY - me.Y;
            if (Math.Abs(dx) < 0.05f && Math.Abs(dy) < 0.05f)
                return;

            world.ApplyCommand(playerId, new ClientCommand(playerId, MoveX: Math.Sign(dx), MoveY: Math.Sign(dy)));
            world.Step(RealtimeStep);
        }
    }

    private static bool World_ToggleManning_RequiresProximityToPeriscope()
    {
        var world = new World();
        world.SpawnCharacter(1); // spawns in the corridor, far from the cockpit periscope

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return !world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);
    }

    private static bool World_ToggleManning_SucceedsNearPeriscope()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, targetX: 1.5f, targetY: 3f);

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1);
    }

    private static bool World_TurretAim_ClampsToDefinitionLimits()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        world.ApplyCommand(1, new ClientCommand(1, TurretAimDirection: 1f));
        for (var i = 0; i < 60; i++) // 2s — far more than enough to hit the 45-degree limit
            world.Step(RealtimeStep);

        var state = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-bow");
        return Math.Abs(state.AimDegrees - 45f) < 0.5f;
    }

    // Shells travel now (World.Projectiles.cs), so there has to be something out there to hit and
    // the shot needs time to reach it - "fire and read the HP next tick" isn't a thing any more.
    private static void EnterBattle(World world, int playerId = 1)
    {
        world.ApplyCommand(playerId, new ClientCommand(playerId, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
    }

    private static void StepFor(World world, int ticks)
    {
        for (var i = 0; i < ticks; i++)
            world.Step(RealtimeStep);
    }

    // The helm flies by heading now (W/A/D/X, World.ShipField.cs), so "go there" is two things:
    // swing the bow onto the bearing, and only then open the throttle - pushing while still broadside
    // to the target just arcs the ship away from it.
    private static ClientCommand SteerToward(World world, int playerId, Vec2 target)
    {
        var field = world.CreateSnapshot().ShipField;
        var toTarget = target - new Vec2(field.X, field.Y);
        var wanted = MathF.Atan2(toTarget.Y, toTarget.X) * (180f / MathF.PI) - world.Ship.ForwardDegrees;
        var error = ((wanted - field.RotationDegrees) % 360f + 540f) % 360f - 180f;

        return new ClientCommand(playerId,
            HelmThrottle: MathF.Abs(error) < 25f ? 1f : 0f,
            HelmTurn: MathF.Abs(error) < 2f ? 0f : MathF.Sign(error));
    }

    private static bool World_Battle_SquadronSpawnsInFieldAndClosesOnTheShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-beta")); // a picket of two
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        var atArrival = world.CreateSnapshot();
        if (atArrival.EnemyShips.Count != 2 || atArrival.EnemyShips.Count(e => e.IsBoardable) != 1)
            return false;

        float Distance(WorldSnapshot s) =>
            new Vec2(s.EnemyShips[0].X - s.ShipField.X, s.EnemyShips[0].Y - s.ShipField.Y).Length();

        var openingRange = Distance(atArrival);
        StepFor(world, 20 * 30);
        var closedRange = Distance(world.CreateSnapshot());

        // They spawn out at arm's length and fly in to a firing distance rather than being parked
        // at a fixed offset from the player forever.
        return closedRange < openingRange - 5f;
    }

    // Hulls stop against each other instead of merging: fly the ship straight at a raider holding
    // station and it comes to rest short of it, never inside it.
    private static bool World_Battle_ShipsDoNotOverlapWhenTheyCollide()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterBattle(world);
        MoveCharacterTo(world, 1, 21.5f, 3f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        // Full ahead into the raider parked off the stern, for long enough to bury the hull in it.
        for (var i = 0; i < 30 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
            world.Step(RealtimeStep);
        }

        var snapshot = world.CreateSnapshot();
        var hullHalfLength = (world.Ship.Rooms.Max(r => r.Right) - world.Ship.Rooms.Min(r => r.Left)) / 2f;
        return snapshot.EnemyShips.All(e =>
            new Vec2(e.X - snapshot.ShipField.X, e.Y - snapshot.ShipField.Y).Length() >= hullHalfLength);
    }

    private static bool World_Battle_EnemyFire_IsBlockedByAnAsteroid()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterBattle(world);

        var enemy = world.CreateSnapshot().EnemyShips.Single();
        var asteroid = world.AsteroidField.Asteroids[0];
        var shipPosition = world.CreateSnapshot().ShipField;

        // Same segment/circle test the AI itself uses: a rock straddling the line means no shot,
        // whatever the range - that's what makes flying behind one worth doing.
        var blocked = World.SegmentHitsCircle(
            new Vec2(enemy.X, enemy.Y), new Vec2(shipPosition.X, shipPosition.Y), asteroid.Position, asteroid.Radius);
        var throughTheRock = World.SegmentHitsCircle(
            asteroid.Position - new Vec2(asteroid.Radius * 4, 0), asteroid.Position + new Vec2(asteroid.Radius * 4, 0),
            asteroid.Position, asteroid.Radius);

        return !blocked && throughTheRock; // clear line here, and the test itself actually detects cover
    }

    private static bool World_Fire_ShellMissesWhenTheGunIsPointedAway()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man the bow turret
        EnterBattle(world);

        // Traverse the barrel to the edge of its arc, then fire: the shell leaves the muzzle along
        // the barrel and sails past the enemy sitting dead astern.
        for (var i = 0; i < 60; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, TurretAimDirection: 1f));
            world.Step(RealtimeStep);
        }
        var aim = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-bow").AimDegrees;

        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true, TurretAimDirection: 0f));
        StepFor(world, 90);

        return Math.Abs(aim - 45f) < 0.5f && Math.Abs(world.CreateSnapshot().Enemy.Hp - 100f) < 0.01f;
    }

    private static bool World_Fire_DamagesEnemyAndRespectsCooldown()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // Man the gun before flying in, not after: raiders start shooting a few seconds into the
        // sector, and one of their hits can knock this very turret out mid-test.
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        EnterBattle(world);

        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        world.Step(RealtimeStep);
        // Second attempt lands within the cooldown window — no second shell leaves the barrel, so
        // only one lot of damage can ever arrive however long we then wait.
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        StepFor(world, 90); // long enough for a shell to cross the gap

        return Math.Abs(world.CreateSnapshot().Enemy.Hp - 90f) < 0.01f;
    }

    private static bool World_Movement_LockedWhileManningTurret()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return Math.Abs(before.X - after.X) < 0.01f && Math.Abs(before.Y - after.Y) < 0.01f;
    }

    private static bool World_Fire_EmptiesMagazineThenRefusesWithoutDamage()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it
        EnterBattle(world); // see World_Fire_DamagesEnemyAndRespectsCooldown on the ordering

        for (var shot = 0; shot < 6; shot++) // magazine capacity
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            StepFor(world, 20); // outlast the 0.5s cooldown, and let the shell arrive
        }

        var afterMagazine = world.CreateSnapshot();
        var hpAfterSix = afterMagazine.Enemy.Hp; // 100 - 6*10 = 40
        var ammoAfterSix = afterMagazine.TurretStates.Single(t => t.Id == "turret-bow").AmmoRemaining;

        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true)); // magazine empty now
        world.Step(RealtimeStep);
        var finalSnapshot = world.CreateSnapshot();

        return ammoAfterSix == 0
            && Math.Abs(hpAfterSix - 40f) < 0.01f
            && Math.Abs(finalSnapshot.Enemy.Hp - 40f) < 0.01f
            && finalSnapshot.TurretStates.Single(t => t.Id == "turret-bow").AmmoRemaining == 0;
    }

    private static bool World_PickUpAmmoCrate_RequiresProximityToStorage()
    {
        var world = new World();
        world.SpawnCharacter(1); // spawns in the corridor, not at the quarters storage point

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).CarryingAmmoCrate;
    }

    private static bool World_PickUpAmmoCrate_SucceedsNearStorage()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 15f, 3f); // the quarters ammo storage point

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).CarryingAmmoCrate;
    }

    private static bool World_ReloadTurret_RefillsAmmoAndClearsCarrying()
    {
        var world = new World();
        world.SpawnCharacter(1);

        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it
        for (var shot = 0; shot < 6; shot++)
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            for (var i = 0; i < 20; i++)
                world.Step(RealtimeStep);
        }
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // stand back up

        MoveCharacterTo(world, 1, 15f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up a crate

        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // reload (carrying + near turret, not manning)

        var snapshot = world.CreateSnapshot();
        var turret = snapshot.TurretStates.Single(t => t.Id == "turret-bow");
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);

        return turret.AmmoRemaining == turret.MagazineCapacity && !me.CarryingAmmoCrate;
    }

    private static bool World_Inventory_PickUpAmmoCrate_OccupiesMainSlot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 15f, 3f); // the quarters ammo storage point

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count == Inventory.MainSlotCount
            && inventory.MainSlots.Count(s => s == ItemType.AmmoCrate) == 1;
    }

    private static bool World_Inventory_ReloadTurret_ClearsAmmoCrateFromSlot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 15f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up a crate

        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // reload from the slot

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.All(s => s != ItemType.AmmoCrate);
    }

    private static bool World_Inventory_DonningSuit_OccupiesClothingSlot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 20f, 3f); // engine-room suit locker

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // start equipping
        for (var i = 0; i < 70; i++) // finish the 2s action, with margin for float accumulation
            world.Step(RealtimeStep);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.Equipped[EquipSlot.Clothing] == ItemType.Spacesuit;
    }

    private static bool Inventory_ToggleHold_OneHandedItemsShareBothHands()
    {
        var inventory = new Inventory();
        inventory.TryAdd(ItemType.Wrench); // slot 0
        inventory.TryAdd(ItemType.Knife); // slot 1

        inventory.ToggleHold(0);
        inventory.ToggleHold(1);

        return inventory.IsHolding(ItemType.Wrench) && inventory.IsHolding(ItemType.Knife) && inventory.HeldSlotIndices.Count == 2;
    }

    private static bool Inventory_ToggleHold_TwoHandedItemDropsExistingHeldItem()
    {
        var inventory = new Inventory();
        inventory.TryAdd(ItemType.Wrench); // slot 0
        inventory.TryAdd(ItemType.WeldingTool); // slot 1

        inventory.ToggleHold(0); // one hand full
        inventory.ToggleHold(1); // needs both hands — must drop the wrench to fit

        return inventory.IsHolding(ItemType.WeldingTool) && !inventory.IsHolding(ItemType.Wrench) && inventory.HeldSlotIndices.Count == 1;
    }

    private static bool Inventory_ToggleHold_ClickingHeldSlotAgainUnholds()
    {
        var inventory = new Inventory();
        inventory.TryAdd(ItemType.Wrench);

        inventory.ToggleHold(0);
        inventory.ToggleHold(0);

        return !inventory.IsHolding(ItemType.Wrench) && inventory.HeldSlotIndices.Count == 0;
    }

    private static bool World_PickUpToolFromStation_AddsItToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 3f); // cross the corridor/reactor door at spawn height first
        MoveCharacterTo(world, 1, 7f, 5f); // reactor wrench station

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.Wrench) == 1;
    }

    private static bool World_WeldBreach_DoesNothingWithoutToolHeld()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor, never picks up a welding tool

        BreachEveryRoom(world); // force every room to end up with at least one breach
        MoveCharacterTo(world, 1, 11.5f, 0.5f); // stand right next to the corridor's top wall block

        var breachedBefore = RoomHasBreach(world.CreateSnapshot(), "corridor");
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        var breachedAfter = RoomHasBreach(world.CreateSnapshot(), "corridor");

        return breachedBefore && breachedAfter; // still breached — no welding tool in hand
    }

    private static bool World_SystemDamage_ZerosEffectiveAllocation()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 3, PowerDirection: 1f)); // WeaponCharger
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var allocatedBefore = world.CreateSnapshot().Power.Allocated[PowerSystemId.WeaponCharger];

        world.CutWireLink("trunk-weaponcharger");
        var effectiveWhileDamaged = world.GetEffectivePower(PowerSystemId.WeaponCharger);

        return allocatedBefore > 0f && effectiveWhileDamaged == 0f;
    }

    private static bool World_RepairSystem_RequiresWrenchHeldInHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.CutWireLink("trunk-shields"); // takes out both shield devices (cockpit + quarters)

        MoveCharacterTo(world, 1, 7f, 3f); // corridor -> reactor
        MoveCharacterTo(world, 1, 3f, 3f); // reactor -> cockpit
        MoveCharacterTo(world, 1, 3.5f, 1.5f); // cockpit shields device

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // no tool held — should fail
        var stillDamagedWithoutTool = !world.IsDeviceConnected("system-shields");

        MoveCharacterTo(world, 1, 7f, 3f); // cockpit -> reactor
        MoveCharacterTo(world, 1, 7f, 5f); // reactor wrench station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up wrench
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3.5f, 1.5f); // back to the shields device
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // repair

        return stillDamagedWithoutTool && world.IsDeviceConnected("system-shields");
    }

    private static bool Reactor_RemovingAllRods_ZerosOutput()
    {
        var reactor = new Reactor(maxOutput: 60f, maxFuel: 500f, fuelPerPowerUnitPerSecond: 0.05f);
        for (var i = 0; i < Reactor.RodSlotCount; i++)
            reactor.RemoveRod(i);

        // The fuel goes out with the rods now — it lives in them, not in a tank behind them.
        return reactor.Fuel == 0f && reactor.CurrentOutput == 0f;
    }

    private static bool Reactor_FreshRodIntoSpentSlot_ComesFullyCharged()
    {
        var reactor = new Reactor(maxOutput: 10f, maxFuel: 10f, fuelPerPowerUnitPerSecond: 1f);
        reactor.Step(deltaSeconds: 10, totalAllocatedPower: 10f); // burn every rod down to nothing
        if (reactor.Fuel != 0f || reactor.CurrentOutput != 0f)
            return false;

        reactor.InsertRod(0); // a rod carried in from the rack is a new one

        return Math.Abs(reactor.Fuel - reactor.RodCapacity) < 0.001f && reactor.CurrentOutput == reactor.MaxOutput;
    }

    private static bool Reactor_Step_BurnsRodsOneAtATime()
    {
        var reactor = new Reactor(maxOutput: 10f, maxFuel: 40f, fuelPerPowerUnitPerSecond: 1f);
        reactor.Step(deltaSeconds: 1.5, totalAllocatedPower: 10f); // 15 of 40 — the first rod and half the second

        return reactor.Rods[0] == 0f && Math.Abs((reactor.Rods[1] ?? -1f) - 5f) < 0.001f && reactor.Rods[3] == 10f;
    }

    private static bool World_ReactorSlot_RequiresProximityToReactor()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor — far from the reactor block

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0));

        return world.PowerGrid.Reactor.IsRodLoaded(0); // unchanged — still loaded, click didn't reach
    }

    private static bool World_ReactorSlot_RemoveRodReturnsItToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 9.5f, 1f); // reactor block

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0));

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return !world.PowerGrid.Reactor.IsRodLoaded(0) && inventory.MainSlots.Count(s => s == ItemType.FuelRod) == 1;
    }

    private static bool World_ReactorSlot_InsertRequiresHoldingRod()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 9.5f, 1f);

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // remove rod 0 -> inventory (not held)
        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // try to reinsert without holding it

        return !world.PowerGrid.Reactor.IsRodLoaded(0); // still empty
    }

    private static bool World_ReactorSlot_ReinsertHeldRod()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 9.5f, 1f);

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // remove rod 0 -> inventory
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it
        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // insert the held rod back

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return world.PowerGrid.Reactor.IsRodLoaded(0) && inventory.MainSlots.All(s => s != ItemType.FuelRod);
    }

    // The point of carrying a rod to the reactor: a dead reactor comes back to life on a fresh rod,
    // rather than the rod being a token that does nothing until some separate tank is topped up.
    private static bool World_ReactorSlot_InsertingCarriedRod_RefuelsFromEmpty()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 9.5f, 1f); // reactor block

        var reactor = world.PowerGrid.Reactor;
        reactor.Step(deltaSeconds: 100000, totalAllocatedPower: 60f); // run the loaded rods flat
        if (reactor.CurrentOutput != 0f)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // pull the spent rod
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0));    // take it in hand
        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // put a rod back in

        return Math.Abs(reactor.Fuel - reactor.RodCapacity) < 0.001f && reactor.CurrentOutput == reactor.MaxOutput;
    }

    private static bool Shield_TryAbsorbHit_DepletesPointsUntilEmpty()
    {
        var shield = new ShieldSystem();
        shield.Step(deltaSeconds: 100, shieldsPowerAllocation: 60f); // charge to full (clamped)

        var absorbedFirst = shield.TryAbsorbHit();
        var pointsAfterOne = shield.Points;

        return absorbedFirst && pointsAfterOne > 0f && pointsAfterOne < ShieldSystem.MaxPoints;
    }

    private static bool World_Shield_AbsorbsFirstAttackWithoutDamagingShip()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 2, PowerDirection: 1f)); // Shields
        for (var i = 0; i < 300; i++) // 10s — shield ramps to full while still in open space
            world.Step(RealtimeStep);
        var pointsBeforeAttack = world.CreateSnapshot().Shield.Points;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));

        // Step tick-by-tick and catch the exact moment the first attack lands (travel time plus
        // the 6s attack cooldown after arriving), rather than sampling long after — shield regen
        // is fast enough to mask the dip by then.
        var absorbedAHit = false;
        for (var i = 0; i < 15 * 30 && !absorbedAHit; i++)
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().Shield.Points < pointsBeforeAttack)
                absorbedAHit = true;
        }

        var snapshot = world.CreateSnapshot();
        return pointsBeforeAttack > 0f
            && absorbedAHit
            && snapshot.WallBlockStates.All(s => !s.Breached)
            && snapshot.TurretStates.All(t => !t.Damaged)
            && snapshot.SystemStates.All(s => !s.Damaged);
    }

    private static bool EnemyShip_IsRetreating_BelowThreshold()
    {
        var enemy = new EnemyShip(maxHp: 100f);
        enemy.ApplyDamage(85f); // Hp=15, under the 20% retreat threshold
        return enemy.IsRetreating;
    }

    private static bool EnemyShip_IsRetreating_FalseAboveThreshold()
    {
        var enemy = new EnemyShip(maxHp: 100f);
        enemy.ApplyDamage(50f); // Hp=50, above the threshold
        return !enemy.IsRetreating;
    }

    // Enemy AI only attacks during the Battle phase — get there via the galaxy map first (player
    // 1 must already exist). Each attack then competes between turrets (2), systems (5) and
    // individual outer-hull wall blocks (spread unevenly across all rooms) — 600 simulated
    // seconds gives enough draws that every room ends up with at least one breach with very low
    // residual flake risk.
    private static void BreachEveryRoom(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 600 * 30; i++)
            world.Step(RealtimeStep);
    }

    private static bool RoomHasBreach(WorldSnapshot snapshot, string roomId) =>
        snapshot.WallBlockStates.Any(s => s.Breached && snapshot.WallBlocks.First(b => b.Id == s.Id).RoomId == roomId);

    private static int CountBreaches(WorldSnapshot snapshot, string roomId) =>
        snapshot.WallBlockStates.Count(s => s.Breached && snapshot.WallBlocks.First(b => b.Id == s.Id).RoomId == roomId);

    private static bool World_EnemyAi_EventuallyBreachesEveryRoom()
    {
        var world = new World();
        world.SpawnCharacter(1); // position doesn't matter for this test

        BreachEveryRoom(world);

        var snapshot = world.CreateSnapshot();
        return world.Ship.Rooms.All(r => RoomHasBreach(snapshot, r.Id));
    }

    private static bool World_Decompression_DrainsHealthInBreachedRoom()
    {
        var world = new World();
        world.SpawnCharacter(1); // pilot — only sends commands, its health is never checked

        // Enemy AI only attacks once in Battle — get there first via the galaxy map.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        // A single breach only leaks oxygen slowly — wait for an actual breach, then keep
        // stepping until oxygen has actually dropped clearly (not just barely, which could
        // flicker back above 50 for a tick from diffusion) under the safe threshold. This search
        // can take a long time in the worst case, so nobody should be sitting in the room while
        // it runs (see below).
        for (var i = 0; i < 600 * 30 && !RoomHasBreach(world.CreateSnapshot(), "corridor"); i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 300 * 30; i++)
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "corridor").Oxygen < 40f)
                break;
        }

        // Spawn a fresh, full-health character straight into the now-dangerous corridor (that's
        // the ship's spawn point) right before measuring. A character present for the whole
        // search above would keep taking damage the entire time oxygen sits under the 50
        // threshold — which the 300s search budget is easily long enough to do, bottoming out at
        // 0 well before the measurement window and making "after < before" fail on bad luck.
        world.SpawnCharacter(2);
        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;
        for (var i = 0; i < 30; i++) // 1 more second while oxygen is critically low
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;

        return after < before;
    }

    // The generator physically sits in the corridor (Ship.cs: "system-oxygen" is in "corridor")
    // and only produces oxygen there in proportion to power routed to it (World.Atmosphere.cs).
    // Waiting for a corridor-specific breach via the normal long random fight (like
    // World_Decompression_DrainsHealthInBreachedRoom does) doesn't work as a "stays healthy when
    // powered" check: by the time corridor takes its own hit, other rooms have likely also taken
    // several unrelated breaches and sit far below FullOxygen — and since only the corridor has a
    // generator, heavily depleted neighbors can diffusion-drain it faster than one generator can
    // keep up, independent of whether the power question this test cares about is even true. So
    // instead: retry fresh, short (single attack-cycle) encounters until one lands exactly one
    // ship-wide breach in the corridor while every other room is still untouched (fresh spawn, so
    // everything else is still at FullOxygen) — an isolated scenario where full power should
    // trivially keep up with just its own room's single 3/sec leak. A single attack has only
    // ~7% odds of landing exactly this (most of its own outcomes are turret/system damage or a
    // wall breach elsewhere on the ship), so the retry budget needs a wide enough margin that
    // exhausting it is negligible, not just "usually enough".
    private static bool World_Oxygen_GeneratorRestoresRoomOxygenWhenPowered()
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var world = new World();
            world.SpawnCharacter(1); // pilot — only sends commands

            // PowerSystemId order: Oxygen(0), Engine, Shields, WeaponCharger, Secondary.
            world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 0, PowerDirection: 1f, TravelToPointId: "sector-alpha"));
            for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
                world.Step(RealtimeStep);

            for (var i = 0; i < 7 * 30; i++) // just past the first 6s attack-cooldown tick
                world.Step(RealtimeStep);

            var snapshot = world.CreateSnapshot();
            var totalBreaches = snapshot.WallBlockStates.Count(s => s.Breached);
            if (totalBreaches != 1 || !RoomHasBreach(snapshot, "corridor"))
                continue; // this attempt's single attack didn't land the isolated scenario we want

            for (var i = 0; i < 10 * 30; i++) // let it settle under full power
                world.Step(RealtimeStep);

            return world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "corridor").Oxygen > 70f;
        }

        return false; // never landed the isolated single-breach scenario within the attempt budget
    }

    private static bool World_RepairBreach_ClearsItViaInteract()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor

        MoveCharacterTo(world, 1, 11.5f, 5f); // corridor welding-tool station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up welding tool
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        MoveCharacterTo(world, 1, 11.5f, 0.5f); // stand next to the corridor's top wall block
        BreachEveryRoom(world); // force every room to end up with at least one breach

        // A room can hold several independent breaches now — a single weld only fixes the
        // nearest one, so assert the count drops by exactly one rather than "cleared".
        var breachCountBefore = CountBreaches(world.CreateSnapshot(), "corridor");
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        var breachCountAfter = CountBreaches(world.CreateSnapshot(), "corridor");

        return breachCountBefore > 0 && breachCountAfter == breachCountBefore - 1;
    }

    // The enemy AI can now damage a turret instead of breaching a room; a single F press on a
    // damaged turret repairs it rather than manning it, so a longer battle may need 2 presses.
    private static void EnsureManning(World world, int playerId, string turretId)
    {
        for (var i = 0; i < 3; i++)
        {
            if (world.CreateSnapshot().TurretStates.Any(t => t.Id == turretId && t.MannedByPlayerId == playerId))
                return;
            world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true));
        }
    }

    // Fires the bow turret until the enemy is defeated, reacting to whatever the enemy AI
    // throws at that turret along the way (reload trips, wrench repairs) instead of assuming a
    // fixed number of clean magazines — the AI's chance to damage a turret makes that assumption
    // flaky over a long fight.
    private static void FireBowTurretUntilEnemyDefeated(World world, int playerId)
    {
        const string turretId = "turret-bow";

        // Grab and hold a wrench up front so a mid-fight turret-damage attack can be repaired —
        // repair now requires the tool actually held in hand, not just F near the turret.
        // Two-leg move: cross the corridor/reactor door at spawn height first, then go to the
        // station — a straight diagonal can clip the door's edge and miss the crossing.
        MoveCharacterTo(world, playerId, 7f, 3f);
        MoveCharacterTo(world, playerId, 7f, 5f); // reactor wrench station
        world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // pick up
        world.ApplyCommand(playerId, new ClientCommand(playerId, ToggleHoldSlotIndex: 0)); // hold it

        // A sector's whole squadron is in the field at once now (World.EnemyFleet.cs), so clearing
        // one means shooting three hulls down rather than one - and shells that miss a wingman
        // sitting off the firing line cost iterations too.
        // The budget is generous on purpose: a run where the raiders keep disabling the gun spends
        // most of its iterations repairing and reloading rather than shooting, and running out mid
        // fight makes whatever test called this fail for a reason that has nothing to do with what
        // it was checking. Now that the roll sequence is seeded (World.EnemyAi.cs) an unlucky run is
        // reproducible rather than occasional - so it has to be survivable rather than rare.
        for (var iteration = 0; iteration < 400 && world.CreateSnapshot().Enemy.Hp > 0; iteration++)
        {
            var state = world.CreateSnapshot().TurretStates.Single(t => t.Id == turretId);

            if (state.Damaged)
            {
                MoveCharacterTo(world, playerId, 1.5f, 3f);
                if (world.CreateSnapshot().TurretStates.Single(t => t.Id == turretId).MannedByPlayerId == playerId)
                    world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // stand up first
                world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // repair
                continue;
            }

            if (state.AmmoRemaining <= 0)
            {
                if (state.MannedByPlayerId == playerId)
                    world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // stand up
                MoveCharacterTo(world, playerId, 15f, 3f);
                world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // pick up a crate
                MoveCharacterTo(world, playerId, 1.5f, 3f);
                world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // reload
                continue;
            }

            MoveCharacterTo(world, playerId, 1.5f, 3f);
            EnsureManning(world, playerId, turretId);
            world.ApplyCommand(playerId, new ClientCommand(playerId, FirePressed: true));
            for (var i = 0; i < 20; i++) // outlast the 0.5s cooldown before the next shot
                world.Step(RealtimeStep);
        }
    }

    private static bool World_Voyage_TravelingToHostileSectorStartsBattle()
    {
        var world = new World();
        world.SpawnCharacter(1);
        if (world.Phase != VoyagePhase.Station) // starts docked at the home station
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++) // ~1.6s travel time, generous margin
            world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.Battle;
    }

    private static bool World_Voyage_DefeatingEnemyReturnsToTraveling()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        FireBowTurretUntilEnemyDefeated(world, 1);

        // Open navigation: victory drops back into open space rather than auto-docking, so the
        // player can freely pick the next destination (a station, or another fight).
        return world.Phase == VoyagePhase.Traveling;
    }

    private static bool World_Voyage_StationRefuelsAndClearsBreaches()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Keep the reactor under real load throughout so there's fuel left to refill. Both go in
        // one command — a second ApplyCommand with default power fields would otherwise reset
        // the slider input before it ever gets a tick to act on.
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 0, PowerDirection: 1f, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
        for (var i = 0; i < 10 * 30; i++) // let the slider ramp to full and actually burn fuel for a while
            world.Step(RealtimeStep);
        var fuelDuringFlight = world.CreateSnapshot().Power.ReactorFuel;

        FireBowTurretUntilEnemyDefeated(world, 1);

        // Head back to the home station to resupply.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
        DockAtStation(world);

        var snapshot = world.CreateSnapshot();
        // Refuel snaps to MaxFuel exactly on arrival, but firing/repair activity can continue
        // (and burn a little more) right after — assert "topped back up", not "still exactly 500".
        // Measured against the tank being nearly full again rather than against a fixed number of
        // units gained: the burn rate is deliberately slow now (PowerGrid), so a fight's worth of
        // flying costs only a few units and any "gained at least N" threshold is really an
        // assertion about the rate, not about refuelling.
        return snapshot.Voyage.Phase == VoyagePhase.Station
            && fuelDuringFlight < 500f
            && snapshot.Power.ReactorFuel > fuelDuringFlight
            && snapshot.Power.ReactorFuel > 490f
            && snapshot.WallBlockStates.All(s => !s.Breached);
    }

    private static bool World_EnemyAi_DormantWhileTraveling()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 10; i++) // a handful of ticks — nowhere near arrival yet
            world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.Traveling && world.CreateSnapshot().WallBlockStates.All(s => !s.Breached);
    }

    private static bool World_Voyage_ShipMovesContinuouslyTowardTarget()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        var before = world.CreateSnapshot().Voyage.ShipMapPosition;
        for (var i = 0; i < 15; i++) // half a second — well short of the ~1.6s full trip
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Voyage.ShipMapPosition;

        return (after - before).Length() > 0f && world.Phase == VoyagePhase.Traveling; // moving, not yet arrived
    }

    private static bool World_Voyage_CannotChangeDestinationMidBattle()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station")); // try to flee
        world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.Battle; // still fighting — the command was ignored
    }

    private static bool World_SuitAction_RequiresProximityToLocker()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor — far from the engine-room locker

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).SuitActionRemaining == 0f;
    }

    private static bool World_SuitAction_TakesTimeAndLocksMovement()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 20f, 3f); // engine-room suit locker

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // start equipping
        var justStarted = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (justStarted.WearingSuit || justStarted.SuitActionRemaining <= 0)
            return false; // must not be instant

        world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0)); // try to walk away mid-action
        for (var i = 0; i < 10; i++) // well short of the 2s action duration
            world.Step(RealtimeStep);
        var mid = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (Math.Abs(mid.X - justStarted.X) > 0.01f)
            return false; // moved while busy

        for (var i = 0; i < 60; i++) // finish the action
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return after.WearingSuit && after.SuitActionRemaining == 0f;
    }

    private static bool World_SuitedCharacter_ImmuneToDecompression()
    {
        var world = new World();
        world.SpawnCharacter(1);

        EquipSuit(world, 1); // suit and its tank: an empty suit is no protection at all now

        // Enemy AI only attacks once in Battle — get there first via the galaxy map. Character 1
        // is suited (fully immune) so it can safely sit in engine through the whole search below;
        // character 2 (the unsuited control) isn't spawned until right before measuring — see why
        // in World_Decompression_DrainsHealthInBreachedRoom just above.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 600 * 30 && !RoomHasBreach(world.CreateSnapshot(), "engine"); i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 300 * 30; i++) // wait for oxygen to be clearly under the safe threshold
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "engine").Oxygen < 40f)
                break;
        }

        world.SpawnCharacter(2); // fresh, full health, spawns in the corridor
        MoveCharacterTo(world, 2, 20f, 3f); // brief walk into the now-dangerous engine room
        world.ApplyCommand(2, new ClientCommand(2)); // stop drifting once close enough

        var before1 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        var before2 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var after1 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        var after2 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;

        return Math.Abs(after1 - before1) < 0.01f // suited: untouched
            && after2 < before2; // unsuited: takes damage
    }

    private static bool World_SuitAction_IgnoredWhileMidAction()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 20f, 3f);

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // start equipping
        for (var i = 0; i < 10; i++)
            world.Step(RealtimeStep);
        var remainingBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).SuitActionRemaining;

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pressed again mid-action
        var remainingAfter = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).SuitActionRemaining;

        // A restart would jump remaining back up near the full 2s duration.
        return Math.Abs(remainingBefore - remainingAfter) < 0.01f;
    }

    private static bool World_Character_FacingTracksLastMoveDirection()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: -1)); // face "up"
        world.Step(RealtimeStep);
        var facingUp = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (facingUp.FacingY >= 0)
            return false;

        world.ApplyCommand(1, new ClientCommand(1)); // stop moving — facing should hold, not reset
        world.Step(RealtimeStep);
        var stillFacingUp = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return stillFacingUp.FacingY < 0;
    }

    private static bool World_LaserTurret_FiresUsingChargeWithoutAmmoCrate()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 6.5f, 3f); // laser turret periscope, reactor room
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it — no crate needed
        EnterBattle(world);

        var before = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge; // starts full
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        StepFor(world, 60);
        var snapshot = world.CreateSnapshot();
        var after = snapshot.TurretStates.Single(t => t.Id == "turret-laser").Charge;

        return before > 0 && after < before && snapshot.Enemy.Hp < 100f;
    }

    private static bool World_LaserTurret_RechargesOnlyFromWeaponChargerAllocation()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 6.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man

        for (var shot = 0; shot < 3; shot++) // 3 shots * 10 charge empties the 30-charge capacitor
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            for (var i = 0; i < 15; i++) // outlast the 0.4s cooldown
                world.Step(RealtimeStep);
        }
        var depleted = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge;

        for (var i = 0; i < 60; i++) // no power allocated to WeaponCharger -> should not recharge
            world.Step(RealtimeStep);
        var stillDepleted = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge;

        // PowerSystemId order: Oxygen, Engine, Shields, WeaponCharger(3), Secondary.
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 3, PowerDirection: 1f));
        for (var i = 0; i < 90; i++)
            world.Step(RealtimeStep);
        var recharged = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge;

        return depleted < 1f && Math.Abs(stillDepleted - depleted) < 0.01f && recharged > depleted;
    }

    private static bool World_Trade_BuyItem_DeductsCreditsAndAddsToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts docked at the home station

        var creditsBefore = world.CreateSnapshot().Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench));

        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;
        return snapshot.Credits == creditsBefore - 20 && inventory.MainSlots.Count(s => s == ItemType.Wrench) == 1;
    }

    private static bool World_Trade_BuyItem_FailsWithoutEnoughCredits()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Two spacesuits at 150 each spend exactly the 300 starting credits.
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Spacesuit));
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Spacesuit));
        var creditsAfterSpending = world.CreateSnapshot().Credits;

        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench)); // can't afford it now
        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;

        return creditsAfterSpending == 0 && snapshot.Credits == 0 && inventory.MainSlots.Count(s => s == ItemType.Wrench) == 0;
    }

    private static bool World_Trade_BuyItem_FailsWhenInventoryFull()
    {
        var world = new World();
        world.SpawnCharacter(1);

        for (var i = 0; i < Inventory.MainSlotCount; i++)
            world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench));
        var creditsAfterFilling = world.CreateSnapshot().Credits; // 300 - 9*20 = 120

        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench)); // row is full — no-op
        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;

        return creditsAfterFilling == 120
            && snapshot.Credits == 120
            && inventory.MainSlots.Count(s => s == ItemType.Wrench) == Inventory.MainSlotCount;
    }

    private static bool World_Trade_SellItem_RefundsCreditsAndClearsSlot()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench)); // lands in slot 0
        var creditsBeforeSell = world.CreateSnapshot().Credits;

        world.ApplyCommand(1, new ClientCommand(1, SellSlotIndex: 0));

        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;
        return snapshot.Credits == creditsBeforeSell + 8 && inventory.MainSlots[0] is null;
    }

    private static bool World_Trade_BuyAndSell_FailWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
        if (world.Phase != VoyagePhase.Battle)
            return false; // must actually have left the station for this test to mean anything

        var creditsBefore = world.CreateSnapshot().Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench));

        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;
        return snapshot.Credits == creditsBefore && inventory.MainSlots.Count(s => s == ItemType.Wrench) == 0;
    }

    private static bool World_Quest_Accept_SetsDestinationDifferentFromCurrentStation()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts docked at home-station

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        var quest = world.CreateSnapshot().ActiveQuest;

        return quest is not null && quest.DestinationPointId != "home-station" && quest.RewardCredits > 0;
    }

    private static bool World_Quest_Accept_FailsWhenAlreadyActive()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        var questAfterFirst = world.CreateSnapshot().ActiveQuest;

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery)); // already have one
        var questAfterSecond = world.CreateSnapshot().ActiveQuest;

        return questAfterFirst is not null && questAfterSecond == questAfterFirst;
    }

    private static bool World_Quest_TurnIn_FailsAtWrongStation()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        var snapshotAfterAccept = world.CreateSnapshot();
        var quest = snapshotAfterAccept.ActiveQuest;

        // Still docked at home-station, which can never be the destination (accept filters it out).
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));
        var snapshot = world.CreateSnapshot();

        return snapshot.ActiveQuest == quest && snapshot.Credits == snapshotAfterAccept.Credits;
    }

    private static bool World_Quest_TurnIn_AtDestination_AwardsCreditsAndClearsQuest()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is null)
            return false;
        var creditsBefore = world.CreateSnapshot().Credits;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: quest.DestinationPointId));
        DockAtStation(world);
        if (world.Phase != VoyagePhase.Station)
            return false; // didn't dock as expected

        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));
        var snapshot = world.CreateSnapshot();

        return snapshot.ActiveQuest is null && snapshot.Credits == creditsBefore + quest.RewardCredits;
    }

    private static bool World_Quest_Accept_FailsWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
        if (world.Phase != VoyagePhase.Battle)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        return world.CreateSnapshot().ActiveQuest is null;
    }

    private static bool World_MedKit_PickupFromToolStation_AddsToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 16f, 3f); // corridor -> quarters at spawn height
        MoveCharacterTo(world, 1, 16f, 5f); // medkit station

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.MedKit) == 1;
    }

    private static bool World_MedKit_HealsSelfAndConsumesItem()
    {
        var world = new World();
        world.SpawnCharacter(1); // stays in the corridor (spawn point) while it gets dangerous

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 600 * 30 && !RoomHasBreach(world.CreateSnapshot(), "corridor"); i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 300 * 30; i++)
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health < 60f)
                break;
        }
        if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health >= 60f)
            return false; // never got hurt enough within budget

        MoveCharacterTo(world, 1, 16f, 3f); // corridor -> quarters at spawn height
        MoveCharacterTo(world, 1, 16f, 5f); // medkit station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        var healthBeforeHeal = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // use the medkit

        var snapshot = world.CreateSnapshot();
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        return me.Health > healthBeforeHeal && me.Inventory!.MainSlots.All(s => s != ItemType.MedKit);
    }

    private static bool World_MedKit_DoesNothingAtFullHealth()
    {
        var world = new World();
        world.SpawnCharacter(1);

        MoveCharacterTo(world, 1, 16f, 3f);
        MoveCharacterTo(world, 1, 16f, 5f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // try to use it at full health

        var snapshot = world.CreateSnapshot();
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        return before >= Character.MaxHealth && me.Health == before
            && me.Inventory!.MainSlots.Any(s => s == ItemType.MedKit); // not consumed
    }

    // Isolates the bleeding drain from decompression without needing to relocate anyone (walking
    // across a ship that's taken 900+ seconds of random combat risks passing through other
    // breached rooms too - and a single isolated breach turns out not to be dangerous at all:
    // with both neighbors still at full oxygen, diffusion inflow from them alone comfortably
    // outpaces what corridor's few wall blocks could ever leak, so real danger here requires
    // broader ship-wide damage, which then makes any other room a gamble too). Instead: two
    // characters share the exact same room at the exact same tick throughout, so they experience
    // identical decompression damage - character 2 enters first and is left to cross below the
    // bleeding threshold, character 3 enters later from full health and hasn't crossed it yet.
    // From that point, any difference in their per-tick Health drop can only be the extra
    // bleeding drain applied to character 2, since the room's oxygen level affects both equally.
    private static bool World_Bleeding_DrainsHealthFasterThanDecompressionAlone()
    {
        var world = new World();
        world.SpawnCharacter(1); // pilot, sends commands

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 600 * 30 && !RoomHasBreach(world.CreateSnapshot(), "corridor"); i++)
            world.Step(RealtimeStep);
        if (!RoomHasBreach(world.CreateSnapshot(), "corridor"))
            return false;

        for (var i = 0; i < 300 * 30; i++)
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "corridor").Oxygen < 40f)
                break;
        }

        // Character 2 enters and takes damage down to just above the bleeding threshold (not yet
        // bleeding) - stopping there, not indefinitely, avoids bottoming out before the window
        // below even starts.
        world.SpawnCharacter(2);
        var reachedJustAboveThreshold = false;
        for (var i = 0; i < 200 * 30; i++)
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health <= 55f)
            {
                reachedJustAboveThreshold = true;
                break;
            }
        }
        if (!reachedJustAboveThreshold)
            return false;

        // Character 3 enters fresh, right now, into the exact same room - from this point on
        // both experience identical decompression conditions every tick.
        world.SpawnCharacter(3);

        var crossedWhileOtherSafe = false;
        for (var i = 0; i < 60 * 30; i++)
        {
            world.Step(RealtimeStep);
            var snapshot = world.CreateSnapshot();
            var c2 = snapshot.Characters.Single(c => c.PlayerId == 2);
            var c3 = snapshot.Characters.Single(c => c.PlayerId == 3);
            if (c2.Health < Character.BleedingThreshold && c3.Health >= Character.BleedingThreshold)
            {
                crossedWhileOtherSafe = true;
                break;
            }
            if (c2.Health <= 0f)
                break; // bottomed out before giving us the comparison window - budget exhausted
        }
        if (!crossedWhileOtherSafe)
            return false;

        var before = world.CreateSnapshot();
        var health2Before = before.Characters.Single(c => c.PlayerId == 2).Health;
        var health3Before = before.Characters.Single(c => c.PlayerId == 3).Health;

        for (var i = 0; i < 30; i++) // 1 more second, both still in the identical room
            world.Step(RealtimeStep);

        var after = world.CreateSnapshot();
        var drop2 = health2Before - after.Characters.Single(c => c.PlayerId == 2).Health;
        var drop3 = health3Before - after.Characters.Single(c => c.PlayerId == 3).Health;

        return drop2 > drop3; // character 2 (bleeding + decompression) drops faster than 3 (decompression only)
    }

    private static bool World_Upgrade_PurchaseReactorOutput_IncreasesMaxOutputAndDeductsCredits()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts docked at the home station

        var before = world.CreateSnapshot();
        var creditsBefore = before.Credits;
        var maxOutputBefore = before.Reactor.MaxOutput;

        world.ApplyCommand(1, new ClientCommand(1, PurchaseUpgradeTrack: ShipUpgradeTrack.ReactorOutput));

        var snapshot = world.CreateSnapshot();
        return snapshot.Credits == creditsBefore - ShipUpgradeCatalog.Find(ShipUpgradeTrack.ReactorOutput).CostPerLevel[0]
            && snapshot.Reactor.MaxOutput > maxOutputBefore
            && snapshot.ShipUpgradeLevels[ShipUpgradeTrack.ReactorOutput] == 1;
    }

    private static bool World_Upgrade_Purchase_FailsWithoutEnoughCredits()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Level 1 (200) leaves 100 - not enough for level 2 (400).
        world.ApplyCommand(1, new ClientCommand(1, PurchaseUpgradeTrack: ShipUpgradeTrack.ReactorOutput));
        var creditsAfterLevel1 = world.CreateSnapshot().Credits;

        world.ApplyCommand(1, new ClientCommand(1, PurchaseUpgradeTrack: ShipUpgradeTrack.ReactorOutput));
        var snapshot = world.CreateSnapshot();

        return creditsAfterLevel1 == 100
            && snapshot.Credits == 100
            && snapshot.ShipUpgradeLevels[ShipUpgradeTrack.ReactorOutput] == 1; // still level 1, second purchase rejected
    }

    private static bool World_Upgrade_Purchase_FailsWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
        if (world.Phase != VoyagePhase.Battle)
            return false;

        var creditsBefore = world.CreateSnapshot().Credits;
        world.ApplyCommand(1, new ClientCommand(1, PurchaseUpgradeTrack: ShipUpgradeTrack.WeaponDamage));

        var snapshot = world.CreateSnapshot();
        return snapshot.Credits == creditsBefore && snapshot.ShipUpgradeLevels[ShipUpgradeTrack.WeaponDamage] == 0;
    }

    private static bool World_Upgrade_WeaponDamage_IncreasesShotDamage()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, PurchaseUpgradeTrack: ShipUpgradeTrack.WeaponDamage)); // +3 dmg

        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man the bow turret
        EnterBattle(world);

        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        StepFor(world, 60);

        // Base shot damage is 10 (Ship.cs turret-bow) - with the upgrade it should deal more.
        return world.CreateSnapshot().Enemy.Hp < 90f;
    }

    private static bool World_Wiring_LayBackup_KeepsSystemPoweredAfterTrunkCut()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 3, PowerDirection: 1f)); // WeaponCharger
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var allocatedBefore = world.CreateSnapshot().Power.Allocated[PowerSystemId.WeaponCharger];

        MoveCharacterTo(world, 1, 19f, 3f); // corridor -> quarters -> engine, at spawn height
        MoveCharacterTo(world, 1, 22.5f, 1.5f); // wire spool station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up spool
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        world.ApplyCommand(1, new ClientCommand(1, WireLinkInteractId: "trunk-weaponcharger")); // lay backup

        world.CutWireLink("trunk-weaponcharger"); // primary severed - backup should now carry it
        var effectiveAfterCut = world.GetEffectivePower(PowerSystemId.WeaponCharger);

        return allocatedBefore > 0f && effectiveAfterCut > 0f;
    }

    private static bool World_Wiring_RepairViaPanel_RestoresConnectionNoProximityNeeded()
    {
        var world = new World();
        world.SpawnCharacter(1); // stays in the corridor the whole test - repair needs no proximity
        world.CutWireLink("trunk-shields");
        var damagedBefore = !world.IsDeviceConnected("system-shields");

        MoveCharacterTo(world, 1, 7f, 3f); // corridor -> reactor
        MoveCharacterTo(world, 1, 7f, 5f); // reactor wrench station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up wrench
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        // Repairs the trunk from the reactor room - nowhere near the shields devices themselves.
        world.ApplyCommand(1, new ClientCommand(1, WireLinkInteractId: "trunk-shields"));

        return damagedBefore && world.IsDeviceConnected("system-shields");
    }

    private static bool World_Wiring_ShieldsOneDropCut_HalvesEffectivePower()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // PowerSystemId order: Oxygen(0), Engine(1), Shields(2), WeaponCharger(3), Secondary(4).
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 2, PowerDirection: 1f));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var fullPower = world.GetEffectivePower(PowerSystemId.Shields);

        world.CutWireLink("drop-shields-2"); // only the second generator's drop is cut
        var halfPower = world.GetEffectivePower(PowerSystemId.Shields);

        return fullPower > 0f && Math.Abs(halfPower - fullPower / 2f) < 0.01f;
    }

    private static bool World_Wiring_LayBackup_RequiresSpoolAndOnlyOnce()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // No spool held yet - clicking the link should do nothing.
        world.ApplyCommand(1, new ClientCommand(1, WireLinkInteractId: "trunk-oxygen"));
        var hasBackupWithoutSpool = world.CreateSnapshot().WireLinkStates.First(s => s.LinkId == "trunk-oxygen").HasBackup;

        MoveCharacterTo(world, 1, 19f, 3f); // corridor -> quarters -> engine, at spawn height
        MoveCharacterTo(world, 1, 22.5f, 1.5f); // wire spool station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up spool
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        world.ApplyCommand(1, new ClientCommand(1, WireLinkInteractId: "trunk-oxygen")); // lay backup, consumes it
        var afterFirst = world.CreateSnapshot();
        var hasBackupAfter = afterFirst.WireLinkStates.First(s => s.LinkId == "trunk-oxygen").HasBackup;
        var spoolConsumed = afterFirst.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.All(s => s != ItemType.WireSpool);

        // Pick up a second spool and try again - the link already has a backup, so this must
        // no-op (and, importantly, not silently consume the second spool for nothing).
        MoveCharacterTo(world, 1, 19f, 3f);
        MoveCharacterTo(world, 1, 22.5f, 1.5f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0));
        world.ApplyCommand(1, new ClientCommand(1, WireLinkInteractId: "trunk-oxygen"));
        var secondSpoolKept = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Count(s => s == ItemType.WireSpool) == 1;

        return !hasBackupWithoutSpool && hasBackupAfter && spoolConsumed && secondSpoolKept;
    }

    // Gets the ship out of dock and into open space. Needed by anything about venting to vacuum:
    // while docked, the outer airlock opens onto the station's pressurized dock chamber instead
    // (World.Atmosphere.cs), so the same door does nothing there.
    private static void CastOffIntoSpace(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);
    }

    // Shared setup for the M15 helm tests: fly to the asteroid field, ramp Engine power up, then
    // walk the character to the helm console and man it.
    private static void EnterAsteroidFieldAndManHelm(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f); // corridor -> reactor -> cockpit, at the doors' shared height
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it
    }

    // Shared setup for tests that need the ship actually docked at a station: arriving now only
    // drops the ship into VoyagePhase.StationApproach (World.StationDocking.cs, manual docking) -
    // fly it the rest of the way in ourselves, same helm pattern as EnterAsteroidFieldAndManHelm.
    // EnterStationApproach always places the ship directly in line with the station (facing +X),
    // so a straight HelmThrustX:1 is all that's needed to reach the docking capture zone.
    private static void DockAtStation(World world)
    {
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        // Three attempts, because the recovery below is best-effort: a badly shot-up ship can need
        // the engine repaired *and* the power grid re-balanced before it will move at all, and a
        // single pass occasionally leaves it still dead in space just short of the dock.
        // ...and the second engine block can be the damaged one, and the wiring can be cut on top of
        // that, so a run of bad luck needs more than three passes to walk off. The seeded roll
        // sequence (World.EnemyAi.cs) makes such a run reproducible rather than occasional, which is
        // exactly why the recovery has to be able to grind through it.
        for (var attempt = 0; attempt < 8 && world.Phase == VoyagePhase.StationApproach; attempt++)
            TryDockingRun(world);
    }

    private static void TryDockingRun(World world)
    {
        // A caller that just fought its way out of a battle (FireBowTurretUntilEnemyDefeated)
        // leaves the character manning the bow turret - can't walk anywhere until standing up.
        if (world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1))
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        // Random combat (World.EnemyAi.cs) can happen to damage the Engine system device itself,
        // not just breach a wall block (game_design.md's "known pitfall" about random attack
        // targets, see continue.md) - if so the ship simply can't move at all here no matter how
        // much power is allocated (IsDeviceConnected gates GetEffectivePower). Repair it first.
        // Every engine block, not just the first: each class carries two of them (WireNetwork's
        // system-engine/system-engine-2), and a run where the second is the damaged one left the
        // ship dead in space with this recovery reporting success.
        foreach (var engineDevice in world.Ship.SystemDevices.Where(d => d.System == PowerSystemId.Engine))
        {
            if (!world.CreateSnapshot().SystemStates.First(s => s.DeviceId == engineDevice.Id).Damaged)
                continue;

            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var holdingTool = me.Inventory!.HeldMainSlotIndices
                .Select(i => me.Inventory.MainSlots[i])
                .Any(t => t is ItemType.Wrench or ItemType.Screwdriver);
            if (!holdingTool)
            {
                MoveCharacterTo(world, 1, 7f, 3f);
                MoveCharacterTo(world, 1, 7f, 5f); // reactor wrench toolbox
                world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up
                var slot = Array.IndexOf(world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.Wrench);
                world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
            }

            MoveCharacterTo(world, 1, engineDevice.Position.X, 3f);
            MoveCharacterTo(world, 1, engineDevice.Position.X, engineDevice.Position.Y);
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // repair
        }

        // A caller may have already boosted some other system to the reactor's full output
        // (e.g. World_Voyage_StationRefuelsAndClearsBreaches keeps Oxygen maxed) - that leaves
        // zero headroom for Engine (PowerGrid.Step's maxForThis is capped by othersTotal), so
        // free it back up first or Engine would never actually ramp above 0.
        foreach (var systemIndex in new[] { 0, 2, 3, 4 })
        {
            world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: systemIndex, PowerDirection: -1f));
            for (var i = 0; i < 90; i++)
                world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm)
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it
        // Docking is a deliberate press now, not an automatic capture (World.StationDocking.cs),
        // so the approach has to actually be flown: a plain bang-bang controller that thrusts
        // toward the berth whenever the ship is going too slowly to close the gap and brakes
        // whenever it's going too fast to mate. Just "full thrust then stabilize" deadlocks -
        // braking from full speed stops the ship short of the berth and it sits there forever.
        for (var i = 0; i < 60 * 30 && world.Phase == VoyagePhase.StationApproach; i++)
        {
            if (world.CanDockNow)
            {
                world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
                world.Step(RealtimeStep);
                continue;
            }

            var shipField = world.CreateSnapshot().ShipField;
            var toPort = world.DockBerthPosition - new Vec2(shipField.X, shipField.Y); // the berth, not the airlock rectangle
            var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();

            if (speed > 1.5f)
                world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
            else
                world.ApplyCommand(1, SteerToward(world, 1, world.DockBerthPosition));

            world.Step(RealtimeStep);
        }

        if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm)
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // stand up from the helm
    }

    // The helm's whole control model: A/D swing the bow without moving the ship, W drives it along
    // whatever heading it's holding, and X backs it straight out again.
    private static bool World_Helm_WasdSteersByHeadingAndReverseBacksOut()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var before = world.CreateSnapshot().ShipField;
        world.ApplyCommand(1, new ClientCommand(1, HelmTurn: 1f));
        StepFor(world, 30);
        var turned = world.CreateSnapshot().ShipField;
        if (Math.Abs(turned.RotationDegrees - before.RotationDegrees) < 45f)
            return false; // the bow didn't swing
        if (new Vec2(turned.X - before.X, turned.Y - before.Y).Length() > 0.01f)
            return false; // turning is not travelling

        float AlignmentWithNose()
        {
            var field = world.CreateSnapshot().ShipField;
            var nose = TurretMount.FromDegrees(field.RotationDegrees + world.Ship.ForwardDegrees);
            var course = new Vec2(field.VelocityX, field.VelocityY).Normalized();
            return nose.X * course.X + nose.Y * course.Y;
        }

        world.ApplyCommand(1, new ClientCommand(1, HelmTurn: 0f, HelmThrottle: 1f));
        StepFor(world, 60);
        if (AlignmentWithNose() < 0.99f)
            return false; // ahead has to mean along the bow, not along some world axis

        world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
        StepFor(world, 120);
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: -1f));
        StepFor(world, 60);

        return AlignmentWithNose() < -0.99f; // moving against its own bow, without having turned round
    }

    private static bool World_Helm_Thrust_AcceleratesShipWithInertia()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var before = world.CreateSnapshot().ShipField;
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 60; i++) // 2s
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().ShipField;

        return before.VelocityX == 0f && after.VelocityX > 0f && after.X > before.X;
    }

    // The saved thrust vector must keep being applied even after the pilot stands up (game_design.md
    // Phase 3, M15 - "если игрок не за пультом... корабль продолжает лететь") - checked here by
    // confirming the ship is still accelerating (not just coasting) with nobody manning the helm.
    private static bool World_Helm_ThrustPersists_AfterStandingUp()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        // Only a few ticks of acceleration before standing up - engine power is ramped enough here
        // that the ship would already be at max speed (and thus no longer measurably accelerating)
        // if given the full 30-tick build-up the other helm tests use.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 5; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // stand up
        var stillManning = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm;
        var velocityAtStandUp = world.CreateSnapshot().ShipField.VelocityX;

        for (var i = 0; i < 30; i++) // no further input at all
            world.Step(RealtimeStep);
        var velocityLater = world.CreateSnapshot().ShipField.VelocityX;

        return !stillManning && velocityLater > velocityAtStandUp;
    }

    private static bool World_Helm_Stabilize_BringsShipToStop()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 60; i++) // build up speed
            world.Step(RealtimeStep);
        var movingFast = world.CreateSnapshot().ShipField.VelocityX > 1f;

        world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
        for (var i = 0; i < 5 * 30; i++) // plenty of time to fully decelerate
            world.Step(RealtimeStep);
        var stopped = world.CreateSnapshot().ShipField;

        return movingFast && Math.Abs(stopped.VelocityX) < 0.01f && Math.Abs(stopped.VelocityY) < 0.01f;
    }

    // No power on Engine at all -> the ship must not accelerate (game_design.md Phase 3 -
    // "двигается... если на него подана энергия"); deliberately never allocates power here.
    private static bool World_Helm_NoEnginePower_ShipDoesNotAccelerate()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f); // corridor -> reactor -> cockpit, at the doors' shared height
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));

        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        var field = world.CreateSnapshot().ShipField;
        return field.VelocityX == 0f && field.VelocityY == 0f;
    }

    // The rock's outline is the thing everything else is measured against, so it has to be the same
    // outline every time it's asked for and it has to actually differ from a circle.
    private static bool AsteroidShape_IsAStableNonCircularOutline()
    {
        var rock = new Asteroid("asteroid-test", 100f, 100f, 5f);

        var first = AsteroidShape.Outline(rock);
        var second = AsteroidShape.Outline(rock);
        for (var i = 0; i < first.Length; i++)
            if ((first[i] - second[i]).Length() > 0.0001f)
                return false; // must not reshuffle between calls

        var radii = new float[first.Length];
        for (var i = 0; i < first.Length; i++)
            radii[i] = (first[i] - rock.Position).Length();
        if (radii.Max() - radii.Min() < 0.5f)
            return false; // that's a circle, not a rock

        // Every vertex sits exactly on the surface by the same measure the physics uses.
        foreach (var vertex in first)
            if (Math.Abs(AsteroidShape.DistanceOutside(rock, vertex)) > 0.01f)
                return false;

        // And a point at the nominal radius is inside on some bearings and outside on others -
        // which is precisely what a circular test could never tell you.
        var insideSomewhere = false;
        var outsideSomewhere = false;
        for (var i = 0; i < 32; i++)
        {
            var angle = i * (MathF.PI * 2f / 32);
            var probe = rock.Position + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * rock.Radius;
            if (AsteroidShape.Contains(rock, probe))
                insideSomewhere = true;
            else
                outsideSomewhere = true;
        }

        return insideSomewhere && outsideSomewhere;
    }

    // The gap between the Corvette's engine pylons is open space. The bounding box the boots used
    // to walk on covers it, so a crewman could stroll across the hole with nothing underfoot.
    private static bool HullSilhouette_TreatsTheGapBetweenPylonsAsOpenSpace()
    {
        var rooms = Ship.Create(ShipKind.Corvette).Rooms;
        var gap = new Vec2(6.75f, 17f); // below the reactor hall, between the two side bays

        var insideBoundingBox = gap.X >= rooms.Min(r => r.Left) && gap.X <= rooms.Max(r => r.Right) &&
                                gap.Y >= rooms.Min(r => r.Top) && gap.Y <= rooms.Max(r => r.Bottom);

        // Standing there should put the boots on the nearest real plating, not leave them hanging
        // in the middle of the notch.
        var stood = HullSilhouette.SnapToSurface(rooms, gap, 0.35f);

        return insideBoundingBox
            && !HullSilhouette.Contains(rooms, gap)
            && HullSilhouette.DistanceOutside(rooms, gap) > 0.5f
            && Math.Abs(HullSilhouette.DistanceOutside(rooms, stood) - 0.35f) < 0.02f;
    }

    private static bool World_Ship_CollidesWithAsteroid_StopsShipAndBreachesHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var field = world.AsteroidField;
        var nearestAsteroid = field.Asteroids.OrderBy(a => (a.Position - field.Center).Length()).First();
        var breached = false;
        for (var i = 0; i < 30 * 30 && !breached; i++)
        {
            world.ApplyCommand(1, SteerToward(world, 1, nearestAsteroid.Position));
            world.Step(RealtimeStep);
            breached = world.CreateSnapshot().WallBlockStates.Any(s => s.Breached);
        }

        // The rock holes the hull and stops the ship - and then the pilot can back out of it. That
        // last part is the whole point: refusing the entire step on contact used to weld the ship
        // to whatever it touched, because every direction with any component into the rock was
        // thrown away along with the part that would have carried it clear.
        float GapToRock()
        {
            var field = world.CreateSnapshot().ShipField;
            return (nearestAsteroid.Position - new Vec2(field.X, field.Y)).Length();
        }

        // Astern on the same heading - the bow is still pointed at the rock, so this is the ship
        // backing straight out of it (HelmThrottle < 0).
        var gapAtImpact = GapToRock();
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: -1f));
        StepFor(world, 8 * 30);

        return breached && GapToRock() > gapAtImpact + 5f;
    }

    private static bool World_ToggleDoor_ViaClientCommand_FlipsState()
    {
        var world = new World();
        world.SpawnCharacter(1);

        var before = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor"));
        var after = world.CreateSnapshot().DoorStates.First(d => d.DoorId == "door-cockpit-reactor").IsOpen;

        return before && !after;
    }

    private static bool World_Door_Closed_BlocksMovementLikeWall()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor")); // starts open -> closed

        MoveCharacterTo(world, 1, 5f, 3f); // corridor -> reactor, right up against the now-closed door

        for (var i = 0; i < 30; i++) // keep pushing left, into the closed door
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.X >= 4.95f; // never made it into the cockpit (would need X < 5)
    }

    // Isolated from the rest of the ship (inner door closed) so this only exercises the vacuum-sink
    // formula itself - with the inner door left open instead, the rest of the ship's oxygen acts as
    // a large reservoir feeding the chamber back via diffusion and the decay is much slower (see
    // World_OpenInnerDoor_LetsVentedChamberDrainRestOfShip for that coupled scenario).
    private static bool World_AirlockOuterDoor_Open_LeaksChamberToVacuum()
    {
        var world = new World();
        world.SpawnCharacter(1);
        CastOffIntoSpace(world); // docked, that door opens onto the station rather than vacuum
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-engine-airlock")); // starts open -> closed
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 15 * 30; i++)
            world.Step(RealtimeStep);

        var chamberOxygen = world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen;
        return chamberOxygen < 10f;
    }

    private static bool World_AirlockOuterDoor_Closed_ChamberStaysPressurized()
    {
        var world = new World();
        world.SpawnCharacter(1); // door-airlock-vacuum is never touched - stays at its safe default (closed)

        for (var i = 0; i < 15 * 30; i++)
            world.Step(RealtimeStep);

        var chamberOxygen = world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen;
        return chamberOxygen > 99f;
    }

    // The core of M16: venting the chamber to space must not doom the rest of the crew, as long as
    // they close the door between the chamber and the rest of the ship first.
    private static bool World_ClosedInnerDoor_KeepsRestOfShipSealedFromVentedChamber()
    {
        var world = new World();
        world.SpawnCharacter(1);
        CastOffIntoSpace(world); // docked, that door opens onto the station rather than vacuum
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-engine-airlock")); // starts open -> closed
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var chamberOxygen = snapshot.RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen;
        var engineOxygen = snapshot.RoomOxygen.First(o => o.RoomId == "engine").Oxygen;
        return chamberOxygen < 10f && engineOxygen > 99f;
    }

    // Same setup, but the inner door is left at its default (open) - the vent now drags the rest
    // of the ship down too, which is exactly the risk the previous test's closed door avoids.
    private static bool World_OpenInnerDoor_LetsVentedChamberDrainRestOfShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        CastOffIntoSpace(world); // docked, that door opens onto the station rather than vacuum
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // starts closed -> open

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var engineOxygen = world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "engine").Oxygen;
        return engineOxygen < 90f;
    }

    // Shared EVA test setup (game_design.md Phase 3, M17).
    private static void EnterAsteroidFieldStationary(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);
    }

    // Suiting up now means suit *and* bottle: an empty suit is a shell that the airlock won't let
    // anyone through in (OxygenTankDefinitions), so every test that goes outside needs the tank as
    // much as it needs the suit. withTank: false is for the tests that check that gate itself.
    private static void EquipSuit(World world, int playerId, bool withTank = true)
    {
        MoveCharacterTo(world, playerId, 20f, 3f); // suit locker, engine room
        world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // start equipping
        for (var i = 0; i < 90; i++) // outlast the 2s equip action
            world.Step(RealtimeStep);

        if (!withTank || playerId != 1)
            return;
        TakeTankFromRack(world);
        AttachTankTo(world, WornSuitSlotIndex);
    }

    // MoveCharacterTo can't be reused for the final approach to the outer door: the instant the
    // crossing happens, CharacterState.X/Y switches from interior to AsteroidField world
    // coordinates (see World.cs CreateSnapshot), so a stale interior target like (27, 3) turns into
    // nonsense and its bang-bang homing would just walk the character right back toward the ship
    // (and potentially back inside). This walks a fixed direction instead, stopping the moment
    // IsOutside flips (or once maxTicks runs out, e.g. when suit/door gating is expected to block it).
    private static void WalkFixedDirection(World world, int playerId, float moveX, float moveY, int maxTicks = 60)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == playerId);
            if (me.IsOutside)
                break;
            world.ApplyCommand(playerId, new ClientCommand(playerId, MoveX: moveX, MoveY: moveY));
            world.Step(RealtimeStep);
        }

        // The real client resends a fresh (usually zero) move vector every tick regardless; a test
        // driving ApplyCommand by hand has to do that explicitly, or the last nonzero direction
        // sent here would keep being applied indefinitely (harmless for interior movement, which
        // is room-clamped, but an EVA character attached to the ship would just keep sliding along
        // the hull on it forever).
        world.ApplyCommand(playerId, new ClientCommand(playerId, MoveX: 0, MoveY: 0));
    }

    private static bool World_Eva_ExitRequiresSuit()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum")); // open it

        MoveCharacterTo(world, 1, 23f, 3f); // corridor -> ... -> engine -> airlock-chamber
        WalkFixedDirection(world, 1, 1f, 0f); // try to walk straight through the open outer door, unsuited

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.IsOutside;
    }

    private static bool World_Eva_ExitSuited_SetsIsOutsideAndAttachesToShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // walk through the open outer door, suited this time

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.IsOutside && me.IsEvaAttached;
    }

    // Walks to the ship's storage rack and picks up one tool along the way, so there's something in
    // the carried row to drag. Returns the slot index the tool landed in.
    // MoveCharacterTo walks both axes at once, which slams into a bulkhead whenever the target
    // isn't on the doors' shared mid-height - so cross the ship along that row first, then step off
    // it. Same routing every other multi-room test does by hand.
    private static void WalkAcrossShipTo(World world, float x, float y)
    {
        const float doorRow = 3f;
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        MoveCharacterTo(world, 1, me.X, doorRow);
        MoveCharacterTo(world, 1, x, doorRow);
        MoveCharacterTo(world, 1, x, y);
    }

    private static int StandAtRackHolding(World world, ItemType item)
    {
        var station = world.Ship.ToolStations.First(s => s.Item == item);
        WalkAcrossShipTo(world, station.X, station.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        var rack = world.Ship.StorageRack;
        WalkAcrossShipTo(world, rack.X, rack.Y);
        var slots = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots;
        for (var i = 0; i < slots.Count; i++)
            if (slots[i] == item)
                return i;
        return -1;
    }

    private static ItemType? RackSlot(World world, int index) => world.CreateSnapshot().RackSlots[index];

    private static ItemType? MainSlot(World world, int index) =>
        world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots[index];

    private static bool World_Rack_DragFromInventory_StowsItem()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        if (from < 0)
            return false;

        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 7)));

        return RackSlot(world, 7) == ItemType.Wrench && MainSlot(world, from) is null;
    }

    // Dropping onto an occupied slot exchanges the two rather than overwriting - losing an item to
    // a slightly-off drop would be a nasty way to destroy the only cutter on the ship.
    private static bool World_Rack_DropOntoOccupiedSlot_SwapsTheTwo()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var wrenchSlot = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, wrenchSlot),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 0)));

        var screwdriverSlot = StandAtRackHolding(world, ItemType.Screwdriver);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, screwdriverSlot),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 0)));

        return RackSlot(world, 0) == ItemType.Screwdriver && MainSlot(world, screwdriverSlot) == ItemType.Wrench;
    }

    // The rack is a physical shelf, not a pocket dimension - you have to be standing at it.
    private static bool World_Rack_AwayFromTheRack_MoveIsRefused()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        WalkAcrossShipTo(world, 3f, 3f); // off to the cockpit, far from the rack

        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 3)));

        return RackSlot(world, 3) is null && MainSlot(world, from) == ItemType.Wrench;
    }

    // Rearranging your own row needs no rack at all - and anything that moves leaves your hands,
    // since the held-hand list is keyed by slot index.
    private static bool World_Inventory_DragBetweenOwnSlots_MovesAndEmptiesHands()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: from));
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.HeldMainSlotIndices.Contains(from))
            return false;

        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Main, 8)));

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots[8] == ItemType.Wrench && inventory.MainSlots[from] is null &&
               inventory.HeldMainSlotIndices.Count == 0;
    }

    private static bool World_Save_RoundTripsRackContents()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var from = StandAtRackHolding(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, from),
            MoveItemTo: new SlotRef(ItemSlotKind.Rack, 12)));

        var save = world.CreateSave();
        var restored = new World();
        restored.SpawnCharacter(1);
        restored.ApplySave(save);

        return restored.CreateSnapshot().RackSlots[12] == ItemType.Wrench;
    }

    // Magnetic boots hold you against the plating, not somewhere in a shell around it: wherever you
    // walk, you're on the hull's outline - never floating a metre or two off it, and never adrift
    // across the middle of the footprint with nothing underfoot.
    private static bool World_Eva_MagnetizedWalk_StaysFlushAgainstTheHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // out onto the hull

        var rooms = world.Ship.Rooms;
        var hullCenter = new Vec2(
            (rooms.Min(r => r.Left) + rooms.Max(r => r.Right)) / 2,
            (rooms.Min(r => r.Top) + rooms.Max(r => r.Bottom)) / 2);

        // The invariant is one number: standing exactly the boot clearance off the plating,
        // measured against the compartments themselves (HullSilhouette). Checking it per axis
        // against a bounding box - which is what this used to do - can't describe an outside
        // corner, where the right answer is to pivot around it at a constant distance.
        // The ship is stationary and unrotated here, so world position minus the field position is
        // the hull-local offset directly.
        bool OnTheOutline()
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            if (!me.IsOutside || !me.IsEvaAttached)
                return false;
            var field = world.CreateSnapshot().ShipField;
            var layoutPoint = hullCenter + new Vec2(me.X - field.X, me.Y - field.Y);
            return Math.Abs(HullSilhouette.DistanceOutside(rooms, layoutPoint) - 0.35f) < 0.02f;
        }

        if (!OnTheOutline())
            return false;

        // Walk each way in turn, including straight into the hull and around a corner.
        foreach (var (dx, dy) in new[] { (0f, -1f), (-1f, 0f), (0f, 1f), (1f, 0f), (-1f, -1f) })
        {
            for (var i = 0; i < 40; i++)
            {
                world.ApplyCommand(1, new ClientCommand(1, MoveX: dx, MoveY: dy));
                world.Step(RealtimeStep);
                if (!OnTheOutline())
                    return false;
            }
        }

        return true;
    }

    // The magnets are contact, not proximity. They used to reach a couple of units, which snatched
    // a jump out of the air and snapped the character onto a surface it visibly hadn't reached yet -
    // the flight ended before it arrived. Measured at the moment the boots grab, on the position
    // from the tick *before* the snap: the gap that was actually crossed, not where the snap put it.
    private static bool World_Eva_BootsGrabOnContact_NotAcrossTheGap()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        var deposit = world.AsteroidField.OreDeposits.First(d => d.Id == "ore-4b");
        var rock = world.AsteroidField.Asteroids.First(a => a.Id == deposit.AsteroidId);
        ExitShipIntoVacuum(world);

        var start = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var push = new Vec2(deposit.X - start.X, deposit.Y - start.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: push.X, PushOffDirectionY: push.Y));

        var gapWhenCaught = float.NaN;
        for (var i = 0; i < 40 * 30; i++)
        {
            var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var gapBefore = AsteroidShape.DistanceOutside(rock, new Vec2(before.X, before.Y));
            var toward = new Vec2(deposit.X - before.X, deposit.Y - before.Y).Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: toward.X, MoveY: toward.Y));
            world.Step(RealtimeStep);

            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsEvaAttached)
            {
                gapWhenCaught = gapBefore;
                break;
            }
        }

        // The boots reach half a unit; the last tick of flight covers a few tenths more at the speed
        // a jetpack burn builds up, and the grab is found by sampling along that step. So contact
        // is a gap of about one unit measured a tick early - and nowhere near the couple of units
        // the old proximity magnets grabbed from, which is what this pins down. A gap this small
        // also proves it caught this rock rather than something it passed on the way.
        return !float.IsNaN(gapWhenCaught) && gapWhenCaught < 1f;
    }

    // Stepping out leaves you standing on the door's own rectangle now that there's no nudge into
    // open space - walking along the hull away from it must not read as "walked back into the
    // airlock" and drag you inside again.
    private static bool World_Eva_WalkingAwayFromTheDoor_StaysOutside()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        // Along the hull, away from the door - and also pushing outward, straight at the door's own
        // rectangle from the outside. Neither counts as going back in.
        foreach (var (dx, dy) in new[] { (0f, -1f), (1f, 0f), (0f, 1f) })
        {
            for (var i = 0; i < 40; i++)
            {
                world.ApplyCommand(1, new ClientCommand(1, MoveX: dx, MoveY: dy));
                world.Step(RealtimeStep);
                if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside)
                    return false;
            }
        }

        return true;
    }

    // Player 1 exits and stays magnetized to the hull; player 2 stays inside and pilots the ship
    // from the helm - player 1's world position must shift in lockstep, since a magnetized EVA
    // character moves rigidly with whatever it's attached to (game_design.md Phase 3, M17).
    private static bool World_Eva_AttachedToShip_MovesWithShipWhenShipMoves()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        var beforeSnapshot = world.CreateSnapshot();
        var player1Before = beforeSnapshot.Characters.Single(c => c.PlayerId == 1);
        if (!player1Before.IsOutside)
            return false;
        var shipBefore = beforeSnapshot.ShipField;

        world.ApplyCommand(2, new ClientCommand(2, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        MoveCharacterTo(world, 2, 3f, 3f);
        MoveCharacterTo(world, 2, 3f, 4f); // helm console
        world.ApplyCommand(2, new ClientCommand(2, InteractPressed: true)); // man it
        world.ApplyCommand(2, new ClientCommand(2, HelmThrottle: 1f)); // straight +X - no rotation involved
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var afterSnapshot = world.CreateSnapshot();
        var shipAfter = afterSnapshot.ShipField;
        var player1After = afterSnapshot.Characters.Single(c => c.PlayerId == 1);

        var shipDeltaX = shipAfter.X - shipBefore.X;
        var characterDeltaX = player1After.X - player1Before.X;

        return shipDeltaX > 0.5f && Math.Abs(characterDeltaX - shipDeltaX) < 0.1f;
    }

    private static bool World_Eva_PushOff_BecomesFreeFloatingWithVelocity()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: 1f, PushOffDirectionY: 0f));
        world.Step(RealtimeStep);

        var afterPush = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (afterPush.IsEvaAttached)
            return false; // must be free-floating now

        var posBefore = new Vec2(afterPush.X, afterPush.Y);
        for (var i = 0; i < 30; i++) // 1s of pure drift, no further input
            world.Step(RealtimeStep);
        var afterDrift = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var posAfter = new Vec2(afterDrift.X, afterDrift.Y);

        return posAfter.X > posBefore.X + 0.5f;
    }

    private static bool World_Eva_Jetpack_ExhaustsFuelThenKeepsDriftingAtLastVelocity()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);
        // Push off along the same axis as the jetpack burn below (+Y) - clears the ship's attach
        // zone faster than pushing sideways would (the zone hugs the whole hull along X).
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: 0f, PushOffDirectionY: 1f));
        world.Step(RealtimeStep);

        // Burn through all jetpack fuel holding a thrust direction - JetpackMaxFuel(100) /
        // JetpackFuelPerSecond(10) = 10s - stopping as soon as it's actually empty rather than
        // continuing to drift needlessly further on a loop sized for the worst case.
        for (var i = 0; i < 15 * 30; i++)
        {
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).JetpackFuel <= 0f)
                break;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 1));
            world.Step(RealtimeStep);
        }

        var afterBurn = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (afterBurn.JetpackFuel != 0f)
            return false; // should be fully exhausted well before 15s

        // Two more equal windows, still holding the same input the whole time: if the jetpack still
        // had any effect, the second window's displacement would exceed the first's (still
        // accelerating). Out of fuel, velocity is constant, so the two should match closely.
        var posAtEmpty = new Vec2(afterBurn.X, afterBurn.Y);
        for (var i = 0; i < 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 1));
            world.Step(RealtimeStep);
        }
        var afterWindow1 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var delta1 = new Vec2(afterWindow1.X - posAtEmpty.X, afterWindow1.Y - posAtEmpty.Y).Length();

        for (var i = 0; i < 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 1));
            world.Step(RealtimeStep);
        }
        var afterWindow2 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var delta2 = new Vec2(afterWindow2.X - afterWindow1.X, afterWindow2.Y - afterWindow1.Y).Length();

        return Math.Abs(delta1 - delta2) < 0.05f;
    }

    private static bool World_Eva_AutoReattachToShip_WhenDriftingBack()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: 1f, PushOffDirectionY: 0f));
        for (var i = 0; i < 30; i++) // drift away for 1s
            world.Step(RealtimeStep);

        var midway = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (midway.IsEvaAttached)
            return false; // sanity: shouldn't have reattached yet, still moving away

        // Jetpack thrust back toward the ship, stopping the instant it reattaches (game_design.md
        // Phase 3, M17) - held any longer than that and, now walking rather than drifting, it'd
        // just keep going and walk itself straight back inside through the airlock door within the
        // same loop, which isn't what this test is checking.
        var reattached = false;
        for (var i = 0; i < 10 * 30 && !reattached; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
            reattached = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsEvaAttached;
        }

        return reattached;
    }

    private static bool World_Eva_ReenterShip_ReturnsInsideAtAirlockChamber()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // exit, attached to the ship

        var afterExit = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (!afterExit.IsOutside)
            return false;

        // Walk back in the -X direction (toward the door) while still attached.
        for (var i = 0; i < 5 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }

        var afterReturn = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !afterReturn.IsOutside;
    }

    // Shared M18 mining setup: pick up and hold the cutter, suit up, exit through the (opened)
    // outer door, then fly - aiming continuously at the target the whole way, coasting once the
    // jetpack's fuel runs out - to within mining range of a specific ore deposit.
    // Inventory.WornSuitSlot - the socket on the suit being worn, addressed like a row slot.
    private const int WornSuitSlotIndex = -1;

    // Walks to whichever tank rack this hull actually has rather than to a hardcoded spot - every
    // class keeps one near its suit locker, but at its own coordinates.
    private static void TakeTankFromRack(World world)
    {
        var rack = world.Ship.ToolStations.First(t => t.Item == ItemType.OxygenTank);
        MoveCharacterTo(world, 1, rack.X, rack.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
    }

    private static void AttachTankTo(World world, int targetSlot)
    {
        var slots = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray();
        var tankSlot = Array.IndexOf(slots, ItemType.OxygenTank);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: tankSlot, AttachTankToSlot: targetSlot));
    }

    // Cutter in hand, suit on, out through the airlock and standing on the plating - the part every
    // trip outside shares, whatever it goes on to do out there.
    private static void ExitShipIntoVacuum(World world)
    {
        MoveCharacterTo(world, 1, 21.5f, 3f); // corridor -> ... -> engine, at the doors' shared height
        MoveCharacterTo(world, 1, 21.5f, 5f); // engine room cutter toolbox
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up cutter
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        // Neither the suit nor the torch works empty (OxygenTankDefinitions), so a trip outside now
        // starts at the tank rack: one tank into the cutter, and EquipSuit brings the suit's own.
        TakeTankFromRack(world);
        AttachTankTo(world, Array.IndexOf(
            world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.Cutter));

        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // exit, attached to the ship
    }

    private static void ExitShipAndFlyTo(World world, Vec2 targetWorldPos)
    {
        ExitShipIntoVacuum(world);

        var exitPos = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var pushDirection = new Vec2(targetWorldPos.X - exitPos.X, targetWorldPos.Y - exitPos.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: pushDirection.X, PushOffDirectionY: pushDirection.Y));
        world.Step(RealtimeStep);

        for (var i = 0; i < 40 * 30; i++) // aim at the target the whole way - a no-op once fuel is spent, just coasting on whatever velocity remains
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(targetWorldPos.X - me.X, targetWorldPos.Y - me.Y);
            // Ride it all the way in until the boots actually grab. The old "close enough" cutoff
            // of a whole unit only worked while the magnets reached that far; now that they grab on
            // contact (World.Eva.cs), stopping short leaves the character adrift beside the rock -
            // and a drifter with an empty jetpack can't push off anything, so the trip back home
            // would never start.
            if (me.IsEvaAttached || toTarget.Length() <= 0.5f)
                return;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }
    }

    // Holds the cutter's flame on a block, aimed from wherever the character ended up, until it
    // comes apart or the budget runs out. Returns the ticks it took, so a caller can tell "cut it"
    // from "never touched it".
    private static int CutBlock(World world, string depositId, int maxTicks = 20 * 30)
    {
        var block = world.AsteroidField.OreDeposits.First(d => d.Id == depositId);
        for (var i = 0; i < maxTicks; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            if ((world.CreateSnapshot().OreDepositStates.First(s => s.DepositId == depositId).Hp) <= 0f)
                return i;

            var aim = new Vec2(block.X - me.X, block.Y - me.Y).Normalized();
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: aim.X, LookY: aim.Y));
            world.Step(RealtimeStep);
        }
        return maxTicks;
    }

    // A suit is a shell: without a tank in it, the airlock won't let anyone through, because
    // stepping into vacuum in an empty suit is just a slower way of stepping into vacuum.
    private static bool World_Eva_SuitWithoutTank_CannotStepOutside()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        EquipSuit(world, 1, withTank: false); // suit on, socket empty

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // push at the open door

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (me.IsOutside || me.SuitTank is not null)
            return false;

        // With a tank plugged in, the same walk works - proving it was the air that was missing and
        // not something else about the door. Back out to the door's own height first: MoveCharacterTo
        // walks both axes at once, and a diagonal out of the airlock chamber leaves the doorway's
        // 1.8-unit band before it reaches the wall, so the crossing never happens.
        MoveCharacterTo(world, 1, 21.5f, 3f);
        TakeTankFromRack(world);
        AttachTankTo(world, WornSuitSlotIndex);
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside;
    }

    // The tank is spent by being outside, and an empty one stops protecting: at that point the suit
    // is a shell again and its wearer starts suffocating (World.OxygenTanks.cs).
    private static bool World_Eva_SuitTankRunsDownInVacuum()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        ExitShipIntoVacuum(world);

        var started = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (!started.IsOutside || started.SuitTank is not > 0f)
            return false;

        for (var i = 0; i < 20 * 30; i++) // stand on the hull and breathe
            world.Step(RealtimeStep);
        var afterAWhile = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (afterAWhile.SuitTank >= started.SuitTank || afterAWhile.Health < 100f)
            return false; // must have been spent, and must not hurt while there's air left

        for (var i = 0; i < 700 * 30; i++) // past the tank's whole endurance and then some
            world.Step(RealtimeStep);
        var starved = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return starved.SuitTank == 0f && starved.Health < 100f;
    }

    private static bool World_Mining_CutterFlameBreaksBlockIntoPickableItem()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        var deposit = world.AsteroidField.OreDeposits.First(d => d.Id == "ore-4b");
        ExitShipAndFlyTo(world, deposit.Position);

        var ticks = CutBlock(world, deposit.Id);
        var afterCut = world.CreateSnapshot();
        if (ticks >= 20 * 30 || afterCut.OreDepositStates.First(s => s.DepositId == deposit.Id).Hp > 0f)
            return false;
        if (!afterCut.DroppedItems.Any(d => d.Item == ItemType.Mineral))
            return false;

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick the ore up off the rock
        var afterPickup = world.CreateSnapshot();
        return afterPickup.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Contains(ItemType.Mineral)
               && !afterPickup.DroppedItems.Any(d => d.Item == ItemType.Mineral);
    }

    // The tank is what makes the torch a torch: without one in its socket the flame never lights,
    // however long the button is held (World.Cutting.cs).
    private static bool World_Mining_CutterWithoutTank_CutsNothing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        var deposit = world.AsteroidField.OreDeposits.First(d => d.Id == "ore-4b");
        ExitShipAndFlyTo(world, deposit.Position);

        // Pull the tank back out of the cutter and try to work with a dead torch.
        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        var cutterSlot = Array.IndexOf(inventory.MainSlots.ToArray(), ItemType.Cutter);
        world.ApplyCommand(1, new ClientCommand(1, DetachTankSlot: cutterSlot));

        var before = world.CreateSnapshot().OreDepositStates.First(s => s.DepositId == deposit.Id).Hp;
        CutBlock(world, deposit.Id, maxTicks: 5 * 30);
        var after = world.CreateSnapshot();

        return Math.Abs(after.OreDepositStates.First(s => s.DepositId == deposit.Id).Hp - before) < 0.001f
               && after.DroppedItems.Count == 0
               && after.Characters.Single(c => c.PlayerId == 1).CutterTank is null;
    }

    // A block is gone once it's cut through: it drops one item and stops being anything the flame
    // can bite on, so standing there burning tank oxygen produces nothing more.
    private static bool World_Mining_CutBlock_DropsOnceAndIsGone()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);
        var deposit = world.AsteroidField.OreDeposits.First(d => d.Id == "ore-3a");
        ExitShipAndFlyTo(world, deposit.Position);

        CutBlock(world, deposit.Id);
        var dropsAfterFirst = world.CreateSnapshot().DroppedItems.Count(d => d.Item == ItemType.Mineral);

        CutBlock(world, deposit.Id, maxTicks: 5 * 30); // keep burning at a hole in the rock
        var dropsAfterExtra = world.CreateSnapshot().DroppedItems.Count(d => d.Item == ItemType.Mineral);

        return dropsAfterFirst == 1 && dropsAfterExtra == 1
               && world.CreateSnapshot().OreDepositStates.First(s => s.DepositId == deposit.Id).Hp <= 0f;
    }

    // The generic sell flow (World.Trade.cs) doesn't care where the character physically is, only
    // that the ship is docked - mining just needed to prove Mineral reaches an inventory slot at
    // all; turning it into credits is exactly the same mechanic already covered by the M10 trade
    // tests, exercised here with a mined item instead of a bought one.
    // Flies to the asteroid field, cuts `count` ore out of a deposit and walks back aboard,
    // leaving the ship free to travel. Shared by the M18 sell test and the mining-contract tests -
    // the Trader prices Mineral out of reach on purpose, so genuinely mining it is the only way a
    // test can get any.
    private static void MineOre(World world, int count)
    {
        EnterAsteroidFieldStationary(world);
        // One block, one item: cutting `count` of them is the only way to come home with `count`
        // minerals now that a block is a body with hit points rather than a marker with charges.
        var blocks = world.AsteroidField.OreDeposits.Where(d => d.AsteroidId == "asteroid-4").Take(count).ToList();
        ExitShipAndFlyTo(world, blocks[0].Position);

        foreach (var block in blocks)
        {
            FlyToWithinReach(world, block.Position);
            CutBlock(world, block.Id);
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick the ore up
        }

        ReturnAboardFromEva(world);
    }

    // Walks along the rock toward a block until it's within the cutter's reach. Magnetised movement
    // is a walk along the surface, so this is the same thing a player does with WASD - the blocks of
    // one vein sit next to each other on the same rock.
    private static void FlyToWithinReach(World world, Vec2 target)
    {
        for (var i = 0; i < 20 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(target.X - me.X, target.Y - me.Y);
            if (toTarget.Length() <= World.CutterReachUnits * 0.7f)
                return;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }
    }

    private static bool World_Mining_SellMineralAtStation_RefundsCreditsAndClearsSlot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MineOre(world, 1);

        var slotIndex = Array.IndexOf(world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.Mineral);
        var creditsBefore = world.Credits;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
        DockAtStation(world);

        world.ApplyCommand(1, new ClientCommand(1, SellSlotIndex: slotIndex));

        var afterSell = world.CreateSnapshot();
        var slotCleared = afterSell.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots[slotIndex] is null;
        return world.Credits > creditsBefore && slotCleared;
    }

    private static void ReturnAboardFromEva(World world)
    {
        // Fly back and physically re-enter the ship before traveling anywhere - a docked/
        // traveling ship isn't somewhere you can be EVA outside of with nowhere to be
        // (World.Eva.cs), and DockAtStation below needs to walk the character to the helm.
        // The outer door's own Position is in ship-local coordinates, a different frame than the
        // EVA character's field-world X/Y - convert it via the ship's hull center (the same
        // convention World.Eva.cs's GetEvaWorldPosition uses), which is trivial here since the
        // ship is stationary and unrotated the whole time (EnterAsteroidFieldStationary).
        var shipFieldForDoor = world.CreateSnapshot().ShipField;
        var hullCenterLocal = new Vec2(
            (world.Ship.Rooms.Min(r => r.Left) + world.Ship.Rooms.Max(r => r.Right)) / 2f,
            (world.Ship.Rooms.Min(r => r.Top) + world.Ship.Rooms.Max(r => r.Bottom)) / 2f);
        var doorLocal = world.Ship.AirlockOuterDoors.First().Position;
        var doorFieldTarget = new Vec2(
            shipFieldForDoor.X + (doorLocal.X - hullCenterLocal.X),
            shipFieldForDoor.Y + (doorLocal.Y - hullCenterLocal.Y));

        // Mining flies far enough that jetpack fuel is very likely already exhausted (M18's own
        // ExitShipAndFlyTo helper drains it getting out there) - MoveX/Y alone would be a no-op
        // (StepFreeFloating only accelerates while JetpackFuel > 0). Push off toward the ship
        // instead (HandlePushOff doesn't need fuel, only needs to already be attached to
        // something - the nearby asteroid it just mined, per TryAutoAttach), then coast/correct.
        var beforePush = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var pushDir = new Vec2(doorFieldTarget.X - beforePush.X, doorFieldTarget.Y - beforePush.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: pushDir.X, PushOffDirectionY: pushDir.Y));
        world.Step(RealtimeStep);

        for (var i = 0; i < 40 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toShip = new Vec2(doorFieldTarget.X - me.X, doorFieldTarget.Y - me.Y);
            if (me.IsEvaAttached || toShip.Length() <= 0.5f) // same as the flight out: coast in until the plating catches
                break;
            var dir = toShip.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y)); // jetpack correction if any fuel remains, harmless otherwise
            world.Step(RealtimeStep);
        }
        for (var i = 0; i < 5 * 30; i++) // walk in through the door (attached to the ship by now)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }
    }

    // Every ship class must keep the same 6 device ids the wiring minigame's fixed topology
    // (WireNetwork.CreateDefault) expects — otherwise a device silently loses its repairable
    // wire link (World.Wiring.cs's IsDeviceConnected degrades to "always on" instead of crashing,
    // but that would quietly break the wiring puzzle for that ship).
    private static readonly string[] ExpectedSystemDeviceIds =
    {
        "system-shields", "system-shields-2", "system-weapon-charger",
        "system-oxygen", "system-secondary", "system-engine", "system-engine-2",
    };

    private static bool Ship_Corvette_HasSideGunsTwoPortsAndSameWireDeviceIds()
    {
        var ship = Ship.Create(ShipKind.Corvette);
        if (ship.Rooms.Count != 5 || ship.AirlockOuterDoors.Count != 2)
            return false;
        // Suits are stored at the ways out, one locker per port, not somewhere across the ship.
        if (!ship.AirlockOuterDoors.All(d => ship.SuitLockers.Any(l => l.RoomId == d.RoomId)))
            return false;
        if (!ship.SystemDevices.Select(d => d.Id).OrderBy(x => x).SequenceEqual(ExpectedSystemDeviceIds.OrderBy(x => x)))
            return false;

        // The broadside: both barrels leave the gun deck's own walls, pointing opposite ways.
        var armory = ship.GetRoom("armory");
        var starboard = TurretMount.For(ship.Rooms, ship.Turrets, ship.Turrets.First(t => t.Id == "turret-starboard"));
        var port = TurretMount.For(ship.Rooms, ship.Turrets, ship.Turrets.First(t => t.Id == "turret-port"));

        return starboard.Position.X > armory.Right && starboard.OutwardDegrees == 0f
            && port.Position.X < armory.Left && port.OutwardDegrees == 180f;
    }

    // The hull runs bow-to-stern down the screen, so its spine doors are horizontal slots - the
    // first ones in the game. Walking the whole ship proves RoomLayout crosses them as happily as
    // the vertical ones every other class uses.
    // A hull laid out down the screen has to lead with its nose. Rotation aligns the ship's own
    // forward axis with its velocity, so a Corvette flying +X ends up rotated +90: its bow (local
    // -Y) is what's pointing along the course, not its flank.
    private static bool World_ShipField_CorvetteFliesNoseFirst()
    {
        var world = new World(ShipKind.Corvette);
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        StepFor(world, 60);

        MoveCharacterTo(world, 1, 6.75f, 2.4f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        for (var i = 0; i < 8 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
            world.Step(RealtimeStep);
        }

        var field = world.CreateSnapshot().ShipField;
        var forward = TurretMount.FromDegrees(field.RotationDegrees + world.Ship.ForwardDegrees);
        var course = new Vec2(field.VelocityX, field.VelocityY).Normalized();

        // The nose and the course agree to within a few degrees (dot product ~1).
        return forward.X * course.X + forward.Y * course.Y > 0.99f;
    }

    // Going EVA used to be keyed to a room literally named "airlock-chamber", so a hull that puts
    // its ports in ordinary compartments (one on each beam here) locked its crew inside for good.
    private static bool World_Eva_CorvetteCrewGoesOutThroughABeamPort()
    {
        var world = new World(ShipKind.Corvette);
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);

        MoveCharacterTo(world, 1, 6.75f, 11f);  // down the spine into the reactor hall
        MoveCharacterTo(world, 1, 12.3f, 10.6f); // starboard bay, at its suit locker
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // suit up
        StepFor(world, 90);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).WearingSuit)
            return false;

        // This hull keeps a tank rack at each of its two suit lockers - grab the starboard one and
        // plug it in, since a suit with an empty socket won't get anyone through the port.
        MoveCharacterTo(world, 1, 12.3f, 12.4f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        AttachTankTo(world, WornSuitSlotIndex);

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 12.5f, 9.5f); // line up with the port
        WalkFixedDirection(world, 1, 1f, 0f);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside;
    }

    private static bool Ship_Corvette_CrewWalksTheSpineAndOutToBothBays()
    {
        var world = new World(ShipKind.Corvette);
        world.SpawnCharacter(1);

        MoveCharacterTo(world, 1, 6.75f, 11f); // down the spine, cockpit -> gun deck -> reactor hall
        var inReactorHall = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (inReactorHall.Y < 8f)
            return false; // never made it through the horizontal doors

        MoveCharacterTo(world, 1, 2f, 11f); // out to the shield bay
        var toPort = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        MoveCharacterTo(world, 1, 6.75f, 11f);
        MoveCharacterTo(world, 1, 11.5f, 11f); // and across to life support
        var toStarboard = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return toPort.X < 4f && toStarboard.X > 9.5f;
    }

    private static bool Ship_Scout_HasAirlockChamberAndSameWireDeviceIds()
    {
        var ship = Ship.CreateScout();
        return ship.Rooms.Any(r => r.Id == "airlock-chamber") &&
               ship.AirlockOuterDoors.Count == 1 &&
               ship.SystemDevices.Select(d => d.Id).OrderBy(x => x).SequenceEqual(ExpectedSystemDeviceIds.OrderBy(x => x));
    }

    private static bool Ship_Cruiser_HasAirlockChamberAndThreeTurrets()
    {
        var ship = Ship.CreateCruiser();
        return ship.Rooms.Any(r => r.Id == "airlock-chamber") &&
               ship.Turrets.Count == 3 &&
               ship.SystemDevices.Select(d => d.Id).OrderBy(x => x).SequenceEqual(ExpectedSystemDeviceIds.OrderBy(x => x));
    }

    private static bool World_ShipKindScout_SpawnsAndSteps()
    {
        var world = new World(ShipKind.Scout);
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > world.Ship.SpawnPoint.X;
    }

    private static bool World_ShipKindCruiser_SpawnsAndSteps()
    {
        var world = new World(ShipKind.Cruiser);
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > world.Ship.SpawnPoint.X;
    }

    private static bool RoomLayout_MoveAlongAxis_BlocksAtWallWithoutDoor()
    {
        var station = Station.CreateDefault();
        var dockRoomId = station.DockRoomId;
        var (pos, roomId) = station.MoveAlongAxis(new Vec2(2.5f, 0.5f), dockRoomId, new Vec2(0, -1f), _ => true);
        return roomId == dockRoomId && Math.Abs(pos.Y - 0f) < 0.01f; // clamped at the top hull wall
    }

    // Arriving at a station (game_design.md section 10 - walkable stations, manual docking) no
    // longer teleports straight into VoyagePhase.Station - it drops into StationApproach first
    // (World.StationDocking.cs), same as the M15 asteroid-field arrival pattern.
    private static bool World_Station_ArrivingSetsStationApproachNotInstantDock()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts already docked at home-station - travel elsewhere first
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.StationApproach;
    }

    private static bool World_Station_DockAtStation_ReachesStationPhase()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        return world.Phase == VoyagePhase.Station && world.CreateSnapshot().Voyage.DockedPointId == "outpost-gamma";
    }

    // Walking through the ship's own outer airlock door while actually docked leads onto the
    // station instead of into vacuum (World.StationDocking.cs's TryCrossIntoStation) - no suit
    // needed, unlike the EVA case, since it's a sealed connector.
    private static bool World_Station_WalkThroughOpenOuterDoor_EntersStation()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts already docked at home-station
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.OnStation && !me.IsOutside;
    }

    private static bool World_Station_WalkBackThroughConnector_ReturnsToShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false; // didn't make it onto the station as expected

        WalkFixedDirection(world, 1, -1f, 0f);
        return !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation;
    }

    // Same open outer door, but the ship isn't docked (mid-battle) - falls through to ordinary
    // hull-boundary movement, exactly like it already does outside VoyagePhase.AsteroidField for
    // the vacuum-EVA case (no new special-casing needed, see World.Movement.cs).
    private static bool World_Station_CannotCrossOuterDoorWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.OnStation && !me.IsOutside;
    }

    // Shared boarding setup (game_design.md Phase 3): start a battle, arm the character with a
    // weapon, suit up, EVA out and fly across to the enemy hull. Reuses M18's exact
    // fly-toward-a-target pattern - boarding is the same "drift to a point in field space" move,
    // just aimed at the enemy ship instead of an ore deposit.
    private static void BoardEnemyShip(World world, ItemType weapon, float toolboxX, float toolboxY)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, toolboxX, 3f);
        MoveCharacterTo(world, 1, toolboxX, toolboxY);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up the weapon
        var slot = Array.IndexOf(world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), weapon);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));

        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // exit into vacuum, attached to the hull

        var target = world.CreateSnapshot().EnemyShipPosition;
        var exitPos = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var pushDirection = new Vec2(target.X - exitPos.X, target.Y - exitPos.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: pushDirection.X, PushOffDirectionY: pushDirection.Y));
        world.Step(RealtimeStep);

        // The target is re-read every tick: enemy hulls fly now (World.EnemyFleet.cs), so steering
        // at where one was when the boarder left the airlock would only ever reach empty space.
        for (var i = 0; i < 60 * 30; i++)
        {
            var snapshot = world.CreateSnapshot();
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            if (me.OnEnemyShip)
                break;
            var current = snapshot.EnemyShipPosition;
            var dir = new Vec2(current.X - me.X, current.Y - me.Y).Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0)); // see WalkFixedDirection's own note
    }

    private static bool World_Boarding_EvaDuringBattle_ReachesEnemyShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife, 14f, 5f);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.OnEnemyShip && !me.IsOutside;
    }

    private static bool World_Boarding_FireWeaponDamagesCrewInSameRoom()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife, 14f, 5f); // knife is melee-only - has to close in

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        var defender = world.CreateSnapshot().EnemyCrew.First(c => c.RoomId == world.EnemyShipLayout.BoardingRoomId);
        var healthBefore = defender.Health;

        // Walk right up to the defender in the boarding room, then swing.
        for (var i = 0; i < 5 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(defender.X - me.X, defender.Y - me.Y);
            if (toTarget.Length() <= 0.6f)
                break;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, FirePressed: true));
        world.Step(RealtimeStep);

        var after = world.CreateSnapshot().EnemyCrew.First(c => c.Id == defender.Id);
        return after.Health < healthBefore;
    }

    private static bool World_Boarding_WithoutWeaponHeld_DoesNothing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife, 14f, 5f);

        // Drop the knife out of hand - unarmed, Space must do nothing at all.
        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        var knifeSlot = Array.IndexOf(inventory.MainSlots.ToArray(), ItemType.Knife);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: knifeSlot)); // un-hold

        var defender = world.CreateSnapshot().EnemyCrew.First(c => c.RoomId == world.EnemyShipLayout.BoardingRoomId);
        for (var i = 0; i < 5 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(defender.X - me.X, defender.Y - me.Y);
            if (toTarget.Length() <= 0.6f)
                break;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

        var healthBefore = world.CreateSnapshot().EnemyCrew.First(c => c.Id == defender.Id).Health;
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, FirePressed: true));
        world.Step(RealtimeStep);

        return world.CreateSnapshot().EnemyCrew.First(c => c.Id == defender.Id).Health == healthBefore;
    }

    // Clearing every defender captures the ship outright - an alternative win condition to
    // shelling it down from the turrets (game_design.md Phase 3).
    private static bool World_Boarding_KillingAllCrew_DestroysEnemyShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.LaserRifle, 3.5f, 5f); // longest range - can clear a room without closing to melee

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        // Work through the ship room by room, walking toward the nearest living defender and
        // firing whenever one is in range.
        for (var i = 0; i < 120 * 30 && world.CreateSnapshot().EnemyCrew.Any(c => c.Alive); i++)
        {
            var snapshot = world.CreateSnapshot();
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            if (me.Health <= 0)
                return false; // died boarding - not what this test is checking

            var target = snapshot.EnemyCrew.Where(c => c.Alive)
                .OrderBy(c => (new Vec2(c.X, c.Y) - new Vec2(me.X, me.Y)).Length())
                .First();
            var toTarget = new Vec2(target.X - me.X, target.Y - me.Y);
            var dir = toTarget.Length() > 0.001f ? toTarget.Normalized() : Vec2.Zero;

            // A boarded hull is buttoned up (World.cs registers its doors closed), so advancing
            // means opening the one in front of you - the same click a player makes, done here as
            // soon as the boarder is within arm's reach of it.
            foreach (var door in world.EnemyShipLayout.Doors)
                if (!world.IsDoorOpen(door.Id) && (door.Position - new Vec2(me.X, me.Y)).Length() < 1.5f)
                    world.ToggleDoor(door.Id);

            // Doors sit at the rooms' shared mid-height, so approach along that row first.
            var moveY = Math.Abs(me.Y - 3f) > 0.2f ? Math.Sign(3f - me.Y) : dir.Y;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: moveY, FirePressed: true));
            world.Step(RealtimeStep);
        }

        return world.CreateSnapshot().EnemyCrew.All(c => !c.Alive) && world.CreateSnapshot().Enemy.Hp <= 0;
    }

    // Every hull class is a distinct structure, and nothing about it may collide with another's:
    // door state and the room a character stands in are flat dictionaries shared by every structure
    // in the game, so two classes reusing an id would be the same door and the same room.
    private static bool EnemyShipClasses_AreDistinctStructures()
    {
        var layouts = EnemyShipLayout.All;
        if (layouts.Count < 3 || layouts.Select(l => l.Kind).Distinct().Count() != layouts.Count)
            return false;

        var roomIds = layouts.SelectMany(l => l.Rooms.Select(r => r.Id)).ToList();
        var doorIds = layouts.SelectMany(l => l.Doors.Select(d => d.Id).Append(l.BoardingHatch.Id)).ToList();
        var crewIds = layouts.SelectMany(l => l.CrewSpawns.Select(c => c.Id)).ToList();

        // Every class also has to be walkable end to end: a breach compartment that is actually one
        // of its rooms, and every defender standing in a room that exists.
        foreach (var layout in layouts)
        {
            if (layout.Rooms.All(r => r.Id != layout.BoardingRoomId))
                return false;
            if (layout.CrewSpawns.Any(c => layout.Rooms.All(r => r.Id != c.RoomId)))
                return false;
        }

        return roomIds.Distinct().Count() == roomIds.Count
               && doorIds.Distinct().Count() == doorIds.Count
               && crewIds.Distinct().Count() == crewIds.Count;
    }

    // Which hull defends a sector is fixed by the sector, not rolled fresh: run from a fight, come
    // back, and it has to be the same opposition waiting - otherwise retreating would be a way to
    // reroll a gunship into a freighter.
    private static bool World_Boarding_SectorAlwaysFieldsTheSameHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-beta");
        var first = world.CreateSnapshot().EnemyShipClassName;

        var again = new World();
        again.SpawnCharacter(1);
        EngageSector(again, "sector-beta");

        var elsewhere = new World();
        elsewhere.SpawnCharacter(1);
        EngageSector(elsewhere, "sector-alpha");

        // Same sector, same hull. (The two sectors are allowed to match - what matters is that the
        // answer is a property of the sector, which the repeat run is what proves.)
        return first == again.CreateSnapshot().EnemyShipClassName
               && EnemyShipLayout.All.Any(l => l.Name == elsewhere.CreateSnapshot().EnemyShipClassName);
    }

    // Air as a weapon (World.EnemyAtmosphere.cs): a boarded hull is buttoned up, so its compartments
    // hold their air until someone opens a door onto the breach - and then whoever is inside without
    // a suit is on a clock, while a crew that fights in suits doesn't care.
    private static bool World_Boarding_OpeningDoors_VentsTheHullAndSuffocatesUnsuitedCrew()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-alpha");

        var layout = world.EnemyShipLayout;
        var deepRoom = layout.Rooms.Last(r => r.Id != layout.BoardingRoomId);
        float Oxygen(string roomId) =>
            world.CreateSnapshot().EnemyRoomOxygen.First(o => o.RoomId == roomId).Oxygen;

        // Sealed: the breach vents its own compartment and nothing else, however long it stands.
        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);
        if (Oxygen(layout.BoardingRoomId) > 1f || Oxygen(deepRoom.Id) < 99f)
            return false;

        foreach (var door in layout.Doors)
            world.ToggleDoor(door.Id);
        for (var i = 0; i < 40 * 30; i++)
            world.Step(RealtimeStep);

        bool Alive(string crewId) => world.CreateSnapshot().EnemyCrew.First(c => c.Id == crewId).Alive;
        var unsuitedGone = layout.CrewSpawns.Where(s => !s.Suited).All(s => !Alive(s.Id));
        var suitedHolding = layout.CrewSpawns.Where(s => s.Suited).All(s => Alive(s.Id));

        return Oxygen(deepRoom.Id) < OxygenSafeThresholdForTests && unsuitedGone && suitedHolding;
    }

    private const float OxygenSafeThresholdForTests = 50f; // mirrors World.Atmosphere.cs's own threshold

    // Losing the hull you are standing in throws you out of it. The next ship of the squadron is a
    // different floor plan, so staying "aboard" would leave the character in a compartment that no
    // longer exists anywhere.
    private static bool World_Boarding_HullDestroyedUnderneath_EjectsTheBoardingParty()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife, 14f, 5f);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        world.Enemy.ApplyDamage(world.Enemy.Hp); // a turret finishes the hull off while they're inside
        world.Step(RealtimeStep);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.OnEnemyShip && me.IsOutside;
    }

    private static bool World_Boarding_CrewFightsBack_DamagesBoarder()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife, 14f, 5f);

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        var defender = world.CreateSnapshot().EnemyCrew.First(c => c.RoomId == world.EnemyShipLayout.BoardingRoomId);
        for (var i = 0; i < 5 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(defender.X - me.X, defender.Y - me.Y);
            if (toTarget.Length() <= 0.6f)
                break;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

        var healthBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0));
        for (var i = 0; i < 5 * 30; i++) // outlast the defenders' attack interval, taking no action
            world.Step(RealtimeStep);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health < healthBefore;
    }

    // Fly to a hostile sector and shell its ship down - the standing-moving event
    // (World.Factions.cs's RecordShipDestroyed) fires on the transition back out of the fight.
    // Clears a whole sector, however many ships defend it. The retry loop matters now that sectors
    // can hold squadrons (game_design.md section 12): FireBowTurretUntilEnemyDefeated gives up
    // after a fixed number of reload/repair cycles, which isn't always enough for three hulls in
    // a row, and stopping early would leave the caller mid-battle instead of victorious.
    private static void WinBattleAt(World world, string sectorId)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: sectorId));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var round = 0; round < 8 && world.Phase == VoyagePhase.Battle; round++)
        {
            FireBowTurretUntilEnemyDefeated(world, 1);
            for (var i = 0; i < 30 && world.Phase == VoyagePhase.Battle && world.Enemy.Hp <= 0; i++)
                world.Step(RealtimeStep); // let StepVoyage resolve the kill and settle the standing
        }
    }

    // Keeps fighting a faction's ships until its standing actually drops past the hostile
    // threshold, putting in at the neutral home station between rounds.
    //
    // Both details are load-bearing. The retry is bounded rather than a fixed count because
    // FireBowTurretUntilEnemyDefeated gives up after a set number of reload/repair cycles, so one
    // battle can end with the enemy still alive - "exactly 3 kills" was flaky for that reason.
    // And the repair stop matters because damage accumulates across rounds (cut wire links, spent
    // fuel, breaches) until the ship simply can't fly itself to a dock any more, which made the
    // *final* docking flaky instead. Home is Independent, so calling there shifts nobody's
    // standing and doesn't disturb what's being measured.
    private static void GrindStandingHostile(World world, string sectorId, FactionId faction)
    {
        // Budget rather than a fixed count: a sector's squadron doesn't always cost the same
        // standing (how many hulls the fight actually gets through varies with how the fight goes),
        // so "fly until they hate us" has to be allowed to take a few more trips than the minimum.
        for (var attempt = 0; attempt < 14 && world.GetStanding(faction) > FactionDefinitions.HostileThreshold; attempt++)
        {
            WinBattleAt(world, sectorId);
            // Always put in for repairs, including after the final battle - the caller still has
            // to fly somewhere afterwards, and doing that on a freshly shot-up ship is exactly
            // what made the last docking flaky.
            world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
            DockAtStation(world);
        }
    }

    private static bool World_Faction_DestroyingShip_LowersOwnerRaisesRival()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WinBattleAt(world, "sector-alpha"); // FreeFleet space

        return world.GetStanding(FactionId.FreeFleet) == FactionDefinitions.StandingPerShipDestroyed
            && world.GetStanding(FactionId.Consortium) == FactionDefinitions.RivalStandingPerShipDestroyed;
    }

    private static bool World_Faction_QuestTurnIn_RaisesStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Starts docked at the neutral home station - take its delivery job and run it to
        // whichever station it names (every other station on the map is Consortium-held).
        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is null)
            return false;

        var before = world.GetStanding(FactionId.Consortium);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: quest.DestinationPointId));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));

        return world.CreateSnapshot().ActiveQuest is null
            && world.GetStanding(FactionId.Consortium) == before + FactionDefinitions.StandingPerQuestTurnIn;
    }

    // Three kills in Consortium space put standing past the hostile threshold (-18 each vs. a -40
    // threshold), at which point their stations stop offering work at all.
    private static bool World_Faction_HostileStanding_BlocksQuestOffers()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // Three fights pile up enough hull breaches to suffocate an unsuited character long before
        // the third one ends - and a dead character can't walk to the helm to dock afterwards.
        // Suiting up first makes the whole run deterministic (World.Atmosphere.cs skips suited
        // characters entirely).
        EquipSuit(world, 1);
        GrindStandingHostile(world, "sector-delta", FactionId.Consortium);

        if (world.GetStanding(FactionId.Consortium) > FactionDefinitions.HostileThreshold)
            return false; // didn't actually anger them enough - setup problem, not the behavior under test

        // trade-station, not outpost-gamma: it's Consortium-held *and* actually has an
        // Administrator to refuse you (outpost-gamma is a Shipyard - no quests there at all, so
        // it would pass for the wrong reason).
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        DockAtStation(world);
        if (world.Phase != VoyagePhase.Station)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        return world.CreateSnapshot().ActiveQuest is null;
    }

    private static bool World_Faction_HostileStanding_RaisesPrices()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // see World_Faction_HostileStanding_BlocksQuestOffers
        GrindStandingHostile(world, "sector-delta", FactionId.Consortium);
        if (world.GetStanding(FactionId.Consortium) > FactionDefinitions.HostileThreshold)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        // Buy with a freshly spawned character: three battles leave player 1 with a full
        // inventory row (tools/crates picked up along the way), and a failed TryAdd is a silent
        // no-charge - which would look exactly like "the markup wasn't applied".
        world.SpawnCharacter(2);
        var good = TradeCatalog.Goods[0];
        var creditsBefore = world.Credits;
        world.ApplyCommand(2, new ClientCommand(2, BuyItemType: good.Item));

        // Paid more than the list price because they dislike the crew (World.Factions.cs's
        // PriceMultiplier) - the exact figure is FactionDefinitions' business, not this test's.
        var paid = creditsBefore - world.Credits;
        return paid > good.BuyPrice;
    }

    private static bool World_Faction_IndependentsNeverShift()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Independents own no hostile sectors, and finishing the home station's own job is
        // credited to whoever holds the destination - so nothing here can move their number.
        WinBattleAt(world, "sector-alpha");
        return world.GetStanding(FactionId.Independent) == 0;
    }

    // Trading a Frigate down to a Scout costs less than the trade-in is worth, so the yard pays
    // out - which is exactly the negative-cost case World.ShipPurchase.cs allows on purpose.
    private static bool World_Shipyard_BuyCheaperHull_SwapsShipAndRefunds()
    {
        var world = new World(); // starts docked at home-station as a Frigate
        world.SpawnCharacter(1);
        // Only a Shipyard-kind station sells hulls (game_design.md section 10) - the home outpost
        // has no Shipwright at all, so the trip is part of the mechanic, not test overhead.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        var creditsBefore = world.Credits;
        var expectedCost = world.GetShipSwapCost(ShipKind.Scout);
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.CurrentShipKind == ShipKind.Scout
            && world.Credits == creditsBefore - expectedCost
            && expectedCost < 0 // trading down really does pay out
            && world.Ship.Rooms.Count == Ship.Create(ShipKind.Scout).Rooms.Count
            && me.X == world.Ship.SpawnPoint.X; // moved onto the new hull's spawn point
    }

    private static bool World_Shipyard_Buy_FailsWithoutEnoughCredits()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma")); // the Shipyard station
        DockAtStation(world);

        // A Cruiser costs far more than the starting wallet even after trading in the Frigate.
        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Cruiser));

        return world.CurrentShipKind == ShipKind.Frigate && world.Credits == creditsBefore;
    }

    private static bool World_Shipyard_Buy_FailsWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));
        return world.CurrentShipKind == ShipKind.Frigate;
    }

    // A new hull comes out of the yard intact, and the crew wallet/inventory carry across - only
    // the ship itself is replaced (World.ShipPurchase.cs).
    private static bool World_Shipyard_SwapKeepsCreditsAndClearsBreaches()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Take some damage first so there's something to be repaired by the swap, then head to
        // the one station that actually has a Shipwright.
        EquipSuit(world, 1);
        WinBattleAt(world, "sector-alpha");
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        var creditsBefore = world.Credits;
        var expectedCost = world.GetShipSwapCost(ShipKind.Scout);
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));

        var snapshot = world.CreateSnapshot();
        return world.CurrentShipKind == ShipKind.Scout
            && world.Credits == creditsBefore - expectedCost
            && snapshot.WallBlockStates.All(s => !s.Breached)
            && snapshot.RoomOxygen.All(o => o.Oxygen >= 100f);
    }

    // A bounty is satisfied by the kill but paid in person, back at the station that issued it
    // (World.Quests.cs) - so it's a round trip, not a fire-and-forget.
    private static bool World_Quest_Bounty_CompletesOnKillAndPaysAtIssuer()
    {
        var world = new World(); // starts docked at the neutral home outpost, which has an Administrator
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // survives the breaches the fight will open up

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Bounty));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is not { Kind: QuestKind.Bounty } || quest.ObjectiveComplete)
            return false;

        WinBattleAt(world, quest.DestinationPointId);
        if (world.CreateSnapshot().ActiveQuest is not { ObjectiveComplete: true })
            return false; // the kill should have marked it done even though we're nowhere near a station

        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: quest.IssuedByPointId));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));

        return world.CreateSnapshot().ActiveQuest is null && world.Credits == creditsBefore + quest.RewardCredits;
    }

    private static bool World_Quest_Bounty_TurnIn_FailsBeforeKill()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Bounty));
        if (world.CreateSnapshot().ActiveQuest is not { Kind: QuestKind.Bounty })
            return false;

        // Still standing at the issuing station, target untouched - handing in must do nothing.
        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));

        return world.CreateSnapshot().ActiveQuest is not null && world.Credits == creditsBefore;
    }

    // Mining jobs take real ore out of the crew's inventory, not an abstract "done" flag.
    private static bool World_Quest_Mining_ConsumesOreAndPays()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Mine first, take the job afterwards: the ore has to be genuinely mined (the Trader
        // prices Mineral out of reach on purpose), and taking the contract before or after the
        // digging makes no difference to what's being tested here - only one docking trip either
        // way, instead of two.
        MineOre(world, 2);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
        DockAtStation(world);
        if (world.Phase != VoyagePhase.Station)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Mining));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is not { Kind: QuestKind.Mining })
            return false;

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (me.Inventory!.MainSlots.Count(s => s == ItemType.Mineral) < quest.RequiredAmount)
            return false; // didn't actually come home with the ore - setup problem, not the behavior under test

        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));

        var after = world.CreateSnapshot();
        return after.ActiveQuest is null
            && world.Credits == creditsBefore + quest.RewardCredits
            && after.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Count(s => s == ItemType.Mineral) == 0;
    }

    private static bool World_Quest_Mining_TurnIn_FailsWithoutEnoughOre()
    {
        var world = new World();
        world.SpawnCharacter(1);

        MineOre(world, 1); // one short of what a mining contract asks for
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
        DockAtStation(world);
        if (world.Phase != VoyagePhase.Station)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Mining));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is not { Kind: QuestKind.Mining })
            return false;

        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));

        var after = world.CreateSnapshot();
        return after.ActiveQuest is not null
            && world.Credits == creditsBefore
            && after.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Count(s => s == ItemType.Mineral) == 1; // ore untouched
    }

    // Not every station kind staffs an Administrator (game_design.md section 10) - a Shipyard
    // sells hulls and nothing else, so there's no work to take there at all.
    private static bool World_Quest_Accept_FailsAtStationWithoutAdministrator()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma")); // Shipyard kind
        DockAtStation(world);
        if (world.Phase != VoyagePhase.Station)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        return world.CreateSnapshot().ActiveQuest is null;
    }

    // A save carries campaign progress across a restart: hull, wallet, reputation, upgrades,
    // inventory, the active job and where the crew is docked (SaveGame's own doc comment covers
    // what's deliberately left out).
    private static bool World_Save_RoundTripsCampaignProgress()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Build up some state worth keeping.
        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: TradeCatalog.Goods[0].Item));
        world.ApplyCommand(1, new ClientCommand(1, PurchaseUpgradeTrack: ShipUpgradeCatalog.Tracks[0].Track));
        EquipSuit(world, 1);
        WinBattleAt(world, "sector-alpha"); // shifts faction standing
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout)); // trading down pays out

        var save = world.CreateSave();

        // Restore onto a brand-new world, exactly as a restart would.
        var restored = new World();
        restored.SpawnCharacter(1);
        restored.ApplySave(save);

        var restoredInventory = restored.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return restored.CurrentShipKind == world.CurrentShipKind
            && restored.Credits == world.Credits
            && restored.GetStanding(FactionId.FreeFleet) == world.GetStanding(FactionId.FreeFleet)
            && restored.GetStanding(FactionId.Consortium) == world.GetStanding(FactionId.Consortium)
            && restored.UpgradeLevels[ShipUpgradeCatalog.Tracks[0].Track] == world.UpgradeLevels[ShipUpgradeCatalog.Tracks[0].Track]
            && restored.CreateSnapshot().ActiveQuest == world.CreateSnapshot().ActiveQuest
            && restored.CreateSnapshot().Voyage.DockedPointId == "outpost-gamma"
            && restored.Phase == VoyagePhase.Station
            && restoredInventory.MainSlots.Count(s => s is not null) == save.Inventory.Count;
    }

    private static bool World_Save_AutosavePendingSetOnDocking()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Construction docks at home, which counts - clear it and prove a *later* dock re-raises it.
        world.ClearAutosavePending();
        if (world.AutosavePending)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        return world.Phase == VoyagePhase.Station && world.AutosavePending;
    }

    private static string TempSavePath() =>
        Path.Combine(Path.GetTempPath(), $"spaceadventure-test-{Guid.NewGuid():N}.json");

    private static bool SaveStore_RoundTripsThroughFile()
    {
        var path = TempSavePath();
        try
        {
            var world = new World();
            world.SpawnCharacter(1);
            world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Bounty));
            var original = world.CreateSave();

            SaveStore.Save(original, path);
            if (!SaveStore.Exists(path))
                return false;

            var loaded = SaveStore.Load(path);
            return loaded is not null
                && loaded.ShipKind == original.ShipKind
                && loaded.Credits == original.Credits
                && loaded.DockedPointId == original.DockedPointId
                && loaded.ActiveQuest == original.ActiveQuest
                && loaded.FactionStandings.Count == original.FactionStandings.Count;
        }
        finally
        {
            SaveStore.Delete(path);
        }
    }

    // A missing or unreadable save must read as "no save" rather than throwing - the game has to
    // fall back to a new run, not fail to start (SaveStore's own doc comment).
    private static bool SaveStore_MissingOrCorruptFile_LoadsAsNoSave()
    {
        var missingPath = TempSavePath();
        if (SaveStore.Load(missingPath) is not null)
            return false;

        var corruptPath = TempSavePath();
        try
        {
            File.WriteAllText(corruptPath, "{ this is not valid json");
            return SaveStore.Load(corruptPath) is null;
        }
        finally
        {
            SaveStore.Delete(corruptPath);
        }
    }

    private static bool GameServer_AutosavesOnDocking()
    {
        var path = TempSavePath();
        try
        {
            // Constructing the world docks it at home immediately, so the very first tick should
            // already flush a save to disk.
            var server = new GameServer(ShipKind.Frigate, loadFrom: null, savePath: path);
            var transport = new InProcessTransport();
            server.Connect(transport);
            server.Tick();

            var saved = SaveStore.Load(path);
            return saved is not null && saved.ShipKind == ShipKind.Frigate && saved.DockedPointId == "home-station";
        }
        finally
        {
            SaveStore.Delete(path);
        }
    }

    // Walks a character out of the freshly docked ship and onto the station (the ship starts
    // docked at home, so no flying needed), then to a given point in the station's own room space.
    private static void WalkOntoStation(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // crosses the connector into the station's dock room
    }

    // Station rooms sit in a straight row at the doors' shared height, so walking along y=3 and
    // only then stepping off it reaches any of them (the same two-leg rule MoveCharacterTo needs
    // aboard ship - see the known pitfalls in continue.md).
    private static void WalkOnStationTo(World world, float x, float y)
    {
        for (var i = 0; i < 40 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var dx = x - me.X;
            if (Math.Abs(dx) <= 0.1f)
                break;
            var dy = Math.Abs(me.Y - 3f) > 0.15f ? Math.Sign(3f - me.Y) : 0;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: Math.Sign(dx), MoveY: dy));
            world.Step(RealtimeStep);
        }

        for (var i = 0; i < 10 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            if (Math.Abs(y - me.Y) <= 0.1f)
                break;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: Math.Sign(y - me.Y)));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0));
    }

    private static bool World_Crime_StealCrate_AddsItemAndMarksLooted()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        var crate = world.Station.Crates.First();
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.IsCrateLooted(crate.Id)
            && world.GetStolenItemCount(1) == 1
            && me.Inventory!.MainSlots.Contains(crate.Item);
    }

    // Caught red-handed: fine, confiscation and a reputation hit, all three (game_design.md §10).
    private static bool World_Crime_CaughtByGuard_FinesConfiscatesAndLowersStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);

        var crate = world.Station.Crates.First();
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        if (world.GetStolenItemCount(1) != 1)
            return false;

        var creditsBefore = world.Credits;
        var standingBefore = world.GetStanding(world.CreateSnapshot().GalaxyPoints
            .First(p => p.Id == world.CreateSnapshot().Voyage.DockedPointId).Faction);

        // Walk right up to the guard and wait out the patrol check.
        var guard = world.Station.Npcs.First(n => n.Kind == NpcKind.Security);
        WalkOnStationTo(world, guard.X, guard.Y);
        for (var i = 0; i < 5 * 30 && world.GetStolenItemCount(1) > 0; i++)
            world.Step(RealtimeStep);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var dockedFaction = world.CreateSnapshot().GalaxyPoints
            .First(p => p.Id == world.CreateSnapshot().Voyage.DockedPointId).Faction;

        return world.GetStolenItemCount(1) == 0 // confiscated
            && world.Credits < creditsBefore // fined
            && !me.Inventory!.MainSlots.Contains(crate.Item) // goods gone
            && world.GetStanding(dockedFaction) < standingBefore; // and they remember it
    }

    private static bool World_Crime_UnseenTheft_GoesUnpunished()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);

        var crate = world.Station.Crates.First(); // first service room - several rooms from the guard
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var creditsBefore = world.Credits;
        for (var i = 0; i < 10 * 30; i++) // loiter well past several patrol checks, out of sight
            world.Step(RealtimeStep);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.GetStolenItemCount(1) == 1 && world.Credits == creditsBefore && me.Inventory!.MainSlots.Contains(crate.Item);
    }

    // Shared setup for the two "resist arrest" tests: get onto the station armed, walk up to the
    // guard, and open fire.
    private static StationNpc ArmAndConfrontGuard(World world)
    {
        MoveCharacterTo(world, 1, 17f, 3f);
        MoveCharacterTo(world, 1, 17f, 5f); // quarters rifle rack
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        var slot = Array.IndexOf(world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.Rifle);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));

        WalkOntoStation(world);
        var guard = world.Station.Npcs.First(n => n.Kind == NpcKind.Security);
        WalkOnStationTo(world, guard.X, guard.Y);
        return guard;
    }

    private static bool World_Crime_ShootingGuard_AlertsStationAndGuardFightsBack()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var guard = ArmAndConfrontGuard(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        if (world.IsStationAlerted)
            return false; // shouldn't be alerted before a shot is fired

        // Aimed and given time to arrive: a shot is a body crossing the room now
        // (World.PersonalShots.cs), not damage applied the instant the button goes down.
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var aim = new Vec2(guard.X - me.X, guard.Y - me.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true, LookX: aim.X, LookY: aim.Y));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var guardAfterShot = world.CreateSnapshot().StationGuards.First(g => g.NpcId == guard.Id);
        if (!world.IsStationAlerted || guardAfterShot.Health >= guardAfterShot.MaxHealth)
            return false;

        var healthBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0));
        for (var i = 0; i < 5 * 30; i++) // stand there and take it
            world.Step(RealtimeStep);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health < healthBefore;
    }

    private static bool World_Crime_KillingGuard_CostsHeavyStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var guard = ArmAndConfrontGuard(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        var dockedFaction = world.CreateSnapshot().GalaxyPoints
            .First(p => p.Id == world.CreateSnapshot().Voyage.DockedPointId).Faction;
        var standingBefore = world.GetStanding(dockedFaction);

        for (var i = 0; i < 60 * 30 && world.CreateSnapshot().StationGuards.First(g => g.NpcId == guard.Id).Alive; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health <= 0)
                return false; // lost the shootout - not what this test measures
        }

        return !world.CreateSnapshot().StationGuards.First(g => g.NpcId == guard.Id).Alive
            && world.GetStanding(dockedFaction) < standingBefore;
    }

    private static bool World_Crime_RedockingRestocksCrates()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);

        var crate = world.Station.Crates.First();
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        if (!world.IsCrateLooted(crate.Id))
            return false;

        // Back aboard, fly somewhere and return - the station shouldn't stay stripped forever.
        WalkOnStationTo(world, 0.5f, 3f);
        for (var i = 0; i < 5 * 30 && world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0));
        // Shut the outer door behind us - it was opened to reach the station, and leaving it open
        // vents the ship to vacuum (World.Atmosphere.cs), which kills the unsuited character on
        // the long walk forward to the helm that DockAtStation needs.
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        DockAtStation(world);

        return world.Phase == VoyagePhase.Station && !world.IsCrateLooted(crate.Id) && world.GetStolenItemCount(1) == 0;
    }

    // Flies to a sector and puts the ship there into a battle. Damage is then applied directly
    // (Enemy.ApplyDamage) rather than through the turrets: FireBowTurretUntilEnemyDefeated loops
    // until the enemy's HP hits zero, which against a squadron seamlessly rolls on into the *next*
    // ship - useful for "just win the fight", useless for observing what happens between kills.
    private static void EngageSector(World world, string sectorId)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: sectorId));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
    }

    private static void DestroyCurrentEnemyShip(World world)
    {
        world.Enemy.ApplyDamage(world.Enemy.Hp);
        world.Step(RealtimeStep); // StepVoyage resolves the kill on the next tick
    }

    // A defended sector sends its ships in one after another (game_design.md section 12) - killing
    // one doesn't end the fight until the last is gone.
    private static bool World_Squadron_NextShipEngagesAfterEachKill()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-beta"); // squadron of 2
        if (world.CreateSnapshot().Enemy.RemainingShips != 2)
            return false;

        DestroyCurrentEnemyShip(world);

        // The fight continues, against a fresh full-health hull with its own intact crew to board.
        var afterFirst = world.CreateSnapshot();
        if (world.Phase != VoyagePhase.Battle || afterFirst.Enemy.RemainingShips != 1)
            return false;
        if (afterFirst.Enemy.Hp < afterFirst.Enemy.MaxHp || afterFirst.EnemyCrew.Any(c => !c.Alive))
            return false;

        DestroyCurrentEnemyShip(world);

        return world.Phase == VoyagePhase.Traveling && world.CreateSnapshot().Enemy.RemainingShips == 0;
    }

    private static bool World_Squadron_EveryKillCostsOwnerStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-beta"); // FreeFleet, squadron of 2

        DestroyCurrentEnemyShip(world);
        if (world.GetStanding(FactionId.FreeFleet) != FactionDefinitions.StandingPerShipDestroyed)
            return false; // the first kill alone should already have registered

        DestroyCurrentEnemyShip(world);

        // Both kills counted, not just the one that ended the engagement.
        return world.GetStanding(FactionId.FreeFleet) == FactionDefinitions.StandingPerShipDestroyed * 2;
    }

    private static bool World_Squadron_BountyCompletesOnlyWhenSectorCleared()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Bounty));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is not { Kind: QuestKind.Bounty })
            return false;

        EngageSector(world, quest.DestinationPointId);
        var squadronSize = world.CreateSnapshot().Enemy.RemainingShips;

        for (var ship = 0; ship < squadronSize; ship++)
        {
            DestroyCurrentEnemyShip(world);

            var complete = world.CreateSnapshot().ActiveQuest?.ObjectiveComplete ?? false;
            var lastShip = ship == squadronSize - 1;
            if (complete != lastShip)
                return false; // a squadron isn't beaten until its last ship is
        }

        return world.Phase == VoyagePhase.Traveling;
    }

    // Flies the ship to the station's berth and leaves it parked there, without docking - the
    // shared setup for the button tests below.
    private static void ApproachBerth(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 60 * 30 && !world.CanDockNow; i++)
        {
            var shipField = world.CreateSnapshot().ShipField;
            var toPort = world.DockBerthPosition - new Vec2(shipField.X, shipField.Y); // the berth, not the airlock rectangle
            var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();

            if (speed > 1.5f)
                world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
            else
                world.ApplyCommand(1, SteerToward(world, 1, world.DockBerthPosition));
            world.Step(RealtimeStep);
        }
    }

    // Drifting into the berth must not dock the ship by itself - that's the whole point of the
    // button (World.StationDocking.cs).
    private static bool World_Docking_ProximityAloneDoesNotDock()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);

        if (!world.CanDockNow)
            return false; // never reached the berth - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 0f));
        for (var i = 0; i < 10 * 30; i++) // sit at the berth doing nothing at all
            world.Step(RealtimeStep);
        if (world.Phase != VoyagePhase.StationApproach)
            return false;

        // ...and the button, once pressed, does dock it.
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return world.Phase == VoyagePhase.Station && world.CreateSnapshot().Voyage.DockedPointId == "trade-station";
    }

    private static bool World_Docking_ButtonFarFromPort_DoesNothing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        // Arrival parks the ship a long way off the berth (StationApproachStartDistance).
        if (world.CanDockNow)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return world.Phase == VoyagePhase.StationApproach;
    }

    // Docking squares the ship up and pulls it the last few metres onto the berth, so its own outer
    // airlock ends up exactly on the station's connector rather than merely near it.
    private static bool World_Docking_MatesAirlockOntoStationConnector()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);
        if (!world.CanDockNow)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        if (world.Phase != VoyagePhase.Station)
            return false;

        var shipField = world.CreateSnapshot().ShipField;
        if (Math.Abs(shipField.RotationDegrees) > 0.001f)
            return false;

        // Both frames now differ by exactly Station.WorldOffset, so the two door rectangles land on
        // the same spot - which is what makes the crossing an ordinary doorway.
        var outerDoor = world.Ship.AirlockOuterDoors.First();
        return (outerDoor.Position - world.Station.ShipConnector.Position).Length() < 0.001f;
    }

    // No teleport at the boundary: the character keeps walking in the same coordinate system, one
    // ordinary step at a time, and simply ends up in a station room.
    private static bool World_Station_CrossingConnector_MovesContinuously()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 25f, 3f);

        static Vec2 PositionOf(World w) =>
            w.CreateSnapshot().Characters.Single(c => c.PlayerId == 1) is var c ? new Vec2(c.X, c.Y) : Vec2.Zero;

        var previous = PositionOf(world);
        var crossed = false;
        for (var i = 0; i < 90; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 1f, MoveY: 0f));
            world.Step(RealtimeStep);

            var now = PositionOf(world);
            if ((now - previous).Length() > 0.5f)
                return false; // a jump - exactly what this change removed
            previous = now;
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            {
                crossed = true;
                break;
            }
        }

        // Past the shared door rectangle and inside the station's own dock chamber, in the very
        // same coordinates the ship's interior uses.
        return crossed && world.Station.GetRoom(world.Station.DockRoomId).Contains(previous);
    }

    // Casting off with someone still ashore can't leave them standing in geometry that's no longer
    // attached to the ship.
    private static bool World_Station_Departing_PullsCrewBackAboard()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.OnStation && world.Ship.Rooms.Any(r => r.Contains(new Vec2(me.X, me.Y)));
    }

    // Going ashore means opening the outer airlock, and a docked ship's outer airlock leads into
    // the station's own pressurized chamber - doing the normal thing must not suffocate the crew.
    private static bool World_Station_OpenAirlockWhileDocked_DoesNotVentTheShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        return snapshot.RoomOxygen.First(o => o.RoomId == "airlock-chamber").Oxygen > 99f &&
               snapshot.RoomOxygen.First(o => o.RoomId == "engine").Oxygen > 99f;
    }

    private static bool World_Docking_TooFastAtPort_ButtonStaysDisarmed()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3f, 4f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f));
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        // Barrel straight at the berth at full throttle, mashing the button the whole way: while
        // moving faster than DockMaxSpeed it must never take.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        var sawPortAtSpeed = false;
        for (var i = 0; i < 30 * 30 && world.Phase == VoyagePhase.StationApproach; i++)
        {
            world.Step(RealtimeStep);
            var shipField = world.CreateSnapshot().ShipField;
            // Measured against the berth (where the hull has to sit), not the airlock rectangle -
            // the hull centre is a good half-ship short of the latter when the two are mated.
            var toBerth = world.DockBerthPosition - new Vec2(shipField.X, shipField.Y);
            var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();
            if (toBerth.Length() < 4f && speed >= 2f)
            {
                sawPortAtSpeed = true;
                if (world.CanDockNow)
                    return false; // armed while still barrelling in
                world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
                if (world.Phase == VoyagePhase.Station)
                    return false; // docked despite the speed
            }
        }

        return sawPortAtSpeed;
    }

    // The station's bulk is solid - the ship stops against it instead of flying through, and the
    // berth deliberately sits outside that radius so lining up never means shouldering the hull.
    private static bool World_Docking_StationHullBlocksTheShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);
        if (!world.CanDockNow)
            return false;

        // Keep pushing past the berth, straight at the station's centre.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 20 * 30 && world.Phase == VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        var final = world.CreateSnapshot().ShipField;
        var distanceToCentre = (world.Station.Position - new Vec2(final.X, final.Y)).Length();
        return world.Phase == VoyagePhase.StationApproach && distanceToCentre >= 4.5f; // never got inside the hull
    }
}
