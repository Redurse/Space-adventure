using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// The people aboard a hostile ship actually live on it: each walks the hull's tiles (TilePathfinder) between their post
// and whatever their job asks for, within their role - the captain stays at the helm, scientists man the guns and tend
// the wounded, engineers patch the hull, fighters go for boarders. Everyone flees a compartment that has lost its air.
public sealed partial class World
{
    private const float EnemyCrewWalkSpeed = 2.4f;        // tiles per second
    private const float EnemyCrewDecisionInterval = 0.5f;
    private const float EnemyCrewWorkReach = 2.0f;        // how close to a wall block / patient to work on it
    private const float EnemyCrewRepairPerSecond = 16f;   // wall block hit points welded back per second
    private const float EnemyCrewHealPerSecond = 6f;
    private const float EnemyCrewPostReach = 1.2f;        // within this of the post counts as "at the post"
    private const float EnemyCrewHelmReach = 2.5f;        // the helm console is a device the captain stands beside
    private const float EnemyCrewHurtFraction = 0.9f;     // below this share of full health someone is worth healing

    private void StepEnemyCrews(double deltaSeconds)
    {
        foreach (var enemy in _enemyShips)
            if (enemy.Alive)
                StepEnemyCrew(enemy, (float)deltaSeconds);
    }

    private void StepEnemyCrew(EnemyShipRuntime enemy, float dt)
    {
        var boarders = ReferenceEquals(enemy, BoardableEnemy)
            ? _characters.Values.Where(c => c.OnEnemyShip && c.Health > 0).ToList()
            : new List<Character>();

        foreach (var crew in enemy.Crew)
        {
            if (!crew.Alive)
                continue;

            crew.DecisionCooldown -= dt;
            if (crew.DecisionCooldown <= 0f)
            {
                crew.DecisionCooldown = EnemyCrewDecisionInterval;
                Decide(enemy, crew, boarders);
            }

            Walk(enemy, crew, dt);
            Work(enemy, crew, dt);
        }
    }

    // Picks the activity and the tile to walk to. Priorities: get out of thin air, then the role's own job, then the post.
    private void Decide(EnemyShipRuntime enemy, EnemyCrewRuntime crew, List<Character> boarders)
    {
        var tiles = enemy.Layout.Tiles;

        if (!crew.Spawn.Suited && enemy.RoomOxygen.GetValueOrDefault(crew.RoomId, FullOxygen) < OxygenSafeThreshold
            && FindSafeTile(enemy, crew) is { } safe)
        {
            crew.Activity = EnemyCrewActivity.Fleeing;
            GoTo(enemy, crew, safe);
            return;
        }

        switch (crew.Spawn.Role)
        {
            case EnemyCrewRole.Fighter:
                if (boarders.Count > 0)
                {
                    var prey = boarders.OrderBy(b => (b.Position - crew.Position).Length()).First();
                    crew.Activity = EnemyCrewActivity.Fighting;
                    var closeEnough = prey.RoomId == crew.RoomId &&
                        (prey.Position - crew.Position).Length() <= Math.Min(WeaponDefinitions.Range(crew.Spawn.Weapon) * 0.7f, 4f);
                    if (closeEnough)
                        StopWalking(crew);
                    else
                        GoTo(enemy, crew, TilePathfinder.NearestPassable(tiles, TilePathfinder.TileAt(prey.Position)));
                    return;
                }
                break;

            case EnemyCrewRole.Engineer:
                crew.Job = FindRepairJob(enemy, crew);
                if (crew.Job is { } job)
                {
                    crew.Activity = EnemyCrewActivity.Repairing;
                    if ((job.Position - crew.Position).Length() <= EnemyCrewWorkReach)
                        StopWalking(crew);
                    else
                        GoTo(enemy, crew, FindWorkTile(enemy, crew, job.Position));
                    return;
                }
                break;

            case EnemyCrewRole.Scientist:
                if (FindPatient(enemy, crew) is { } patient)
                {
                    crew.Activity = EnemyCrewActivity.Healing;
                    if ((patient.Position - crew.Position).Length() <= EnemyCrewWorkReach)
                        StopWalking(crew);
                    else
                        GoTo(enemy, crew, TilePathfinder.NearestPassable(tiles, TilePathfinder.TileAt(patient.Position)));
                    return;
                }
                break;
        }

        // Nothing to do: back to the post (the captain's helm, a scientist's periscope, an engineer's bench...).
        if ((crew.Spawn.Position - crew.Position).Length() > 0.3f)
        {
            crew.Activity = EnemyCrewActivity.Walking;
            GoTo(enemy, crew, TilePathfinder.TileAt(crew.Spawn.Position));
        }
        else
        {
            crew.Activity = EnemyCrewActivity.AtPost;
            StopWalking(crew);
        }
    }

