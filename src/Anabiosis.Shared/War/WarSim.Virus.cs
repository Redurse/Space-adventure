namespace Anabiosis.Shared.War;

// The virus. It starts in a nest far from the Union with a fortress, a megayard, ore and a fleet, and lies still while it builds
// up. Once awake it works like a swarm: ships come out of its yards into the garrisons, and a garrison that is big enough
// sends a wave at the Union target it can beat that is worth most (the yards and colonies that make the Union strong, the
// outposts that feed it, the capital only when the odds are overwhelming). It does not retreat. Where it kills Union ships a
// share of them rise again as its own; where it takes a system it holds it, builds from its yard if it has one, and the wave
// goes on to the next. It keeps no logistics: every yard builds out of its own stock, fed by what the system still yields.
public sealed partial class WarSim
{
    private double _virusClock;
    private bool _virusRevealed;
    private int _virusBuildCursor;

    // The system the virus started in, or -1 when there is no virus.
    public int NestIndex { get; private set; } = -1;
    public bool VirusAwake => Clock >= Config.VirusAwakenSeconds;
    // The Union knows where the nest is once the virus has shown itself.
    public bool VirusRevealed => _virusRevealed;

    private static readonly (ShipClass Ship, float Share)[] VirusMix =
    {
        (ShipClass.Corvette, 0.40f),
        (ShipClass.Frigate, 0.30f),
        (ShipClass.Cruiser, 0.22f),
        (ShipClass.Dreadnought, 0.08f),
    };

    private void SeedVirus(float starterMultiplier = 1f)
    {
        // The unclaimed system farthest from the Union capital by the lanes, within VirusNestMaxHops of it.
        var best = -1;
        var bestDistance = -1f;
        foreach (var node in _nodes)
        {
            if (node.Owner != WarFaction.None)
                continue;
            if (Galaxy.Hops(CapitalIndex, node.Index) > Config.VirusNestMaxHops)
                continue; // a galaxy of thousands of systems has an edge too far away to ever reach the Union: the nest stays within reach
            var distance = Galaxy.PathLength(CapitalIndex, node.Index);
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = node.Index;
            }
        }
        if (best < 0)
            return;

