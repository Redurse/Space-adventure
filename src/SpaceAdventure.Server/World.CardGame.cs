using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// A hand of "Дурак переводной" (transferable fool), 2 players only, played at the ship's
// CardTable. The trigger is purely "2 living, non-bot crew standing at the table together"
// (StepCardGame) - the same continuous room-occupancy check World.ComponentLogic.cs's
// MotionSensor already uses for "is anyone in this room", just narrowed to exactly 2 and to this
// one spot. Nothing is clicked to start or open it; CardGamePanel just appears for the two
// participants the moment the server says a game exists.
//
// Rules implemented (standard 36-card deck, ranks 6-14/Ace, 4 suits):
//  - Deal 6 each, reveal the next card as trump, rest is the draw pile.
//  - Attacker plays a card; defender beats it (higher rank, same suit, or any trump over a
//    non-trump) or takes everything currently on the table.
//  - Переводной: once per round, before anything has been successfully beaten, the defender may
//    instead play a card of the SAME RANK as the one they're facing - this doesn't beat it, it
//    hands the whole attack (now 2 cards) to the other player as the new defender instead. Real
//    переводной lets this chain for as long as each new defender keeps holding a matching card;
//    this implementation caps it at one transfer per round, which is enough to make the mechanic
//    real without an unbounded pending-attack list.
//  - Once every pending attack card is beaten, the attacker may "подкинуть" another card (any
//    rank already showing on the table) or press "Бито" to discard the round and refill both
//    hands from the deck (attacker first) - the round's defender becomes the next round's attacker.
//  - The deck eventually runs out. The first player to empty their hand once it has is safe; the
//    other, if they still hold cards, is the "дурак". Both emptying on the same refill is a draw.
public sealed partial class World
{
    private const float CardGameEndedDisplaySeconds = 6f;
    // The table never holds more undefended cards than this - 2 only ever happens right after the
    // one transfer this implementation permits per round.
    private const int MaxPendingAttacks = 2;
    // Standard Дурак's own "no more than 6 attacking cards a round" rule, expressed as the real
    // limit it enforces: attack + defense together never exceed this many cards laid out on the
    // table at once. Checked where a new attack is thrown in (TryPlayCard) - PendingAttacks is
    // always 0 there, so ResolvedPairs.Count * 2 is exactly how many cards are already down.
    private const int MaxTableCards = 12;

    private CardGameSession? _cardGame;

    private sealed class CardGameSession
    {
        public required int Player1Id { get; init; }
        public required int Player2Id { get; init; }
        public readonly List<PlayingCard> Player1Hand = new();
        public readonly List<PlayingCard> Player2Hand = new();
        public readonly List<PlayingCard> Deck = new();
        public CardSuit TrumpSuit;
        public PlayingCard TrumpCard;
        public readonly List<PlayingCard> PendingAttacks = new();
        public readonly List<(PlayingCard Attack, PlayingCard Defense)> ResolvedPairs = new();
        public bool TransferUsedThisRound;
        public int AttackerId;
        public int DefenderId;
        public int? WinnerId;
        public bool Finished;
        public float EndedSecondsRemaining;

        public List<PlayingCard> HandOf(int playerId) => playerId == Player1Id ? Player1Hand : Player2Hand;
    }

    private void StepCardGame(double deltaSeconds)
    {
        if (_cardGame is { Finished: true } finished)
        {
            finished.EndedSecondsRemaining -= (float)deltaSeconds;
            if (finished.EndedSecondsRemaining <= 0f)
                _cardGame = null;
            return;
        }

        bool StillSeated(int playerId)
        {
            var c = _characters.GetValueOrDefault(playerId);
            return c is { Health: > 0f } && !c.IsBot && c.RoomId == Ship.CardTable.RoomId &&
                (c.Position - Ship.CardTable.Position).Length() < InteractionRadius;
        }

        if (_cardGame is { } active)
        {
            // Either player walking away, dying or disconnecting simply cancels the hand - there's
            // no "pause" state, and nothing is lost since nothing was ever wagered.
            if (!StillSeated(active.Player1Id) || !StillSeated(active.Player2Id))
                _cardGame = null;
            return;
        }

        var seated = _characters.Values
            .Where(c => !c.IsBot && c.Health > 0f && c.RoomId == Ship.CardTable.RoomId &&
                (c.Position - Ship.CardTable.Position).Length() < InteractionRadius)
            .Select(c => c.PlayerId)
            .ToList();
        if (seated.Count == 2)
            _cardGame = StartNewCardGame(seated[0], seated[1]);
    }

