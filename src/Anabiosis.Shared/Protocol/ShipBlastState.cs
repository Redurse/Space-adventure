namespace Anabiosis.Shared.Protocol;

public enum ShipBlastKind
{
    Compartment, // a destroyed compartment
    Reactor,     // the reactor going up - the biggest one
}

// An explosion on the player's ship (World.ShipBlasts.cs). X/Y are in the ship-local (layout) frame like every
// room, Age is how many seconds ago it went off - the client draws the fireball/shockwave from it. Kept in the
// snapshot for a couple of seconds, so it survives a dropped snapshot; the client tells a new one by Id.
// InField: X/Y are field (world) coordinates instead - a torn-off piece blowing up out in space.
public sealed record ShipBlastState(string Id, float X, float Y, float Radius, float Age, ShipBlastKind Kind, bool InField = false);
