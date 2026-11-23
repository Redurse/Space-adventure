namespace Anabiosis.Shared.Protocol;

// A ship's shuttle (World.Shuttle.cs): the small craft that lives in a shuttle hangar bay and flies out of it. One per bay,
// named by the bay's device id. Docked: in the bay, X/Y are the bay. Opening: the bay doors are going up (and the room is venting)
// before the launch. Flying: out in the system, going where it was told (or braking to a stop). Returning: on its way back to the
// bay. Hp is a percent. The destination is only meaningful while HasDestination.
public enum ShuttlePhase : byte { Docked, Opening, Flying, Returning }

public sealed record ShuttleState(string BayId, ShuttlePhase Phase, double X, double Y, float RotationDegrees, float VelocityX, float VelocityY,
    int Hp, bool BayDoorOpen, bool HasDestination, double DestinationX, double DestinationY, float OpeningSecondsLeft);

// What a player asked of a shuttle: Launch (from the bay panel), Recall (back to the bay), MoveTo (a point in the system's field,
// the same space the ship's own autopilot destination uses) and Stop (cancel the destination and brake).
public enum ShuttleOrder : byte { Launch, Recall, MoveTo, Stop }

public sealed record ShuttleRequest(string BayId, ShuttleOrder Order, float TargetX = 0f, float TargetY = 0f);
