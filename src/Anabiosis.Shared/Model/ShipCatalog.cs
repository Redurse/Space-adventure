namespace Anabiosis.Shared.Model;

// Display metadata for the ship the player is currently flying — kept separate from Ship itself
// since it's presentation, not simulation state. Direct user request ("удали все текущие корабли в
// разделе начать новую игру... полностью удалить из кода") - ShipKind is just Custom now (every
// hand-authored/catalog hull class is gone), so every method here collapses to one constant answer;
// kept as methods (not raw constants) only so every existing call site keeps compiling unchanged.
// Price/TradeInValue/TradeInFraction are gone entirely along with them - those only ever priced the
// now-deleted fixed hulls at a Shipwright (World.ShipState.cs's own doc comment on why that feature
// is gone too), and nothing reads them any more.
public static class ShipCatalog
{
    public static string Name(ShipKind kind) => "Свой корабль";

    public static string Description(ShipKind kind) => "Нарисован вами в редакторе корабля.";

    // Which way the bow points in the hull's own layout. Lives here rather than only inside Ship,
    // because the renderer has to know where to put the nose and only ever gets the ShipKind, not
    // the Ship. Every Custom hull (including the frozen default, ShipDefaultHull.cs) is a row of
    // compartments, nose to the right - CustomShipDefinition.ForwardDegrees carries a per-ship value
    // for the free-tile editor's own rotated designs, this is just the catalog-level fallback.
    public static float ForwardDegrees(ShipKind kind) => 0f;

    // F = m*a (World.ShipField.cs's IntegrateShipFieldMotion): a fixed per-hull-kind constant, no
    // fuel/depletion involved (there is no tank to drain - thrust stays inexhaustible either way).
    // Dimensionless relative scale, not real kg. 1.0 is the baseline every existing thrust constant
    // was already tuned against (back when this was Frigate's own mass) - kept exactly 1.0 so
    // today's flat-constant feel is unchanged now that it's the only mass any hull has.
    public static float Mass(ShipKind kind) => 1.0f;
}
