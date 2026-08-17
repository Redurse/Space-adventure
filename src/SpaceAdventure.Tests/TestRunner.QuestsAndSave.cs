using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
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

}
