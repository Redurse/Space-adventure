namespace Anabiosis.Shared.Model;

// One hireable crew member on the Recruiter's board right now (game_design.md section 10 -
// "случайный набор кандидатов на каждой станции... у каждого своё имя/характеристики/
// специализация; чем круче бот, тем дороже"). Id is per-roster, not a player id - it only has to be
// unique among candidates currently on offer (World.Recruiting.cs).
public sealed record BotCandidate(string Id, string Name, CrewRole Role, int Cost);
