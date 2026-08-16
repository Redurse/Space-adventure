namespace SpaceAdventure.Shared.Model;

// Display metadata for the pre-game ship-select screen (Client's Game1) — kept separate from
// Ship itself since it's presentation, not simulation state.
public static class ShipCatalog
{
    public static string Name(ShipKind kind) => kind switch
    {
        ShipKind.Scout => "Разведчик",
        ShipKind.Cruiser => "Крейсер",
        ShipKind.Corvette => "Корвет",
        _ => "Фрегат",
    };

    public static string Description(ShipKind kind) => kind switch
    {
        ShipKind.Scout => "Дёшево и слабо: 2 отсека, 1 орудие, только нож из личного оружия.",
        ShipKind.Cruiser => "Дорого и мощно: 7 отсеков, 3 орудия, второй склад боеприпасов.",
        ShipKind.Corvette => "Вертикальная компоновка: 6 отсеков, бортовой залп, 2 двигателя, 2 стыковочных порта.",
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
        _ => 900, // Frigate
    };

    public static int TradeInValue(ShipKind kind) => (int)(Price(kind) * TradeInFraction);
}
