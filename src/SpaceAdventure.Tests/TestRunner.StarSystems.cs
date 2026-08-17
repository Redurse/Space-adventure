using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // The galaxy is more than one system from the start (GalaxyMap.CreateStarter's "sol" +
    // "alpha-centauri"), even though nothing can reach the second one yet (M31 is data-model
    // only) - and a fresh crew's home station is unambiguously in the first.
    private static bool World_StarSystem_GalaxyHasMoreThanOneSystemFromTheStart()
    {
        var world = new World();
        return world.GalaxyMap.Systems.Count > 1
            && world.GalaxyMap.SystemOf("home-station").Id == "sol"
            && world.AsteroidField == world.GalaxyMap.GetSystem("sol").Field;
    }

    // Flying to the system's own WarpPoint and parking there arms CanWarpNow - the same "parked
    // alongside, under the speed limit" gate as docking (World.StationDocking.cs's CanDockNow),
    // just for a different kind of point (World.StarSystems.cs).
    private static bool World_StarSystem_FlyToWarpPointThenJumpToOtherSystem()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sol-warp-point"));
        for (var i = 0; i < 10 * 30 && !world.CanWarpNow; i++)
            world.Step(RealtimeStep);

        if (!world.CanWarpNow)
            return false; // never reached the warp point - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var snapshot = world.CreateSnapshot();

        return snapshot.CurrentSystemId == "alpha-centauri"
            && world.AsteroidField == world.GalaxyMap.GetSystem("alpha-centauri").Field;
    }

    // Only arms once actually parked at the warp point - mashing the button from across the
    // system (or from a different system entirely) must not teleport the ship.
    private static bool World_StarSystem_WarpDoesNothingWithoutBeingAtTheWarpPoint()
    {
        var world = new World();
        world.SpawnCharacter(1);

        if (world.CanWarpNow)
            return false; // starts docked, nowhere near the warp point - setup problem

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        return world.CreateSnapshot().CurrentSystemId == "sol";
    }
}
