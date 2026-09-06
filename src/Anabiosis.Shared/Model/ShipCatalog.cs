namespace Anabiosis.Shared.Model;

// Display metadata for the pre-game ship-select screen (Client's Game1) — kept separate from
// Ship itself since it's presentation, not simulation state.
public static class ShipCatalog
{
    public static string Name(ShipKind kind) => kind switch
    {
        ShipKind.Scout => "Разведчик",
        ShipKind.Cruiser => "Крейсер",
        ShipKind.Corvette => "Корвет",
        ShipKind.Destroyer => "Эсминец",
        ShipKind.Freighter => "Транспорт",
        ShipKind.Custom => "Свой корабль",
        _ => "Фрегат",
    };

    public static string Description(ShipKind kind) => kind switch
    {
        ShipKind.Scout => "Дёшево и слабо: 2 отсека, 1 орудие, только нож из личного оружия.",
        ShipKind.Cruiser => "Дорого и мощно: 7 отсеков, 3 орудия, второй склад боеприпасов.",
        ShipKind.Corvette => "Вертикальная компоновка: 6 отсеков, бортовой залп, 2 двигателя, 2 стыковочных порта.",
        ShipKind.Destroyer => "Боевой корабль: 9 отсеков, 2 турели, отдельный медотсек.",
        ShipKind.Freighter => "Рабочая лошадка: 9 отсеков, широкий реактор, больше места для экипажа.",
        ShipKind.Custom => "Нарисован вами в редакторе корабля.",
        _ => "Сбалансированный старт: 5 отсеков, 2 орудия.",
    };

    // Which way the bow points in the hull's own layout. Lives here rather than only inside the
    // Ship each class builds, because the renderer has to know where to put the nose and only ever
    // gets the ShipKind, not the Ship.
    public static float ForwardDegrees(ShipKind kind) => kind switch
    {
        ShipKind.Corvette => -90f, // laid out along its own axis: bow at the top of the plan
        _ => 0f, // a row of compartments, nose to the right
    };

    // What a hull costs at a station's Shipwright (game_design.md section 9 - "все классы
    // доступны с самого начала, но дороже/дешевле"). Trading in the current ship refunds a
    // fraction of its price, so switching classes always costs something.
    public const float TradeInFraction = 0.6f;

    public static int Price(ShipKind kind) => kind switch
    {
        ShipKind.Scout => 400,
        ShipKind.Cruiser => 1800,
        ShipKind.Corvette => 1400,
        ShipKind.Destroyer => 1600,
        ShipKind.Freighter => 1100,
        _ => 900, // Frigate, and Custom
    };

    public static int TradeInValue(ShipKind kind) => (int)(Price(kind) * TradeInFraction);

    // F = m*a (World.ShipField.cs's IntegrateShipFieldMotion): a fixed per-hull-kind constant, no
    // fuel/depletion involved (there is no tank to drain - thrust stays inexhaustible either way).
    // Dimensionless relative scale, not real kg - the thrust FORCE constants are tuned to match
    // this, not the other way round. Frigate = 1.0 is the baseline every existing thrust constant
    // was already tuned against before mass existed, so it stays exactly 1.0 - today's flat-constant
    // feel is preserved for that one hull, unchanged.
    public static float Mass(ShipKind kind) => kind switch
    {
        ShipKind.Scout => 0.6f,
        ShipKind.Corvette => 1.3f,
        ShipKind.Cruiser => 1.8f,
        ShipKind.Destroyer => 1.6f,
        ShipKind.Freighter => 1.4f,
        _ => 1.0f, // Frigate, and Custom - same catch-all Price/TradeInValue above already use
    };
}
