namespace Anabiosis.Shared.War;

// A count of what the Union has, taken whenever the government plans or the report is written.
public sealed class UnionCensus
{
    public int Owned { get; set; }
    public int Colonies { get; set; }
    public int Outposts { get; set; }
    public int Yards { get; set; }
    public int MegaYards { get; set; }
    public int Freighters { get; set; }
    public int FreightersIdle { get; set; }
    public int Outposters { get; set; }
    public int Miners { get; set; }
    public int Drones { get; set; }
    public int ProjectsRunning { get; set; }
    public double OrePool { get; set; }
    public int Supply { get; set; }
    public int SupplyQueued { get; set; }
    public int SupplyCap { get; set; }
    // Ore the economy (freighters, outposters, projects) is short of right now, summed over every node.
    public double EconomicShortfall { get; set; }
    public double[] Garrison { get; } = new double[ShipStats.ClassCount];
    public int[] Queued { get; } = new int[ShipStats.ClassCount];
    // Enemy fleets, systems with a battle going on, operations under way, systems the enemy holds, enemy supply points.
    public int EnemyFleets { get; set; }
    public int Battles { get; set; }
    public int Operations { get; set; }
    public int LostSystems { get; set; }
    public int EnemySupply { get; set; }
    // The virus in all: supply points in garrisons, fleets and yards, and the systems it holds.
    public int VirusSupply { get; set; }
    public int VirusYards { get; set; }
}

public sealed partial class WarSim
{
    public UnionCensus TakeCensus()
    {
        var census = new UnionCensus();
        foreach (var node in _nodes)
        {
            if (!node.IsUnion)
                continue;
            census.Owned++;
            census.OrePool += node.Stock;
            census.Miners += node.Miners;
            census.Drones += node.Drones;
            if (node.Planet != PlanetKind.None)
                census.Colonies++;
            if (node.Outpost)
                census.Outposts++;
            if (node.Yard == YardTier.Yard)
                census.Yards++;
            else if (node.Yard == YardTier.MegaYard)
                census.MegaYards++;
            if (node.Project != null)
                census.ProjectsRunning++;
            for (var i = 0; i < ShipStats.ClassCount; i++)
                census.Garrison[i] += node.Garrison[i];
            foreach (var order in node.Queue)
                census.Queued[(int)order.Class]++;
            census.EconomicShortfall += Math.Max(0f, EconomicNeed(node) - node.Stock - node.Incoming);
        }

        // Ships on the lanes belong to the fleet too.
        foreach (var fleet in _fleets)
        {
            if (fleet.Owner == WarFaction.Union)
                for (var i = CombatStats.FirstWarship; i < ShipStats.ClassCount; i++)
                    census.Garrison[i] += fleet.Ships[i];
            else
            {
                census.EnemyFleets++;
                census.EnemySupply += (int)Math.Round(fleet.Supply);
            }
        }
        census.Battles = BattleCount;
        census.Operations = _operations.Count;
        census.LostSystems = _nodes.Count(n => n.Owner == WarFaction.Virus && !n.Nest);
        census.VirusSupply = (int)Math.Round(VirusSupply());
        census.VirusYards = _nodes.Count(n => n.Owner == WarFaction.Virus && n.HasYard);

        foreach (var mover in _movers)
        {
            if (mover.Class == ShipClass.Freighter)
            {
                census.Freighters++;
                if (mover.IsIdleFreighter)
                    census.FreightersIdle++;
            }
            else if (mover.Class == ShipClass.Outposter)
            {
                census.Outposters++;
            }
        }

        for (var i = (int)ShipClass.Corvette; i < ShipStats.ClassCount; i++)
        {
            census.Supply += (int)Math.Round(census.Garrison[i] * ShipStats.Supply((ShipClass)i));
            census.SupplyQueued += census.Queued[i] * ShipStats.Supply((ShipClass)i);
        }
        var cap = Config.SupplyBase + census.Colonies * Config.SupplyPerColony + census.Outposts * Config.SupplyPerOutpost;
        census.SupplyCap = AtWar ? (int)(cap * (1f + Config.WarSupplyBonus)) : cap;
        return census;
    }
}
