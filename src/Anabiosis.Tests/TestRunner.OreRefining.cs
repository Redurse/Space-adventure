using Anabiosis.Client.Rendering;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Raw ore from the asteroids, its processing into the materials at the fabricator, the ore pictures, and the cheat panel's
// "explore the whole system map" button.
internal static partial class TestRunner
{
    // Every raw ore has a one-for-one "Переработка" recipe, and what comes out is a material the game already had.
    private static bool FabricatorCatalog_EveryRawOreIsRefinedIntoAnExistingMaterial()
    {
        foreach (var (raw, refined) in ItemDefinitions.RefiningPairs)
        {
            var recipe = FabricatorCatalog.Recipes.SingleOrDefault(r => r.Output == refined && r.Category == FabricatorCatalog.RefiningCategory);
            if (recipe is null || recipe.Ingredients.Count != 1 || recipe.Ingredients[0].Item != raw || recipe.Ingredients[0].Count != 1)
                return false;
            if (!ItemDefinitions.IsRawOre(raw) || ItemDefinitions.IsRawOre(refined) || ItemDefinitions.RawOf(refined) != raw)
                return false;
            if (TradeCatalog.Find(raw) is null)
                return false;
        }
        // The old recipes still exist in their own category, and refining cannot be undone in the Деконструктор.
        return FabricatorCatalog.Find("steel-plate")!.Category == "Материалы"
            && FabricatorCatalog.FindByOutput(ItemType.IronOre) is null
            && FabricatorCatalog.FindByOutput(ItemType.SteelPlate) is not null;
    }

    private static bool World_Fabricator_RefinesRawOreIntoTheMaterial()
    {
        var world = new World(ShipKind.Custom, BuildFabricatorShipDefinition());
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, ItemType.RawCopper);

        MoveCharacterTo(world, 1, 6f, 6f);
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: "refine-rawcopper"));
        StepPastProductionTimer(world);

        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.RawCopper) == 0
            && inventory.MainSlots.Count(s => s == ItemType.CopperOre) == 1;
    }

    // What the asteroids give is raw ore only - never the materials themselves.
    private static bool AsteroidField_DepositsYieldRawOreOnly()
    {
        var seen = new HashSet<ItemType>();
        for (var n = 1; n <= 40; n++)
            foreach (var deposit in AsteroidField.CreateForSystem($"sys-{n:000}").OreDeposits)
            {
                if (ItemDefinitions.RefinedFrom(deposit.OreType) is null)
                    return false;
                seen.Add(deposit.OreType);
            }
        foreach (var deposit in AsteroidField.CreateDefault().OreDeposits)
            if (ItemDefinitions.RefinedFrom(deposit.OreType) is null)
                return false;
        return seen.Count >= 8; // a good spread of the eleven, not one or two kinds
    }

    // Every ore paints a picture (not empty, not the same as another ore) at every stage and as an icon.
    private static bool OreArt_EveryRawOrePaintsADistinctPicture()
    {
        var signatures = new HashSet<long>();
        foreach (var (raw, _) in ItemDefinitions.RefiningPairs)
        {
            if (!OreArt.Has(raw))
                return false;
            for (var stage = 0; stage < 3; stage++)
            {
                var pixels = OreArt.Render(raw, 48, stage, variant: 0, rubble: true, zoom: 1f);
                if (pixels.Count(p => p.A > 200) < 48 * 48 / 14)
                    return false;
                if (stage == 2)
                {
                    long sum = 0;
                    foreach (var p in pixels)
                        sum = unchecked(sum * 31 + p.R * 3 + p.G * 5 + p.B * 7 + p.A);
                    if (!signatures.Add(sum))
                        return false;
                }
            }
            if (OreArt.Render(raw, 32, 2, 0, rubble: false, zoom: 1.22f).Count(p => p.A > 200) < 32 * 32 / 5)
                return false;
        }
        return true;
    }

    // Cheat panel: one click clears the fog over the whole system and finds every hidden place in it.
    private static bool World_DebugRevealSystemMap_ClearsTheFogAndFindsEveryPlace()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var system = world.GalaxyMap.GetSystem(world.CreateSnapshot().CurrentSystemId);
        var size = system.Field.Width;
        if (world.IsFogRevealedAt(new Vec2(size - 1f, size - 1f)) && system.Pois.Count == 0)
            return false; // nothing to prove, or the setup is not what the test assumes

        world.ApplyCommand(1, new ClientCommand(1, DebugRevealSystemMap: true));
        world.Step(RealtimeStep);

        var corners = new[] { new Vec2(1f, 1f), new Vec2(size - 1f, 1f), new Vec2(1f, size - 1f), new Vec2(size - 1f, size - 1f), new Vec2(size / 2f, size / 2f) };
        return corners.All(world.IsFogRevealedAt)
            && system.Pois.All(poi => world.IdentifiedPoiIds.Contains(poi.Id));
    }
}
