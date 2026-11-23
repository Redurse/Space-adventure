namespace Anabiosis.Shared.War;

// The Union military doctrine, as orders given every ten seconds of war time:
//   - in peace the warships are spread over the systems in proportion to what each is worth to lose (the capital most,
//     megayards and yards, colonies, outposts least), so every work stands under guard;
//   - when an enemy fleet is making for a system, the garrison alone is compared with it; if it is not enough, ships are
//     taken from the other garrisons nearest first, gathered at a rally system next to the threatened one, and sent in
//     together once they are strong enough - never piecemeal;
//   - when even all of them together would lose, nothing is thrown away: the garrison of the doomed system withdraws to a
//     safe yard, the rest are kept, and the fleet is rebuilt;
//   - once the enemy is no longer coming, the force at hand takes back what was lost if it is clearly stronger than the
//     enemy there.
public sealed partial class WarSim
{
    private sealed class Operation
    {
        public int Id { get; init; }
        public int Target { get; init; }
        public int Rally { get; init; }
        public bool Counter { get; set; }
        public bool Striking { get; set; }
        public double Started { get; init; }
        public float EnemyStrength { get; set; }
    }

    private sealed class Threat
    {
        public float Attack;
        public float Hp;
        public float Eta = float.PositiveInfinity;
        public float Strength => CombatStats.Strength(Attack, Hp);
    }

    private readonly List<Operation> _operations = new();
    private int _nextOperationId = 1;
    private double _militaryClock;
    private readonly HashSet<int> _abandoned = new();

    // Set by whoever wants to follow the war: a line for every notable thing that happens.
    public Action<string>? OnEvent { get; set; }

    public int OperationCount => _operations.Count;

    // For the fast-forward report: what every running operation is doing.
    public IEnumerable<string> DescribeOperations()
    {
        foreach (var op in _operations)
        {
            var members = _fleets.Where(f => f.OperationId == op.Id).ToList();
            var where = string.Join("; ", members.Select(f => $"{(f.Standing ? "стоит " + NameOf(f.Node) : "идёт в " + NameOf(f.Destination))} {f.Supply:0} {f.Task}"));
            yield return $"оп.{op.Id} цель {NameOf(op.Target)} ({_nodes[op.Target].Owner}, форт {_nodes[op.Target].FortHp:0}) сбор {NameOf(op.Rally)} контр={op.Counter} удар={op.Striking} враг {op.EnemyStrength:0}: {where}";
        }
    }
    public bool AtWar => _fleets.Any(f => f.Owner == WarFaction.Virus) || _nodes.Any(n => n.Owner == WarFaction.Virus && !n.Nest);

    private void Emit(string text) => OnEvent?.Invoke($"[{Clock / 3600.0,6:0.00} ч] {text}");
    private string NameOf(int node) => Galaxy.Systems[node].Name;

    private void RunMilitary()
    {
        var threats = CollectThreats();
        ManageOperations(threats);
        PlanRetreats(threats);
        if (!AtWar && _operations.Count == 0)
            DistributeGarrisons();
        RunMother();
        RunVirus();
    }

    // ---- what is coming ----

    // Every enemy fleet counted against the Union system it is standing in or making for.
    private Dictionary<int, Threat> CollectThreats()
    {
        var threats = new Dictionary<int, Threat>();
        foreach (var fleet in _fleets)
        {
            if (fleet.Owner != WarFaction.Virus)
                continue;
            var target = fleet.Standing ? fleet.Node : fleet.Destination;
            if (target < 0 || _nodes[target].Owner != WarFaction.Union)
                continue;
            if (!threats.TryGetValue(target, out var threat))
                threats[target] = threat = new Threat();
            threat.Attack += CombatStats.Attack(fleet.Ships);
            threat.Hp += CombatStats.Hp(fleet.Ships);
            threat.Eta = Math.Min(threat.Eta, SecondsToDestination(fleet));
        }
        return threats;
    }

