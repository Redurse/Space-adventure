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
    // Stricter than HostileThreshold on purpose (World.StationDocking.cs) - being turned away from
    // a faction's own territory entirely has to be rarer than just losing access to its job board.
    public const int WarThreshold = -70;

    // Killing a faction's raider pleases its rivals as much as it angers the faction itself -
    // that mutual pull is what makes standing a real choice rather than a number that only grows.
    public const int StandingPerQuestTurnIn = 12;
    public const int StandingPerShipDestroyed = -18;
    public const int RivalStandingPerShipDestroyed = 6;
    // Doing work for one side is a small political act even when nobody's shooting - a courier run
    // for the Consortium costs a little goodwill with FreeFleet, just far less than blowing up one
    // of their ships would (World.Quests.cs's TryTurnInQuest).
    public const int RivalStandingPerQuestTurnIn = -4;
    // Smaller than a guard kill or an arrest (World.StationCrime.cs) - this is reneging on a deal,
    // not violence, but it still has to cost something or "accept, then quietly drop it" would be
    // a strictly-better version of just not taking work in the first place (World.Quests.cs's
    // TryAbandonQuest).
    public const int StandingPenaltyForAbandoningQuest = -10;

    public static string Name(FactionId faction) => faction switch
    {
        FactionId.Consortium => "Консорциум",
        FactionId.FreeFleet => "Вольный флот",
        FactionId.MinersGuild => "Гильдия старателей",
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
