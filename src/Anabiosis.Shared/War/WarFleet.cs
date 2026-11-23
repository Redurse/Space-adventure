namespace Anabiosis.Shared.War;

public enum FleetTask
{
    Idle,     // standing where it is
    Move,     // going to a system (to be merged into the garrison there on arrival, unless it belongs to an operation)
    Retreat,  // withdrawing from a lost battle to a safe yard
    Strike,   // an operation fleet sent against its target
    Invade,   // an enemy fleet marching on a system to take it
}

// A group of warships. Its ships are counts by class (fractions allowed: a battle wears a fleet down gradually). It stands in
// a system or travels the lanes like a mover. A fleet that belongs to an operation waits at its rally system until the
// operation sends it on; any other fleet joins the garrison of the system where it arrives.
public sealed class WarFleet
{
    public int Id { get; init; }
    public WarFaction Owner { get; init; }
    public float[] Ships { get; } = new float[ShipStats.ClassCount];

    public int Node { get; set; }
    public int Next { get; set; } = -1;
    public float Along { get; set; }
    public float LaneLength { get; set; }
    public Queue<int> Route { get; } = new();
    public int Destination { get; set; } = -1;

    public FleetTask Task { get; set; }
    public int OperationId { get; set; }

    public bool Standing => Next < 0;
    public float Supply => CombatStats.Supply(Ships);
    public float Strength => CombatStats.Strength(Ships);
    public bool Fighting { get; set; }
    public bool Retreating => Task == FleetTask.Retreat;

    // The pace of the slowest ship in it.
    public float Speed
    {
        get
        {
            var speed = float.PositiveInfinity;
            for (var c = CombatStats.FirstWarship; c < ShipStats.ClassCount; c++)
                if (Ships[c] >= 0.5f)
                    speed = Math.Min(speed, CombatStats.Speed((ShipClass)c));
            return float.IsPositiveInfinity(speed) ? CombatStats.Speed(ShipClass.Corvette) : speed;
        }
    }
}