    // What the Union has in a system to fight with: the ships standing there, and the works if they are its own.
    private (float Attack, float Hp) DefenceAt(int index, bool withFort)
    {
        var ships = ForceAt(index, WarFaction.Union);
        var attack = CombatStats.Attack(ships);
        var hp = CombatStats.Hp(ships);
        if (withFort && _nodes[index].Owner == WarFaction.Union)
        {
            attack += FortAttack(_nodes[index]);
            hp += _nodes[index].FortHp;
        }
        return (attack, hp);
    }

    // What the enemy has in a system: ships, and the works if the system is theirs.
    private (float Attack, float Hp) EnemyAt(int index)
    {
        var ships = ForceAt(index, WarFaction.Virus);
        var attack = CombatStats.Attack(ships);
        var hp = CombatStats.Hp(ships);
        if (_nodes[index].Owner == WarFaction.Virus)
        {
            attack += FortAttack(_nodes[index]);
            hp += _nodes[index].FortHp;
        }
        return (attack, hp);
    }

    // ---- operations: relief of a threatened system, and counterattacks ----

    private void ManageOperations(Dictionary<int, Threat> threats)
    {
        // Operations that are over, and the figures of those that go on.
        foreach (var op in _operations.ToList())
        {
            // An operation whose ships are all gone has nothing left to do.
            if (Clock - op.Started > 120.0 && !_fleets.Any(f => f.OperationId == op.Id))
            {
                EndOperation(op, "флот потерян");
                continue;
            }
            if (_nodes[op.Target].Owner == WarFaction.Virus && !op.Counter)
                op.Counter = true;
            if (op.Counter)
            {
                if (_nodes[op.Target].Owner != WarFaction.Virus)
                {
                    EndOperation(op, "система возвращена");
                    continue;
                }
                var (attack, hp) = EnemyAt(op.Target);
                op.EnemyStrength = CombatStats.Strength(attack, hp);
            }
            else
            {
                if (!threats.TryGetValue(op.Target, out var threat))
                {
                    EndOperation(op, "угроза миновала");
                    continue;
                }
                op.EnemyStrength = threat.Strength;
            }
        }

        // A strike force that is gone (beaten, or merged away) leaves late arrivals standing at the rally: gather again.
        foreach (var op in _operations.Where(o => o.Striking).ToList())
        {
            var members = _fleets.Where(f => f.OperationId == op.Id).ToList();
            if (members.Count > 0 && members.All(f => f.Standing && f.Node != op.Target))
                op.Striking = false;
        }
        foreach (var op in _operations.Where(o => !o.Striking).ToList())
            ControlStrike(op);

        // New relief operations, the most valuable systems first.
        foreach (var (target, threat) in threats.OrderByDescending(t => GarrisonWeight(_nodes[t.Key])))
        {
            if (_operations.Any(o => o.Target == target))
                continue;
            PlanOperation(target, threat.Attack, threat.Hp, counter: false, threats);
        }

        // Counterattacks, only when nothing is coming at us.
        if (threats.Count == 0 && _operations.Count < 2)
            foreach (var node in _nodes.Where(n => n.Owner == WarFaction.Virus && (!n.Nest || _virusRevealed)).OrderBy(n => Galaxy.PathLength(CapitalIndex, n.Index)))
            {
                if (_operations.Any(o => o.Target == node.Index))
                    continue;
                var (attack, hp) = EnemyAt(node.Index);
                if (PlanOperation(node.Index, attack, hp, counter: true, threats))
                    break;
            }
    }

