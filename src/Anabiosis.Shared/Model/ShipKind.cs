namespace Anabiosis.Shared.Model;

// Selectable ship classes (game_design.md section 9 - "несколько классов кораблей... своя
// фиксированная планировка отсеков, разное количество систем/орудий"). Deliberately NOT wired to
// money/reputation yet (design doc's "все классы доступны с самого начала, но дороже" purchase
// gating is a separate, not-yet-built milestone) - for now this only drives which fixed layout
// World starts with, picked once at the pre-game ship-select screen.
public enum ShipKind
{
    Scout,
    Frigate, // the original M2 starter layout (Ship.CreateStarter) - kept as the default/mid tier
    Cruiser,
    Corvette, // laid out along its own axis instead of as a row of boxes (Ship.Corvette.cs)
    // M85 follow-up - two more hand-authored starter hulls, this time built from the M80 compartment
    // catalog (CompartmentCatalog.cs/CompartmentPlacer.cs) stamped onto a plain TileGrid and run
    // through TileShipBuilder/Ship.FromCustomDefinition (Ship.cs's Create), rather than a from-scratch
    // Room-rectangle literal the way Scout/Frigate/Cruiser/Corvette are. Genuinely additional classes
    // (items 5-6 at the ship-select screen, Game1.Menu.cs's SelectableShipKinds) - none of the 4
    // above are touched by this.
    Destroyer, // a 2D spine-plus-branches combat hull: 2 turrets, reactor, life support, medical bay
    Freighter, // same spine-plus-branches shape, wider reactor/engineering focus, more crew space
    Custom, // player-drawn in the Ship Editor (Ship.Custom.cs) - not sold at any Shipwright
}
