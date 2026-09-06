namespace Anabiosis.Shared.Model;

// What the Administrator can send a crew to do (game_design.md section 7). Delivery was the only
// type for a long time (M11); the other two hang off mechanics that already exist - a hostile
// sector to clear, and ore that can be mined and carried home.
public enum QuestKind
{
    Delivery, // dock at DestinationPointId
    Bounty,   // destroy the ship guarding DestinationPointId
    Mining,   // bring RequiredAmount ore back to the issuing station
}

// One active job (game_design.md section 7). Only one can be active at a time - the per-station
// quest limit, simplified to 1 for the solo MVP - and it's crew-wide, not per-player, matching the
// shared-wallet framing of the crew's credits.
//
// DestinationPointId means different things per kind: where to dock (Delivery), whose ship to kill
// (Bounty), or - for Mining - the station that issued it and expects the ore back. IssuedByPointId
// is always the issuing station, so turning in is checkable uniformly.
public sealed record Quest(
    QuestKind Kind,
    string DestinationPointId,
    string DestinationName,
    int RewardCredits,
    string IssuedByPointId,
    int RequiredAmount = 0)
{
    // Bounty progress: set once the target has actually been destroyed (World.Quests.cs).
    public bool ObjectiveComplete { get; init; }

    public string Describe() => Kind switch
    {
        QuestKind.Bounty => $"уничтожить корабль в {DestinationName}",
        QuestKind.Mining => $"добыть руду ({RequiredAmount} шт.) и вернуть",
        _ => $"доставить груз на {DestinationName}",
    };
}
