using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.War;

// One star system as the war sees it.
public sealed class WarSystem
{
    public int Index { get; }
    public string Id { get; }
    public string Name { get; }
    public float X { get; }
    public float Y { get; }
    // How much ore the asteroid field holds, 1 for an ordinary system: it scales what an outpost miners bring in.
    public float Richness { get; }
    public bool HasRockyPlanet { get; }
    public int PlanetCount { get; }

    public WarSystem(int index, string id, string name, float x, float y, float richness, bool hasRockyPlanet, int planetCount)
    {
        Index = index;
        Id = id;
        Name = name;
        X = x;
        Y = y;
        Richness = richness;
        HasRockyPlanet = hasRockyPlanet;
        PlanetCount = planetCount;
    }
}

public readonly record struct WarLane(int To, float Length);

// The map the war is fought on: star systems joined by hyperlanes. Ships travel only along lanes, so the lanes - not the
// jump circle the player ship uses - decide where the chokepoints and the fronts are. The lanes are built from the
// jump circle pairs but thinned to a Gabriel graph (a lane survives only if no third system lies inside the circle
// across it), which gives the sparse, mostly planar look of Stellaris hyperlanes; pairs are then put back, shortest
// first, wherever the thinning would have cut the galaxy in two.
public sealed class WarGalaxy
{
    private readonly WarSystem[] _systems;
    private readonly WarLane[][] _lanes;
    private readonly Dictionary<string, int> _indexById = new();

    // Shortest routes are not tabulated for every pair (4000 systems would be sixteen million pairs and a cubic search): a row - the
    // distance, the next step and the lane count from ONE system to all the others - is computed the first time something asks about
    // that system, and the most recently used rows are kept. The lanes are the same both ways, so a row answers for either end.
    private sealed class Row
    {
        public float[] Distance = Array.Empty<float>();
        public int[] Toward = Array.Empty<int>();   // Toward[x]: the next system to go to from x to reach the row's own system
        public int[] Hops = Array.Empty<int>();
        public long LastUsed;
    }

    private const int MaxCachedRows = 700;
    private readonly Dictionary<int, Row> _rows = new();
    private readonly int[] _asked;
    private long _clock;

    public IReadOnlyList<WarSystem> Systems => _systems;
    public int Count => _systems.Length;

    public WarGalaxy(IReadOnlyList<WarSystem> systems, IReadOnlyList<(int A, int B)> lanePairs)
    {
        _systems = systems.ToArray();
        var adjacency = new List<WarLane>[_systems.Length];
        for (var i = 0; i < adjacency.Length; i++)
            adjacency[i] = new List<WarLane>();
        foreach (var (a, b) in lanePairs)
        {
            var length = Length(_systems[a], _systems[b]);
            adjacency[a].Add(new WarLane(b, length));
            adjacency[b].Add(new WarLane(a, length));
        }
        _lanes = adjacency.Select(list => list.OrderBy(l => l.To).ToArray()).ToArray();
        foreach (var system in _systems)
            _indexById[system.Id] = system.Index;

        _asked = new int[_systems.Length];
    }

    public IReadOnlyList<WarLane> LanesOf(int system) => _lanes[system];
    public int IndexOf(string id) => _indexById[id];
    public bool TryIndexOf(string id, out int index) => _indexById.TryGetValue(id, out index);

    // Length of the lane between two neighbours; throws for two systems that are not joined.
    public float LaneLength(int a, int b)
    {
        foreach (var lane in _lanes[a])
            if (lane.To == b)
                return lane.Length;
        throw new ArgumentException($"no lane between {a} and {b}");
    }

    // Distance along the shortest route, in map units, and the number of lanes it crosses.
    public float PathLength(int from, int to) => from == to ? 0f : RowFor(from, to).Distance[RowOwner(from, to)];
    public int Hops(int from, int to) => from == to ? 0 : RowFor(from, to).Hops[RowOwner(from, to)];

