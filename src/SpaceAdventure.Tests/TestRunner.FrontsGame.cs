using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool World_FrontsGame_ChoosingFrontsStartsAMatch()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);

        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 2, CardTableGameKind.Fronts);

        var snapshot = world.CreateSnapshot();
        var game = snapshot.FrontsGame;
        return game is not null && snapshot.CardTableChoiceSeatedIds is null && snapshot.CardGame is null &&
            (game.PlayerAId == 1 || game.PlayerAId == 2) && game.PlayerBId != game.PlayerAId &&
            game.Turn == 1 && game.FrontProgress.Count == 3 && game.FrontProgress[0] == 0 && game.FrontProgress[1] == 0 && game.FrontProgress[2] == 0 &&
            !game.Captured[0] && !game.Captured[1] && !game.Captured[2];
    }

    private static bool World_FrontsGame_OutcommittingAFrontPushesItAndResetsAllocationsAfterResolve()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Fronts);

        var game = world.CreateSnapshot().FrontsGame!;
        var playerA = game.PlayerAId;
        var playerB = game.PlayerBId;

        world.ApplyCommand(playerA, new ClientCommand(playerA, FrontsSetAllocationIndex: 0, FrontsSetAllocationAmount: 9));
        world.ApplyCommand(playerB, new ClientCommand(playerB, FrontsSetAllocationIndex: 0, FrontsSetAllocationAmount: 0));
        world.Step(RealtimeStep);
        world.ApplyCommand(playerA, new ClientCommand(playerA, FrontsResolvePressed: true));
        world.Step(RealtimeStep);

        var after = world.CreateSnapshot().FrontsGame!;
        // 9-0 net commitment clamps to the max +-3 shift per turn, always in the outcommitting
        // side's favor, and both sides' allocations reset to 0 for the next turn.
        return after.Turn == 2 && after.FrontProgress[0] == 3 &&
            after.AllocationA[0] == 0 && after.AllocationB[0] == 0;
    }

    private static bool World_FrontsGame_CapturingAFrontLocksItAgainstFurtherAllocation()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Fronts);
        var startGame = world.CreateSnapshot().FrontsGame!;
        var playerA = startGame.PlayerAId;
        var playerB = startGame.PlayerBId;

        // +3/turn, needs 2 resolves to clear FrontRange=5 (3, then 6 clamped to 5).
        for (var i = 0; i < 2; i++)
        {
            world.ApplyCommand(playerA, new ClientCommand(playerA, FrontsSetAllocationIndex: 0, FrontsSetAllocationAmount: 9));
            world.ApplyCommand(playerB, new ClientCommand(playerB, FrontsSetAllocationIndex: 0, FrontsSetAllocationAmount: 0));
            world.Step(RealtimeStep);
            world.ApplyCommand(playerA, new ClientCommand(playerA, FrontsResolvePressed: true));
            world.Step(RealtimeStep);
        }

        var game = world.CreateSnapshot().FrontsGame!;
        if (!game.Captured[0] || game.FrontProgress[0] != 5)
            return false;

        // A further attempt to allocate to the now-captured front must be silently rejected.
        world.ApplyCommand(playerA, new ClientCommand(playerA, FrontsSetAllocationIndex: 0, FrontsSetAllocationAmount: 4));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().FrontsGame!.AllocationA[0] == 0;
    }

    private static bool World_FrontsGame_SweepingAllThreeFrontsDeclaresTheSweepingSideTheWinner()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Fronts);
        var startGame = world.CreateSnapshot().FrontsGame!;
        var playerA = startGame.PlayerAId;
        var playerB = startGame.PlayerBId;

        // Player A commits the whole pool to one uncaptured front at a time (fastest possible
        // capture rate - splitting it would only slow every front down); Player B never commits
        // anything. Each front needs 2 resolves to sweep (same math
        // World_FrontsGame_CapturingAFrontLocksItAgainstFurtherAllocation already proved), so all 3
        // need at most 6 - a handful of spare turns above that keeps this from being fragile.
        for (var turn = 0; turn < 8; turn++)
        {
            var game = world.CreateSnapshot().FrontsGame!;
            if (game.Finished)
                break;
            var remaining = game.ArmyPool;
            for (var front = 0; front < 3 && remaining > 0; front++)
            {
                if (game.Captured[front])
                    continue;
                var give = System.Math.Min(remaining, game.ArmyPool);
                world.ApplyCommand(playerA, new ClientCommand(playerA, FrontsSetAllocationIndex: front, FrontsSetAllocationAmount: give));
                remaining -= give;
            }
            world.Step(RealtimeStep);
            world.ApplyCommand(playerA, new ClientCommand(playerA, FrontsResolvePressed: true));
            world.Step(RealtimeStep);
        }

        var finished = world.CreateSnapshot().FrontsGame!;
        return finished.Finished && finished.WinnerId == playerA &&
            finished.Captured[0] && finished.Captured[1] && finished.Captured[2];
    }

    private static bool World_FrontsGame_WalkingAwayCancelsTheMatch()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);
        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Fronts);
        if (world.CreateSnapshot().FrontsGame is null)
            return false;

        MoveCharacterTo(world, 2, CardTableX, 5.5f); // clear across the cockpit, well outside InteractionRadius
        world.ApplyCommand(2, new ClientCommand(2));
        world.Step(RealtimeStep);

        return world.CreateSnapshot().FrontsGame is null;
    }

    // Direct user request - "можно играть в хойку в одиночку": alone at the table, Фронты starts
    // against a bot opponent instead of needing a second real hand.
    private static bool World_FrontsGame_SoloPlayerChoosingFrontsStartsAMatchAgainstABot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterToCardTable(world, 1);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Fronts);

        var snapshot = world.CreateSnapshot();
        var game = snapshot.FrontsGame;
        return game is not null && game.VsBot && game.PlayerAId == 1 && game.PlayerBId != 1 &&
            snapshot.CardTableChoiceSeatedIds is null;
    }

    // Дурак genuinely needs a second real hand - a lone player choosing it must be silently
    // ignored (no bot mode was asked for it), leaving the table's choice step still open.
    private static bool World_FrontsGame_SoloPlayerChoosingDurakDoesNothing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterToCardTable(world, 1);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Durak);

        var snapshot = world.CreateSnapshot();
        return snapshot.CardGame is null && snapshot.FrontsGame is null &&
            snapshot.CardTableChoiceSeatedIds is { Count: 1 };
    }

    // Direct user request - "бот, который зеркалит/противодействует игроку": the bot always
    // concentrates its whole pool on whichever uncaptured front the player currently has the
    // LEAST committed to (their weakest spot), recomputed continuously - not just at resolve time.
    private static bool World_FrontsGame_BotConcentratesOnThePlayersWeakestFront()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterToCardTable(world, 1);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Fronts);

        // Front 1 (index 1, Центральный) is the player's weakest - everything else is stronger.
        world.ApplyCommand(1, new ClientCommand(1, FrontsSetAllocationIndex: 0, FrontsSetAllocationAmount: 4));
        world.ApplyCommand(1, new ClientCommand(1, FrontsSetAllocationIndex: 1, FrontsSetAllocationAmount: 1));
        world.ApplyCommand(1, new ClientCommand(1, FrontsSetAllocationIndex: 2, FrontsSetAllocationAmount: 5));
        world.Step(RealtimeStep); // StepFrontsGame's per-tick update, no resolve pressed yet

        var game = world.CreateSnapshot().FrontsGame!;
        return game.AllocationB[0] == 0 && game.AllocationB[1] == game.ArmyPool && game.AllocationB[2] == 0;
    }
}
