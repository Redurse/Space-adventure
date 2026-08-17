using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
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
        var expectedCredits = 300 - Inventory.MainSlotCount * 20;
        var creditsAfterFilling = world.CreateSnapshot().Credits;

        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Wrench)); // row is full — no-op
        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;

        return creditsAfterFilling == expectedCredits
            && snapshot.Credits == expectedCredits
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

    // No docked gate on purpose (World.Quests.cs's TryAbandonQuest) - giving up has to work from
    // wherever the ship happens to be, not just back at the counter.
    private static bool World_Quest_Abandon_ClearsQuestAndCostsIssuerStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is null)
            return false;
        var issuerFaction = world.GalaxyMap.GetPoint(quest.IssuedByPointId).Faction;
        var standingBefore = world.GetStanding(issuerFaction);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: quest.DestinationPointId)); // left the dock
        world.ApplyCommand(1, new ClientCommand(1, AbandonQuestPressed: true));

        return world.CreateSnapshot().ActiveQuest is null
            && world.GetStanding(issuerFaction) == standingBefore + FactionDefinitions.StandingPenaltyForAbandoningQuest;
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

    // MedKits live in the starter rack stock now (World.Storage.cs's InitializeRackSlots), not a
    // dedicated ToolStation pickup - dragging one into a main slot is an ordinary rack withdrawal.
    private static bool World_MedKit_TakeFromRack_AddsToInventory()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var medkitSlot = Array.IndexOf(world.CreateSnapshot().RackSlots.ToArray(), ItemType.MedKit);
        var rack = world.Ship.StorageRacks[medkitSlot / StorageRack.Capacity];
        WalkAcrossShipTo(world, rack.X, rack.Y);

        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Rack, medkitSlot), MoveItemTo: new SlotRef(ItemSlotKind.Main, 0)));

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

        var medkitSlot = TakeFromRack(world, ItemType.MedKit);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: medkitSlot)); // hold it

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

        var medkitSlot = TakeFromRack(world, ItemType.MedKit);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: medkitSlot)); // hold it

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

    // The generic Component/Wire graph (World.Wiring.cs) replaces the old fixed WireNetwork/
}