    private CardGameSession StartNewCardGame(int player1Id, int player2Id)
    {
        var deck = new List<PlayingCard>(36);
        foreach (var suit in Enum.GetValues<CardSuit>())
            for (var rank = PlayingCard.MinRank; rank <= PlayingCard.MaxRank; rank++)
                deck.Add(new PlayingCard(rank, suit));
        ShuffleCards(deck);

        var session = new CardGameSession { Player1Id = player1Id, Player2Id = player2Id };
        for (var i = 0; i < 6; i++)
        {
            session.Player1Hand.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
            session.Player2Hand.Add(deck[^1]);
            deck.RemoveAt(deck.Count - 1);
        }
        // The next card stays revealed as trump for the rest of the hand (traditionally tucked
        // face-up under the draw pile). Dealing above only ever removed from the *end* of deck, so
        // index 0 is untouched here - keeping it at the front of Deck rather than a separate field
        // means it naturally becomes the last card either player ever draws (RefillHands below only
        // ever takes from the back too), exactly like the physical pile it's standing in for.
        session.TrumpCard = deck[0];
        session.TrumpSuit = deck[0].Suit;
        session.Deck.AddRange(deck);

        // Lowest trump in either starting hand attacks first - the one bit of real Дурак ritual
        // simple enough to keep; no trump in either hand just leaves Player1 to open.
        PlayingCard? LowestTrump(List<PlayingCard> hand) => hand
            .Where(c => c.Suit == session.TrumpSuit)
            .OrderBy(c => c.Rank)
            .Select(c => (PlayingCard?)c)
            .FirstOrDefault();
        var p1Low = LowestTrump(session.Player1Hand);
        var p2Low = LowestTrump(session.Player2Hand);
        var player1Starts = p2Low is null || (p1Low is { } low1 && low1.Rank <= p2Low.Value.Rank);
        session.AttackerId = player1Starts ? player1Id : player2Id;
        session.DefenderId = player1Starts ? player2Id : player1Id;
        return session;
    }

