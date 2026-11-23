using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The fabricator's recipes for the basic tools, tanks, radio and sidearms (FabricatorCatalog.CraftedItemRecipes) - the ones Barotrauma's own item files give,
// as far as this game has the ingredients.
internal static partial class TestRunner
{
    private static bool FabricatorCatalog_CraftedItemRecipes_AreWellFormedAndUnique()
    {
        var crafted = FabricatorCatalog.Recipes
            .Where(r => r.Category is FabricatorCatalog.ToolsCategory or FabricatorCatalog.WeaponsCategory).ToList();
        if (crafted.Count != 10)
            return false;
        var allOutputs = FabricatorCatalog.Recipes.Select(r => r.Output).ToList();
        if (allOutputs.Distinct().Count() != allOutputs.Count)
            return false; // one recipe per item: the Деконструктор finds the recipe by what comes out
        if (FabricatorCatalog.Recipes.Select(r => r.Id).Distinct().Count() != FabricatorCatalog.Recipes.Count)
            return false;
        // The panel lists one page of recipes under the filter boxes: no page may be longer than fits.
        foreach (var category in FabricatorCatalog.Categories)
            if (FabricatorCatalog.Recipes.Count(r => r.Category == category) is 0 or > 13)
                return false;
        // Real ingredients only (materials this game has), and only a handful of lines so the input row can show them.
        return crafted.All(r => r.Ingredients.Count is >= 1 and <= 2 && r.Ingredients.All(i => i.Count is >= 1 and <= 3)
                                && r.Ingredients.All(i => ItemDefinitions.IsRawOre(i.Item) == false));
    }

    private static InventoryState CraftAtFabricator(string recipeId, params ItemType[] materials)
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        foreach (var material in materials)
            world.DebugGiveItem(1, material);
        MoveCharacterTo(world, 1, 6f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: recipeId));
        StepPastProductionTimer(world);
        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
    }

    // Wrench: two iron (Barotrauma's own recipe).
    private static bool World_Fabricator_CraftsAWrenchFromTwoIron()
    {
        var inventory = CraftAtFabricator("wrench", ItemType.IronOre, ItemType.IronOre);
        return inventory.MainSlots.Count(s => s == ItemType.Wrench) == 1 && inventory.MainSlots.Count(s => s == ItemType.IronOre) == 0;
    }

    // Revolver: two steel and a plastic - two kinds of ingredient, one of them twice.
    private static bool World_Fabricator_CraftsARevolverFromSteelAndPlastic()
    {
        var inventory = CraftAtFabricator("revolver", ItemType.SteelPlate, ItemType.SteelPlate, ItemType.Plastic);
        return inventory.MainSlots.Count(s => s == ItemType.Revolver) == 1
            && inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 0 && inventory.MainSlots.Count(s => s == ItemType.Plastic) == 0;
    }

    // One steel is not enough for a welding tool (it needs two plus two plastic): nothing is made and nothing is spent.
    private static bool World_Fabricator_WeldingToolNeedsItsFullIngredients()
    {
        var inventory = CraftAtFabricator("welding-tool", ItemType.SteelPlate, ItemType.Plastic, ItemType.Plastic);
        return inventory.MainSlots.Count(s => s == ItemType.WeldingTool) == 0 && inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 1;
    }

    // A crafted tool comes apart into what made it, in the Деконструктор.
    private static bool World_Deconstructor_TakesAWrenchBackIntoTwoIron()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.Wrench);
        MoveCharacterTo(world, 1, 2f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, DeconstructItemType: ItemType.Wrench));
        StepPastProductionTimer(world);
        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.Wrench) == 0 && inventory.MainSlots.Count(s => s == ItemType.IronOre) == 2;
    }
}
