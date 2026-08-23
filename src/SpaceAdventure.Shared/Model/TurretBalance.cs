namespace SpaceAdventure.Shared.Model;

// Shared stat numbers for the three turret weapons (enemy/weapon overhaul), so every hull's turret
// loadout (Ship.Corvette.cs, Ship.Scout.cs, Ship.Cruiser.cs, Ship.Custom.cs) and the enemy squadron
// (World.EnemyFleet.cs) draw from one place instead of repeating magic numbers per hull.
public static class TurretBalance
{
    // Magnetic cannon - real fast, big magazine, low damage per hit (game_design.md: "стреляет
    // реально быстро... боезапас на 200 снарядов но и наносит она мало урона").
    public const float MagneticDamage = 3f;
    public const float MagneticCooldownSeconds = 0.1f;
    public const int MagneticMagazineCapacity = 200;

    // Laser - a continuous beam rather than discrete shots: World.Combat.cs's TryFire is called
    // every tick the trigger is held, so CooldownSeconds here is really "how often the beam ticks",
    // not a rate-of-fire in the usual sense. MaxCharge/ChargePerShot/RechargePerPowerUnitPerSecond
    // are reused as a heat pool instead of ammo: firing for the full 3 seconds
    // (MaxCharge / ChargePerShot * LaserTickIntervalSeconds = 30 * 0.1 = 3s) drains it to 0
    // ("перегревается"), and it takes up to 5 seconds fully idle to recover
    // (MaxCharge / RechargePerPowerUnitPerSecond = 30 / 6 = 5s at full WeaponCharger power) -
    // proportionally less if it was only partially drained, which is what makes the cooldown
    // partial rather than a flat tax regardless of how long the beam actually fired.
    public const float LaserDamagePerTick = 3f;
    public const float LaserTickIntervalSeconds = 0.1f;
    public const float LaserMaxCharge = 30f;
    public const float LaserChargePerTick = 1f;
    public const float LaserRechargePerPowerUnitPerSecond = 6f;

    // Machine gun - a burst of small, individually-traced pellets per trigger-pull, good against
    // soft/small targets and cheap on hull damage (game_design.md: "стреляет кучей мелких патронов,
    // идеально против ракет и торпед и против людей в космосе, но мало наносит урона кораблю").
    public const float MachineGunDamagePerPellet = 2f;
    public const float MachineGunCooldownSeconds = 0.2f;
    public const int MachineGunMagazineCapacity = 300;
    public const int MachineGunPelletsPerBurst = 5;
    public const float MachineGunPelletSpreadDegrees = 8f;

    // Enemy fire against the PLAYER's own wall blocks (World.EnemyAi.cs's ApplyEnemyAttack) -
    // separate from the numbers above, which are the player's own turrets shooting at EnemyShip's
    // abstract 100-Hp pool. Raiders fire far less often than a player holding a trigger down
    // (World.EnemyFleet.cs's fire intervals), so these are tuned per-hit against WallBlockMaxHp(100)
    // rather than per-tick/per-burst: a magnetic cannon chews through a wall over several quick
    // weak hits, a laser can punch one clean through, a machine gun pellet barely dents it (matches
    // each weapon's own flavor - "наносит она мало урона"/"мощный луч"/"мало наносит урона кораблю").
    public const float EnemyMagneticWallDamage = 25f;
    public const float EnemyLaserWallDamage = 50f;
    public const float EnemyMachineGunWallDamage = 10f;
}
