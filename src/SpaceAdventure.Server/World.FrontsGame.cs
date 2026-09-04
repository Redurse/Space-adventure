using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// "Фронты" - a simplified Hearts of Iron IV-style 2-player wargame at the ship's one CardTable -
// one of 2 games it can now run (World.CardTable.cs, Дурак переводной is the other,
// World.CardGame.cs). Direct user request ("повтори игру hearts of iron 4"): full HOI4 (map,
// divisions, supply, tech, diplomacy) has no place in a card-table minigame, so this keeps just its
// core shape - a handful of independent fronts, a shared reinforcement pool, pushing a front to
// either end captures territory - agreed with the user as the right level of abstraction.
//
// 3 independent fronts (Северный/Центральный/Южный), each a single int position from -FrontRange
// to +FrontRange (0 is the untouched contested start). Both sides freely set how many of their
// fixed ArmyPool points go to each front - no hidden state, same no-secrets trust model
// CardGameState's own doc comment already established for the table's other game - and either one
// presses "Провести бой" (TryResolveFrontsBattle) to resolve using whatever is currently set for
// both: the side with the bigger net commitment on a front pushes it their way. Reaching
// +-FrontRange captures that front for good - no further allocation there, ever. The match ends
// once all 3 are captured (win = more captured fronts, a 3-0 or 2-1 sweep is always decisive) or
// the turn cap is hit (win = more captured fronts, ties broken by summed signed FrontProgress).
public sealed partial class World
{
    private const int FrontCount = 3;
    private const int FrontRange = 5; // -5..+5; reaching either end captures the front
    private const int FrontsArmyPool = 10; // per side, per turn - resets on every resolve, no carry-over
    private const int FrontsTurnCap = 30;
    private const float FrontsGameEndedDisplaySeconds = 6f;
    // Real player ids start at 1 (GameServer's own Interlocked.Increment) and only ever go up, so
    // this can never collide with one - the solo-vs-bot opponent (direct user request, "можно
    // играть в хойку в одиночку") has no character/connection of its own, just this sentinel id.
    private const int FrontsBotPlayerId = -1;

    private FrontsGameSession? _frontsGame;

    private sealed class FrontsGameSession
    {
        public required int PlayerAId { get; init; }
        public required int PlayerBId { get; init; }
        public required bool VsBot { get; init; }
        public readonly int[] FrontProgress = new int[FrontCount];
        public readonly int[] AllocationA = new int[FrontCount];
        public readonly int[] AllocationB = new int[FrontCount];
        public readonly bool[] Captured = new bool[FrontCount];
        public int Turn = 1;
        public int? WinnerId;
        public bool Finished;
        public float EndedSecondsRemaining;

        public int[] AllocationOf(int playerId) => playerId == PlayerAId ? AllocationA : AllocationB;
    }

    private void StepFrontsGame(double deltaSeconds)
    {
        if (_frontsGame is { Finished: true } finished)
        {
            finished.EndedSecondsRemaining -= (float)deltaSeconds;
            if (finished.EndedSecondsRemaining <= 0f)
                _frontsGame = null;
            return;
        }

        if (_frontsGame is { } active)
        {
            // Either player walking away, dying or disconnecting simply cancels the match - same
            // no-pause, nothing-wagered shape World.CardGame.cs's own StepCardGame already uses.
            // The bot opponent has no character to check for, obviously - only the real human side
            // has to still be at the table for a solo match to keep going.
            var seated = SeatedAtCardTable();
            var stillGoing = active.VsBot ? seated.Contains(active.PlayerAId) : seated.Contains(active.PlayerAId) && seated.Contains(active.PlayerBId);
            if (!stillGoing)
                _frontsGame = null;
            // Recomputed every tick (not just at resolve time) so FrontsGamePanel's own "Противник:"
            // readout reacts live as the human adjusts their own sliders, the same live-transparency
            // feel the table's other game already gives both real hands (CardGameState's own doc
            // comment) - there is no secret here to protect by only revealing it at resolve time.
            else if (active.VsBot)
                UpdateBotAllocation(active);
        }
    }

    private static FrontsGameSession StartNewFrontsGame(int playerAId, int playerBId, bool vsBot) =>
        new() { PlayerAId = playerAId, PlayerBId = playerBId, VsBot = vsBot };

