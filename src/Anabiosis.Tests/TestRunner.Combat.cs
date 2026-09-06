using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool World_Battle_SquadronSpawnsInFieldAndClosesOnTheShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterBattle(world, sectorId: "sector-beta"); // a picket of two

        var atArrival = world.CreateSnapshot();
        if (atArrival.EnemyShip.Ships.Count != 2 || atArrival.EnemyShip.Ships.Count(e => e.IsBoardable) != 1)
            return false;

        float Distance(WorldSnapshot s) =>
            (float)new Vec2(s.EnemyShip.Ships[0].X - s.ShipField.X, s.EnemyShip.Ships[0].Y - s.ShipField.Y).Length();

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
        return snapshot.EnemyShip.Ships.All(e =>
            new Vec2(e.X - snapshot.ShipField.X, e.Y - snapshot.ShipField.Y).Length() >= hullHalfLength);
    }

    private static bool World_Battle_EnemyFire_IsBlockedByAnAsteroid()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterBattle(world);

        var enemy = world.CreateSnapshot().EnemyShip.Ships.Single();
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
        // Flying there needs a hand on the helm (no more autopilot) - only man the bow turret once
        // the fight has actually started.
        EnterBattle(world);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man the bow turret

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
        // Flying there needs a hand on the helm first (no more autopilot) - the gun can only be
        // manned once the fight has actually started, so there's a short window right after
        // arrival where a raider could in principle land a hit on this turret before it's manned;
        // unlike the old autopilot version (manned well before the sector was ever reached), that
        // window is no longer zero, just short.
        EnterBattle(world);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        // The enemy no longer sits perfectly still (World.EnemyFleet.cs's ambient sway, "не стояли
        // на одном месте") - track its actual bearing rather than assuming the turret's default aim
        // still lines up, and retry the whole shot if it swayed off the aimed line before the shell
        // arrived (same "budget rather than a single fixed attempt" shape other AI-dependent tests
        // in this suite already use).
        for (var attempt = 0; attempt < 30; attempt++)
        {
            for (var aimTick = 0; aimTick < 10; aimTick++)
            {
                var error = TurretAimErrorToEnemy(world, "turret-bow");
                if (MathF.Abs(error) < 1f)
                    break;
                world.ApplyCommand(1, new ClientCommand(1, TurretAimDirection: MathF.Sign(error)));
                world.Step(RealtimeStep);
            }

            var hpBefore = world.CreateSnapshot().Enemy.Hp;
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            world.Step(RealtimeStep);
            // Second attempt lands within the cooldown window — no second shell leaves the barrel,
            // so only one lot of damage can ever arrive however long we then wait.
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            StepFor(world, 20); // outlast the magnetic cannon's short cooldown, let the shell arrive

            var hpAfter = world.CreateSnapshot().Enemy.Hp;
            if (hpAfter < hpBefore)
                return Math.Abs(hpAfter - (hpBefore - TurretBalance.MagneticDamage)) < 0.01f;
        }

        return false; // never landed a hit within the retry budget
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

    // The magnetic cannon's magazine is big enough now (TurretBalance.MagneticMagazineCapacity)
    // that emptying it outright kills the one enemy this scenario spawns partway through - once
    // that happens Enemy falls back to a fresh, undamaged placeholder (World.EnemyFleet.cs's own
    // Enemy property), so HP is no longer a meaningful signal for "did exactly N shots fire" here.
    // This only checks the magazine mechanic itself: it actually runs dry, and firing again past
    // empty changes nothing further.
    private static bool World_Fire_EmptiesMagazineThenRefusesWithoutDamage()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterBattle(world); // see World_Fire_DamagesEnemyAndRespectsCooldown on the ordering
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it

        var magazineCapacity = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-bow").MagazineCapacity;
        for (var shot = 0; shot < magazineCapacity; shot++)
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            StepFor(world, 5); // outlast the magnetic cannon's short cooldown
        }

        var ammoAfterMagazine = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-bow").AmmoRemaining;

        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true)); // magazine empty now
        world.Step(RealtimeStep);
        var ammoAfterOneMore = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-bow").AmmoRemaining;

        return ammoAfterMagazine == 0 && ammoAfterOneMore == 0;
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

    // Regression: AmmoStorage used to be an unlimited pickup - each crate now comes off a finite
    // per-storage stock (World.Ammo.cs), so running the crate/turret loop enough times has to
    // actually run the storage dry rather than supplying forever.
    private static bool World_AmmoStorage_StockIsFiniteAndDepletes()
    {
        var world = new World();
        world.SpawnCharacter(1);

        for (var i = 0; i < World.AmmoStorageCapacity; i++)
        {
            MoveCharacterTo(world, 1, 15f, 3f);
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up a crate
            MoveCharacterTo(world, 1, 1.5f, 3f);
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // reload, frees the crate
        }

        MoveCharacterTo(world, 1, 15f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // storage should now be empty

        var snapshot = world.CreateSnapshot();
        var stock = snapshot.AmmoStorageStates.First(s => s.StorageId == "ammo-storage-quarters");
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        return stock.Remaining == 0 && !me.CarryingAmmoCrate;
    }

    // A depleted storage isn't stuck empty forever - a station visit tops it back up, the same
    // resupply pass that already refuels the reactor and patches the hull (World.Voyage.cs's
    // EnterStation).
    private static bool World_AmmoStorage_RestocksAtStation()
    {
        var world = new World();
        world.SpawnCharacter(1);

        MoveCharacterTo(world, 1, 15f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // take one crate

        var before = world.CreateSnapshot().AmmoStorageStates.First(s => s.StorageId == "ammo-storage-quarters").Remaining;
        if (before != World.AmmoStorageCapacity - 1)
            return false;

        // Use up the carried crate before heading to the helm - HandleInteract's carrying-a-crate
        // branch takes priority over sitting down at any console, so a held crate would otherwise
        // swallow every subsequent [F] press instead of seating the character at the helm.
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        ApproachBerth(world); // undocks, flies out, and back to a berth
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        if (!world.IsDocked)
            return false;

        var after = world.CreateSnapshot().AmmoStorageStates.First(s => s.StorageId == "ammo-storage-quarters").Remaining;
        return after == World.AmmoStorageCapacity;
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
        return inventory.Equipped[EquipSlot.Suit] == ItemType.Spacesuit;
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

    private static bool World_TakeToolFromRack_AddsItToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = TakeFromRack(world, ItemType.Wrench);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return slot >= 0 && inventory.MainSlots[slot] == ItemType.Wrench;
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

        world.CutWire("trunk-weaponcharger");
        var effectiveWhileDamaged = world.GetEffectivePower(PowerSystemId.WeaponCharger);

        return allocatedBefore > 0f && effectiveWhileDamaged == 0f;
    }

    private static bool World_RepairSystem_RequiresWrenchHeldInHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.CutWire("trunk-system-shields"); // takes out just the first shield device (reactor room) - its sibling has its own dedicated trunk now

        WalkAcrossShipTo(world, 7.2f, 0.7f); // reactor room's shields device

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // no tool held — should fail
        var stillDamagedWithoutTool = !world.IsDeviceConnected("system-shields");

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot)); // hold it

        WalkAcrossShipTo(world, 7.2f, 0.7f); // back to the shields device
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // starts the repair

        // Repair is gradual now (World.SystemRepair.cs's minigame), a real 12-hour elapsed-time
        // timer rather than fixed by that one press - DebugFastForwardAllRepairs skips the WAIT
        // (1.3 million real Step calls to sit through 12 hours tick-by-tick isn't practical for a
        // unit test), not the requirement: the character still has to be genuinely in reach with
        // the tool held for the one more Step below to actually land the finish.
        world.DebugFastForwardAllRepairs(13.0 * 3600.0);
        world.Step(RealtimeStep);

        return stillDamagedWithoutTool && world.IsDeviceConnected("system-shields");
    }

    // The gradual-repair rework's own regression guard - a single F press used to fix a device
    // outright; now it only starts the minigame (World.SystemRepair.cs), so the device must still
    // read as damaged right after it.
    private static bool World_RepairSystem_SinglePressDoesNotInstantlyFixIt()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.CutWire("trunk-system-shields");
        WalkAcrossShipTo(world, 7.2f, 0.7f);

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        WalkAcrossShipTo(world, 7.2f, 0.7f);

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // one press only
        return !world.IsDeviceConnected("system-shields");
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

        return absorbedFirst && pointsAfterOne > 0f && pointsAfterOne < shield.MaxPoints;
    }

    private static bool World_Shield_AbsorbsFirstAttackWithoutDamagingShip()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Only 2s, not long enough to actually hit the reactor's own ceiling (60): PowerGrid's
        // sliders share one hard budget, and FlyToward is about to need real Engine allocation of
        // its own to fly anywhere at all - maxing Shields out first would leave it none (a fully
        // capped Shields slider blocks Engine from growing past 0, since othersTotal already
        // equals CurrentOutput), stalling the ship in place for the rest of this test. The
        // assertion below only needs some charge before the fight (pointsBeforeAttack > 0f), not
        // a full bar.
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 2, PowerDirection: 1f)); // Shields
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        var chargedBeforeDeparture = world.CreateSnapshot().Shield.Points;

        // Flying there now needs a hand on the helm (no more autopilot), which FlyToward handles
        // itself (undocking, ramping Engine, peeling clear of the berth, avoiding any other
        // hostile sector along the way) - the Shields hold above survives it untouched, since
        // ApplyCommand's PowerSystemIndex is a single per-player slot (World.cs) that FlyToward
        // only ever points at Engine.
        var target = world.GalaxyMap.GetPoint("sector-alpha").Position;

        // Only fly for real until the fight actually starts - FlyToward's own SteerToward keeps
        // pointing at the sector's marker every tick it runs, which is exactly wrong once the
        // squadron has already caught up (it overshoots the marker and just keeps flying, giving
        // the enemy AI a fast, unpredictable target that can take a long time to actually hit).
        FlyToward(world, target, () => world.IsInBattle, 1, maxTicks: 400 * 30, targetPointId: "sector-alpha");

        // Snap onto the sector's own marker before settling - wherever the real flight happened
        // to be standing when TryEngageHostileSector fired is arbitrary, and an asteroid field
        // surrounds this sector (World.EnemyFleet.cs's HasLineOfSight checks AsteroidField.Asteroids
        // for exactly this). Landing in that asteroid's shadow means the squadron never gets a
        // clear shot for as long as this waits, no matter how long the budget is - the marker
        // itself is the one position every EnterBattle-based test already relies on being clear.
        world.DebugPlaceShip(target);
        world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));

        // The real baseline for "did a hit land" is whatever the shield sits at right now, not
        // back when it was first charged: it keeps recharging off its own held allocation for the
        // whole flight out here (FlyToward can take minutes at ShipMaxSpeed over the M40-sized
        // field), so by the time the fight actually starts it's usually back at MaxPoints(100) -
        // comparing a later dip against the old, much lower reading captured before departure
        // would only ever trip once the shield had somehow dropped below where it started, which
        // a single 34-point hit against a full bar never does.
        var pointsBeforeAttack = world.CreateSnapshot().Shield.Points;

        // Step tick-by-tick and catch the exact moment the first attack lands (the 6s attack
        // cooldown after the fight starts), rather than sampling long after — shield regen is
        // fast enough to mask the dip by then.
        var absorbedAHit = false;
        for (var i = 0; i < 60 * 30 && world.IsInBattle && !absorbedAHit; i++)
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().Shield.Points < pointsBeforeAttack)
                absorbedAHit = true;
        }

        var snapshot = world.CreateSnapshot();
        return chargedBeforeDeparture > 0f
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
}
