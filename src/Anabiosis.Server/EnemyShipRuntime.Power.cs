using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// A hostile ship's electrics, the same chain the player's ship has: the reactor feeds the distribution block, which feeds a
// junction box (щиток) per system, which feeds every consumer of that system (the guns, the engines, the oxygen generator...)
// over a wire of its own. Shooting a consumer, a junction box, the distribution block or the reactor breaks that link, and
// what hangs off it goes dark until a crew engineer repairs it (World.EnemyCrew.cs).
internal sealed record EnemyWire(string Id, string From, string To);

public sealed partial class EnemyShipRuntime
{
    internal const string DistributionNode = "distribution";
    // A system's allocation has to be at least this much (out of an even split of ~12) for its consumers to work.
    private const float PowerThreshold = 1f;

    internal PowerGrid Grid { get; } = CreateGrid();
    internal List<EnemyWire> Wires { get; } = new();
    internal Dictionary<string, PowerSystemId> SystemOfConsumer { get; } = new();
    // Which of the hull's junction boxes (щитки) is the physical box of a system's junction.
    internal Dictionary<PowerSystemId, string> JunctionBoxOfSystem { get; } = new();
    internal HashSet<string> CutWires { get; } = new();
    internal bool HelmBroken { get; set; }
    internal bool WasWithoutReactorPower { get; set; }
    // Progress (seconds of work done) of timed repairs, by job key.
    internal Dictionary<string, float> RepairProgress { get; } = new();

    private static PowerGrid CreateGrid()
    {
        var grid = new PowerGrid();
        grid.SplitEvenly();
        return grid;
    }

    internal static string TrunkWireId(PowerSystemId system) => $"trunk-{system}";
    internal static string DropWireId(string consumerId) => $"drop-{consumerId}";
    internal static string JunctionNode(PowerSystemId system) => $"junction-{system}";

    // (Re)builds the wiring for the current hull: one trunk per system that has consumers, one drop per consumer. Wires that
    // already existed keep their damage (the cut set is by id).
    internal void BuildPowerGraph()
    {
        Wires.Clear();
        SystemOfConsumer.Clear();
        JunctionBoxOfSystem.Clear();
        var ship = Layout.Ship;

        void Consume(string id, PowerSystemId system) => SystemOfConsumer[id] = system;
        foreach (var turret in ship.Turrets)
            Consume(turret.Id, PowerSystemId.WeaponCharger);
        foreach (var engine in ship.Engines)
            Consume(engine.Id, PowerSystemId.Engine);
        foreach (var device in ship.SystemDevices)
            Consume(device.Id, device.System);
        foreach (var camera in ship.Cameras)
            Consume(camera.Id, PowerSystemId.Secondary);

        foreach (var system in SystemOfConsumer.Values.Distinct())
            Wires.Add(new EnemyWire(TrunkWireId(system), DistributionNode, JunctionNode(system)));
        foreach (var (consumer, system) in SystemOfConsumer)
            Wires.Add(new EnemyWire(DropWireId(consumer), JunctionNode(system), consumer));

        // Give each system's junction a physical box: the nearest unused junction box to its consumers.
        var free = ship.JunctionBoxes.ToList();
        foreach (var system in SystemOfConsumer.Values.Distinct())
        {
            if (free.Count == 0)
                break;
            var points = SystemOfConsumer.Where(kv => kv.Value == system).Select(kv => ConsumerPosition(kv.Key)).ToList();
            var centroid = new Vec2(points.Average(p => p.X), points.Average(p => p.Y));
            var box = free.OrderBy(b => (new Vec2(b.X, b.Y) - centroid).Length()).First();
            free.Remove(box);
            JunctionBoxOfSystem[system] = box.Id;
        }

        CutWires.RemoveWhere(id => Wires.All(w => w.Id != id));
    }

    internal Vec2 ConsumerPosition(string consumerId)
    {
        var ship = Layout.Ship;
        if (ship.Turrets.FirstOrDefault(t => t.Id == consumerId) is { } turret)
            return turret.PeriscopePosition;
        if (ship.Engines.FirstOrDefault(e => e.Id == consumerId) is { } engine)
            return engine.ControlPosition;
        if (ship.SystemDevices.FirstOrDefault(d => d.Id == consumerId) is { } device)
            return device.Position;
        if (ship.Cameras.FirstOrDefault(c => c.Id == consumerId) is { } camera)
            return new Vec2(camera.X, camera.Y);
        return Vec2.Zero;
    }

    // Is there an unbroken path of wires from the distribution block to this node?
    internal bool IsNodePowered(string node)
    {
        if (node == DistributionNode)
            return !Grid.DistributionBroken;
        return Wires.Any(w => w.To == node && !CutWires.Contains(w.Id) && IsNodePowered(w.From));
    }

    // Does this consumer actually get power: wired up AND its system has some allocated (the reactor is alive).
    internal bool HasPower(string consumerId) =>
        SystemOfConsumer.TryGetValue(consumerId, out var system)
        && Grid.GetAllocation(system) >= PowerThreshold
        && IsNodePowered(consumerId);

    internal bool IsTurretFiring(string turretId) => IsTurretWorking(turretId) && HasPower(turretId);

    internal bool IsEngineRunning(string engineId) => IsEngineWorking(engineId) && HasPower(engineId);

    // The wire a hit on this consumer breaks (its own drop), or the trunk when the junction box itself is hit.
    internal void CutDrop(string consumerId) => CutWires.Add(DropWireId(consumerId));
    internal void CutTrunk(PowerSystemId system) => CutWires.Add(TrunkWireId(system));
}
