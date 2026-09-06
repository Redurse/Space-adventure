namespace Anabiosis.Shared.Protocol;

// Parallels ComponentMountState's id+flag shape - whether this particular locker currently has a
// suit in it to hand out, so the client can draw a full locker differently from an empty one and
// gate the "take a suit" click on there actually being one (World.SuitLockers.cs).
public sealed record SuitLockerState(string LockerId, bool HasSuit);
