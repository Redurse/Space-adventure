namespace Anabiosis.Shared.Model;

// The scripted intro campaign ("Груз для Гаммы" - World.Campaign.cs): a fixed sequence of
// otherwise-ordinary quests/arrivals stitched together with narrative log lines, not a new
// gameplay mechanic of its own. Each value is a checkpoint the crew has reached; once past one,
// StepCampaign never re-fires the beat that reached it.
public enum CampaignStage
{
    NotStarted,
    DeliveryAssigned,
    RescueAssigned,
    EdgeBeckons,
    Returning,
    Complete,
    // Chapter two (World.CoreHunt.cs), after the intro: the artifact is the core of the virus. The crew goes for the precursor tool,
    // then races to the mother ship before the virus does. Appended last so older saves read as they were.
    CoreHunt,       // looking for the tool at the great precursor post
    MotherChase,    // has the tool, must reach the mother ship
    CoreDestroyed,  // the end: the virus is dead everywhere
    ArtifactLost,   // the mother ship fell first: the artifact is the virus own now
}
