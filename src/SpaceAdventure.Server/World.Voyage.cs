using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Open galaxy navigation (game_design.md section 5): the player picks any point on the map from
// the navigation console and the ship flies there in a straight line. Arrival resolves based on
// the point's kind — a hostile sector starts a fight, a station docks and resupplies. The ship
// starts docked at the home station.
public sealed partial class World
{
    private const float MapTravelSpeedPerSecond = 20f;
    private const float ArrivalRadius = 1f;

    public GalaxyMap GalaxyMap { get; } = GalaxyMap.CreateStarter();

    private Vec2 _shipMapPosition;
    private string? _dockedPointId;
    private string? _travelTargetPointId;

    private void StepVoyage(double deltaSeconds)
    {
        switch (Phase)
        {
            case VoyagePhase.Traveling:
                StepTraveling(deltaSeconds);
                break;

            case VoyagePhase.Battle:
                if (Enemy.Hp <= 0)
                    Phase = VoyagePhase.Traveling; // back in open space, free to pick a new destination
                break;

            case VoyagePhase.Station:
                break; // sits docked until the player chooses somewhere to go
        }
    }

    private void StepTraveling(double deltaSeconds)
    {
        if (_travelTargetPointId is not { } targetId)
            return; // idling in open space with nowhere chosen yet

        var target = GalaxyMap.GetPoint(targetId);
        var toTarget = target.Position - _shipMapPosition;
        var distance = toTarget.Length();

        if (distance <= ArrivalRadius)
        {
            Arrive(target);
            return;
        }

        var step = toTarget.Normalized() * MapTravelSpeedPerSecond * (float)deltaSeconds;
        _shipMapPosition = step.Length() >= distance ? target.Position : _shipMapPosition + step;
    }

    private void Arrive(GalaxyPoint target)
    {
        _travelTargetPointId = null;
        _shipMapPosition = target.Position;

        if (target.Kind == GalaxyPointKind.HostileSector)
        {
            Enemy.Reset();
            Phase = VoyagePhase.Battle;
        }
        else
        {
            EnterStation(target.Id);
        }
    }

    // Station stop (game_design.md Phase1: "станция для дозаправки/ремонта") — tops fuel back
    // up, welds every hull breach and refills every room's air.
    private void EnterStation(string pointId)
    {
        Phase = VoyagePhase.Station;
        _dockedPointId = pointId;
        PowerGrid.Reactor.Refuel();
        _breachedWallBlockIds.Clear();
        foreach (var room in Ship.Rooms)
            _roomOxygen[room.Id] = FullOxygen;
    }

    // From the navigation console: pick a destination. Ignored mid-fight (can't flee to the
    // map) and for an unknown point id.
    private void TryStartTravel(string? pointId)
    {
        if (pointId is null || Phase == VoyagePhase.Battle)
            return;
        if (!GalaxyMap.Points.Any(p => p.Id == pointId))
            return;

        Phase = VoyagePhase.Traveling;
        _dockedPointId = null;
        _travelTargetPointId = pointId;
    }
}
