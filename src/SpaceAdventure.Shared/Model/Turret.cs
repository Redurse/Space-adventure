namespace SpaceAdventure.Shared.Model;

// Static definition of a turret's periscope station (game_design.md section 2 — manual aiming
// only, no auto-aim). Runtime aim angle / manned-by / cooldown live server-side.
public sealed record Turret(
    string Id,
    string RoomId,
    float PeriscopeX,
    float PeriscopeY,
    float MinAimDegrees,
    float MaxAimDegrees,
    float DamagePerShot,
    float CooldownSeconds,
    TurretWeaponType WeaponType,
    int MagazineCapacity = 0,              // ballistic only
    float MaxCharge = 0f,                  // laser only
    float ChargePerShot = 0f,              // laser only
    float RechargePerPowerUnitPerSecond = 0f) // laser only — scales with WeaponCharger allocation
{
    public Vec2 PeriscopePosition => new(PeriscopeX, PeriscopeY);
}
