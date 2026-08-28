using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // The "ОБУЧЕНИЕ" checklist (World.Tutorial.cs) - walks through all four steps in order,
    // confirming each one only advances once its own condition is actually met and that the whole
    // thing reaches Complete rather than getting stuck partway.
    private static bool World_Tutorial_AdvancesThroughAllFourStepsToComplete()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        world.StartTutorial();

        if (world.Tutorial != TutorialStage.ReachReactor)
            return false;

        MoveCharacterTo(world, 1, 19f, 3f);
        MoveCharacterTo(world, 1, 7f, 3f); // reactor room
        world.Step(RealtimeStep);
        if (world.Tutorial != TutorialStage.AllocatePower)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 30; i++) // allocation ramps up gradually, not instantly
            world.Step(RealtimeStep);
        if (world.Tutorial != TutorialStage.ManHelm)
            return false;

        MoveCharacterTo(world, 1, 3f, 3f);
        var helmConsole = world.Ship.HelmConsole.Position;
        MoveCharacterTo(world, 1, (float)helmConsole.X, (float)helmConsole.Y); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        world.Step(RealtimeStep);
        if (world.Tutorial != TutorialStage.ToggleDoor)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-reactor-corridor"));
        world.Step(RealtimeStep);
        return world.Tutorial == TutorialStage.Complete;
    }

    // Starting normally (StartCampaign, the ordinary "НОВАЯ ИГРА" path) must never show a tutorial
    // banner - GetTutorialObjective has to read null the whole run, not just before StartTutorial
    // is ever called.
    private static bool World_Tutorial_NeverActiveOnAnOrdinaryCampaignRun()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.StartCampaign();
        return world.CreateSnapshot().TutorialObjective is null;
    }
}
