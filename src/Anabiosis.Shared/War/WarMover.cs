namespace Anabiosis.Shared.War;

public enum MoverTask
{
    Idle,
    ToSource, // freighter on its way to collect ore
    Deliver,  // freighter on its way to hand it over
    Found,    // outposter on its way to found an outpost
}

// A ship travelling the lanes: where it is is a node it last left (or stands in) and how far along the lane to the next
// one it has got, which is all the galactic map needs to draw it.
public sealed class WarMover
{
    public int Id { get; init; }
    public ShipClass Class { get; init; }
    public WarFaction Owner { get; init; }

    public int Node { get; set; }
    public int Next { get; set; } = -1;
    public float Along { get; set; }
    public float LaneLength { get; set; }
    public Queue<int> Route { get; } = new();

    public MoverTask Task { get; set; }
    public int Source { get; set; } = -1;
    public int Sink { get; set; } = -1;
    public float Planned { get; set; }
    public float Cargo { get; set; }

    public bool Standing => Next < 0;
    public bool IsIdleFreighter => Class == ShipClass.Freighter && Task == MoverTask.Idle && Standing;
}
