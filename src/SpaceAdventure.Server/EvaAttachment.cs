namespace SpaceAdventure.Server;

// What an outside character is magnetized to (game_design.md Phase 3, M17) - server-only, never
// sent to the client, which only ever needs the resulting absolute world position.
public enum EvaAttachment
{
    None,   // free-floating, drifting on EvaVelocity (jetpack/momentum)
    Ship,
    Asteroid,
    Station,
    // The currently boardable enemy hull (World.Boarding.cs's BoardableEnemy) - same "magnetized,
    // walk the plating" model as Ship, just against a hull that moves and turns on its own
    // (World.EnemyFleet.cs), so EvaLocalOffset is read back out via that hull's own
    // Position/RotationDegrees each tick rather than the player's own fixed ones.
    EnemyShip,
}

// Which body a drifter just kicked off from, so its boots don't immediately grab them back while
// everything else still can (World.Eva.cs's TryAutoAttach).
public enum PushOffOrigin
{
    None,
    Ship,
    Asteroid,
    Station,
    EnemyShip,
}
