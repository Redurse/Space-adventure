using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Direct user request ("сделай меню фабрикатора как в баротравме" / "добавим множество
    // предметов материалов") - a minimal one-room custom ship carrying a single Фабрикатор
    // (CustomDeviceKind.Fabricator) and one Деконструктор, same "small ship built just for this one
    // interaction" shape as TestRunner.ClickInteract.cs's own BuildTwoTerminalShipDefinition.
    private static CustomShipDefinition BuildFabricatorShipDefinition() => new(
        "Тестовый корабль с фабрикатором",
        new[] { new CustomRoomDef("a", "Мостик", 0, 0, 8, 8) },
        System.Array.Empty<CustomDoorDef>(),
        new[] { new CustomAirlockDef("a", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 4),
            new CustomDeviceDef(CustomDeviceKind.Helm, 4, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 4, 4),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 7, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 7, 2),
            new CustomDeviceDef(CustomDeviceKind.Fabricator, 6, 6),
            new CustomDeviceDef(CustomDeviceKind.Deconstructor, 2, 6),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(2f, 5f, TileSide.West, 20f) });

    // Direct user request ("процесс сборки и разборки... занимал 2 секунды") - steps a little past
    // FabricatorCatalog.ProductionDurationSeconds so a timer that started this tick is guaranteed to
    // have completed by the time the caller checks anything.
    private static void StepPastProductionTimer(World world) => StepSeconds(world, FabricatorCatalog.ProductionDurationSeconds + 0.1);

    private static void StepSeconds(World world, double seconds)
    {
        var ticks = (int)System.Math.Ceiling(seconds / RealtimeStep);
        for (var i = 0; i < ticks; i++)
            world.Step(RealtimeStep);
    }

    private static bool World_Fabricator_CraftsSteelPlate_ConsumesIngredientsAndProducesOutput()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.AluminumOre);

        MoveCharacterTo(world, 1, 6f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: "steel-plate"));
        world.Step(RealtimeStep);

        // Direct user request ("занимал 2 секунды... зелёная зарисовка") - nothing is consumed or
        // produced yet, the timer is just armed and ticking (CharacterState.ProductionActionRemaining
        // is what the panel's own progress bar reads).
        var midway = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (midway.ProductionActionRemaining <= 0 || midway.Inventory!.MainSlots.Count(s => s == ItemType.SteelPlate) != 0)
            return false;

        StepPastProductionTimer(world);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.IronOre) == 0
            && inventory.MainSlots.Count(s => s == ItemType.AluminumOre) == 0
            && inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 1;
    }

    // Atomic - either every ingredient is there and gets consumed, or nothing is touched at all.
    private static bool World_Fabricator_RefusesWithoutEnoughIngredients()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.IronOre); // recipe needs 2

        MoveCharacterTo(world, 1, 6f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: "steel-plate"));
        StepPastProductionTimer(world);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.IronOre) == 1
            && inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 0;
    }

    // Same trust-boundary re-check every other id-based interaction gets (World.ClickInteract.cs's
    // own doc comment) - having the id and the materials isn't enough without also being near a real
    // Ship.DecorativeDevices Fabricator.
    private static bool World_Fabricator_RefusesWhenFarFromDevice()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.AluminumOre);

        MoveCharacterTo(world, 1, 1f, 1f); // far from the fabricator at (6,6)
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: "steel-plate"));
        StepPastProductionTimer(world);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 0;
    }

    // A recipe needing a refined material as its own ingredient (titanium plate needs a steel
    // plate) works the same way as any raw-ore recipe - FabricatorCatalog.cs makes no distinction.
    private static bool World_Fabricator_CraftsTitaniumPlate_FromOreAndARefinedIngredient()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.TitaniumOre);
        world.DebugGiveItem(1, ItemType.TitaniumOre);
        world.DebugGiveItem(1, ItemType.SteelPlate);

        MoveCharacterTo(world, 1, 6f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: "titanium-plate"));
        StepPastProductionTimer(world);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.TitaniumOre) == 0
            && inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 0
            && inventory.MainSlots.Count(s => s == ItemType.TitaniumPlate) == 1;
    }

    // Direct user request ("процесс сборки... кнопочку разобрать снизу получаются его содержимое как
    // если бы его скрафтили наоборот") - deconstructing a Steel Plate at the Деконструктор yields
    // exactly the ingredients FabricatorCatalog's own "steel-plate" recipe consumes.
    private static bool World_Deconstructor_ReversesSteelPlateIntoItsOwnIngredients()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.SteelPlate);

        MoveCharacterTo(world, 1, 2f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, DeconstructItemType: ItemType.SteelPlate));
        world.Step(RealtimeStep);

        var midway = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (midway.ProductionActionRemaining <= 0 || !midway.ProductionIsDeconstruct)
            return false;

        StepPastProductionTimer(world);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 0
            && inventory.MainSlots.Count(s => s == ItemType.IronOre) == 2
            && inventory.MainSlots.Count(s => s == ItemType.AluminumOre) == 1;
    }

    // A raw ore has no recipe of its own (nothing crafts FROM it via any FabricatorRecipe's own
    // Output), so it simply can't be deconstructed - FabricatorCatalog.FindByOutput finds nothing.
    private static bool World_Deconstructor_RefusesARawOreWithNoRecipe()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.IronOre);

        MoveCharacterTo(world, 1, 2f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, DeconstructItemType: ItemType.IronOre));
        StepPastProductionTimer(world);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.IronOre) == 1;
    }

    // "ОТМЕНА" - nothing was ever consumed to begin with (TryCraftAtFabricator/TryDeconstructAt only
    // arm the timer), so cancelling mid-way must leave the ingredients untouched and stop the timer.
    private static bool World_Production_CancelPressed_StopsTimerWithoutConsumingAnything()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.AluminumOre);

        MoveCharacterTo(world, 1, 6f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: "steel-plate"));
        world.Step(RealtimeStep);
        world.ApplyCommand(1, new ClientCommand(1, ProductionCancelPressed: true));
        StepPastProductionTimer(world);

        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return after.ProductionActionRemaining == 0
            && after.Inventory!.MainSlots.Count(s => s == ItemType.IronOre) == 2
            && after.Inventory!.MainSlots.Count(s => s == ItemType.AluminumOre) == 1
            && after.Inventory!.MainSlots.Count(s => s == ItemType.SteelPlate) == 0;
    }
}
