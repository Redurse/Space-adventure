namespace Anabiosis.Shared.Model;

// Personal weapons finally get a gameplay effect (game_design.md section 12, Phase 3 - boarding).
// Until now Knife/Rifle/LaserRifle existed only as carryable ItemTypes with no combat use at all.
// Range is in the same world units as room geometry: the knife is strictly melee, firearms reach
// across a room but not through walls (World.Boarding.cs checks same-room as well as distance).
public static class WeaponDefinitions
{
    public static bool IsWeapon(ItemType type) => DamagePerHit(type) > 0f;

    // The rifle fires bursts: a stream of BurstRounds bullets, BurstIntervalSeconds apart, for one pull of the trigger.
    public const int BurstRounds = 6;
    public const float BurstIntervalSeconds = 0.06f;
    public static bool IsAutomatic(ItemType type) => type == ItemType.Rifle;
    // The laser rifle fires an instant beam rather than a bolt, and needs no magazine.
    public static bool IsBeam(ItemType type) => type == ItemType.LaserRifle;

    public static float DamagePerHit(ItemType type) => type switch
    {
        ItemType.Knife => 18f,
        ItemType.Rifle => 26f,
        ItemType.LaserRifle => 34f,
        _ => 0f,
    };

    public static float Range(ItemType type) => type switch
    {
        ItemType.Knife => 1.0f, // melee only - has to be right next to the target
        ItemType.Rifle => 6f,
        ItemType.LaserRifle => 8f,
        _ => 0f,
    };

    public static float CooldownSeconds(ItemType type) => type switch
    {
        ItemType.Knife => 0.8f,
        ItemType.Rifle => BurstRounds * BurstIntervalSeconds + 0.25f, // one burst, then a short breath before the next
        ItemType.LaserRifle => 0.9f, // slowest but hardest hitting
        _ => 0f,
    };
}
