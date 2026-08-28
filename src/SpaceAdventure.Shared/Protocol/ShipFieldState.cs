using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// The ship's physical state in the current system's field (game_design.md section 5, M15;
// continuous/always-on since M39). Position/RotationDegrees are in the current AsteroidField's
// local coordinates — the same frame WallBlocks and every interior room use, so a collision's
// contact point maps straight onto the ship's hull without any extra transform. ThrustX/Y is the
// currently commanded joystick vector (kept for rendering the helm panel's handle even when nobody
// is actively dragging it).
// X/Y are double, not float (M58 follow-up - matching World.ShipField.cs's own M54 double-precision
// _shipFieldPosition accumulator, and the same Asteroid/GalaxyPoint fix this session already made):
// at KSP-real field scale (hundreds of billions of units) a float32 snapshot position can't tell two
// points fewer than ~77,000 units apart apart at all - every "did the ship's position actually
// change" check reading straight off a WorldSnapshot (tests, and any client code doing the same)
// silently saw two bit-identical numbers despite genuine, correctly-simulated movement underneath.
public sealed record ShipFieldState(
    double X,
    double Y,
    float RotationDegrees,
    float VelocityX,
    float VelocityY,
    float ThrustX,
    float ThrustY,
    bool AutoStabilize,
    // Arc (banked turning, tied to speed) or Rcs (free rotation) - World.ShipField.cs, M41.
    ShipControlMode ControlMode = ShipControlMode.Arc);