    // A tile to stand on to work on something at `target`: within reach of it AND actually reachable by walking (the nearest
    // floor can be on the far side of a wall).
    private static TileCoord? FindWorkTile(EnemyShipRuntime enemy, EnemyCrewRuntime crew, Vec2 target)
    {
        var tiles = enemy.Layout.Tiles;
        var origin = TilePathfinder.TileAt(target);
        var start = TilePathfinder.TileAt(crew.Position);
        var candidates = new List<TileCoord>();
        for (var dx = -2; dx <= 2; dx++)
            for (var dy = -2; dy <= 2; dy++)
            {
                var tile = new TileCoord(origin.X + dx, origin.Y + dy);
                if (TilePathfinder.IsPassable(tiles, tile) && (TilePathfinder.CenterOf(tile) - target).Length() <= EnemyCrewWorkReach)
                    candidates.Add(tile);
            }
        foreach (var tile in candidates.OrderBy(t => (TilePathfinder.CenterOf(t) - target).Length()))
            if (tile == start || TilePathfinder.FindPath(tiles, start, tile) is not null)
                return tile;
        return null;
    }

    private static void GoTo(EnemyShipRuntime enemy, EnemyCrewRuntime crew, TileCoord? goal)
    {
        if (goal is not { } target)
        {
            StopWalking(crew);
            return;
        }
        if (crew.GoalTile == target && crew.Path.Count > 0)
            return;

        crew.GoalTile = target;
        crew.Path.Clear();
        if (TilePathfinder.FindPath(enemy.Layout.Tiles, TilePathfinder.TileAt(crew.Position), target) is { } path)
            crew.Path.AddRange(path);
    }

    private static void StopWalking(EnemyCrewRuntime crew)
    {
        crew.Path.Clear();
        crew.GoalTile = null;
    }

    private static void Walk(EnemyShipRuntime enemy, EnemyCrewRuntime crew, float dt)
    {
        var budget = EnemyCrewWalkSpeed * dt;
        while (budget > 0f && crew.Path.Count > 0)
        {
            var target = TilePathfinder.CenterOf(crew.Path[0]);
            var delta = target - crew.Position;
            var distance = (float)delta.Length();
            if (distance <= budget)
            {
                crew.Position = target;
                budget -= distance;
                crew.Path.RemoveAt(0);
            }
            else
            {
                crew.Position += delta.Normalized() * budget;
                budget = 0f;
            }
        }
        if (crew.Path.Count == 0)
            crew.GoalTile = null;

        crew.RoomId = TileMovement.RoomIdAt(enemy.Layout.Rooms, crew.Position) ?? crew.RoomId;
    }

    // The part of the job that happens while standing still next to the work.
    private static void Work(EnemyShipRuntime enemy, EnemyCrewRuntime crew, float dt)
    {
        switch (crew.Activity)
        {
            case EnemyCrewActivity.Repairing:
                if (crew.Job is { } job && (job.Position - crew.Position).Length() <= EnemyCrewWorkReach)
                    WorkOn(enemy, job, dt);
                break;

            case EnemyCrewActivity.Healing:
                if (FindPatient(enemy, crew) is { } patient && (patient.Position - crew.Position).Length() <= EnemyCrewWorkReach)
                    patient.Health = Math.Min(EnemyCrewRuntime.MaxHealth, patient.Health + EnemyCrewHealPerSecond * dt);
                break;
        }
    }

