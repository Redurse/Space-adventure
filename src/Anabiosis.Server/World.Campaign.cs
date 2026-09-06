using System.Collections.Generic;
using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// The scripted intro campaign "Груз для Гаммы" - a fixed chain of otherwise-ordinary quests
// (World.Quests.cs) and arrivals (World.StarSystems.cs), stitched together with narrative log
// lines rather than any new gameplay mechanic. Every beat is idempotent (guarded by the current
// CampaignStage, which only ever advances forward).
public sealed partial class World
{
    private CampaignStage _campaignStage = CampaignStage.NotStarted;
    private readonly List<string> _storyLog = new();

    public IReadOnlyList<string> StoryLog => _storyLog;
    public CampaignStage Campaign => _campaignStage;

    private void LogStory(string line) => _storyLog.Add(line);

    // Called once, only for a genuinely NEW game (GameServer's constructor, in the branch where
    // there's no save to load) - deliberately NOT wired into Step or the constructor itself, since
    // a plain `new World()` is exactly what nearly every other test in this project builds, and
    // none of them expect their own quest silently swapped out for the campaign's. Loading an
    // existing save carries its own Campaign stage forward through ApplySave instead of calling
    // this again.
    public void StartCampaign()
    {
        // The ship arrives with its reactor already split five ways rather than with every slider at
        // zero - twelve units each, which is the same figure World.CrewAi treats as a modest,
        // sustainable allocation for a system.
        PowerGrid.SplitEvenly();

        if (_campaignStage != CampaignStage.NotStarted)
            return;

        LogStory("Капитан, добро пожаловать на борт. Администратор станции ждёт с первым заданием: груз до Торговой станции.");
        ActiveQuest = new Quest(QuestKind.Delivery, "trade-station", "Торговая станция", DeliveryQuestReward, "home-station");
        _campaignStage = CampaignStage.DeliveryAssigned;
    }

    // Polled every tick (World.cs's Step) - both branches only ever fire once the campaign is
    // already under way (StartCampaign has run and the first quest has been turned in), so a
    // plain, campaign-unaware World never takes either of them.
    private void StepCampaign()
    {
        if (_campaignStage == CampaignStage.EdgeBeckons && _currentSystemId == "alpha-centauri")
        {
            LogStory("Альфа Центавра. Одинокий форпост Независимых на самом краю освоенного пространства. " +
                "Диспетчер по радио: «Здесь пока тихо... пока Консорциум и Вольный Флот не решат иначе».");
            _campaignStage = CampaignStage.Returning;
        }
        else if (_campaignStage == CampaignStage.Returning && _currentSystemId == "sol" &&
                 _dockedPointId == "home-station")
        {
            LogStory("Возвращение домой. Груз довезли. Врагов нажили. Экипаж жив. На сегодня достаточно.");
            _campaignStage = CampaignStage.Complete;
        }
    }

    // Called from World.Quests.cs's TryTurnInQuest right after a quest is actually handed in (not
    // abandoned) - checked against the exact quest the current stage is waiting on, so an unrelated
    // quest turned in after the campaign is already ahead (or done) never re-triggers a beat.
    private void NotifyStoryQuestTurnedIn(Quest turnedIn)
    {
        if (_campaignStage == CampaignStage.DeliveryAssigned && turnedIn.Kind == QuestKind.Delivery && turnedIn.DestinationPointId == "trade-station")
        {
            LogStory("Груз сдан. Торговец, понизив голос: «Старатели с Форпоста пропали без вести у сектора Дельта. " +
                "Может, вам стоит проверить...»");
            ActiveQuest = new Quest(QuestKind.Bounty, "sector-delta", "Сектор Дельта", BountyQuestReward, "mining-outpost");
            _campaignStage = CampaignStage.RescueAssigned;
        }
        else if (_campaignStage == CampaignStage.RescueAssigned && turnedIn.Kind == QuestKind.Bounty && turnedIn.DestinationPointId == "sector-delta")
        {
            LogStory("Старатели спасены. В толпе кто-то бормочет о войне кланов и о границе системы, " +
                "куда лучше не соваться в одиночку...");
            _campaignStage = CampaignStage.EdgeBeckons;
        }
    }
}
