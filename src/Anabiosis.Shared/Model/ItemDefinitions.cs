namespace Anabiosis.Shared.Model;

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
        ItemType.Cutter => 2,
        ItemType.Knife => 1,
        ItemType.Axe => 1,
        ItemType.GoshaScrewdriver => 1,
        ItemType.FuelRod => 1,
        ItemType.MedKit => 1,
        ItemType.Bandage or ItemType.Tourniquet or ItemType.Antiseptic or ItemType.Splint or ItemType.PlasterCast or ItemType.Painkiller
            or ItemType.Antibiotic or ItemType.BurnGel or ItemType.Adrenaline or ItemType.Defibrillator or ItemType.SurgeryKit => 1,
        ItemType.WireSpool => 1,
        ItemType.Mineral => 1,
        ItemType.OxygenTank => 1,
        ItemType.WeldingTank => 1,
        ItemType.IronOre or ItemType.NickelOre or ItemType.ZincOre or ItemType.CopperOre or ItemType.TitaniumOre
            or ItemType.UraniumOre or ItemType.Silicon or ItemType.Carbon or ItemType.AluminumOre or ItemType.PlastalloyOre => 1,
        ItemType.SteelPlate or ItemType.TitaniumPlate or ItemType.CopperCable or ItemType.Plastic or ItemType.Plastalloy => 1,
        ItemType.Hyperium => 1,
        _ when RefinedFrom(type) is not null => 1, // raw ore
        ItemType.Magazine => 1,
        ItemType.Revolver => 1,
        ItemType.BodyArmor or ItemType.BallisticHelmet => 1,
        ItemType.RevolverRounds => 1,
        _ when GunDefinitions.Of(type) is { } gun => gun.Hands,
        _ when GunDefinitions.IsAmmo(type) => 1,
        _ when ComponentDefinitions.ComponentKindFor(type) is not null => 1, // small electronics box
        _ => 0, // AmmoCrate, Spacesuit
    };

    public static bool IsHoldable(ItemType type) => HandsRequired(type) > 0;

    // Raw, unrefined mining output only - not the FabricatorCatalog.cs refined materials crafted
    // FROM these (SteelPlate/TitaniumPlate/CopperCable/Plastic/Plastalloy), which never come out of
    // an OreDeposit directly. World.Trade.cs's MiningStationOreSellBonus checks this - a mining
    // outpost pays extra for raw ore, not for goods a Fabricator already added value to.
    public static bool IsRawOre(ItemType type) => type == ItemType.Mineral || RefinedFrom(type) is not null;

    // What an asteroid yields and what the fabricator makes of it ("Переработка"): the raw ore on the left, the material that
    // already existed in the game (the one the plates, cable and jump fuel are made from) on the right.
    public static readonly IReadOnlyList<(ItemType Raw, ItemType Refined)> RefiningPairs = new[]
    {
        (ItemType.RawIron, ItemType.IronOre), (ItemType.RawNickel, ItemType.NickelOre), (ItemType.RawZinc, ItemType.ZincOre),
        (ItemType.RawCopper, ItemType.CopperOre), (ItemType.RawTitanium, ItemType.TitaniumOre), (ItemType.RawUranium, ItemType.UraniumOre),
        (ItemType.RawSilicon, ItemType.Silicon), (ItemType.RawCarbon, ItemType.Carbon), (ItemType.RawAluminum, ItemType.AluminumOre),
        (ItemType.RawPlastalloy, ItemType.PlastalloyOre), (ItemType.RawHyperium, ItemType.Hyperium),
    };

    // The material a raw ore is processed into; null for anything that is not a raw ore.
    public static ItemType? RefinedFrom(ItemType raw)
    {
        foreach (var (r, refined) in RefiningPairs)
            if (r == raw)
                return refined;
        return null;
    }

    // The raw ore a material is made from; null for anything that is not one of those materials.
    public static ItemType? RawOf(ItemType refined)
    {
        foreach (var (raw, r) in RefiningPairs)
            if (r == refined)
                return raw;
        return null;
    }

    // The 14 purchasable component items delegate to ComponentDefinitions - the ComponentKind
    // they install as (World.ComponentMounts.cs, M23) already owns the one true name/label for
    // each kind, so the item shouldn't keep its own separate copy.
    public static string DisplayName(ItemType type) =>
        ComponentDefinitions.ComponentKindFor(type) is { } kind ? ComponentDefinitions.DisplayName(kind) : DisplayNameForBaseItem(type);

    private static string DisplayNameForBaseItem(ItemType type) => type switch
    {
        ItemType.Radio => "рация",
        ItemType.AmmoCrate => "ящик патронов",
        ItemType.Spacesuit => "скафандр",
        ItemType.Wrench => "гаечный ключ",
        ItemType.Screwdriver => "отвёртка",
        ItemType.WeldingTool => "сварочный аппарат",
        ItemType.Cutter => "резак",
        ItemType.Knife => "нож",
        ItemType.Axe => "топор поломки",
        ItemType.GoshaScrewdriver => "отвёртка поломки",
        ItemType.BeltBag => "поясная сумка",
        ItemType.IdCard => "карточка экипажа",
        ItemType.Rifle => "автомат",
        ItemType.LaserRifle => "лазерная винтовка",
        ItemType.FuelRod => "ядерный стержень",
        ItemType.MedKit => "аптечка",
        ItemType.Bandage => "бинт",
        ItemType.Tourniquet => "жгут",
        ItemType.Antiseptic => "антисептик",
        ItemType.Splint => "шина",
        ItemType.PlasterCast => "гипс",
        ItemType.Painkiller => "обезболивающее",
        ItemType.Antibiotic => "антибиотик",
        ItemType.BurnGel => "гель от ожогов",
        ItemType.Adrenaline => "адреналин",
        ItemType.Defibrillator => "дефибриллятор",
        ItemType.SurgeryKit => "хирургический набор",
        ItemType.WireSpool => "катушка провода",
        ItemType.Mineral => "минеральная руда",
        ItemType.OxygenTank => "кислородный баллон",
        ItemType.WeldingTank => "сварочный баллон",
        // the materials (what ore is processed into) ...
        ItemType.IronOre => "железо",
        ItemType.NickelOre => "никель",
        ItemType.ZincOre => "цинк",
        ItemType.CopperOre => "медь",
        ItemType.TitaniumOre => "титан",
        ItemType.UraniumOre => "уран",
        ItemType.Silicon => "кремний",
        ItemType.Carbon => "углерод",
        ItemType.AluminumOre => "алюминий",
        ItemType.PlastalloyOre => "пласталевый порошок",
        // ... and the raw ores an asteroid gives
        ItemType.RawIron => "железная руда",
        ItemType.RawNickel => "никелевая руда",
        ItemType.RawZinc => "цинковая руда",
        ItemType.RawCopper => "медная руда",
        ItemType.RawTitanium => "титановая руда",
        ItemType.RawUranium => "урановая руда",
        ItemType.RawSilicon => "кремниевая руда",
        ItemType.RawCarbon => "углеродная руда",
        ItemType.RawAluminum => "алюминиевая руда",
        ItemType.RawPlastalloy => "пласталевая руда",
        ItemType.RawHyperium => "руда гиперия",
        ItemType.SteelPlate => "стальная пластина",
        ItemType.TitaniumPlate => "титановая пластина",
        ItemType.CopperCable => "медный кабель",
        ItemType.Plastic => "пластик",
        ItemType.Plastalloy => "пласталь",
        ItemType.Hyperium => "гиперий",
        ItemType.HullPlating => "обшивка корпуса",
        ItemType.Magazine => "обойма",
        ItemType.Revolver => "револьвер",
        ItemType.BodyArmor => "бронежилет",
        ItemType.BallisticHelmet => "баллистический шлем",
        ItemType.RevolverRounds => "патроны для револьвера",
        ItemType.Handcannon => "ручная пушка",
        ItemType.MachinePistol => "автоматический пистолет",
        ItemType.Smg => "пистолет-пулемёт",
        ItemType.Shotgun => "дробовик",
        ItemType.AutoShotgun => "автоматический дробовик",
        ItemType.BoltRifle => "винтовка",
        ItemType.Hmg => "тяжёлый пулемёт",
        ItemType.HarpoonGun => "гарпунное ружьё",
        ItemType.HandcannonRounds => "патроны для ручной пушки",
        ItemType.SmgMagazine => "магазин пистолета-пулемёта",
        ItemType.ShotgunShells => "патроны для дробовика",
        ItemType.RifleRounds => "винтовочные патроны",
        ItemType.HmgMagazine => "короб пулемёта",
        ItemType.Harpoons => "гарпуны",
        ItemType.RevolverRoundsDepleted => "револьверные патроны с обеднённым сердечником",
        ItemType.SmgMagazineDepleted => "магазин ПП с обеднённым сердечником",
        ItemType.ShotgunShellsRubber => "патроны с резиновой дробью",
        ItemType.ShotgunSlugs => "разрывные пули для дробовика",
        ItemType.HarpoonsHeavy => "тяжёлые гарпуны",
        ItemType.HarpoonsExplosive => "разрывные гарпуны",
        _ => type.ToString(),
    };

    // Short 1-2 letter code for tight HUD slots/markers.
    public static string ShortLabel(ItemType type) =>
        ComponentDefinitions.ComponentKindFor(type) is { } kind ? ComponentDefinitions.ShortLabel(kind) : ShortLabelForBaseItem(type);

    private static string ShortLabelForBaseItem(ItemType type) => type switch
    {
        ItemType.Radio => "Рц",
        ItemType.AmmoCrate => "П",
        ItemType.Spacesuit => "С",
        ItemType.Wrench => "К",
        ItemType.Screwdriver => "О",
        ItemType.WeldingTool => "Св",
        ItemType.Cutter => "Рз",
        ItemType.Knife => "Нж",
        ItemType.Axe => "Тп",
        ItemType.GoshaScrewdriver => "ОП",
        ItemType.BeltBag => "Сум",
        ItemType.IdCard => "ID",
        ItemType.Rifle => "Ав",
        ItemType.LaserRifle => "ЛВ",
        ItemType.FuelRod => "Яд",
        ItemType.MedKit => "Ап",
        ItemType.Bandage => "Бн",
        ItemType.Tourniquet => "Жг",
        ItemType.Antiseptic => "Ас",
        ItemType.Splint => "Шн",
        ItemType.PlasterCast => "Гп",
        ItemType.Painkiller => "Об",
        ItemType.Antibiotic => "Аб",
        ItemType.BurnGel => "Гл",
        ItemType.Adrenaline => "Ад",
        ItemType.Defibrillator => "Дф",
        ItemType.SurgeryKit => "Хр",
        ItemType.WireSpool => "Пр",
        ItemType.Mineral => "Ру",
        ItemType.OxygenTank => "О2",
        ItemType.WeldingTank => "Сб",
        ItemType.IronOre => "Fe",
        ItemType.NickelOre => "Ni",
        ItemType.ZincOre => "Zn",
        ItemType.CopperOre => "Cu",
        ItemType.TitaniumOre => "Ti",
        ItemType.UraniumOre => "U",
        ItemType.Silicon => "Si",
        ItemType.Carbon => "C",
        ItemType.AluminumOre => "Al",
        ItemType.PlastalloyOre => "Пл.р",
        ItemType.SteelPlate => "СП",
        ItemType.TitaniumPlate => "ТП",
        ItemType.CopperCable => "МК",
        ItemType.Plastic => "Пк",
        ItemType.Plastalloy => "Пл",
        ItemType.Hyperium => "Hy",
        ItemType.RawIron => "Feр",
        ItemType.RawNickel => "Niр",
        ItemType.RawZinc => "Znр",
        ItemType.RawCopper => "Cuр",
        ItemType.RawTitanium => "Tiр",
        ItemType.RawUranium => "Uр",
        ItemType.RawSilicon => "Siр",
        ItemType.RawCarbon => "Cр",
        ItemType.RawAluminum => "Alр",
        ItemType.RawPlastalloy => "Плр",
        ItemType.RawHyperium => "Hyр",
        ItemType.HullPlating => "Об",
        ItemType.Magazine => "Ма",
        ItemType.Revolver => "Рв",
        ItemType.BodyArmor => "Бж",
        ItemType.BallisticHelmet => "Шл",
        ItemType.RevolverRounds => "Пт",
        ItemType.Handcannon => "РП",
        ItemType.MachinePistol => "АП",
        ItemType.Smg => "ПП",
        ItemType.Shotgun => "Др",
        ItemType.AutoShotgun => "АД",
        ItemType.BoltRifle => "Вн",
        ItemType.Hmg => "Пм",
        ItemType.HarpoonGun => "ГР",
        ItemType.HandcannonRounds => "Пп",
        ItemType.SmgMagazine => "Мп",
        ItemType.ShotgunShells => "Пд",
        ItemType.RifleRounds => "Вп",
        ItemType.HmgMagazine => "Кп",
        ItemType.Harpoons => "Га",
        ItemType.RevolverRoundsDepleted => "РО",
        ItemType.SmgMagazineDepleted => "МО",
        ItemType.ShotgunShellsRubber => "Рез",
        ItemType.ShotgunSlugs => "Пул",
        ItemType.HarpoonsHeavy => "ГТ",
        ItemType.HarpoonsExplosive => "ГР",
        _ => "?",
    };
}
