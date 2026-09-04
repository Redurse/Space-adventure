using System.Linq;
using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // The frigate (World's default ShipKind) puts CardTable in the cockpit at (4, 1) - see
    // Ship.cs's CreateStarter. Spawn is in the corridor (World's own SpawnCharacter comment), so
    // getting there crosses two doors, both centered on y=3 - MoveCharacterTo is a simple
    // bang-bang mover with no real pathing, so heading for (4, 1) directly can leave the door's
    // y=3 band before x clears the wall and get stuck at a room boundary. Routing through y=3
    // waypoints first keeps every leg inside a door's passable band or a single room.
    private const float CardTableX = 4f;
    private const float CardTableY = 1f;

    private static void MoveCharacterToCardTable(World world, int playerId)
    {
        MoveCharacterTo(world, playerId, 9f, 3f); // reactor room, aligned with both doors
        MoveCharacterTo(world, playerId, CardTableX, 3f); // now inside the cockpit
        MoveCharacterTo(world, playerId, CardTableX, CardTableY); // same-room hop, no door needed
        world.ApplyCommand(playerId, new ClientCommand(playerId)); // stop drifting once arrived
    }

    // Sitting down no longer auto-starts a hand (World.CardTable.cs) - it just makes the table's
    // choice available. Getting an actual game running needs one more step: either seated player
    // sends ChooseCardTableGame.
    private static void ChooseCardTableGame(World world, int playerId, CardTableGameKind kind)
    {
        world.ApplyCommand(playerId, new ClientCommand(playerId, ChooseCardTableGame: kind));
        world.Step(RealtimeStep);
    }

    private static bool World_CardGame_TwoPlayersAtTableOffersAChoiceButDoesNotAutoStart()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);

        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        return snapshot.CardGame is null && snapshot.FrontsGame is null &&
            snapshot.CardTableChoiceSeatedIds is { Count: 2 } seated && seated.Contains(1) && seated.Contains(2);
    }

    // Direct user request - "активировать дурак надо вдвоем нажать на стол": one player choosing
    // Дурак alone must NOT start it, only register their own vote and leave the table still open.
    private static bool World_CardGame_OnePlayerChoosingDurakAloneDoesNotStartIt()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);

        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Durak);

        var snapshot = world.CreateSnapshot();
        return snapshot.CardGame is null &&
            snapshot.CardTableChoiceSeatedIds is { Count: 2 } &&
            snapshot.CardTableDurakVotes is { Count: 1 } votes && votes.Contains(1);
    }

    private static bool World_CardGame_BothPlayersChoosingDurakStartsAHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);

        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Durak);
        ChooseCardTableGame(world, 2, CardTableGameKind.Durak);

        var snapshot = world.CreateSnapshot();
        var game = snapshot.CardGame;
        return game is not null && snapshot.CardTableChoiceSeatedIds is null && snapshot.CardTableDurakVotes is null &&
            game.Player1Hand.Count == 6 && game.Player2Hand.Count == 6 &&
            game.DeckCount == 24 && (game.AttackerId == 1 || game.AttackerId == 2) && game.DefenderId != game.AttackerId;
    }

    private static bool World_CardGame_WalkingAwayClearsAPendingDurakVote()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);

        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Durak);
        if (world.CreateSnapshot().CardTableDurakVotes is not { Count: 1 })
            return false; // the vote never registered - nothing to clear

        MoveCharacterTo(world, 1, CardTableX, 5.5f); // clear across the cockpit, well outside InteractionRadius
        world.ApplyCommand(1, new ClientCommand(1));
        world.Step(RealtimeStep);
        if (world.CreateSnapshot().CardTableDurakVotes is not null)
            return false; // walking away should have dropped the vote

        // Coming back and having BOTH choose again must still work - a stale vote isn't silently
        // resurrected once the same player returns.
        MoveCharacterToCardTable(world, 1);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Durak);
        ChooseCardTableGame(world, 2, CardTableGameKind.Durak);
        return world.CreateSnapshot().CardGame is not null;
    }

    // Solo does offer a choice now (direct user request, "можно играть в хойку в одиночку" -
    // World_FrontsGame_SoloPlayerChoosingFrontsStartsAMatchAgainstABot proves what it actually
    // offers) - it just never starts Дурак, which is what this test guards.
    private static bool World_CardGame_OnlyOnePlayerAtTableCannotStartDurak()
    {
        var world = new World();
        world.SpawnCharacter(1);

        MoveCharacterToCardTable(world, 1);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Durak);

        return world.CreateSnapshot().CardGame is null;
    }

    private static bool World_CardGame_WalkingAwayCancelsTheHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);

        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);
        ChooseCardTableGame(world, 1, CardTableGameKind.Durak);
        ChooseCardTableGame(world, 2, CardTableGameKind.Durak);
        if (world.CreateSnapshot().CardGame is null)
            return false; // the hand never started - nothing to cancel

        MoveCharacterTo(world, 2, CardTableX, 5.5f); // clear across the cockpit, well outside InteractionRadius
        world.ApplyCommand(2, new ClientCommand(2));
        world.Step(RealtimeStep);

        return world.CreateSnapshot().CardGame is null;
    }
}
