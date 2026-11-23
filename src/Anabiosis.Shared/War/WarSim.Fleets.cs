namespace Anabiosis.Shared.War;

// Fortifications and fleets: what a system can do for itself, and the groups of warships moving about the lanes.
public sealed partial class WarSim
{
    private readonly List<WarFleet> _fleets = new();
    private int _nextFleetId = 1;

    public IReadOnlyList<WarFleet> Fleets => _fleets;

    // ---- fortifications: the sum of what is built in the system ----

    public float FortMax(WarNode node)
    {
        var max = 0f;
        if (node.Planet == PlanetKind.Capital)
            max += Config.CapitalFortHp;
        else if (node.Planet == PlanetKind.Colony)
            max += Config.ColonyFortHp;
        if (node.Yard == YardTier.Yard)
            max += Config.YardFortHp;
        else if (node.Yard == YardTier.MegaYard)
            max += Config.MegaYardFortHp;
        if (node.Outpost)
            max += Config.OutpostFortHp;
        if (node.Nest)
            max += Config.VirusNestFortHp;
        return max;
    }

    // What the works hit back with in a round: less as they are knocked down.
    public float FortAttack(WarNode node) => node.FortHp * Config.FortAttackPerHp;

    // Something is built or changed: the fortifications grow by what the change adds.
    private void ChangeInstallations(WarNode node, Action change)
    {
        var before = FortMax(node);
        change();
        node.FortHp = Math.Max(0f, node.FortHp + FortMax(node) - before);
    }

    private void StepFortRegeneration(double dt)
    {
        foreach (var node in _nodes)
        {
            if (node.Fighting)
                continue;
            node.SinceCombat += (float)dt;
            if (node.SinceCombat < Config.FortRegenAfterSeconds)
                continue;
            var max = FortMax(node);
            if (node.FortHp < max)
                node.FortHp = Math.Min(max, node.FortHp + max * Config.FortRegenPerSecond * (float)dt);
        }
    }

    // ---- fleets ----

    public WarFleet SpawnFleet(WarFaction owner, int node)
    {
        var fleet = new WarFleet { Id = _nextFleetId++, Owner = owner, Node = node };
        _fleets.Add(fleet);
        return fleet;
    }

    // Takes this share of the ships standing in a system out of its garrison into a fleet of their own.
    public WarFleet? DetachFromGarrison(WarNode node, float share)
    {
        var supply = CombatStats.Supply(node.Garrison);
        if (supply < 0.5f || share <= 0f)
            return null;
        var fleet = SpawnFleet(node.Owner, node.Index);
        for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
        {
            var taken = node.Garrison[c] * Math.Min(1f, share);
            fleet.Ships[c] = taken;
            node.Garrison[c] -= taken;
        }
        return fleet;
    }

    // Everything of one faction that stands in a system and could fight there, as one set of counts: the garrison (if the
    // system is theirs) and every fleet standing there.
    public float[] ForceAt(int node, WarFaction faction)
    {
        var total = new float[ShipStats.ClassCount];
        if (_nodes[node].Owner == faction)
            for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                total[c] += _nodes[node].Garrison[c];
        foreach (var fleet in _fleets)
            if (fleet.Owner == faction && fleet.Standing && fleet.Node == node)
                for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                    total[c] += fleet.Ships[c];
        return total;
    }

    public void SendFleet(WarFleet fleet, int target, FleetTask task)
    {
        if (!fleet.Standing)
            return;
        fleet.Task = task;
        fleet.Destination = target;
        if (fleet.Node == target)
        {
            FleetArrived(fleet);
            return;
        }
        var path = Galaxy.Path(fleet.Node, target);
        fleet.Route.Clear();
        for (var i = 1; i < path.Count; i++)
            fleet.Route.Enqueue(path[i]);
        fleet.Next = path[0];
        fleet.LaneLength = Galaxy.LaneLength(fleet.Node, fleet.Next);
        fleet.Along = 0f;
    }

    // How long a fleet standing in a system would take to reach another.
    public float TravelSeconds(float[] ships, int from, int to)
    {
        var speed = float.PositiveInfinity;
        for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
            if (ships[c] >= 0.5f)
                speed = Math.Min(speed, CombatStats.Speed((ShipClass)c));
        if (float.IsPositiveInfinity(speed))
            speed = CombatStats.Speed(ShipClass.Corvette);
        return Galaxy.PathLength(from, to) / speed;
    }

    // Seconds until a fleet already under way reaches its destination.
    public float SecondsToDestination(WarFleet fleet)
    {
        if (fleet.Standing)
            return 0f;
        return (fleet.LaneLength - fleet.Along + (fleet.Route.Count > 0 ? Galaxy.PathLength(fleet.Next, fleet.Destination) : 0f)) / fleet.Speed;
    }

    private void StepFleets(double dt)
    {
        for (var i = _fleets.Count - 1; i >= 0; i--)
        {
            var fleet = _fleets[i];
            var left = fleet.Speed * dt;
            while (left > 1e-6 && !fleet.Standing)
            {
                var room = fleet.LaneLength - fleet.Along;
                if (left < room)
                {
                    fleet.Along += (float)left;
                    left = 0;
                    continue;
                }
                left -= room;
                fleet.Node = fleet.Next;
                fleet.Along = 0f;
                if (fleet.Route.Count > 0)
                {
                    fleet.Next = fleet.Route.Dequeue();
                    fleet.LaneLength = Galaxy.LaneLength(fleet.Node, fleet.Next);
                }
                else
                {
                    fleet.Next = -1;
                    fleet.LaneLength = 0f;
                    FleetArrived(fleet);
                }
            }
        }
    }

    // A fleet has come to the end of its route. One with no operation joins the garrison of its own system; the rest wait.
    private void FleetArrived(WarFleet fleet)
    {
        var node = _nodes[fleet.Node];
        var task = fleet.Task;
        fleet.Task = FleetTask.Idle;
        if (task is FleetTask.Strike or FleetTask.Invade || fleet.OperationId != 0)
            return;
        if (node.Owner == fleet.Owner)
            JoinGarrison(fleet);
    }

    private void JoinGarrison(WarFleet fleet)
    {
        var node = _nodes[fleet.Node];
        for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
            node.Garrison[c] += fleet.Ships[c];
        _fleets.Remove(fleet);
    }

    // Folds a fleet into another standing in the same place.
    private void MergeFleets(WarFleet into, WarFleet other)
    {
        for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
            into.Ships[c] += other.Ships[c];
        _fleets.Remove(other);
    }
}
