namespace Anabiosis.Shared.Model;

// The weapons skill, after Barotrauma: a number from 0 to 100 that a crew member starts with by job and that
// grows by hurting hostiles. A gun asks for a certain skill (GunSpec.RequiredSkill); how far a shooter is above
// or below it decides how much of the gun's unskilled spread comes in (GunSpec.SpreadFor). The starting numbers
// are the middle of each job's range in its job list (security 40-50, captain 25-35, everyone else 15-25); the
// growth rate is its own setting for damage dealt to hostiles.
public static class WeaponSkills
{
    public const float Max = 100f;
    public const float GainPerHostileDamage = 0.25f;

    public static float Starting(CrewRole role) => role switch
    {
        CrewRole.Security => 45f,
        CrewRole.Captain => 30f,
        _ => 20f,
    };

    public static float Starting(EnemyCrewRole role) => role switch
    {
        EnemyCrewRole.Fighter => 45f,
        EnemyCrewRole.Captain => 30f,
        _ => 20f,
    };

    // 0.5 when the skill is exactly what the gun asks for, 1 at a hundred points above, 0 at a hundred below.
    public static float DegreeOfSuccess(float skill, float required) => Math.Clamp((skill - required + 100f) / 200f, 0f, 1f);

    // Skill earned for damage done to a hostile: the more skilled the shooter already is, the slower it comes.
    public static float GainFor(float damage, float skill) => GainPerHostileDamage * damage / Math.Max(skill, 1f);
}