    // The systems to pass through to get from one to the other, the destination included and the start left out.
    public List<int> Path(int from, int to)
    {
        var path = new List<int>();
        if (from == to)
            return path;
        // A row knows the way from anywhere to its own system, so it can be walked from the far end: the row of the destination forwards,
        // the row of the start backwards (and reversed). Whichever is kept, or is asked about more, is used.
        var row = RowFor(from, to);
        if (_ownerOf == from) // the row belongs to `to`... RowFor leaves in _ownerOf the OTHER end
        {
            var at = from;
            while (at != to)
            {
                at = row.Toward[at];
                path.Add(at);
            }
            return path;
        }
        var back = to;
        while (back != from)
        {
            path.Add(back);
            back = row.Toward[back];
        }
        path.Reverse();
        return path;
    }

    // The fewest lanes from each system to the nearest of `sources` (one sweep for all of them, int.MaxValue where none can be reached).
    public int[] HopsFromAny(IEnumerable<int> sources)
    {
        var hops = new int[_systems.Length];
        Array.Fill(hops, int.MaxValue);
        var queue = new Queue<int>();
        foreach (var source in sources)
            if (hops[source] == int.MaxValue)
            {
                hops[source] = 0;
                queue.Enqueue(source);
            }
        while (queue.Count > 0)
        {
            var at = queue.Dequeue();
            foreach (var lane in _lanes[at])
                if (hops[lane.To] == int.MaxValue)
                {
                    hops[lane.To] = hops[at] + 1;
                    queue.Enqueue(lane.To);
                }
        }
        return hops;
    }

    // Which end of a question the row belongs to: the one whose row is already kept, or else the one asked about more often (a loop over
    // every system against one fixed system then costs one row, not thousands).
    private int _ownerOf;
    private Row RowFor(int a, int b)
    {
        _asked[a]++;
        _asked[b]++;
        int owner, other;
        if (_rows.ContainsKey(a)) (owner, other) = (a, b);
        else if (_rows.ContainsKey(b)) (owner, other) = (b, a);
        else if (_asked[a] >= _asked[b]) (owner, other) = (a, b);
        else (owner, other) = (b, a);
        _ownerOf = other;
        return RowOf(owner);
    }

    // The index to read in the row RowFor just returned.
    private int RowOwner(int a, int b) => _ownerOf;

    private Row RowOf(int system)
    {
        _clock++;
        if (_rows.TryGetValue(system, out var row))
        {
            row.LastUsed = _clock;
            return row;
        }
        if (_rows.Count >= MaxCachedRows)
        {
            var oldest = _rows.OrderBy(kv => kv.Value.LastUsed).Take(MaxCachedRows / 4).Select(kv => kv.Key).ToList();
            foreach (var key in oldest)
                _rows.Remove(key);
        }
        row = Compute(system);
        row.LastUsed = _clock;
        _rows[system] = row;
        return row;
    }

    private Row Compute(int source)
    {
        var n = _systems.Length;
        var distance = new float[n];
        var toward = new int[n];
        var hops = new int[n];
        Array.Fill(distance, float.PositiveInfinity);
        Array.Fill(toward, -1);
        Array.Fill(hops, -1);
        distance[source] = 0f;
        hops[source] = 0;
        // Dijkstra from the source over the lanes; Toward[x] is the system x leaves toward (its parent in the tree grown from the source).
        var queue = new PriorityQueue<int, float>();
        queue.Enqueue(source, 0f);
        while (queue.TryDequeue(out var at, out var priority))
        {
            if (priority > distance[at])
                continue;
            foreach (var lane in _lanes[at])
            {
                var through = distance[at] + lane.Length;
                if (through < distance[lane.To])
                {
                    distance[lane.To] = through;
                    toward[lane.To] = at;
                    queue.Enqueue(lane.To, through);
                }
            }
        }
        // Lane counts: breadth-first from the source.
        var bfs = new Queue<int>();
        bfs.Enqueue(source);
        while (bfs.Count > 0)
        {
            var at = bfs.Dequeue();
            foreach (var lane in _lanes[at])
                if (hops[lane.To] < 0)
                {
                    hops[lane.To] = hops[at] + 1;
                    bfs.Enqueue(lane.To);
                }
        }
        return new Row { Distance = distance, Toward = toward, Hops = hops };
    }

