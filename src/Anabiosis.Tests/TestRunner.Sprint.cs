using Anabiosis.Server;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Distance covered walking straight along +Y in the spawn corridor for a few ticks.
    private static double WalkedDistance(bool sprint)
    {
        var world = new World();
        world.SpawnCharacter(1);
        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        for (var i = 0; i < 6; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 1, Sprint: sprint));
            world.Step(RealtimeStep);
        }
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return Math.Sqrt((after.X - before.X) * (after.X - before.X) + (after.Y - before.Y) * (after.Y - before.Y));
    }

    private static bool World_Sprint_ShiftWalksOneAndAQuarterTimesFaster()
    {
        var walk = WalkedDistance(sprint: false);
        var run = WalkedDistance(sprint: true);
        return walk > 0.1 && Math.Abs(run / walk - World.SprintSpeedMultiplier) < 0.02;
    }

    private static bool World_Sprint_WithoutMovementInputDoesNotMoveTheCharacter()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        for (var i = 0; i < 6; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, Sprint: true));
            world.Step(RealtimeStep);
        }
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return before.X == after.X && before.Y == after.Y;
    }
}
