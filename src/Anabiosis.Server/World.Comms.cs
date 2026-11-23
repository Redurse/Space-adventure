using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Radio comms with a station (direct user request: the comms console becomes a map where a station in range is right-clicked to hail it, with the station's
// window - trade, hire crew, jobs - as in Cosmoteer). Once a line is open the station's services work as if docked: the trader's prices apply, the recruiter's
// board and the administrator's jobs are open. What is bought lands in the inventory of whoever is at the console, what is sold is taken from it - the cargo is
// beamed across, the part this game can do without a second ship. Delivering and handing in a job still needs the ship at the station.
public sealed partial class World
{
    // How far a station can be hailed, in field units (the field a system plays out in is thousands wide).
    public const float CommsRangeUnits = 1200f;
    // The line is dropped when the ship has drifted this far past the range, so a station right at the edge does not flicker on and off.
    private const float CommsRangeSlack = 1.1f;
    private const double CommsNoticeSeconds = 4.0;

    private string? _commsPointId;
    private string? _commsNotice;
    private double _commsNoticeSecondsLeft;
    // Whose recruits the board currently holds, so hailing the same station again does not reroll them.
    private string? _rosterPointId;

    // Docked, the station is the one the ship is at; otherwise the one on the line.
    private string? ServicePointId => _dockedPointId ?? _commsPointId;

    private Station? ServiceStation => ServicePointId is { } id ? GetOrCreateStation(id) : null;

    private bool HasServiceNpc(NpcKind kind) => ServiceStation is { } station && station.Npcs.Any(n => n.Kind == kind && IsNpcAlive(n));

    private bool ServiceTraderIsDead =>
        ServiceStation is { } station && station.Npcs.Any(n => n.Kind == NpcKind.Trader) && !HasServiceNpc(NpcKind.Trader);

    private FactionId ServiceFaction => ServicePointId is { } id ? OwnerOf(id) : FactionId.Independent;

    private StationKind? ServiceStationKind => ServicePointId is { } id ? GalaxyMap.GetPoint(id).StationKind : null;

    public string? CommsPointId => _commsPointId;

    private float DistanceToStation(GalaxyPoint point) => (float)(ShipMapPosition - point.Position).Length();

    private void SetCommsNotice(string text)
    {
        _commsNotice = text;
        _commsNoticeSecondsLeft = CommsNoticeSeconds;
    }

    // The station's answer to a hail: only a station in this system, in range, that is staffed and not at odds with the crew picks up.
    private void HandleHail(string pointId)
    {
        if (IsInBattle)
        {
            SetCommsNotice("Связь невозможна: бой");
            return;
        }
        if (GalaxyMap.GetSystem(_currentSystemId).Points.FirstOrDefault(p => p.Id == pointId) is not { Kind: GalaxyPointKind.Station } point)
            return;
        if (DistanceToStation(point) > CommsRangeUnits && _dockedPointId != pointId)
        {
            SetCommsNotice("Станция вне радиуса связи");
            return;
        }
        if (IsAbandonedStationPoint(pointId))
        {
            SetCommsNotice("Станция не отвечает");
            return;
        }
        if (GetStanding(OwnerOf(pointId)) <= FactionDefinitions.HostileThreshold)
        {
            SetCommsNotice("ВЫЗОВ ОТКЛОНЁН");
            return;
        }

        _commsPointId = pointId;
        _commsNotice = null;
        // A new board for a station not seen yet; the same one for a station called again.
        if (_rosterPointId != pointId)
            RegenerateRecruitRoster(GetOrCreateStation(pointId), pointId);
    }

    private void EndComms()
    {
        _commsPointId = null;
    }

    // The line holds while the station stays in range, in the same system, and nothing is shooting.
    private void StepComms(double deltaSeconds)
    {
        if (_commsNoticeSecondsLeft > 0)
        {
            _commsNoticeSecondsLeft -= deltaSeconds;
            if (_commsNoticeSecondsLeft <= 0)
                _commsNotice = null;
        }
        if (_commsPointId is not { } pointId)
            return;
        var point = GalaxyMap.GetSystem(_currentSystemId).Points.FirstOrDefault(p => p.Id == pointId);
        if (point is null || IsInBattle || DistanceToStation(point) > CommsRangeUnits * CommsRangeSlack)
        {
            if (point is not null && !IsInBattle)
                SetCommsNotice("Связь потеряна: вне радиуса");
            _commsPointId = null;
        }
    }

    private CommsState CreateCommsState()
    {
        var linked = _commsPointId is { } id ? GalaxyMap.GetPoint(id) : null;
        return new CommsState(_commsPointId, linked?.Name, HasServiceNpc(NpcKind.Trader), HasServiceNpc(NpcKind.Recruiter),
            HasServiceNpc(NpcKind.Administrator), CommsRangeUnits, _commsNoticeSecondsLeft > 0 ? _commsNotice : null);
    }
}
