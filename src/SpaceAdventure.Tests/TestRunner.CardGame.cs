using SpaceAdventure.Server;
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

    private static bool World_CardGame_TwoPlayersAtTableStartAHand()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.SpawnCharacter(2);

        MoveCharacterToCardTable(world, 1);
        MoveCharacterToCardTable(world, 2);
        world.Step(RealtimeStep);

        var game = world.CreateSnapshot().CardGame;
        return game is not null && game.Player1Hand.Count == 6 && game.Player2Hand.Count == 6 &&
            game.DeckCount == 24 && (game.AttackerId == 1 || game.AttackerId == 2) && game.DefenderId != game.AttackerId;
    }

    private static bool World_CardGame_OnlyOnePlayerAtTableDoesNotStart()
    {
        var world = new World();
        world.SpawnCharacter(1);

        MoveCharacterToCardTable(world, 1);
        world.Step(RealtimeStep);

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
        if (world.CreateSnapshot().CardGame is null)
            return false; // the hand never started - nothing to cancel

        MoveCharacterTo(world, 2, CardTableX, 5.5f); // clear across the cockpit, well outside InteractionRadius
        world.ApplyCommand(2, new ClientCommand(2));
        world.Step(RealtimeStep);

        return world.CreateSnapshot().CardGame is null;
    }
}
