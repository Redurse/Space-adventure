namespace Anabiosis.Shared.Model;

// An empty physical socket for a purchased logic/sensor/actuator part - placed per hull class like
// ToolStation, but two-way like the reactor's rod slots (pull the part back out into your hand).
// Not player-placed: adding a new socket isn't a purchasable action, only what plugs into one is.
// TargetDoorId is set only on mounts the ship designer meant for an AutoDoorController - which
// door it drives is decided once, at hull-design time, not by the player.
public sealed record ComponentMount(string Id, string RoomId, float X, float Y, string? TargetDoorId = null)
{
    public Vec2 Position => new(X, Y);
}
