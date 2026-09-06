namespace Anabiosis.Shared.Model;

// game_design.md section 2 (enemy/weapon overhaul) - three turrets, each a real tradeoff rather
// than a strict upgrade: Magnetic is fast and cheap per shot off a big magazine but hits soft;
// Laser trades ammo for a capacitor that overheats on a sustained burn; MachineGun trades hull
// damage for a wide spray of individually-traced pellets, good against small/soft targets.
public enum TurretWeaponType
{
    Magnetic,
    Laser,
    MachineGun,
}
