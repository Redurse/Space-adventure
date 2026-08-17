using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Tanks live in the starter rack stock now too (World.Storage.cs's InitializeRackSlots) -
    // same "find it, walk there, drag it out" as TakeFromRack, just discarding the returned slot
    // since none of this test suite's tank setups care which one it landed in.
    private static void TakeTankFromRack(World world, ItemType tankType = ItemType.OxygenTank) => TakeFromRack(world, tankType);

    private static void AttachTankTo(World world, int targetSlot, ItemType tankType = ItemType.OxygenTank)
    {
        var slots = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray();
        var tankSlot = Array.IndexOf(slots, tankType);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: tankSlot, AttachTankToSlot: targetSlot));
    }

    // Cutter in hand, suit on, out through the airlock and standing on the plating - the part every
    // trip outside shares, whatever it goes on to do out there.
    private static void ExitShipIntoVacuum(World world)
    {
        var cutterSlot = TakeFromRack(world, ItemType.Cutter);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: cutterSlot)); // hold it

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

    // Click-to-pick-up (World.Mining.cs's TryPickupDroppedItem) works in EVA too, alongside the
    // existing F-key path exercised by the test above - additive, not a replacement.
    private static bool World_Mining_ClickPickup_WorksSameAsInteractKey()
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

        var droppedId = afterCut.DroppedItems.First(d => d.Item == ItemType.Mineral).Id;
        world.ApplyCommand(1, new ClientCommand(1, PickupDroppedItemId: droppedId));
        var afterPickup = world.CreateSnapshot();
        return afterPickup.Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.Contains(ItemType.Mineral)
            && afterPickup.DroppedItems.All(d => d.Id != droppedId);
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

    // Holding a welding tool is not enough: it needs a tank with something left in it, just like
    // the cutter (World.Welding.cs).
    private static bool World_Welding_WithoutTank_WeldsNothing()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor

        MoveCharacterTo(world, 1, 11.5f, 5f); // corridor welding-tool station
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pick up welding tool
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: 0)); // hold it, no tank attached

        MoveCharacterTo(world, 1, 11.5f, 0.5f); // stand next to the corridor's top wall block
        BreachEveryRoom(world);

        var breachCountBefore = CountBreaches(world.CreateSnapshot(), "corridor");
        if (breachCountBefore == 0)
            return false;

        for (var i = 0; i < 3 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: 0f, LookY: -1f));
            world.Step(RealtimeStep);
        }

        var afterCharacter = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return CountBreaches(world.CreateSnapshot(), "corridor") == breachCountBefore
               && afterCharacter.WelderTank is null;
    }

    // A welding tank never fits a cutter's socket and an oxygen tank never fits a welder's - the
    // two consumables don't interchange (TankSockets), so the attach is refused rather than
    // quietly accepted.
    private static bool World_TankSockets_WrongTankTypeIsRejected()
    {
        var world = new World();
        world.SpawnCharacter(1);

        TakeFromRack(world, ItemType.Cutter);
        TakeTankFromRack(world, ItemType.WeldingTank);
        var slotsBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray();
        var cutterSlot = Array.IndexOf(slotsBefore, ItemType.Cutter);
        var weldingTankSlot = Array.IndexOf(slotsBefore, ItemType.WeldingTank);

        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: weldingTankSlot, AttachTankToSlot: cutterSlot));

        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return after.MainSlotTanks[cutterSlot] is null
               && after.MainSlots[weldingTankSlot] == ItemType.WeldingTank;
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

}