    private void ShuffleCards(List<PlayingCard> deck)
    {
        for (var i = deck.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
    }

    private static bool BeatsCard(PlayingCard defended, PlayingCard candidate, CardSuit trump)
    {
        if (candidate.Suit == defended.Suit)
            return candidate.Rank > defended.Rank;
        return candidate.Suit == trump && defended.Suit != trump;
    }

    private void TryPlayCard(Character character, int rank, CardSuit suit)
    {
        if (_cardGame is not { Finished: false } game)
            return;
        var playerId = character.PlayerId;
        if (playerId != game.Player1Id && playerId != game.Player2Id)
            return;

        var hand = game.HandOf(playerId);
        var card = new PlayingCard(rank, suit);
        if (!hand.Contains(card))
            return;

        if (playerId == game.AttackerId && game.PendingAttacks.Count == 0)
        {
            // Attacking (подкидывание once a round is under way): a fresh round accepts anything,
            // but once cards have already been resolved this round, a new one has to match a rank
            // already showing on the table.
            if (game.ResolvedPairs.Count > 0)
            {
                var ranksInPlay = game.ResolvedPairs.SelectMany(p => new[] { p.Attack.Rank, p.Defense.Rank }).ToHashSet();
                if (!ranksInPlay.Contains(rank))
                    return;
            }
            var defenderHandSize = game.HandOf(game.DefenderId).Count;
            var cardsOnTable = game.ResolvedPairs.Count * 2; // PendingAttacks is always 0 here
            if (cardsOnTable >= MaxTableCards || game.ResolvedPairs.Count >= defenderHandSize)
                return;

            hand.Remove(card);
            game.PendingAttacks.Add(card);
            return;
        }

        if (playerId == game.DefenderId && game.PendingAttacks.Count > 0)
        {
            // Beat: cover the first still-pending card this card legally beats.
            var beatIndex = game.PendingAttacks.FindIndex(p => BeatsCard(p, card, game.TrumpSuit));
            if (beatIndex >= 0)
            {
                hand.Remove(card);
                var defended = game.PendingAttacks[beatIndex];
                game.PendingAttacks.RemoveAt(beatIndex);
                game.ResolvedPairs.Add((defended, card));
                return;
            }

            // Перевод: only before anything's been beaten this round, only once, and only onto a
            // rank actually pending - the receiving player becomes the new defender for what is now
            // (at most) 2 pending cards.
            if (!game.TransferUsedThisRound && game.ResolvedPairs.Count == 0 &&
                game.PendingAttacks.Count < MaxPendingAttacks && game.PendingAttacks.Any(p => p.Rank == rank))
            {
                hand.Remove(card);
                game.PendingAttacks.Add(card);
                game.TransferUsedThisRound = true;
                (game.AttackerId, game.DefenderId) = (game.DefenderId, game.AttackerId);
            }
        }
    }

    private void TryCardGameTake(Character character)
    {
        if (_cardGame is not { Finished: false } game || character.PlayerId != game.DefenderId || game.PendingAttacks.Count == 0)
            return;

        var hand = game.HandOf(game.DefenderId);
        hand.AddRange(game.PendingAttacks);
        foreach (var (attack, defense) in game.ResolvedPairs)
        {
            hand.Add(attack);
            hand.Add(defense);
        }
        game.PendingAttacks.Clear();
        game.ResolvedPairs.Clear();
        game.TransferUsedThisRound = false;

        // The defender just lost this round to a full hand of cards drawing won't fix - only the
        // attacker (who "won" it) refills, then attacks again.
        RefillCardHand(game, game.AttackerId);
        CheckCardGameEnd(game);
    }

    private void TryCardGameEndRound(Character character)
    {
        if (_cardGame is not { Finished: false } game || character.PlayerId != game.AttackerId ||
            game.PendingAttacks.Count > 0 || game.ResolvedPairs.Count == 0)
            return;

        game.ResolvedPairs.Clear();
        game.TransferUsedThisRound = false;
        RefillCardHand(game, game.AttackerId);
        RefillCardHand(game, game.DefenderId);
        (game.AttackerId, game.DefenderId) = (game.DefenderId, game.AttackerId); // the defender won this round
        CheckCardGameEnd(game);
    }

    private void RefillCardHand(CardGameSession game, int playerId)
    {
        var hand = game.HandOf(playerId);
        while (hand.Count < 6 && game.Deck.Count > 0)
        {
            hand.Add(game.Deck[^1]);
            game.Deck.RemoveAt(game.Deck.Count - 1);
        }
    }

    private static void CheckCardGameEnd(CardGameSession game)
    {
        if (game.Deck.Count > 0)
            return;
        var p1Empty = game.Player1Hand.Count == 0;
        var p2Empty = game.Player2Hand.Count == 0;
        if (!p1Empty && !p2Empty)
            return;

        game.Finished = true;
        game.EndedSecondsRemaining = CardGameEndedDisplaySeconds;
        game.WinnerId = p1Empty && p2Empty ? null : p1Empty ? game.Player1Id : game.Player2Id;
    }

    private CardGameState? CreateCardGameState()
    {
        var game = _cardGame;
        if (game is null)
            return null;

        return new CardGameState(
            game.Player1Id, game.Player2Id,
            game.Player1Hand.ToArray(), game.Player2Hand.ToArray(),
            game.PendingAttacks.ToArray(),
            game.ResolvedPairs.Select(p => new CardGameTablePair(p.Attack, p.Defense)).ToArray(),
            game.TrumpSuit, game.TrumpCard, game.Deck.Count,
            game.AttackerId, game.DefenderId,
            !game.TransferUsedThisRound && game.ResolvedPairs.Count == 0 && game.PendingAttacks.Count == 1,
            game.WinnerId, game.Finished);
    }
}
