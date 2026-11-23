namespace Anabiosis.Shared.War;

// Where ore comes from: miners and drones working an outpost asteroid field, and the planets themselves.
public sealed partial class WarSim
{
    // The most ore a node can hold: the largest of what its installations allow.
    public float StockCap(WarNode node)
    {
        var cap = 0f;
        if (node.Outpost)
            cap = Math.Max(cap, Config.OutpostStockCap);
        if (node.Planet == PlanetKind.Colony)
            cap = Math.Max(cap, Config.ColonyStockCap);
        if (node.Planet == PlanetKind.Capital)
            cap = Math.Max(cap, Config.CapitalStockCap);
        if (node.Yard == YardTier.Yard)
            cap = Math.Max(cap, Config.YardStockCap);
        if (node.Yard == YardTier.MegaYard)
            cap = Math.Max(cap, Config.MegaYardStockCap);
        // Room for whatever is being saved up for: a project or a ship in the queue must fit in the stock that pays for it.
        if (node.Project is { } project)
            cap = Math.Max(cap, ProjectStats.Cost(project.Kind) + 200f);
        foreach (var order in node.Queue)
            cap = Math.Max(cap, ShipStats.Cost(order.Class) + 200f);
        return cap;
    }

    // Ore a node brings in per second.
    public float OreRate(WarNode node)
    {
        var rate = 0f;
        if (node.Outpost)
            rate += (node.Miners * Config.MinerOrePerSecond + node.Drones * Config.DroneOrePerSecond) * Galaxy.Systems[node.Index].Richness;
        if (node.Nest)
            rate += Config.VirusNestOrePerSecond * (1f + (float)(Clock / 3600.0) / Config.VirusGrowthHours);
        if (node.Planet == PlanetKind.Colony)
            rate += Config.ColonyOrePerSecond;
        else if (node.Planet == PlanetKind.Capital)
            rate += Config.CapitalOrePerSecond;
        return rate;
    }

    private void Produce(double dt)
    {
        foreach (var node in _nodes)
        {
            if (node.Owner == WarFaction.None)
                continue;
            var made = (float)(OreRate(node) * dt * (node.IsUnion ? 1f : Config.VirusOreFactor));
            if (made <= 0f)
                continue;
            var space = Math.Max(0f, StockCap(node) - node.Stock);
            var stored = Math.Min(made, space);
            node.Stock += stored;
            if (node.IsUnion)
            {
                OreMined += stored;
                OreWasted += made - stored;
            }
        }
    }

    // An outpost builds its own miners and drones out of its own ore, one at a time, until every slot is filled.
    private void StepWorkshops(double dt)
    {
        foreach (var node in _nodes)
        {
            if (!node.IsUnion || !node.Outpost)
                continue;

            if (node.WorkshopBuilding is { } building)
            {
                node.WorkshopSecondsLeft -= (float)dt;
                if (node.WorkshopSecondsLeft > 0f)
                    continue;
                if (building == ShipClass.Miner)
                    node.Miners++;
                else
                    node.Drones++;
                node.WorkshopBuilding = null;
            }

            var system = Galaxy.Systems[node.Index];
            ShipClass? wanted = null;
            if (node.Miners < MinerSlots(system))
                wanted = ShipClass.Miner;
            else if (node.Drones < DroneSlots(system))
                wanted = ShipClass.Drone;
            if (wanted is not { } next || node.Stock < ShipStats.Cost(next) + Config.OutpostKeepOre)
                continue;

            node.Stock -= ShipStats.Cost(next);
            node.WorkshopBuilding = next;
            node.WorkshopSecondsLeft = ShipStats.BuildSeconds(next);
        }
    }
}
