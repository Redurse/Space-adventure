namespace Anabiosis.Shared.War;

// The Union government: every half minute it takes stock of the whole state and gives the orders - what to build where, how
// many freighters to keep, where to settle next. It sees the whole map. Orders only ever go into yard queues and project
// slots; the rest (ore moving, ships being built) happens by itself, so a plan that turns out wrong costs only ore.
public sealed partial class WarSim
{
    // Systems an outposter is already on its way to (or being built for), so two are never sent to the same place.
    private readonly HashSet<int> _foundingTargets = new();

    private void RunGovernment()
    {
        var census = TakeCensus();
        PlanProjects(census);
        PlanFreighters(census);
        PlanExpansion(census);
        // At war the ships come first: the economy waits.
        if (AtWar || census.EconomicShortfall <= Config.FreighterCapacity)
            PlanWarships(census);
    }

    private bool HasRoom(WarNode node) => node.IsUnion && node.HasYard && node.Queue.Count < SlotsOf(node) + 1;

    // ---- projects: yards where there are none within reach, a megayard, colonies on rocky planets ----

    private void PlanProjects(UnionCensus census)
    {
        if (census.ProjectsRunning >= Config.ProjectsInParallel)
            return;

        // 1. A yard wherever the territory has grown out of reach of the existing ones.
        var yardSites = _nodes.Where(n => n.IsUnion && (n.HasYard || n.Project is { Kind: ProjectKind.Yard or ProjectKind.MegaYard })).Select(n => n.Index).ToList();
        var uncovered = _nodes.Where(n => n.IsUnion && yardSites.All(y => Galaxy.Hops(y, n.Index) > Config.YardCoverageHops)).Select(n => n.Index).ToList();
        if (uncovered.Count > 0)
        {
            var site = uncovered
                .Where(i => _nodes[i].Project == null && !_nodes[i].HasYard)
                .OrderByDescending(i => uncovered.Count(u => Galaxy.Hops(i, u) <= 3))
                .ThenByDescending(i => _nodes[i].Planet != PlanetKind.None)
                .FirstOrDefault(-1);
            if (site >= 0)
            {
                _nodes[site].Project = new WarProject { Kind = ProjectKind.Yard };
                return;
            }
        }

        // 2. A megayard, once there is the ore to build one without stopping everything else.
        var megaYards = census.MegaYards + _nodes.Count(n => n.Project is { Kind: ProjectKind.MegaYard });
        if (megaYards < Config.MaxMegaYards && census.OrePool >= ProjectStats.Cost(ProjectKind.MegaYard) * 1.5f)
        {
            // Any yard will do; one on a colony first, and the farther from the capital the better, so the megayards are spread out.
            var site = _nodes
                .Where(n => n.IsUnion && n.Yard == YardTier.Yard && n.Project == null)
                .OrderByDescending(n => n.Planet == PlanetKind.Colony)
                .ThenByDescending(n => _nodes.Where(m => m.Yard == YardTier.MegaYard).Select(m => Galaxy.Hops(m.Index, n.Index)).DefaultIfEmpty(10).Min())
                .FirstOrDefault();
            if (site is not null)
            {
                site.Project = new WarProject { Kind = ProjectKind.MegaYard };
                return;
            }
        }

        // 3. A colony on a rocky planet of an outpost, the richest system nearest the capital first.
        var colonies = census.Colonies + _nodes.Count(n => n.Project is { Kind: ProjectKind.Colony });
        if (colonies < census.Owned * Config.ColonyShareMax)
        {
            var site = _nodes
                .Where(n => n.IsUnion && n.Outpost && n.Planet == PlanetKind.None && n.Project == null && Galaxy.Systems[n.Index].HasRockyPlanet)
                .OrderBy(n => Galaxy.Hops(CapitalIndex, n.Index) / Galaxy.Systems[n.Index].Richness)
                .FirstOrDefault();
            if (site is not null)
                site.Project = new WarProject { Kind = ProjectKind.Colony };
        }
    }

    // ---- freighters: enough of them for the producers there are ----

