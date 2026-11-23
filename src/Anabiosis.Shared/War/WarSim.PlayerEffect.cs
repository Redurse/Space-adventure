namespace Anabiosis.Shared.War;

// What the player own ship does to the war: where the virus is, and a blow against it.
public sealed partial class WarSim
{
    // Supply points of virus ships in a system: the garrison if it is theirs, and every fleet standing there.
    public float VirusPresenceAt(int node) => CombatStats.Supply(ForceAt(node, WarFaction.Virus));

    // The player destroyed virus ships in a system: this many supply points of them are gone from the war, from the fleets standing
    // there first and then the garrison, every ship class thinned alike.
    public void StrikeVirusAt(int node, float supply)
    {
        var left = supply;
        foreach (var fleet in _fleets.Where(f => f.Owner == WarFaction.Virus && f.Standing && f.Node == node).ToList())
        {
            var total = fleet.Supply;
            if (total <= 0.01f)
                continue;
            var share = Math.Min(1f, left / total);
            for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                fleet.Ships[c] *= 1f - share;
            left -= total * share;
            if (left <= 0.01f)
                return;
        }
        var owned = _nodes[node];
        if (owned.Owner == WarFaction.Virus)
        {
            var total = CombatStats.Supply(owned.Garrison);
            if (total > 0.01f)
            {
                var share = Math.Min(1f, left / total);
                for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                    owned.Garrison[c] *= 1f - share;
            }
        }
    }
}
