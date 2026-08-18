using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Null on WorldSnapshot whenever no hand of Дурак переводной is in progress at the ship's
// CardTable (World.CardGame.cs) - the moment 2 crew stand there together starts one automatically.
//
// Both hands travel to every client in full - the same no-hidden-state trust model this whole
// project already uses for wallets, positions, tank charges, etc. There is no server-side
// redaction to defeat; CardGamePanel is simply courteous enough not to render anyone else's hand
// face-up.
public sealed record CardGameState(
    int Player1Id,
    int Player2Id,
    IReadOnlyList<PlayingCard> Player1Hand,
    IReadOnlyList<PlayingCard> Player2Hand,
    // 0, 1 or (right after the one перевод a round allows) 2 cards currently on the table waiting
    // on the defender - everything already-beaten this round instead sits in ResolvedPairs.
    IReadOnlyList<PlayingCard> PendingAttacks,
    IReadOnlyList<CardGameTablePair> ResolvedPairs,
    CardSuit TrumpSuit,
    PlayingCard TrumpCard,
    int DeckCount,
    int AttackerId,
    int DefenderId,
    // Whether the defender could legally перевести right now (there's exactly one pending card,
    // nothing's been beaten yet this round, and the one transfer a round allows hasn't been used) -
    // not whether they actually hold a matching card, which CardGamePanel can tell from their own
    // hand without the server's help.
    bool TransferAvailable,
    // Null while the hand is ongoing. Set the moment the deck runs out and at least one hand is
    // empty: the other player's id if only theirs is, null (a draw) if both emptied at once.
    int? WinnerId,
    bool Finished);

public sealed record CardGameTablePair(PlayingCard Attack, PlayingCard Defense);