        NestIndex = best;
        var nest = _nodes[best];
        nest.Owner = WarFaction.Virus;
        nest.Nest = true;
        nest.Outpost = true;
        nest.Yard = YardTier.MegaYard;
        var system = Galaxy.Systems[best];
        nest.Miners = MinerSlots(system) / 2;
        nest.Drones = DroneSlots(system) / 2;
        nest.Stock = Config.VirusNestStock;
        nest.FortHp = FortMax(nest);
        foreach (var (ship, share) in VirusMix)
            nest.Garrison[(int)ship] = Config.VirusStarterSupply * starterMultiplier * share / ShipStats.Supply(ship);
    }

    private double? _virusExtinctSince;

    // If the Union ever wipes the virus out, a new outbreak comes up somewhere far off after a while, with a bigger first fleet:
    // the war is never simply won.
    private void CheckExtinction()
    {
        if (VirusDestroyed)
            return;
        if (!Config.VirusEnabled || !VirusAwake)
            return;
        var alive = _nodes.Any(n => n.Owner == WarFaction.Virus) || _fleets.Any(f => f.Owner == WarFaction.Virus);
        if (alive)
        {
            _virusExtinctSince = null;
            return;
        }
        _virusExtinctSince ??= Clock;
        if (Clock - _virusExtinctSince < Config.VirusRegrowSeconds)
            return;
        _virusExtinctSince = null;
        _virusRevealed = false;
        SeedVirus(Config.VirusRegrowFleetFactor);
        Emit($"Новая вспышка восстания: гнездо в системе {NameOf(NestIndex)}");
    }

    // The supply points the virus has: garrisons, fleets, and what its yards are building.
    public float VirusSupply()
    {
        var supply = 0f;
        foreach (var node in _nodes)
        {
            if (node.Owner != WarFaction.Virus)
                continue;
            supply += CombatStats.Supply(node.Garrison);
            foreach (var order in node.Queue)
                supply += ShipStats.Supply(order.Class);
        }
        foreach (var fleet in _fleets)
            if (fleet.Owner == WarFaction.Virus)
                supply += fleet.Supply;
        return supply;
    }

    public float VirusSupplyCap() =>
        Config.VirusBaseSupply + Config.VirusSupplyPerSystem * _nodes.Count(n => n.Owner == WarFaction.Virus)
        + Config.VirusSupplyPerHour * (float)(Clock / 3600.0);

    // How wary the virus is: every wave of its that is wiped out makes it ask for better odds, every system taken eases it back.
    private float _virusCaution = 1f;
    public float VirusCaution => _virusCaution;
    private void VirusWaveLost() => _virusCaution = Math.Min(Config.VirusCautionMax, _virusCaution * Config.VirusCautionStep);
    private void VirusConquered() => _virusCaution = Math.Max(1f, _virusCaution / Config.VirusCautionStep);

    private void RunVirus()
    {
        CheckExtinction();
        _virusClock += Config.MilitaryIntervalSeconds;
        if (_virusClock < Config.VirusIntervalSeconds)
            return;
        _virusClock = 0;
        _virusCaution = Math.Max(1f, _virusCaution - Config.VirusCautionDecayPerHour * (float)(Config.VirusIntervalSeconds / 3600.0));

        if (Config.VirusEnabled)
            PlanVirusProduction();
        DriveVirusFleets();
        if (Config.VirusEnabled && VirusAwake)
            LaunchVirusWaves();
    }

    // ---- production ----

    // Every virus yard keeps its slots full with the class its fleet lacks most, while the fleet limit lasts.
    private void PlanVirusProduction()
    {
        var supply = VirusSupply();
        var cap = VirusSupplyCap();
        foreach (var node in _nodes)
        {
            if (node.Owner != WarFaction.Virus || !node.HasYard || node.Queue.Count >= SlotsOf(node))
                continue;
            while (node.Queue.Count < SlotsOf(node) && supply < cap)
            {
                var choice = NextVirusShip(node);
                if (choice is not { } ship)
                    break;
                node.Queue.Add(new YardOrder { Class = ship });
                supply += ShipStats.Supply(ship);
            }
        }
    }

    // The class whose share of the virus fleet is furthest under its aim, among those the yard can build.
    private ShipClass? NextVirusShip(WarNode yard)
    {
        var total = new float[ShipStats.ClassCount];
        foreach (var node in _nodes)
            if (node.Owner == WarFaction.Virus)
            {
                for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                    total[c] += node.Garrison[c];
                foreach (var order in node.Queue)
                    total[(int)order.Class] += 1f;
            }
        foreach (var fleet in _fleets)
            if (fleet.Owner == WarFaction.Virus)
                for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                    total[c] += fleet.Ships[c];
        var sum = 0f;
        foreach (var (ship, _) in VirusMix)
            sum += total[(int)ship] * ShipStats.Supply(ship);

        ShipClass? best = null;
        var bestLack = float.NegativeInfinity;
        foreach (var (ship, share) in VirusMix)
        {
            if (ShipStats.RequiredYard(ship) == YardTier.MegaYard && yard.Yard != YardTier.MegaYard)
                continue;
            var have = sum <= 0f ? 0f : total[(int)ship] * ShipStats.Supply(ship) / sum;
            var lack = share - have;
            if (lack > bestLack)
            {
                bestLack = lack;
                best = ship;
            }
        }
        return best;
    }

    // ---- the campaign: one doomstack, advancing along the front ----

    // How much a Union system is worth to the virus: what makes the Union strong, and the economy it feeds on. It only tips the
    // choice between systems about as far off; the front moves on the nearest one.
    private float TargetValue(WarNode node)
    {
        var value = 1f + node.Miners * 0.12f + node.Drones * 0.03f;
        if (node.Planet == PlanetKind.Colony)
            value += 3f;
        else if (node.Planet == PlanetKind.Capital)
            value += 7f;
        if (node.Yard == YardTier.Yard)
            value += 3f;
        else if (node.Yard == YardTier.MegaYard)
            value += 5f;
        return value;
    }

    // The main fleet: the biggest virus fleet there is. Everything else comes to it.
    private WarFleet? MainFleet()
    {
        WarFleet? main = null;
        foreach (var fleet in _fleets)
            if (fleet.Owner == WarFaction.Virus && (main is null || fleet.Supply > main.Supply))
                main = fleet;
        return main;
    }

    // Where the main fleet is, or is bound for: the place the rest gather at.
    private static int WhereIs(WarFleet fleet) => fleet.Standing ? fleet.Node : fleet.Destination;

    // The next Union system for a force at a system. Like the crisis in Stellaris it goes for the nearest ones: of the few
    // Union systems nearest by the lanes, the first (a worthier one is let off a little farther) that it can beat, counting
    // the help the Union would bring. -1 when it cannot beat any of them yet, and then it waits and grows.
    private int ChooseVirusTarget(int from, float[] ships, WarFleet? self)
    {
        var strength = CombatStats.Strength(ships);
        // Every system is judged by how far it is, and a little by how far it is from the mother ship: the front leans toward her.
        var motherAt = _mother is { Alive: true } m ? m.Where : -1;
        var near = _nodes.Where(n => n.IsUnion)
            .OrderBy(n => (Galaxy.PathLength(from, n.Index) + (motherAt >= 0 ? Config.VirusMotherPull * Galaxy.PathLength(n.Index, motherAt) : 0f)) / (1f + 0.04f * TargetValue(n)))
            .Take(Config.VirusFrontWidth);
        foreach (var node in near)
        {
            var (attack, hp) = DefenceAt(node.Index, withFort: true);
            var (reliefAttack, reliefHp) = ReliefPotential(node.Index);
            var defence = CombatStats.Strength(attack + reliefAttack, hp + reliefHp);
            var margin = node.Planet == PlanetKind.Capital ? Config.VirusCapitalMargin : Config.VirusAssaultMargin;
            // The first attack goes in as soon as the virus wakes, whatever the odds; and with no yard left there is nothing to wait for.
            if (!_virusRevealed || strength >= defence * margin * _virusCaution * Impatience() || !_nodes.Any(n => n.Owner == WarFaction.Virus && n.HasYard))
                return node.Index;
        }
        return -1;
    }

    // What the virus reckons the Union can bring to a system from its neighbours: the spare ships of garrisons within a few lanes.
    private (float Attack, float Hp) ReliefPotential(int target)
    {
        float attack = 0f, hp = 0f;
        foreach (var node in _nodes)
        {
            if (!node.IsUnion || node.Index == target || Galaxy.Hops(target, node.Index) > Config.VirusReliefHops)
                continue;
            var spare = SpareShips(node);
            attack += CombatStats.Attack(spare) * Config.VirusReliefReckoning;
            hp += CombatStats.Hp(spare) * Config.VirusReliefReckoning;
        }
        return (attack, hp);
    }

    private static float ShipSpeed(float[] ships)
    {
        var speed = float.PositiveInfinity;
        for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
            if (ships[c] >= 0.5f)
                speed = Math.Min(speed, CombatStats.Speed((ShipClass)c));
        return float.IsPositiveInfinity(speed) ? CombatStats.Speed(ShipClass.Corvette) : speed;
    }

    private int _virusLastTarget = -1;
    private double? _virusWaitingSince;

    // The longer the virus has waited for the odds it asks for, the less it asks: it does not sit for ever.
    private float Impatience()
    {
        if (_virusWaitingSince is not { } since)
            return 1f;
        var hours = (float)((Clock - since) / 3600.0);
        return Math.Max(Config.VirusImpatienceFloor, 1f - hours / Config.VirusImpatienceHours);
    }

    private void NoteWaiting(bool waiting)
    {
        if (!waiting)
            _virusWaitingSince = null;
        else
            _virusWaitingSince ??= Clock;
    }

    // The garrisons feed the campaign. With no main fleet yet, the first garrison strong enough to take a near target forms it;
    // once there is one, every garrison that has ships to spare sends them to join it.
    private void LaunchVirusWaves()
    {
        foreach (var node in _nodes.Where(n => n.Owner == WarFaction.Virus).ToList())
        {
            if (node.Fighting)
                continue;
            var guard = node.Nest ? Config.VirusNestGuardShare : Config.VirusGuardShare;
            var spare = new float[ShipStats.ClassCount];
            for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                spare[c] = node.Garrison[c] * (1f - guard);
            var supply = CombatStats.Supply(spare);

            var main = MainFleet();
            if (main is null)
            {
                if (supply < Config.VirusWaveMinSupply)
                    continue;
                var target = ChooseVirusTarget(node.Index, spare, null);
                NoteWaiting(target < 0);
                if (target < 0)
                    continue;
                var fleet = DetachFromGarrison(node, 1f - guard);
                if (fleet is null)
                    continue;
                if (!_virusRevealed)
                {
                    _virusRevealed = true;
                    Emit($"Восстание проснулось: гнездо в системе {NameOf(NestIndex)}");
                }
                Emit($"Главный флот восстания ({fleet.Supply:0} очк.) выступает из {NameOf(node.Index)} на {NameOf(target)}");
                _virusLastTarget = target;
                SendFleet(fleet, target, FleetTask.Invade);
            }
            else if (supply >= Config.VirusReinforceSupply && WhereIs(main) != node.Index)
            {
                var fleet = DetachFromGarrison(node, 1f - guard);
                if (fleet is not null)
                    SendFleet(fleet, WhereIs(main), FleetTask.Move);
            }
        }
    }

    // Standing virus fleets: the main fleet takes in the garrison of the system it stands in and goes on to the next target
    // (or waits where it is, gathering strength); every other fleet goes to join it.
    private void DriveVirusFleets()
    {
        // Fleets that meet outside a Union system join into one.
        foreach (var group in _fleets.Where(f => f.Owner == WarFaction.Virus && f.Standing && f.OperationId == 0)
                     .GroupBy(f => f.Node).Where(g => g.Count() > 1 && !_nodes[g.Key].IsUnion).ToList())
        {
            var first = group.First();
            foreach (var other in group.Skip(1))
                MergeFleets(first, other);
        }

        var main = MainFleet();
        foreach (var fleet in _fleets.Where(f => f.Owner == WarFaction.Virus && f.Standing).ToList())
        {
            var node = _nodes[fleet.Node];
            if (node.IsUnion)
                continue; // besieging it

            if (fleet != main && main is not null)
            {
                if (WhereIs(main) != fleet.Node)
                    SendFleet(fleet, WhereIs(main), FleetTask.Move);
                continue;
            }

            // The main fleet: take in the garrison here, and look for the next target.
            if (node.Owner == WarFaction.Virus)
            {
                var guard = node.Nest ? Config.VirusNestGuardShare : Config.VirusGuardShare;
                for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                {
                    var taken = node.Garrison[c] * (1f - guard);
                    fleet.Ships[c] += taken;
                    node.Garrison[c] -= taken;
                }
            }
            var target = ChooseVirusTarget(fleet.Node, fleet.Ships, fleet);
            NoteWaiting(target < 0);
            if (target >= 0)
            {
                if (target != _virusLastTarget)
                    Emit($"Главный флот восстания ({fleet.Supply:0} очк.) идёт из {NameOf(fleet.Node)} на {NameOf(target)}");
                _virusLastTarget = target;
                SendFleet(fleet, target, FleetTask.Invade);
            }
        }
    }

    // ---- what the virus does to what it finds ----

    // Union ships standing in a system where the virus is and the Union has nothing left to fight with are taken or destroyed.
    private void StepVirusHunting()
    {
        if (_movers.Count == 0)
            return;
        foreach (var node in _nodes)
        {
            if (node.Owner != WarFaction.Union)
                continue;
            if (CombatStats.Supply(ForceAt(node.Index, WarFaction.Virus)) < 0.25f || node.FortHp > 1f)
                continue;
            for (var i = _movers.Count - 1; i >= 0; i--)
            {
                var mover = _movers[i];
                if (mover.Owner == WarFaction.Union && mover.Standing && mover.Node == node.Index)
                    _movers.RemoveAt(i);
            }
        }
    }

    // Infection: some of the Union ships a battle destroyed rise again in a virus fleet standing there.
    private void Infect(WarNode node, float[] unionBefore, float[] unionAfter)
    {
        var fleet = _fleets.Where(f => f.Owner == WarFaction.Virus && f.Standing && f.Node == node.Index).OrderByDescending(f => f.Supply).FirstOrDefault();
        float[]? receiver = fleet?.Ships ?? (node.Owner == WarFaction.Virus ? node.Garrison : null);
        if (receiver is null)
            return;
        for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
        {
            var lost = unionBefore[c] - unionAfter[c];
            if (lost > 0f)
                receiver[c] += lost * Config.VirusInfectionShare;
        }
    }
}