    // Plans a relief or a counterattack against one system. Returns whether an operation was started.
    private bool PlanOperation(int target, float enemyAttack, float enemyHp, bool counter, Dictionary<int, Threat> threats)
    {
        var enemyStrength = CombatStats.Strength(enemyAttack, enemyHp);
        var required = enemyStrength * (counter ? Config.CounterMargin : Config.ReliefMargin);
        var (localAttack, localHp) = counter ? (0f, 0f) : DefenceAt(target, withFort: true);
        if (!counter && CombatStats.Strength(localAttack, localHp) >= required)
            return false; // what is there holds it alone

        // The ships that can be spared, nearest first.
        var donors = new List<(int Node, float Eta)>();
        foreach (var node in _nodes)
        {
            if (!node.IsUnion || node.Index == target || threats.ContainsKey(node.Index) || node.Fighting)
                continue;
            var spare = SpareShips(node);
            if (CombatStats.Supply(spare) < 0.5f)
                continue;
            donors.Add((node.Index, TravelSeconds(spare, node.Index, target)));
        }
        donors.Sort((a, b) => a.Eta.CompareTo(b.Eta));

        var chosen = new List<int>();
        var attack = localAttack;
        var hp = localHp;
        foreach (var donor in donors)
        {
            var spare = SpareShips(_nodes[donor.Node]);
            chosen.Add(donor.Node);
            attack += CombatStats.Attack(spare);
            hp += CombatStats.Hp(spare);
            if (CombatStats.Strength(attack, hp) >= required)
                break;
        }

        var reached = CombatStats.Strength(attack, hp);
        if (reached < required)
        {
            if (counter)
                return false; // not yet: the fleet keeps growing
            if (reached < enemyStrength * Config.ReliefFloor)
            {
                // Not even all of it together would win: save the ships.
                if (_abandoned.Add(target))
                    Emit($"{NameOf(target)}: врага не одолеть (сила врага {enemyStrength:0}, у нас {reached:0}) — флот не бросаем, гарнизон отходит");
                return false;
            }
            // About level or better: everything goes, and the works fight beside it.
        }
        if (chosen.Count == 0)
            return false;

        var rally = ChooseRally(target, chosen, counter);
        var op = new Operation { Id = _nextOperationId++, Target = target, Rally = rally, Counter = counter, Started = Clock, EnemyStrength = enemyStrength };
        _operations.Add(op);
        foreach (var donor in chosen)
        {
            var fleet = DetachFromGarrison(_nodes[donor], 1f - Config.HomeGuardShare);
            if (fleet is null)
                continue;
            fleet.OperationId = op.Id;
            SendFleet(fleet, rally, FleetTask.Move);
        }
        Emit($"{(counter ? "Контрудар на" : "Подмога к")} {NameOf(target)}: сбор у {NameOf(rally)}, {chosen.Count} гарнизонов, нужно {required:0}, соберём {reached:0}");
        return true;
    }

    // The ships of a garrison that can be taken without leaving it bare.
    private float[] SpareShips(WarNode node)
    {
        var spare = new float[ShipStats.ClassCount];
        for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
            spare[c] = node.Garrison[c] * (1f - Config.HomeGuardShare);
        return spare;
    }

