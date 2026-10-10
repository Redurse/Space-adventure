using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// The damage state of a hostile hull beyond its wall blocks: engines, guns and the reactor take hits as pools of hit
// points, and the whole thing can be dying (the reactor going up) or losing compartments one after another.
public sealed partial class EnemyShipRuntime
{
    internal const float TurretMaxHp = 120f;
    internal const float ReactorMaxHp = 200f;

    private readonly Dictionary<string, float> _fixtureHp = new();

    // A pool by id; engines use "<id>:bulkhead" and "<id>:nozzle" (World.EnginePartMaxHp each, like the player's).
    internal float FixtureHp(string id, float max) => _fixtureHp.GetValueOrDefault(id, max);

    internal void DamageFixture(string id, float max, float amount) =>
        _fixtureHp[id] = Math.Max(0f, FixtureHp(id, max) - amount);

    internal float EngineBulkheadHp(string engineId) => FixtureHp(engineId + ":bulkhead", World.EnginePartMaxHp);
    internal float EngineNozzleHp(string engineId) => FixtureHp(engineId + ":nozzle", World.EnginePartMaxHp);
    internal bool IsEngineWorking(string engineId) => EngineBulkheadHp(engineId) > 0f && EngineNozzleHp(engineId) > 0f;
    internal bool IsTurretWorking(string turretId) => FixtureHp(turretId, TurretMaxHp) > 0f;
    internal void RepairFixture(string id, float max, float amount) => _fixtureHp[id] = Math.Min(max, FixtureHp(id, max) + amount);

    // The full-power thrust of the hull's marching engines when it was whole - flight ability is measured against it.
    public float InitialThrust { get; }

    // How many compartments the hull started with - the integrity readout is measured against it.
    public int InitialRoomCount { get; }
    // Bumped every time the hull is rebuilt (clients redraw it from the new definition).
    public int LayoutVersion { get; private set; } = System.Threading.Interlocked.Increment(ref s_layoutVersions);
    private static int s_layoutVersions;

    // Compartments that are lost (or queued to be) and so must not be blown up twice, and the ones waiting their turn in a
    // chain of explosions.
    internal HashSet<string> DoomedRooms { get; } = new();
    internal List<(string RoomId, float Delay)> PendingDestructions { get; } = new();

    // The reactor going up: rooms blow one after another from the reactor outward, then the ship is gone.
    internal sealed class Explosion
    {
        public float Elapsed;
        public required List<(Room Room, float At, bool Fired)> Rooms;
    }
    internal Explosion? ShipExplosion { get; set; }
    public bool IsExploding => ShipExplosion is not null;

    private float? _hullRadius;
    // A circle that surely encloses the hull (the true minimal circle around its bounding box, plus a margin).
    public float HullRadius
    {
        get
        {
            if (_hullRadius is { } cached)
                return cached;
            var (_, half) = Layout.GetLocalBounds();
            return (_hullRadius = (float)Math.Sqrt(half.X * half.X + half.Y * half.Y) + 0.5f).Value;
        }
    }

    // Swaps in the hull after it lost compartments: everything keyed by room is reconciled to what is left.
    internal void ReplaceLayout(EnemyShipLayout layout)
    {
        var idMap = FixtureIdMap(Layout.Ship, layout.Ship);
        Layout = layout;
        _hullRadius = null;
        LayoutVersion = System.Threading.Interlocked.Increment(ref s_layoutVersions);
        RemapFixtureIds(idMap);
        BuildPowerGraph();

        var rooms = layout.Rooms.Select(r => r.Id).ToHashSet();
        foreach (var gone in RoomOxygen.Keys.Where(k => !rooms.Contains(k)).ToList())
            RoomOxygen.Remove(gone);
        foreach (var room in layout.Rooms)
            RoomOxygen.TryAdd(room.Id, World.FullOxygenLevel);

        var turrets = layout.Ship.Turrets.Select(t => t.Id).ToHashSet();
        foreach (var gone in TurretCooldowns.Keys.Where(k => !turrets.Contains(k)).ToList())
            TurretCooldowns.Remove(gone);

        foreach (var crew in Crew)
        {
            crew.Path.Clear();
            crew.GoalTile = null;
            if (rooms.Contains(crew.RoomId))
            {
                // The rebuilt hull can differ in small ways (a half-wall that became a full one): never leave anyone inside a wall.
                if (!TilePathfinder.IsPassable(layout.Tiles, TilePathfinder.TileAt(crew.Position))
                    && TilePathfinder.NearestPassable(layout.Tiles, TilePathfinder.TileAt(crew.Position)) is { } free)
                    crew.Position = TilePathfinder.CenterOf(free);
                continue;
            }
            // Standing in a compartment that is gone: they are lost with it (the caller kills them before it gets here).
            crew.Health = 0f;
        }
    }

    // Turrets, engines and devices are numbered by their order in the hull's definition, which shifts when a compartment
    // before them is lost - so when the hull is rebuilt every id-keyed record is carried over by WHERE the fixture stands.
    private static Dictionary<string, string> FixtureIdMap(Ship old, Ship next)
    {
        var map = new Dictionary<string, string>();
        void Match<T>(IEnumerable<T> before, IEnumerable<T> after, Func<T, string> id, Func<T, Vec2> position)
        {
            var nextByPosition = after.GroupBy(position).ToDictionary(g => g.Key, g => id(g.First()));
            foreach (var item in before)
                if (nextByPosition.TryGetValue(position(item), out var newId))
                    map[id(item)] = newId;
        }
        Match(old.Turrets, next.Turrets, t => t.Id, t => t.PeriscopePosition);
        Match(old.Engines, next.Engines, e => e.Id, e => e.ControlPosition);
        Match(old.SystemDevices, next.SystemDevices, d => d.Id, d => d.Position);
        Match(old.Cameras, next.Cameras, c => c.Id, c => new Vec2(c.X, c.Y));
        return map;
    }

    private void RemapFixtureIds(Dictionary<string, string> map)
    {
        string Remap(string id) => map.TryGetValue(id, out var n) ? n : id;

        var hp = _fixtureHp.ToList();
        _fixtureHp.Clear();
        foreach (var (key, value) in hp)
        {
            var colon = key.IndexOf(':');
            var baseId = colon < 0 ? key : key[..colon];
            if (!map.ContainsKey(baseId) && (baseId.StartsWith("turret") || baseId.StartsWith("engine")))
                continue; // that fixture is gone with its compartment
            _fixtureHp[Remap(baseId) + (colon < 0 ? "" : key[colon..])] = value;
        }

        var cooldowns = TurretCooldowns.ToList();
        TurretCooldowns.Clear();
        foreach (var (id, value) in cooldowns)
            if (map.ContainsKey(id))
                TurretCooldowns[map[id]] = value;

        var cut = CutWires.ToList();
        CutWires.Clear();
        foreach (var wire in cut)
            CutWires.Add(wire.StartsWith("drop-") ? DropWireId(Remap(wire["drop-".Length..])) : wire);

        RepairProgress.Clear();
        foreach (var crew in Crew)
            if (crew.PostId is { } post)
                crew.PostId = map.TryGetValue(post, out var moved) ? moved : null;
    }
}
