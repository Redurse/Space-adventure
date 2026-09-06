using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Reputation with the galaxy's powers (game_design.md section 12, Phase 3). Standing is one
// number per faction on the shared crew account - same framing as Credits, since it's the ship's
// reputation, not any individual crew member's. It moves on exactly two events (finishing work for
// a faction, and destroying one of its ships) and feeds back into what stations charge and whether
// their Administrator will hand out work at all.
public sealed partial class World
{
    private readonly Dictionary<FactionId, int> _factionStanding =
        Enum.GetValues<FactionId>().ToDictionary(f => f, _ => 0);

    public int GetStanding(FactionId faction) => _factionStanding.GetValueOrDefault(faction);

    // Independents are included here on purpose. Their neutrality is *political* - they take no
    // side in the Consortium/FreeFleet rivalry, so nothing ripples to them from a fight between
    // those two (FactionDefinitions.Rival returns null for them, and their prices never move).
    // But they still notice what's done to them directly: robbing an independent outpost has to
    // cost something, or the home station becomes a consequence-free place to loot forever.
    private void AdjustStanding(FactionId faction, int delta)
    {
        _factionStanding[faction] = Math.Clamp(
            _factionStanding[faction] + delta,
            FactionDefinitions.MinStanding,
            FactionDefinitions.MaxStanding);
    }

    // Sets standing directly (same "Debug*" convention as DebugAddCredits/DebugSetHullPlatingStock
    // etc.) - a test that needs a station's own faction already hostile BEFORE docking there can't
    // grind to that standing by fighting in the open first, since a station's own defensive squadron
    // now intercepts an approach at exactly that standing (World.Voyage.cs's UpdateNearestStation) -
    // there is no longer a way to physically dock at a station whose faction you've already angered
    // enough. Setting it directly after docking friendly avoids that chicken-and-egg problem.
    public void DebugSetStanding(FactionId faction, int value) =>
        _factionStanding[faction] = Math.Clamp(value, FactionDefinitions.MinStanding, FactionDefinitions.MaxStanding);

    // Who currently controls a point - GalaxyPoint.Faction (Shared's static starting data) unless
    // the war below (WarEffort/ContestedPointId) has flipped it, the same reason _factionStanding
    // itself lives here rather than as a mutable field on FactionDefinitions.
    private readonly Dictionary<string, FactionId> _pointOwner = new();
    private FactionId OwnerOf(string pointId) =>
        _pointOwner.TryGetValue(pointId, out var owner) ? owner : GalaxyMap.GetPoint(pointId).Faction;

    // Faction that owns wherever the ship currently is - what the trader's prices and the
    // administrator's willingness to talk are both keyed off.
    private FactionId DockedFaction =>
        _dockedPointId is { } id ? OwnerOf(id) : FactionId.Independent;

    // Destroying a ship angers its owner and pleases their rival by a smaller amount - the
    // asymmetry is what makes picking a side actually cost something.
    private void RecordShipDestroyed(FactionId faction)
    {
        AdjustStanding(faction, FactionDefinitions.StandingPerShipDestroyed);
        if (FactionDefinitions.Rival(faction) is { } rival)
        {
            AdjustStanding(rival, FactionDefinitions.RivalStandingPerShipDestroyed);
            NudgeWarEffort(loser: faction, winner: rival);
        }
    }

    // The one front this version models (game_design.md section 12, Phase 4 - "война фракций друг
    // с другом"): whichever rival is currently losing ships loses ground here too, once enough of
    // them have died. A whole map that can flip anywhere would need a real background simulation;
    // this is the same effect on a single, deliberately chosen border station instead.
    private const string ContestedPointId = "outpost-gamma";
    // Deliberately above what grinding a faction just past HostileThreshold or even WarThreshold
    // costs (3 and 4 kills respectively, at StandingPerShipDestroyed each) - losing a station
    // outright has to take a sustained campaign, not just the same fighting that already made a
    // faction hostile enough to refuse quests or lock the player out of its territory.
    private const int WarEffortToFlipSector = 5;
    private readonly Dictionary<(FactionId Loser, FactionId Winner), int> _warEffort = new();

    private void NudgeWarEffort(FactionId loser, FactionId winner)
    {
        if (OwnerOf(ContestedPointId) != loser)
            return; // this front already belongs to the side that's currently winning it

        var key = (loser, winner);
        var effort = _warEffort.GetValueOrDefault(key) + 1;
        if (effort < WarEffortToFlipSector)
        {
            _warEffort[key] = effort;
            return;
        }

        _pointOwner[ContestedPointId] = winner;
        _warEffort.Remove(key);
    }

    private bool IsHostileHere => GetStanding(DockedFaction) <= FactionDefinitions.HostileThreshold;

    // Applied to both buy and sell prices at the docked station (World.Trade.cs).
    private float LocalPriceMultiplier =>
        FactionDefinitions.PriceMultiplier(DockedFaction, GetStanding(DockedFaction));

    // Same standing that moves prices also moves how hard a sector's defenders are to get past
    // (World.EnemyFleet.cs's SpawnEnemySquadron, called from World.Voyage.cs's Arrive): a faction
    // that hates you throws more hulls at you, one that loves you thins its own patrol out.
    private const int HostilitySquadronBonus = 1;
    private int SquadronSizeAdjustment(FactionId faction)
    {
        var standing = GetStanding(faction);
        if (standing <= FactionDefinitions.HostileThreshold) return HostilitySquadronBonus;
        if (standing >= FactionDefinitions.FriendlyThreshold) return -HostilitySquadronBonus;
        return 0;
    }

    private IReadOnlyList<FactionStandingState> CreateFactionStandings() =>
        _factionStanding
            .Select(kv => new FactionStandingState(kv.Key, FactionDefinitions.Name(kv.Key), kv.Value))
            .ToArray();

    // Only the CURRENT system's points - GalaxyMap.Points is every system's combined, which is
    // exactly what a client-side map must never draw as one space (World.StarSystems.cs). Faction
    // is re-checked per point so the client (which just reads GalaxyPoint.Faction, same as it
    // always has) never needs to know a war exists at all.
    private IReadOnlyList<GalaxyPoint> CreateGalaxyPoints() =>
        GalaxyMap.GetSystem(_currentSystemId).Points
            .Select(p => OwnerOf(p.Id) == p.Faction ? p : p with { Faction = OwnerOf(p.Id) })
            .ToArray();
}
