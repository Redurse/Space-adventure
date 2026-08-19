using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // StartCampaign is the one-shot entry point GameServer calls for a genuinely new game (never
    // for a plain `new World()`, which is what almost every other test in this project builds, and
    // never automatically from Step) - calling it here is exactly what a fresh game does.
    private static bool World_Campaign_StartsWithDeliveryQuestAssigned()
    {
        var world = new World();
        world.StartCampaign();

        return world.Campaign == CampaignStage.DeliveryAssigned
            && world.ActiveQuest is { Kind: QuestKind.Delivery, DestinationPointId: "trade-station" }
            && world.StoryLog.Count == 1;
    }

    // A second call (e.g. if something ever called it twice by mistake) must not re-log the intro
    // or re-roll the quest - StartCampaign guards on CampaignStage.NotStarted.
    private static bool World_Campaign_StartCampaignIsIdempotent()
    {
        var world = new World();
        world.StartCampaign();
        var questAfterFirstCall = world.ActiveQuest;
        world.StartCampaign();

        return world.Campaign == CampaignStage.DeliveryAssigned
            && world.ActiveQuest == questAfterFirstCall
            && world.StoryLog.Count == 1;
    }

    // Giving up on the story quest must not be mistaken for completing it - NotifyStoryQuestTurnedIn
    // only ever fires from an actual turn-in (World.Quests.cs's TryTurnInQuest), never from
    // TryAbandonQuest, so the campaign simply stalls rather than skipping ahead.
    private static bool World_Campaign_AbandoningStoryQuestDoesNotAdvanceStage()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.StartCampaign();

        world.ApplyCommand(1, new ClientCommand(1, AbandonQuestPressed: true));

        return world.Campaign == CampaignStage.DeliveryAssigned
            && world.ActiveQuest is null
            && world.StoryLog.Count == 1;
    }

    // The whole "Груз для Гаммы" chain end to end: deliver to trade-station, get sent after the
    // missing miners at sector-delta, hand them off at mining-outpost, fly out past the system's
    // own warp zone, jump to alpha-centauri, and come back home - every beat exercised through the
    // exact same commands a player would send, no shortcuts.
    private static bool World_Campaign_FullChainReachesComplete()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // survives the breaches the sector-delta fight will open up
        world.StartCampaign(); // Act 1: delivery quest assigned, same call GameServer makes for a new game

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));
        if (world.Campaign != CampaignStage.RescueAssigned ||
            world.ActiveQuest is not { Kind: QuestKind.Bounty, DestinationPointId: "sector-delta", IssuedByPointId: "mining-outpost" })
            return false;

        WinBattleAt(world, "sector-delta");
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "mining-outpost"));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));
        if (world.Campaign != CampaignStage.EdgeBeckons)
            return false;

        // Undock and fly clear past sol's own warp zone, due south from mining-outpost - checked
        // clear of every asteroid and hostile sector along the way (the straight line from
        // mining-outpost's own position, (100,237), to here stays a wide margin from sector-beta,
        // the nearest hazard, unlike a straight line toward the warp-mechanic tests' own (10,150)
        // target, which passes almost through sector-beta's own capture radius from this different
        // starting point).
        world.ApplyCommand(1, new ClientCommand(1, TravelToX: 150f, TravelToY: 295f));
        for (var i = 0; i < 120 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);
        if (!world.CanWarpNow)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        world.Step(RealtimeStep);
        if (world.Campaign != CampaignStage.Returning || world.CreateSnapshot().CurrentSystemId != "alpha-centauri")
            return false;

        // Jump straight back - CanWarpNow is already armed the instant a jump lands (World.StarSystems.cs).
        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "sol"));
        if (world.CreateSnapshot().CurrentSystemId != "sol")
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
        DockAtStation(world);
        world.Step(RealtimeStep);

        return world.Campaign == CampaignStage.Complete && world.StoryLog.Count == 5;
    }
}
