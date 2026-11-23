namespace Anabiosis.Shared.War;

// A ship waiting in a yard queue. It is paid for (the ore leaves the yard stock) the moment it starts, then takes its build
// time. Target is the system an outposter is to found an outpost in.
public sealed class YardOrder
{
    public ShipClass Class { get; init; }
    public int Target { get; init; } = -1;
    public bool Started { get; set; }
    public float SecondsLeft { get; set; }
}

// Something being built on the spot: it is paid for first (ore carried in by freighters), then takes its time.
public sealed class WarProject
{
    public ProjectKind Kind { get; init; }
    public bool Started { get; set; }
    public float SecondsLeft { get; set; }
}

// Everything that stands in one star system. Which system it is lives in the galaxy (same index); this is what changes.
public sealed class WarNode
{
    public int Index { get; }
    public WarFaction Owner { get; set; }
    public PlanetKind Planet { get; set; }
    public bool Outpost { get; set; }
    public YardTier Yard { get; set; }

    // Ore held here, ore promised away to freighters that have not collected it yet, ore promised to arrive.
    public float Stock { get; set; }
    public float Reserved { get; set; }
    public float Incoming { get; set; }

    // An outpost mining crew: miners are ships, drones the small ones.
    public int Miners { get; set; }
    public int Drones { get; set; }
    public ShipClass? WorkshopBuilding { get; set; }
    public float WorkshopSecondsLeft { get; set; }

    public List<YardOrder> Queue { get; } = new();
    public WarProject? Project { get; set; }

    // The warships standing here to defend it, by class (fractions: a battle wears them down gradually).
    public float[] Garrison { get; } = new float[ShipStats.ClassCount];

    // What the planet and its works can do for themselves: hit points of the fortifications, and how long an enemy has held
    // the system with nothing left to stop it. FortHp is meaningful only after the first RefreshFort.
    public float FortHp { get; set; }
    public float OccupationSeconds { get; set; }
    // Seconds since the last round of fighting here: fortifications only mend in peace.
    public float SinceCombat { get; set; } = 1e6f;
    public bool Fighting { get; set; }

    public WarNode(int index)
    {
        Index = index;
    }

    public bool IsUnion => Owner == WarFaction.Union;
    public bool HasYard => Yard != YardTier.None;

    // The virus starting system: a megayard and a fortress far beyond what an outpost has.
    public bool Nest { get; set; }
}
