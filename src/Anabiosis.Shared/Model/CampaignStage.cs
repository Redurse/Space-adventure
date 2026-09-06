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
}
