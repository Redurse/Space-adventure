using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// A laser rifle's shot is not a travelling bolt but a beam: a straight red line from the muzzle that
// ends where it hit something or ran into a wall. It exists for a fraction of a second, fading as
// `Life` (1 at the moment of the shot, 0 when it is gone) runs down.
public sealed record LaserBeamState(string Id, ShotScene Scene, float StartX, float StartY, float EndX, float EndY, float Life);
