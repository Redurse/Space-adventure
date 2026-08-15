namespace SpaceAdventure.Shared.Model;

// Central catalog of item metadata shared by server (hand-capacity checks) and client (HUD
// labels). Hand counts per the design brief: welding tool and firearms need both hands; wrench,
// screwdriver, cutter and knife are one-handed; ammo crates and suits are never held in a hand.
public static class ItemDefinitions
{
    public static int HandsRequired(ItemType type) => type switch
    {
        ItemType.WeldingTool => 2,
        ItemType.Rifle => 2,
        ItemType.LaserRifle => 2,
        ItemType.Wrench => 1,
        ItemType.Screwdriver => 1,
        ItemType.Cutter => 1,
        ItemType.Knife => 1,
        ItemType.FuelRod => 1,
        _ => 0, // AmmoCrate, Spacesuit
    };

    public static bool IsHoldable(ItemType type) => HandsRequired(type) > 0;

    public static string DisplayName(ItemType type) => type switch
    {
        ItemType.AmmoCrate => "ящик патронов",
        ItemType.Spacesuit => "скафандр",
        ItemType.Wrench => "гаечный ключ",
        ItemType.Screwdriver => "отвёртка",
        ItemType.WeldingTool => "сварочный аппарат",
        ItemType.Cutter => "резак",
        ItemType.Knife => "нож",
        ItemType.Rifle => "автомат",
        ItemType.LaserRifle => "лазерная винтовка",
        ItemType.FuelRod => "ядерный стержень",
        _ => type.ToString(),
    };

    // Short 1-2 letter code for tight HUD slots/markers.
    public static string ShortLabel(ItemType type) => type switch
    {
        ItemType.AmmoCrate => "П",
        ItemType.Spacesuit => "С",
        ItemType.Wrench => "К",
        ItemType.Screwdriver => "О",
        ItemType.WeldingTool => "Св",
        ItemType.Cutter => "Рз",
        ItemType.Knife => "Нж",
        ItemType.Rifle => "Ав",
        ItemType.LaserRifle => "ЛВ",
        ItemType.FuelRod => "Яд",
        _ => "?",
    };
}
