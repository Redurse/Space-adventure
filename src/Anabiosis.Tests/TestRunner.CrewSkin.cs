using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    private static bool CrewSkin_UniformFor_EveryRoleHasItsOwnColourAndBotsKeepTheirBlueWithoutOne()
    {
        var roleColors = Enum.GetValues<CrewRole>().Select(r => CrewSkin.UniformFor(r, false).PackedValue).ToList();
        if (roleColors.Distinct().Count() != roleColors.Count)
            return false; // two roles look the same

        var defaultHuman = CrewSkin.UniformFor(null, false);
        var defaultBot = CrewSkin.UniformFor(null, true);
        return defaultHuman != defaultBot
            && !roleColors.Contains(defaultHuman.PackedValue)
            && CrewSkin.UniformFor(CrewRole.Captain, true) == CrewSkin.UniformFor(CrewRole.Captain, false)
            && CrewSkin.AccentFor(true) != CrewSkin.AccentFor(false);
    }
}

internal static partial class TestRunner
{
    private static bool ShipRenderer_NpcLook_EveryKindHasItsOwnUniformAndGuardsWearArmour()
    {
        var kinds = Enum.GetValues<NpcKind>();
        var uniforms = kinds.Select(k => ShipRenderer.NpcLook(k).Uniform.PackedValue).ToList();
        return uniforms.Distinct().Count() == uniforms.Count
            && ShipRenderer.NpcLook(NpcKind.Security).Outfit == Outfit.Guard
            && kinds.Where(k => k != NpcKind.Security).All(k => ShipRenderer.NpcLook(k).Outfit == Outfit.Crew);
    }
}

internal static partial class TestRunner
{
    private static bool ItemIcons_WorldPresentation_EveryItemHasANameAndAColour()
    {
        foreach (var item in Enum.GetValues<ItemType>())
        {
            var name = ItemIcons.WorldLabel(item);
            // An English enum name leaking into the scene is a missing translation.
            if (name.Length == 0 || name == item.ToString() || char.IsLower(name[0]))
                return false;
            if (ItemIcons.CategoryColor(item).A == 0)
                return false;
        }
        return true;
    }
}
