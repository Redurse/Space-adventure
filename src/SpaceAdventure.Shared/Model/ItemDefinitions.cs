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
        ItemType.Axe => 1,
        ItemType.FuelRod => 1,
        ItemType.MedKit => 1,
        ItemType.WireSpool => 1,
        ItemType.Mineral => 1,
        ItemType.OxygenTank => 1,
        ItemType.WeldingTank => 1,
        _ when ComponentDefinitions.ComponentKindFor(type) is not null => 1, // small electronics box
        _ => 0, // AmmoCrate, Spacesuit
    };

    public static bool IsHoldable(ItemType type) => HandsRequired(type) > 0;

    // The 14 purchasable component items delegate to ComponentDefinitions - the ComponentKind
    // they install as (World.ComponentMounts.cs, M23) already owns the one true name/label for
    // each kind, so the item shouldn't keep its own separate copy.
    public static string DisplayName(ItemType type) =>
        ComponentDefinitions.ComponentKindFor(type) is { } kind ? ComponentDefinitions.DisplayName(kind) : DisplayNameForBaseItem(type);

    private static string DisplayNameForBaseItem(ItemType type) => type switch
    {
        ItemType.AmmoCrate => "ящик патронов",
        ItemType.Spacesuit => "скафандр",
        ItemType.Wrench => "гаечный ключ",
        ItemType.Screwdriver => "отвёртка",
        ItemType.WeldingTool => "сварочный аппарат",
        ItemType.Cutter => "резак",
        ItemType.Knife => "нож",
        ItemType.Axe => "топор Гоши",
        ItemType.BeltBag => "поясная сумка",
        ItemType.IdCard => "карточка экипажа",
        ItemType.Rifle => "автомат",
        ItemType.LaserRifle => "лазерная винтовка",
        ItemType.FuelRod => "ядерный стержень",
        ItemType.MedKit => "аптечка",
        ItemType.WireSpool => "катушка провода",
        ItemType.Mineral => "минеральная руда",
        ItemType.OxygenTank => "кислородный баллон",
        ItemType.WeldingTank => "сварочный баллон",
        _ => type.ToString(),
    };

    // Short 1-2 letter code for tight HUD slots/markers.
    public static string ShortLabel(ItemType type) =>
        ComponentDefinitions.ComponentKindFor(type) is { } kind ? ComponentDefinitions.ShortLabel(kind) : ShortLabelForBaseItem(type);

    private static string ShortLabelForBaseItem(ItemType type) => type switch
    {
        ItemType.AmmoCrate => "П",
        ItemType.Spacesuit => "С",
        ItemType.Wrench => "К",
        ItemType.Screwdriver => "О",
        ItemType.WeldingTool => "Св",
        ItemType.Cutter => "Рз",
        ItemType.Knife => "Нж",
        ItemType.Axe => "Тп",
        ItemType.BeltBag => "Сум",
        ItemType.IdCard => "ID",
        ItemType.Rifle => "Ав",
        ItemType.LaserRifle => "ЛВ",
        ItemType.FuelRod => "Яд",
        ItemType.MedKit => "Ап",
        ItemType.WireSpool => "Пр",
        ItemType.Mineral => "Ру",
        ItemType.OxygenTank => "О2",
        ItemType.WeldingTank => "Сб",
        _ => "?",
    };
}
