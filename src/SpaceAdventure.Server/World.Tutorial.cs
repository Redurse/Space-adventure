using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// The "ОБУЧЕНИЕ" button on the main menu (Game1.Menu.cs) - a short, guided run through the ship's
// core controls, entirely separate from the real campaign (World.Campaign.cs): no quest, no
// autosave (GameServer is built with a null save path for it), just a checklist that advances on
// its own as the player actually does each thing, the same "observe existing state, don't invent
// new commands" shape the campaign's own stage tracking already uses. Always the starter/Frigate
// hull (its room ids - "reactor" etc. - are hardcoded below), so there's no ship-select step first.
public sealed partial class World
{
    private TutorialStage _tutorialStage = TutorialStage.NotStarted;
    // Two of the four steps are one-shot player actions rather than a standing world state (an
    // allocation or a room can be checked at any time; "did the player ever push the stick" or
    // "did a door ever get toggled" can't), so they're latched here the moment ApplyCommand sees
    // them, instead of trying to reconstruct "did this ever happen" from a snapshot later.
    private bool _tutorialHelmThrusted;
    private bool _tutorialDoorToggled;

    public TutorialStage Tutorial => _tutorialStage;

    public void StartTutorial() => _tutorialStage = TutorialStage.ReachReactor;

    private void ObserveTutorialInput(Character character, ClientCommand command)
    {
        if (_tutorialStage is TutorialStage.NotStarted or TutorialStage.Complete)
            return;

        if (character.IsAtHelm && (command.HelmThrottle != 0f || command.HelmTurn != 0f))
            _tutorialHelmThrusted = true;
        if (command.DoorToggleId is not null)
            _tutorialDoorToggled = true;
    }

    private void StepTutorial()
    {
        if (_tutorialStage is TutorialStage.NotStarted or TutorialStage.Complete)
            return;
        if (!_characters.TryGetValue(1, out var character))
            return;

        switch (_tutorialStage)
        {
            case TutorialStage.ReachReactor when character.RoomId == "reactor":
                _tutorialStage = TutorialStage.AllocatePower;
                break;
            case TutorialStage.AllocatePower when Enum.GetValues<PowerSystemId>().Any(s => PowerGrid.GetAllocation(s) > 0.01f):
                _tutorialStage = TutorialStage.ManHelm;
                break;
            case TutorialStage.ManHelm when _tutorialHelmThrusted:
                _tutorialStage = TutorialStage.ToggleDoor;
                break;
            case TutorialStage.ToggleDoor when _tutorialDoorToggled:
                _tutorialStage = TutorialStage.Complete;
                break;
        }
    }

    // What the client shows as a persistent banner (Game1.cs) - null outside the tutorial run
    // entirely, so the banner just doesn't draw for every other session.
    public string? GetTutorialObjective() => _tutorialStage switch
    {
        TutorialStage.ReachReactor => "ОБУЧЕНИЕ: дойдите до реакторного отсека (WASD)",
        TutorialStage.AllocatePower => "ОБУЧЕНИЕ: откройте распределительный блок (E) и подайте энергию на любую систему (Q/E)",
        TutorialStage.ManHelm => "ОБУЧЕНИЕ: дойдите до штурвала, встаньте за него (E) и подвигайте корабль (WASD)",
        TutorialStage.ToggleDoor => "ОБУЧЕНИЕ: откройте или закройте дверь (ЛКМ)",
        TutorialStage.Complete => "ОБУЧЕНИЕ ЗАВЕРШЕНО! Возвращайтесь в главное меню, когда будете готовы.",
        _ => null,
    };
}
