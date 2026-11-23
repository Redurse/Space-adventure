using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The docked station's reactor as shore power for the ship (World.StationPower.cs).
internal static partial class TestRunner
{
    private static void StepSeconds2(World world, double seconds)
    {
        for (var i = 0; i < (int)(seconds * 30); i++)
            world.Step(RealtimeStep);
    }

    // Off until the player plugs in, available at a station that has a reactor.
    private static bool StationPower_StartsAvailableButUnplugged()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var power = world.CreateSnapshot().Station.Power;
        return power is { Available: true, Connected: false, Delivered: 0f };
    }

    // The plug is pulled standing at the station's reactor, not from across the station.
    private static bool StationPower_ToggleNeedsStandingAtTheStationReactor()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, ToggleShorePowerPressed: true)); // aboard the ship, far from it
        if (world.CreateSnapshot().Station.Power!.Connected)
            return false;
        var reactor = world.CreateSnapshot().ExtraReactors!.First(r => r.Id.StartsWith("home-station"));
        world.DebugPlaceCharacterOnStation(1, reactor.Position + new Vec2(0, 3));
        world.ApplyCommand(1, new ClientCommand(1, ToggleShorePowerPressed: true));
        var connected = world.CreateSnapshot().Station.Power!.Connected;
        world.ApplyCommand(1, new ClientCommand(1, ToggleShorePowerPressed: true));
        return connected && !world.CreateSnapshot().Station.Power!.Connected;
    }

    // Plugged in, the station feeds the ship's grid, so the ship's own reactor is asked for much less.
    private static bool StationPower_ShorePowerRelievesTheShipReactor()
    {
        float Output(bool shore)
        {
            var world = new World();
            world.SpawnCharacter(1);
            world.DebugSetShorePower(shore);
            StepSeconds2(world, 20);
            var snapshot = world.CreateSnapshot();
            if (shore && snapshot.Station.Power!.Delivered <= 1f)
                return float.MaxValue;
            return snapshot.Power.ReactorOutput;
        }
        var alone = Output(false);
        var shored = Output(true);
        Console.WriteLine($"     ship reactor output alone {alone:0.0}, with shore {shored:0.0}");
        return alone > 1f && shored < alone * 0.6f;
    }

    // Casting off pulls the plug by itself: nothing is fed once the ship is away.
    private static bool StationPower_UndockingCutsTheShoreFeed()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugSetShorePower(true);
        StepSeconds2(world, 3);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        StepSeconds2(world, 2);
        var power = world.CreateSnapshot().Station.Power;
        return !world.IsDocked && (power is null || (!power.Available && !power.Connected && power.Delivered == 0f));
    }
}
