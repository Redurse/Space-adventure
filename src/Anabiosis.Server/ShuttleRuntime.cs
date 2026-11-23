using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// One shuttle: lives in the hangar bay whose device id it carries (World.Shuttle.cs), and while it is out it is a small craft
// with its own position and velocity in the system's field - the same space the ship flies in. Nothing walks about inside it.
public sealed class ShuttleRuntime
{
    public const int MaxHp = 100;

    public required string BayId { get; init; }
    public ShuttlePhase Phase { get; set; } = ShuttlePhase.Docked;
    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; set; }
    public float RotationDegrees { get; set; }
    public int Hp { get; set; } = MaxHp;
    // The bay doors are open: the room behind them is open to space.
    public bool BayDoorOpen { get; set; }
    public Vec2? Destination { get; set; }
    public double OpeningSecondsLeft { get; set; }
}