    private static float Length(WarSystem a, WarSystem b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Reads the whole galaxy: positions, how rich each asteroid field is, what planets there are. The jump circle pairs
    // are the candidates for lanes.
    public static WarGalaxy FromGalaxyMap(GalaxyMap map)
    {
        var systems = new List<WarSystem>();
        foreach (var system in map.Systems)
        {
            var planets = system.Bodies.Where(b => b.MassTier is BodyMassTier.Rocky or BodyMassTier.IceGiant or BodyMassTier.GasGiant).ToList();
            systems.Add(new WarSystem(systems.Count, system.Id, system.Name, system.GalaxyX, system.GalaxyY,
                Math.Clamp(system.Field.OreDeposits.Count / 90f, 0.4f, 2.2f),
                planets.Any(b => b.MassTier == BodyMassTier.Rocky), planets.Count));
        }

        // The jump circle pairs, found through a grid of the systems rather than by testing every pair (4000 systems).
        var grid = new SystemGrid(systems, GalaxyMap.WarpJumpRadius);
        var candidates = new List<(int A, int B, float Length)>();
        var near = new List<int>();
        foreach (var system in systems)
        {
            near.Clear();
            grid.Collect(system.X, system.Y, GalaxyMap.WarpJumpRadius, near);
            near.Sort();
            foreach (var other in near)
                if (other > system.Index && Length(system, systems[other]) <= GalaxyMap.WarpJumpRadius)
                    candidates.Add((system.Index, other, Length(system, systems[other])));
        }
        return new WarGalaxy(systems, ThinToLanes(systems, candidates));
    }

    // Gabriel thinning, then the shortest dropped pairs that reconnect what the thinning disconnected.
    public static List<(int A, int B)> ThinToLanes(IReadOnlyList<WarSystem> systems, List<(int A, int B, float Length)> candidates)
    {
        var kept = new List<(int A, int B, float Length)>();
        var dropped = new List<(int A, int B, float Length)>();
        var grid = new SystemGrid(systems, GalaxyMap.WarpJumpRadius);
        var near = new List<int>();
        foreach (var pair in candidates)
        {
            var (a, b, length) = pair;
            // Only a system inside the circle that has the pair as its diameter can block the lane, so only those near its middle are tried.
            var blocked = false;
            near.Clear();
            grid.Collect((systems[a].X + systems[b].X) / 2f, (systems[a].Y + systems[b].Y) / 2f, length / 2f, near);
            foreach (var c in near)
            {
                if (c == a || c == b)
                    continue;
                var ac = Length(systems[a], systems[c]);
                var bc = Length(systems[b], systems[c]);
                if (ac * ac + bc * bc < length * length)
                {
                    blocked = true;
                    break;
                }
            }
            (blocked ? dropped : kept).Add(pair);
        }

        var parent = Enumerable.Range(0, systems.Count).ToArray();
        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }
        foreach (var (a, b, _) in kept)
            parent[Find(a)] = Find(b);
        foreach (var (a, b, length) in dropped.OrderBy(p => p.Length))
            if (Find(a) != Find(b))
            {
                kept.Add((a, b, length));
                parent[Find(a)] = Find(b);
            }
        return kept.Select(p => (p.A, p.B)).ToList();
    }
}

// A grid of cells over the systems' positions: which systems lie within a radius of a point, without looking at all of them.
internal sealed class SystemGrid
{
    private readonly float _cell;
    private readonly Dictionary<(int, int), List<int>> _cells = new();
    private readonly IReadOnlyList<WarSystem> _systems;

    public SystemGrid(IReadOnlyList<WarSystem> systems, float cell)
    {
        _systems = systems;
        _cell = cell;
        foreach (var system in systems)
        {
            var key = ((int)MathF.Floor(system.X / cell), (int)MathF.Floor(system.Y / cell));
            if (!_cells.TryGetValue(key, out var list))
                _cells[key] = list = new List<int>();
            list.Add(system.Index);
        }
    }

    // Adds every system within `radius` of the point (the cells around it are read, so a few farther ones come with them).
    public void Collect(float x, float y, float radius, List<int> into)
    {
        var reach = (int)MathF.Ceiling(radius / _cell);
        var cx = (int)MathF.Floor(x / _cell);
        var cy = (int)MathF.Floor(y / _cell);
        for (var gx = cx - reach; gx <= cx + reach; gx++)
        for (var gy = cy - reach; gy <= cy + reach; gy++)
            if (_cells.TryGetValue((gx, gy), out var list))
                into.AddRange(list);
    }
}
