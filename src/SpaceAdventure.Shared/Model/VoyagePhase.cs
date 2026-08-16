namespace SpaceAdventure.Shared.Model;

// Open-ended voyage loop (game_design.md section 5 — "маршрут выбирает сам игрок"): pick any
// galaxy point and fly there freely; what happens on arrival depends on the point's kind.
public enum VoyagePhase
{
    Traveling,     // in open space; TravelTargetPointId set once a destination is chosen, else idle
    Battle,        // arrived at a HostileSector point, fighting
    StationApproach, // arrived near a Station point; pilot manually from the helm to the docking port
    Station,       // docked at a Station point, services available
    AsteroidField, // arrived at an AsteroidField point; ship is piloted manually from the helm (M15)
}

public static class VoyagePhases
{
    // Whether there is actually a station out there in the field right now. The World always holds
    // a Station object (it's the layout you dock with), but it only physically exists where the
    // voyage has taken you to a station point — a hostile sector or an asteroid belt has none, and
    // drawing one anyway put a friendly berth, complete with "Стыковка" markers, in the middle of
    // a firefight. Every view that plots the station goes through this so they can't disagree.
    public static bool HasStationInField(this VoyagePhase phase) =>
        phase is VoyagePhase.StationApproach or VoyagePhase.Station;
}
