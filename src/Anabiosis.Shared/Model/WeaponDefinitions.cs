namespace Anabiosis.Shared.Model;

// Personal weapons (game_design.md section 12, Phase 3 - boarding). The firearms are described in
// GunDefinitions.cs, with Barotrauma numbers; this is the common face of every weapon - guns, the knife
// and the laser rifle - for the code that only asks "is it a weapon, how hard, how far, how often".
// Range is in the same world units as room geometry: the knife is strictly melee, firearms reach
// across a room but not through walls (a round dies at the edge of the compartment it was fired in).
public static class WeaponDefinitions
{
    public static bool IsWeapon(ItemType type) => DamagePerHit(type) > 0f;

    // Anything fired with the trigger (right mouse): every gun, and the laser rifle.
    public static bool IsFirearm(ItemType type) => GunDefinitions.Of(type) is not null || IsBeam(type);

    // Keeps firing for as long as the trigger is held.
    public static bool IsAutomatic(ItemType type) => GunDefinitions.Of(type) is { Automatic: true };
    // The laser rifle fires an instant beam rather than a bolt, and needs no magazine.
    public static bool IsBeam(ItemType type) => type == ItemType.LaserRifle;

    // For a gun: the wound one round (one pellet of a shell) leaves.
    public static float DamagePerHit(ItemType type) => type switch
    {
        ItemType.Knife => 18f,
        ItemType.LaserRifle => 34f,
        _ => GunDefinitions.Of(type)?.HitWound ?? 0f,
    };

    public static float Range(ItemType type) => type switch
    {
        ItemType.Knife => 1.0f, // melee only - has to be right next to the target
        ItemType.LaserRifle => 8f,
        _ => GunDefinitions.Of(type)?.Range ?? 0f,
    };

    // The shortest time between two shots (a trained hand, for the guns that care - GunSpec.ReloadFor).
    public static float CooldownSeconds(ItemType type) => type switch
    {
        ItemType.Knife => 0.8f,
        ItemType.LaserRifle => 0.9f, // slowest but hardest hitting
        _ => GunDefinitions.Of(type)?.ReloadSeconds ?? 0f,
    };
}
