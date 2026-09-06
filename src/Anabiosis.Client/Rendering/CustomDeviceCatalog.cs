using Microsoft.Xna.Framework;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

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
        CustomDeviceKind.StorageRack, CustomDeviceKind.CardTable, CustomDeviceKind.Jukebox, CustomDeviceKind.Terminal, CustomDeviceKind.Junction,
        CustomDeviceKind.Battery,
    };

    // At most one of these may exist in a definition - placing a new one silently replaces the old
    // (CustomShipValidator would only reject the duplicate anyway; the editor just never creates one).
    public static bool IsSingleton(CustomDeviceKind kind) => kind is
        CustomDeviceKind.Reactor or CustomDeviceKind.Distribution or CustomDeviceKind.Helm
        or CustomDeviceKind.Navigation or CustomDeviceKind.CardTable or CustomDeviceKind.Jukebox or CustomDeviceKind.Terminal;

    public static string Name(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Reactor => "Реактор",
        CustomDeviceKind.Distribution => "Распределитель",
        // Renamed by direct user request - Helm/Navigation's OLD names ("Штурвал"/"Навигация") read
        // backwards next to what each console actually does in-game: the Helm is what you actually
        // navigate/fly with, while the "Navigation" console is the one World.Scanner.cs treats as
        // the scanner interaction point. Same physical fixtures/mechanics, only the label changes -
        // also renamed in EngineerDevicePanel.cs and ShipRenderer.cs's own on-device label.
        CustomDeviceKind.Helm => "Навигационная панель",
        CustomDeviceKind.Navigation => "Сканер",
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
        CustomDeviceKind.Jukebox => "Музыкальный автомат",
        CustomDeviceKind.Terminal => "Терминал",
        CustomDeviceKind.TurretMachineGun => "Турель (пулемёт)",
        CustomDeviceKind.Camera => "Камера",
        CustomDeviceKind.ComponentMount => "Крепление модуля",
        // Renamed by direct user request - same "короб" fixture, new label.
        CustomDeviceKind.Junction => "Щиток",
        CustomDeviceKind.Battery => "Батарея",
        CustomDeviceKind.EngineSmall => "Двигатель малый",
        CustomDeviceKind.EngineMedium => "Двигатель средний",
        CustomDeviceKind.EngineLarge => "Двигатель большой",
        CustomDeviceKind.WarpEngine => "Варп двигатель",
        CustomDeviceKind.ShuttleHangar => "Ангар шаттла",
        CustomDeviceKind.DroneHangar => "Ангар дронов",
        CustomDeviceKind.SmallStorage => "Малое хранилище",
        CustomDeviceKind.LargeStorage => "Большое хранилище",
        CustomDeviceKind.Morgue => "Морг",
        CustomDeviceKind.FuelRodStorage => "Хранилище для ядерных стержней",
        CustomDeviceKind.ConstructionBench => "Строительный станок",
        CustomDeviceKind.Fabricator => "Фабрикатор",
        CustomDeviceKind.Deconstructor => "Деконструктор",
        CustomDeviceKind.WeaponWorkbench => "Оружейный верстак",
        CustomDeviceKind.PowerConduit => "Провод",
        CustomDeviceKind.Table => "Стол",
        CustomDeviceKind.Chair => "Стул",
        CustomDeviceKind.Sofa => "Диван",
        CustomDeviceKind.Bed => "Кровать",
        CustomDeviceKind.Nightstand => "Тумбочка",
        CustomDeviceKind.WallLamp => "Настенная лампа",
        CustomDeviceKind.Spotlight => "Прожектор",
        CustomDeviceKind.Lamp => "Лампа",
        CustomDeviceKind.DecorativePlant => "Декоративное растение",
        CustomDeviceKind.DefensiveTurret => "Оборонительная турель",
        CustomDeviceKind.ShieldGeneratorSmall => "Малый генератор щита",
        CustomDeviceKind.ShieldGeneratorLarge => "Большой генератор щита",
        CustomDeviceKind.WeaponPanel => "Оружейная панель",
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
        CustomDeviceKind.Jukebox => "J",
        CustomDeviceKind.Terminal => "Q",
        CustomDeviceKind.TurretMachineGun => "M",
        CustomDeviceKind.Camera => "V",
        CustomDeviceKind.ComponentMount => "P",
        CustomDeviceKind.Junction => "B",
        CustomDeviceKind.Battery => "Y",
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
        CustomDeviceKind.Jukebox => new Color(224, 196, 120),
        CustomDeviceKind.Terminal => new Color(70, 190, 210),
        CustomDeviceKind.TurretMachineGun => new Color(200, 90, 90),
        CustomDeviceKind.Camera => new Color(140, 170, 200),
        CustomDeviceKind.ComponentMount => new Color(170, 170, 170),
        CustomDeviceKind.Junction => new Color(210, 200, 80),
        CustomDeviceKind.Battery => new Color(90, 200, 255),
        CustomDeviceKind.EngineSmall => new Color(230, 140, 40),
        CustomDeviceKind.EngineMedium => new Color(230, 140, 40),
        CustomDeviceKind.EngineLarge => new Color(230, 140, 40),
        CustomDeviceKind.WarpEngine => new Color(150, 90, 230),
        CustomDeviceKind.ShuttleHangar => new Color(150, 150, 180),
        CustomDeviceKind.DroneHangar => new Color(150, 150, 180),
        CustomDeviceKind.SmallStorage => new Color(160, 140, 110),
        CustomDeviceKind.LargeStorage => new Color(160, 140, 110),
        CustomDeviceKind.Morgue => new Color(150, 180, 170),
        CustomDeviceKind.FuelRodStorage => new Color(180, 210, 60),
        CustomDeviceKind.ConstructionBench => new Color(100, 180, 180),
        CustomDeviceKind.Fabricator => new Color(100, 180, 180),
        CustomDeviceKind.Deconstructor => new Color(100, 180, 180),
        CustomDeviceKind.WeaponWorkbench => new Color(100, 180, 180),
        CustomDeviceKind.PowerConduit => new Color(200, 140, 60),
        CustomDeviceKind.Table => new Color(180, 160, 130),
        CustomDeviceKind.Chair => new Color(180, 160, 130),
        CustomDeviceKind.Sofa => new Color(180, 160, 130),
        CustomDeviceKind.Bed => new Color(180, 160, 130),
        CustomDeviceKind.Nightstand => new Color(180, 160, 130),
        CustomDeviceKind.WallLamp => new Color(220, 200, 140),
        CustomDeviceKind.Spotlight => new Color(220, 200, 140),
        CustomDeviceKind.Lamp => new Color(220, 200, 140),
        CustomDeviceKind.DecorativePlant => new Color(120, 180, 110),
        CustomDeviceKind.DefensiveTurret => new Color(190, 60, 60),
        CustomDeviceKind.ShieldGeneratorSmall => new Color(80, 200, 190),
        CustomDeviceKind.ShieldGeneratorLarge => new Color(80, 200, 190),
        CustomDeviceKind.WeaponPanel => new Color(210, 80, 200),
        _ => Color.White,
    };
}
