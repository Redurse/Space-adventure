using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Hiring bot crew from the station's Recruiter (game_design.md section 10 - "случайный набор
// кандидатов на каждой станции..., у каждого своё имя/характеристики/специализация; чем круче бот,
// тем дороже"). A hired bot is an ordinary Character (World.CrewAi.cs drives it every tick) parked
// permanently at its role's post rather than a separate kind of entity - it never walks, since
// nothing in this codebase does pathfinding (World.EnemyAi.cs's "AI" is dice rolls, not navigation),
// and standing at the post it does its job at is indistinguishable from having walked there and
// stayed.
public sealed partial class World
{
    public const int MaxHiredBots = 4;

    private static readonly string[] BotFirstNames =
    {
        "Игорь", "Марина", "Дмитрий", "Ольга", "Сергей", "Анна", "Виктор", "Елена",
        "Павел", "Наталья", "Артём", "Ксения",
    };

    private static int RoleBaseCost(CrewRole role) => role switch
    {
        CrewRole.Captain => 220,
        CrewRole.Security => 180,
        CrewRole.Engineer => 160,
        CrewRole.Mechanic => 150,
        _ => 130, // Medic
    };

    private List<BotCandidate> _recruitRoster = new();
    private int _nextBotId; // decrements: -1, -2, ... - never collides with GameServer's 1, 2, ...

    // Rerolled on every docking (World.Voyage.cs's EnterStation) - the same "new visit, new board"
    // convention the Trader/Shipwright's stock already follows. Only a station with a Recruiter
    // offers anyone; the rest show an empty board, same as an Outpost with no Shipwright sells no
    // hulls.
    private void RegenerateRecruitRoster()
    {
        _recruitRoster = new List<BotCandidate>();
        if (Station.Npcs.All(n => n.Kind != NpcKind.Recruiter))
            return;

        var roles = Enum.GetValues<CrewRole>().OrderBy(_ => _random.Next()).Take(3).ToList();
        foreach (var role in roles)
        {
            // A quality tier the price scales with, per game_design.md's "чем круче бот, тем
            // дороже" - it's the only thing distinguishing two candidates for the same job.
            var qualityPercent = 100 + _random.Next(0, 3) * 50; // 100/150/200%
            var name = BotFirstNames[_random.Next(BotFirstNames.Length)];
            var id = $"candidate-{Tick}-{role}-{_recruitRoster.Count}";
            var cost = RoleBaseCost(role) * qualityPercent / 100;
            _recruitRoster.Add(new BotCandidate(id, name, role, cost));
        }
    }

    private void TryHireCandidate(string? candidateId)
    {
        if (candidateId is null || Phase != VoyagePhase.Station)
            return;

        if (IsHostileHere)
            return;

        if (_characters.Values.Count(c => c.IsBot) >= MaxHiredBots)
            return;

        if (_recruitRoster.FirstOrDefault(c => c.Id == candidateId) is not { } candidate)
            return;

        if (Credits < candidate.Cost)
            return;

        Credits -= candidate.Cost;
        _recruitRoster.Remove(candidate);

        var botId = --_nextBotId;
        var (position, roomId) = CrewPostFor(candidate.Role);
        _characters[botId] = new Character(botId, position, roomId)
        {
            IsBot = true,
            BotName = candidate.Name,
            Role = candidate.Role,
        };

        if (candidate.Role == CrewRole.Security)
            TryAssignSecurityBotToTurret(_characters[botId]);
    }

    // A Security bot claims whichever turret is free the moment it's hired, and keeps it forever
    // (World.CrewAi.cs never releases one) - if every gun is already spoken for it just stands by
    // its assigned periscope, unmanned, until StepCrewBots finds an opening.
    private void TryAssignSecurityBotToTurret(Character bot)
    {
        var free = Ship.Turrets.FirstOrDefault(t => _turretRuntimes[t.Id].MannedByPlayerId is null);
        if (free is null)
            return;

        _turretRuntimes[free.Id].MannedByPlayerId = bot.PlayerId;
        bot.ManningTurretId = free.Id;
        bot.Position = free.PeriscopePosition;
        bot.RoomId = free.RoomId;
    }

    // Where a role's bot stands for the rest of the game - the same fixture a live player would
    // walk up to and use for that job (World.Interact.cs), so it reads as "someone is already at
    // their post" rather than a body dropped in the corridor.
    private (Vec2 Position, string RoomId) CrewPostFor(CrewRole role) => role switch
    {
        CrewRole.Captain => (Ship.HelmConsole.Position, Ship.HelmConsole.RoomId),
        CrewRole.Engineer => (Ship.DistributionBlock.Position, Ship.DistributionBlock.RoomId),
        CrewRole.Mechanic => (Ship.ReactorBlock.Position, Ship.ReactorBlock.RoomId),
        CrewRole.Security => Ship.Turrets.Count > 0
            ? (Ship.Turrets[0].PeriscopePosition, Ship.Turrets[0].RoomId)
            : (Ship.SpawnPoint, Ship.SpawnRoomId),
        _ => (Ship.SpawnPoint, Ship.SpawnRoomId), // Medic: no sickbay fixture exists, tends the crew from wherever it stands
    };
}
