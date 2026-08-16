using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

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

    // Faction that owns wherever the ship currently is - what the trader's prices and the
    // administrator's willingness to talk are both keyed off.
    private FactionId DockedFaction =>
        _dockedPointId is { } id ? GalaxyMap.GetPoint(id).Faction : FactionId.Independent;

    // Destroying a ship angers its owner and pleases their rival by a smaller amount - the
    // asymmetry is what makes picking a side actually cost something.
    private void RecordShipDestroyed(FactionId faction)
    {
        AdjustStanding(faction, FactionDefinitions.StandingPerShipDestroyed);
        if (FactionDefinitions.Rival(faction) is { } rival)
            AdjustStanding(rival, FactionDefinitions.RivalStandingPerShipDestroyed);
    }

    private bool IsHostileHere => GetStanding(DockedFaction) <= FactionDefinitions.HostileThreshold;

    // Applied to both buy and sell prices at the docked station (World.Trade.cs).
    private float LocalPriceMultiplier =>
        FactionDefinitions.PriceMultiplier(DockedFaction, GetStanding(DockedFaction));

    private IReadOnlyList<FactionStandingState> CreateFactionStandings() =>
        _factionStanding
            .Select(kv => new FactionStandingState(kv.Key, FactionDefinitions.Name(kv.Key), kv.Value))
            .ToArray();
}
