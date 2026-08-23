using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const float TurretAimRateDegreesPerSecond = 60f;

    // Pulling the trigger now puts a shell in the field instead of subtracting HP from an abstract
    // enemy: it leaves the muzzle outside the hull (TurretMount), flies the way the barrel is
    // pointing, and hits whatever it runs into - which may well be nothing.
    //
    // Called every tick the trigger is held (World.cs's HandleCommand, FireHeld), not once per
    // press - CooldownRemaining is what actually paces the shots, which is what turns "hold to
    // fire" into each weapon's own rate of fire: fast for the magnetic cannon, tick-by-tick for the
    // laser's beam (see TurretBalance's own doc comment), burst-by-burst for the machine gun.
    private void TryFire(TurretRuntime turret)
    {
        if (turret.CooldownRemaining > 0 || turret.Damaged)
            return;

        var definition = turret.Definition;
        var isLaser = definition.WeaponType == TurretWeaponType.Laser;
        var pellets = definition.WeaponType == TurretWeaponType.MachineGun
            ? Math.Max(1, definition.PelletsPerBurst)
            : 1;

        if (isLaser)
        {
            if (turret.Charge < definition.ChargePerShot)
                return;
            turret.Charge -= definition.ChargePerShot;
        }
        else
        {
            if (turret.AmmoRemaining < pellets)
                return;
            turret.AmmoRemaining -= pellets;
        }

        turret.CooldownRemaining = definition.CooldownSeconds;

        var mount = TurretMount.For(Ship.Rooms, Ship.Turrets, definition);
        var (hullLocalCenter, _) = GetHullLocalBounds();

        for (var i = 0; i < pellets; i++)
        {
            // Each pellet in a machine-gun burst gets its own small random jitter and is traced
            // independently through the hit-geometry (World.EnemyAi.cs's ApplyEnemyAttack) - a
            // spray, not one shot standing in for the whole burst. Every other weapon fires exactly
            // one pellet with no jitter, so this loop is a no-op wrapper for them.
            var jitterDegrees = pellets > 1 && definition.PelletSpreadDegrees > 0f
                ? ((float)_random.NextDouble() * 2f - 1f) * definition.PelletSpreadDegrees
                : 0f;

            // The mount is laid out in ship-local coordinates like every other fixture; rotating
            // both the muzzle offset and the shot's heading by the hull's attitude is what puts it
            // in field space, so a turret on a ship standing on its ear still fires out of its own
            // barrel.
            var localMuzzle = mount.Muzzle(turret.AimDegrees);
            var origin = _shipFieldPosition + RotateLocalToWorld(localMuzzle - hullLocalCenter, _shipRotationDegrees);
            var direction = RotateLocalToWorld(mount.FireDirection(turret.AimDegrees + jitterDegrees), _shipRotationDegrees);

            // WeaponDamageBonus is the station Mechanic's weapon-damage upgrade (World.Upgrades.cs,
            // game_design.md section 9, M13) — applies to every turret, not per-turret leveling.
            SpawnProjectile(origin, direction, fromEnemy: false, isLaser,
                damage: definition.DamagePerShot + WeaponDamageBonus);
        }
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
