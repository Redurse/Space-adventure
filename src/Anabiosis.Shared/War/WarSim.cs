namespace Anabiosis.Shared.War;

// The war, step by step. It knows nothing of ships in a single system or of the player: it moves ore, builds things and
// moves fleets along lanes of the galaxy graph. Step takes any amount of time (in pieces of at most MaxStepSeconds), so the
// same code runs in real time inside the game and for hours at a stretch in a fast-forward.
public sealed partial class WarSim
{
    public WarGalaxy Galaxy { get; }
    public WarConfig Config { get; }
    public double Clock { get; private set; }

    private readonly WarNode[] _nodes;
    private readonly List<WarMover> _movers = new();
    private int _nextMoverId = 1;
    private double _governmentClock;
    private double _logisticsClock;

    public IReadOnlyList<WarNode> Nodes => _nodes;
    public IReadOnlyList<WarMover> Movers => _movers;
    public int CapitalIndex { get; private set; } = -1;

    // Running totals, for the report.
    public double OreMined { get; private set; }
    public double OreWasted { get; private set; }

    public WarSim(WarGalaxy galaxy, WarConfig? config = null)
    {
        Galaxy = galaxy;
        Config = config ?? new WarConfig();
        _nodes = Enumerable.Range(0, galaxy.Count).Select(i => new WarNode(i)).ToArray();
        SeedUnion();
        if (Config.VirusEnabled)
            SeedVirus();
        if (Config.MotherEnabled)
            SeedMother();
        SeedPrecursor();
    }

    public void Step(double seconds)
    {
        var remaining = seconds;
        while (remaining > 1e-9)
        {
            var dt = Math.Min(remaining, Config.MaxStepSeconds);
            Advance(dt);
            remaining -= dt;
        }
    }

    private void Advance(double dt)
    {
        Clock += dt;
        Produce(dt);
        StepWorkshops(dt);
        StepMovers(dt);
        StepYards(dt);
        StepProjects(dt);
        StepFleets(dt);
        StepMother(dt);
        StepCombat(dt);
        StepVirusHunting();
        StepFortRegeneration(dt);

        _governmentClock += dt;
        if (_governmentClock >= Config.GovernmentIntervalSeconds)
        {
            _governmentClock = 0;
            RunGovernment();
        }
        _militaryClock += dt;
        if (_militaryClock >= Config.MilitaryIntervalSeconds)
        {
            _militaryClock = 0;
            RunMilitary();
        }
        _logisticsClock += dt;
        if (_logisticsClock >= Config.LogisticsIntervalSeconds)
        {
            _logisticsClock = 0;
            DispatchFreighters();
        }
    }

    // The Union at the start of the war: a capital with a megayard, colonies and yards around it, outposts on the rest of
    // the first few dozen systems, and some freighters. Everything else is unclaimed.
    private void SeedUnion()
    {
        CapitalIndex = ChooseCapital();
        var order = Enumerable.Range(0, Galaxy.Count)
            .OrderBy(i => Galaxy.Hops(CapitalIndex, i))
            .ThenBy(i => Galaxy.PathLength(CapitalIndex, i))
            .Take(Config.StartingUnionSystems)
            .ToList();

        var colonies = new List<int>();
        foreach (var index in order)
        {
            var node = _nodes[index];
            node.Owner = WarFaction.Union;
            node.Outpost = true;
            if (index == CapitalIndex)
            {
                node.Planet = PlanetKind.Capital;
                node.Yard = YardTier.MegaYard;
                node.Stock = 6000f;
            }
            else if (colonies.Count < Config.StartingColonies && Galaxy.Systems[index].HasRockyPlanet)
            {
                node.Planet = PlanetKind.Colony;
                colonies.Add(index);
                node.Stock = 300f;
            }
            else
            {
                node.Stock = 100f;
            }
            var system = Galaxy.Systems[index];
            node.Miners = MinerSlots(system) / 2;
            node.Drones = DroneSlots(system) / 2;
        }

        // Yards on colonies spread out from each other, so the first ones cover different parts of the territory.
        var yards = new List<int> { CapitalIndex };
        foreach (var colony in colonies)
        {
            if (yards.Count > Config.StartingYards)
                break;
            if (yards.All(y => Galaxy.Hops(y, colony) >= 3))
            {
                _nodes[colony].Yard = YardTier.Yard;
                _nodes[colony].Stock = 800f;
                yards.Add(colony);
            }
        }

        foreach (var index in order)
            _nodes[index].FortHp = FortMax(_nodes[index]);

        var freighters = Math.Max(Config.MinFreighters, order.Count / 3);
        for (var i = 0; i < freighters; i++)
            SpawnMover(ShipClass.Freighter, yards[i % yards.Count]);
    }

    // The system nearest the middle of the galaxy that has a rocky planet for the fortress world.
    private int ChooseCapital()
    {
        if (Config.CapitalSystemId is { } id && Galaxy.TryIndexOf(id, out var chosen))
            return chosen;
        var cx = Galaxy.Systems.Average(s => s.X);
        var cy = Galaxy.Systems.Average(s => s.Y);
        return Galaxy.Systems
            .Where(s => s.HasRockyPlanet)
            .OrderBy(s => (s.X - cx) * (s.X - cx) + (s.Y - cy) * (s.Y - cy))
            .First().Index;
    }

    private WarMover SpawnMover(ShipClass shipClass, int node)
    {
        var mover = new WarMover { Id = _nextMoverId++, Class = shipClass, Owner = WarFaction.Union, Node = node };
        _movers.Add(mover);
        return mover;
    }

    public int MinerSlots(WarSystem system) => Math.Max(1, (int)MathF.Round(Config.MinerSlotsBase + system.Richness * Config.MinerSlotsPerRichness));
    public int DroneSlots(WarSystem system) => Math.Max(1, (int)MathF.Round(Config.DroneSlotsBase + system.Richness * Config.DroneSlotsPerRichness));
}
