using Microsoft.Xna.Framework;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// Display metadata for the Ship Editor's device palette - kept separate from the CustomDeviceKind
// enum itself the same way ShipCatalog keeps a fixed hull's display strings out of Ship.
public static class CustomDeviceCatalog
{
    // Every kind the palette can place, in the order it's listed - required-exactly-one systems
    // first, then required-at-least-one, then optional flavor/combat/cargo.
    public static readonly CustomDeviceKind[] All =
    {
        CustomDeviceKind.Reactor, CustomDeviceKind.Distribution, CustomDeviceKind.Helm, CustomDeviceKind.Navigation,
        CustomDeviceKind.Engine, CustomDeviceKind.SuitLocker,
        CustomDeviceKind.Shields, CustomDeviceKind.WeaponCharger, CustomDeviceKind.Oxygen, CustomDeviceKind.Secondary,
        CustomDeviceKind.TurretBallistic, CustomDeviceKind.TurretLaser, CustomDeviceKind.AmmoStorage,
        CustomDeviceKind.StorageRack, CustomDeviceKind.CardTable,
    };

    // At most one of these may exist in a definition - placing a new one silently replaces the old
    // (CustomShipValidator would only reject the duplicate anyway; the editor just never creates one).
    public static bool IsSingleton(CustomDeviceKind kind) => kind is
        CustomDeviceKind.Reactor or CustomDeviceKind.Distribution or CustomDeviceKind.Helm
        or CustomDeviceKind.Navigation or CustomDeviceKind.CardTable;

    public static string Name(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Reactor => "Реактор",
        CustomDeviceKind.Distribution => "Распределитель",
        CustomDeviceKind.Helm => "Штурвал",
        CustomDeviceKind.Navigation => "Навигация",
        CustomDeviceKind.Engine => "Двигатель",
        CustomDeviceKind.Shields => "Генератор щита",
        CustomDeviceKind.WeaponCharger => "Зарядка оружия",
        CustomDeviceKind.Oxygen => "Кислород",
        CustomDeviceKind.Secondary => "Доп. система",
        CustomDeviceKind.TurretBallistic => "Турель (баллист.)",
        CustomDeviceKind.TurretLaser => "Турель (лазер)",
        CustomDeviceKind.AmmoStorage => "Склад патронов",
        CustomDeviceKind.SuitLocker => "Шкаф скафандра",
        CustomDeviceKind.StorageRack => "Стеллаж",
        CustomDeviceKind.CardTable => "Карточный стол",
        _ => kind.ToString(),
    };

    public static string ShortGlyph(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Reactor => "R",
        CustomDeviceKind.Distribution => "D",
        CustomDeviceKind.Helm => "H",
        CustomDeviceKind.Navigation => "N",
        CustomDeviceKind.Engine => "E",
        CustomDeviceKind.Shields => "S",
        CustomDeviceKind.WeaponCharger => "W",
        CustomDeviceKind.Oxygen => "O",
        CustomDeviceKind.Secondary => "X",
        CustomDeviceKind.TurretBallistic => "T",
        CustomDeviceKind.TurretLaser => "L",
        CustomDeviceKind.AmmoStorage => "A",
        CustomDeviceKind.SuitLocker => "U",
        CustomDeviceKind.StorageRack => "C",
        CustomDeviceKind.CardTable => "K",
        _ => "?",
    };

    public static Color Tint(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Reactor => new Color(220, 90, 60),
        CustomDeviceKind.Distribution => new Color(220, 160, 60),
        CustomDeviceKind.Helm => new Color(90, 160, 230),
        CustomDeviceKind.Navigation => new Color(90, 200, 220),
        CustomDeviceKind.Engine => new Color(230, 140, 40),
        CustomDeviceKind.Shields => new Color(80, 200, 190),
        CustomDeviceKind.WeaponCharger => new Color(210, 80, 200),
        CustomDeviceKind.Oxygen => new Color(120, 210, 120),
        CustomDeviceKind.Secondary => new Color(180, 180, 90),
        CustomDeviceKind.TurretBallistic => new Color(190, 60, 60),
        CustomDeviceKind.TurretLaser => new Color(230, 60, 160),
        CustomDeviceKind.AmmoStorage => new Color(150, 120, 80),
        CustomDeviceKind.SuitLocker => new Color(90, 130, 210),
        CustomDeviceKind.StorageRack => new Color(160, 140, 110),
        CustomDeviceKind.CardTable => new Color(140, 100, 190),
        _ => Color.White,
    };
}
