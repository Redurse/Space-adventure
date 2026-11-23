namespace Anabiosis.Shared.War;

// The numbers of the virus: how it starts, how it grows, and how it picks its fights.
public sealed partial record WarConfig
{
    // Off, the war is the Union alone (and whatever SpawnInvasion lands, for looking at the defence).
    public bool VirusEnabled { get; init; } = true;

    // The nest: where the virus starts, far from the Union capital. It begins with a megayard, a fortress, ore and a fleet,
    // and keeps still (building up) until it wakes, and then attacks at once.
    public double VirusAwakenSeconds { get; init; } = 10.0 * 3600.0;
    public float VirusStarterSupply { get; init; } = 140f;
    // How far from the Union capital, in lanes, the nest may be, and the great precursor post: in a galaxy of four thousand systems the
    // farthest one is sixty lanes away, and a war that takes days to arrive is no war.
    public int VirusNestMaxHops { get; init; } = 14;
    public int PrecursorMaxHops { get; init; } = 22;
    // If the virus is wiped out it breaks out again somewhere new after this long, with the starting fleet this many times bigger.
    public double VirusRegrowSeconds { get; init; } = 3.0 * 3600.0;
    public float VirusRegrowFleetFactor { get; init; } = 3f;
    public float VirusNestStock { get; init; } = 4000f;
    public float VirusNestFortHp { get; init; } = 100000f;
    // What a virus-held system yields next to the same system held by the Union (what is left of its works, and the virus doesn't mine well).
    // What the nest brings in by itself (ore per second), and the share of its garrison it never sends out.
    public float VirusNestOrePerSecond { get; init; } = 3500f / 3600f;
    public float VirusNestGuardShare { get; init; } = 0.4f;
    // The nest builds many ships at once.
    public int VirusNestSlots { get; init; } = 14;
    // How much of the Union ships within three lanes of a target the virus reckons will come to its help.
    public float VirusReliefReckoning { get; init; } = 1.0f;
    public int VirusReliefHops { get; init; } = 10;
    // The nest brings in more as the campaign goes on: its income grows by this share of itself every hour of war divided by this.
    public float VirusGrowthHours { get; init; } = 30f;
    public float VirusOreFactor { get; init; } = 0.7f;

    // The virus fleet limit: a base, a share for every system it holds, and a growth through the campaign so that the pressure rises.
    public float VirusBaseSupply { get; init; } = 250f;
    public float VirusSupplyPerSystem { get; init; } = 16f;
    public float VirusSupplyPerHour { get; init; } = 4f; // 4000-star galaxy: 4 keeps the Union alive to 200 h on most seeds (5 lost 2 of 3, 3 lost none)

    // Of every Union warship that dies to a virus fleet, this share rises again on the virus side.
    public float VirusInfectionShare { get; init; } = 0.11f;

    // Waves: the smallest group worth sending, how much stronger than the defence of a target it must be, and how much of a
    // garrison stays home when a wave leaves it.
    public float VirusWaveMinSupply { get; init; } = 28f;
    // Ships a garrison has to have to spare before it sends them on to the main fleet, and how many of the nearest Union systems the front looks at.
    public float VirusReinforceSupply { get; init; } = 20f;
    public int VirusFrontWidth { get; init; } = 3;
    // After this many hours of waiting for odds, the odds the virus asks for are down to the floor share of what they were.
    public float VirusImpatienceHours { get; init; } = 12f;
    public float VirusImpatienceFloor { get; init; } = 0.5f;
    public float VirusAssaultMargin { get; init; } = 1.3f;
    // Caution: the odds the virus asks for are multiplied by this, which rises by this step for each wave it loses, falls back by the decay per hour, and has a ceiling.
    public float VirusCautionStep { get; init; } = 1.25f;
    public float VirusCautionMax { get; init; } = 5f;
    public float VirusCautionDecayPerHour { get; init; } = 0.12f;
    public float VirusCapitalMargin { get; init; } = 2.2f;
    public float VirusGuardShare { get; init; } = 0.2f;
    public double VirusIntervalSeconds { get; init; } = 15.0;
}
