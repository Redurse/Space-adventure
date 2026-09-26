namespace Anabiosis.Shared.Model;

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
    int MagazineCapacity = 0,              // Magnetic/MachineGun only
    float MaxCharge = 0f,                  // laser only — full coolant headroom
    float ChargePerShot = 0f,              // laser only — heat drained per firing tick (CooldownSeconds apart)
    float RechargePerPowerUnitPerSecond = 0f, // laser only — cooling rate, scales with WeaponCharger allocation
    TurretMountSide MountSide = TurretMountSide.Aft,
    int PelletsPerBurst = 1,               // MachineGun only — how many individually-traced pellets one trigger-pull fires
    float PelletSpreadDegrees = 0f,        // MachineGun only — random aim jitter applied to each pellet
    // Direct user request (screenshot of a turret mount built out of wall tiles - "реальные такие
    // границы... при наведении мышкой в игре") - the periscope's own real tile footprint shrank
    // from a plain 3x3 square to a 1x3 column (CustomDeviceFootprint.Size), so which way it's
    // turned now actually changes its shape - unlike every other rotatable-but-square kind before
    // it, a turret's own hover/click rect (Game1.Interactables.cs/Game1.Input.cs) needs to know
    // this to stay correct client-side, the same reason HelmConsole/NavigationConsole already carry
    // their own Rotated flag through to the snapshot instead of leaving it editor-only cosmetic.
    bool Rotated = false)
{
    public Vec2 PeriscopePosition => new(PeriscopeX, PeriscopeY);
}
