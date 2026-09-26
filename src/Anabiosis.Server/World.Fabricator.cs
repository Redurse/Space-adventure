using System.Linq;
using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Direct user request ("сделай меню фабрикатора как в баротравме" / "сделай Деконструктор" /
// "процесс сборки и разборки... занимал 2 секунды") - unlike Terminal/SuitLocker/Turret this doesn't
// need its own per-instance id or state dictionary: any Ship.DecorativeDevices Fabricator/
// Deconstructor in the character's own room, within reach, is enough to start a job, and the actual
// timer/ingredients/output live on the CHARACTER (Character.ProductionActionRemaining/
// ProductionRecipeId/ProductionIsDeconstruct) - the same "just a personal timer" shape
// SuitActionRemaining already uses for the suit-locker action, not a per-device queue. Same trust
// model as World.ClickInteract.cs's own id-based interactions - the client only ever shows the
// panel/enables the button when it already believes the conditions hold, but every check here is
// re-verified from scratch server-side regardless.
public sealed partial class World
{
    // Test-only precondition setter, same convention as DebugAddCredits (World.Trade.cs) - setting
    // up a multi-ingredient craft (e.g. 2 iron ore + 1 aluminum ore, both raw ItemTypes with no
    // dedicated "give me one" gameplay path the way DebugAddCredits' own doc comment describes for
    // credits) by actually mining/trading for every ingredient first would make every crafting test
    // its own small mining expedition, for no better assurance than this - the mining/pickup path
    // itself is already covered end to end by TestRunner.Mining.cs.
    public void DebugGiveItem(int playerId, ItemType type) => _characters[playerId].Inventory.TryAdd(type);

    private bool NearDeviceKind(Character character, CustomDeviceKind kind) =>
        Ship.DecorativeDevices.Any(d => d.Kind == kind && d.RoomId == character.RoomId &&
            (d.Position - character.Position).Length() < InteractionRadius);

    private static bool HasIngredients(Inventory inventory, FabricatorRecipe recipe) =>
        recipe.Ingredients.All(ing => Enumerable.Range(0, Inventory.MainSlotCount).Count(i => inventory.ItemAt(i) == ing.Item) >= ing.Count);

    // Direct user request ("занимал 2 секунды... зелёная зарисовка") - starts the timer only; the
    // actual ingredient removal/output happens in StepProduction once it reaches zero, so walking
    // away or losing an ingredient mid-timer never leaves a half-consumed craft (nothing is touched
    // until the moment it completes).
    private void TryCraftAtFabricator(Character character, string recipeId)
    {
        if (character.IsOutside || character.OnStation || character.ProductionActionRemaining > 0)
            return;
        if (FabricatorCatalog.Find(recipeId) is not { } recipe)
            return;
        if (!NearDeviceKind(character, CustomDeviceKind.Fabricator))
            return;
        if (!HasIngredients(character.Inventory, recipe))
            return;

        character.ProductionActionRemaining = FabricatorCatalog.ProductionDurationSeconds;
        character.ProductionRecipeId = recipe.Id;
        character.ProductionIsDeconstruct = false;
    }

    // Direct user request ("Деконструктор... получается его содержимое как если бы его скрафтили
    // наоборот") - runs a FabricatorRecipe backwards: `itemType` has to be some recipe's own Output,
    // and the character has to actually be holding one.
    private void TryDeconstructAt(Character character, ItemType itemType)
    {
        if (character.IsOutside || character.OnStation || character.ProductionActionRemaining > 0)
            return;
        if (FabricatorCatalog.FindByOutput(itemType) is not { } recipe)
            return;
        if (!NearDeviceKind(character, CustomDeviceKind.Deconstructor))
            return;
        if (!character.Inventory.Has(itemType))
            return;

        character.ProductionActionRemaining = FabricatorCatalog.ProductionDurationSeconds;
        character.ProductionRecipeId = recipe.Id;
        character.ProductionIsDeconstruct = true;
    }

    // The Cancel button on either panel - nothing was ever consumed (TryCraftAtFabricator/
    // TryDeconstructAt only ever arm the timer), so this is a bare reset with no side effects.
    private void TryCancelProduction(Character character)
    {
        character.ProductionActionRemaining = 0f;
        character.ProductionRecipeId = null;
        character.ProductionIsDeconstruct = false;
    }

    // Called once per character every World.Step (World.Movement.cs, alongside
    // SuitActionRemaining's own countdown) - movement is deliberately NOT locked while this runs
    // (direct user request never asked for that, and Barotrauma's own fabricator doesn't root you in
    // place either), so a character can start a craft and walk off; it still finishes on schedule.
    private void StepProduction(Character character, double deltaSeconds)
    {
        if (character.ProductionActionRemaining <= 0)
            return;

        character.ProductionActionRemaining = System.Math.Max(0f, character.ProductionActionRemaining - (float)deltaSeconds);
        if (character.ProductionActionRemaining > 0)
            return;

        var recipe = FabricatorCatalog.Find(character.ProductionRecipeId ?? "");
        character.ProductionRecipeId = null;
        var isDeconstruct = character.ProductionIsDeconstruct;
        character.ProductionIsDeconstruct = false;
        if (recipe is null)
            return;

        if (isDeconstruct)
        {
            // Silently no-ops if the item got dropped/sold mid-timer - never crashes, just produces
            // nothing, the same "re-check everything at the moment it actually matters" idiom the
            // craft direction below already follows.
            if (!character.Inventory.TryRemove(recipe.Output))
                return;
            foreach (var ingredient in recipe.Ingredients)
                for (var i = 0; i < ingredient.Count; i++)
                    character.Inventory.TryAdd(ingredient.Item);
        }
        else
        {
            if (!HasIngredients(character.Inventory, recipe))
                return;
            foreach (var ingredient in recipe.Ingredients)
                for (var i = 0; i < ingredient.Count; i++)
                    character.Inventory.TryRemove(ingredient.Item);
            for (var i = 0; i < recipe.OutputCount; i++)
                character.Inventory.TryAdd(recipe.Output);
        }
    }
}
