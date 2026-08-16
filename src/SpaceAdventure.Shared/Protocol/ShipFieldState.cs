namespace SpaceAdventure.Shared.Protocol;

// The ship's physical state while VoyagePhase.AsteroidField is active (game_design.md Phase 3,
// M15). Position/RotationDegrees are in the current AsteroidField's local coordinates — the same
// frame WallBlocks and every interior room use, so a collision's contact point maps straight onto
// the ship's hull without any extra transform. ThrustX/Y is the currently commanded joystick
// vector (kept for rendering the helm panel's handle even when nobody's actively dragging it).
public sealed record ShipFieldState(
    float X,
    float Y,
    float RotationDegrees,
    float VelocityX,
    float VelocityY,
    float ThrustX,
    float ThrustY,
    bool AutoStabilize);