    // Where the relief gathers: a quiet Union system next to the target that the ships reach soonest. For a counterattack, the
    // Union system nearest the target. With nowhere better the ships go straight to the target.
    private int ChooseRally(int target, List<int> donors, bool counter)
    {
        // Where enemy fleets are standing or heading: the rally must keep a lane clear of all of them.
        var enemyPlaces = _fleets.Where(f => f.Owner == WarFaction.Virus).Select(f => f.Standing ? f.Node : f.Destination).Where(i => i >= 0).Distinct().ToList();
        var candidates = _nodes.Where(n => n.IsUnion && !n.Fighting && n.Index != target
                && (counter ? Galaxy.Hops(n.Index, target) <= 6 : Galaxy.Hops(n.Index, target) is >= 1 and <= 3)
                && enemyPlaces.All(e => Galaxy.Hops(e, n.Index) >= 2))
            .Select(n => n.Index);
        var best = -1;
        var bestCost = float.PositiveInfinity;
        foreach (var candidate in candidates)
        {
            var cost = counter
                ? Galaxy.PathLength(candidate, target)
                : donors.Max(d => Galaxy.PathLength(d, candidate)) + 0.5f * Galaxy.PathLength(candidate, target);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = candidate;
            }
        }
        return best >= 0 ? best : target;
    }

    // Sends the gathered fleets in together once they are strong enough, or once all have arrived, or when waiting too long.
    private void ControlStrike(Operation op)
    {
        var members = _fleets.Where(f => f.OperationId == op.Id).ToList();
        var waiting = members.Where(f => f.Standing && f.Node == op.Rally).ToList();
        if (waiting.Count == 0)
            return;
        if (op.Rally == op.Target)
        {
            op.Striking = true;
            return;
        }

        var attack = waiting.Sum(f => CombatStats.Attack(f.Ships));
        var hp = waiting.Sum(f => CombatStats.Hp(f.Ships));
        if (!op.Counter)
        {
            var (localAttack, localHp) = DefenceAt(op.Target, withFort: true);
            attack += localAttack;
            hp += localHp;
        }
        var strong = CombatStats.Strength(attack, hp) >= op.EnemyStrength * Config.StrikeMargin;
        var allHere = members.All(f => f.Standing && f.Node == op.Rally);
        if (!strong && !allHere && Clock - op.Started < 1800.0)
            return;
        if (!strong && op.Counter)
        {
            // A counterattack is only worth it with the odds: otherwise the force goes home and the fleet keeps growing.
            EndOperation(op, "сил недостаточно, отложено");
            return;
        }

        var strike = waiting[0];
        foreach (var other in waiting.Skip(1))
            MergeFleets(strike, other);
        Emit($"Удар по {NameOf(op.Target)}: флот {strike.Supply:0} очк. выходит из {NameOf(op.Rally)}");
        SendFleet(strike, op.Target, FleetTask.Strike);
        op.Striking = true;
    }

    private void EndOperation(Operation op, string why)
    {
        _operations.Remove(op);
        Emit($"Операция против {NameOf(op.Target)} окончена: {why}");
        foreach (var fleet in _fleets.Where(f => f.OperationId == op.Id).ToList())
        {
            fleet.OperationId = 0;
            if (!fleet.Standing)
            {
                fleet.Task = FleetTask.Move;
                continue;
            }
            if (_nodes[fleet.Node].Owner == WarFaction.Union)
                JoinGarrison(fleet);
            else
                SendFleet(fleet, HavenFor(fleet.Node, new Dictionary<int, Threat>()), FleetTask.Move);
        }
    }

    // ---- withdrawing from a lost fight ----

    private void PlanRetreats(Dictionary<int, Threat> threats)
    {
        foreach (var node in _nodes)
        {
            if (!node.IsUnion || !node.Fighting)
                continue;
            var (ownAttack, ownHp) = DefenceAt(node.Index, withFort: true);
            var enemyShips = ForceAt(node.Index, WarFaction.Virus);
            var enemy = CombatStats.Strength(enemyShips);
            var overmatched = CombatStats.Strength(ownAttack, ownHp) < enemy * Config.RetreatRatio;
            if (!overmatched && !_abandoned.Contains(node.Index))
                continue;

            // Help on its way in time is reason to hold.
            var enemyAttack = CombatStats.Attack(enemyShips);
            var holdsFor = enemyAttack <= 1f ? float.PositiveInfinity : ownHp / enemyAttack * (float)Config.CombatRoundSeconds;
            var reliefEta = _fleets.Where(f => f.Owner == WarFaction.Union && f.Task == FleetTask.Strike && f.Destination == node.Index)
                .Select(SecondsToDestination).DefaultIfEmpty(float.PositiveInfinity).Min();
            if (reliefEta < holdsFor * 0.7f)
                continue;

            var haven = HavenFor(node.Index, threats);
            if (haven == node.Index)
                continue;
            var retreat = DetachFromGarrison(node, 1f);
            foreach (var fleet in _fleets.Where(f => f.Owner == WarFaction.Union && f.Standing && f.Node == node.Index && f != retreat).ToList())
            {
                retreat ??= fleet;
                if (!ReferenceEquals(retreat, fleet))
                    MergeFleets(retreat, fleet);
            }
            if (retreat is null)
                continue;
            retreat.OperationId = 0;
            Emit($"Отход из {NameOf(node.Index)} в {NameOf(haven)}: флот {retreat.Supply:0} очк. уходит, чтобы не погибнуть (враг сильнее: {enemy:0} против {CombatStats.Strength(ownAttack, ownHp):0})");
            SendFleet(retreat, haven, FleetTask.Retreat);
            _abandoned.Remove(node.Index);
        }
    }

    // The yard to fall back on: the nearest Union yard that is quiet and not itself the aim of an enemy.
    private int HavenFor(int from, Dictionary<int, Threat> threats)
    {
        var best = -1;
        var bestDistance = float.PositiveInfinity;
        foreach (var node in _nodes)
        {
            if (!node.IsUnion || !node.HasYard || node.Index == from || node.Fighting || threats.ContainsKey(node.Index))
                continue;
            var distance = Galaxy.PathLength(from, node.Index);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = node.Index;
            }
        }
        return best >= 0 ? best : from;
    }

    // ---- peacetime: every work under guard in proportion to its worth ----

    private float GarrisonWeight(WarNode node)
    {
        if (!node.IsUnion)
            return 0f;
        var weight = 0f;
        if (node.Planet == PlanetKind.Capital)
            weight += Config.CapitalGarrisonWeight;
        else if (node.Planet == PlanetKind.Colony)
            weight += Config.ColonyGarrisonWeight;
        if (node.Yard == YardTier.MegaYard)
            weight += Config.MegaYardGarrisonWeight;
        else if (node.Yard == YardTier.Yard)
            weight += Config.YardGarrisonWeight;
        if (node.Outpost)
            weight += Config.OutpostGarrisonWeight;
        return weight;
    }

    private void DistributeGarrisons()
    {
        var union = _nodes.Where(n => n.IsUnion).ToList();
        var totalWeight = union.Sum(GarrisonWeight);
        if (totalWeight <= 0f)
            return;
        var inbound = new float[_nodes.Length];
        var total = 0f;
        foreach (var fleet in _fleets)
            if (fleet.Owner == WarFaction.Union && fleet.Task == FleetTask.Move && fleet.Destination >= 0)
            {
                inbound[fleet.Destination] += fleet.Supply;
                total += fleet.Supply;
            }
        foreach (var node in union)
            total += CombatStats.Supply(node.Garrison);

        // The two biggest surpluses move toward the two biggest shortfalls, if the gap is worth the trip.
        var desired = new float[_nodes.Length];
        foreach (var node in union)
            desired[node.Index] = total * GarrisonWeight(node) / totalWeight;
        const float worthMoving = 3f;
        for (var transfer = 0; transfer < 2; transfer++)
        {
            var donor = union.Where(n => CombatStats.Supply(n.Garrison) - desired[n.Index] * 1.25f > worthMoving)
                .OrderByDescending(n => CombatStats.Supply(n.Garrison) - desired[n.Index]).FirstOrDefault();
            var receiver = union.Where(n => desired[n.Index] - CombatStats.Supply(n.Garrison) - inbound[n.Index] > worthMoving)
                .OrderByDescending(n => desired[n.Index] - CombatStats.Supply(n.Garrison) - inbound[n.Index]).FirstOrDefault();
            if (donor is null || receiver is null || donor == receiver)
                return;
            var surplus = CombatStats.Supply(donor.Garrison) - desired[donor.Index];
            var shortfall = desired[receiver.Index] - CombatStats.Supply(receiver.Garrison) - inbound[receiver.Index];
            var amount = Math.Min(Math.Min(surplus, shortfall), 60f);
            var fleet = DetachFromGarrison(donor, amount / CombatStats.Supply(donor.Garrison));
            if (fleet is null)
                return;
            inbound[receiver.Index] += fleet.Supply;
            SendFleet(fleet, receiver.Index, FleetTask.Move);
        }
    }
}
