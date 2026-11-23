using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

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

    // Station shopping now covers the two tools' own replacement tanks (game_design.md section
    // 10), bought fully charged straight from the rack - Inventory.TryAdd already fills any
    // TankSockets.IsTank item on pickup, the generic buy path (World.Trade.cs's TryBuyItem) needed
    // nothing of its own beyond the two new TradeCatalog entries.
    private static bool World_Trade_BuyOxygenAndWeldingTank_AddsFullyChargedTanks()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts docked at the home station
        var creditsBefore = world.CreateSnapshot().Credits;

        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.OxygenTank));
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.WeldingTank));

        var snapshot = world.CreateSnapshot();
        var inventory = snapshot.Characters.Single(c => c.PlayerId == 1).Inventory!;
        var slots = inventory.MainSlots.ToArray();
        var oxygenSlot = Array.IndexOf(slots, ItemType.OxygenTank);
        var weldingSlot = Array.IndexOf(slots, ItemType.WeldingTank);

        return snapshot.Credits == creditsBefore - 35 - 35 && oxygenSlot >= 0 && weldingSlot >= 0
            && inventory.MainSlotTanks[oxygenSlot] == TankSockets.FullChargeOf(ItemType.OxygenTank)
            && inventory.MainSlotTanks[weldingSlot] == TankSockets.FullChargeOf(ItemType.WeldingTank);
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

        EnterBattle(world);
        if (!world.IsInBattle)
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

        DockAtStation(world, quest.DestinationPointId);
        if (!world.IsDocked)
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

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // left the dock
        world.ApplyCommand(1, new ClientCommand(1, AbandonQuestPressed: true));

        return world.CreateSnapshot().ActiveQuest is null
            && world.GetStanding(issuerFaction) == standingBefore + FactionDefinitions.StandingPenaltyForAbandoningQuest;
    }

    private static bool World_Quest_Accept_FailsWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);

        EnterBattle(world);
        if (!world.IsInBattle)
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

    // A first-aid kit stops a wound's bleeding, gives blood and health back, and is used up.
    private static bool World_MedKit_HealsSelfAndConsumesItem()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var medkitSlot = TakeFromRack(world, ItemType.MedKit);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: medkitSlot)); // hold it

        world.DebugDamageCharacter(1, DamageKind.Bullet, 30f, BodyPart.LeftArm);
        var hurt = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (hurt.Health >= Character.MaxHealth || !hurt.IsBleeding || world.DebugBody(1).IsDowned)
            return false; // setup problem - the wound must hurt and bleed without knocking them out

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // use the medkit

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.Health > hurt.Health && !me.IsBleeding && me.Inventory!.MainSlots.All(s => s != ItemType.MedKit);
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

        EnterBattle(world);
        if (!world.IsInBattle)
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

        // Flying there needs a hand on the helm first (no more autopilot) - man the bow turret only
        // once the fight has actually started.
        EnterBattle(world);
        MoveCharacterTo(world, 1, 1.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man the bow turret

        // The enemy no longer sits perfectly still (World.EnemyFleet.cs's ambient sway) - track its
        // actual bearing and retry the shot if it swayed off the aimed line before the shell arrived.
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

            var hpBefore = world.DebugEnemyHullHp();
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            StepFor(world, 20);

            // Base shot damage is TurretBalance.MagneticDamage - with the upgrade it should deal more.
            var hpAfter = world.DebugEnemyHullHp();
            if (hpAfter < hpBefore)
                return hpBefore - hpAfter > TurretBalance.MagneticDamage * World.PlayerShotWallDamageScale + 0.01f;
        }

        return false; // never landed a hit within the retry budget
    }

    // The generic Component/Wire graph (World.Wiring.cs) replaces the old fixed WireNetwork/
}
