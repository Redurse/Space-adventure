namespace Anabiosis.Shared.Model;

// One ingredient line of a FabricatorRecipe - an ItemType plus how many separate Inventory slots of
// it a craft consumes. Inventory has no stack-count concept (Inventory.cs's own doc comment - one
// ItemType per slot), so "2 iron ore" genuinely means 2 occupied main slots, not a quantity field on
// one slot.
public sealed record FabricatorIngredient(ItemType Item, int Count);

// A single Фабрикатор (Fabricator) recipe - direct user request ("добавим множество предметов
// материалов", "сделай меню фабрикатора как в баротравме"). Category is purely a UI grouping label
// (FabricatorPanel.cs's own left-hand list) - every recipe today is "Материалы", but the field
// exists so a future recipe (weapons, medical, ...) doesn't need a format change.
public sealed record FabricatorRecipe(string Id, string Category, ItemType Output, int OutputCount, IReadOnlyList<FabricatorIngredient> Ingredients);

public static class FabricatorCatalog
{
    private const string MaterialsCategory = "Материалы";

    public static readonly IReadOnlyList<FabricatorRecipe> Recipes = new[]
    {
        new FabricatorRecipe("steel-plate", MaterialsCategory, ItemType.SteelPlate, OutputCount: 1, new[]
        {
            new FabricatorIngredient(ItemType.IronOre, 2),
            new FabricatorIngredient(ItemType.AluminumOre, 1),
        }),
        new FabricatorRecipe("titanium-plate", MaterialsCategory, ItemType.TitaniumPlate, OutputCount: 1, new[]
        {
            new FabricatorIngredient(ItemType.TitaniumOre, 2),
            new FabricatorIngredient(ItemType.SteelPlate, 1),
        }),
        new FabricatorRecipe("copper-cable", MaterialsCategory, ItemType.CopperCable, OutputCount: 1, new[]
        {
            new FabricatorIngredient(ItemType.CopperOre, 2),
        }),
        new FabricatorRecipe("plastalloy", MaterialsCategory, ItemType.Plastalloy, OutputCount: 1, new[]
        {
            new FabricatorIngredient(ItemType.PlastalloyOre, 2),
            new FabricatorIngredient(ItemType.AluminumOre, 1),
        }),
        new FabricatorRecipe("plastic", MaterialsCategory, ItemType.Plastic, OutputCount: 1, new[]
        {
            new FabricatorIngredient(ItemType.Carbon, 2),
            new FabricatorIngredient(ItemType.Silicon, 1),
        }),
    };

    public static FabricatorRecipe? Find(string id) => Recipes.FirstOrDefault(r => r.Id == id);

    // Direct user request ("Деконструктор... получается его содержимое как если бы его скрафтили
    // наоборот") - the Деконструктор has no recipes of its own; it just runs whichever
    // FabricatorRecipe produces the item the player is deconstructing, in reverse (output consumed,
    // ingredients returned). Every recipe's own Output is unique across the catalog, so this is
    // always at most one match.
    public static FabricatorRecipe? FindByOutput(ItemType output) => Recipes.FirstOrDefault(r => r.Output == output);

    // Direct user request ("процесс сборки и разборки всех предметов на обоих устройствах занимал 2
    // секунды") - same duration for a Fabricator craft and a Deconstructor reverse-craft, both
    // driven by Character.ProductionActionRemaining (World.Fabricator.cs's own StepProduction).
    public const float ProductionDurationSeconds = 2f;
}
