namespace Anabiosis.Shared.War;

// The mother ship: the huge colonial vessel that carries the artifact. It is not a garrison and it is not a fleet; it stays in a
// Union system, a slow giant with thick armour and little to shoot with, and runs from the virus. When an enemy fleet or an
// enemy-held system comes within a few lanes of it, it makes for the Union system that is farthest from all of them by a road
// that does not pass near them; when things are quiet it goes back to the capital. Caught, it fights where it stands until it
// is broken. The war goes on whether it lives or not. The virus is drawn to it: its front leans toward the mother ship.
public sealed class WarMotherShip
{
    public int Node { get; set; }
    public int Next { get; set; } = -1;
    public float Along { get; set; }
    public float LaneLength { get; set; }
    public Queue<int> Route { get; } = new();
    public int Destination { get; set; } = -1;
    public float Hp { get; set; }
    public float MaxHp { get; init; }
    public bool Alive { get; set; } = true;
    public bool Fighting { get; set; }

    public bool Standing => Next < 0;
    // The system it is in, or the one it is about to reach.
    public int Where => Standing ? Node : Next;
}

public sealed partial class WarSim
{
    private WarMotherShip? _mother;

    public WarMotherShip? Mother => _mother;
    public bool MotherAlive => _mother is { Alive: true };

    private void SeedMother()
    {
        _mother = new WarMotherShip { Node = CapitalIndex, Hp = Config.MotherHp, MaxHp = Config.MotherHp };
    }

    // ---- movement ----

    private void StepMother(double dt)
    {
        if (_mother is not { Alive: true } mother)
            return;
        var left = Config.MotherSpeed * dt;
        while (left > 1e-6 && !mother.Standing)
        {
            var room = mother.LaneLength - mother.Along;
            if (left < room)
            {
                mother.Along += (float)left;
                return;
            }
            left -= room;
            mother.Node = mother.Next;
            mother.Along = 0f;
            if (mother.Route.Count > 0)
            {
                mother.Next = mother.Route.Dequeue();
                mother.LaneLength = Galaxy.LaneLength(mother.Node, mother.Next);
            }
            else
            {
                mother.Next = -1;
                mother.LaneLength = 0f;
            }
        }
    }

    // Sets the mother ship on the way to a system, from where it is (or from the next system if it is under way).
    private void SendMother(int destination)
    {
        var mother = _mother!;
        var start = mother.Standing ? mother.Node : mother.Next;
        mother.Route.Clear();
        mother.Destination = destination;
        if (start == destination)
        {
            if (mother.Standing)
                return;
            return; // reaches it as the lane ends
        }
        var path = Galaxy.Path(start, destination);
        if (mother.Standing)
        {
            for (var i = 1; i < path.Count; i++)
                mother.Route.Enqueue(path[i]);
            mother.Next = path[0];
            mother.LaneLength = Galaxy.LaneLength(mother.Node, mother.Next);
            mother.Along = 0f;
        }
        else
        {
            // Under way: the path starts at the next system after the one it is heading to, so all of it is still ahead.
            foreach (var step in path)
                mother.Route.Enqueue(step);
        }
    }

    // ---- where to be ----

    // How many lanes the nearest enemy is from each system: enemy fleets standing or bound there, and enemy-held systems.
    private int[] DangerMap()
    {
        var danger = new int[_nodes.Length];
        Array.Fill(danger, int.MaxValue);
        var sources = new List<int>();
        foreach (var fleet in _fleets)
            if (fleet.Owner == WarFaction.Virus)
            {
                sources.Add(fleet.Standing ? fleet.Node : fleet.Next);
                if (fleet.Destination >= 0)
                    sources.Add(fleet.Destination);
            }
        foreach (var node in _nodes)
            if (node.Owner == WarFaction.Virus)
                sources.Add(node.Index);
        if (sources.Count == 0)
            return danger;
        return Galaxy.HopsFromAny(sources);
    }

    // The mother ship thinks every military tick.
    private void RunMother()
    {
        if (_mother is not { Alive: true } mother)
            return;
        var danger = DangerMap();
        var here = mother.Where;

        // Under way: change course if the place it is going to has become dangerous.
        if (!mother.Standing)
        {
            if (mother.Destination >= 0 && danger[mother.Destination] <= Config.MotherFleeHops)
            {
                var better = SaferPlace(danger, mother.Where);
                if (better >= 0 && better != mother.Destination)
                    SendMother(better);
            }
            return;
        }

        if (danger[here] <= Config.MotherFleeHops)
        {
            var refuge = SaferPlace(danger, here);
            if (refuge >= 0 && refuge != here)
            {
                Emit($"Матка уходит из {NameOf(here)} в {NameOf(refuge)}: враг в {danger[here]} переходах");
                SendMother(refuge);
            }
            return;
        }

        // Quiet: home to the capital, if the way there and the capital itself are safe.
        if (here != CapitalIndex && _nodes[CapitalIndex].IsUnion && danger[CapitalIndex] > Config.MotherFleeHops + 2 && PathIsSafe(here, CapitalIndex, danger))
            SendMother(CapitalIndex);
    }

    // The system (the Union one, or empty space) farthest from every enemy that can be reached by a road keeping clear of them, nearer ones preferred
    // among the equally far. -1 when nowhere is any safer than where the ship is.
    private int SaferPlace(int[] danger, int from)
    {
        var best = -1;
        var bestScore = float.NegativeInfinity;
        foreach (var node in _nodes)
        {
            if (node.Owner == WarFaction.Virus || danger[node.Index] <= danger[from] && node.Index != from)
                continue;
            if (!PathIsSafe(from, node.Index, danger))
                continue;
            // Far from the enemy matters most; a fortified world and a short road count for something.
            var score = Math.Min(danger[node.Index], 12) * 100f - Galaxy.PathLength(from, node.Index) * 0.1f + (node.Planet != PlanetKind.None ? 25f : 0f);
            if (score > bestScore)
            {
                bestScore = score;
                best = node.Index;
            }
        }
        return best;
    }

    // A road is safe when no system on it (past the first step) has the enemy a lane or less away.
    private bool PathIsSafe(int from, int to, int[] danger)
    {
        if (from == to)
            return true;
        foreach (var step in Galaxy.Path(from, to))
            if (danger[step] <= 1)
                return false;
        return true;
    }

    // ---- fighting ----

    private void MotherDestroyed()
    {
        var mother = _mother!;
        mother.Alive = false;
        mother.Hp = 0f;
        Emit($"МАТКА УНИЧТОЖЕНА в системе {NameOf(mother.Where)}: артефакт утрачен");
    }
}
