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

        DockAtStation(world, "trade-station");
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));
        if (world.Campaign != CampaignStage.RescueAssigned ||
            world.ActiveQuest is not { Kind: QuestKind.Bounty, DestinationPointId: "sector-delta", IssuedByPointId: "mining-outpost" })
            return false;

        WinBattleAt(world, "sector-delta");
        DockAtStation(world, "mining-outpost");
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));
        if (world.Campaign != CampaignStage.EdgeBeckons)
            return false;

        // Teleports clear of sol's own warp zone rather than actually flying there - same reason
        // TestRunner.StarSystems.cs's own FlyToSolWarpZoneAndStop does (see its doc comment): at
        // KSP scale the warp zone sits hundreds of billions of units out, days of simulated flight
        // even at CruiseMaxSpeed, wildly past any test's tick budget. The bearing (50,58) is the
        // same one the old 300x300 field used (was a fixed point at that offset from the field's
        // own centre, (150,295)) - preserved here and just extended out to the current, real
        // WarpZoneRadius instead of repeating the old fixed offset.
        var fieldCenter = world.AsteroidField.Center;
        var safeBearing = new Vec2(50f, 58f).Normalized();
        var warpZoneTarget = fieldCenter + safeBearing * (world.GalaxyMap.GetSystem("sol").WarpZoneRadius + 50f);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        world.DebugPlaceShip(warpZoneTarget);
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

        DockAtStation(world, "home-station");
        world.Step(RealtimeStep);

        return world.Campaign == CampaignStage.Complete && world.StoryLog.Count == 5;
    }
}
