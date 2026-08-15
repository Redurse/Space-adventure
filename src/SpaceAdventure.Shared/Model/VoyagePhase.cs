namespace SpaceAdventure.Shared.Model;

// Open-ended voyage loop (game_design.md section 5 — "маршрут выбирает сам игрок"): pick any
// galaxy point and fly there freely; what happens on arrival depends on the point's kind.
public enum VoyagePhase
{
    Traveling, // in open space; TravelTargetPointId set once a destination is chosen, else idle
    Battle,    // arrived at a HostileSector point, fighting
    Station,   // docked at a Station point, services available
}