    // Someone worth healing - and, for a healer without a suit, one standing where there is air to breathe.
    private static EnemyCrewRuntime? FindPatient(EnemyShipRuntime enemy, EnemyCrewRuntime healer) =>
        enemy.Crew
            .Where(c => c != healer && c.Alive && c.Health < EnemyCrewRuntime.MaxHealth * EnemyCrewHurtFraction)
            .Where(c => healer.Spawn.Suited || enemy.RoomOxygen.GetValueOrDefault(c.RoomId, FullOxygen) >= OxygenSafeThreshold)
            .OrderBy(c => (c.Position - healer.Position).Length())
            .FirstOrDefault();

    // The nearest walkable tile (by straight line, then confirmed reachable) in a compartment that still has air.
    private static TileCoord? FindSafeTile(EnemyShipRuntime enemy, EnemyCrewRuntime crew)
    {
        var safeRooms = enemy.Layout.Rooms
            .Where(r => enemy.RoomOxygen.GetValueOrDefault(r.Id, FullOxygen) >= OxygenSafeThreshold)
            .OrderBy(r => (r.Center - crew.Position).Length())
            .Take(4);
        foreach (var room in safeRooms)
            if (TilePathfinder.NearestPassable(enemy.Layout.Tiles, TilePathfinder.TileAt(room.Center)) is { } tile
                && TilePathfinder.FindPath(enemy.Layout.Tiles, TilePathfinder.TileAt(crew.Position), tile) is not null)
                return tile;
        return null;
    }

    // Test precondition: hurts one of the boardable hull's crew directly, taking the ship if that was the last one standing.
    public void DebugDamageEnemyCrew(string crewId, float amount)
    {
        if (BoardableEnemy is not { } enemy || enemy.Crew.FirstOrDefault(c => c.Spawn.Id == crewId) is not { } crew)
            return;
        crew.Health = Math.Max(0f, crew.Health - amount);
        CaptureIfCrewWiped(enemy);
    }

    // A gun fires only while its scientist (the one posted to this turret) is alive and standing at the periscope.
    private static bool IsEnemyTurretManned(EnemyShipRuntime enemy, string turretId) =>
        enemy.Crew.Any(c => c.Alive && c.PostId == turretId && (c.Spawn.Position - c.Position).Length() <= EnemyCrewPostReach);

    private const float EnemyDriftControl = 0.2f;        // even with no control the ship bleeds speed and slowly turns
    private const float EnemyUnmannedHelmFactor = 0.25f; // nobody at the helm: the ship is only barely steered

    // 0..1: how much of its flying ability a hostile ship has left - the share of its engine thrust still working, times
    // whether the captain is at the helm.
    private static float EnemyControlFactor(EnemyShipRuntime enemy)
    {
        if (enemy.IsExploding)
            return 0f;
        var working = enemy.Layout.Ship.Engines
            .Where(e => e.Role == EngineRole.Marching && enemy.IsEngineRunning(e.Id))
            .Sum(e => e.MaxThrust);
        var share = enemy.InitialThrust <= 0f ? 1f : Math.Clamp(working / enemy.InitialThrust, 0f, 1f);
        return share * (IsEnemyHelmManned(enemy) && !enemy.HelmBroken ? 1f : EnemyUnmannedHelmFactor);
    }

    // The captain steers: alive and standing at the helm. Without them the ship flies on barely under control.
    private static bool IsEnemyHelmManned(EnemyShipRuntime enemy) =>
        enemy.Crew.Any(c => c.Alive && c.Spawn.Role == EnemyCrewRole.Captain
            && (enemy.Layout.Ship.HelmConsole.Position - c.Position).Length() <= EnemyCrewHelmReach);
}
