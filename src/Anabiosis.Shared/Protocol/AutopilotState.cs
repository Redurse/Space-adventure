namespace Anabiosis.Shared.Protocol;

// Direct user request ("игрок сможет указать на карте точку куда корабль должен долететь") - lets
// the client draw a destination marker/line while World.Autopilot.cs has an active course, the same
// "resend the live state every tick" shape every other snapshot field already uses (no delta/diff
// protocol in this codebase). Null DestinationX/Y (not just IsActive=false) so the client never
// needs a separate "was this ever set" check.
public sealed record AutopilotState(bool IsActive, float? DestinationX, float? DestinationY);
