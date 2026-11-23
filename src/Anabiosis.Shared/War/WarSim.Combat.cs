namespace Anabiosis.Shared.War;

// Battles and the taking of systems. A battle is fought wherever fleets of the two sides, or a fleet and the other side
// fortifications, are in one system: every round each side hits the other with all its attack, and the damage is spread over
// everything the other has there in proportion to hit points, so big fleets wear down together rather than one ship at a time.
public sealed partial class WarSim
{
    private double _combatClock;

    // How many systems have a battle going on right now, for the map.
    public int BattleCount => _nodes.Count(n => n.Fighting);

    private void StepCombat(double dt)
    {
        _combatClock += dt;
        while (_combatClock >= Config.CombatRoundSeconds)
        {
            _combatClock -= Config.CombatRoundSeconds;
            ResolveRound();
        }
        StepOccupation(dt);
    }

    private void ResolveRound()
    {
        if (_mother is not null)
            _mother.Fighting = false;
        foreach (var fleet in _fleets)
            fleet.Fighting = false;

        foreach (var node in _nodes)
        {
            var union = ShipsAt(node.Index, WarFaction.Union);
            var virus = ShipsAt(node.Index, WarFaction.Virus);
            var motherHere = _mother is { Alive: true, Standing: true } mother && mother.Node == node.Index;
            var unionShips = union.Sum(a => CombatStats.Supply(a)) > 0.05f || motherHere;
            var virusShips = virus.Sum(a => CombatStats.Supply(a)) > 0.05f;
            var unionFort = node.Owner == WarFaction.Union && node.FortHp > 1f;
            var virusFort = node.Owner == WarFaction.Virus && node.FortHp > 1f;

            var fighting = (unionShips && (virusShips || virusFort)) || (virusShips && (unionShips || unionFort));
            node.Fighting = fighting;
            if (!fighting)
                continue;
            node.SinceCombat = 0f;

            // Both sides strike with what they have at the start of the round.
            var unionAttack = union.Sum(a => CombatStats.Attack(a)) + (unionFort ? FortAttack(node) : 0f) + (motherHere ? Config.MotherAttack : 0f);
            var virusAttack = virus.Sum(a => CombatStats.Attack(a)) + (virusFort ? FortAttack(node) : 0f);
            var unionBefore = SumShips(union);
            var damageToShips = virusAttack;
            if (motherHere)
            {
                // The mother ship takes its share of the blows in proportion to its hit points, like everything else standing there.
                var m = _mother!;
                var others = union.Sum(a => CombatStats.Hp(a)) + (unionFort ? node.FortHp : 0f);
                var share = m.Hp / Math.Max(1f, m.Hp + others);
                m.Hp -= virusAttack * share;
                damageToShips = virusAttack * (1f - share);
                m.Fighting = true;
                if (m.Hp <= 0f)
                    MotherDestroyed();
            }
            ApplyDamage(node, union, WarFaction.Union, unionFort, damageToShips);
            if (virusShips)
                Infect(node, unionBefore, SumShips(union));
            ApplyDamage(node, virus, WarFaction.Virus, virusFort, unionAttack);

            foreach (var fleet in _fleets)
                if (fleet.Standing && fleet.Node == node.Index)
                    fleet.Fighting = true;
        }

        foreach (var dead in _fleets.Where(f => f.Supply < 0.25f))
            if (dead.Owner == WarFaction.Virus)
                VirusWaveLost();
        _fleets.RemoveAll(f => f.Supply < 0.25f);
    }

    // The ship counts of a faction in a system, each as its own array so the damage can wear them all down: the garrison, if
    // the system is theirs, and every fleet of theirs standing there.
    private List<float[]> ShipsAt(int node, WarFaction faction)
    {
        var arrays = new List<float[]>();
        if (_nodes[node].Owner == faction)
            arrays.Add(_nodes[node].Garrison);
        foreach (var fleet in _fleets)
            if (fleet.Owner == faction && fleet.Standing && fleet.Node == node)
                arrays.Add(fleet.Ships);
        return arrays;
    }

    private static float[] SumShips(List<float[]> arrays)
    {
        var total = new float[ShipStats.ClassCount];
        foreach (var ships in arrays)
            for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                total[c] += ships[c];
        return total;
    }

    private static void ApplyDamage(WarNode node, List<float[]> arrays, WarFaction faction, bool fortPresent, float damage)
    {
        if (damage <= 0f)
            return;
        var pool = arrays.Sum(a => CombatStats.Hp(a)) + (fortPresent ? node.FortHp : 0f);
        if (pool <= 0f)
            return;
        var kept = 1f - Math.Min(1f, damage / pool);
        foreach (var ships in arrays)
            for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
            {
                ships[c] *= kept;
                if (ships[c] < 0.02f)
                    ships[c] = 0f;
            }
        if (fortPresent)
            node.FortHp = node.FortHp * kept < 1f ? 0f : node.FortHp * kept;
    }

    // ---- taking a system: the enemy holds it with nothing left to oppose them ----

    public float OccupationNeeded(WarNode node)
    {
        var needed = 0f;
        if (node.Planet == PlanetKind.Capital)
            needed = Math.Max(needed, Config.CapitalOccupationSeconds);
        else if (node.Planet == PlanetKind.Colony)
            needed = Math.Max(needed, Config.ColonyOccupationSeconds);
        if (node.HasYard)
            needed = Math.Max(needed, Config.YardOccupationSeconds);
        return Math.Max(needed, Config.OutpostOccupationSeconds);
    }

    private void StepOccupation(double dt)
    {
        foreach (var node in _nodes)
        {
            if (node.Owner == WarFaction.None)
                continue;
            var enemy = node.Owner == WarFaction.Union ? WarFaction.Virus : WarFaction.Union;
            var enemyHere = CombatStats.Supply(ForceAt(node.Index, enemy)) > 0.25f;
            var defended = CombatStats.Supply(ForceAt(node.Index, node.Owner)) > 0.25f || node.FortHp > 1f
                || (node.Owner == WarFaction.Union && _mother is { Alive: true, Standing: true } m && m.Node == node.Index);
            if (enemyHere && !defended)
            {
                node.OccupationSeconds += (float)dt;
                if (node.OccupationSeconds >= OccupationNeeded(node))
                    Capture(node, enemy);
            }
            else if (node.OccupationSeconds > 0f)
            {
                node.OccupationSeconds = Math.Max(0f, node.OccupationSeconds - 2f * (float)dt);
            }
        }
    }

    // A system changes hands. What was being built there is lost, the works stand but are half wrecked, and ships and ore
    // that were on their way to it find a stranger there.
    private void Capture(WarNode node, WarFaction by)
    {
        node.Owner = by;
        if (by == WarFaction.Union)
            node.Nest = false;
        if (by == WarFaction.Virus)
            VirusConquered();
        node.OccupationSeconds = 0f;
        node.Queue.Clear();
        node.Project = null;
        node.WorkshopBuilding = null;
        node.Reserved = 0f;
        node.Incoming = 0f;
        node.Stock *= 0.5f;
        Array.Clear(node.Garrison);
        if (by == WarFaction.Virus)
        {
            node.Miners /= 2;
            node.Drones /= 2;
        }
        node.FortHp = FortMax(node) * 0.25f;
        _foundingTargets.Remove(node.Index);
    }
}
