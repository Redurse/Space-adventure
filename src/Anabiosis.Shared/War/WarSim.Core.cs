namespace Anabiosis.Shared.War;

// The story hooks of the war: the great precursor post where the tool lies, and the end of the virus when the artifact (its core)
// is destroyed. The campaign (World.CoreHunt.cs) asks these; the war itself never depends on them.
public sealed partial class WarSim
{
    // The system of the great precursor post: far from the Union capital and from the nest alike, so reaching it means a real
    // journey, and not in anyone hands. -1 when there is none.
    public int PrecursorIndex { get; private set; } = -1;

    // True once the core is destroyed: the virus is gone and does not come back.
    public bool VirusDestroyed { get; private set; }

    private void SeedPrecursor()
    {
        var best = -1;
        var bestScore = -1f;
        foreach (var node in _nodes)
        {
            if (node.Owner != WarFaction.None)
                continue;
            if (Galaxy.Hops(CapitalIndex, node.Index) > Config.PrecursorMaxHops)
                continue; // a journey, but one a campaign can make: not the far rim of a 4000-star galaxy
            var fromCapital = Galaxy.PathLength(CapitalIndex, node.Index);
            var fromNest = NestIndex >= 0 ? Galaxy.PathLength(NestIndex, node.Index) : fromCapital;
            var score = Math.Min(fromCapital, fromNest);
            if (score > bestScore)
            {
                bestScore = score;
                best = node.Index;
            }
        }
        PrecursorIndex = best;
    }

    // The core is destroyed and the whole virus stops at once: every ship of it, everywhere, is gone, and the systems it held are
    // empty. What it built there is lost with it.
    public void DestroyVirus()
    {
        if (VirusDestroyed)
            return;
        VirusDestroyed = true;
        _fleets.RemoveAll(f => f.Owner == WarFaction.Virus);
        foreach (var node in _nodes)
        {
            if (node.Owner != WarFaction.Virus)
                continue;
            node.Owner = WarFaction.None;
            node.Nest = false;
            node.Outpost = false;
            node.Planet = PlanetKind.None;
            node.Yard = YardTier.None;
            node.Miners = 0;
            node.Drones = 0;
            node.Stock = 0f;
            node.FortHp = 0f;
            node.OccupationSeconds = 0f;
            node.Queue.Clear();
            node.Project = null;
            node.WorkshopBuilding = null;
            Array.Clear(node.Garrison);
        }
        _virusRevealed = false;
        Emit("Ядро восстания уничтожено: всё охваченное остановилось");
    }
}
