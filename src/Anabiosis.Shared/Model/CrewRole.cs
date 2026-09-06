namespace Anabiosis.Shared.Model;

// The 5 crew jobs (game_design.md section 4). A hired bot (NpcKind.Recruiter, World.Recruiting.cs)
// is fixed to one for its whole time aboard - unlike a live player, who can just walk up and do any
// job, a bot only ever does its own (World.CrewAi.cs has no "notice what's needed" logic, only "do
// my job continuously").
public enum CrewRole
{
    Captain,
    Engineer,
    Mechanic,
    Security,
    // Heals the crew (the same job Medic always did) and, from the navigation console, operates
    // the scanner (M44) - the console's old "pick an autopilot destination" job became useless
    // once flight went fully manual (M39), so it's repurposed rather than adding a new block.
    Scientist,
}

public static class CrewRoles
{
    public static string Name(CrewRole role) => role switch
    {
        CrewRole.Captain => "Капитан",
        CrewRole.Engineer => "Инженер",
        CrewRole.Mechanic => "Механик",
        CrewRole.Security => "Охрана",
        _ => "Учёный",
    };
}
