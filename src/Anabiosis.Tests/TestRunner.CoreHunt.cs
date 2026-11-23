using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;
using Anabiosis.Shared.War;

// Chapter two of the campaign (World.CoreHunt.cs): the precursor tool, then the mother ship, then the end of the virus.
internal static partial class TestRunner
{
    // A world with a war whose campaign is already past the intro, ship undocked.
    private static World WorldPastTheIntro()
    {
        var first = new World(worldSeed: 4242);
        var save = first.CreateSave() with { Campaign = CampaignStage.Complete };
        var world = new World(worldSeed: 4242);
        world.SpawnCharacter(1);
        world.ApplySave(save);
        world.EnableWar();
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        return world;
    }

    private static bool World_CoreHunt_StartsAfterTheIntro_WithTheTargetOnTheMap()
    {
        var world = WorldPastTheIntro();
        world.Step(RealtimeStep);
        var snapshot = world.CreateSnapshot();
        var post = world.GalaxyMap.Systems[world.War!.PrecursorIndex].Id;
        return world.Campaign == CampaignStage.CoreHunt && snapshot.CampaignTargetSystemId == post
            && snapshot.CampaignObjective is { } text && text.Contains("инструмент");
    }

    private static bool World_CoreHunt_ReachingThePost_GivesTheTool_ThenTheMotherShipEndsTheVirus()
    {
        var world = WorldPastTheIntro();
        world.Step(RealtimeStep);
        world.DebugSetCurrentSystem(world.GalaxyMap.Systems[world.War!.PrecursorIndex].Id);
        world.DebugDefeatGuardians();
        StepFor(world, 10 * 30);
        if (world.Campaign != CampaignStage.MotherChase)
            return false;

        var mother = world.War.Mother!;
        world.DebugSetCurrentSystem(world.GalaxyMap.Systems[mother.Where].Id);
        StepFor(world, 10 * 30);
        var virusGone = !world.War.Nodes.Any(n => n.Owner == WarFaction.Virus) && !world.War.Fleets.Any(f => f.Owner == WarFaction.Virus);
        return world.Campaign == CampaignStage.CoreDestroyed && world.War.VirusDestroyed && virusGone
            && world.CreateSnapshot().CampaignObjective is null;
    }

    private static bool World_CoreHunt_MotherShipLost_LosesTheArtifact()
    {
        var world = WorldPastTheIntro();
        world.Step(RealtimeStep);
        var mother = world.War!.Mother!;
        mother.Alive = false;
        mother.Hp = 0f;
        StepFor(world, 3 * 30);
        return world.Campaign == CampaignStage.ArtifactLost && !world.War.VirusDestroyed;
    }

    // The post is guarded: arriving there starts a fight with its defenders, and the tool is not given while they live.
    private static bool World_CoreHunt_ThePostIsGuarded_BeforeTheToolIsGiven()
    {
        var world = WorldPastTheIntro();
        world.Step(RealtimeStep);
        world.DebugSetCurrentSystem(world.GalaxyMap.Systems[world.War!.PrecursorIndex].Id);
        StepFor(world, 12 * 30);
        var fighting = world.IsInBattle && world.DebugEnemyShipsInField >= 1;
        StepFor(world, 12 * 30); // still under attack: the tool must not have been handed over
        return fighting && world.Campaign == CampaignStage.CoreHunt;
    }
}
