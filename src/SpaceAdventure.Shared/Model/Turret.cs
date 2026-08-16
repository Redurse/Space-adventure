namespace SpaceAdventure.Shared.Model;

// Which plating the gun itself is bolted to (TurretMount turns this into a position and a firing
// arc). Part of the layout rather than something inferred from the periscope's position: a hull
// designed around a broadside wants its guns on the flanks whatever room the gunners sit in.
public enum TurretMountSide
{
    Aft,        // out the stern - the default the row-of-boxes classes are built around
    Fore,
    Port,       // out the left flank
    Starboard,  // out the right flank
}

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
    float RechargePerPowerUnitPerSecond = 0f, // laser only — scales with WeaponCharger allocation
    TurretMountSide MountSide = TurretMountSide.Aft)
{
    public Vec2 PeriscopePosition => new(PeriscopeX, PeriscopeY);
}
