namespace Anabiosis.Shared.Model;

// The sets a player can start with (the 1/2/3 markers over the character in the lobby): items that are in the inventory the moment the character
// appears. Three per role, so a captain, an engineer and a guard each have a different pick; the first set of every role is the plain one.
public sealed record StartKit(string Name, IReadOnlyList<ItemType> Items);

public static class StartKits
{
    public const int PerRole = 3;

    private static readonly StartKit[] Captain =
    {
        new("Командный", new[] { ItemType.IdCard, ItemType.MedKit }),
        new("Дипломат", new[] { ItemType.IdCard, ItemType.Knife, ItemType.MedKit }),
        new("Запасливый", new[] { ItemType.MedKit, ItemType.MedKit, ItemType.OxygenTank }),
    };

    private static readonly StartKit[] Engineer =
    {
        new("Ремонтный", new[] { ItemType.Wrench, ItemType.Screwdriver, ItemType.WireSpool }),
        new("Сварщик", new[] { ItemType.WeldingTool, ItemType.WeldingTank, ItemType.Screwdriver }),
        new("Электрик", new[] { ItemType.Screwdriver, ItemType.WireSpool, ItemType.WireSpool, ItemType.Timer }),
    };

    private static readonly StartKit[] Mechanic =
    {
        new("Слесарный", new[] { ItemType.Wrench, ItemType.WeldingTool, ItemType.WeldingTank }),
        new("Резчик", new[] { ItemType.Cutter, ItemType.Wrench, ItemType.OxygenTank }),
        new("Универсал", new[] { ItemType.Wrench, ItemType.Screwdriver, ItemType.OxygenTank }),
    };

    private static readonly StartKit[] Security =
    {
        new("Патруль", new[] { ItemType.Knife, ItemType.Axe, ItemType.MedKit }),
        new("Лазерный", new[] { ItemType.LaserRifle, ItemType.MedKit }),
        new("Стрелок", new[] { ItemType.Rifle, ItemType.AmmoCrate, ItemType.MedKit }),
    };

    private static readonly StartKit[] Scientist =
    {
        new("Медик", new[] { ItemType.MedKit, ItemType.MedKit, ItemType.MedKit }),
        new("Полевой", new[] { ItemType.MedKit, ItemType.OxygenTank, ItemType.Screwdriver }),
        new("Исследователь", new[] { ItemType.MedKit, ItemType.OxygenTank, ItemType.OxygenTank }),
    };

    private static readonly StartKit[] NoRole =
    {
        new("Походный", new[] { ItemType.MedKit }),
        new("Рабочий", new[] { ItemType.Wrench, ItemType.Screwdriver }),
        new("Лёгкий", new[] { ItemType.Knife }),
    };

    public static IReadOnlyList<StartKit> For(CrewRole? role) => role switch
    {
        CrewRole.Captain => Captain,
        CrewRole.Engineer => Engineer,
        CrewRole.Mechanic => Mechanic,
        CrewRole.Security => Security,
        CrewRole.Scientist => Scientist,
        _ => NoRole,
    };

    // The kit for a role by index, wrapped into range (an index left over from another role still points at something).
    public static StartKit Get(CrewRole? role, int index)
    {
        var kits = For(role);
        return kits[((index % kits.Count) + kits.Count) % kits.Count];
    }
}
