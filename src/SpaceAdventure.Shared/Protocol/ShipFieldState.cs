using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// The ship's physical state in the current system's field (game_design.md section 5, M15;
// continuous/always-on since M39). Position/RotationDegrees are in the current AsteroidField's
// local coordinates — the same frame WallBlocks and every interior room use, so a collision's
// contact point maps straight onto the ship's hull without any extra transform. ThrustX/Y is the
// currently commanded joystick vector (kept for rendering the helm panel's handle even when nobody
// is actively dragging it).
public sealed record ShipFieldState(
    float X,
    float Y,
    float RotationDegrees,
    float VelocityX,
    float VelocityY,
    float ThrustX,
    float ThrustY,
    bool AutoStabilize,
    // Arc (banked turning, tied to speed) or Rcs (free rotation) - World.ShipField.cs, M41.
    ShipControlMode ControlMode = ShipControlMode.Arc);