    private void PlanFreighters(UnionCensus census)
    {
        var wanted = Math.Clamp((int)MathF.Ceiling(census.Owned * Config.FreightersPerProducer), Config.MinFreighters, Config.MaxFreighters);
        var toOrder = wanted - census.Freighters - census.Queued[(int)ShipClass.Freighter];
        for (var i = 0; i < Math.Min(toOrder, 2); i++)
        {
            var yard = _nodes.Where(HasRoom).OrderByDescending(n => n.Stock - EconomicNeed(n)).FirstOrDefault();
            if (yard is null)
                return;
            yard.Queue.Add(new YardOrder { Class = ShipClass.Freighter });
        }
    }

    // ---- expansion: outposters sent to claim free systems next to the territory ----

    private void PlanExpansion(UnionCensus census)
    {
        if (census.Owned + _foundingTargets.Count >= Config.ExpansionMaxSystems)
            return;

        // Which yards can send an outposter, and how far each free system next to the territory is from the nearest of them.
        var yards = _nodes.Where(HasRoom).ToList();
        if (yards.Count == 0)
            return;

        while (_foundingTargets.Count < Config.ExpansionParallel)
        {
            var bestTarget = -1;
            WarNode? bestYard = null;
            var bestScore = 0f;
            foreach (var node in _nodes)
            {
                if (node.Owner != WarFaction.None || _foundingTargets.Contains(node.Index))
                    continue;
                if (!Galaxy.LanesOf(node.Index).Any(l => _nodes[l.To].IsUnion))
                    continue;
                WarNode? nearest = null;
                var nearestDistance = float.PositiveInfinity;
                foreach (var yard in yards)
                {
                    var distance = Galaxy.PathLength(yard.Index, node.Index);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = yard;
                    }
                }
                if (nearest is null)
                    continue;
                var system = Galaxy.Systems[node.Index];
                var score = system.Richness * (system.HasRockyPlanet ? 1.3f : 1f) / (1f + nearestDistance / 300f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = node.Index;
                    bestYard = nearest;
                }
            }
            if (bestTarget < 0 || bestYard is null)
                return;

            bestYard.Queue.Add(new YardOrder { Class = ShipClass.Outposter, Target = bestTarget });
            _foundingTargets.Add(bestTarget);
            if (!HasRoom(bestYard))
                yards.Remove(bestYard);
            if (yards.Count == 0)
                return;
        }
    }

    // ---- warships: what is left of the ore after the economy has had its share, up to the fleet limit ----

    private void PlanWarships(UnionCensus census)
    {
        var used = census.Supply + census.SupplyQueued;
        var counts = new int[ShipStats.ClassCount];
        for (var i = 0; i < counts.Length; i++)
            counts[i] = (int)Math.Round(census.Garrison[i]) + census.Queued[i];

        foreach (var yard in _nodes.Where(HasRoom).OrderByDescending(n => n.Stock).ToList())
        {
            var choice = ChooseWarship(yard, counts, census, used);
            if (choice is not { } ship)
                continue;
            var supply = ShipStats.Supply(ship);
            if (used + supply > census.SupplyCap)
                continue;
            yard.Queue.Add(new YardOrder { Class = ship });
            counts[(int)ship]++;
            used += supply;
        }
    }

    // A dreadnought on a megayard until each has its share, otherwise whichever of the three smaller classes is furthest
    // below its share of the fleet.
    private ShipClass? ChooseWarship(WarNode yard, int[] counts, UnionCensus census, int used)
    {
        if (yard.Yard == YardTier.MegaYard && counts[(int)ShipClass.Dreadnought] < Config.DreadnoughtsPerMegaYard * census.MegaYards
            && used + ShipStats.Supply(ShipClass.Dreadnought) <= census.SupplyCap)
            return ShipClass.Dreadnought;

        var total = Math.Max(1, counts[(int)ShipClass.Corvette] * 1 + counts[(int)ShipClass.Frigate] * 2 + counts[(int)ShipClass.Cruiser] * 4);
        ShipClass? best = null;
        var bestGap = float.NegativeInfinity;
        foreach (var (ship, share) in new[]
                 {
                     (ShipClass.Corvette, Config.CorvetteShare),
                     (ShipClass.Frigate, Config.FrigateShare),
                     (ShipClass.Cruiser, Config.CruiserShare),
                 })
        {
            var current = counts[(int)ship] * ShipStats.Supply(ship) / (float)total;
            var gap = share - current;
            if (gap > bestGap)
            {
                bestGap = gap;
                best = ship;
            }
        }
        return best;
    }
}
