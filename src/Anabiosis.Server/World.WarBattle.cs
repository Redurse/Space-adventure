using Anabiosis.Shared.Model;
using Anabiosis.Shared.War;

namespace Anabiosis.Server;

// Where the galactic war reaches the player ship. When virus ships hold the system the ship is in (they stand there, or the
// system is theirs) they find the ship after a short while and attack it with a squadron as big as what they have there. The
// fight is the same one every other battle is (World.EnemyFleet.cs), set apart from a sector battle and from an NPC hull battle
// as a third kind (IsInBattle). What dies there is taken off the war, so a player who clears the ships out of a system helps the
// Union hold it. The stations of a system the virus holds do not answer.
public sealed partial class World
{
    private const double WarEncounterDelaySeconds = 25.0;
    private const double WarEncounterCooldownSeconds = 150.0;
    private const float WarSupplyPerHull = 14f;
    private const float WarSupplyPerSquadronHull = 25f;
    private const int WarSquadronMax = 6;

    // The guardians of the great precursor post (World.CoreHunt.cs): the same kind of fight, against the post own defenders.
    private bool _battleGuardians;
    private double _guardianClock;
    private bool _guardiansDefeated;
    private bool _battleWar;
    private Vec2 _battleWarEncounterPosition;
    private int _battleWarNode = -1;
    private int _battleWarHulls;
    private string? _warEncounterSystemId;
    private double _warEncounterClock;
    private double _warEncounterCooldown;

    // The war index for a system: the order of the galaxy list of systems.
    private int WarIndexOf(string systemId)
    {
        var systems = GalaxyMap.Systems;
        for (var i = 0; i < systems.Count; i++)
            if (systems[i].Id == systemId)
                return i;
        return -1;
    }

    // True while the virus holds the system the ship is in, so its stations are overrun.
    private bool IsVirusHeldHere
    {
        get
        {
            if (_war is null)
                return false;
            var index = WarIndexOf(_currentSystemId);
            return index >= 0 && index < _war.Nodes.Count && _war.Nodes[index].Owner == WarFaction.Virus;
        }
    }

    private void StepWarEncounter(double deltaSeconds)
    {
        if (_warEncounterCooldown > 0)
            _warEncounterCooldown -= deltaSeconds;
        if (_war is null || IsInBattle || IsDocked || IsLandedOnPlanet)
            return;

        if (TryStartGuardianBattle(deltaSeconds))
            return;

        var index = WarIndexOf(_currentSystemId);
        if (index < 0 || index >= _war.Nodes.Count)
            return;
        var presence = _war.VirusPresenceAt(index);
        if (presence < 1f)
        {
            _warEncounterClock = 0;
            return;
        }
        if (_warEncounterSystemId != _currentSystemId)
        {
            _warEncounterSystemId = _currentSystemId;
            _warEncounterClock = 0;
            Notify(_war.Nodes[index].Owner == WarFaction.Virus
                ? "Система в руках восстания: станции молчат, на радаре чужие корабли"
                : "В системе повстанческий флот: он вас заметит");
        }
        if (_warEncounterCooldown > 0)
            return;
        _warEncounterClock += deltaSeconds;
        if (_warEncounterClock < WarEncounterDelaySeconds)
            return;

        _warEncounterClock = 0;
        _battleWar = true;
        _battleWarNode = index;
        _battleFaction = null; // no faction standing is at stake: the virus is no one
        _battleWarEncounterPosition = _shipFieldPosition;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
        _battleWarHulls = Math.Clamp((int)MathF.Ceiling(presence / WarSupplyPerSquadronHull), 1, WarSquadronMax);
        SpawnEnemySquadron(_battleWarHulls);
        _crewShipId = BoardableEnemy?.Id;
        Notify("Повстанческий флот атакует!");
    }

    // The battle is over when none of the squadron is left flying: what was destroyed comes off the virus in that system.
    private void ResolveWarBattleLosses()
    {
        SpawnEnemyReinforcements();
        var stillFlying = _enemyShips.Count(e => e.Alive && !e.Derelict) + _enemyReinforcements;
        if (stillFlying > 0)
            return;
        if (_battleGuardians)
        {
            _battleGuardians = false;
            _guardiansDefeated = true;
            Notify("Стражи поста уничтожены");
        }
        else
        {
            _war?.StrikeVirusAt(_battleWarNode, _battleWarHulls * WarSupplyPerHull);
            Notify("Повстанческий флот уничтожен");
        }
        _battleWar = false;
        _battleFaction = null;
        _warEncounterCooldown = WarEncounterCooldownSeconds;
        _projectiles.Clear();
    }

    private bool HasFledTheWarBattle() =>
        _battleWar && (_battleWarEncounterPosition - _shipFieldPosition).Length() > BattleFleeDistance;

    private void FleeWarBattle()
    {
        _battleWar = false;
        _battleGuardians = false;
        _battleFaction = null;
        _warEncounterCooldown = WarEncounterCooldownSeconds;
        _projectiles.Clear();
        NudgeAwayFromFieldEdge();
    }
}
