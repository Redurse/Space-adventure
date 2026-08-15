using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const float TurretAimRateDegreesPerSecond = 60f;

    private static void TryFire(TurretRuntime turret)
    {
        if (turret.CooldownRemaining > 0 || turret.Damaged)
            return;

        if (turret.Definition.WeaponType == TurretWeaponType.Laser)
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
        turret.PendingShotDamage = turret.Definition.DamagePerShot;
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

            if (turret.PendingShotDamage > 0)
            {
                Enemy.ApplyDamage(turret.PendingShotDamage);
                turret.PendingShotDamage = 0;
            }

            if (turret.Definition.WeaponType == TurretWeaponType.Laser)
            {
                var weaponChargerPower = PowerGrid.GetAllocation(PowerSystemId.WeaponCharger);
                var recharge = weaponChargerPower * turret.Definition.RechargePerPowerUnitPerSecond * (float)deltaSeconds;
                turret.Charge = Math.Min(turret.Definition.MaxCharge, turret.Charge + recharge);
            }
        }
    }
}
