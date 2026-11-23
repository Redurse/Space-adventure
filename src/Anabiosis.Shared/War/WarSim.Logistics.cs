namespace Anabiosis.Shared.War;

// Freighters carry ore from the nodes that have more than they need to the nodes that want some.
public sealed partial class WarSim
{
    // What the economy wants built here and has not paid for yet: freighters, outposters, and a project.
    public float EconomicNeed(WarNode node)
    {
        var need = 0f;
        foreach (var order in node.Queue)
            if (!order.Started && !ShipStats.IsWarship(order.Class))
                need += ShipStats.Cost(order.Class);
        if (node.Project is { Started: false } project)
            need += ProjectStats.Cost(project.Kind);
        return need;
    }

    // The same for warships.
    public float MilitaryNeed(WarNode node)
    {
        var need = 0f;
        foreach (var order in node.Queue)
            if (!order.Started && ShipStats.IsWarship(order.Class))
                need += ShipStats.Cost(order.Class);
        return need;
    }

    // Ore a node still wants delivered: what it needs, less what it holds and what is already on its way.
    public float Demand(WarNode node) => Math.Max(0f, EconomicNeed(node) + MilitaryNeed(node) - node.Stock - node.Incoming);

    // Ore a node can part with: what it holds beyond what it keeps, what is promised away, and what it needs itself.
    public float Surplus(WarNode node)
    {
        var keep = node.Outpost && node.Planet == PlanetKind.None && !node.HasYard ? Config.OutpostKeepOre : 0f;
        return node.Stock - node.Reserved - keep - EconomicNeed(node) - MilitaryNeed(node);
    }

    private void DispatchFreighters()
    {
        var idle = _movers.Where(m => m.IsIdleFreighter).ToList();
        if (idle.Count == 0)
            return;

        var demand = new float[_nodes.Length];
        var sinks = new List<int>();
        var sources = new List<(int Node, float Surplus)>();
        foreach (var node in _nodes)
        {
            if (!node.IsUnion)
                continue;
            var wanted = Demand(node);
            if (wanted > 1f)
            {
                demand[node.Index] = wanted;
                sinks.Add(node.Index);
                continue;
            }
            var spare = Surplus(node);
            if (spare >= Config.PickupThreshold)
                sources.Add((node.Index, spare));
        }
        if (sinks.Count == 0 || sources.Count == 0)
            return;

        var dispatched = 0;
        foreach (var (source, startingSurplus) in sources.OrderByDescending(s => s.Surplus))
        {
            var surplus = startingSurplus;
            while (surplus >= Config.PickupThreshold && dispatched < Config.MaxDispatchesPerRound && idle.Count > 0)
            {
                // The sink that wants the most for the shortest haul.
                var best = -1;
                var bestScore = 0f;
                foreach (var sink in sinks)
                {
                    if (demand[sink] < 1f)
                        continue;
                    var score = Math.Min(demand[sink], Config.FreighterCapacity) / (1f + Galaxy.PathLength(source, sink) / Config.HaulDistanceScale);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = sink;
                    }
                }
                if (best < 0)
                    break;

                // The idle freighter nearest the source.
                var pick = 0;
                var pickDistance = float.PositiveInfinity;
                for (var i = 0; i < idle.Count; i++)
                {
                    var distance = Galaxy.PathLength(idle[i].Node, source);
                    if (distance < pickDistance)
                    {
                        pickDistance = distance;
                        pick = i;
                    }
                }
                var freighter = idle[pick];
                idle.RemoveAt(pick);

                var planned = Math.Min(Config.FreighterCapacity, Math.Min(surplus, demand[best]));
                freighter.Task = MoverTask.ToSource;
                freighter.Source = source;
                freighter.Sink = best;
                freighter.Planned = planned;
                freighter.Cargo = 0f;
                _nodes[source].Reserved += planned;
                _nodes[best].Incoming += planned;
                surplus -= planned;
                demand[best] -= planned;
                dispatched++;
                SendMover(freighter, source);
            }
        }
    }
}
