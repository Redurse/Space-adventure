namespace Anabiosis.Shared.Protocol;

// Direct user request ("игрок сможет указать на карте точку куда корабль должен долететь") - lets
// the client draw a destination marker/line while World.Autopilot.cs has an active course, the same
// "resend the live state every tick" shape every other snapshot field already uses (no delta/diff
// protocol in this codebase). Null DestinationX/Y (not just IsActive=false) so the client never
// needs a separate "was this ever set" check.
//
// PredictedFacingDegrees - direct user request ("призрак начинает поворачиваться, а так ведь быть
// не должно") - the SAME desiredBearingDegrees World.Autopilot.cs's own StepAutopilot actually
// steers toward this tick (RMB override, velocity-direction, or the shorter-of-two-headings reverse
// pick for a hull with no real strafe), echoed straight to the client rather than having it guess
// its own "bearing from wherever I am right now to the destination" approximation - that guess
// isn't just less accurate, it's actively unstable near arrival (flips a full 180° the instant the
// ship's own overshoot carries it past the point), which is exactly why the ghost used to visibly
// spin as the ship closed in. Relaying the server's own already-stabilized value sidesteps
// reproducing all of that stabilization logic a second time on the client.
public sealed record AutopilotState(bool IsActive, float? DestinationX, float? DestinationY, float? PredictedFacingDegrees);