    // "Зеркалит/противодействует игроку" (direct user request): attacks wherever the human is
    // currently investing the LEAST on an uncaptured front - not a fixed strategy, so spreading
    // thin to defend everywhere just means the bot finds an even weaker spot to push through, and
    // committing everything to one front leaves every other front open for it to walk through
    // instead. Ties (including "nothing allocated anywhere yet") resolve to the lowest front index.
    private static void UpdateBotAllocation(FrontsGameSession game)
    {
        var weakestFront = -1;
        var weakestValue = int.MaxValue;
        for (var i = 0; i < FrontCount; i++)
        {
            if (game.Captured[i])
                continue;
            if (game.AllocationA[i] < weakestValue)
            {
                weakestValue = game.AllocationA[i];
                weakestFront = i;
            }
        }
        Array.Clear(game.AllocationB);
        if (weakestFront >= 0)
            game.AllocationB[weakestFront] = FrontsArmyPool;
    }

    private void TrySetFrontsAllocation(Character character, int frontIndex, int amount)
    {
        if (_frontsGame is not { Finished: false } game || frontIndex < 0 || frontIndex >= FrontCount)
            return;
        var playerId = character.PlayerId;
        if (playerId != game.PlayerAId && playerId != game.PlayerBId)
            return;
        if (game.Captured[frontIndex])
            return;

        var mine = game.AllocationOf(playerId);
        mine[frontIndex] = Math.Clamp(amount, 0, FrontsArmyPool);

        // The pool is shared across all 3 fronts, not one budget per front - if that just pushed
        // the total over FrontsArmyPool, trim the other (uncaptured) fronts' own allocations back
        // down until it fits again, rather than silently letting the side spend more than it has.
        var overflow = mine[0] + mine[1] + mine[2] - FrontsArmyPool;
        for (var i = 0; i < FrontCount && overflow > 0; i++)
        {
            if (i == frontIndex || game.Captured[i])
                continue;
            var cut = Math.Min(mine[i], overflow);
            mine[i] -= cut;
            overflow -= cut;
        }
    }

    private void TryResolveFrontsBattle(Character character)
    {
        if (_frontsGame is not { Finished: false } game)
            return;
        if (character.PlayerId != game.PlayerAId && character.PlayerId != game.PlayerBId)
            return;
        // One last, fully up-to-date reaction to whatever the human just set, in case this
        // resolve lands the same tick as their last allocation change (StepFrontsGame's own
        // per-tick update would otherwise be a tick stale here).
        if (game.VsBot)
            UpdateBotAllocation(game);

        for (var i = 0; i < FrontCount; i++)
        {
            if (game.Captured[i])
                continue;
            var delta = game.AllocationA[i] - game.AllocationB[i];
            // A tie holds the line exactly where it is - a real stalemate, not a rounding quirk.
            var shift = Math.Clamp(delta / 3, -3, 3);
            game.FrontProgress[i] = Math.Clamp(game.FrontProgress[i] + shift, -FrontRange, FrontRange);
            if (Math.Abs(game.FrontProgress[i]) >= FrontRange)
                game.Captured[i] = true;
        }
        Array.Clear(game.AllocationA);
        Array.Clear(game.AllocationB);
        game.Turn++;
        CheckFrontsGameEnd(game);
    }

    private static void CheckFrontsGameEnd(FrontsGameSession game)
    {
        var allCaptured = true;
        for (var i = 0; i < FrontCount; i++)
            if (!game.Captured[i])
            {
                allCaptured = false;
                break;
            }
        if (!allCaptured && game.Turn <= FrontsTurnCap)
            return;

        var aFronts = 0;
        var bFronts = 0;
        var signedTotal = 0;
        for (var i = 0; i < FrontCount; i++)
        {
            signedTotal += game.FrontProgress[i];
            if (!game.Captured[i])
                continue;
            if (game.FrontProgress[i] > 0)
                aFronts++;
            else
                bFronts++;
        }

        game.Finished = true;
        game.EndedSecondsRemaining = FrontsGameEndedDisplaySeconds;
        game.WinnerId = aFronts != bFronts ? (aFronts > bFronts ? game.PlayerAId : game.PlayerBId)
            : signedTotal != 0 ? (signedTotal > 0 ? game.PlayerAId : game.PlayerBId)
            : null; // a genuine, fully symmetric draw
    }

    private FrontsGameState? CreateFrontsGameState()
    {
        var game = _frontsGame;
        if (game is null)
            return null;

        return new FrontsGameState(
            game.PlayerAId, game.PlayerBId,
            game.FrontProgress.ToArray(), game.AllocationA.ToArray(), game.AllocationB.ToArray(), game.Captured.ToArray(),
            FrontsArmyPool, game.Turn, FrontsTurnCap,
            game.WinnerId, game.Finished, game.VsBot);
    }
}
