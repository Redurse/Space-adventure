namespace Anabiosis.Shared.War;

// Every balance number of the economy in one place, so they can be tuned together without hunting through the code.
// Times are seconds of war time (which is real time in the game: a campaign is 100-200 hours), distances are galactic-map
// units (a lane between neighbouring systems is roughly 60-220 of them), ore is the one resource.
public sealed partial record WarConfig
{
    public int Seed { get; init; } = 1;

    // Where the Union starts. CapitalSystemId null picks the system nearest the middle of the galaxy that has a rocky planet.
    public string? CapitalSystemId { get; init; }
    public int StartingUnionSystems { get; init; } = 40;
    public int StartingColonies { get; init; } = 12;
    public int StartingYards { get; init; } = 5;

    // The simulation never takes a step longer than this, however long a fast-forward asks for.
    public double MaxStepSeconds { get; init; } = 5.0;
    public double GovernmentIntervalSeconds { get; init; } = 30.0;
    public double LogisticsIntervalSeconds { get; init; } = 20.0;

    // How fast ships cross the map: a 150-unit lane is about four minutes for a freighter.
    public float FreighterSpeed { get; init; } = 0.65f;
    public float OutposterSpeed { get; init; } = 0.8f;
    public float FreighterCapacity { get; init; } = 300f;

    // Ore per second. A colony gives more than a fully staffed outpost, the capital more than anything.
    public float MinerOrePerSecond { get; init; } = 6f / 3600f;
    public float DroneOrePerSecond { get; init; } = 1f / 3600f;
    public float ColonyOrePerSecond { get; init; } = 150f / 3600f;
    public float CapitalOrePerSecond { get; init; } = 1000f / 3600f;

    // How many miners and drones an outpost can work: a rich asteroid field has room for more.
    public float MinerSlotsBase { get; init; } = 2f;
    public float MinerSlotsPerRichness { get; init; } = 3f;
    public float DroneSlotsBase { get; init; } = 6f;
    public float DroneSlotsPerRichness { get; init; } = 6f;
    // The ore an outpost keeps back for its workshop and never hands to a freighter.
    public float OutpostKeepOre { get; init; } = 80f;

    public float OutpostStockCap { get; init; } = 600f;
    public float ColonyStockCap { get; init; } = 1500f;
    public float YardStockCap { get; init; } = 4000f;
    public float MegaYardStockCap { get; init; } = 20000f;
    public float CapitalStockCap { get; init; } = 100000f;

    public int YardSlots { get; init; } = 2;
    public int MegaYardSlots { get; init; } = 4;

    // A node offers ore to freighters once it holds this much more than it keeps.
    public float PickupThreshold { get; init; } = 150f;
    public int MaxDispatchesPerRound { get; init; } = 60;
    // How far, in map units, a longer haul counts against a sink when choosing where ore goes.
    public float HaulDistanceScale { get; init; } = 250f;

    public int MinFreighters { get; init; } = 6;
    public int MaxFreighters { get; init; } = 220;
    public float FreightersPerProducer { get; init; } = 0.25f;

    // Growth of the Union: how many outposters are on their way at once and the most systems it will hold.
    public int ExpansionParallel { get; init; } = 2;
    public int ExpansionMaxSystems { get; init; } = 100;
    public int ProjectsInParallel { get; init; } = 3;
    public float ColonyShareMax { get; init; } = 0.35f;
    // Every owned system should be within this many lanes of a yard.
    public int YardCoverageHops { get; init; } = 4;
    public int MaxMegaYards { get; init; } = 3;

    // The fleet limit (supply points) and how the warships are mixed.
    public int SupplyBase { get; init; } = 40;
    public int SupplyPerColony { get; init; } = 8;
    public int SupplyPerOutpost { get; init; } = 2;
    public int DreadnoughtsPerMegaYard { get; init; } = 2;
    public float CorvetteShare { get; init; } = 0.4f;
    public float FrigateShare { get; init; } = 0.35f;
    public float CruiserShare { get; init; } = 0.25f;
}
