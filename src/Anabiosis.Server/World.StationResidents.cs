using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Where the docked station's walking residents live (StationResidentSim). One crowd per station, built
// the first time it is needed and dropped whenever the station layouts are rebuilt (a hull swap re-anchors
// every cached station, so the old crowd's coordinates would be stale).
public sealed partial class World
{
    private (Station Station, StationResidentSim Sim)? _residentSim;

    private StationResidentSim ResidentSim()
    {
        var station = Station;
        if (_residentSim is { } current && ReferenceEquals(current.Station, station))
            return current.Sim;
        var sim = new StationResidentSim(station, _dockedPointId ?? _nearestStationPointId ?? GalaxyMap.HomePointId);
        _residentSim = (station, sim);
        return sim;
    }

    // Only simulated while somebody is actually on the station to see it - docked, with the player
    // elsewhere, nothing needs to walk.
    private void StepStationResidents(double deltaSeconds, List<Character> onStation)
    {
        if (!IsDocked)
            return;
        ResidentSim().Step(deltaSeconds, _stationAlerted, onStation.Select(c => c.Position).ToList());
    }

    private IReadOnlyList<StationResidentState>? CreateStationResidentStates() =>
        IsDocked ? ResidentSim().States : null;
}
