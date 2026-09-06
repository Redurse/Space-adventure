namespace Anabiosis.Shared.Protocol;

// Null on WorldSnapshot whenever no match of "Фронты" is in progress at the ship's CardTable
// (World.FrontsGame.cs) - picked instead of Дурак via the table's game-choice step
// (World.CardTable.cs's TryChooseCardTableGame). A simplified Hearts of Iron IV-style 2-player
// wargame: 3 independent fronts, a shared reinforcement pool per side, push a front to either end
// to capture it for good.
//
// Both sides' current allocations travel to every client in full - the same no-hidden-state trust
// model CardGameState's own doc comment already established for the table's other game. There is
// no secret to keep here either: "Провести бой" just locks in whatever is currently showing for
// both sides.
public sealed record FrontsGameState(
    int PlayerAId,
    int PlayerBId,
    // -FrontRange..+FrontRange per front (World.FrontsGame.cs); positive favors PlayerA, negative
    // favors PlayerB, 0 is the untouched contested start. Always exactly FrontCount (3) long:
    // Северный/Центральный/Южный, in that order - FrontsGamePanel's own FrontNames array.
    IReadOnlyList<int> FrontProgress,
    IReadOnlyList<int> AllocationA,
    IReadOnlyList<int> AllocationB,
    // A captured front no longer accepts allocation and no longer moves - its FrontProgress sign at
    // the moment it hit +-FrontRange decides who holds it, permanently, for the rest of the match.
    IReadOnlyList<bool> Captured,
    int ArmyPool,
    int Turn,
    int TurnCap,
    // Null while the match is ongoing, and while a genuine, fully symmetric draw ends it. Set the
    // moment every front is captured (or the turn cap is hit) to whichever side holds more fronts,
    // or - if that's tied - whichever side's summed FrontProgress across all 3 fronts favors them.
    int? WinnerId,
    bool Finished,
    // Solo play (direct user request, "можно играть в хойку в одиночку") - PlayerBId is then a
    // sentinel with no real character/connection behind it (World.FrontsGame.cs's
    // FrontsBotPlayerId); the bot's own allocation still travels here like any other, computed by
    // UpdateBotAllocation instead of a second human's clicks.
    bool VsBot);
