namespace Anabiosis.Shared.War;

// The war as it stands, for the save file: everything WarSim keeps that is not rebuilt from the galaxy. Plain data, written as JSON
// with the rest of the save. A save from a different galaxy (another system count) is ignored and the war starts afresh.

public sealed class WarSave
{
    public int SystemCount { get; set; }
    // A fingerprint of the lanes the war was fought on: a save from a galaxy laid out differently (its ships stand on lanes
    // that are no longer there) is not restored. Zero means a save from before this was kept, which counts as different.
    public int LayoutHash { get; set; }
    public double Clock { get; set; }
    public double GovernmentClock { get; set; }
    public double LogisticsClock { get; set; }
    public double MilitaryClock { get; set; }
    public double CombatClock { get; set; }
    public double VirusClock { get; set; }
    public double OreMined { get; set; }
    public double OreWasted { get; set; }
    public int NextMoverId { get; set; }
    public int NextFleetId { get; set; }
    public int NextOperationId { get; set; }
    public int CapitalIndex { get; set; }

    public List<WarNodeSave> Nodes { get; set; } = new();
    public List<WarMoverSave> Movers { get; set; } = new();
    public List<WarFleetSave> Fleets { get; set; } = new();
    public List<WarOperationSave> Operations { get; set; } = new();
    public List<int> FoundingTargets { get; set; } = new();
    public List<int> Abandoned { get; set; } = new();

    public int NestIndex { get; set; } = -1;
    public bool VirusRevealed { get; set; }
    public float VirusCaution { get; set; } = 1f;
    public int VirusLastTarget { get; set; } = -1;
    public double? VirusWaitingSince { get; set; }
    public double? VirusExtinctSince { get; set; }
    public WarMotherSave? Mother { get; set; }
    public int PrecursorIndex { get; set; } = -1;
    public bool VirusDestroyed { get; set; }
}

public sealed class WarNodeSave
{
    public WarFaction Owner { get; set; }
    public PlanetKind Planet { get; set; }
    public bool Outpost { get; set; }
    public YardTier Yard { get; set; }
    public bool Nest { get; set; }
    public float Stock { get; set; }
    public float Reserved { get; set; }
    public float Incoming { get; set; }
    public int Miners { get; set; }
    public int Drones { get; set; }
    public ShipClass? WorkshopBuilding { get; set; }
    public float WorkshopSecondsLeft { get; set; }
    public List<WarOrderSave> Queue { get; set; } = new();
    public ProjectKind? ProjectKind { get; set; }
    public bool ProjectStarted { get; set; }
    public float ProjectSecondsLeft { get; set; }
    public float[] Garrison { get; set; } = Array.Empty<float>();
    public float FortHp { get; set; }
    public float OccupationSeconds { get; set; }
    public float SinceCombat { get; set; }
}

public sealed class WarOrderSave
{
    public ShipClass Class { get; set; }
    public int Target { get; set; }
    public bool Started { get; set; }
    public float SecondsLeft { get; set; }
}

public sealed class WarMoverSave
{
    public int Id { get; set; }
    public ShipClass Class { get; set; }
    public WarFaction Owner { get; set; }
    public int Node { get; set; }
    public int Next { get; set; }
    public float Along { get; set; }
    public float LaneLength { get; set; }
    public List<int> Route { get; set; } = new();
    public MoverTask Task { get; set; }
    public int Source { get; set; }
    public int Sink { get; set; }
    public float Planned { get; set; }
    public float Cargo { get; set; }
}

public sealed class WarFleetSave
{
    public int Id { get; set; }
    public WarFaction Owner { get; set; }
    public float[] Ships { get; set; } = Array.Empty<float>();
    public int Node { get; set; }
    public int Next { get; set; }
    public float Along { get; set; }
    public float LaneLength { get; set; }
    public List<int> Route { get; set; } = new();
    public int Destination { get; set; }
    public FleetTask Task { get; set; }
    public int OperationId { get; set; }
    public bool Fighting { get; set; }
}

public sealed class WarOperationSave
{
    public int Id { get; set; }
    public int Target { get; set; }
    public int Rally { get; set; }
    public bool Counter { get; set; }
    public bool Striking { get; set; }
    public double Started { get; set; }
    public float EnemyStrength { get; set; }
}

public sealed class WarMotherSave
{
    public int Node { get; set; }
    public int Next { get; set; }
    public float Along { get; set; }
    public float LaneLength { get; set; }
    public List<int> Route { get; set; } = new();
    public int Destination { get; set; }
    public float Hp { get; set; }
    public float MaxHp { get; set; }
    public bool Alive { get; set; }
}
