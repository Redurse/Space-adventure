namespace SpaceAdventure.Shared.Model;

// The "ОБУЧЕНИЕ" run (World.Tutorial.cs) - a short, linear checklist through the ship's own core
// controls, entirely separate from the real campaign (CampaignStage). Only ever advances forward,
// same convention CampaignStage already uses.
public enum TutorialStage
{
    NotStarted,
    ReachReactor,
    AllocatePower,
    ManHelm,
    ToggleDoor,
    Complete,
}
