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

    // The second recipe page: raw ore from the asteroids goes in, the material the game already had comes out (one for one).
    public const string RefiningCategory = "Переработка";

    // The things a crew makes for itself (direct user request, "крафты базовых предметов как в баротравме с теми же рецептами"). Each recipe is the one
    // Barotrauma's own item files give for the same item - which of them are here is decided by what this game has: an ingredient it has no such thing as
    // (tin, lead, rubber, lithium, an FPGA circuit ...) leaves the item out. Steel is our SteelPlate, iron our IronOre, aluminium our AluminumOre.
    public const string ToolsCategory = "Инструменты";
    public const string WeaponsCategory = "Оружие";

    // Wiring parts (W8 of wiring-plan.md): the circuit-board logic and maths. Barotrauma's own recipes want an FPGA circuit, which this game does
    // not have, so silicon and copper stand in (the radio's recipe does the same). The sensors and actuators are bought at a trader instead.
    public const string ElectronicsCategory = "Электроника";

    public static readonly IReadOnlyList<string> Categories = new[] { MaterialsCategory, RefiningCategory, ToolsCategory, WeaponsCategory, ElectronicsCategory };

    private static FabricatorRecipe Craft(string id, string category, ItemType output, params (ItemType Item, int Count)[] ingredients) =>
        new(id, category, output, OutputCount: 1, ingredients.Select(i => new FabricatorIngredient(i.Item, i.Count)).ToArray());

    private static readonly FabricatorRecipe[] CraftedItemRecipes =
    {
        // wrench, screwdriver, diving knife: 2 iron each
        Craft("wrench", ToolsCategory, ItemType.Wrench, (ItemType.IronOre, 2)),
        Craft("screwdriver", ToolsCategory, ItemType.Screwdriver, (ItemType.IronOre, 2)),
        Craft("knife", ToolsCategory, ItemType.Knife, (ItemType.IronOre, 2)),
        // welding tool and plasma cutter (our Cutter): 2 steel + 2 plastic
        Craft("welding-tool", ToolsCategory, ItemType.WeldingTool, (ItemType.SteelPlate, 2), (ItemType.Plastic, 2)),
        Craft("cutter", ToolsCategory, ItemType.Cutter, (ItemType.SteelPlate, 2), (ItemType.Plastic, 2)),
        // oxygen tank: 2 aluminium. Welding fuel tank is 2 aluminium + 1 ethanol there; this game has no ethanol, so carbon (the thing ethanol is made of) stands in.
        Craft("oxygen-tank", ToolsCategory, ItemType.OxygenTank, (ItemType.AluminumOre, 2)),
        Craft("welding-tank", ToolsCategory, ItemType.WeldingTank, (ItemType.AluminumOre, 2), (ItemType.Carbon, 1)),
        // the headset: 1 silicon + 1 copper
        Craft("radio", ToolsCategory, ItemType.Radio, (ItemType.Silicon, 1), (ItemType.CopperOre, 1)),
        // revolver: 2 steel + 1 plastic; machine pistol: 2 steel + 2 plastic
        Craft("revolver", WeaponsCategory, ItemType.Revolver, (ItemType.SteelPlate, 2), (ItemType.Plastic, 1)),
        Craft("machine-pistol", WeaponsCategory, ItemType.MachinePistol, (ItemType.SteelPlate, 2), (ItemType.Plastic, 2)),

        // the simple parts: 1 silicon + 1 copper
        Craft("part-button", ElectronicsCategory, ItemType.Button, (ItemType.Silicon, 1), (ItemType.CopperOre, 1)),
        Craft("part-gate-and", ElectronicsCategory, ItemType.GateAnd, (ItemType.Silicon, 1), (ItemType.CopperOre, 1)),
        Craft("part-gate-or", ElectronicsCategory, ItemType.GateOr, (ItemType.Silicon, 1), (ItemType.CopperOre, 1)),
        // (NOT and XOR are bought at a trader: the page holds 13 recipes, and the two text parts take their places)
        Craft("part-terminal", ElectronicsCategory, ItemType.TerminalPart, (ItemType.Silicon, 1), (ItemType.CopperOre, 1)),
        Craft("part-signal-check", ElectronicsCategory, ItemType.SignalCheck, (ItemType.Silicon, 1), (ItemType.CopperOre, 1)),
        // the ones that remember or calculate: 2 silicon + 1 copper
        Craft("part-memory", ElectronicsCategory, ItemType.Memory, (ItemType.Silicon, 2), (ItemType.CopperOre, 1)),
        Craft("part-delay", ElectronicsCategory, ItemType.Delay, (ItemType.Silicon, 2), (ItemType.CopperOre, 1)),
        Craft("part-oscillator", ElectronicsCategory, ItemType.Oscillator, (ItemType.Silicon, 2), (ItemType.CopperOre, 1)),
        Craft("part-comparator", ElectronicsCategory, ItemType.Comparator, (ItemType.Silicon, 2), (ItemType.CopperOre, 1)),
        Craft("part-math", ElectronicsCategory, ItemType.MathPart, (ItemType.Silicon, 2), (ItemType.CopperOre, 1)),
        Craft("part-function", ElectronicsCategory, ItemType.FunctionPart, (ItemType.Silicon, 2), (ItemType.CopperOre, 1)),
        Craft("part-regex", ElectronicsCategory, ItemType.RegexPart, (ItemType.Silicon, 2), (ItemType.CopperOre, 1)),
        Craft("part-wireless", ElectronicsCategory, ItemType.WirelessPart, (ItemType.Silicon, 2), (ItemType.CopperOre, 2)),
    };

    private static IEnumerable<FabricatorRecipe> RefiningRecipes() =>
        ItemDefinitions.RefiningPairs.Select(pair => new FabricatorRecipe(
            $"refine-{pair.Raw.ToString().ToLowerInvariant()}", RefiningCategory, pair.Refined, OutputCount: 1,
            new[] { new FabricatorIngredient(pair.Raw, 1) }));

    private static readonly FabricatorRecipe[] MaterialRecipes = new[]
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

    // Declared after MaterialRecipes: static fields initialise in the order they are written.
    public static readonly IReadOnlyList<FabricatorRecipe> Recipes = MaterialRecipes.Concat(CraftedItemRecipes).Concat(RefiningRecipes()).ToArray();

    public static FabricatorRecipe? Find(string id) => Recipes.FirstOrDefault(r => r.Id == id);

    // Direct user request ("Деконструктор... получается его содержимое как если бы его скрафтили
    // наоборот") - the Деконструктор has no recipes of its own; it just runs whichever
    // FabricatorRecipe produces the item the player is deconstructing, in reverse (output consumed,
    // ingredients returned). Every recipe's own Output is unique across the catalog, so this is
    // always at most one match.
    // Processing ore is a one-way trip: the Деконструктор does not turn a material back into raw ore.
    public static FabricatorRecipe? FindByOutput(ItemType output) =>
        Recipes.FirstOrDefault(r => r.Output == output && r.Category != RefiningCategory);

    // Direct user request ("процесс сборки и разборки всех предметов на обоих устройствах занимал 2
    // секунды") - same duration for a Fabricator craft and a Deconstructor reverse-craft, both
    // driven by Character.ProductionActionRemaining (World.Fabricator.cs's own StepProduction).
    public const float ProductionDurationSeconds = 2f;
}
