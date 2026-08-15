namespace SpaceAdventure.Shared.Model;

// game_design.md section 2: ballistic is the default (magazine + ammo crate); the laser turret
// is the sole exception, drawing from a capacitor charged by the WeaponCharger power slider.
public enum TurretWeaponType
{
    Ballistic,
    Laser,
}
