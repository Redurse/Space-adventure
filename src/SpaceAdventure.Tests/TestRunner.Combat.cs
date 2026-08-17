using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
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
        world.CutWire("trunk-shields"); // takes out both shield devices (reactor room)

        WalkAcrossShipTo(world, 7.2f, 0.7f); // reactor room's shields device

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // no tool held — should fail
        var stillDamagedWithoutTool = !world.IsDeviceConnected("system-shields");

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot)); // hold it

        WalkAcrossShipTo(world, 7.2f, 0.7f); // back to the shields device
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
}
