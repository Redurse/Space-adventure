using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const float TurretAimRateDegreesPerSecond = 60f;

    // Pulling the trigger now puts a shell in the field instead of subtracting HP from an abstract
    // enemy: it leaves the muzzle outside the hull (TurretMount), flies the way the barrel is
    // pointing, and hits whatever it runs into - which may well be nothing.
    private void TryFire(TurretRuntime turret)
    {
        if (turret.CooldownRemaining > 0 || turret.Damaged)
            return;

        var isLaser = turret.Definition.WeaponType == TurretWeaponType.Laser;
        if (isLaser)
        {
            if (turret.Charge < turret.Definition.ChargePerShot)
                return;
            turret.Charge -= turret.Definition.ChargePerShot;
        }
        else
        {
            if (turret.AmmoRemaining <= 0)
                return;
            turret.AmmoRemaining--;
        }

        turret.CooldownRemaining = turret.Definition.CooldownSeconds;

        var mount = TurretMount.For(Ship.Rooms, Ship.Turrets, turret.Definition);
        // The mount is laid out in ship-local coordinates like every other fixture; rotating both
        // the muzzle offset and the shot's heading by the hull's attitude is what puts it in field
        // space, so a turret on a ship standing on its ear still fires out of its own barrel.
        var localMuzzle = mount.Muzzle(turret.AimDegrees);
        var (hullLocalCenter, _) = GetHullLocalBounds();
        var origin = _shipFieldPosition + RotateLocalToWorld(localMuzzle - hullLocalCenter, _shipRotationDegrees);
        var direction = RotateLocalToWorld(mount.FireDirection(turret.AimDegrees), _shipRotationDegrees);

        // WeaponDamageBonus is the station Mechanic's weapon-damage upgrade (World.Upgrades.cs,
        // game_design.md section 9, M13) — applies to every turret, not per-turret leveling.
        SpawnProjectile(origin, direction, fromEnemy: false, isLaser,
            damage: turret.Definition.DamagePerShot + WeaponDamageBonus);
    }

    private void StepTurrets(double deltaSeconds)
    {
        foreach (var turret in _turretRuntimes.Values)
        {
            if (turret.CooldownRemaining > 0)
                turret.CooldownRemaining = Math.Max(0, turret.CooldownRemaining - (float)deltaSeconds);

            if (_turretAimInput.TryGetValue(turret.Definition.Id, out var aimDirection) && aimDirection != 0)
            {
                var next = turret.AimDegrees + aimDirection * TurretAimRateDegreesPerSecond * (float)deltaSeconds;
                turret.AimDegrees = Math.Clamp(next, turret.Definition.MinAimDegrees, turret.Definition.MaxAimDegrees);
            }

            if (turret.Definition.WeaponType == TurretWeaponType.Laser)
            {
                var weaponChargerPower = GetEffectivePower(PowerSystemId.WeaponCharger);
                var recharge = weaponChargerPower * turret.Definition.RechargePerPowerUnitPerSecond * (float)deltaSeconds;
                turret.Charge = Math.Min(turret.Definition.MaxCharge, turret.Charge + recharge);
            }
        }
    }
}
