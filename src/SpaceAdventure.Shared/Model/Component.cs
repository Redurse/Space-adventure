namespace SpaceAdventure.Shared.Model;

// Replaces WireNode. A physical, walkable-to wiring part - either part of the hull's built-in power
// backbone (WireGraphFactory, Kind is Distribution/Junction/Device, never removable, same role
// ShipSystemDevice already plays) or a purchasable logic/sensor/actuator part installed at a
// ComponentMount (M23), which can be uninstalled again.
//
// TargetId/TargetPowerSystem/TimerSeconds are the only kind-specific payload any component needs -
// the same shape as ShipSystemDevice.System or ToolStation.Item: TargetId holds an AutoDoorController's
// driven Door id (copied from its ComponentMount.TargetDoorId at install time, M23); sensors need no
// separate target, they read their own RoomId directly. TargetPowerSystem/TimerSeconds are read only
// by PowerLossSensor/Timer respectively (M21-M22) - present now so the record never needs to grow
// again once those kinds start using it.
public sealed record Component(
    string Id,
    ComponentKind Kind,
    string RoomId,
    float X,
    float Y,
    string? TargetId = null,
    PowerSystemId? TargetPowerSystem = null,
    float TimerSeconds = 1f)
{
    public Vec2 Position => new(X, Y);
}
