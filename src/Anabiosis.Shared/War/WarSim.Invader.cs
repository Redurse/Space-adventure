namespace Anabiosis.Shared.War;

// A stand-in for the virus, so that the Union defence can be seen working before the virus itself exists: fleets that march
// on the nearest Union system, besiege it until it falls, and go on to the next. The virus proper (infection, its own
// production, hunting the economy) will replace this.
public sealed partial class WarSim
{
    // How a raid is made up, by share of its supply points.
    private static readonly (ShipClass Ship, float Share)[] InvaderMix =
    {
        (ShipClass.Corvette, 0.45f),
        (ShipClass.Frigate, 0.30f),
        (ShipClass.Cruiser, 0.20f),
        (ShipClass.Dreadnought, 0.05f),
    };

    // Lands an enemy fleet of the given strength (in supply points) in a system.
    public WarFleet SpawnInvasion(int node, float supplyPoints)
    {
        var fleet = SpawnFleet(WarFaction.Virus, node);
        foreach (var (ship, share) in InvaderMix)
            fleet.Ships[(int)ship] = supplyPoints * share / ShipStats.Supply(ship);
        fleet.Task = FleetTask.Invade;
        Emit($"ВТОРЖЕНИЕ: враг ({supplyPoints:0} очк.) появился у {NameOf(node)}");
        return fleet;
    }

    // The free system at the edge of the Union farthest from its capital: where a raid out of the dark would come from.
    public int FindFrontierNode()
    {
        var best = -1;
        var bestHops = -1;
        foreach (var node in _nodes)
        {
            if (node.Owner != WarFaction.None || !Galaxy.LanesOf(node.Index).Any(l => _nodes[l.To].IsUnion))
                continue;
            var hops = Galaxy.Hops(CapitalIndex, node.Index);
            if (hops > bestHops)
            {
                bestHops = hops;
                best = node.Index;
            }
        }
        return best;
    }

}
