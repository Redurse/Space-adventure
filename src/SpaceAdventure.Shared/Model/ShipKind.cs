namespace SpaceAdventure.Shared.Model;

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
}
