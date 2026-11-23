using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The revolver, after Barotrauma's: one shot per trigger pull, six rounds in the cylinder that come from a stack of rounds dragged onto it, a reload
// from a stack in the pockets once it runs dry.
internal static partial class TestRunner
{
    private static (World World, int RevolverSlot) RevolverWorld(float loaded)
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.Revolver);
        var slots = world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.ToArray();
        var slot = Array.IndexOf(slots, ItemType.Revolver);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
        if (loaded > 0f)
        {
            world.DebugGiveItem(1, ItemType.RevolverRounds);
            var rounds = Array.IndexOf(world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.ToArray(), ItemType.RevolverRounds);
            world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: rounds, AttachTankToSlot: slot));
            world.DebugBody(1); // (nothing - just keeps the call shape of the other helpers)
        }
        return (world, slot);
    }

    private static float? Loaded(World world, int slot) => world.CreateSnapshot().Characters.Single().Inventory!.MainSlotTanks[slot];

    private static bool Revolver_RoundsLoadIntoTheCylinderAsAStackOfSix()
    {
        var (world, slot) = RevolverWorld(1f);
        return Loaded(world, slot) == RevolverDefinitions.CylinderRounds
            && !world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.Contains(ItemType.RevolverRounds);
    }

    private static bool Revolver_FiresOneRoundPerPullAndRespectsItsCooldown()
    {
        var (world, slot) = RevolverWorld(1f);
        world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
        world.Step(RealtimeStep);
        if (Loaded(world, slot) != 5f || world.CreateSnapshot().PersonalShots.Count != 1)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true)); // too soon: the hammer has to be cocked again
        world.Step(RealtimeStep);
        if (Loaded(world, slot) != 5f)
            return false;

        StepFor(world, (int)(WeaponDefinitions.CooldownSeconds(ItemType.Revolver) * 30) + 2);
        world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
        world.Step(RealtimeStep);
        return Loaded(world, slot) == 4f;
    }

    private static bool Revolver_EmptyCylinderReloadsFromAStackBeforeFiringAgain()
    {
        var (world, slot) = RevolverWorld(1f);
        // Spend all six rounds.
        for (var shot = 0; shot < 6; shot++)
        {
            world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
            StepFor(world, 20);
        }
        if (Loaded(world, slot) is not null)
            return false; // a spent cylinder is simply empty, there is nothing to take out

        // With no rounds anywhere the hammer only clicks.
        world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
        StepFor(world, 20);
        if (Loaded(world, slot) is not null)
            return false;

        // A stack in the pockets: the next pull reloads (no shot that pull), the one after fires.
        world.DebugGiveItem(1, ItemType.RevolverRounds);
        world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
        world.Step(RealtimeStep);
        if (Loaded(world, slot) != 6f)
            return false;
        StepFor(world, (int)(World.RevolverReloadSeconds * 30) + 2);
        world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
        world.Step(RealtimeStep);
        return Loaded(world, slot) == 5f;
    }

    private static bool Revolver_ADraggedStackOnlyTopsTheCylinderUp()
    {
        var (world, slot) = RevolverWorld(1f);
        for (var shot = 0; shot < 2; shot++)
        {
            world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
            StepFor(world, 20);
        }
        if (Loaded(world, slot) != 4f)
            return false;

        world.DebugGiveItem(1, ItemType.RevolverRounds);
        var stack = Array.IndexOf(world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.ToArray(), ItemType.RevolverRounds);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: stack, AttachTankToSlot: slot));
        var inventory = world.CreateSnapshot().Characters.Single().Inventory!;
        // Two rounds went in, four stay in the stack.
        return Loaded(world, slot) == 6f && inventory.MainSlots[stack] == ItemType.RevolverRounds && inventory.MainSlotTanks[stack] == 4f;
    }

    private static bool Revolver_IsASoldItemWithItsOwnNameAndPrice()
    {
        return TradeCatalog.Goods.Any(g => g.Item == ItemType.Revolver && TradeCatalog.IsForSale(g))
            && TradeCatalog.Goods.Any(g => g.Item == ItemType.RevolverRounds && TradeCatalog.IsForSale(g))
            && ItemDefinitions.DisplayName(ItemType.Revolver) == "револьвер"
            && WeaponDefinitions.IsWeapon(ItemType.Revolver) && ItemDefinitions.HandsRequired(ItemType.Revolver) == 1
            && TankSockets.AcceptedTank(ItemType.Revolver) == ItemType.RevolverRounds;
    }
}
