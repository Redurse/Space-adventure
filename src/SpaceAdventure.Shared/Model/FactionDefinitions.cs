namespace SpaceAdventure.Shared.Model;

// Reputation model (game_design.md section 12, Phase 3). Standing runs -100..+100 starting at 0;
// it moves when the player does something a faction cares about (turning in that faction's cargo
// quest, destroying one of its ships) and feeds back into prices at its stations and whether its
// Administrator will hand out work at all.
public static class FactionDefinitions
{
    public const int MinStanding = -100;
    public const int MaxStanding = 100;
    public const int HostileThreshold = -40; // at or below this, the faction's stations refuse quests
    public const int FriendlyThreshold = 40; // at or above this, its trader gives the best prices

    // Killing a faction's raider pleases its rivals as much as it angers the faction itself -
    // that mutual pull is what makes standing a real choice rather than a number that only grows.
    public const int StandingPerQuestTurnIn = 12;
    public const int StandingPerShipDestroyed = -18;
    public const int RivalStandingPerShipDestroyed = 6;

    public static string Name(FactionId faction) => faction switch
    {
        FactionId.Consortium => "Консорциум",
        FactionId.FreeFleet => "Вольный флот",
        _ => "Независимые",
    };

    // Who benefits when the named faction takes a loss. Independents stay out of everything, so
    // hitting them shifts nobody's standing but their own.
    public static FactionId? Rival(FactionId faction) => faction switch
    {
        FactionId.Consortium => FactionId.FreeFleet,
        FactionId.FreeFleet => FactionId.Consortium,
        _ => null,
    };

    // Price multiplier at a faction's own stations: liked crews buy cheaper, disliked ones pay a
    // markup. Independents are indifferent and always charge list price.
    public static float PriceMultiplier(FactionId faction, int standing)
    {
        if (faction == FactionId.Independent)
            return 1f;
        if (standing >= FriendlyThreshold)
            return 0.8f;
        if (standing <= HostileThreshold)
            return 1.35f;
        return 1f;
    }

    public static string StandingLabel(int standing) =>
        standing >= FriendlyThreshold ? "союзник"
        : standing <= HostileThreshold ? "враждебны"
        : "нейтральны";
}
