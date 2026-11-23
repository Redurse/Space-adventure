using Anabiosis.Shared.Model;
using Anabiosis.Shared.War;

namespace Anabiosis.Server;

// Chapter two of the campaign, after the intro ("Груз для Гаммы"): the artifact the Union is guarding on the mother ship is the
// core of the virus, and destroying it stops the virus everywhere at once. The crew first finds the tool at the great precursor
// post, then has to reach the mother ship (which runs from the virus, World.War - WarSim.Mother.cs) before the virus catches it.
// If the mother ship falls first, the artifact is the virus own and the chapter is lost - the war goes on all the same. It only
// runs in a game with a war; a plain world never leaves the intro.
public sealed partial class World
{
    private const double CoreHuntDwellSeconds = 6.0;
    private const double GuardianDelaySeconds = 8.0;
    private const int GuardianCount = 4;
    private double _coreHuntDwell;

    // What the crew is after right now and where, for the galactic map: null when there is nothing to chase.
    public string? CampaignObjective { get; private set; }
    public string? CampaignTargetSystemId { get; private set; }

    // Test hook: the post defenders are already beaten.
    public void DebugDefeatGuardians() => _guardiansDefeated = true;

    // Test hook: the ship is in this system now, wherever it was.
    public void DebugSetCurrentSystem(string systemId)
    {
        _currentSystemId = systemId;
        SetShipFieldPosition(GalaxyMap.GetSystem(systemId).Field.Center + new Vec2(900, 0));
    }

    private string SystemName(int warIndex) => GalaxyMap.Systems[warIndex].Name;

    private void StepCoreHunt(double deltaSeconds)
    {
        CampaignObjective = null;
        CampaignTargetSystemId = null;
        if (_war is null)
            return;

        switch (_campaignStage)
        {
            case CampaignStage.Complete:
                LogStory("Весть с границы: колония пала, её не осталось. Артефакт эвакуировали на колониальную матку, но восстание идёт следом - оно тянется к артефакту, как к собственному сердцу. " +
                    "Учёные говорят: артефакт и есть ядро восстания. Уничтожь его, и остановится всё. Нужен инструмент предтечей, он лежит в Великом посте, и потом нужно успеть к матке раньше восстания.");
                _campaignStage = CampaignStage.CoreHunt;
                break;

            case CampaignStage.CoreHunt:
            {
                if (_war.Mother is { Alive: false })
                {
                    LoseArtifact();
                    break;
                }
                var post = _war.PrecursorIndex;
                if (post < 0)
                    break;
                CampaignObjective = _guardiansDefeated
                    ? $"Заберите инструмент предтечей: Великий пост в системе «{SystemName(post)}»"
                    : $"Найдите инструмент предтечей: Великий пост в системе «{SystemName(post)}» (его стерегут)";
                CampaignTargetSystemId = GalaxyMap.Systems[post].Id;
                if (_currentSystemId != CampaignTargetSystemId || IsDocked || IsInBattle || !_guardiansDefeated)
                {
                    _coreHuntDwell = 0;
                    break;
                }
                _coreHuntDwell += deltaSeconds;
                if (_coreHuntDwell < CoreHuntDwellSeconds)
                    break;
                _coreHuntDwell = 0;
                LogStory("Великий пост предтечей. Среди тихих залов лежит их незаконченное оружие против той же беды. Инструмент у вас. Теперь - к матке, и быстрее, чем восстание.");
                Notify("Инструмент предтечей получен");
                _campaignStage = CampaignStage.MotherChase;
                break;
            }

            case CampaignStage.MotherChase:
            {
                if (_war.Mother is not { Alive: true } mother)
                {
                    LoseArtifact();
                    break;
                }
                var where = mother.Where;
                CampaignObjective = $"Доберитесь до матки: сейчас она в системе «{SystemName(where)}»";
                CampaignTargetSystemId = GalaxyMap.Systems[where].Id;
                if (_currentSystemId != CampaignTargetSystemId || IsInBattle)
                {
                    _coreHuntDwell = 0;
                    break;
                }
                _coreHuntDwell += deltaSeconds;
                if (_coreHuntDwell < CoreHuntDwellSeconds)
                    break;
                _coreHuntDwell = 0;
                _war.DestroyVirus();
                LogStory("Вы успели. Инструмент касается артефакта, и тот рассыпается. Восстание гаснет всюду разом: корабли замирают, колонии затихают, без огня и взрывов. " +
                    "Связь с родной галактикой возвращается. Союз начинает отстраиваться, а вы - герои.");
                Notify("Ядро уничтожено: восстание угасло");
                _campaignStage = CampaignStage.CoreDestroyed;
                break;
            }
        }
    }

    // The post defenders take a ship that comes near for an infected one (the same signature, as the lore has it) and attack it
    // once, before the tool can be taken. True when that fight has just begun.
    private bool TryStartGuardianBattle(double deltaSeconds)
    {
        if (_campaignStage != CampaignStage.CoreHunt || _guardiansDefeated || _war is null || _war.PrecursorIndex < 0)
            return false;
        if (_currentSystemId != GalaxyMap.Systems[_war.PrecursorIndex].Id)
        {
            _guardianClock = 0;
            return false;
        }
        _guardianClock += deltaSeconds;
        if (_guardianClock < GuardianDelaySeconds)
            return false;
        _guardianClock = 0;
        _battleWar = true;
        _battleGuardians = true;
        _battleWarNode = _war.PrecursorIndex;
        _battleWarHulls = GuardianCount;
        _battleFaction = null;
        _battleWarEncounterPosition = _shipFieldPosition;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
        SpawnEnemySquadron(GuardianCount);
        _crewShipId = BoardableEnemy?.Id;
        Notify("Стражи поста принимают вас за повстанцев и атакуют!");
        return true;
    }

    private void LoseArtifact()
    {
        LogStory("Колониальная матка уничтожена. Артефакт в руках восстания, и теперь ничто не остановит его. Союз обречён, но война продолжается.");
        Notify("Матка потеряна: артефакт утрачен");
        _campaignStage = CampaignStage.ArtifactLost;
    }
}
