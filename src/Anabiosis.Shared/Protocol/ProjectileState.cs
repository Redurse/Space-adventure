namespace Anabiosis.Shared.Protocol;

// A shell or laser bolt in flight, in field/world coordinates - the same space the ship, the
// asteroids and the enemy hulls live in. Sent every tick so the client can draw it travelling
// rather than inferring a hit from a damage number: a shot you can watch cross the gap is a shot
// you can see miss (game_design.md section 2).
// X/Y are double, not float (M58 follow-up - same fix as ShipFieldState's own doc comment): a
// projectile lives in the same KSP-scale field the ship/asteroids/enemy hulls do.
public sealed record ProjectileState(
    string Id,
    double X,
    double Y,
    float DirectionDegrees,
    bool FromEnemy,
    bool IsLaser);
