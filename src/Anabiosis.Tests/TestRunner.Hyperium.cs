using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool GalaxyMap_HyperiumCostForDistance_RoundsUpAndNeverBelowOne()
    {
        return GalaxyMap.HyperiumCostForDistance(1f) == 1
            && GalaxyMap.HyperiumCostForDistance(75f) == 1
            && GalaxyMap.HyperiumCostForDistance(76f) == 2
            && GalaxyMap.HyperiumCostForDistance(GalaxyMap.WarpJumpRadius) == 3;
    }

    private static bool World_Hyperium_Jump_BurnsExactlyTheRouteCostFromTheRack()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FlyToSolWarpZoneAndStop(world);
        if (!world.CanWarpNow)
            return false;

        var before = world.HyperiumAboard;
        var cost = world.GalaxyMap.HyperiumCost("sol", "alpha-centauri");
        if (before < cost)
            return false; // starter stock too small for this route - setup problem

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        return world.CreateSnapshot().CurrentSystemId == "alpha-centauri"
            && world.HyperiumAboard == before - cost;
    }

    private static bool World_Hyperium_Jump_UsesPhysicalItemsBeforeTheBuiltInReserve()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slots = world.RackSlots.ToArray();
        for (var i = 0; i < 6; i++)
            slots[i] = ItemType.Hyperium;
        world.LoadRackSlots(slots);
        FlyToSolWarpZoneAndStop(world);
        if (!world.CanWarpNow)
            return false;

        var cost = world.GalaxyMap.HyperiumCost("sol", "alpha-centauri");
        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var itemsLeft = world.RackSlots.Count(s => s == ItemType.Hyperium);
        return world.CreateSnapshot().CurrentSystemId == "alpha-centauri"
            && itemsLeft == Math.Max(0, 6 - cost)
            && world.HyperiumAboard == 6 + World.StarterHyperiumReserve - cost;
    }

    private static bool World_Hyperium_NoFuelAboard_JumpIsRefused()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugSetHyperiumReserve(0);
        FlyToSolWarpZoneAndStop(world);
        if (!world.CanWarpNow || world.HyperiumAboard != 0)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        return world.CreateSnapshot().CurrentSystemId == "sol";
    }
}
