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
    // A landed planet's own surface (M55 - World.PlanetLanding.cs) - unlike every case above, this
    // isn't "magnetized to a moving/turning structure and grabbed on contact": the ground is real
    // gravity holding a suited character down, so it's walked directly (StepPlanetSurfaceWalk),
    // never entered via TryAutoAttach's "drift until you touch it" model. EvaLocalOffset means an
    // absolute PlanetSurface-local position, same convention as None's own "this field just holds
    // the world position directly" - the ground never moves or rotates, so no further conversion
    // is needed reading it back out (GetEvaWorldPosition).
    Planet,
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
