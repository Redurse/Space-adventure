namespace SpaceAdventure.Shared.Protocol;

// A shell or laser bolt in flight, in field/world coordinates - the same space the ship, the
// asteroids and the enemy hulls live in. Sent every tick so the client can draw it travelling
// rather than inferring a hit from a damage number: a shot you can watch cross the gap is a shot
// you can see miss (game_design.md section 2).
public sealed record ProjectileState(
    string Id,
    float X,
    float Y,
    float DirectionDegrees,
    bool FromEnemy,
    bool IsLaser);
