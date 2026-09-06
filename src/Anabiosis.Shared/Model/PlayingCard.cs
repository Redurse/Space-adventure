namespace Anabiosis.Shared.Model;

// Rank 6..14 (11=Валет/Jack, 12=Дама/Queen, 13=Король/King, 14=Туз/Ace) - the standard 36-card
// deck Дурак is played with, no 2-5s. A struct, not a class: two cards with the same Rank/Suit
// are the same card, and a 36-card deck never deals a duplicate, so plain value equality is
// exactly the identity check World.CardGame.cs needs ("does this hand contain this exact card").
public readonly record struct PlayingCard(int Rank, CardSuit Suit)
{
    public const int MinRank = 6;
    public const int MaxRank = 14; // Ace
}
