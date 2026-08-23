namespace SpaceAdventure.Shared.Model;

// Static definition of a hull camera's own junction box (M48 follow-up - "камеры как устройства
// корабля, как и любая другая система"): a fixed fixture per ship class, not something a player
// installs or a virtual mode toggled out of thin air. RoomId/X/Y is the physical box a crew member
// walks up to and wires/repairs - the same interior/exterior split Turret already has between its
// PeriscopePosition (crewed from inside) and its muzzle (HullCameraMount does the same job here,
// deriving the optical head's actual outside position from MountSide instead of storing it).
public sealed record HullCamera(string Id, string RoomId, float X, float Y, CameraMountSide MountSide)
{
    public Vec2 InteriorPosition => new(X, Y);
}
