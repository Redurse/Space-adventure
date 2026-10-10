using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The people who live on one station and walk around it (direct user request: "более продвинутые боты,
// которые двигаются по станции" and more of them). A plain simulation over the station's own TileGrid,
// kept apart from World so it can be tested on its own:
//
//   * civilians idle for a few seconds, pick somewhere to go in one of the station's rooms, walk there
//     along a TilePathfinder path, and repeat;
//   * guards patrol a fixed loop through the rooms, pausing at each stop;
//   * when the station is alerted (someone caught stealing / resisting arrest) civilians freeze and
//     the guards run to the nearest crew member on the station.
//
// They never enter the connector to the player's ship (forbidden tiles), do not collide with anyone, and
// are not saved - a station gets a fresh, deterministic crowd (seeded from its point id) every time it
// is first docked at in a session.
public sealed class StationResidentSim
{
    private const float MinSpeed = 1.1f;
    private const float MaxSpeed = 1.6f;
    private const float AlertGuardSpeedFactor = 1.7f;
    private const float GuardReachDistance = 2.2f;
    private const float AlertRepathSeconds = 0.8f;
    private const int MinResidents = 3;
    private const int MaxResidents = 22;
    private const float KeepClearOfJobNpcs = 1.5f;

    private static readonly string[] FirstNames =
    {
        "Игорь", "Марина", "Дмитрий", "Ольга", "Сергей", "Анна", "Виктор", "Елена",
        "Павел", "Наталья", "Артём", "Ксения", "Роман", "Алиса", "Глеб", "Вера",
    };

    private static readonly string[] LastNames =
    {
        "Волков", "Орлова", "Соколов", "Миронова", "Белов", "Лебедева", "Громов", "Фролова",
        "Тихонов", "Зайцева", "Кротов", "Нестеров",
    };

    private sealed class Resident
    {
        public required string Id;
        public required string Name;
        public required int Look;
        public required ResidentRole Role;
        public Vec2 Position;
        public float Speed;
        public List<TileCoord> Path = new();
        public int PathIndex;
        public float IdleRemaining;
        public float RepathCooldown;
        public List<TileCoord> Patrol = new();
        public int PatrolIndex;
        public bool Walking => PathIndex < Path.Count;
    }

    private readonly Station _station;
    private readonly string _pointId;
    private readonly Random _random;
    private readonly HashSet<TileCoord> _forbidden = new();
    private readonly List<TileCoord> _spots = new();
    private readonly List<Resident> _residents = new();

    public StationResidentSim(Station station, string pointId)
    {
        _station = station;
        _pointId = pointId;
        _random = new Random(StableSeed(pointId));

        // The connector tile(s) lead into the player's own ship - nobody from the station wanders in.
        var connector = station.ShipConnector;
        foreach (var coord in TileGridRasterizer.DoorTileCoords(new[] { station.GetRoom(connector.RoomAId) }, connector.X, connector.Y, connector.Width, connector.Height))
            _forbidden.Add(coord);

        // Every tile a person could stand on and walk away from: floor inside a room, not on a door, not
        // right on top of a job NPC's desk.
        foreach (var (coord, cell) in station.Tiles.Cells)
        {
            if (cell.Wall != TileWallKind.None || !TilePathfinder.IsPassable(station.Tiles, coord, _forbidden))
                continue;
            var center = TilePathfinder.CenterOf(coord);
            if (!station.Rooms.Any(r => r.Contains(center)))
                continue;
            if (station.Npcs.Any(n => (n.Position - center).Length() < KeepClearOfJobNpcs))
                continue;
            _spots.Add(coord);
        }
        // Dictionary order is not stable across runs; sort so the same station always gets the same crowd.
        _spots.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

        Populate();
    }

    public IReadOnlyList<StationResidentState> States =>
        _residents.Select(r => new StationResidentState(r.Id, r.Name, r.Look, r.Role, (float)r.Position.X, (float)r.Position.Y)).ToList();

    public int Count => _residents.Count;
    public int GuardCount => _residents.Count(r => r.Role == ResidentRole.Guard);
    public bool IsForbidden(TileCoord coord) => _forbidden.Contains(coord);

    // How many people a station of this many rooms holds - roughly one and a half per room, a small
    // outpost still feels lived in and a big hub does not turn into a crowd the machine has to pay for.
    public static int ResidentCountFor(int roomCount) => Math.Clamp((int)MathF.Round(roomCount * 1.4f), MinResidents, MaxResidents);

    private void Populate()
    {
        if (_spots.Count == 0)
            return;

        var total = ResidentCountFor(_station.Rooms.Count);
        var guards = _station.Rooms.Count >= 5 ? Math.Max(1, total / 6) : 0;
        for (var i = 0; i < total; i++)
        {
            var role = i < guards ? ResidentRole.Guard : ResidentRole.Civilian;
            var spot = _spots[_random.Next(_spots.Count)];
            var resident = new Resident
            {
                Id = $"{_pointId}-res-{i}",
                Name = $"{FirstNames[_random.Next(FirstNames.Length)]} {LastNames[_random.Next(LastNames.Length)]}",
                Look = _random.Next(1000),
                Role = role,
                Position = TilePathfinder.CenterOf(spot) + new Vec2(_random.NextDouble() * 0.3 - 0.15, _random.NextDouble() * 0.3 - 0.15),
                Speed = MinSpeed + (float)_random.NextDouble() * (MaxSpeed - MinSpeed),
                IdleRemaining = (float)_random.NextDouble() * 6f,
            };
            if (role == ResidentRole.Guard)
                resident.Patrol = BuildPatrolRoute();
            _residents.Add(resident);
        }
    }

    // One stop per room (the tile nearest its centre), in a shuffled order so two guards do not walk the
    // same loop in step.
    private List<TileCoord> BuildPatrolRoute()
    {
        var stops = new List<TileCoord>();
        foreach (var room in _station.Rooms)
        {
            var centre = TilePathfinder.TileAt(new Vec2(room.X + room.Width / 2f, room.Y + room.Height / 2f));
            if (TilePathfinder.NearestPassable(_station.Tiles, centre, _forbidden, 4) is { } stop)
                stops.Add(stop);
        }
        for (var i = stops.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (stops[i], stops[j]) = (stops[j], stops[i]);
        }
        return stops;
    }

    // alerted: someone on the station is being hunted. crew: where the people on the station stand.
    public void Step(double deltaSeconds, bool alerted, IReadOnlyList<Vec2> crew)
    {
        var dt = (float)deltaSeconds;
        foreach (var resident in _residents)
        {
            if (alerted && resident.Role == ResidentRole.Civilian)
            {
                resident.Path.Clear(); // freeze - nobody wants to be in the way
                resident.PathIndex = 0;
                continue;
            }
            if (alerted && crew.Count > 0)
            {
                StepGuardRushing(resident, dt, crew);
                continue;
            }

            if (resident.Walking)
            {
                Advance(resident, dt, resident.Speed);
                if (!resident.Walking)
                    resident.IdleRemaining = 2f + (float)_random.NextDouble() * 9f;
                continue;
            }

            resident.IdleRemaining -= dt;
            if (resident.IdleRemaining > 0f)
                continue;
            ChooseNextDestination(resident);
        }
    }

    private void StepGuardRushing(Resident guard, float dt, IReadOnlyList<Vec2> crew)
    {
        var target = crew.OrderBy(c => (c - guard.Position).Length()).First();
        if ((target - guard.Position).Length() <= GuardReachDistance)
        {
            guard.Path.Clear();
            guard.PathIndex = 0;
            return;
        }

        guard.RepathCooldown -= dt;
        if (guard.RepathCooldown <= 0f || !guard.Walking)
        {
            guard.RepathCooldown = AlertRepathSeconds;
            if (TilePathfinder.NearestPassable(_station.Tiles, TilePathfinder.TileAt(target), _forbidden, 3) is { } goal)
                SetPath(guard, goal);
        }
        if (guard.Walking)
            Advance(guard, dt, guard.Speed * AlertGuardSpeedFactor);
    }

    private void ChooseNextDestination(Resident resident)
    {
        if (resident.Role == ResidentRole.Guard && resident.Patrol.Count > 0)
        {
            resident.PatrolIndex = (resident.PatrolIndex + 1) % resident.Patrol.Count;
            if (!SetPath(resident, resident.Patrol[resident.PatrolIndex]))
                resident.IdleRemaining = 2f;
            return;
        }

        // A few random candidates; take the first one that is worth walking to and actually reachable.
        var from = TilePathfinder.TileAt(resident.Position);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var goal = _spots[_random.Next(_spots.Count)];
            if (Math.Abs(goal.X - from.X) + Math.Abs(goal.Y - from.Y) < 4)
                continue;
            if (SetPath(resident, goal))
                return;
        }
        resident.IdleRemaining = 3f + (float)_random.NextDouble() * 5f;
    }

    private bool SetPath(Resident resident, TileCoord goal)
    {
        var path = TilePathfinder.FindPath(_station.Tiles, TilePathfinder.TileAt(resident.Position), goal, _forbidden);
        if (path is null || path.Count == 0)
            return false;
        resident.Path = path;
        resident.PathIndex = 0;
        return true;
    }

    // Walks `distance = speed * dt` along the path, tile centre to tile centre.
    private static void Advance(Resident resident, float dt, float speed)
    {
        var remaining = speed * dt;
        while (remaining > 0f && resident.Walking)
        {
            var target = TilePathfinder.CenterOf(resident.Path[resident.PathIndex]);
            var toTarget = target - resident.Position;
            var distance = (float)toTarget.Length();
            if (distance <= remaining)
            {
                resident.Position = target;
                remaining -= distance;
                resident.PathIndex++;
            }
            else
            {
                resident.Position += toTarget * (remaining / distance);
                remaining = 0f;
            }
        }
    }

    // string.GetHashCode is randomised per process in .NET, so a station's crowd would change from run to
    // run - FNV-1a instead.
    private static int StableSeed(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in text)
                hash = (hash ^ c) * 16777619;
            return hash;
        }
    }
}
