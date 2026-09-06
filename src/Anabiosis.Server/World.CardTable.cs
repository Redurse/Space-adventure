using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Shared "who is sitting at the one CardTable and what do they want to play" logic - split out of
// World.CardGame.cs (which used to own this alone, back when Дурак was the only game the table
// could ever start) now that Ship.CardTable can seat either Дурак (World.CardGame.cs) or Фронты
// (World.FrontsGame.cs). Direct user request - "чтобы на карточном столе можно было выбирать игры".
//
// Sitting down no longer auto-starts anything: 2 living, non-bot crew standing at the table with
// NEITHER game active just makes the table available to choose from (CreateCardTableChoiceSeatedIds,
// surfaced to CardTableChoicePanel) - the match itself only begins once a game button is actually
// pressed (TryChooseCardTableGame). Walking away before choosing simply clears the availability
// (and any pending Дурак vote below); walking away mid-hand/mid-match is still each game's own
// StepXxx's job to catch.
//
// Direct user request - "активировать дурак надо вдвоем нажать на стол": Дурак specifically needs
// BOTH seated players to each choose it before it actually starts (mutual consent - one player can
// no longer unilaterally drag the other into a hand they only happened to be standing near for).
// Фронты keeps the original one-click activation - agreed with the user as the simpler, sufficient
// rule for a 2-side duel, where picking it already IS the challenge.
public sealed partial class World
{
    private readonly HashSet<int> _durakVotes = new();

    private List<int> SeatedAtCardTable() => _characters.Values
        .Where(c => !c.IsBot && c.Health > 0f && c.RoomId == Ship.CardTable.RoomId &&
            (c.Position - Ship.CardTable.Position).Length() < InteractionRadius)
        .Select(c => c.PlayerId)
        .ToList();

    // Runs every tick regardless of which (if any) game is active - a stray vote from someone who
    // has since walked away from the table must not silently count toward starting a hand later,
    // whether or not a match happens to be running for someone else in the meantime.
    private void StepCardTable()
    {
        var seated = SeatedAtCardTable();
        _durakVotes.RemoveWhere(id => !seated.Contains(id));
    }

    private void TryChooseCardTableGame(Character character, CardTableGameKind kind)
    {
        if (_cardGame is not null || _frontsGame is not null)
            return;
        var seated = SeatedAtCardTable();
        if (!seated.Contains(character.PlayerId))
            return;

        // Solo (direct user request, "можно играть в хойку в одиночку") - only Фронты makes sense
        // against a bot; Дурак still genuinely needs a second real hand, so a lone Дурак click is
        // silently ignored rather than ever starting one against no one.
        if (seated.Count == 1)
        {
            if (kind == CardTableGameKind.Fronts)
                _frontsGame = StartNewFrontsGame(seated[0], FrontsBotPlayerId, vsBot: true);
            return;
        }
        if (seated.Count != 2)
            return;

        if (kind == CardTableGameKind.Fronts)
        {
            _frontsGame = StartNewFrontsGame(seated[0], seated[1], vsBot: false);
            _durakVotes.Clear();
            return;
        }

        _durakVotes.Add(character.PlayerId);
        if (_durakVotes.Contains(seated[0]) && _durakVotes.Contains(seated[1]))
        {
            _cardGame = StartNewCardGame(seated[0], seated[1]);
            _durakVotes.Clear();
        }
    }

    private IReadOnlyList<int>? CreateCardTableChoiceSeatedIds() =>
        _cardGame is null && _frontsGame is null && SeatedAtCardTable() is { Count: 1 or 2 } seated ? seated : null;

    private IReadOnlyList<int>? CreateCardTableDurakVotes() => _durakVotes.Count > 0 ? _durakVotes.ToArray() : null;
}
