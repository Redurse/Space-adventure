using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// Flavor/explanation text for the inventory tooltip (InventoryPanel.DrawTooltip) - purely a HUD
// label, no simulation meaning, so it lives here rather than in Shared next to the real item data.
public static class ItemDescriptions
{
    public static string? Describe(ItemType type) => type switch
    {
        ItemType.AmmoCrate => "Патроны для орудия. Несите к турели и загрузите магазин.",
        ItemType.Spacesuit => "Защищает от вакуума, пока в баллоне на спине есть кислород.",
        ItemType.Wrench => "Чинит повреждённые щитки, турели и системы корабля.",
        ItemType.Screwdriver => "Открывает панель проводки щитка вместо обычного статуса.",
        ItemType.Axe => "Вышибает запертую или повреждённую дверь за пару ударов. В ближнем бою тоже годится.",
        ItemType.GoshaScrewdriver => "ЛКМ по прибору ломает его вместо ремонта. Починить ею нельзя ничего.",
        ItemType.WeldingTool => "Заваривает пробоины в корпусе, пока в баллоне есть топливо.",
        ItemType.Cutter => "Режет руду в поясе астероидов, пока в баллоне есть кислород.",
        ItemType.Knife => "Оружие ближнего боя, не требует патронов.",
        ItemType.Rifle => "Автоматическое оружие, расходует патроны.",
        ItemType.LaserRifle => "Лазерное оружие, расходует заряд аккумулятора.",
        ItemType.FuelRod => "Топливный стержень для реактора.",
        ItemType.MedKit => "Восстанавливает здоровье при использовании.",
        ItemType.WireSpool => "Провод для прокладки проводки между щитками и компонентами.",
        ItemType.Mineral => "Добытая руда. Продаётся торговцу на станции.",
        ItemType.OxygenTank => "Кислород для скафандра или резака.",
        ItemType.WeldingTank => "Топливо для сварочного аппарата.",
        ItemType.IronOre => "Добытая железная руда. Продаётся торговцу или идёт на стальную пластину.",
        ItemType.NickelOre => "Добытая никелевая руда. Продаётся торговцу на станции.",
        ItemType.ZincOre => "Добытая цинковая руда. Продаётся торговцу на станции.",
        ItemType.CopperOre => "Добытая медная руда. Продаётся торговцу или идёт на медный кабель.",
        ItemType.TitaniumOre => "Добытая титановая руда. Продаётся торговцу или идёт на титановую пластину.",
        ItemType.UraniumOre => "Добытая урановая руда. Слабо фонит. Продаётся торговцу на станции.",
        ItemType.Silicon => "Добытый кремний. Продаётся торговцу или идёт на пластик.",
        ItemType.Carbon => "Добытый углерод. Продаётся торговцу или идёт на пластик.",
        ItemType.AluminumOre => "Добытая алюминиевая руда. Продаётся торговцу или идёт на пластины и сплавы.",
        ItemType.PlastalloyOre => "Добытая руда пластали. Продаётся торговцу или идёт на слиток пластали.",
        ItemType.SteelPlate => "Стальная пластина. Крафтится в фабрикаторе из железной и алюминиевой руды.",
        ItemType.TitaniumPlate => "Титановая пластина. Крафтится в фабрикаторе из титановой руды и стальной пластины.",
        ItemType.CopperCable => "Медный кабель. Крафтится в фабрикаторе из медной руды.",
        ItemType.Plastic => "Пластик. Крафтится в фабрикаторе из углерода и кремния.",
        ItemType.Plastalloy => "Слиток пластали. Крафтится в фабрикаторе из руды пластали и алюминиевой руды.",
        _ => ComponentDefinitions.ComponentKindFor(type) is not null
            ? "Электронный компонент. Устанавливается в свободное гнездо и подключается проводом."
            : null,
    };
}
