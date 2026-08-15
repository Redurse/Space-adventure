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
        ("World_Movement_LockedWhileManningTurret", World_Movement_LockedWhileManningTurret),
        ("World_Fire_EmptiesMagazineThenRefusesWithoutDamage", World_Fire_EmptiesMagazineThenRefusesWithoutDamage),
        ("World_PickUpAmmoCrate_RequiresProximityToStorage", World_PickUpAmmoCrate_RequiresProximityToStorage),
        ("World_PickUpAmmoCrate_SucceedsNearStorage", World_PickUpAmmoCrate_SucceedsNearStorage),
        ("World_ReloadTurret_RefillsAmmoAndClearsCarrying", World_ReloadTurret_RefillsAmmoAndClearsCarrying),
        ("EnemyShip_IsRetreating_BelowThreshold", EnemyShip_IsRetreating_BelowThreshold),
        ("EnemyShip_IsRetreating_FalseAboveThreshold", EnemyShip_IsRetreating_FalseAboveThreshold),
        ("World_EnemyAi_EventuallyBreachesEveryRoom", World_EnemyAi_EventuallyBreachesEveryRoom),
        ("World_Decompression_DrainsHealthInBreachedRoom", World_Decompression_DrainsHealthInBreachedRoom),
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
        ("Reactor_RemovingAllRods_ZerosOutputEvenWithFuel", Reactor_RemovingAllRods_ZerosOutputEvenWithFuel),
        ("World_ReactorSlot_RequiresProximityToReactor", World_ReactorSlot_RequiresProximityToReactor),
        ("World_ReactorSlot_RemoveRodReturnsItToInventory", World_ReactorSlot_RemoveRodReturnsItToInventory),
        ("World_ReactorSlot_InsertRequiresHoldingRod", World_ReactorSlot_InsertRequiresHoldingRod),
        ("World_ReactorSlot_ReinsertHeldRod", World_ReactorSlot_ReinsertHeldRod),
        ("Shield_TryAbsorbHit_DepletesPointsUntilEmpty", Shield_TryAbsorbHit_DepletesPointsUntilEmpty),
        ("World_Shield_AbsorbsFirstAttackWithoutDamagingShip", World_Shield_AbsorbsFirstAttackWithoutDamagingShip),
        ("World_Voyage_ShipMovesContinuouslyTowardTarget", World_Voyage_ShipMovesContinuouslyTowardTarget),
        ("World_Voyage_CannotChangeDestinationMidBattle", World_Voyage_CannotChangeDestinationMidBattle),
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
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(2.5f, 0.5f), "cockpit", new Vec2(0, -1f));
        return roomId == "cockpit" && Math.Abs(pos.Y - 0f) < 0.01f; // clamped at the top hull wall
    }

    private static bool Ship_MoveAlongAxis_PassesThroughAlignedDoor()
    {
        var ship = Ship.CreateStarter();
        // Near the cockpit/reactor wall (x=5) at the door's y=3 — should cross through.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.9f, 3f), "cockpit", new Vec2(0.3f, 0));
        return roomId == "reactor" && Math.Abs(pos.X - 5.2f) < 0.01f;
    }

    private static bool Ship_MoveAlongAxis_BlockedWhenMisalignedWithDoor()
    {
        var ship = Ship.CreateStarter();
        // Same wall, but y=0.5 is outside the door's 2.1..3.9 opening — should hit the wall.
        var (pos, roomId) = ship.MoveAlongAxis(new Vec2(4.9f, 0.5f), "cockpit", new Vec2(0.3f, 0));
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

    private static bool World_Fire_DamagesEnemyAndRespectsCooldown()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        world.Step(RealtimeStep);
        var hpAfterFirstShot = world.CreateSnapshot().Enemy.Hp;

        // Second attempt lands within the cooldown window — should not deal more damage.
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        world.Step(RealtimeStep);
        var hpAfterSecondAttempt = world.CreateSnapshot().Enemy.Hp;

        return hpAfterFirstShot < 100f && Math.Abs(hpAfterFirstShot - hpAfterSecondAttempt) < 0.01f;
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

        for (var shot = 0; shot < 6; shot++) // magazine capacity
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            for (var i = 0; i < 20; i++) // outlast the 0.5s cooldown before the next shot
                world.Step(RealtimeStep);
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

        world.PowerGrid.SetDamaged(PowerSystemId.WeaponCharger, true);
        var effectiveWhileDamaged = world.PowerGrid.GetAllocation(PowerSystemId.WeaponCharger);

        return allocatedBefore > 0f && effectiveWhileDamaged == 0f;
    }

    private static bool World_RepairSystem_RequiresWrenchHeldInHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.PowerGrid.SetDamaged(PowerSystemId.Shields, true); // cockpit device

        MoveCharacterTo(world, 1, 7f, 3f); // corridor -> reactor
        MoveCharacterTo(world, 1, 3f, 3f); // reactor -> cockpit
        MoveCharacterTo(world, 1, 3.5f, 1.5f); // cockpit shields device

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // no tool held — should fail
        var stillDamagedWithoutTool = world.PowerGrid.IsDamaged(PowerSystemId.Shields);

        MoveCharacterTo(world, 1, 7f, 3f); // cockpit -> reactor
        MoveCharacterTo(world, 1, 7f, 5f); // reactor wrench station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up wrench
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it

        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3.5f, 1.5f); // back to the shields device
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // repair

        return stillDamagedWithoutTool && !world.PowerGrid.IsDamaged(PowerSystemId.Shields);
    }

    private static bool Reactor_RemovingAllRods_ZerosOutputEvenWithFuel()
    {
        var reactor = new Reactor(maxOutput: 60f, maxFuel: 500f, fuelPerPowerUnitPerSecond: 0.05f);
        for (var i = 0; i < Reactor.RodSlotCount; i++)
            reactor.RodSlots[i] = false;

        return reactor.Fuel > 0f && reactor.CurrentOutput == 0f;
    }

    private static bool World_ReactorSlot_RequiresProximityToReactor()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor — far from the reactor block

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0));

        return world.PowerGrid.Reactor.RodSlots[0]; // unchanged — still loaded, click didn't reach
    }

    private static bool World_ReactorSlot_RemoveRodReturnsItToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 9.5f, 1f); // reactor block

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0));

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return !world.PowerGrid.Reactor.RodSlots[0] && inventory.MainSlots.Count(s => s == ItemType.FuelRod) == 1;
    }

    private static bool World_ReactorSlot_InsertRequiresHoldingRod()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 3f);
        MoveCharacterTo(world, 1, 9.5f, 1f);

        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // remove rod 0 -> inventory (not held)
        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorSlotIndex: 0)); // try to reinsert without holding it

        return !world.PowerGrid.Reactor.RodSlots[0]; // still empty
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
        return world.PowerGrid.Reactor.RodSlots[0] && inventory.MainSlots.All(s => s != ItemType.FuelRod);
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
    // individual outer-hull wall blocks (58, spread unevenly across 5 rooms) — 600 simulated
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

        for (var iteration = 0; iteration < 30 && world.CreateSnapshot().Enemy.Hp > 0; iteration++)
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
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.Station; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        // Refuel snaps to MaxFuel exactly on arrival, but firing/repair activity can continue
        // (and burn a little more) right after — assert "topped back up", not "still exactly 500".
        return snapshot.Voyage.Phase == VoyagePhase.Station
            && fuelDuringFlight < 500f
            && snapshot.Power.ReactorFuel > fuelDuringFlight + 10f
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

        MoveCharacterTo(world, 1, 20f, 3f); // suit locker, engine room
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // start equipping
        for (var i = 0; i < 90; i++) // outlast the 2s equip action
            world.Step(RealtimeStep);

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

        var before = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge; // starts full
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        world.Step(RealtimeStep);
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
}
