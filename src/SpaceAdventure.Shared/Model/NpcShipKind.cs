namespace SpaceAdventure.Shared.Model;

// Persistent ambient traffic in the current star system (game_design.md - M43): unlike the
// squadrons hostile sectors/stations spawn on demand (World.EnemyFleet.cs), these fly the field
// whether or not the player ever notices them. Cargo never fights; Scout never fights either (its
// role is reconnaissance, not combat - M44's scanner is the intended payoff for that distinction);
// Military is the only kind that can turn a standing-driven hostility into a real fight
// (World.NpcShips.cs's TryEngageHostileNpc).
public enum NpcShipKind
{
    Cargo,
    Military,
    Scout,
}
