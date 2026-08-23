namespace SpaceAdventure.Shared.Model;

// Generation-time bookkeeping only (Station.Procedural.cs) - drives zoning, room naming, and which
// NpcKind/crate a module gets while the generator is still assembling the layout. Never survives
// into the finished Station: the runtime Room/Door/StationNpc/StationCrate lists this produces are
// exactly the same plain types every other station consumer (docking, rendering, save/load) already
// works with, so nothing downstream needs to know this enum exists.
public enum StationModuleKind
{
    // Mandatory - present on every station regardless of kind.
    Dock,
    Trade,
    Administrator,
    Engineering,
    Storage,
    Security,
    Recruiting,
    // Mandatory only for StationKind.Shipyard - the one kind that sells hulls.
    Shipwright,

    // Trade's secondary pool.
    ExtraTrade,
    Cantina,
    Brokerage,
    BondedWarehouse,
    PassengerLounge,

    // Mining's secondary pool.
    OreRefinery,
    BulkStorage,
    Foundry,
    ProspectorsBunkroom,
    OreVault,

    // Shipyard's secondary pool.
    DrydockBay,
    OutfittingBay,
    SalvageYard,
    CrewBunkroom,
    FittingDock,

    // Military's secondary pool.
    Armory,
    Barracks,
    Brig,
    CommandCenter,
    MunitionsStorage,
    TrainingHall,
    RadarPost,
    MedBay,

    // Research's secondary pool.
    Laboratory,
    Observatory,
    DataArchive,
    Greenhouse,
}
