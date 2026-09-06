namespace Anabiosis.Shared.Protocol;

// Whether a station crate has already been lifted (game_design.md section 10) - the layout itself
// is static, only this flips.
public sealed record StationCrateState(string CrateId, bool Looted);

// A station guard's condition. Alerted is station-wide rather than per-guard: shooting one puts
// them all on alert (World.StationCrime.cs).
public sealed record StationGuardState(string NpcId, float Health, float MaxHealth, bool Alerted)
{
    public bool Alive => Health > 0;
}
